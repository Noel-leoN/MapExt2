// Game.Simulation.HouseholdFindPropertySystem
// 独立系统，可进行ECS替换(便于修改常量和Loop)

// Game.Simulation.CitizenPathFindSetup + SetupFindHomeJob
// 特殊装箱调用，使用HarmonyPrefix修补。

using System;
using System.Reflection;
using System.Threading;
using Colossal.Entities;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Game.Zones;
using HarmonyLib;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstEcoSystemModeE
{
    /// <summary>
    /// 家庭寻找房产系统。
    /// 负责为无家可归者或想要搬家的家庭寻找合适的住所。
    /// 包括计算房产吸引力、处理寻路请求以及执行搬入逻辑。 
    /// </summary>
    // 主要流程分为两步：
    // 1. PreparePropertyJob：遍历所有可用的住宅物业，预先计算并缓存其质量得分和空余位置数量。
    // 2. FindPropertyJob：遍历需要寻找住所的家庭，发起寻路查询，以寻找附近且评分高的住所。
    // 3. CitizenPathFindSetup.SetupFindHomeJob 处理寻路，当寻路结果返回后，系统会评估最佳的房产，如果找到合适的，则为家庭安排租赁。

    // =========================================================================================
    // 1. Mod 自定义系统类型 (当前类)
    using ModSystem = HouseholdFindPropertySystemMod;
    // 2. 原版系统类型 (用于禁用和定位)
    using TargetSystem = HouseholdFindPropertySystem;
    // =========================================================================================

    public partial class HouseholdFindPropertySystemMod : GameSystemBase
    {


#if DEBUG
        public enum DebugStatIndex
        {
            TotalProcessed = 0,
            CooldownSkipped = 1,
            PathfindStarted = 2,
            PathfindResultReceived = 3,
            MovedIn = 4,
            FailedNoCandidate = 5,
            Count // 总数
        }

        // 新增：调试计数器
        private NativeArray<int> m_DebugStats;
        private bool m_EnableDebug = false; // 开发时设为 true，发布设为 false
#endif

        #region 内部数据结构

        /// <summary>
        /// 缓存的房产信息，用于在寻找过程中快速评估
        /// </summary>
        public struct CachedPropertyInformation
        {
            public GenericApartmentQuality quality; // 公寓质量评分
            public int free;                        // 剩余空位数量
        }

        /// <summary>
        /// 通用公寓质量指标
        /// </summary>
        public struct GenericApartmentQuality
        {
            public float apartmentSize;  // 公寓大小
            public float2 educationBonus; // 教育加成
            public float welfareBonus;    // 福利加成
            public float score;           // 综合评分
            public int level;             // 建筑等级
        }

        #endregion

        #region 常量与配置

        private const int UPDATE_INTERVAL = 16;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UPDATE_INTERVAL;

        public static readonly int kMaxProcessNormalHouseholdPerUpdate = 256; // 每帧处理的最大普通家庭数
        public static readonly int kMaxProcessHomelessHouseholdPerUpdate = 1024; // 每帧处理的最大无家可归家庭数
        public static readonly int kFindPropertyCoolDown = 2000; // 寻房冷却时间（帧）
                                                                 // 新增：对于失败者的惩罚性冷却（约0.5个游戏天）
        public static readonly int kFailedCoolDown = 15000;

        #endregion

        #region 查询与系统依赖

        // 实体查询
        private EntityQuery m_HouseholdQuery;         // 普通家庭查询
        private EntityQuery m_HomelessHouseholdQuery; // 无家可归家庭查询
        private EntityQuery m_FreePropertyQuery;      // 空闲房产查询

        // 参数查询（用于获取全局配置单例）
        private EntityQuery m_EconomyParameterQuery;
        private EntityQuery m_DemandParameterQuery;
        private EntityQuery m_HealthcareParameterQuery;
        private EntityQuery m_ParkParameterQuery;
        private EntityQuery m_EducationParameterQuery;
        private EntityQuery m_TelecomParameterQuery;
        private EntityQuery m_GarbageParameterQuery;
        private EntityQuery m_PoliceParameterQuery;
        private EntityQuery m_CitizenHappinessParameterQuery;

        // 依赖的其他系统
        private EndFrameBarrier m_EndFrameBarrier;
        private PathfindSetupSystem m_PathfindSetupSystem;
        private TaxSystem m_TaxSystem;
        private TriggerSystem m_TriggerSystem;
        private GroundPollutionSystem m_GroundPollutionSystem;
        private AirPollutionSystem m_AirPollutionSystem;
        private NoisePollutionSystem m_NoisePollutionSystem;
        private TelecomCoverageSystem m_TelecomCoverageSystem;
        private CitySystem m_CitySystem;
        private CityStatisticsSystem m_CityStatisticsSystem;
        private SimulationSystem m_SimulationSystem;
        private PropertyProcessingSystem m_PropertyProcessingSystem;
        private CountResidentialPropertySystem m_CountResidentialPropertySystem;

        #endregion

        #region 生命周期 (OnCreate, OnUpdate, GetUpdateInterval)


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

#if DEBUG
            // 使用 Persistent 分配，一直存在，不需要每帧 Dispose
            m_DebugStats = new NativeArray<int>((int)DebugStatIndex.Count, Allocator.Persistent);
#endif

            // 获取系统引用
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
            m_AirPollutionSystem = World.GetOrCreateSystemManaged<AirPollutionSystem>();
            m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
            m_TelecomCoverageSystem = World.GetOrCreateSystemManaged<TelecomCoverageSystem>();
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
            m_TriggerSystem = World.GetOrCreateSystemManaged<TriggerSystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_PropertyProcessingSystem = World.GetOrCreateSystemManaged<PropertyProcessingSystem>();
            m_CountResidentialPropertySystem = World.GetOrCreateSystemManaged<CountResidentialPropertySystem>();

            // 构建查询
            // 1. 无家可归家庭：包含 HomelessHousehold 组件，排除正在搬离、游客等
            m_HomelessHouseholdQuery = SystemAPI.QueryBuilder()
                .WithAllRW<HomelessHousehold, PropertySeeker>()
                .WithAll<HouseholdCitizen>()
                .WithNone<MovingAway, TouristHousehold, CommuterHousehold, CurrentBuilding, Deleted, Temp>()
                .Build();

            // 2. 普通家庭：包含 Household 组件，排除无家可归等
            m_HouseholdQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Household, PropertySeeker>()
                .WithAll<HouseholdCitizen>()
                .WithNone<HomelessHousehold, MovingAway, TouristHousehold, CommuterHousehold, CurrentBuilding, Deleted, Temp>()
                .Build();

            // 3. 空闲房产：查询所有可能有空位的建筑或公园
            var shelterDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Building>() },
                Any = new ComponentType[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Game.Buildings.Park>() },
                None = new ComponentType[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>() }
            };
            var residentialDesc = new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<PropertyOnMarket>(), ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<Building>() },
                None = new ComponentType[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Condemned>() }
            };
            m_FreePropertyQuery = GetEntityQuery(shelterDesc, residentialDesc);

            // 4. 参数查询初始化
            m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
            m_DemandParameterQuery = GetEntityQuery(ComponentType.ReadOnly<DemandParameterData>());
            m_HealthcareParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>());
            m_ParkParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ParkParameterData>());
            m_EducationParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EducationParameterData>());
            m_TelecomParameterQuery = GetEntityQuery(ComponentType.ReadOnly<TelecomParameterData>());
            m_GarbageParameterQuery = GetEntityQuery(ComponentType.ReadOnly<GarbageParameterData>());
            m_PoliceParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            m_CitizenHappinessParameterQuery = GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());

            // 确保核心参数存在才更新
            RequireForUpdate(m_EconomyParameterQuery);
            RequireForUpdate(m_HealthcareParameterQuery);
            RequireForUpdate(m_ParkParameterQuery);
            RequireForUpdate(m_EducationParameterQuery);
            RequireForUpdate(m_TelecomParameterQuery);
            RequireForUpdate(m_HouseholdQuery);
            RequireForUpdate(m_DemandParameterQuery);

        }

        protected override void OnUpdate()
        {
#if DEBUG
            // 0. 每帧开始前重置计数器 (或者每隔 N 帧)
            if (m_EnableDebug)
            {
                // 在主线程打印上一帧的数据
                // 为了防止 Log 刷屏，建议只在有搬入发生时，或者每隔 60 帧打印
                if (m_DebugStats[(int)DebugStatIndex.MovedIn] > 0 || UnityEngine.Time.frameCount % 64 == 0)
                {
                    UnityEngine.Debug.Log($"[FindHome] Total:{m_DebugStats[0]} | Cool:{m_DebugStats[1]} | StartPath:{m_DebugStats[2]} | Res:{m_DebugStats[3]} | OK:{m_DebugStats[4]} | Fail:{m_DebugStats[5]}");
                }

                // 清零
                for (int i = 0; i < (int)DebugStatIndex.Count; i++) m_DebugStats[i] = 0;
            }
#endif


            // 1. 准备污染和覆盖率数据
            NativeArray<GroundPollution> groundPollutionMap = m_GroundPollutionSystem.GetMap(true, out JobHandle groundDeps);
            NativeArray<AirPollution> airPollutionMap = m_AirPollutionSystem.GetMap(true, out JobHandle airDeps);
            NativeArray<NoisePollution> noiseMap = m_NoisePollutionSystem.GetMap(true, out JobHandle noiseDeps);
            CellMapData<TelecomCoverage> telecomCoverage = m_TelecomCoverageSystem.GetData(true, out JobHandle telecomDeps);

            // 2. 创建并调度 PreparePropertyJob
            // 该Job负责计算所有可用房产的质量和剩余空间，写入Hashmap中
            NativeParallelHashMap<Entity, CachedPropertyInformation> cachedPropertyInfo =
             new NativeParallelHashMap<Entity, CachedPropertyInformation>(m_FreePropertyQuery.CalculateEntityCount(), Allocator.TempJob);

            var prepareJob = new PreparePropertyJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_BuildingProperties = SystemAPI.GetComponentLookup<BuildingPropertyData>(false), // 虽然是Lookup，但在CountProperties中可能需要读取
                m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_BuildingDatas = SystemAPI.GetComponentLookup<BuildingData>(true),
                m_ParkDatas = SystemAPI.GetComponentLookup<ParkData>(true),
                m_Renters = SystemAPI.GetBufferLookup<Renter>(true),
                m_Households = SystemAPI.GetComponentLookup<Household>(true),
                m_Abandoneds = SystemAPI.GetComponentLookup<Abandoned>(true),
                m_Parks = SystemAPI.GetComponentLookup<Game.Buildings.Park>(true),
                m_SpawnableDatas = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true),
                m_BuildingPropertyData = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
                m_Buildings = SystemAPI.GetComponentLookup<Building>(true),
                m_ServiceCoverages = SystemAPI.GetBufferLookup<Game.Net.ServiceCoverage>(true),
                m_Crimes = SystemAPI.GetComponentLookup<CrimeProducer>(true),
                m_Locked = SystemAPI.GetComponentLookup<Locked>(true),
                m_Transforms = SystemAPI.GetComponentLookup<Transform>(true),
                m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(true),
                m_ElectricityConsumers = SystemAPI.GetComponentLookup<ElectricityConsumer>(true),
                m_WaterConsumers = SystemAPI.GetComponentLookup<WaterConsumer>(true),
                m_GarbageProducers = SystemAPI.GetComponentLookup<GarbageProducer>(true),
                m_MailProducers = SystemAPI.GetComponentLookup<MailProducer>(true),

                m_PollutionMap = groundPollutionMap,
                m_AirPollutionMap = airPollutionMap,
                m_NoiseMap = noiseMap,
                m_TelecomCoverages = telecomCoverage,

                // 单例数据获取
                m_HealthcareParameters = m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = m_ParkParameterQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = m_EducationParameterQuery.GetSingleton<EducationParameterData>(),
                m_TelecomParameters = m_TelecomParameterQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = m_GarbageParameterQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = m_PoliceParameterQuery.GetSingleton<PoliceConfigurationData>(),
                m_CitizenHappinessParameterData = m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),

                m_City = m_CitySystem.City,
                m_PropertyData = cachedPropertyInfo.AsParallelWriter()
            };

            // 组合依赖并调度 PrepareJob
            JobHandle prepareJobHandle = JobChunkExtensions.ScheduleParallel(
                prepareJob,
                m_FreePropertyQuery,
                JobUtils.CombineDependencies(Dependency, groundDeps, airDeps, noiseDeps, telecomDeps)
            );

            // 3. 关键修改：移除 ToEntityListAsync，改用 IJobChunk 并行处理
            // 这样可以处理 200万+ 家庭而不会在主线程卡顿

            NativeQueue<RentAction>.ParallelWriter rentActionQueue = m_PropertyProcessingSystem.GetRentActionQueue(out JobHandle rentQueueDeps).AsParallelWriter();
            NativeQueue<SetupQueueItem>.ParallelWriter pathfindQueue = m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter();

            var findJob = new FindPropertyParallelJob
            {
                // 输入句柄
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_PropertySeekerType = SystemAPI.GetComponentTypeHandle<PropertySeeker>(false),
                m_HouseholdType = SystemAPI.GetComponentTypeHandle<Household>(true),
                m_HomelessHouseholdType = SystemAPI.GetComponentTypeHandle<HomelessHousehold>(true),
                m_HouseholdCitizenType = SystemAPI.GetBufferTypeHandle<HouseholdCitizen>(true),
                m_PathInformationType = SystemAPI.GetBufferTypeHandle<PathInformations>(true), // ReadOnly? 可能是 RW 如果需要移除

                m_CachedPropertyInfo = cachedPropertyInfo,

                // Lookups
                m_BuildingDatas = SystemAPI.GetComponentLookup<BuildingData>(true),
                m_PropertiesOnMarket = SystemAPI.GetComponentLookup<PropertyOnMarket>(true),
                m_PrefabRefs = SystemAPI.GetComponentLookup<PrefabRef>(true),
                m_Buildings = SystemAPI.GetComponentLookup<Building>(true),
                m_Workers = SystemAPI.GetComponentLookup<Worker>(true),
                m_Students = SystemAPI.GetComponentLookup<Game.Citizens.Student>(true),
                m_BuildingProperties = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
                m_SpawnableDatas = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                m_Availabilities = SystemAPI.GetBufferLookup<ResourceAvailability>(true),
                m_ServiceCoverages = SystemAPI.GetBufferLookup<Game.Net.ServiceCoverage>(true),
                m_HomelessHouseholds = SystemAPI.GetComponentLookup<HomelessHousehold>(true),
                m_Citizens = SystemAPI.GetComponentLookup<Citizen>(true),
                m_Crimes = SystemAPI.GetComponentLookup<CrimeProducer>(true),
                m_Transforms = SystemAPI.GetComponentLookup<Transform>(true),
                m_Lockeds = SystemAPI.GetComponentLookup<Locked>(true),
                m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(true),
                m_HealthProblems = SystemAPI.GetComponentLookup<HealthProblem>(true),
                m_Parks = SystemAPI.GetComponentLookup<Game.Buildings.Park>(true),
                m_Abandoneds = SystemAPI.GetComponentLookup<Abandoned>(true),
                m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(true),
                m_ElectricityConsumers = SystemAPI.GetComponentLookup<ElectricityConsumer>(true),
                m_WaterConsumers = SystemAPI.GetComponentLookup<WaterConsumer>(true),
                m_GarbageProducers = SystemAPI.GetComponentLookup<GarbageProducer>(true),
                m_MailProducers = SystemAPI.GetComponentLookup<MailProducer>(true),
                m_Households = SystemAPI.GetComponentLookup<Household>(true),
                m_CurrentBuildings = SystemAPI.GetComponentLookup<CurrentBuilding>(true),
                m_CurrentTransports = SystemAPI.GetComponentLookup<CurrentTransport>(true),
                m_PathInformations = SystemAPI.GetComponentLookup<PathInformation>(true),
                // 注意：BufferLookup 用于随机访问，Chunk处理时直接访问Chunk数据效率更高

                // 数据
                m_AirPollutionMap = airPollutionMap,
                m_PollutionMap = groundPollutionMap,
                m_NoiseMap = noiseMap,
                m_TelecomCoverages = telecomCoverage,
                m_TaxRates = m_TaxSystem.GetTaxRates(),
                m_ResidentialPropertyData = m_CountResidentialPropertySystem.GetResidentialPropertyData(),
                m_HealthcareParameters = m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = m_ParkParameterQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = m_EducationParameterQuery.GetSingleton<EducationParameterData>(),
                m_TelecomParameters = m_TelecomParameterQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = m_GarbageParameterQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = m_PoliceParameterQuery.GetSingleton<PoliceConfigurationData>(),
                m_CitizenHappinessParameterData = m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
                m_EconomyParameters = m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),

                m_SimulationFrame = m_SimulationSystem.frameIndex,
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                m_City = m_CitySystem.City,

                m_PathfindQueue = pathfindQueue,
                m_RentActionQueue = rentActionQueue,

#if DEBUG
                // 传入 Debug 字段
                m_DebugStats = m_DebugStats,
                m_EnableDebug = m_EnableDebug
#endif
            };

            // 联合 Homeless 和 普通 Household 查询一起并行执行
            JobHandle jobHandle1 = JobChunkExtensions.ScheduleParallel(findJob, m_HomelessHouseholdQuery,
                JobHandle.CombineDependencies(prepareJobHandle, rentQueueDeps));

            JobHandle jobHandle2 = JobChunkExtensions.ScheduleParallel(findJob, m_HouseholdQuery,
                JobHandle.CombineDependencies(jobHandle1, rentQueueDeps));

            // 清理
            cachedPropertyInfo.Dispose(jobHandle2);

            Dependency = jobHandle2;
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
            m_PathfindSetupSystem.AddQueueWriter(Dependency);

            // 添加读取依赖，防止地图系统过早释放
            m_AirPollutionSystem.AddReader(Dependency);
            m_NoisePollutionSystem.AddReader(Dependency);
            m_GroundPollutionSystem.AddReader(Dependency);
            m_TelecomCoverageSystem.AddReader(Dependency);
            m_TriggerSystem.AddActionBufferWriter(Dependency);
            m_CityStatisticsSystem.AddWriter(Dependency);
            m_TaxSystem.AddReader(Dependency);
        }

        protected override void OnDestroy()
        {
#if DEBUG
            if (m_DebugStats.IsCreated) m_DebugStats.Dispose();
#endif
            base.OnDestroy();
        }

        #endregion

        #region Jobs

        /// <summary>
        /// 预处理房产Job
        /// 遍历所有潜在的住宅建筑，计算其剩余空间和质量评分，存入HashMap。
        /// </summary>
        [BurstCompile]
        private struct PreparePropertyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;

            // 组件查找表
            [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingProperties;
            [ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
            [ReadOnly] public BufferLookup<Renter> m_Renters;
            [ReadOnly] public ComponentLookup<Abandoned> m_Abandoneds;
            [ReadOnly] public ComponentLookup<Game.Buildings.Park> m_Parks;
            [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
            [ReadOnly] public ComponentLookup<ParkData> m_ParkDatas;
            [ReadOnly] public ComponentLookup<Household> m_Households;
            [ReadOnly] public ComponentLookup<Building> m_Buildings;
            [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;
            [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingPropertyData;
            [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverages;
            [ReadOnly] public ComponentLookup<CrimeProducer> m_Crimes;
            [ReadOnly] public ComponentLookup<Transform> m_Transforms;
            [ReadOnly] public ComponentLookup<Locked> m_Locked;
            [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
            [ReadOnly] public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;
            [ReadOnly] public ComponentLookup<WaterConsumer> m_WaterConsumers;
            [ReadOnly] public ComponentLookup<GarbageProducer> m_GarbageProducers;
            [ReadOnly] public ComponentLookup<MailProducer> m_MailProducers;

            // 环境数据
            [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
            [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
            [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
            [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;

            // 全局参数
            public HealthcareParameterData m_HealthcareParameters;
            public ParkParameterData m_ParkParameters;
            public EducationParameterData m_EducationParameters;
            public TelecomParameterData m_TelecomParameters;
            public GarbageParameterData m_GarbageParameters;
            public PoliceConfigurationData m_PoliceParameters;
            public CitizenHappinessParameterData m_CitizenHappinessParameterData;
            public Entity m_City;

            // 输出
            public NativeParallelHashMap<Entity, CachedPropertyInformation>.ParallelWriter m_PropertyData;

            /// <summary>
            /// 计算建筑内的空余位置
            /// </summary>
            //private int CalculateFree(Entity property)
            //{
            //    Entity prefab = m_Prefabs[property].m_Prefab;
            //    int freeCount = 0;

            //    // 1. 如果是避难所或允许流浪者的公园
            //    if (m_BuildingDatas.HasComponent(prefab) &&
            //       (m_Abandoneds.HasComponent(property) || (m_Parks.HasComponent(property) && m_ParkDatas[prefab].m_AllowHomeless)))
            //    {
            //        // 计算流浪者容量 - 当前租户数量
            //        freeCount = BuildingUtils.GetShelterHomelessCapacity(prefab, ref m_BuildingDatas, ref m_BuildingPropertyData) - m_Renters[property].Length;
            //    }
            //    // 2. 如果是普通住宅建筑
            //    else if (m_BuildingProperties.HasComponent(prefab))
            //    {
            //        BuildingPropertyData buildingPropertyData = m_BuildingProperties[prefab];
            //        DynamicBuffer<Renter> renters = m_Renters[property];
            //        // 总住宅属性数量
            //        freeCount = buildingPropertyData.CountProperties(AreaType.Residential);

            //        // 减去已被家庭占用的数量
            //        for (int i = 0; i < renters.Length; i++)
            //        {
            //            Entity renter = renters[i].m_Renter;
            //            if (m_Households.HasComponent(renter))
            //            {
            //                freeCount--;
            //            }
            //        }
            //    }
            //    return freeCount;
            //}

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    //int freeSlots = CalculateFree(entity);

                    Entity prefab = m_Prefabs[entity].m_Prefab;

                    int freeCount = 0;

                    // 计算空位逻辑 (简化版展示)
                    if (m_BuildingDatas.HasComponent(prefab) && (m_Abandoneds.HasComponent(entity) || m_Parks.HasComponent(entity)))
                    {
                        freeCount = BuildingUtils.GetShelterHomelessCapacity(prefab, ref m_BuildingDatas, ref m_BuildingProperties) - m_Renters[entity].Length;
                    }
                    else if (m_BuildingProperties.HasComponent(prefab))
                    {
                        freeCount = m_BuildingProperties[prefab].CountProperties(AreaType.Residential);
                        var renters = m_Renters[entity];
                        for (int r = 0; r < renters.Length; r++)
                            if (m_Households.HasComponent(renters[r].m_Renter)) freeCount--;
                    }

                    if (freeCount > 0)
                    {
                        //Entity prefab = m_Prefabs[entity].m_Prefab;
                        Building buildingData = m_Buildings[entity];
                        DynamicBuffer<CityModifier> cityModifiers = m_CityModifiers[m_City];

                        // 计算房产质量评分
                        GenericApartmentQuality quality = GetGenericApartmentQuality(
                            entity, prefab, ref buildingData, ref m_BuildingProperties, ref m_BuildingDatas,
                            ref m_SpawnableDatas, ref m_Crimes, ref m_ServiceCoverages, ref m_Locked,
                            ref m_ElectricityConsumers, ref m_WaterConsumers, ref m_GarbageProducers,
                            ref m_MailProducers, ref m_Transforms, ref m_Abandoneds,
                            m_PollutionMap, m_AirPollutionMap, m_NoiseMap, m_TelecomCoverages,
                            cityModifiers,
                            m_HealthcareParameters.m_HealthcareServicePrefab,
                            m_ParkParameters.m_ParkServicePrefab,
                            m_EducationParameters.m_EducationServicePrefab,
                            m_TelecomParameters.m_TelecomServicePrefab,
                            m_GarbageParameters.m_GarbageServicePrefab,
                            m_PoliceParameters.m_PoliceServicePrefab,
                            m_CitizenHappinessParameterData,
                            m_GarbageParameters
                        );

                        // 缓存结果
                        m_PropertyData.TryAdd(entity, new CachedPropertyInformation
                        {
                            free = freeCount,
                            quality = quality
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 寻找房产Job
        /// 处理家庭的搬迁逻辑，包括发起寻路请求、评估寻路结果、决定是否搬入。
        /// </summary>
        [BurstCompile]
        private
#if DEBUG
        unsafe
#endif
            struct FindPropertyParallelJob : IJobChunk // DEBUG必须标记为 unsafe
        {
            //public NativeList<Entity> m_HomelessHouseholdEntities;
            //public NativeList<Entity> m_MovedInHouseholdEntities;
            public NativeParallelHashMap<Entity, CachedPropertyInformation> m_CachedPropertyInfo;

            // --- 实体句柄 ---
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<Household> m_HouseholdType;
            //[ReadOnly] public ComponentTypeHandle<PropertySeeker> m_PropertySeekerType;
            [ReadOnly] public ComponentTypeHandle<HomelessHousehold> m_HomelessHouseholdType;
            [ReadOnly] public BufferTypeHandle<HouseholdCitizen> m_HouseholdCitizenType;

            // 读写组件
            public ComponentTypeHandle<PropertySeeker> m_PropertySeekerType;
            public BufferTypeHandle<PathInformations> m_PathInformationType; // RW，需要移除Pending状态

            // 大量组件Lookup用于评分和判断
            [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
            [ReadOnly] public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;
            //[ReadOnly] public BufferLookup<PathInformations> m_PathInformationBuffers;
            [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefs;
            [ReadOnly] public ComponentLookup<Building> m_Buildings;
            [ReadOnly] public ComponentLookup<Worker> m_Workers;
            [ReadOnly] public ComponentLookup<Game.Citizens.Student> m_Students;
            [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingProperties;
            [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;
            [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
            [ReadOnly] public BufferLookup<ResourceAvailability> m_Availabilities;
            [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverages;
            [ReadOnly] public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;
            [ReadOnly] public ComponentLookup<Citizen> m_Citizens;
            [ReadOnly] public ComponentLookup<CrimeProducer> m_Crimes;
            [ReadOnly] public ComponentLookup<Transform> m_Transforms;
            [ReadOnly] public ComponentLookup<Locked> m_Lockeds;
            [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
            [ReadOnly] public ComponentLookup<HealthProblem> m_HealthProblems;
            [ReadOnly] public ComponentLookup<Game.Buildings.Park> m_Parks;
            [ReadOnly] public ComponentLookup<Abandoned> m_Abandoneds;
            [ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles;
            [ReadOnly] public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;
            [ReadOnly] public ComponentLookup<WaterConsumer> m_WaterConsumers;
            [ReadOnly] public ComponentLookup<GarbageProducer> m_GarbageProducers;
            [ReadOnly] public ComponentLookup<MailProducer> m_MailProducers;
            [ReadOnly] public ComponentLookup<Household> m_Households;// Lookup needed for other entities
            [ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildings;
            [ReadOnly] public ComponentLookup<CurrentTransport> m_CurrentTransports;
            [ReadOnly] public ComponentLookup<PathInformation> m_PathInformations;
            //[ReadOnly] public BufferLookup<HouseholdCitizen> m_CitizenBuffers;
            // m_CitizenBuffers 不需要Lookup，从Chunk获取

            // 环境参数
            [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
            [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
            [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
            [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;
            [ReadOnly] public NativeArray<int> m_TaxRates;
            [ReadOnly] public CountResidentialPropertySystem.ResidentialPropertyData m_ResidentialPropertyData;

            // 服务参数
            [ReadOnly] public HealthcareParameterData m_HealthcareParameters;
            [ReadOnly] public ParkParameterData m_ParkParameters;
            [ReadOnly] public EducationParameterData m_EducationParameters;
            [ReadOnly] public TelecomParameterData m_TelecomParameters;
            [ReadOnly] public GarbageParameterData m_GarbageParameters;
            [ReadOnly] public PoliceConfigurationData m_PoliceParameters;
            [ReadOnly] public CitizenHappinessParameterData m_CitizenHappinessParameterData;
            [ReadOnly] public EconomyParameterData m_EconomyParameters;

            [ReadOnly] public uint m_SimulationFrame;
            [ReadOnly] public Entity m_City;

            // 输出
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
            public NativeQueue<RentAction>.ParallelWriter m_RentActionQueue;

#if DEBUG
            // 允许并行写入的 Attribute
            [NativeDisableParallelForRestriction]
            public NativeArray<int> m_DebugStats;
            public bool m_EnableDebug;
#endif

            /// <summary>
            /// 发起寻路请求，帮助家庭寻找新住处
            /// </summary>

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
#if DEBUG
                // 获取原始指针 (极快，无 GC) 用于原子操作
                int* statsPtr = (int*)m_DebugStats.GetUnsafePtr(); 
#endif

                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                NativeArray<PropertySeeker> seekers = chunk.GetNativeArray(ref m_PropertySeekerType);
                NativeArray<Household> householdDatas = chunk.GetNativeArray(ref m_HouseholdType);
                BufferAccessor<HouseholdCitizen> citizenBuffers = chunk.GetBufferAccessor(ref m_HouseholdCitizenType);

                // 可选组件
                bool hasHomeless = chunk.Has(ref m_HomelessHouseholdType);
                NativeArray<HomelessHousehold> homelessDatas = hasHomeless ? chunk.GetNativeArray(ref m_HomelessHouseholdType) : default;

                bool hasPathInfos = chunk.Has(ref m_PathInformationType);
                BufferAccessor<PathInformations> pathInfoBuffers = hasPathInfos ? chunk.GetBufferAccessor(ref m_PathInformationType) : default;

                for (int i = 0; i < entities.Length; i++)
                {
#if DEBUG
                    // 统计总数
                    if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.TotalProcessed]);

#endif

                    Entity entity = entities[i];
                    PropertySeeker seeker = seekers[i];
                    Household household = householdDatas[i];
                    DynamicBuffer<HouseholdCitizen> citizens = citizenBuffers[i];

                    // 跳过空家庭
                    if (citizens.Length == 0) continue;

                    // int householdIncome = EconomyUtils.GetHouseholdIncome(citizens, ref m_Workers, ref m_Citizens, ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);

                    // 1. 处理寻路结果
                    bool isPathFinding = false;
                    if (hasPathInfos && pathInfoBuffers.Length > i)
                    {
                        DynamicBuffer<PathInformations> pathInfos = pathInfoBuffers[i];
                        if (pathInfos.Length > 0)
                        {
#if DEBUG
                            if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.PathfindResultReceived]);

#endif
                            // 如果状态是 Pending，说明寻路还在进行中，跳过此人，等待下一帧
                            if ((pathInfos[0].m_State & PathFlags.Pending) != 0)
                            {
                                isPathFinding = true; // 正在寻路中，不要再发起新请求，也不要进入冷却检查
                            }
                            else
                            {
                                // 寻路已完成，处理结果
#if DEBUG
                                if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.PathfindResultReceived]); 
#endif

                                ProcessPathInformations(entity, pathInfos, ref seeker, citizens, unfilteredChunkIndex
                                    #if DEBUG
		, statsPtr 
	#endif
                                    ); // 收入传0即可
                                seekers[i] = seeker;
                                continue; // 处理完毕，退出
                            }
                        }
                    }

                    // 如果正在寻路中，直接跳过后续逻辑
                    if (isPathFinding) continue;

                    // =========================================================
                    // 2. 获取当前住所 (关键修正：在这里定义变量)
                    // =========================================================
                    // 即使是冷却中，我们也需要知道他是不是流浪汉，这决定了冷却策略
                    // 【修复 1】：将冷却检查移到最外层，对所有人都生效
                    // 逻辑：如果在冷却期内，且不是无家可归者急需找房（或者你可以让无家可归者也遵守冷却），则跳过
                    Entity currentHome = GetHouseholdHomeBuilding(entity, ref m_PropertyRenters, ref m_HomelessHouseholds);
                    bool isHomeless = currentHome == Entity.Null;

                    // =========================================================
                    // 3. 冷却检查 (Cooldown)
                    // =========================================================
                    // 流浪汉(Homeless)通常比较急，可以设置较短的冷却，或者和普通人一样
                    if (seeker.m_LastPropertySeekFrame + kFindPropertyCoolDown > m_SimulationFrame)
                    {
#if DEBUG
                        if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.CooldownSkipped]);
#endif
                        if (isHomeless)
                        {                           
                            // 简单的逃离检查逻辑，全城总空房少于10
                            if (math.csum(m_ResidentialPropertyData.m_FreeProperties) < 10)
                                CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, entity, MoveAwayReason.NoSuitableProperty);
                        
                        }
                        continue; // 【关键】冷却中，跳过！
                    }

                    //                    // 2. 检查冷却
                    //                    Entity currentHome = GetHouseholdHomeBuilding(entity, ref m_PropertyRenters, ref m_HomelessHouseholds);
                    //                    if (currentHome == Entity.Null && seeker.m_LastPropertySeekFrame + HouseholdFindPropertySystemMod.kFindPropertyCoolDown > m_SimulationFrame)
                    //                    {
                    //#if DEBUG
                    //                        if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.CooldownSkipped]); 
                    //#endif

                    //                        // 简单逃离逻辑
                    //                        if (math.csum(m_ResidentialPropertyData.m_FreeProperties) < 10 && !hasPathInfos) // 如果没在寻路且没房
                    //                        {
                    //                            CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, entity, MoveAwayReason.NoSuitableProperty);
                    //                        }
                    //                        continue;
                    //                    }

                    // =========================================================
                    // 4. 随机限流 (关键性能优化)
                    // =========================================================
                    // 即使冷却结束了，也不要让几万人在同一帧全部发起寻路。
                    // 我们引入一个随机概率，将负载分散到接下来的几十帧里。

                    // 使用 Entity Index 和 帧数 制造伪随机
                    var random = Unity.Mathematics.Random.CreateFromIndex((uint)(entity.Index + m_SimulationFrame));

                    // 设定概率：比如只有 5% 的人能在冷却结束的当帧发起请求
                    // 流浪汉可以给高一点的优先级 (比如 20%)
                    float chance = isHomeless ? 0.10f : 0.05f;

                    if (random.NextFloat() > chance)
                    {
                        // 虽然冷却好了，但这次轮空。
                        // 技巧：把 LastSeekFrame 设为最近的某个时间，让它在未来几帧内再次尝试，而不是下一帧立刻重试
                        // 这样能完美地把压力抹平
                        seeker.m_LastPropertySeekFrame = m_SimulationFrame - (uint)random.NextInt(100, 500);
                        seekers[i] = seeker;
                        continue;
                    }

                    // =========================================================
                    // 5. 评估当前状态 & 发起寻路
                    // =========================================================
                    float currentScore = float.NegativeInfinity;
                    Entity currentHomeForScore = GetHouseholdHomeBuilding(entity, ref m_PropertyRenters, ref m_HomelessHouseholds);

                    // 3. 评估当前状态 (如果已有房子)

                    //if (currentHomeForScore != Entity.Null)
                    //{
                    //    // 为了性能，增加随机概率检查：只有 2% 的有房家庭在冷却结束后会真正尝试计算分数搬家
                    //    // 这样可以避免一波冷却结束后所有人同时发起寻路
                    //    // 使用 entity.Index 做伪随机，或者引入 Random
                    //    var random = Unity.Mathematics.Random.CreateFromIndex((uint)(entity.Index + m_SimulationFrame));
                    //    if (random.NextFloat() > 0.02f)
                    //    {
                    //        // 虽然冷却好了，但这次算了，稍微推迟一下，避免峰值
                    //        seeker.m_LastPropertySeekFrame = m_SimulationFrame - (uint)random.NextInt(100, 500); // 设为不久前刚搜过，稍后重试
                    //        seekers[i] = seeker;
                    //        continue;
                    //    }

                    //    currentScore = GetPropertyScore(...);
                    //}

                    // 3. 评估当前状态
                    //float currentScore = float.NegativeInfinity;
                    if (currentHome != Entity.Null)
                    {
                        // 这里可以直接调用 GetPropertyScore，但如果想极致优化，可以只在特定间隔调用
                        currentScore = GetPropertyScore(
                            currentHome, entity, citizens,
                            ref m_PrefabRefs, ref m_BuildingProperties, ref m_Buildings, ref m_BuildingDatas,
                            ref m_Households, ref m_Citizens, ref m_Students, ref m_Workers, ref m_SpawnableDatas,
                            ref m_Crimes, ref m_ServiceCoverages, ref m_Lockeds, ref m_ElectricityConsumers,
                            ref m_WaterConsumers, ref m_GarbageProducers, ref m_MailProducers, ref m_Transforms,
                            ref m_Abandoneds, ref m_Parks, ref m_Availabilities, m_TaxRates, m_PollutionMap,
                            m_AirPollutionMap, m_NoiseMap, m_TelecomCoverages, m_CityModifiers[m_City],
                            m_HealthcareParameters.m_HealthcareServicePrefab, m_ParkParameters.m_ParkServicePrefab,
                            m_EducationParameters.m_EducationServicePrefab, m_TelecomParameters.m_TelecomServicePrefab,
                            m_GarbageParameters.m_GarbageServicePrefab, m_PoliceParameters.m_PoliceServicePrefab,
                            m_CitizenHappinessParameterData, m_GarbageParameters
                        );
                    }

                    // 4. 发起寻路
                    Entity commuter = Entity.Null;
                    Entity workOrSchool = GetFirstWorkplaceOrSchool(citizens, ref commuter);
                    bool targetIsOrigin = (workOrSchool == Entity.Null);
                    Entity searchOrigin = targetIsOrigin ? GetCurrentLocation(citizens) : workOrSchool;

                    if (currentHome == Entity.Null && searchOrigin == Entity.Null)
                    {
                        CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, entity, MoveAwayReason.NoSuitableProperty);
                        continue;
                    }

                    // 更新 Seeker 状态
                    seeker.m_TargetProperty = workOrSchool;
                    seeker.m_BestProperty = currentHome;
                    seeker.m_BestPropertyScore = currentScore;
                    seeker.m_LastPropertySeekFrame = m_SimulationFrame;
                    seekers[i] = seeker;

                    // 【关键】更新冷却时间，防止下一帧立刻重试
                    seeker.m_LastPropertySeekFrame = m_SimulationFrame;
                    seekers[i] = seeker;

                    StartHomeFinding(entity, commuter, searchOrigin, currentHome, currentScore, targetIsOrigin, citizens, unfilteredChunkIndex);

#if DEBUG
                    if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.PathfindStarted]);

#endif
                }
            }

            /// <summary>
            /// 处理寻路返回的候选房产列表
            /// </summary>
            private void ProcessPathInformations(Entity householdEntity, DynamicBuffer<PathInformations> pathInformations, ref PropertySeeker propertySeeker, DynamicBuffer<HouseholdCitizen> citizens, int sortKey
#if DEBUG
        , int* statsPtr 
#endif
                )
            {
                // 寻找最佳候选
                Entity bestCandidate = Entity.Null;
                // float bestCandidateScore = float.NegativeInfinity;
                bool hasTarget = propertySeeker.m_TargetProperty != Entity.Null;

                // 遍历所有寻路返回的结果 (通常是按 Cost 排序，即分数从高到低)
                for (int i = 0; i < pathInformations.Length; i++)
                {
                    var pathInfo = pathInformations[i];
                    Entity candidate = hasTarget ? pathInfo.m_Origin : pathInfo.m_Destination;

                    // 1. 快速有效性检查：必须在缓存中且有空位
                    // 注意：这里读 HashMap 是安全的，但不能写 (free--)
                    if (!m_CachedPropertyInfo.TryGetValue(candidate, out var cachedInfo) || cachedInfo.free <= 0)
                    {
                        continue;
                    }

                    // 2. 详细评分 (如果需要，或者直接信任 Pathfinding 的结果顺序)
                    // 由于 SetupJob 已经做了大量计算，这里可以信任第一个符合条件的
                    bestCandidate = candidate;
                    break; // 找到第一个有效的就停止
                }

                if (bestCandidate != Entity.Null)
                {
#if DEBUG
                    // 搬入成功
                    if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.MovedIn]);

#endif

                    // 决定搬入
                    // 这里不再进行复杂的 Score 重算，因为 SetupJob 已经算过了。
                    // 除非需要对比 CurrentHome 的分数 (propertySeeker.m_BestPropertyScore)

                    // 发送租赁动作
                    m_RentActionQueue.Enqueue(new RentAction
                    {
                        m_Property = bestCandidate,
                        m_Renter = householdEntity
                    });

                    // 防止在等待租赁结果的几帧内重复发起搜索，设置一个小冷却
                    propertySeeker.m_LastPropertySeekFrame = m_SimulationFrame + 200;

                    // 不再手动减少 cachedInfo.free，避免竞态。
                    // 让 SimulationSystem 在下一帧处理失败的情况（如果被抢占了）。
                    m_CommandBuffer.SetComponentEnabled<PropertySeeker>(sortKey, householdEntity, false);
                }
                else
                {
#if DEBUG
                    // 失败
                    if (m_EnableDebug) Interlocked.Increment(ref statsPtr[(int)DebugStatIndex.FailedNoCandidate]);

#endif
                    // [FIX] 失败：没有候选 (可能买不起，或没空房)
                    // 施加惩罚性冷却，防止死循环洪水
                    propertySeeker.m_LastPropertySeekFrame = m_SimulationFrame + (uint)kFailedCoolDown;

                    // 重置最佳记录，让其下次降低标准
                    propertySeeker.m_BestProperty = Entity.Null;
                    propertySeeker.m_BestPropertyScore = float.NegativeInfinity;

                    // 流浪者多次没找到
                    if (m_HomelessHouseholds.HasComponent(householdEntity) && m_HomelessHouseholds[householdEntity].m_TempHome == Entity.Null)
                    {
                        // 失败多次后强制离开
                        int failcount = 0;failcount++;
                        if (failcount == 3)
                            CitizenUtils.HouseholdMoveAway(m_CommandBuffer, sortKey, householdEntity, MoveAwayReason.NoSuitableProperty);
                    }
                }

                // 【关键修复】：处理完毕后，必须移除 PathInformation 和 PathInformations 组件！
                // 否则下一帧还会重复处理这个结果。
                // 1. 移除路径数据 Buffer
                m_CommandBuffer.RemoveComponent<PathInformations>(sortKey, householdEntity);
                // 2. 移除路径状态 Component (新增修复)
                m_CommandBuffer.RemoveComponent<PathInformation>(sortKey, householdEntity);

            }

            private void StartHomeFinding(Entity household, Entity commuteCitizen, Entity targetLocation, Entity oldHome, float minimumScore, bool targetIsOrigin, DynamicBuffer<HouseholdCitizen> citizens, int sortKey)
            {
                // 标记PathInfo为挂起状态
                //m_CommandBuffer.AddComponent(sortKey, household, new PathInformation { m_State = PathFlags.Pending });
                m_CommandBuffer.AddComponent(sortKey, household, new PathInformation { m_State = PathFlags.Pending });


                // 计算寻路权重
                Household householdData = m_Households[household];
                PathfindWeights weights = default;
                if (m_Citizens.TryGetComponent(commuteCitizen, out var citizenData))
                {
                    weights = CitizenUtils.GetPathfindWeights(citizenData, householdData, citizens.Length);
                }
                else
                {
                    // 如果没有主要通勤者，平均计算所有成员
                    for (int i = 0; i < citizens.Length; i++)
                    {
                        if (m_Citizens.TryGetComponent(citizens[i].m_Citizen, out var cData))
                        {
                            weights.m_Value += CitizenUtils.GetPathfindWeights(cData, householdData, citizens.Length).m_Value;
                        }
                    }
                    weights.m_Value *= 1f / (float)citizens.Length;
                }

                // 设置寻路参数
                PathfindParameters parameters = new PathfindParameters
                {
                    m_MaxSpeed = 111.111115f,
                    m_WalkSpeed = 1.6666667f,
                    m_Weights = weights,
                    m_Methods = (PathMethod.Pedestrian | PathMethod.PublicTransportDay),
                    m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost,
                    m_PathfindFlags = (PathfindFlags.Simplified | PathfindFlags.IgnorePath)
                };

                // 设置起点和终点
                SetupQueueTarget targetA = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = targetLocation
                };
                SetupQueueTarget targetB = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.FindHome,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = household,
                    m_Entity2 = oldHome,
                    m_Value2 = minimumScore
                };

                // 如果拥有车辆，调整寻路规则
                if (m_OwnedVehicles.TryGetBuffer(household, out var vehicles) && vehicles.Length != 0)
                {
                    parameters.m_Methods |= (PathMethod)(targetIsOrigin ? 8194 : 8198); // Road | Medium Road
                    parameters.m_ParkingSize = float.MinValue;
                    parameters.m_IgnoredRules |= RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic | RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles;
                    targetA.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    targetA.m_RoadTypes |= RoadTypes.Car;
                    targetB.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    targetB.m_RoadTypes |= RoadTypes.Car;
                }

                if (targetIsOrigin)
                {
                    parameters.m_MaxSpeed.y = 277.77777f;
                    parameters.m_Methods |= PathMethod.Taxi | PathMethod.PublicTransportNight;
                    parameters.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                }
                else
                {
                    CommonUtils.Swap(ref targetA, ref targetB);
                }

                parameters.m_MaxResultCount = 10;
                parameters.m_PathfindFlags |= (PathfindFlags)(targetIsOrigin ? 256 : 128); // MultipleDestinations : MultipleOrigins

                // 添加路径缓冲区
                m_CommandBuffer.AddBuffer<PathInformations>(sortKey, household).Add(new PathInformations { m_State = PathFlags.Pending });


                // 入队寻路任务
                SetupQueueItem item = new SetupQueueItem(household, parameters, targetA, targetB);
                m_PathfindQueue.Enqueue(item);
            }

            // 获取家庭中第一个有工作或上学的成员的目的地（工作场所或学校）
            private Entity GetFirstWorkplaceOrSchool(DynamicBuffer<HouseholdCitizen> citizens, ref Entity citizen)
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    citizen = citizens[i].m_Citizen;
                    if (m_Workers.HasComponent(citizen))
                        return m_Workers[citizen].m_Workplace;
                    if (m_Students.HasComponent(citizen))
                        return m_Students[citizen].m_School;
                }
                return Entity.Null;
            }

            // 获取家庭成员当前的物理位置
            private Entity GetCurrentLocation(DynamicBuffer<HouseholdCitizen> citizens)
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    if (m_CurrentBuildings.TryGetComponent(citizens[i].m_Citizen, out var building))
                        return building.m_CurrentBuilding;
                    if (m_CurrentTransports.TryGetComponent(citizens[i].m_Citizen, out var transport))
                        return transport.m_CurrentTransport;
                }
                return Entity.Null;
            }

            // --- 辅助方法: 计算收入 (简化版) ---
            //public int GetHouseholdIncome(DynamicBuffer<HouseholdCitizen> citizens)
            //{
            //    // 这里应调用 EconomyUtils.GetHouseholdIncome，为性能内联或简化
            //    // 假设已引用 EconomyUtils
            //    return EconomyUtils.GetHouseholdIncome(citizens, ref m_Workers, ref m_Citizens, ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);
            //}
        }

        #endregion

        #region  辅助静态方法
        public static GenericApartmentQuality GetGenericApartmentQuality(
                Entity building,
                Entity buildingPrefab,
                ref Building buildingData,
                ref ComponentLookup<BuildingPropertyData> buildingProperties,
                ref ComponentLookup<BuildingData> buildingDatas,
                ref ComponentLookup<SpawnableBuildingData> spawnableDatas,
                ref ComponentLookup<CrimeProducer> crimes,
                ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages,
                ref ComponentLookup<Locked> locked,
                ref ComponentLookup<ElectricityConsumer> electricityConsumers,
                ref ComponentLookup<WaterConsumer> waterConsumers,
                ref ComponentLookup<GarbageProducer> garbageProducers,
                ref ComponentLookup<MailProducer> mailProducers,
                ref ComponentLookup<Transform> transforms,
                ref ComponentLookup<Abandoned> abandoneds,
                NativeArray<GroundPollution> pollutionMap,
                NativeArray<AirPollution> airPollutionMap,
                NativeArray<NoisePollution> noiseMap,
                CellMapData<TelecomCoverage> telecomCoverages,
                DynamicBuffer<CityModifier> cityModifiers,
                Entity healthcareService,
                Entity entertainmentService,
                Entity educationService,
                Entity telecomService,
                Entity garbageService,
                Entity policeService,
                CitizenHappinessParameterData happinessParameterData,
                GarbageParameterData garbageParameterData)
        {
            // 原PropertyUtils class常量
            float kHomelessApartmentSize = 0.01f;

            GenericApartmentQuality result = default(GenericApartmentQuality);

            // 原 flag: 用于标记是否为无家可归/非正常住宅状态
            bool isHomeless = true;

            BuildingPropertyData buildingPropertyData = default(BuildingPropertyData);
            SpawnableBuildingData spawnableBuildingData = default(SpawnableBuildingData);

            if (buildingProperties.HasComponent(buildingPrefab))
            {
                buildingPropertyData = buildingProperties[buildingPrefab];
                isHomeless = false;
            }

            // 原 buildingData2: 区分于运行时的 buildingData，这是预制体数据
            BuildingData prefabData = buildingDatas[buildingPrefab];

            if (spawnableDatas.HasComponent(buildingPrefab) && !abandoneds.HasComponent(building))
            {
                spawnableBuildingData = spawnableDatas[buildingPrefab];
            }
            else
            {
                isHomeless = true;
            }

            // 计算公寓大小
            result.apartmentSize = (isHomeless ? kHomelessApartmentSize : (buildingPropertyData.m_SpaceMultiplier * (float)prefabData.m_LotSize.x * (float)prefabData.m_LotSize.y / math.max(1f, buildingPropertyData.m_ResidentialProperties)));
            result.level = spawnableBuildingData.m_Level;

            // 原 @int: 用于累加各项幸福度/分数的变量
            int2 totalScoreAccumulator = default(int2);

            // 原 healthcareBonuses: 这个变量被复用于存储每一次计算的临时加成，不只针对医疗
            int2 currentStepBonus;

            // 1. 计算服务覆盖带来的加成（医疗、娱乐、教育、福利）
            if (serviceCoverages.HasBuffer(buildingData.m_RoadEdge))
            {
                DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = serviceCoverages[buildingData.m_RoadEdge];

                currentStepBonus = CitizenHappinessSystem.GetHealthcareBonuses(buildingData.m_CurvePosition, serviceCoverage, ref locked, healthcareService, in happinessParameterData);
                totalScoreAccumulator += currentStepBonus;

                currentStepBonus = CitizenHappinessSystem.GetEntertainmentBonuses(buildingData.m_CurvePosition, serviceCoverage, cityModifiers, ref locked, entertainmentService, in happinessParameterData);
                totalScoreAccumulator += currentStepBonus;

                result.welfareBonus = CitizenHappinessSystem.GetWelfareValue(buildingData.m_CurvePosition, serviceCoverage, in happinessParameterData);
                result.educationBonus = CitizenHappinessSystem.GetEducationBonuses(buildingData.m_CurvePosition, serviceCoverage, ref locked, educationService, in happinessParameterData, 1);
            }

            // 2. 计算犯罪加成/惩罚
            int2 crimeBonuses = CitizenHappinessSystem.GetCrimeBonuses(default(CrimeVictim), building, ref crimes, ref locked, policeService, in happinessParameterData);
            // 如果是无家可归状态，应用特定的犯罪惩罚逻辑
            currentStepBonus = (isHomeless ? new int2(0, -happinessParameterData.m_MaxCrimePenalty - crimeBonuses.y) : crimeBonuses);
            totalScoreAccumulator += currentStepBonus;

            // 3. 地面污染
            currentStepBonus = CellMapSystemRe.GetGroundPollutionBonuses(building, ref transforms, pollutionMap, cityModifiers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 4. 空气污染
            currentStepBonus = CellMapSystemRe.GetAirPollutionBonuses(building, ref transforms, airPollutionMap, cityModifiers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 5. 噪音污染
            currentStepBonus = CellMapSystemRe.GetNoiseBonuses(building, ref transforms, noiseMap, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 6. 电信覆盖
            currentStepBonus = CitizenHappinessSystem.GetTelecomBonuses(building, ref transforms, telecomCoverages, ref locked, telecomService, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 7. 电力供应
            currentStepBonus = PropertyUtils.GetElectricityBonusForApartmentQuality(building, ref electricityConsumers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 8. 用水供应
            currentStepBonus = PropertyUtils.GetWaterBonusForApartmentQuality(building, ref waterConsumers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 9. 污水处理
            currentStepBonus = PropertyUtils.GetSewageBonusForApartmentQuality(building, ref waterConsumers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 10. 水污染
            currentStepBonus = CitizenHappinessSystem.GetWaterPollutionBonuses(building, ref waterConsumers, cityModifiers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 11. 垃圾处理
            currentStepBonus = CitizenHappinessSystem.GetGarbageBonuses(building, ref garbageProducers, ref locked, garbageService, in garbageParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 12. 邮件服务
            currentStepBonus = CitizenHappinessSystem.GetMailBonuses(building, ref mailProducers, ref locked, telecomService, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 13. 无家可归状态修正
            if (isHomeless)
            {
                currentStepBonus = CitizenHappinessSystem.GetHomelessBonuses(in happinessParameterData);
                totalScoreAccumulator += currentStepBonus;
            }

            result.score = totalScoreAccumulator.x + totalScoreAccumulator.y;
            return result;
        }

        public static float GetPropertyScore(Entity property, Entity household, DynamicBuffer<HouseholdCitizen> citizenBuffer, ref ComponentLookup<PrefabRef> prefabRefs, ref ComponentLookup<BuildingPropertyData> buildingProperties, ref ComponentLookup<Building> buildings, ref ComponentLookup<BuildingData> buildingDatas, ref ComponentLookup<Household> households, ref ComponentLookup<Citizen> citizens, ref ComponentLookup<Game.Citizens.Student> students, ref ComponentLookup<Worker> workers, ref ComponentLookup<SpawnableBuildingData> spawnableDatas, ref ComponentLookup<CrimeProducer> crimes, ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages, ref ComponentLookup<Locked> locked, ref ComponentLookup<ElectricityConsumer> electricityConsumers, ref ComponentLookup<WaterConsumer> waterConsumers, ref ComponentLookup<GarbageProducer> garbageProducers, ref ComponentLookup<MailProducer> mailProducers, ref ComponentLookup<Transform> transforms, ref ComponentLookup<Abandoned> abandoneds, ref ComponentLookup<Game.Buildings.Park> parks, ref BufferLookup<ResourceAvailability> availabilities, NativeArray<int> taxRates, NativeArray<GroundPollution> pollutionMap, NativeArray<AirPollution> airPollutionMap, NativeArray<NoisePollution> noiseMap, CellMapData<TelecomCoverage> telecomCoverages, DynamicBuffer<CityModifier> cityModifiers, Entity healthcareService, Entity entertainmentService, Entity educationService, Entity telecomService, Entity garbageService, Entity policeService, CitizenHappinessParameterData citizenHappinessParameterData, GarbageParameterData garbageParameterData)
        {
            // 检查物业是否存在
            if (!buildings.HasComponent(property))
            {
                return float.NegativeInfinity;
            }

            // flag -> isAlreadyMovedIn: 检查家庭是否已经搬入
            bool isAlreadyMovedIn = (households[household].m_Flags & HouseholdFlags.MovedIn) != 0;

            // flag2 -> isHomelessShelter: 检查该物业是否为避难所（如公园或废弃建筑）
            bool isHomelessShelter = IsHomelessShelterBuilding(property, ref parks, ref abandoneds);

            // 如果是避难所且尚未搬入，则不允许评分（即不能主动选择搬入避难所）
            if (isHomelessShelter && !isAlreadyMovedIn)
            {
                return float.NegativeInfinity;
            }

            Building buildingInstance = buildings[property];
            Entity prefab = prefabRefs[property].m_Prefab;

            // 获取通用公寓质量评分
            GenericApartmentQuality genericApartmentQuality = GetGenericApartmentQuality(property, prefab, ref buildingInstance, ref buildingProperties, ref buildingDatas, ref spawnableDatas, ref crimes, ref serviceCoverages, ref locked, ref electricityConsumers, ref waterConsumers, ref garbageProducers, ref mailProducers, ref transforms, ref abandoneds, pollutionMap, airPollutionMap, noiseMap, telecomCoverages, cityModifiers, healthcareService, entertainmentService, educationService, telecomService, garbageService, policeService, citizenHappinessParameterData, garbageParameterData);

            int totalCitizenCount = citizenBuffer.Length; // length

            // 初始化统计变量
            float averageCommuteTime = 0f; // num: 累计通勤时间，后计算平均值
            int commuterCount = 0;         // num2: 上班族和学生总数
            int taxpayerCount = 0;         // num3: 非儿童（纳税人口）数量
            int averageHappiness = 0;      // num4: 累计幸福度，后计算平均值
            int childCount = 0;            // num5: 儿童数量
            int averageTaxBonus = 0;       // num6: 累计税收加成，后计算平均值

            // 遍历家庭成员
            for (int i = 0; i < citizenBuffer.Length; i++)
            {
                Entity citizenEntity = citizenBuffer[i].m_Citizen;
                Citizen citizenData = citizens[citizenEntity];

                // 累加幸福度
                averageHappiness += citizenData.Happiness;

                if (citizenData.GetAge() == CitizenAge.Child)
                {
                    childCount++;
                }
                else
                {
                    taxpayerCount++;
                    // 累加税收带来的幸福度加成
                    averageTaxBonus += CitizenHappinessSystem.GetTaxBonuses(citizenData.GetEducationLevel(), taxRates, cityModifiers, in citizenHappinessParameterData).y;
                }

                // 计算通勤时间（学生或工人）
                if (students.HasComponent(citizenEntity))
                {
                    commuterCount++;
                    Game.Citizens.Student student = students[citizenEntity];
                    if (student.m_School != property)
                    {
                        averageCommuteTime += student.m_LastCommuteTime;
                    }
                }
                else if (workers.HasComponent(citizenEntity))
                {
                    commuterCount++;
                    Worker worker = workers[citizenEntity];
                    if (worker.m_Workplace != property)
                    {
                        averageCommuteTime += worker.m_LastCommuteTime;
                    }
                }
            }

            // 计算平均通勤时间
            if (commuterCount > 0)
            {
                averageCommuteTime /= (float)commuterCount;
            }

            // 计算平均幸福度和平均税收加成
            if (citizenBuffer.Length > 0)
            {
                averageHappiness /= citizenBuffer.Length;
                if (taxpayerCount > 0)
                {
                    averageTaxBonus /= taxpayerCount;
                }
            }

            // 获取服务覆盖率评分
            float serviceAvailability = PropertyUtils.GetServiceAvailability(buildingInstance.m_RoadEdge, buildingInstance.m_CurvePosition, availabilities);

            // 根据人口结构和基础质量计算缓存的公寓质量
            float cachedApartmentQuality = GetCachedApartmentQuality(totalCitizenCount, childCount, averageHappiness, genericApartmentQuality);

            // num7 -> shelterPenalty: 如果是避难所，给予巨大的评分惩罚
            float shelterPenalty = (isHomelessShelter ? (-1000) : 0);

            // 最终评分公式：服务分 + 质量分*10 + 税收红利*2 - 通勤时间 + 避难所惩罚
            return serviceAvailability + cachedApartmentQuality * 10f + (float)(2 * averageTaxBonus) - averageCommuteTime + shelterPenalty;
        }

        public static float GetCachedApartmentQuality(int familySize, int children, int averageHappiness, GenericApartmentQuality quality)
        {
            int2 cachedWelfareBonuses = CitizenHappinessSystem.GetCachedWelfareBonuses(quality.welfareBonus, averageHappiness);
            return CitizenHappinessSystem.GetApartmentWellbeing(quality.apartmentSize / (float)familySize, quality.level) + math.sqrt(children) * (quality.educationBonus.x + quality.educationBonus.y) + (float)cachedWelfareBonuses.x + (float)cachedWelfareBonuses.y + quality.score;
        }

        public static bool IsHomelessShelterBuilding(Entity propertyEntity, ref ComponentLookup<Game.Buildings.Park> parks, ref ComponentLookup<Abandoned> abandoneds)
        {
            if (!parks.HasComponent(propertyEntity))
            {
                return abandoneds.HasComponent(propertyEntity);
            }

            return true;
        }

        public static Entity GetHouseholdHomeBuilding(Entity householdEntity, ref ComponentLookup<PropertyRenter> propertyRenters, ref ComponentLookup<HomelessHousehold> homelessHouseholds)
        {
            if (propertyRenters.TryGetComponent(householdEntity, out var componentData))
            {
                return componentData.m_Property;
            }

            if (homelessHouseholds.TryGetComponent(householdEntity, out var componentData2))
            {
                return componentData2.m_TempHome;
            }

            return Entity.Null;
        }

        public static bool IsHouseholdNeedSupport(DynamicBuffer<HouseholdCitizen> householdCitizens, ref ComponentLookup<Citizen> citizens, ref ComponentLookup<Game.Citizens.Student> students)
        {
            bool result = true;
            for (int i = 0; i < householdCitizens.Length; i++)
            {
                Entity citizen = householdCitizens[i].m_Citizen;
                if (citizens[citizen].GetAge() == CitizenAge.Adult && !students.HasComponent(citizen))
                {
                    result = false;
                    break;
                }
            }

            return result;
        }

        #endregion

    }

    /// <summary>
    /// Harmony修补CitizenPathFindSetup.SetupFindHomeJob
    /// </summary>

    // 1. 定义自定义的 Job
    // 直接复制原Job的所有字段，确保数据对齐。
    [BurstCompile]
    public struct CustomSetupFindHomeJob : IJobChunk
    {
        // --- 字段复刻开始 ---
        [ReadOnly] public EntityTypeHandle m_EntityType;
        [ReadOnly] public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly] public ComponentLookup<Building> m_Buildings;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneDatas;
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;
        [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> m_Coverages;

        public PathfindSetupSystem.SetupData m_SetupData; // 注意：这是 PathfindSetupSystem 内部定义的 struct

        [ReadOnly] public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;
        [ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefs;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingProperties;
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;
        [ReadOnly] public BufferLookup<ResourceAvailability> m_Availabilities;
        [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverages;
        [ReadOnly] public ComponentLookup<Household> m_Households;
        [ReadOnly] public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;
        [ReadOnly] public ComponentLookup<Citizen> m_Citizens;
        [ReadOnly] public ComponentLookup<HealthProblem> m_HealthProblems;
        [ReadOnly] public ComponentLookup<Worker> m_Workers;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> m_Students;
        [ReadOnly] public ComponentLookup<CrimeProducer> m_Crimes;
        [ReadOnly] public ComponentLookup<Transform> m_Transforms;
        [ReadOnly] public ComponentLookup<Locked> m_Lockeds;
        [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
        [ReadOnly] public ComponentLookup<Game.Buildings.Park> m_Parks;
        [ReadOnly] public ComponentLookup<Abandoned> m_Abandoneds;
        [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
        [ReadOnly] public BufferLookup<Resources> m_ResourcesBufs;
        [ReadOnly] public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;
        [ReadOnly] public ComponentLookup<WaterConsumer> m_WaterConsumers;
        [ReadOnly] public ComponentLookup<GarbageProducer> m_GarbageProducers;
        [ReadOnly] public ComponentLookup<MailProducer> m_MailProducers;

        [ReadOnly] public NativeArray<int> m_TaxRates;
        [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
        [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
        [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
        [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;

        public HealthcareParameterData m_HealthcareParameters;
        public ParkParameterData m_ParkParameters;
        public EducationParameterData m_EducationParameters;
        public EconomyParameterData m_EconomyParameters;
        public TelecomParameterData m_TelecomParameters;
        public GarbageParameterData m_GarbageParameters;
        public PoliceConfigurationData m_PoliceParameters;
        public ServiceFeeParameterData m_ServiceFeeParameterData;
        public CitizenHappinessParameterData m_CitizenHappinessParameterData;

        [ReadOnly] public Entity m_City;
        // --- 字段复刻结束 ---

        // 实现 Execute 方法
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> chunkEntities = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<PrefabRef> chunkPrefabRefs = chunk.GetNativeArray(ref this.m_PrefabType);
            BufferAccessor<Renter> chunkRenters = chunk.GetBufferAccessor(ref this.m_RenterType);

            for (int i = 0; i < this.m_SetupData.Length; i++)
            {
                this.m_SetupData.GetItem(i, out var _, out var targetSeeker);
                // 修复: 使用 i 而不是 chunkIndex 作为随机种子的一部分，防止相同 chunk 内所有 seeker 行为完全一致
                Unity.Mathematics.Random random = targetSeeker.m_RandomSeed.GetRandom(i + unfilteredChunkIndex * 1000);

                Entity householdEntity = targetSeeker.m_SetupQueueTarget.m_Entity;
                if (!this.m_HouseholdCitizens.TryGetBuffer(householdEntity, out var householdMembers)) continue;

                bool isAlreadyInShelter = this.m_HomelessHouseholds.HasComponent(householdEntity) &&
                                          this.m_HomelessHouseholds[householdEntity].m_TempHome != Entity.Null;

                // 预先计算家庭财务状况，避免在内层循环重复计算
                int householdIncome = EconomyUtils.GetHouseholdIncome(
                        householdMembers, ref this.m_Workers, ref this.m_Citizens,
                        ref this.m_HealthProblems, ref this.m_EconomyParameters, this.m_TaxRates);
                bool needsWelfare = CitizenUtils.IsHouseholdNeedSupport(householdMembers, ref this.m_Citizens, ref this.m_Students);

                for (int j = 0; j < chunkEntities.Length; j++)
                {
                    Entity buildingEntity = chunkEntities[j];
                    Entity buildingPrefab = chunkPrefabRefs[j].m_Prefab;
                    Building buildingComponent = this.m_Buildings[buildingEntity];

                    // 基础检查
                    if (buildingComponent.m_RoadEdge == Entity.Null ||
                        !this.m_Coverages.HasBuffer(buildingComponent.m_RoadEdge) ||
                        !this.m_BuildingDatas.HasComponent(buildingPrefab))
                    {
                        continue;
                    }

                    // 避难所逻辑 (保持原样，这部分不是性能瓶颈)
                    if (BuildingUtils.IsHomelessShelterBuilding(buildingEntity, ref this.m_Parks, ref this.m_Abandoneds))
                    {
                        if (!isAlreadyInShelter)
                        {
                            float policeCoverage = NetUtils.GetServiceCoverage(
                                this.m_Coverages[buildingComponent.m_RoadEdge],
                                CoverageService.Police,
                                buildingComponent.m_CurvePosition);
                            int shelterCapacity = BuildingUtils.GetShelterHomelessCapacity(
                                buildingPrefab, ref this.m_BuildingDatas, ref this.m_BuildingProperties);

                            if (chunkRenters[j].Length < shelterCapacity)
                            {
                                // 修正Cost计算: 拥挤度惩罚不应过大，否则他们宁愿睡大街
                                float cost = 10f * policeCoverage +
                                             100f * (float)chunkRenters[j].Length / (float)shelterCapacity +
                                             2000f; // 降低基础惩罚，让避难所比“无结果”更有吸引力
                                targetSeeker.FindTargets(buildingEntity, cost);
                            }
                        }
                        continue;
                    }

                    // === 正常住房逻辑优化 ===

                    // 1. 快速过滤：如果没有 PropertiesOnMarket 组件，直接跳过
                    if (!this.m_PropertiesOnMarket.HasComponent(buildingEntity)) continue;

                    int askingRent = this.m_PropertiesOnMarket[buildingEntity].m_AskingRent;

                    // 2. 快速过滤：容量检查
                    int totalProperties = 1;
                    if (this.m_BuildingProperties.HasComponent(buildingPrefab))
                    {
                        totalProperties = this.m_BuildingProperties[buildingPrefab].CountProperties();
                    }

                    // 如果满了，绝对不要浪费时间计算Score
                    if (chunkRenters[j].Length >= totalProperties) continue;

                    // 3. 快速过滤：经济能力 (Affordability)
                    int garbageFeePerHousehold = this.m_ServiceFeeParameterData.m_GarbageFeeRCIO.x / totalProperties;

                    Entity zonePrefabEntity = this.m_SpawnableDatas[buildingPrefab].m_ZonePrefab;
                    float rentBudgetFactor = 1f;
                    if (this.m_ZonePropertiesDatas.TryGetComponent(zonePrefabEntity, out var zoneProps))
                    {
                        var density = PropertyUtils.GetZoneDensity(this.m_ZoneDatas[zonePrefabEntity], zoneProps);
                        rentBudgetFactor = density switch { ZoneDensity.Medium => 0.7f, ZoneDensity.Low => 0.5f, _ => 1f };
                    }

                    bool canAfford = needsWelfare || ((float)(askingRent + garbageFeePerHousehold) <= (float)householdIncome * rentBudgetFactor);

                    // 性能优化: 如果买不起，直接跳过，不要做 Score 计算
                    if (!canAfford) continue;

                    // 4. 可选优化：租金带过滤 (Rent Band Filter)
                    // 如果这是低密度区(通常是House)，但家庭非常穷，或者这是高密度(Apartment)，但家庭非常富
                    // 可以增加启发式过滤。例如：如果不穷且租金极低(askingRent < income * 0.05)，可能不考虑。
                    // 暂时不加，以免影响游戏逻辑正确性。

                    // 5. 昂贵的评分计算 (现在只有买得起且有空位的房子才会走到这里)
                    float propertyScore = HouseholdFindPropertySystemMod.GetPropertyScore(
                        buildingEntity, householdEntity, householdMembers,
                        ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings,
                        ref this.m_BuildingDatas, ref this.m_Households, ref this.m_Citizens,
                        ref this.m_Students, ref this.m_Workers, ref this.m_SpawnableDatas,
                        ref this.m_Crimes, ref this.m_ServiceCoverages, ref this.m_Lockeds,
                        ref this.m_ElectricityConsumers, ref this.m_WaterConsumers,
                        ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms,
                        ref this.m_Abandoneds, ref this.m_Parks, ref this.m_Availabilities,
                        this.m_TaxRates, this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap,
                        this.m_TelecomCoverages, this.m_CityModifiers[this.m_City],
                        this.m_HealthcareParameters.m_HealthcareServicePrefab,
                        this.m_ParkParameters.m_ParkServicePrefab,
                        this.m_EducationParameters.m_EducationServicePrefab,
                        this.m_TelecomParameters.m_TelecomServicePrefab,
                        this.m_GarbageParameters.m_GarbageServicePrefab,
                        this.m_PoliceParameters.m_PoliceServicePrefab,
                        this.m_CitizenHappinessParameterData, this.m_GarbageParameters);

                    // 6. 计算最终 Cost
                    // propertyScore 越高越好，Cost 越低越好。
                    // 增加拥挤度惩罚 (prefer empty houses slightly)
                    float finalCost = -propertyScore +
                                      500f * (chunkRenters[j].Length / (float)totalProperties) +
                                      random.NextFloat(0, 100f); // 减少随机波动，让更好的房子更容易被选中

                    targetSeeker.FindTargets(buildingEntity, finalCost);
                }
            }

        }
    }

    // =========================================================
    // 2. Harmony 补丁
    // =========================================================
    [HarmonyPatch]
    public static class PathfindSetupSystem_FindTargets_Patch
    {
        // 诊断计数器
        //private static int _callCount = 0;
        //private static bool _hasLoggedSuccess = false;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // 目标：PathfindSetupSystem.FindTargets(SetupTargetType, in SetupData)
            // 注意：SetupData 是结构体引用
            return typeof(PathfindSetupSystem).GetMethod("FindTargets",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(SetupTargetType), typeof(PathfindSetupSystem.SetupData).MakeByRefType() },
                null);
        }

        // 缓存 EntityQuery
        private static EntityQuery _findHomeQuery;
        private static EntityQuery _healthcareParamQuery;
        private static EntityQuery _parkParamQuery;
        private static EntityQuery _educationParamQuery;
        private static EntityQuery _economyParamQuery;
        private static EntityQuery _telecomParamQuery;
        private static EntityQuery _garbageParamQuery;
        private static EntityQuery _policeParamQuery;
        private static EntityQuery _serviceFeeParamQuery;
        private static EntityQuery _citizenHappinessParamQuery;

        // 缓存访问受保护 Dependency 属性的委托
        private static Func<SystemBase, JobHandle> _getDependencyAccessor;

        private static void EnsureInitialized(PathfindSetupSystem system)
        {
            // 1. 初始化访问器 (解决 Dependency 不可访问的问题)
            if (_getDependencyAccessor == null)
            {
                // 获取 SystemBase.Dependency 的 Getter 方法
                MethodInfo dependencyGetter = AccessTools.PropertyGetter(typeof(SystemBase), "Dependency");
                // 创建强类型委托，性能比直接反射 Invoke 快得多
                _getDependencyAccessor = (Func<SystemBase, JobHandle>)Delegate.CreateDelegate(typeof(Func<SystemBase, JobHandle>), dependencyGetter);
            }

            // 2. 初始化 Queries (解决 CS1503 问题)
            if (_findHomeQuery != default) return;

            // 使用 system.GetSetupQuery (这是 PathfindSetupSystem 里的 Public 方法，专门为了暴露给 Setup 结构体用的)
            // 而不是受保护的 system.GetEntityQuery

            // 重构 FindHomeQuery 的 Desc
            // 利用系统现有方法，不使用QueryBuilder避免性能开销
            var desc1 = new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<PropertyOnMarket>(), ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<Building>() },
                None = new ComponentType[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Condemned>() }
            };
            var desc2 = new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<PropertyOnMarket>(), ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<Building>() },
                None = new ComponentType[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Condemned>() }
            };
            _findHomeQuery = system.GetSetupQuery(desc1, desc2);

            // 初始化参数 Query，注意这里也使用 GetSetupQuery
            _healthcareParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<HealthcareParameterData>());
            _parkParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<ParkParameterData>());
            _educationParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<EducationParameterData>());
            _economyParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<EconomyParameterData>());
            _telecomParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<TelecomParameterData>());
            _garbageParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<GarbageParameterData>());
            _policeParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            _serviceFeeParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<ServiceFeeParameterData>());
            _citizenHappinessParamQuery = system.GetSetupQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
        }

        public static bool Prefix(
            PathfindSetupSystem __instance,
            SetupTargetType targetType,
            ref PathfindSetupSystem.SetupData setupData, // 使用 ref 对应 in/ref
            ref JobHandle __result)
        {
            // 1. 快速过滤
            // 如果不是我们要修改的类型，执行原版逻辑
            if (targetType != SetupTargetType.FindHome)
            {
                return true;
            }

            // 2. 诊断日志 (每 600 次调用或首次调用打印一次，避免刷屏)
            //_callCount++;
            //if (!_hasLoggedSuccess || _callCount % 600 == 0)
            //{
            //    Mod.Logger.Info($"[SetupFindHomeJob] FindHome Patch Triggered! Count: {_callCount}");
            //    _hasLoggedSuccess = true;
            //}

            EnsureInitialized(__instance);

            var world = __instance.World;
            var taxSystem = world.GetOrCreateSystemManaged<TaxSystem>();
            var groundPollutionSystem = world.GetOrCreateSystemManaged<GroundPollutionSystem>();
            var airPollutionSystem = world.GetOrCreateSystemManaged<AirPollutionSystem>();
            var noisePollutionSystem = world.GetOrCreateSystemManaged<NoisePollutionSystem>();
            var telecomCoverageSystem = world.GetOrCreateSystemManaged<TelecomCoverageSystem>();
            var citySystem = world.GetOrCreateSystemManaged<CitySystem>();

            // 构建 Job
            // Harmony修补不使用SystemAPI，直接从__instance 获取运行时数据
            var jobData = new CustomSetupFindHomeJob
            {
                m_EntityType = __instance.GetEntityTypeHandle(),
                m_RenterType = __instance.GetBufferTypeHandle<Renter>(true),
                m_PrefabType = __instance.GetComponentTypeHandle<PrefabRef>(true),

                m_Buildings = __instance.GetComponentLookup<Building>(true),
                m_Households = __instance.GetComponentLookup<Household>(true),
                m_HomelessHouseholds = __instance.GetComponentLookup<HomelessHousehold>(true),
                m_BuildingDatas = __instance.GetComponentLookup<BuildingData>(true),
                m_Coverages = __instance.GetBufferLookup<Game.Net.ServiceCoverage>(true),
                m_PropertiesOnMarket = __instance.GetComponentLookup<PropertyOnMarket>(true),
                m_Availabilities = __instance.GetBufferLookup<ResourceAvailability>(true),
                m_SpawnableDatas = __instance.GetComponentLookup<SpawnableBuildingData>(true),
                m_BuildingProperties = __instance.GetComponentLookup<BuildingPropertyData>(true),
                m_PrefabRefs = __instance.GetComponentLookup<PrefabRef>(true),
                m_ServiceCoverages = __instance.GetBufferLookup<Game.Net.ServiceCoverage>(true),
                m_Citizens = __instance.GetComponentLookup<Citizen>(true),
                m_Crimes = __instance.GetComponentLookup<CrimeProducer>(true),
                m_Lockeds = __instance.GetComponentLookup<Locked>(true),
                m_Transforms = __instance.GetComponentLookup<Transform>(true),
                m_CityModifiers = __instance.GetBufferLookup<CityModifier>(true),
                m_HouseholdCitizens = __instance.GetBufferLookup<HouseholdCitizen>(true),
                m_Abandoneds = __instance.GetComponentLookup<Abandoned>(true),
                m_Parks = __instance.GetComponentLookup<Game.Buildings.Park>(true),
                m_ElectricityConsumers = __instance.GetComponentLookup<ElectricityConsumer>(true),
                m_WaterConsumers = __instance.GetComponentLookup<WaterConsumer>(true),
                m_GarbageProducers = __instance.GetComponentLookup<GarbageProducer>(true),
                m_MailProducers = __instance.GetComponentLookup<MailProducer>(true),
                m_HealthProblems = __instance.GetComponentLookup<HealthProblem>(true),
                m_Workers = __instance.GetComponentLookup<Worker>(true),
                m_Students = __instance.GetComponentLookup<Game.Citizens.Student>(true),
                m_ResourcesBufs = __instance.GetBufferLookup<Resources>(true),
                m_ZoneDatas = __instance.GetComponentLookup<ZoneData>(true),
                m_ZonePropertiesDatas = __instance.GetComponentLookup<ZonePropertiesData>(true),

                // 获取外部数据
                m_TaxRates = taxSystem.GetTaxRates(),
                m_PollutionMap = groundPollutionSystem.GetMap(true, out var dep1),
                m_AirPollutionMap = airPollutionSystem.GetMap(true, out var dep2),
                m_NoiseMap = noisePollutionSystem.GetMap(true, out var dep3),
                m_TelecomCoverages = telecomCoverageSystem.GetData(true, out var dep4),

                // 单例组件数据
                m_HealthcareParameters = _healthcareParamQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = _parkParamQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = _educationParamQuery.GetSingleton<EducationParameterData>(),
                m_EconomyParameters = _economyParamQuery.GetSingleton<EconomyParameterData>(),
                m_TelecomParameters = _telecomParamQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = _garbageParamQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = _policeParamQuery.GetSingleton<PoliceConfigurationData>(),
                m_ServiceFeeParameterData = _serviceFeeParamQuery.GetSingleton<ServiceFeeParameterData>(),
                m_CitizenHappinessParameterData = _citizenHappinessParamQuery.GetSingleton<CitizenHappinessParameterData>(),

                m_City = citySystem.City,
                m_SetupData = setupData
            };

            // 获取基类的 Dependency
            JobHandle inputDeps = _getDependencyAccessor(__instance);

            // 合并依赖
            JobHandle combinedDeps = JobUtils.CombineDependencies(inputDeps, dep1, dep2, dep3, dep4);

            // 调度 Job
            JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, _findHomeQuery, combinedDeps);

            // 添加 Reader 依赖（防止资源竞争报错）
            groundPollutionSystem.AddReader(jobHandle);
            airPollutionSystem.AddReader(jobHandle);
            noisePollutionSystem.AddReader(jobHandle);
            telecomCoverageSystem.AddReader(jobHandle);
            taxSystem.AddReader(jobHandle);

            // 设置返回值并阻止原方法执行
            __result = jobHandle;
            return false;
        }
    }

}

