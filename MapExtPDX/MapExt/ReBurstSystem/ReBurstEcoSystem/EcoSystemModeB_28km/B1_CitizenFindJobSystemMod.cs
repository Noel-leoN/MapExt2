// Game.Simulation.CitizenFindJobSystem
// v1.4.2无变化

using System.Threading;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

// 原始代码备份模板
namespace MapExtPDX.MapExt.ReBurstEcoSystemModeB
{
    /// <summary>
    /// 市民寻找工作的系统。处理失业者找工作和在职者寻找更好工作的逻辑。
    /// </summary>

    // =========================================================================================
    // 1. Mod 自定义系统类型 (当前类)
    using ModSystem = CitizenFindJobSystemMod;
    // 2. 原版系统类型 (用于禁用和定位)
    using TargetSystem = CitizenFindJobSystem;
    // =========================================================================================

    public partial class CitizenFindJobSystemMod : GameSystemBase
    {
        #region Constants
        // 每天更新次数，用于将负载分散到不同帧，可调低以提高性能
        public static readonly int kUpdatesPerDay = 256;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / (kUpdatesPerDay * 16);// 16 是分片系数，计算每多少帧运行一次，以保证所有Chunk在一天内被轮询一遍

        // 找工作失败后冷却时间范围 (帧数)，可调高以提高性能，但需要配合幸福度
        //public static readonly int kJobSeekCoolDownMax = 10000;
        //public static readonly int kJobSeekCoolDownMin = 5000;

        // [配置] 健康的空置率标准 (例如 15%)
        // 当 (空缺/总岗位) >= 15% 时，视为就业市场完全饱和(Saturation=1)
        private const float kHealthyVacancyRate = 0.15f;
        // [配置] 只有当具体空缺数 > 0 且 满足概率时才寻找。
        // 这里定义 "市场饱和" 时的最大概率
        private const float kMaxUnemployedSearchChance = 0.8f; // 失业者最大每日寻找概率
        //private const float kMaxEmployedSwitchChance = 0.05f;  // 在职者最大每日跳槽概率

        // [优化] 动态冷却范围
        public static readonly int kCoolDownMin_Abundant = 2000;  // 岗位充足，冷却短
        public static readonly int kCoolDownMax_Scarce = 30000;   // 岗位紧缺，冷却极长

        // 失业时间累加值
        private const float kUnemploymentIncrement = 1f / 256f; // 1f / kUpdatesPerDay

        // [优化] 新增常量：每tick最大允许创建的求职者数量，防止瞬间压力过大
        private const int kMaxSeekersPerFrame = 500;

        // [优化] 激进剪枝阈值：如果某等级空缺少于此值，视为无空缺，不发起寻路
        private const int kMinVacanciesToSearch = 50;

        // [优化] 用于跨线程计数的原子计数器
        private NativeArray<int> m_CreatedSeekerCount;

#if DEBUG
        // [Debug] 统计索引定义
        // [Debug] 重构计数器索引
        // 0-6: 失业者统计, 7-13: 在职者统计
        private const int CNT_Total = 0;
        private const int CNT_Skip_CoolDown = 1;
        private const int CNT_Skip_NoVacancy = 2;
        private const int CNT_Skip_RNG = 3;
        private const int CNT_Skip_Throttled = 4;
        private const int CNT_Success = 5;
        private const int CNT_Skip_Other = 6; // 搬家、小孩等

        private const int DBG_Offset_Unemployed = 0;
        private const int DBG_Offset_Employed = 7;
        private const int DBG_ArrayLength = 14;
        private NativeArray<int> m_DebugCounters;
        private bool m_EnableDebug = false; // 开发时设为 true，发布设为 false
#endif

        #endregion

        #region Fields
        private EndFrameBarrier m_EndFrameBarrier;
        private SimulationSystem m_SimulationSystem;
        private CountWorkplacesSystem m_CountWorkplacesSystem;

        private EntityQuery m_UnemployedQuery;
        private EntityQuery m_EmployedQuery;
        private EntityQuery m_CitizenParametersQuery;

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

            // 引入系统依赖
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CountWorkplacesSystem = World.GetOrCreateSystemManaged<CountWorkplacesSystem>();

            // 构建查询：失业者 (有Citizen和HouseholdMember组件，但没有Worker等组件)
            m_UnemployedQuery = SystemAPI.QueryBuilder()
                .WithAll<Citizen, HouseholdMember>()
                .WithNone<Temp, Worker, Game.Citizens.Student, HasJobSeeker, HasSchoolSeeker, HealthProblem, Deleted>()
                .Build();

            // 构建查询：在职者
            m_EmployedQuery = SystemAPI.QueryBuilder()
                .WithAll<Citizen, HouseholdMember, Worker>()
                .WithNone<Temp, Game.Citizens.Student, HasJobSeeker, HasSchoolSeeker, HealthProblem, Deleted>()
                .Build();

            // 构建查询：市民参数(本系统中主要使用找工作概率、换工作概率)
            m_CitizenParametersQuery = SystemAPI.QueryBuilder()
                .WithAll<CitizenParametersData>()
                .Build();

            RequireForUpdate(m_CitizenParametersQuery);
            // [优化] 原版错误逻辑：失业者为空时跳槽者逻辑也无法执行；虽然罕见但仍需考虑去除该条件，然而由于该情况罕见因此也不会影响性能。
            // RequireForUpdate(m_UnemployedQuery);

            // [优化] 初始化计数器
            m_CreatedSeekerCount = new NativeArray<int>(1, Allocator.Persistent);

#if DEBUG
            m_DebugCounters = new NativeArray<int>(DBG_ArrayLength, Allocator.Persistent);
#endif
        }

        protected override void OnDestroy()
        {
            // [优化] 释放资源
            if (m_CreatedSeekerCount.IsCreated) m_CreatedSeekerCount.Dispose();

#if DEBUG
            if (m_DebugCounters.IsCreated) m_DebugCounters.Dispose();
#endif

            base.OnDestroy();
        }
        #endregion

        #region Update Loop
        protected override void OnUpdate()
        {
            // 1. 先打印上一帧的 Debug 信息 (如果有活动)
#if DEBUG
            if (m_EnableDebug)
            {
                Dependency.Complete(); // 确保数据写入完成
                PrintDebugLog();
            }
            // 清零
            for (int i = 0; i < DBG_ArrayLength; i++) m_DebugCounters[i] = 0;
#endif

            // [优化] 每帧重置计数器
            m_CreatedSeekerCount[0] = 0;

            // 计算更新帧索引，当前帧对应的分片索引 (0-15)用于分片更新
            uint updateFrame = SimulationUtils.GetUpdateFrame(m_SimulationSystem.frameIndex, kUpdatesPerDay, 16);
            uint simulationFrame = m_SimulationSystem.frameIndex;
            var commandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
            var citizenParams = m_CitizenParametersQuery.GetSingleton<CitizenParametersData>();

            // --- 宏观经济数据准备 ---
            var unemployedWorkplaces = m_CountWorkplacesSystem.GetUnemployedWorkspaceByLevel();
            var freeWorkplaces = m_CountWorkplacesSystem.GetFreeWorkplaces();
            var totalCapacity = m_CountWorkplacesSystem.GetTotalWorkplaces();

            int totalFree = 0;
            int totalUnemployedFree = 0;
            int totalCapacitySum = 0;
            for (int i = 0; i < 5; i++)
            {
                totalFree += freeWorkplaces[i];
                totalUnemployedFree += unemployedWorkplaces[i];
                totalCapacitySum += totalCapacity[i];
            }

            // [需求1 & 2] 动态计算市场饱和度 (Saturation)
            // 饱和度 1.0 表示岗位非常充足(达到15%空置率)，0.0 表示完全无岗位
            float currentVacancyRate = (totalCapacitySum > 0) ? (float)totalFree / (float)totalCapacitySum : 0f;
            float marketSaturation = math.clamp(currentVacancyRate / kHealthyVacancyRate, 0f, 1f);

            // 基于饱和度计算实际概率
            float actualUnemployedChance = kMaxUnemployedSearchChance * marketSaturation;
            float actualEmployedSwitchChance = citizenParams.m_SwitchJobRate * marketSaturation; // 岗位少时，极少跳槽

            // 1. 失业者找工作
            if (totalUnemployedFree > kMinVacanciesToSearch)
            {
                ScheduleJob(
                    m_UnemployedQuery,
                    unemployedWorkplaces,
                    true,
                    updateFrame,
                    simulationFrame,
                    commandBuffer,
                    actualUnemployedChance, // 传入计算后的概率
                    marketSaturation
#if DEBUG
        ,
                    DBG_Offset_Unemployed
#endif
                );
            }

            // 2. 处理在职者跳槽 (根据配置的概率随机触发)            

            // 增加判断：如果计算出的概率太小（比如小于 0.0001%），直接跳过整个Query，省性能
            if (!m_EmployedQuery.IsEmpty &&
                totalFree > kMinVacanciesToSearch * 5 &&
                actualEmployedSwitchChance > 0.000001f)
            // 简单的随机检查是否执行跳槽逻辑
            // 注意：GetRandom的种子随帧变化
            // [优化] 增加随机判断，减少在职者查询的频率，因为跳槽是低频事件
            {
                // 预先随机过滤 (Pre-check): 
                // 这里的 2.0f 是为了给 Job 内部留一点随机余地，避免外面过滤太死
                // 只要预筛选通过了，进入 Job 还会进行精确的 Random 判定
                if (RandomSeed.Next().GetRandom((int)simulationFrame).NextFloat(1f) <= actualEmployedSwitchChance * 20.0f) // 适当放宽预筛选
                {
                    ScheduleJob(
                        m_EmployedQuery,
                        freeWorkplaces,
                        false,
                        updateFrame,
                        simulationFrame,
                        commandBuffer,
                        actualEmployedSwitchChance, // 传入基于原版参数修正后的概率
                        marketSaturation
#if DEBUG
        ,
                        DBG_Offset_Employed
#endif
                    );
                }
            }
            // Job 依赖传递
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
        }

        /// <summary>
        /// 封装 Job 调度逻辑
        /// </summary>
        private void ScheduleJob(
            EntityQuery query,
            Workplaces availableWorkplaces,
            bool isUnemployedFindJob,
            uint updateFrame,
            uint simulationFrame,
            EntityCommandBuffer.ParallelWriter commandBuffer,
            float searchChance,
            float marketSaturation
#if DEBUG
         ,
            int debugOffset
#endif
            )
        {
            var job = new CitizenFindJobJob
            {
                // Handle Types
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_CitizenType = SystemAPI.GetComponentTypeHandle<Citizen>(false),
                m_CurrentBuildingType = SystemAPI.GetComponentTypeHandle<CurrentBuilding>(true),
                m_WorkerType = SystemAPI.GetComponentTypeHandle<Worker>(true),
                m_UpdateFrameType = SystemAPI.GetSharedComponentTypeHandle<UpdateFrame>(),

                // Component Lookups
                m_HouseholdMembers = SystemAPI.GetComponentLookup<HouseholdMember>(true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                m_TouristHouseholds = SystemAPI.GetComponentLookup<TouristHousehold>(true),
                m_HomelessHouseholds = SystemAPI.GetComponentLookup<HomelessHousehold>(true),
                m_MovingAways = SystemAPI.GetComponentLookup<MovingAway>(true),
                m_OutsideConnections = SystemAPI.GetComponentLookup<Game.Objects.OutsideConnection>(true),
                m_HasJobSeekers = SystemAPI.GetComponentLookup<HasJobSeeker>(true),

                // Data
                m_AvailableWorkspacesByLevel = availableWorkplaces,
                m_SimulationFrame = simulationFrame,
                m_UpdateFrameIndex = updateFrame,
                m_IsUnemployedFindJob = isUnemployedFindJob,
                m_RandomSeed = RandomSeed.Next(),
                m_CommandBuffer = commandBuffer,

                // 新增限流计数
                m_CreatedSeekerCount = m_CreatedSeekerCount,
                m_MaxSeekers = kMaxSeekersPerFrame,

                // 传入计算好的参数
                m_SearchChance = searchChance,
                m_MarketSaturation = marketSaturation,
                m_MinVacancies = kMinVacanciesToSearch,

#if DEBUG
                m_DebugCounters = m_DebugCounters,
                m_DebugOffset = debugOffset
#endif
            };

            Dependency = job.ScheduleParallel(query, Dependency);
        }
        #endregion

#if DEBUG
        private void PrintDebugLog()
        {
            // 只有当有扫描行为时才打印，或者每 60 帧强制打印一次心跳
            int unempScan = m_DebugCounters[DBG_Offset_Unemployed + CNT_Total];
            int empScan = m_DebugCounters[DBG_Offset_Employed + CNT_Total];

            if (unempScan == 0 && empScan == 0) return;

            // 使用 StringBuilder 避免大量 GC
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[CitizenFindJobSystem] Frame {m_SimulationSystem.frameIndex}");

            if (unempScan > 0)
            {
                AppendLogSection(sb, "Unemployed", DBG_Offset_Unemployed);
            }
            if (empScan > 0)
            {
                AppendLogSection(sb, "Employed  ", DBG_Offset_Employed);
            }

            Mod.Debug(sb.ToString());
        }

        private void AppendLogSection(System.Text.StringBuilder sb, string label, int offset)
        {
            sb.Append($"  > {label}: Scanned={m_DebugCounters[offset + CNT_Total]} | ");
            sb.Append($"Success=<color=green>{m_DebugCounters[offset + CNT_Success]}</color> | ");
            sb.Append($"CoolDown={m_DebugCounters[offset + CNT_Skip_CoolDown]} | ");
            sb.Append($"NoVac={m_DebugCounters[offset + CNT_Skip_NoVacancy]} | ");
            sb.Append($"RNG={m_DebugCounters[offset + CNT_Skip_RNG]} | ");
            sb.Append($"Throttle=<color=yellow>{m_DebugCounters[offset + CNT_Skip_Throttled]}</color> | ");
            sb.Append($"Skip(Age/Mov)={m_DebugCounters[offset + CNT_Skip_Other]}");
            sb.AppendLine();
        }
#endif

        #region CitizenFindJobJob Struct
        [BurstCompile]
        private struct CitizenFindJobJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;

            [ReadOnly] public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;
            [ReadOnly] public ComponentTypeHandle<Worker> m_WorkerType;
            [ReadOnly] public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

            [ReadOnly] public ComponentLookup<HouseholdMember> m_HouseholdMembers;
            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
            [ReadOnly] public ComponentLookup<TouristHousehold> m_TouristHouseholds;
            [ReadOnly] public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;
            [ReadOnly] public ComponentLookup<MovingAway> m_MovingAways;
            [ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;
            [ReadOnly] public ComponentLookup<HasJobSeeker> m_HasJobSeekers;

            [ReadOnly] public Workplaces m_AvailableWorkspacesByLevel;
            [ReadOnly] public uint m_SimulationFrame;
            [ReadOnly] public uint m_UpdateFrameIndex;
            [ReadOnly] public bool m_IsUnemployedFindJob;
            [ReadOnly] public RandomSeed m_RandomSeed;

            // 读写Citizen组件m_UnemploymentTimeCounter
            public ComponentTypeHandle<Citizen> m_CitizenType;
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            // [优化] 限流计数器
            [NativeDisableUnsafePtrRestriction] public NativeArray<int> m_CreatedSeekerCount;

            [ReadOnly] public int m_MaxSeekers;
            [ReadOnly] public float m_SearchChance;
            [ReadOnly] public float m_MarketSaturation;
            [ReadOnly] public int m_MinVacancies;

#if DEBUG
            [NativeDisableUnsafePtrRestriction] public NativeArray<int> m_DebugCounters;
            [ReadOnly] public int m_DebugOffset;
#endif

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // 检查分片帧是否匹配：如果当前Chunk的更新帧索引不等于本帧计算出的索引，直接跳过。
                if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
                    return;

                // [优化] 如果本帧配额已满，直接返回，不再遍历Chunk内的实体
                if (m_CreatedSeekerCount[0] >= m_MaxSeekers)
                    return;

                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                NativeArray<Citizen> citizens = chunk.GetNativeArray(ref m_CitizenType);
                NativeArray<CurrentBuilding> currentBuildings = chunk.GetNativeArray(ref m_CurrentBuildingType);

                // 每个 Chunk 使用一个随机生成器，避免 False Sharing
                Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);

                // [优化] 动态计算冷却时间范围
                // 动态冷却计算
                int coolDownMin = (int)math.lerp(kCoolDownMax_Scarce * 0.8f, kCoolDownMin_Abundant, m_MarketSaturation);
                int coolDownMax = (int)math.lerp(kCoolDownMax_Scarce, kCoolDownMin_Abundant * 1.5f, m_MarketSaturation);

                for (int i = 0; i < entities.Length; i++)
                {
                    // [Debug] 总扫描数
#if DEBUG
                    unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Total]); }
#endif
                    // [优化] 再次检查配额（因为是并行Job，其他线程可能已填满）
                    // 没必要非常精确，大致控制量级即可，减少Atomics竞争
                    if (i % 8 == 0 && m_CreatedSeekerCount[0] >= m_MaxSeekers)
                    {
#if DEBUG
                        // 标记为 Throttled 也可以不增加 Total，看统计需求。这里算作已扫描但跳过
#endif
                        return;
                    }

                    Entity citizenEntity = entities[i];
                    Citizen citizen = citizens[i];
                    Entity household = m_HouseholdMembers[citizenEntity].m_Household;
                    CitizenAge age = citizen.GetAge();

                    // 1. 排除儿童和老人
                    if (age == CitizenAge.Child || age == CitizenAge.Elderly)
                    {
                        if (m_IsUnemployedFindJob) // 只重置失业者的失业时间，在职者不用管
                        {
                            citizen.m_UnemploymentTimeCounter = 0f;
                            citizens[i] = citizen;
                        }
#if DEBUG
                        unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_Other]); }
#endif
                        continue;
                    }

                    // 2. 检查求职冷却时间 (HasJobSeeker 组件被移除后不会立即销毁，数据仍保留供检查)（HasJobSeeker组件存在但Seeker实体可能已空）
                    if (m_HasJobSeekers.HasComponent(citizenEntity))
                    {
                        uint lastSeekFrame = m_HasJobSeekers[citizenEntity].m_LastJobSeekFrameIndex;

                        // [优化] 使用动态冷却
                        int cooldown = random.NextInt(coolDownMin, coolDownMax); ;

                        if (lastSeekFrame + cooldown > m_SimulationFrame)
                        {
                            if (m_IsUnemployedFindJob)
                            {
                                citizen.m_UnemploymentTimeCounter += kUnemploymentIncrement;
                                citizens[i] = citizen;
                            }
#if DEBUG
                            unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_CoolDown]); }
#endif
                            continue;
                        }
                    }

                    // 3. 如果家庭正在搬离城市，不找工作
                    if (m_MovingAways.HasComponent(household))
                    {
                        // 正在搬离没必要计算失业时间
                        // citizen.m_UnemploymentTimeCounter += kUnemploymentIncrement;
                        // citizens[i] = citizen;
#if DEBUG
                        unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_Other]); }
#endif                    
                        continue;
                    }

                    int educationLevel = citizen.GetEducationLevel();
                    bool shouldCreateSeeker = false;
                    bool noVacancy = false;

                    // 4. 根据状态（失业/在职）判断是否尝试寻找工作
                    if (m_IsUnemployedFindJob)
                    {
                        // 失业者逻辑
                        citizen.m_UnemploymentTimeCounter += kUnemploymentIncrement;
                        citizens[i] = citizen;

                        // 计算所有不高于当前学历的可用岗位总数
                        int suitableWorkplaces = 0;
                        for (int level = 0; level <= educationLevel; level++)
                        {
                            if (m_AvailableWorkspacesByLevel[level] > 0)
                            {
                                suitableWorkplaces += m_AvailableWorkspacesByLevel[level];
                            }
                        }

                        // [优化] 逻辑判断：如果空缺极少，直接不考虑
                        if (suitableWorkplaces < m_MinVacancies)
                        {
                            // UpdateJobSeekerTimestamp(unfilteredChunkIndex, citizenEntity);
                            // continue;
                            noVacancy = true;
                        }

                        // [需求2] 使用传入的 Chance
                        if (random.NextFloat() < m_SearchChance)
                        {
                            shouldCreateSeeker = true;
                        }
                        else
                        {
                            // 失败也要重置时间戳，进入冷却
                            UpdateJobSeekerTimestamp(unfilteredChunkIndex, citizenEntity);
                            continue;
                        }
                    }

                    else
                    {
                        // 在职者逻辑
                        citizen.m_UnemploymentTimeCounter = 0f;
                        citizens[i] = citizen;

                        NativeArray<Worker> workers = chunk.GetNativeArray(ref m_WorkerType);
                        Worker workerData = workers[i];

                        // 获取当前工作等级 (如果是外部连接，视为0级)
                        int currentJobLevel = m_OutsideConnections.HasComponent(workerData.m_Workplace) ? 0 : workerData.m_Level;

                        // 如果当前工作等级已经匹配或高于学历，不跳槽
                        if (currentJobLevel >= educationLevel)
                        {
                            continue;
                        }

                        // 计算比当前工作更好且符合学历的岗位
                        int betterWorkplaces = 0;
                        for (int level = currentJobLevel; level <= educationLevel; level++)
                        {
                            if (m_AvailableWorkspacesByLevel[level] > 0)
                            {
                                betterWorkplaces += m_AvailableWorkspacesByLevel[level];
                            }
                        }

                        if (betterWorkplaces < m_MinVacancies)
                        {
                            noVacancy = true;
                        }
                        else if (random.NextFloat() < m_SearchChance) // 跳槽传入的chance通常更低
                        {
                            shouldCreateSeeker = true;
                        }
                        //else
                        //{
                        //    UpdateJobSeekerTimestamp(unfilteredChunkIndex, citizenEntity);
                        //    continue;
                        //}
                    }

                    // 5. 创建求职实体 (JobSeeker)
                    if (noVacancy)
                    {
                        // 没空缺，更新时间戳进入冷却（如果已有组件）
                        UpdateJobSeekerTimestamp(unfilteredChunkIndex, citizenEntity);
#if DEBUG
                        unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_NoVacancy]); }
#endif
                    }

                    else if (shouldCreateSeeker)
                    {
                        // 尝试创建
                        int currentCount;

                        // [优化] 原子操作增加计数，如果成功且未超标，则创建
                        unsafe
                        {
                            // 获取底层指针，对第一个元素进行原子递增
                            int* countPtr = (int*)m_CreatedSeekerCount.GetUnsafePtr();
                            currentCount = Interlocked.Increment(ref countPtr[0]);
                        }

                        if (currentCount <= m_MaxSeekers)
                        {
                            CreateJobSeekerEntity(unfilteredChunkIndex, citizenEntity, citizen, household, currentBuildings, i, educationLevel);
#if DEBUG
                            unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Success]); }
#endif
                        }
                        else
                        {
                            // 即使超标，为了避免下一帧立即重试导致逻辑死锁，给予一个短冷却
                            UpdateJobSeekerTimestamp(unfilteredChunkIndex, citizenEntity);
#if DEBUG
                            unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_Throttled]); }
#endif
                        }
                    }
                    else
                    {
                        // 概率检定失败
                        // 策略：不强制添加冷却组件，下帧依赖概率继续判定即可
                        // 或者：如果已有组件，更新时间戳让其冷却一会
                        UpdateJobSeekerTimestamp(unfilteredChunkIndex, citizenEntity);
#if DEBUG
                        unsafe { System.Threading.Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_RNG]); }
#endif
                    }
                }
            } // Execute

            /// <summary>
            /// 仅仅重置 HasJobSeeker 组件的时间戳，不创建实际的寻路实体
            /// </summary>
            private void UpdateJobSeekerTimestamp(int sortKey, Entity citizenEntity)
            {
                // 注意：SetComponent 会添加组件如果它不存在，或者修改现有组件
                // 这里对应原代码的 SetComponent 逻辑
                // 即使没找到工作，也更新 LastJobSeekFrameIndex，防止下一帧立即再次尝试
                // 这实际上就是设置了冷却开始时间
                //m_CommandBuffer.SetComponent(sortKey, citizenEntity, new HasJobSeeker
                //{
                //    m_Seeker = Entity.Null,
                //    m_LastJobSeekFrameIndex = m_SimulationFrame
                //});

                // [Fix] 只有当组件已存在时才更新，避免给全城人添加组件导致巨大的结构变化开销
                // 如果想要冷却机制，建议仅对已有 seeker 组件的人执行冷却。
                // 新人如果失败，下一帧由概率控制即可，不需要强制冷却组件。
                if (m_HasJobSeekers.HasComponent(citizenEntity))
                {
                    m_CommandBuffer.SetComponent(sortKey, citizenEntity, new HasJobSeeker
                    {
                        m_Seeker = Entity.Null,
                        m_LastJobSeekFrameIndex = m_SimulationFrame
                    });
                }
            }

            /// <summary>
            /// 创建 JobSeeker 实体并初始化相关组件
            /// </summary>
            private void CreateJobSeekerEntity(int sortKey, Entity citizenEntity, Citizen citizen, Entity household, NativeArray<CurrentBuilding> currentBuildings, int index, int educationLevel)
            {
                // 确定出发位置实体
                Entity startLocation = Entity.Null;

                if (!m_TouristHouseholds.HasComponent(household) && m_PropertyRenters.HasComponent(household))
                {
                    // 普通居民：家(非游客且有本地房产)
                    startLocation = m_PropertyRenters[household].m_Property;
                }
                else if (m_HomelessHouseholds.HasComponent(household))
                {
                    // 游民：临时住所
                    startLocation = m_HomelessHouseholds[household].m_TempHome;
                }
                else if (currentBuildings.IsCreated && (citizen.m_State & CitizenFlags.Commuter) != CitizenFlags.None)
                {
                    // 通勤者：当前所在建筑
                    startLocation = currentBuildings[index].m_CurrentBuilding;
                }

                // 如果找到了有效的位置，创建 Agent
                if (startLocation != Entity.Null)
                {
                    // 创建一个新的实体代表 "求职行为" (Agent)
                    Entity seekerEntity = m_CommandBuffer.CreateEntity(sortKey);

                    m_CommandBuffer.AddComponent(sortKey, seekerEntity, new Owner { m_Owner = citizenEntity });
                    m_CommandBuffer.AddComponent(sortKey, seekerEntity, new JobSeeker
                    {
                        m_Level = (byte)educationLevel,
                        m_Outside = (byte)(((citizen.m_State & CitizenFlags.Commuter) != CitizenFlags.None) ? 1u : 0u)
                    });
                    // Agent 从哪里出发
                    m_CommandBuffer.AddComponent(sortKey, seekerEntity, new CurrentBuilding { m_CurrentBuilding = startLocation });

                    // 更新标记 Citizen 拥有正在寻找工作的 Agent
                    m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(sortKey, citizenEntity, true);
                    m_CommandBuffer.SetComponent(sortKey, citizenEntity, new HasJobSeeker
                    {
                        m_Seeker = seekerEntity,
                        m_LastJobSeekFrameIndex = m_SimulationFrame
                    });
                }
            }
        }
        #endregion
    }
}