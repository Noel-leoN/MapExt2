// Game.Simulation.CitizenFindJobSystem
// v2.0 - 大型人口城市适配版（等比缩放概率 + 软限流）

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
using Unity.Mathematics;

namespace MapExtPDX.ModeB
{
    // =========================================================================================
    // 1. Mod 自定义系统类型 (当前类)
    using ModSystem = CitizenFindJobSystemMod;
    // 2. 原版系统类型 (用于禁用和定位)
    using TargetSystem = CitizenFindJobSystem;
    // =========================================================================================

    public partial class CitizenFindJobSystemMod : GameSystemBase
    {
        #region Constants
        /// <summary>
        /// 每天更新次数，16分片 → 每帧处理 1/16 的实体
        /// 每个市民每天被扫描 kUpdatesPerDay/16 ≈ 16 次
        /// </summary>
        public static readonly int kUpdatesPerDay = 256;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 262144 / (kUpdatesPerDay * 16);

        /// <summary>
        /// 原版冷却时间范围（帧数），失败后等待多久再试
        /// 5000~10000帧 ≈ 0.3~0.6天
        /// </summary>
        public static readonly int kJobSeekCoolDownMin = 5000;
        public static readonly int kJobSeekCoolDownMax = 10000;

        /// <summary>
        /// 失业时间累加值 = 1天/kUpdatesPerDay
        /// </summary>
        private const float kUnemploymentIncrement = 1f / 256f;

        /// <summary>
        /// 原子计数器：软限流用
        /// </summary>
        private NativeArray<int> m_CreatedSeekerCount;

#if DEBUG
        // [Debug] 统计索引定义
        private const int CNT_Total = 0;
        private const int CNT_Skip_CoolDown = 1;
        private const int CNT_Skip_NoVacancy = 2;
        private const int CNT_Skip_RNG = 3;
        private const int CNT_Skip_Throttled = 4;
        private const int CNT_Success = 5;
        private const int CNT_Skip_Other = 6;

        private const int DBG_Offset_Unemployed = 0;
        private const int DBG_Offset_Employed = 7;
        private const int DBG_ArrayLength = 14;
        private NativeArray<int> m_DebugCounters;
        private readonly bool m_EnableDebug = false;
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

            // 禁用原版系统
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                Mod.Info($"[{typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
            }
#if DEBUG
            else
            {
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系统: {typeof(TargetSystem).Name}");
            }
#endif

            // 引入系统依赖
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_CountWorkplacesSystem = World.GetOrCreateSystemManaged<CountWorkplacesSystem>();

            // 构建查询：失业者
            m_UnemployedQuery = SystemAPI.QueryBuilder()
                .WithAll<Citizen, HouseholdMember>()
                .WithNone<Temp, Worker, Game.Citizens.Student, HasJobSeeker, HasSchoolSeeker, HealthProblem, Deleted>()
                .Build();

            // 构建查询：在职者
            m_EmployedQuery = SystemAPI.QueryBuilder()
                .WithAll<Citizen, HouseholdMember, Worker>()
                .WithNone<Temp, Game.Citizens.Student, HasJobSeeker, HasSchoolSeeker, HealthProblem, Deleted>()
                .Build();

            // 构建查询：市民参数
            m_CitizenParametersQuery = SystemAPI.QueryBuilder()
                .WithAll<CitizenParametersData>()
                .Build();

            RequireForUpdate(m_CitizenParametersQuery);

            // 初始化计数器
            m_CreatedSeekerCount = new NativeArray<int>(1, Allocator.Persistent);

#if DEBUG
            m_DebugCounters = new NativeArray<int>(DBG_ArrayLength, Allocator.Persistent);
#endif
        }

        protected override void OnDestroy()
        {
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
#if DEBUG
            if (m_EnableDebug)
            {
                Dependency.Complete();
                PrintDebugLog();
            }
            for (int i = 0; i < DBG_ArrayLength; i++) m_DebugCounters[i] = 0;
#endif

            // 每帧重置限流计数器
            m_CreatedSeekerCount[0] = 0;

            // 计算更新帧索引，当前帧对应的分片索引 (0-15)
            uint updateFrame = SimulationUtils.GetUpdateFrame(m_SimulationSystem.frameIndex, kUpdatesPerDay, 16);
            uint simulationFrame = m_SimulationSystem.frameIndex;
            var commandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
            var citizenParams = m_CitizenParametersQuery.GetSingleton<CitizenParametersData>();

            // === 宏观经济数据准备 ===
            var unemployedWorkplaces = m_CountWorkplacesSystem.GetUnemployedWorkspaceByLevel();
            var freeWorkplaces = m_CountWorkplacesSystem.GetFreeWorkplaces();
            var totalCapacity = m_CountWorkplacesSystem.GetTotalWorkplaces();

            int totalCapacitySum = 0;
            int totalFreeSum = 0;
            for (int i = 0; i < 5; i++)
            {
                totalCapacitySum += totalCapacity[i];
                totalFreeSum += math.max(0, freeWorkplaces[i]);
            }

            // === 等比缩放概率参数 ===
            // 原版分母 100 对应 ~10万人口城市；按 0.1% 空置率阈值等比缩放
            int unemployedProbDenom = math.max(100, totalCapacitySum / 1000);
            // 在职者门槛和分母：原版 100 和 500
            int employedThreshold = math.max(100, totalCapacitySum / 5000);
            int employedProbDenom = math.max(500, totalCapacitySum / 200);

            // 从 ModSettings 读取限流参数
            int seekerCap = Mod.Instance.Settings.JobSeekerCap;

            // === 1. 失业者找工作（无条件调度，与原版一致）===
            ScheduleJob(
                m_UnemployedQuery,
                unemployedWorkplaces,
                true,
                updateFrame,
                simulationFrame,
                commandBuffer,
                unemployedProbDenom,
                0, // 失业者不用 threshold
                0, // 失业者不用 employedDenom
                seekerCap
#if DEBUG
                , DBG_Offset_Unemployed
#endif
            );

            // === 2. 在职者跳槽（带宏观预筛选）===
            // 宏观预筛：总空缺 <= employedThreshold 时 Job 内必然全部 skip，直接跳过
            if (!m_EmployedQuery.IsEmpty && totalFreeSum > employedThreshold)
            {
                // 保持原版 switchRate 随机预筛选方向：NextFloat() > rate → 大概率通过
                if (RandomSeed.Next().GetRandom((int)simulationFrame).NextFloat(1f) > citizenParams.m_SwitchJobRate)
                {
                    ScheduleJob(
                        m_EmployedQuery,
                        freeWorkplaces,
                        false,
                        updateFrame,
                        simulationFrame,
                        commandBuffer,
                        0, // 在职者不用 unemployedDenom
                        employedThreshold,
                        employedProbDenom,
                        seekerCap
#if DEBUG
                        , DBG_Offset_Employed
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
            int unemployedProbDenom,
            int employedThreshold,
            int employedProbDenom,
            int maxSeekers
#if DEBUG
            , int debugOffset
#endif
            )
        {
            var job = new CitizenFindJobJob
            {
                // Handle Types
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_CitizenType = SystemAPI.GetComponentTypeHandle<Citizen>(),
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

                // 限流
                m_CreatedSeekerCount = m_CreatedSeekerCount,
                m_MaxSeekers = maxSeekers,

                // 等比缩放概率参数
                m_UnemployedProbDenom = unemployedProbDenom,
                m_EmployedThreshold = employedThreshold,
                m_EmployedProbDenom = employedProbDenom,

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
            int unempScan = m_DebugCounters[DBG_Offset_Unemployed + CNT_Total];
            int empScan = m_DebugCounters[DBG_Offset_Employed + CNT_Total];

            if (unempScan == 0 && empScan == 0) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[CitizenFindJobSystem] Frame {m_SimulationSystem.frameIndex}");

            if (unempScan > 0) AppendLogSection(sb, "Unemployed", DBG_Offset_Unemployed);
            if (empScan > 0) AppendLogSection(sb, "Employed  ", DBG_Offset_Employed);

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

            // 读写
            public ComponentTypeHandle<Citizen> m_CitizenType;
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            // 限流
            [NativeDisableUnsafePtrRestriction] public NativeArray<int> m_CreatedSeekerCount;
            [ReadOnly] public int m_MaxSeekers;

            // 等比缩放概率参数
            [ReadOnly] public int m_UnemployedProbDenom;  // max(100, totalCapacity/1000)
            [ReadOnly] public int m_EmployedThreshold;    // max(100, totalCapacity/5000)
            [ReadOnly] public int m_EmployedProbDenom;    // max(500, totalCapacity/200)

#if DEBUG
            [NativeDisableUnsafePtrRestriction] public NativeArray<int> m_DebugCounters;
            [ReadOnly] public int m_DebugOffset;
#endif

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // 检查分片帧是否匹配
                if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
                    return;

                // 软限流：如果本帧配额已满，直接返回（不遍历 chunk）
                if (m_CreatedSeekerCount[0] >= m_MaxSeekers)
                    return;

                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                NativeArray<Citizen> citizens = chunk.GetNativeArray(ref m_CitizenType);
                NativeArray<CurrentBuilding> currentBuildings = chunk.GetNativeArray(ref m_CurrentBuildingType);

                // 每个 Chunk 使用一个随机生成器
                Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);

                for (int i = 0; i < entities.Length; i++)
                {
#if DEBUG
                    unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Total]); }
#endif
                    // 软限流再次检查（每8个实体检查一次，减少原子操作开销）
                    if (i % 8 == 0 && m_CreatedSeekerCount[0] >= m_MaxSeekers)
                    {
                        return; // ⚠️ 不设冷却，下次分片轮到时自然重试
                    }

                    Entity citizenEntity = entities[i];
                    Citizen citizen = citizens[i];
                    Entity household = m_HouseholdMembers[citizenEntity].m_Household;
                    CitizenAge age = citizen.GetAge();

                    // === 1. 排除儿童和老人 ===
                    if (age == CitizenAge.Child || age == CitizenAge.Elderly)
                    {
                        if (m_IsUnemployedFindJob)
                        {
                            citizen.m_UnemploymentTimeCounter = 0f;
                            citizens[i] = citizen;
                        }
#if DEBUG
                        unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_Other]); }
#endif
                        continue;
                    }

                    // === 2. 冷却检查（原版固定范围 5000-10000）===
                    // HasJobSeeker 是 IEnableableComponent，disabled 实体仍有组件数据
                    if (m_HasJobSeekers.HasComponent(citizenEntity))
                    {
                        uint lastSeekFrame = m_HasJobSeekers[citizenEntity].m_LastJobSeekFrameIndex;
                        int cooldown = random.NextInt(kJobSeekCoolDownMin, kJobSeekCoolDownMax);

                        if (lastSeekFrame + cooldown > m_SimulationFrame)
                        {
                            if (m_IsUnemployedFindJob)
                            {
                                citizen.m_UnemploymentTimeCounter += kUnemploymentIncrement;
                                citizens[i] = citizen;
                            }
#if DEBUG
                            unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_CoolDown]); }
#endif
                            continue;
                        }
                    }

                    // === 3. 搬离检查 ===
                    if (m_MovingAways.HasComponent(household))
                    {
#if DEBUG
                        unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_Other]); }
#endif
                        continue;
                    }

                    int educationLevel = citizen.GetEducationLevel();

                    // === 4. 根据状态（失业/在职）判断是否尝试找工作 ===
                    if (m_IsUnemployedFindJob)
                    {
                        // --- 失业者逻辑 ---
                        citizen.m_UnemploymentTimeCounter += kUnemploymentIncrement;
                        citizens[i] = citizen;

                        // 计算所有不高于当前学历的可用岗位总数
                        int suitableWorkplaces = 0;
                        for (int level = 0; level <= educationLevel; level++)
                        {
                            if (m_AvailableWorkspacesByLevel[level] > 0)
                                suitableWorkplaces += m_AvailableWorkspacesByLevel[level];
                        }

                        // 等比缩放概率判定（原版逻辑的缩放版）
                        // 原版: suitableWorkplaces <= 0 || suitableWorkplaces < random.NextInt(100)
                        // 缩放: suitableWorkplaces <= 0 || suitableWorkplaces < random.NextInt(scaledDenom)
                        if (suitableWorkplaces <= 0 || suitableWorkplaces < random.NextInt(m_UnemployedProbDenom))
                        {
                            // 设置冷却（原版行为：无条件 SetComponent）
                            m_CommandBuffer.SetComponent(unfilteredChunkIndex, citizenEntity, new HasJobSeeker
                            {
                                m_Seeker = Entity.Null,
                                m_LastJobSeekFrameIndex = m_SimulationFrame
                            });
#if DEBUG
                            if (suitableWorkplaces <= 0)
                                unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_NoVacancy]); }
                            else
                                unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_RNG]); }
#endif
                            continue;
                        }

                        // 通过概率判定 → 进入创建流程
                    }
                    else
                    {
                        // --- 在职者逻辑 ---
                        citizen.m_UnemploymentTimeCounter = 0f;
                        citizens[i] = citizen;

                        NativeArray<Worker> workers = chunk.GetNativeArray(ref m_WorkerType);
                        Worker workerData = workers[i];

                        // 外部连接工作视为 0 级
                        int currentJobLevel = m_OutsideConnections.HasComponent(workerData.m_Workplace) ? 0 : workerData.m_Level;

                        // 如果当前工作等级 >= 学历，不跳槽
                        if (currentJobLevel >= educationLevel)
                        {
                            continue;
                        }

                        // 计算比当前更好的岗位数
                        int betterWorkplaces = 0;
                        for (int level = currentJobLevel; level <= educationLevel; level++)
                        {
                            if (m_AvailableWorkspacesByLevel[level] > 0)
                                betterWorkplaces += m_AvailableWorkspacesByLevel[level];
                        }

                        // 等比缩放概率判定（原版逻辑的缩放版）
                        // 原版: betterWorkplaces <= 100 || betterWorkplaces < random.NextInt(500)
                        if (betterWorkplaces <= m_EmployedThreshold || betterWorkplaces < random.NextInt(m_EmployedProbDenom))
                        {
                            m_CommandBuffer.SetComponent(unfilteredChunkIndex, citizenEntity, new HasJobSeeker
                            {
                                m_Seeker = Entity.Null,
                                m_LastJobSeekFrameIndex = m_SimulationFrame
                            });
#if DEBUG
                            if (betterWorkplaces <= m_EmployedThreshold)
                                unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_NoVacancy]); }
                            else
                                unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_RNG]); }
#endif
                            continue;
                        }

                        // 通过概率判定 → 进入创建流程
                    }

                    // === 5. 创建求职实体 (JobSeeker) ===
                    // 软限流：原子递增，超额则跳过（不设冷却）
                    unsafe
                    {
                        int* countPtr = (int*)m_CreatedSeekerCount.GetUnsafePtr();
                        int currentCount = Interlocked.Increment(ref countPtr[0]);

                        if (currentCount > m_MaxSeekers)
                        {
                            // ⚠️ 被限流：不设冷却、不更新时间戳
                            // 下次分片轮到时自然重试，概率判定仍然生效
#if DEBUG
                            Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Skip_Throttled]);
#endif
                            return; // 退出整个 chunk（此后本帧不再处理）
                        }
                    }

                    CreateJobSeekerEntity(unfilteredChunkIndex, citizenEntity, citizen, household, currentBuildings, i, educationLevel);
#if DEBUG
                    unsafe { Interlocked.Increment(ref ((int*)m_DebugCounters.GetUnsafePtr())[m_DebugOffset + CNT_Success]); }
#endif
                }
            } // Execute

            /// <summary>
            /// 创建 JobSeeker 实体并初始化相关组件
            /// </summary>
            private void CreateJobSeekerEntity(int sortKey, Entity citizenEntity, Citizen citizen, Entity household, NativeArray<CurrentBuilding> currentBuildings, int index, int educationLevel)
            {
                // 确定出发位置实体
                Entity startLocation = Entity.Null;

                if (!m_TouristHouseholds.HasComponent(household) && m_PropertyRenters.HasComponent(household))
                {
                    // 普通居民：家
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

                if (startLocation != Entity.Null)
                {
                    Entity seekerEntity = m_CommandBuffer.CreateEntity(sortKey);

                    m_CommandBuffer.AddComponent(sortKey, seekerEntity, new Owner { m_Owner = citizenEntity });
                    m_CommandBuffer.AddComponent(sortKey, seekerEntity, new JobSeeker
                    {
                        m_Level = (byte)educationLevel,
                        m_Outside = (byte)(((citizen.m_State & CitizenFlags.Commuter) != CitizenFlags.None) ? 1u : 0u)
                    });
                    m_CommandBuffer.AddComponent(sortKey, seekerEntity, new CurrentBuilding { m_CurrentBuilding = startLocation });

                    // 标记 Citizen 拥有正在寻找工作的 Agent
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
