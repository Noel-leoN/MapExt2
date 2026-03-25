// Game.Simulation.FindJobSystem
// v1.4.2无变化

using Colossal.Collections;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Debug;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace MapExtPDX.ModeB
{
    /// <summary>
    /// 为无业和寻求更好工作的市民寻找工作岗位。
    /// 系统会分两步执行：
    /// 1. 为所有符合条件的无业市民寻找工作。
    /// 2. 为一小部分已就业但学历高于当前职位的市民寻找更好的工作。
    /// </summary>

    // =========================================================================================
    // 1. Mod 自定义系统类型 (当前类)
    using ModSystem = FindJobSystemMod;
    // 2. 原版系统类型 (用于禁用和定位)
    using TargetSystem = FindJobSystem;
    // =========================================================================================

    public partial class FindJobSystemMod : GameSystemBase
    {
        #region Constants
        private const int UPDATE_INTERVAL = 16; // 系统更新频率（每16帧一次）
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UPDATE_INTERVAL;

        // [优化] 每帧最大处理寻路请求数，防止PathfindQueue溢出
        private const int MAX_PATHFIND_REQUESTS_PER_UPDATE = 2000;
        // [优化] 请求计数器/限流计数
        private NativeArray<int> m_RequestCount;

#if DEBUG
        // Debug 索引定义
        private const int IDX_TotalSeekers = 0;       // 处理的总实体数
        private const int IDX_Throttled = 1;          // 被限流跳过的
                                                      // 失业者 (Unemployed)
        private const int IDX_Unemp_Sent = 2;         // 发起寻路
        private const int IDX_Unemp_NoVacancy = 3;    // 全城无空缺直接放弃
        private const int IDX_Unemp_Success = 4;      // 入职成功
        private const int IDX_Unemp_Fail_Path = 5;    // 寻路失败/无路径
        private const int IDX_Unemp_Fail_Taken = 6;   // 岗位被抢
                                                      // 跳槽者 (Switcher)
        private const int IDX_Swit_Sent = 7;          // 发起寻路
        private const int IDX_Swit_GiveUp = 8;        // 竞争太大主动放弃
        private const int IDX_Swit_Success = 9;       // 跳槽成功
        private const int IDX_Swit_Fail_Path = 10;    // 寻路失败/无路径
        private const int IDX_Swit_Fail_Taken = 11;   // 岗位被抢

        private const int DEBUG_ARRAY_SIZE = 12;
        // 用于在 Job 和主线程间传递统计数据
        private NativeArray<int> m_DebugStats;       // Debug统计数组
        private bool m_EnableDebug = false; // 开发时设为 true，发布设为 false
#endif

        #endregion

        #region Entity Queries
        private EntityQuery m_JobSeekerQuery; // 查询正在等待寻路的Seeker
        private EntityQuery m_ResultsQuery;   // 查询寻路完成的Seeker
        private EntityQuery m_FreeQuery;      // 查询有空位的工作场所
        #endregion

        #region System Dependencies
        private SimulationSystem m_SimulationSystem;
        private TriggerSystem m_TriggerSystem;
        private CountHouseholdDataSystem m_CountHouseholdDataSystem;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private EndFrameBarrier m_EndFrameBarrier;
        #endregion

        #region Native Containers
        private NativeArray<int> m_FreeCache;  // 全局空位缓存 (Level 0-4)

        [DebugWatchValue]
        private NativeValue<int> m_StartedWorking; // 累计入职总数 (用于Debug面板监视)

        [DebugWatchDeps]
        private JobHandle m_WriteDeps;

#if DEBUG
        // 用于监视总入职数
        private NativeValue<int> m_GlobalHiredCounter;
#endif
        #endregion

        #region System Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();

            // 1.禁用原版系统并获取原版系统引用
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系统
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                //#if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
                //#endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加载
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系统(尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 获取系统引用
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_TriggerSystem = World.GetOrCreateSystemManaged<TriggerSystem>();
            m_CountHouseholdDataSystem = World.GetOrCreateSystemManaged<CountHouseholdDataSystem>();

            // 创建查询
            // m_FreeQuery: 查找空闲工作场所
            m_FreeQuery = SystemAPI.QueryBuilder()
                .WithAll<FreeWorkplaces>()
                .WithNone<Destroyed, Temp, Deleted>()
                .Build();

            // m_JobSeekerQuery: 查找正在找工作的实体，CitizenFindJobSystem 创建且尚未开始寻路
            m_JobSeekerQuery = SystemAPI.QueryBuilder()
                .WithAll<JobSeeker, Owner>()
                .WithNone<PathInformation, Deleted>()
                .Build();

            // m_ResultsQuery: 查找已经找到工作（有路径信息）的实体
            m_ResultsQuery = SystemAPI.QueryBuilder()
                .WithAll<JobSeeker, Owner, PathInformation>()
                .WithNone<Deleted>()
                .Build();

            // 初始化 Native 容器
            m_StartedWorking = new NativeValue<int>(Allocator.Persistent);
            m_FreeCache = new NativeArray<int>(5, Allocator.Persistent);
            m_RequestCount = new NativeArray<int>(1, Allocator.Persistent); // 长度1的数组

            RequireAnyForUpdate(m_JobSeekerQuery, m_ResultsQuery);

#if DEBUG
            m_DebugStats = new NativeArray<int>(DEBUG_ARRAY_SIZE, Allocator.Persistent);
            m_GlobalHiredCounter = new NativeValue<int>(Allocator.Persistent);
#endif
        }

        protected override void OnDestroy()
        {
            if (m_StartedWorking.IsCreated) m_StartedWorking.Dispose();
            if (m_FreeCache.IsCreated) m_FreeCache.Dispose();
            if (m_RequestCount.IsCreated) m_RequestCount.Dispose();
#if DEBUG
            if (m_DebugStats.IsCreated) m_DebugStats.Dispose();
            if (m_GlobalHiredCounter.IsCreated) m_GlobalHiredCounter.Dispose();
#endif
            base.OnDestroy();
        }
        #endregion

        #region System Update
        protected override void OnUpdate()
        {
            // --- Debug 输出逻辑 ---
#if DEBUG
            if (m_EnableDebug == true)
            {
                PrintDebugLog();
            }

            // 清空每一帧的 Debug 计数器 (但保留全局累计的 m_GlobalHiredCounter)
            Dependency = new ClearDebugStatsJob { m_Stats = m_DebugStats, m_ReqCount = m_RequestCount }.Schedule(Dependency);
#endif

            // 依赖链管理
            //JobHandle deps = Dependency;

            // --- 阶段 1: 处理新请求 (JobSeeker -> Pathfind Queue) ---
            if (!m_JobSeekerQuery.IsEmptyIgnoreFilter && !m_CountHouseholdDataSystem.IsCountDataNotReady())
            {
                ProcessJobSeekers();
            }

            // --- 阶段 2: 处理寻路结果 (Pathfind Result -> Hired) ---
            if (!m_ResultsQuery.IsEmptyIgnoreFilter)
            {
                ProcessJobResults();
            }
        }
        #endregion

        #region Job Processing Methods
        /// <summary>
        /// 处理求职者查找工作的逻辑
        /// </summary>
        private void ProcessJobSeekers()
        {
            // 1. [优化] 重置缓存和计数器
            m_RequestCount[0] = 0;
            JobHandle resetCacheJob = new ResetCacheJob { m_FreeCache = m_FreeCache }.Schedule(Dependency);

            // 2. [优化] 并行计算 Free Workplaces。原系统使用 ToComponentDataListAsync 拷贝全量数据，非常慢。
            // 现在直接使用 IJobChunk 在 ComponentData 上进行并行规约。
            var calculateFreeJob = new CountFreeWorkplacesJob
            {
                m_FreeWorkplacesType = SystemAPI.GetComponentTypeHandle<FreeWorkplaces>(true),
                m_FreeCache = m_FreeCache
            }.ScheduleParallel(m_FreeQuery, resetCacheJob);

            // 查找工作任务
            var findJobJob = new FindJobJob
            {
                // Entity 和组件类型句柄
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_JobSeekerType = SystemAPI.GetComponentTypeHandle<JobSeeker>(false),
                m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(false),
                m_CurrentBuildingType = SystemAPI.GetComponentTypeHandle<CurrentBuilding>(true),

                // 组件查找
                m_HouseholdMembers = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                m_CitizenDatas = SystemAPI.GetComponentLookup<Citizen>(true),
                m_Workers = SystemAPI.GetComponentLookup<Worker>(true),
                m_Households = SystemAPI.GetComponentLookup<Household>(true),
                m_HomelessHouseholds = SystemAPI.GetComponentLookup<HomelessHousehold>(true),
                m_OutsideConnections = SystemAPI.GetComponentLookup<Game.Objects.OutsideConnection>(true),
                m_Deleteds = SystemAPI.GetComponentLookup<Deleted>(true),

                // 缓冲区查找
                m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(true),
                m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(true),

                // 系统数据
                m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter(),
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                m_FreeCache = m_FreeCache,
                m_EmployableByEducation = m_CountHouseholdDataSystem.GetEmployables(),
                m_RandomSeed = RandomSeed.Next(),
                m_DynamicFindJobMaxCost = Mod.Instance.Settings.FindJobMaxCost,

                // [优化] 传入计数器进行限流
                m_RequestCount = m_RequestCount,
                m_MaxRequests = MAX_PATHFIND_REQUESTS_PER_UPDATE,
#if DEBUG
                m_DebugStats = m_DebugStats
#endif
            };

            Dependency = findJobJob.ScheduleParallel(
                m_JobSeekerQuery,
                JobHandle.CombineDependencies(calculateFreeJob, Dependency)
            );

            m_PathfindSetupSystem.AddQueueWriter(Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

        /// <summary>
        /// 处理已找到工作的求职者，让他们开始工作
        /// </summary>
        private void ProcessJobResults()
        {
            //JobHandle chunksJobHandle;
            var startWorkingJob = new StartWorkingJob
            {
                // 查询结果
                //m_Chunks = m_ResultsQuery.ToArchetypeChunkListAsync(                    World.UpdateAllocator.ToAllocator,                    out chunksJobHandle                ),

                // 类型句柄
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_JobSeekerType = SystemAPI.GetComponentTypeHandle<JobSeeker>(true),
                m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(true),
                m_PathInfoType = SystemAPI.GetComponentTypeHandle<PathInformation>(true),

                // 组件查找
                m_Citizens = SystemAPI.GetComponentLookup<Citizen>(true),
                m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_EmployeeBuffers = SystemAPI.GetBufferLookup<Employee>(false),
                m_FreeWorkplaces = SystemAPI.GetComponentLookup<FreeWorkplaces>(false),
                m_WorkplaceDatas = SystemAPI.GetComponentLookup<WorkplaceData>(true),
                m_Deleteds = SystemAPI.GetComponentLookup<Deleted>(true),
                m_Workers = SystemAPI.GetComponentLookup<Worker>(true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                m_SpawnableBuildings = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true),
                m_WorkProviders = SystemAPI.GetComponentLookup<WorkProvider>(true),

                // 系统数据
                m_TriggerBuffer = m_TriggerSystem.CreateActionBuffer(),
                m_SimulationFrame = m_SimulationSystem.frameIndex,
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer(),
                m_StartedWorking = m_StartedWorking,

#if DEBUG
                m_GlobalHiredCounter = m_GlobalHiredCounter,
                m_DebugStats = m_DebugStats
#endif
            };

            // ⚠️ 串行调度 (.Schedule)：该 Job 直接写入 m_EmployeeBuffers / m_FreeWorkplaces 等共享组件，
            // 且使用非 ParallelWriter 的 EntityCommandBuffer。 禁止改为 .ScheduleParallel()，否则将引发竞态条件。
            Dependency = startWorkingJob.Schedule(m_ResultsQuery, Dependency);

            m_TriggerSystem.AddActionBufferWriter(Dependency);
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
            m_WriteDeps = JobHandle.CombineDependencies(Dependency, m_WriteDeps);
        }
        #endregion

#if DEBUG
        private void PrintDebugLog()
        {
            // 需要先完成任务才能读取 NativeArray
            Dependency.Complete();

            int total = m_DebugStats[IDX_TotalSeekers];
            if (total == 0) return;

            // 构建日志
            var log = new System.Text.StringBuilder();
            log.AppendLine($"[FindJobSystem] Frame {m_SimulationSystem.frameIndex} | Processed: {total} | Throttled: {m_DebugStats[IDX_Throttled]}");

            // 失业者数据
            int unempSent = m_DebugStats[IDX_Unemp_Sent];
            if (unempSent > 0 || m_DebugStats[IDX_Unemp_NoVacancy] > 0)
            {
                log.AppendLine($"  > Unemployed: Sent={unempSent}, NoVacancySkip={m_DebugStats[IDX_Unemp_NoVacancy]}, " +
                               $"Hired={m_DebugStats[IDX_Unemp_Success]}, Fail(Taken)={m_DebugStats[IDX_Unemp_Fail_Taken]}, Fail(Path)={m_DebugStats[IDX_Unemp_Fail_Path]}");
            }

            // 跳槽者数据
            int switSent = m_DebugStats[IDX_Swit_Sent];
            if (switSent > 0 || m_DebugStats[IDX_Swit_GiveUp] > 0)
            {
                log.AppendLine($"  > Switchers : Sent={switSent}, GiveUp(RNG)={m_DebugStats[IDX_Swit_GiveUp]}, " +
                               $"Switched={m_DebugStats[IDX_Swit_Success]}, Fail(Taken)={m_DebugStats[IDX_Swit_Fail_Taken]}, Fail(Path)={m_DebugStats[IDX_Swit_Fail_Path]}");
            }

            log.Append($"  > Global Hired Total: {m_GlobalHiredCounter.value}");

            Mod.Debug(log.ToString());
        }
#endif

        [BurstCompile]
        private struct ClearDebugStatsJob : IJob
        {
            public NativeArray<int> m_Stats;
            public NativeArray<int> m_ReqCount;
            public void Execute()
            {
                for (int i = 0; i < m_Stats.Length; i++) m_Stats[i] = 0;
                m_ReqCount[0] = 0;
            }
        }

        #region Optimized Jobs
        // [优化] 使用 IJob 清理缓存，无需复制
        [BurstCompile]
        private struct ResetCacheJob : IJob
        {
            public NativeArray<int> m_FreeCache;
            public void Execute()
            {
                for (int i = 0; i < m_FreeCache.Length; i++) m_FreeCache[i] = 0;
            }
        }
        #endregion

        #region JobStruct CalculateFreeWorkplaceJob
        // [优化] 使用 IJobChunk 并行计算，使用原子操作累加。避免了将几十万组件复制到 List 的开销。
        [BurstCompile]
        private struct CountFreeWorkplacesJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<FreeWorkplaces> m_FreeWorkplacesType;

            // 使用原子操作进行并行写入
            [NativeDisableUnsafePtrRestriction]
            public NativeArray<int> m_FreeCache;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)

            {
                NativeArray<FreeWorkplaces> freeWorkplaces = chunk.GetNativeArray(ref m_FreeWorkplacesType);

                // 本地缓存减少原子操作次数
                int c0 = 0, c1 = 0, c2 = 0, c3 = 0, c4 = 0;

                for (int i = 0; i < freeWorkplaces.Length; i++)
                {
                    FreeWorkplaces fw = freeWorkplaces[i];
                    c0 += fw.m_Uneducated;
                    c1 += fw.m_PoorlyEducated;
                    c2 += fw.m_Educated;
                    c3 += fw.m_WellEducated;
                    c4 += fw.m_HighlyEducated;
                }

                unsafe
                {
                    // 获取数组首地址指针
                    int* ptr = (int*)m_FreeCache.GetUnsafePtr();

                    // [需求3] 修复 CS1061: 使用指针索引进行 Interlocked.Add
                    if (c0 > 0) System.Threading.Interlocked.Add(ref ptr[0], c0);
                    if (c1 > 0) System.Threading.Interlocked.Add(ref ptr[1], c1);
                    if (c2 > 0) System.Threading.Interlocked.Add(ref ptr[2], c2);
                    if (c3 > 0) System.Threading.Interlocked.Add(ref ptr[3], c3);
                    if (c4 > 0) System.Threading.Interlocked.Add(ref ptr[4], c4);
                }
            }
        }

        #endregion

        #region JobStruct FindJobJob
        /// <summary>
        /// 寻职任务：负责为“求职者(JobSeeker)”实体寻找潜在的工作地点。
        /// 它会根据教育水平、当前就业状态和全城空缺职位情况，决定是否发起寻路请求。
        /// </summary>
        [BurstCompile]
        private struct FindJobJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType; // 实体类型句柄
            [ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType; // 所有者组件（通常指向Citizen实体）
            [ReadOnly] public ComponentTypeHandle<JobSeeker> m_JobSeekerType; // 求职者数据组件
            [ReadOnly] public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType; // 当前所在建筑组件
            [ReadOnly] public ComponentLookup<HouseholdMember> m_HouseholdMembers; // 家庭成员查询
            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters; // 租户查询
            [ReadOnly] public ComponentLookup<Citizen> m_CitizenDatas; // 市民数据查询
            [ReadOnly] public ComponentLookup<Worker> m_Workers; // 工人数据查询（如果已有工作）
            [ReadOnly] public ComponentLookup<Household> m_Households; // 家庭数据查询
            [ReadOnly] public ComponentLookup<HomelessHousehold> m_HomelessHouseholds; // 无家可归家庭查询
            [ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections; // 外部连接查询
            [ReadOnly] public ComponentLookup<Deleted> m_Deleteds; // 已删除标记查询
            [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens; // 家庭市民缓冲区
            [ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles; // 拥有车辆缓冲区

            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue; // 寻路队列写入器
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer; // 命令缓冲区（用于添加/删除组件）

            [ReadOnly] public NativeArray<int> m_FreeCache; // 全局空缺职位缓存（按等级索引）

            [ReadOnly] public NativeArray<int> m_EmployableByEducation; // 按教育程度统计的待业人数

            public RandomSeed m_RandomSeed; // 随机种子

            public float m_DynamicFindJobMaxCost; // 动态最大寻路成本

            // [优化] 限流参数
            [NativeDisableUnsafePtrRestriction]
            public NativeArray<int> m_RequestCount;
            public int m_MaxRequests;

#if DEBUG
            [NativeDisableUnsafePtrRestriction] public NativeArray<int> m_DebugStats;
#endif

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
#if !DEBUG
                // [优化] Chunk 级快速剪枝：如果请求队列已满，跳过整个Chunk的处理
                if (m_RequestCount[0] >= m_MaxRequests) return; 
#endif

                NativeArray<Entity> jobSeekerEntities = chunk.GetNativeArray(this.m_EntityType);
                NativeArray<Owner> owners = chunk.GetNativeArray(ref this.m_OwnerType);
                NativeArray<JobSeeker> jobSeekers = chunk.GetNativeArray(ref this.m_JobSeekerType);
                NativeArray<CurrentBuilding> currentBuildings = chunk.GetNativeArray(ref this.m_CurrentBuildingType);

                // 获取当前Chunk的随机数生成器
                Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);

#if DEBUG
                // 安全指针获取用于统计
                unsafe
                {
                    int* debugPtr = (int*)m_DebugStats.GetUnsafePtr();
                    System.Threading.Interlocked.Add(ref debugPtr[IDX_TotalSeekers], jobSeekerEntities.Length);
                }
#endif

                for (int i = 0; i < jobSeekerEntities.Length; i++)
                {
                    Entity jobSeekerEntity = jobSeekerEntities[i];
                    Entity citizenEntity = owners[i].m_Owner;

                    // [优化] 内部限流检查
                    // 检查限流
                    if (m_RequestCount[0] >= m_MaxRequests)
                    {
                        // 限流：不删除实体，留待下一帧处理。但 HasJobSeeker 保持为 true
#if DEBUG
                        unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugStats.GetUnsafePtr())[IDX_Throttled]); }
#endif
                        return; // 退出，该 Chunk 剩余部分也由于限流跳过
                    }

                    // 1. 基础校验：如果市民已被删除或数据丢失，则标记当前求职实体为删除并跳过
                    if (this.m_Deleteds.HasComponent(citizenEntity) || !this.m_CitizenDatas.HasComponent(citizenEntity))
                    {
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, jobSeekerEntities[i], default(Deleted));
                        continue;
                    }

                    Entity householdEntity = this.m_HouseholdMembers[citizenEntity].m_Household;
                    Citizen citizenData = this.m_CitizenDatas[citizenEntity];

                    // 2. 确定市民当前的出发位置（作为寻路的起点）
                    Entity currentLocationEntity = Entity.Null;
                    if (this.m_PropertyRenters.HasComponent(householdEntity))
                    {
                        // 如果租房，起点是租住的房产
                        currentLocationEntity = this.m_PropertyRenters[householdEntity].m_Property;
                    }
                    else if (chunk.Has(ref this.m_CurrentBuildingType))
                    {
                        // 如果是通勤者（Commuter），起点是当前所在建筑
                        if ((citizenData.m_State & CitizenFlags.Commuter) != CitizenFlags.None)
                        {
                            currentLocationEntity = currentBuildings[i].m_CurrentBuilding;
                        }
                    }
                    else if (this.m_HomelessHouseholds.HasComponent(householdEntity))
                    {
                        // 如果无家可归，起点是临时避难所/所在地
                        currentLocationEntity = this.m_HomelessHouseholds[householdEntity].m_TempHome;
                    }

                    // Entity jobSeekerEntity = jobSeekerEntities[i];

                    // 3. 如果找到了有效的出发位置，开始处理求职逻辑
                    if (currentLocationEntity != Entity.Null)
                    {
                        int educationLevel = jobSeekers[i].m_Level; // 市民的教育等级
                        int targetJobLevel = educationLevel;        // 期望寻找的工作等级（初始为教育等级）
                        int currentJobLevel = -1;                   // 当前已有的工作等级（-1表示失业）
                        bool isSwitcher = false;

                        // 检查是否在外部连接工作（例如住在城里但在城外工作）
                        bool worksOutside = this.m_Workers.HasComponent(citizenEntity) && this.m_OutsideConnections.HasComponent(this.m_Workers[citizenEntity].m_Workplace);

                        // 获取当前工作等级（如果不是外部工作）
                        if (this.m_Workers.HasComponent(citizenEntity) && !worksOutside)
                        {
                            currentJobLevel = this.m_Workers[citizenEntity].m_Level;
                            isSwitcher = true;
                        }

                        // 4. 判定是否需要换工作：
                        // 如果已有工作，且当前工作等级 >= 期望等级，或者期望等级并不比当前高，则无需找工作。
                        // 逻辑解释：如果我有2级工作，想找2级或1级工作，没必要换。如果我有2级工作，想找3级，才继续。
                        if (currentJobLevel >= 0 && targetJobLevel > educationLevel && targetJobLevel <= currentJobLevel)
                        {
                            // 移除求职状态，删除求职实体
                            EndJobSeeking(unfilteredChunkIndex, citizenEntity, jobSeekerEntity);
                            continue;
                        }

                        // 5. 降级搜索：
                        // 如果期望等级的职位没有空缺（FreeCache <= 0），则降低期望等级，直到找到有空缺的等级或比当前工作还差为止。
                        while (targetJobLevel > currentJobLevel && this.m_FreeCache[targetJobLevel] <= 0)
                        {
                            targetJobLevel--;
                        }

                        // 如果降级后没有找到比当前工作更好的机会（或者失业者连最低级工作都没空缺）
                        if (targetJobLevel == -1)
                        {
                            EndJobSeeking(unfilteredChunkIndex, citizenEntity, jobSeekerEntity);
#if DEBUG
                            // ⚠️ 统计：区分失业者和跳槽者的"全城无空缺"计数
                            if (!isSwitcher)
                                unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugStats.GetUnsafePtr())[IDX_Unemp_NoVacancy]); }
#endif
                            continue; // 无论失业者还是跳槽者，都应跳过后续逻辑
                        }

                        // 6. 概率放弃（模拟市场竞争或换工作意愿）：
                        float freeJobsCount = this.m_FreeCache[targetJobLevel];
                        // 计算竞争比：该学历的总待业人数 / 该等级的空缺职位数
                        float competitionRatio = (float)this.m_EmployableByEducation[targetJobLevel] / freeJobsCount;

                        // 如果已有工作，且竞争激烈（ratio > 2），有一定概率放弃跳槽
                        if (isSwitcher && random.NextFloat(competitionRatio) > 2f)
                        {
                            EndJobSeeking(unfilteredChunkIndex, citizenEntity, jobSeekerEntity);
#if DEBUG
                            unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugStats.GetUnsafePtr())[IDX_Swit_GiveUp]); }
#endif
                            continue;
                        }

                        // [优化] 在准备发起寻路前增加计数。
                        // 只有确定要寻路了才占用配额。
                        // 额度满了，这帧不做事，直接返回（JobSeeker实体保留到下一帧处理）
                        unsafe
                        {
                            int* reqPtr = (int*)m_RequestCount.GetUnsafePtr();
                            if (System.Threading.Interlocked.Increment(ref reqPtr[0]) > m_MaxRequests)
                            {
#if DEBUG
                                // 刚好这帧超了，回退
                                System.Threading.Interlocked.Increment(ref ((int*)m_DebugStats.GetUnsafePtr())[IDX_Throttled]);
#endif
                                return;
                            }
                        }

                        // 7. 准备寻路：找到合适的目标等级后，发起寻路请求
                        // 给求职实体添加路径信息组件，状态设为 Pending
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, jobSeekerEntity, new PathInformation
                        {
                            m_State = PathFlags.Pending
                        });

                        Household householdData = this.m_Households[householdEntity];
                        DynamicBuffer<HouseholdCitizen> householdCitizens = this.m_HouseholdCitizens[householdEntity];

                        // 配置寻路参数
                        PathfindParameters parameters = new PathfindParameters
                        {
                            m_MaxSpeed = 111.111115f, // 极高速度，通常用于抽象计算
                            m_WalkSpeed = 1.6666667f, // 步行速度 (~6km/h)
                            m_Weights = CitizenUtils.GetPathfindWeights(citizenData, householdData, householdCitizens.Length),
                            m_Methods = (PathMethod.Pedestrian | PathMethod.PublicTransportDay | PathMethod.PublicTransportNight),
                            m_MaxCost = m_DynamicFindJobMaxCost,
                            m_PathfindFlags = (PathfindFlags.Simplified | PathfindFlags.IgnorePath) // 简化寻路，不需要实际路径，只需要找到目的地
                        };

                        SetupQueueTarget origin = new SetupQueueTarget
                        {
                            m_Type = SetupTargetType.CurrentLocation,
                            m_Methods = PathMethod.Pedestrian
                        };

                        // 设置寻路目标：寻找工作 (JobSeekerTo)
                        SetupQueueTarget destination = new SetupQueueTarget
                        {
                            m_Type = SetupTargetType.JobSeekerTo,
                            m_Methods = PathMethod.Pedestrian,
                            // Value用于寻路启发式评分：等级越高，权重越高
                            m_Value = educationLevel + 5 * (targetJobLevel + 1),
                            // Value2 可能是竞争系数，如果在外部工作则为0
                            m_Value2 = (worksOutside ? 0f : competitionRatio)
                        };

                        // 如果允许去城外工作
                        if (jobSeekers[i].m_Outside > 0)
                        {
                            destination.m_Flags |= SetupTargetFlags.Export;
                        }
                        // 如果目前在外部工作，可能倾向于找回本地工作（Import）? 或者是标记允许寻找外部?
                        if (worksOutside)
                        {
                            destination.m_Flags |= SetupTargetFlags.Import;
                        }

                        // 如果有车，更新寻路方法
                        PathUtils.UpdateOwnedVehicleMethods(householdEntity, ref this.m_OwnedVehicles, ref parameters, ref origin, ref destination);

                        // 将寻路请求加入队列
                        SetupQueueItem queueItem = new SetupQueueItem(jobSeekerEntity, parameters, origin, destination);
                        this.m_PathfindQueue.Enqueue(queueItem);
#if DEBUG
                        unsafe
                        {
                            int idx = isSwitcher ? IDX_Swit_Sent : IDX_Unemp_Sent;
                            System.Threading.Interlocked.Increment(ref ((int*)m_DebugStats.GetUnsafePtr())[idx]);
                        }
#endif
                    }
                    else
                    {
                        // 无法定位起始点
                        EndJobSeeking(unfilteredChunkIndex, citizenEntity, jobSeekerEntity);
                    }

                } // for 循环
            } // Execute
              // 辅助方法：结束求职状态
            private void EndJobSeeking(int sortKey, Entity citizen, Entity jobSeeker)
            {
                // 关键：关闭Citizen上的HasJobSeeker，避免状态卡死
                m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(sortKey, citizen, false);
                // 删除JobSeeker实体
                m_CommandBuffer.AddComponent(sortKey, jobSeeker, default(Deleted));
            }
        }
        #endregion

        #region JobStruct StartWorkingJob
        /// <summary>
        /// 入职任务：处理寻路完成后的结果。
        /// 如果寻路成功找到了工作地点，将市民分配到该工作岗位，更新员工列表和市民状态。
        /// </summary>
        [BurstCompile]
        private struct StartWorkingJob : IJobChunk
        {
            //[ReadOnly] public NativeList<ArchetypeChunk> m_Chunks; // 待处理的块列表
            [ReadOnly] public EntityTypeHandle m_EntityType; // 实体类型
            [ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType; // 所有者组件
            [ReadOnly] public ComponentTypeHandle<JobSeeker> m_JobSeekerType; // 求职者组件
            [ReadOnly] public ComponentTypeHandle<PathInformation> m_PathInfoType; // 路径信息组件（包含寻路结果）
            [ReadOnly] public ComponentLookup<Citizen> m_Citizens; // 市民查询
            [ReadOnly] public ComponentLookup<Deleted> m_Deleteds; // 删除标记查询
            [ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs; // 预制体引用查询
            [ReadOnly] public ComponentLookup<WorkplaceData> m_WorkplaceDatas; // 工作场所数据查询
            [ReadOnly] public ComponentLookup<Worker> m_Workers; // 工人查询
            [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings; // 可生成建筑数据
            [ReadOnly] public ComponentLookup<WorkProvider> m_WorkProviders; // 职位提供者查询
            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters; // 租户查询

            // 这些必须是 ReadWrite，且由于是串行 Schedule，所以安全
            public BufferLookup<Employee> m_EmployeeBuffers; // 员工缓冲区（可写）
            public ComponentLookup<FreeWorkplaces> m_FreeWorkplaces; // 空闲工位查询（可写）

            public NativeQueue<TriggerAction> m_TriggerBuffer; // 触发动作队列（用于统计或UI）
            public EntityCommandBuffer m_CommandBuffer; // 命令缓冲区
            public uint m_SimulationFrame; // 当前模拟帧
            public NativeValue<int> m_StartedWorking; // 统计成功入职人数

#if DEBUG
            // [诊断] 传入计数器
            public NativeValue<int> m_GlobalHiredCounter;
            [NativeDisableUnsafePtrRestriction] public NativeArray<int> m_DebugStats;
#endif

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Owner> owners = chunk.GetNativeArray(ref m_OwnerType);
                NativeArray<PathInformation> pathInfos = chunk.GetNativeArray(ref m_PathInfoType);
                NativeArray<Entity> jobSeekerEntities = chunk.GetNativeArray(m_EntityType);
                NativeArray<JobSeeker> jobSeekers = chunk.GetNativeArray(ref m_JobSeekerType);

                // int successCount = 0;

                // 遍历所有待处理的Chunk实体
                for (int j = 0; j < jobSeekerEntities.Length; j++)
                {
                    // 1. 如果寻路仍在进行中 (Pending)，跳过，等待下一帧
                    if ((pathInfos[j].m_State & PathFlags.Pending) != 0) continue;

                    Entity jobSeekerEntity = jobSeekerEntities[j];
                    Entity citizenEntity = owners[j].m_Owner;

                    bool isSwitcher = m_Workers.HasComponent(citizenEntity);
#if DEBUG
                    bool success = false;
#endif
                    bool isPathFail = (pathInfos[j].m_Destination == Entity.Null);

                    // 2. 确认市民有效
                    if (this.m_Citizens.HasComponent(citizenEntity) && !this.m_Deleteds.HasComponent(citizenEntity))
                    {
                        // 获取寻路找到的目的地（工作地点）
                        Entity workplaceEntity = pathInfos[j].m_Destination;

                        // 3. 验证工作地点是否有效（包含Prefab且有员工缓冲区）
                        if (!isPathFail && m_Prefabs.HasComponent(workplaceEntity) && m_EmployeeBuffers.HasBuffer(workplaceEntity))
                        {
                            DynamicBuffer<Employee> employees = this.m_EmployeeBuffers[workplaceEntity];
                            WorkProvider workProvider = this.m_WorkProviders[workplaceEntity];

                            // 处理租户情况：如果工作地点是被租赁的房产，需指向实际物业
                            Entity propertyEntity = m_PropertyRenters.HasComponent(workplaceEntity) ? m_PropertyRenters[workplaceEntity].m_Property : workplaceEntity;
                            Entity buildingPrefab = m_Prefabs[propertyEntity].m_Prefab;
                            int buildingLevel = m_SpawnableBuildings.HasComponent(buildingPrefab) ? m_SpawnableBuildings[buildingPrefab].m_Level : 1;

                            // 4. 再次确认：市民没有在这里工作（避免重复入职）
                            if (!m_Workers.HasComponent(citizenEntity) || m_Workers[citizenEntity].m_Workplace != workplaceEntity)

                            {
                                Entity workplacePrefab = this.m_Prefabs[workplaceEntity].m_Prefab;
                                if (m_WorkplaceDatas.HasComponent(workplacePrefab) && m_FreeWorkplaces.HasComponent(workplaceEntity))
                                {
                                    FreeWorkplaces freeWorkplaces = m_FreeWorkplaces[workplaceEntity];

                                    // 必须刷新，因为单线程处理时上一个人可能刚抢了位置
                                    WorkplaceData workplaceData = m_WorkplaceDatas[workplacePrefab];
                                    freeWorkplaces.Refresh(employees, workProvider.m_MaxWorkers, workplaceData.m_Complexity, buildingLevel);

                                    int assignedLevel = freeWorkplaces.GetBestFor(jobSeekers[j].m_Level);

                                    // 6. 成功匹配到职位
                                    if (assignedLevel >= 0)
                                    {
                                        // === 成功入职 ===
#if DEBUG
                                        success = true;
#endif

                                        // 随机分配班次（晚班/夜班）
                                        Workshift shift = Workshift.Day;
                                        float shiftRng = new Unity.Mathematics.Random(1 + (m_SimulationFrame ^ m_Citizens[citizenEntity].m_PseudoRandom)).NextFloat();
                                        if (shiftRng < workplaceData.m_EveningShiftProbability) shift = Workshift.Evening;
                                        else if (shiftRng < workplaceData.m_EveningShiftProbability + workplaceData.m_NightShiftProbability) shift = Workshift.Night;

                                        // 添加员工记录到工作地点
                                        employees.Add(new Employee { m_Worker = citizenEntity, m_Level = (byte)assignedLevel });

                                        if (isSwitcher) m_CommandBuffer.RemoveComponent<Worker>(citizenEntity);

                                        m_CommandBuffer.AddComponent(citizenEntity, new Worker
                                        {
                                            m_Workplace = workplaceEntity,
                                            m_Level = (byte)assignedLevel,
                                            m_LastCommuteTime = pathInfos[j].m_Duration,
                                            m_Shift = shift
                                        });

                                        // 触发入职事件
                                        m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenStartedWorking, Entity.Null, citizenEntity, workplaceEntity));

                                        // 更新空缺数 (虽然 Refresh 会重算，但保持数据一致性)
                                        freeWorkplaces.Refresh(employees, workProvider.m_MaxWorkers, workplaceData.m_Complexity, buildingLevel);
                                        m_FreeWorkplaces[workplaceEntity] = freeWorkplaces;

#if DEBUG
                                        m_GlobalHiredCounter.value++;
#endif

                                    }
                                }
                            }
                        }
                        else if ((m_Citizens[citizenEntity].m_State & CitizenFlags.Commuter) != CitizenFlags.None)
                        {
                            m_CommandBuffer.AddComponent(citizenEntity, default(Deleted));
                        }

#if DEBUG
                        // 统计失败原因
                        unsafe
                        {
                            int* ptr = (int*)m_DebugStats.GetUnsafePtr();
                            if (success)
                            {
                                System.Threading.Interlocked.Increment(ref ptr[isSwitcher ? IDX_Swit_Success : IDX_Unemp_Success]);
                            }
                            else
                            {
                                if (isPathFail)
                                    System.Threading.Interlocked.Increment(ref ptr[isSwitcher ? IDX_Swit_Fail_Path : IDX_Unemp_Fail_Path]);
                                else
                                    System.Threading.Interlocked.Increment(ref ptr[isSwitcher ? IDX_Swit_Fail_Taken : IDX_Unemp_Fail_Taken]);
                            }
                        }
#endif
                        // 完成求职流程，移除HasJobSeeker标记:无论成功与否，关闭求职状态
                        this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(citizenEntity, value: false);
                    }
                    // 删除临时的JobSeeker实体
                    this.m_CommandBuffer.AddComponent(jobSeekerEntity, default(Deleted));
                }

            } // Execute
        } // job
        #endregion
    } // class

}



