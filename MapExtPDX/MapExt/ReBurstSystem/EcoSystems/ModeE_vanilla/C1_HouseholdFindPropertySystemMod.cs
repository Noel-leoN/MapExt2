// Game.Simulation.HouseholdFindPropertySystem

// Game.Simulation.CitizenPathFindSetup + SetupFindHomeJob

using System;
using System.Reflection;
using Colossal.Entities;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Debug;
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
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.ModeE
{
    // =========================================================================================
    using ModSystem = HouseholdFindPropertySystemMod;
    using TargetSystem = HouseholdFindPropertySystem;

    // =========================================================================================

    public partial class HouseholdFindPropertySystemMod : GameSystemBase
    {
        #region Constants

        private const int UPDATE_INTERVAL = 16;
        public static readonly int kMaxProcessNormalHouseholdPerUpdate = 128;
        public static readonly int kMaxProcessHomelessHouseholdPerUpdate = 1280;
        public static readonly int kFindPropertyCoolDown = 5000;
        public override int GetUpdateInterval(SystemUpdatePhase phase) => UPDATE_INTERVAL;

        #endregion

        #region Fields

        public bool debugDisableHomeless;
        [DebugWatchValue] private DebugWatchDistribution m_DefaultDistribution;
        [DebugWatchValue] private DebugWatchDistribution m_EvaluateDistributionLow;
        [DebugWatchValue] private DebugWatchDistribution m_EvaluateDistributionMedium;
        [DebugWatchValue] private DebugWatchDistribution m_EvaluateDistributionHigh;
        [DebugWatchValue] private DebugWatchDistribution m_EvaluateDistributionLowrent;
        private EntityQuery m_HouseholdQuery;
        private EntityQuery m_HomelessHouseholdQuery;
        private EntityQuery m_FreePropertyQuery;
        private EntityQuery m_EconomyParameterQuery;
        private EntityQuery m_DemandParameterQuery;
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
        private EntityQuery m_HealthcareParameterQuery;
        private EntityQuery m_ParkParameterQuery;
        private EntityQuery m_EducationParameterQuery;
        private EntityQuery m_TelecomParameterQuery;
        private EntityQuery m_GarbageParameterQuery;
        private EntityQuery m_PoliceParameterQuery;
        private EntityQuery m_CitizenHappinessParameterQuery;

        #endregion

        #region Lifecycle

        protected override void OnCreate()
        {
            base.OnCreate();
            this.m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            this.m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            this.m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
            this.m_AirPollutionSystem = World.GetOrCreateSystemManaged<AirPollutionSystem>();
            this.m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
            this.m_TelecomCoverageSystem = World.GetOrCreateSystemManaged<TelecomCoverageSystem>();
            this.m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            this.m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>();
            this.m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
            this.m_TriggerSystem = World.GetOrCreateSystemManaged<TriggerSystem>();
            this.m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_PropertyProcessingSystem = World.GetOrCreateSystemManaged<PropertyProcessingSystem>();
            this.m_CountResidentialPropertySystem = World.GetOrCreateSystemManaged<CountResidentialPropertySystem>();
            this.m_HomelessHouseholdQuery = GetEntityQuery(ComponentType.ReadWrite<HomelessHousehold>(),
                ComponentType.ReadWrite<PropertySeeker>(), ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<MovingAway>(), ComponentType.Exclude<TouristHousehold>(),
                ComponentType.Exclude<CommuterHousehold>(), ComponentType.Exclude<CurrentBuilding>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            this.m_HouseholdQuery = GetEntityQuery(ComponentType.ReadWrite<Household>(),
                ComponentType.ReadWrite<PropertySeeker>(), ComponentType.ReadOnly<HouseholdCitizen>(),
                ComponentType.Exclude<HomelessHousehold>(), ComponentType.Exclude<MovingAway>(),
                ComponentType.Exclude<TouristHousehold>(), ComponentType.Exclude<CommuterHousehold>(),
                ComponentType.Exclude<CurrentBuilding>(), ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>());
            this.m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
            this.m_DemandParameterQuery = GetEntityQuery(ComponentType.ReadOnly<DemandParameterData>());
            this.m_HealthcareParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>());
            this.m_ParkParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ParkParameterData>());
            this.m_EducationParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EducationParameterData>());
            this.m_TelecomParameterQuery = GetEntityQuery(ComponentType.ReadOnly<TelecomParameterData>());
            this.m_GarbageParameterQuery = GetEntityQuery(ComponentType.ReadOnly<GarbageParameterData>());
            this.m_PoliceParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            this.m_CitizenHappinessParameterQuery =
                GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
            EntityQueryDesc entityQueryDesc = new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<Building>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<Abandoned>(),
                    ComponentType.ReadOnly<Game.Buildings.Park>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>()
                }
            };
            EntityQueryDesc entityQueryDesc2 = new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PropertyOnMarket>(),
                    ComponentType.ReadOnly<ResidentialProperty>(),
                    ComponentType.ReadOnly<Building>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Abandoned>(),
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Condemned>()
                }
            };
            this.m_FreePropertyQuery = GetEntityQuery(entityQueryDesc, entityQueryDesc2);
            RequireForUpdate(this.m_EconomyParameterQuery);
            RequireForUpdate(this.m_HealthcareParameterQuery);
            RequireForUpdate(this.m_ParkParameterQuery);
            RequireForUpdate(this.m_EducationParameterQuery);
            RequireForUpdate(this.m_TelecomParameterQuery);
            RequireForUpdate(this.m_HouseholdQuery);
            RequireForUpdate(this.m_DemandParameterQuery);
            this.m_DefaultDistribution = new DebugWatchDistribution(persistent: true, relative: true);
            this.m_EvaluateDistributionLow = new DebugWatchDistribution(persistent: true, relative: true);
            this.m_EvaluateDistributionMedium = new DebugWatchDistribution(persistent: true, relative: true);
            this.m_EvaluateDistributionHigh = new DebugWatchDistribution(persistent: true, relative: true);
            this.m_EvaluateDistributionLowrent = new DebugWatchDistribution(persistent: true, relative: true);
        }

        protected override void OnDestroy()
        {
            this.m_DefaultDistribution.Dispose();
            this.m_EvaluateDistributionLow.Dispose();
            this.m_EvaluateDistributionMedium.Dispose();
            this.m_EvaluateDistributionHigh.Dispose();
            this.m_EvaluateDistributionLowrent.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            NativeParallelHashMap<Entity, CachedPropertyInformation> cachedPropertyInfo =
                new NativeParallelHashMap<Entity, CachedPropertyInformation>(
                    this.m_FreePropertyQuery.CalculateEntityCount(), Allocator.TempJob);
            NativeArray<GroundPollution> groundPollutionMap =
                this.m_GroundPollutionSystem.GetMap(readOnly: true, out var groundPollutionDependencies);
            NativeArray<AirPollution> airPollutionMap =
                this.m_AirPollutionSystem.GetMap(readOnly: true, out var airPollutionDependencies);
            NativeArray<NoisePollution> noisePollutionMap =
                this.m_NoisePollutionSystem.GetMap(readOnly: true, out var noisePollutionDependencies);
            CellMapData<TelecomCoverage> telecomCoverageData =
                this.m_TelecomCoverageSystem.GetData(readOnly: true, out var telecomDependencies);
            PreparePropertyJob preparePropertyJobData = new PreparePropertyJob
            {
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_BuildingProperties = SystemAPI.GetComponentLookup<BuildingPropertyData>(isReadOnly: false),
                m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
                m_BuildingDatas = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: true),
                m_ParkDatas = SystemAPI.GetComponentLookup<ParkData>(isReadOnly: true),
                m_Renters = SystemAPI.GetBufferLookup<Renter>(isReadOnly: true),
                m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
                m_Abandoneds = SystemAPI.GetComponentLookup<Abandoned>(isReadOnly: true),
                m_Parks = SystemAPI.GetComponentLookup<Game.Buildings.Park>(isReadOnly: true),
                m_SpawnableDatas = SystemAPI.GetComponentLookup<SpawnableBuildingData>(isReadOnly: true),
                m_BuildingPropertyData = SystemAPI.GetComponentLookup<BuildingPropertyData>(isReadOnly: true),
                m_Buildings = SystemAPI.GetComponentLookup<Building>(isReadOnly: true),
                m_ServiceCoverages = SystemAPI.GetBufferLookup<Game.Net.ServiceCoverage>(isReadOnly: true),
                m_Crimes = SystemAPI.GetComponentLookup<CrimeProducer>(isReadOnly: true),
                m_Locked = SystemAPI.GetComponentLookup<Locked>(isReadOnly: true),
                m_Transforms = SystemAPI.GetComponentLookup<Transform>(isReadOnly: true),
                m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(isReadOnly: true),
                m_ElectricityConsumers = SystemAPI.GetComponentLookup<ElectricityConsumer>(isReadOnly: true),
                m_WaterConsumers = SystemAPI.GetComponentLookup<WaterConsumer>(isReadOnly: true),
                m_GarbageProducers = SystemAPI.GetComponentLookup<GarbageProducer>(isReadOnly: true),
                m_MailProducers = SystemAPI.GetComponentLookup<MailProducer>(isReadOnly: true),
                m_PollutionMap = groundPollutionMap,
                m_AirPollutionMap = airPollutionMap,
                m_NoiseMap = noisePollutionMap,
                m_TelecomCoverages = telecomCoverageData,
                m_HealthcareParameters = m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = m_ParkParameterQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = m_EducationParameterQuery.GetSingleton<EducationParameterData>(),
                m_TelecomParameters = m_TelecomParameterQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = m_GarbageParameterQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = m_PoliceParameterQuery.GetSingleton<PoliceConfigurationData>(),
                m_CitizenHappinessParameterData =
                    m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
                m_City = m_CitySystem.City,
                m_PropertyData = cachedPropertyInfo.AsParallelWriter()
            };
            JobHandle homelessHouseholdListDeps;
            JobHandle movedInHouseholdListDeps;
            JobHandle deps;
            FindPropertyJob findPropertyJobData = new FindPropertyJob
            {
                m_HomelessHouseholdEntities =
                    m_HomelessHouseholdQuery.ToEntityListAsync(World.UpdateAllocator.ToAllocator,
                        out homelessHouseholdListDeps),
                m_MovedInHouseholdEntities =
                    m_HouseholdQuery.ToEntityListAsync(World.UpdateAllocator.ToAllocator,
                        out movedInHouseholdListDeps),
                m_CachedPropertyInfo = cachedPropertyInfo,
                m_BuildingDatas = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: true),
                m_PropertiesOnMarket = SystemAPI.GetComponentLookup<PropertyOnMarket>(isReadOnly: true),
                m_Availabilities = SystemAPI.GetBufferLookup<ResourceAvailability>(isReadOnly: true),
                m_SpawnableDatas = SystemAPI.GetComponentLookup<SpawnableBuildingData>(isReadOnly: true),
                m_BuildingProperties = SystemAPI.GetComponentLookup<BuildingPropertyData>(isReadOnly: true),
                m_Buildings = SystemAPI.GetComponentLookup<Building>(isReadOnly: true),
                m_PathInformationBuffers = SystemAPI.GetBufferLookup<PathInformations>(isReadOnly: true),
                m_PrefabRefs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
                m_ServiceCoverages = SystemAPI.GetBufferLookup<Game.Net.ServiceCoverage>(isReadOnly: true),
                m_Workers = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
                m_Students = SystemAPI.GetComponentLookup<Game.Citizens.Student>(isReadOnly: true),
                m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
                m_HomelessHouseholds = SystemAPI.GetComponentLookup<HomelessHousehold>(isReadOnly: true),
                m_Citizens = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: true),
                m_Crimes = SystemAPI.GetComponentLookup<CrimeProducer>(isReadOnly: true),
                m_Lockeds = SystemAPI.GetComponentLookup<Locked>(isReadOnly: true),
                m_Transforms = SystemAPI.GetComponentLookup<Transform>(isReadOnly: true),
                m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(isReadOnly: true),
                m_HealthProblems = SystemAPI.GetComponentLookup<HealthProblem>(isReadOnly: true),
                m_Abandoneds = SystemAPI.GetComponentLookup<Abandoned>(isReadOnly: true),
                m_Parks = SystemAPI.GetComponentLookup<Game.Buildings.Park>(isReadOnly: true),
                m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(isReadOnly: true),
                m_ElectricityConsumers = SystemAPI.GetComponentLookup<ElectricityConsumer>(isReadOnly: true),
                m_WaterConsumers = SystemAPI.GetComponentLookup<WaterConsumer>(isReadOnly: true),
                m_GarbageProducers = SystemAPI.GetComponentLookup<GarbageProducer>(isReadOnly: true),
                m_MailProducers = SystemAPI.GetComponentLookup<MailProducer>(isReadOnly: true),
                m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
                m_CurrentBuildings = SystemAPI.GetComponentLookup<CurrentBuilding>(isReadOnly: true),
                m_CurrentTransports = SystemAPI.GetComponentLookup<CurrentTransport>(isReadOnly: true),
                m_PathInformations = SystemAPI.GetComponentLookup<PathInformation>(isReadOnly: true),
                m_CitizenBuffers = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
                m_PropertySeekers = SystemAPI.GetComponentLookup<PropertySeeker>(isReadOnly: false),
                m_PollutionMap = groundPollutionMap,
                m_AirPollutionMap = airPollutionMap,
                m_NoiseMap = noisePollutionMap,
                m_TelecomCoverages = telecomCoverageData,
                m_ResidentialPropertyData = m_CountResidentialPropertySystem.GetResidentialPropertyData(),
                m_HealthcareParameters = m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = m_ParkParameterQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = m_EducationParameterQuery.GetSingleton<EducationParameterData>(),
                m_TelecomParameters = m_TelecomParameterQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = m_GarbageParameterQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = m_PoliceParameterQuery.GetSingleton<PoliceConfigurationData>(),
                m_CitizenHappinessParameterData =
                    m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
                m_TaxRates = m_TaxSystem.GetTaxRates(),
                m_EconomyParameters = m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
                m_SimulationFrame = m_SimulationSystem.frameIndex,
                m_RentActionQueue = m_PropertyProcessingSystem.GetRentActionQueue(out deps).AsParallelWriter(),
                m_City = m_CitySystem.City,
                m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter(),
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer(),
                m_DynamicFindHomeMaxCost = Mod.Instance.CurrentSettings.FindHomeMaxCost
            };
            JobHandle prepareJobHandle = preparePropertyJobData.ScheduleParallel(m_FreePropertyQuery,
                JobUtils.CombineDependencies(Dependency, groundPollutionDependencies, noisePollutionDependencies,
                    airPollutionDependencies, telecomDependencies, deps));
            Dependency = findPropertyJobData.Schedule(JobHandle.CombineDependencies(prepareJobHandle,
                movedInHouseholdListDeps, homelessHouseholdListDeps));
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
            m_PathfindSetupSystem.AddQueueWriter(Dependency);
            m_AirPollutionSystem.AddReader(Dependency);
            m_NoisePollutionSystem.AddReader(Dependency);
            m_GroundPollutionSystem.AddReader(Dependency);
            m_TelecomCoverageSystem.AddReader(Dependency);
            m_TriggerSystem.AddActionBufferWriter(Dependency);
            m_CityStatisticsSystem.AddWriter(Dependency);
            m_TaxSystem.AddReader(Dependency);
            cachedPropertyInfo.Dispose(Dependency);
        }

        #endregion

        #region Jobs

        /// <summary>
        /// [Job] PreparePropertyJob: 生成所有可用房源的缓存数据。
        /// <para>
        /// 核心职责：遍历地图上所有具有空位能力的建筑（普通住宅 或 收容所），
        /// 预先计算并缓存每个建筑的基础物理质量（GenericApartmentQuality）和其实际剩余空位（freeCapacity）。
        /// 这些属性与找房家庭的具体情况无关，预先缓存后可以避免在后续数以万计的找房循环中重复计算
        /// （例如复杂的污染、服务覆盖面积计算等），极大地提升了系统性能。
        /// </para>
        /// </summary>
        [BurstCompile]
        private struct PreparePropertyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle m_EntityType;
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
            [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
            [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
            [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
            [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;
            public HealthcareParameterData m_HealthcareParameters;
            public ParkParameterData m_ParkParameters;
            public EducationParameterData m_EducationParameters;
            public TelecomParameterData m_TelecomParameters;
            public GarbageParameterData m_GarbageParameters;
            public PoliceConfigurationData m_PoliceParameters;
            public CitizenHappinessParameterData m_CitizenHappinessParameterData;
            public Entity m_City;

            public NativeParallelHashMap<Entity, CachedPropertyInformation>.ParallelWriter m_PropertyData;

            /// <summary>
            /// 🏠 计算建筑当前还能容纳的家庭数量或无家可归者数量。
            /// </summary>
            private int CalculateFree(Entity propertyEntity)
            {
                Entity propertyPrefab = this.m_Prefabs[propertyEntity].m_Prefab;
                int freeCapacity = 0;

                // ⛺ 如果是废弃建筑或允许流浪汉居住的公园，则按收容所计算空位
                if (this.m_BuildingDatas.HasComponent(propertyPrefab) &&
                    (this.m_Abandoneds.HasComponent(propertyEntity) ||
                     (this.m_Parks.HasComponent(propertyEntity) &&
                      this.m_ParkDatas[propertyPrefab].m_AllowHomeless)))
                {
                    // 将剩余容量正确赋值给 freeCapacity
                    freeCapacity =
                        BuildingUtils.GetShelterHomelessCapacity(propertyPrefab, ref this.m_BuildingDatas,
                            ref this.m_BuildingPropertyData) - this.m_Renters[propertyEntity].Length;
                }
                // 🏘️ 如果是普通住宅，按户数计算空位
                else if (this.m_BuildingProperties.HasComponent(propertyPrefab))
                {
                    BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[propertyPrefab];
                    DynamicBuffer<Renter> rentersBuffer = this.m_Renters[propertyEntity];
                    freeCapacity = buildingPropertyData.CountProperties(AreaType.Residential);

                    for (int i = 0; i < rentersBuffer.Length; i++)
                    {
                        Entity renterEntity = rentersBuffer[i].m_Renter;
                        // 这里判断是否为家庭Household，主要是排除商业等其他类型的租户（如果是混合建筑）
                        if (this.m_Households.HasComponent(renterEntity))
                        {
                            freeCapacity--;
                        }
                    }
                }

                return freeCapacity;
            }

            /// <summary>
            /// 🔄 遍历处理拥有空闲房屋的建筑 Chunk。
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                NativeArray<Entity> chunkEntities = chunk.GetNativeArray(this.m_EntityType);
                for (int i = 0; i < chunkEntities.Length; i++)
                {
                    Entity propertyEntity = chunkEntities[i];
                    int freeCapacity = this.CalculateFree(propertyEntity);

                    // 🌟 只有有空位的房子才值得缓存其物理质量
                    if (freeCapacity > 0)
                    {
                        Entity propertyPrefab = this.m_Prefabs[propertyEntity].m_Prefab;
                        Building buildingData = this.m_Buildings[propertyEntity];

                        Entity healthcareServicePrefab = this.m_HealthcareParameters.m_HealthcareServicePrefab;
                        Entity parkServicePrefab = this.m_ParkParameters.m_ParkServicePrefab;
                        Entity educationServicePrefab = this.m_EducationParameters.m_EducationServicePrefab;
                        Entity telecomServicePrefab = this.m_TelecomParameters.m_TelecomServicePrefab;
                        Entity garbageServicePrefab = this.m_GarbageParameters.m_GarbageServicePrefab;
                        Entity policeServicePrefab = this.m_PoliceParameters.m_PoliceServicePrefab;
                        DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];

                        // 📊 计算该套公寓的客观通用指标 (GenericApartmentQuality)
                        XCellMapSystemRe.GenericApartmentQuality apartmentQuality =
                            XCellMapSystemRe.GetGenericApartmentQuality(propertyEntity, propertyPrefab,
                                ref buildingData, ref this.m_BuildingProperties, ref this.m_BuildingDatas,
                                ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages,
                                ref this.m_Locked, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers,
                                ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms,
                                ref this.m_Abandoneds, this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap,
                                this.m_TelecomCoverages, cityModifiers, healthcareServicePrefab, parkServicePrefab,
                                educationServicePrefab, telecomServicePrefab, garbageServicePrefab, policeServicePrefab,
                                this.m_CitizenHappinessParameterData, this.m_GarbageParameters);

                        // 💾 提交到缓存供后续的单人找房线程高速调用
                        this.m_PropertyData.TryAdd(propertyEntity, new CachedPropertyInformation
                        {
                            free = freeCapacity,
                            quality = apartmentQuality
                        });
                    }
                }
            }
        }

        /// <summary>
        /// [Job] FindPropertyJob: 收集所有有需求的家庭（由于破产、刚搬入或改善型需求），
        /// 评估其经济状况并整理其找房参数，最终封装放入 PathfindSetupQueue 队列中交给底层 A* 寻路。
        /// <para>
        /// 注意：由于目前采用了 IJob 而不是 IJobChunk，且未通过任何 NativeArray 切分，
        /// 这个 Job 实际上是【单线程顺序执行】的。
        /// 这虽然避免了操作同一 CommandBuffer 及 NativeQueue 时的资源竞争（竞态条件），
        /// 但当城市规模达百万级、单帧需处理几千个家庭迁徙时，该单步 Job 的执行将占用大量主线程时间。
        /// </para>
        /// </summary>
        [BurstCompile]
        private struct FindPropertyJob : IJob
        {
            public NativeList<Entity> m_HomelessHouseholdEntities;
            public NativeList<Entity> m_MovedInHouseholdEntities;

            public NativeParallelHashMap<Entity, CachedPropertyInformation> m_CachedPropertyInfo;
            [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
            [ReadOnly] public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;
            [ReadOnly] public BufferLookup<PathInformations> m_PathInformationBuffers;
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
            [ReadOnly] public ComponentLookup<Household> m_Households;
            [ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildings;
            [ReadOnly] public ComponentLookup<CurrentTransport> m_CurrentTransports;
            [ReadOnly] public ComponentLookup<PathInformation> m_PathInformations;
            [ReadOnly] public BufferLookup<HouseholdCitizen> m_CitizenBuffers;
            public ComponentLookup<PropertySeeker> m_PropertySeekers;
            [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
            [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
            [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
            [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;
            [ReadOnly] public NativeArray<int> m_TaxRates;
            [ReadOnly] public CountResidentialPropertySystem.ResidentialPropertyData m_ResidentialPropertyData;
            [ReadOnly] public HealthcareParameterData m_HealthcareParameters;
            [ReadOnly] public ParkParameterData m_ParkParameters;
            [ReadOnly] public EducationParameterData m_EducationParameters;
            [ReadOnly] public TelecomParameterData m_TelecomParameters;
            [ReadOnly] public GarbageParameterData m_GarbageParameters;
            [ReadOnly] public PoliceConfigurationData m_PoliceParameters;
            [ReadOnly] public CitizenHappinessParameterData m_CitizenHappinessParameterData;
            [ReadOnly] public EconomyParameterData m_EconomyParameters;
            [ReadOnly] public uint m_SimulationFrame;
            public EntityCommandBuffer m_CommandBuffer;
            [ReadOnly] public Entity m_City;
            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
            public NativeQueue<RentAction>.ParallelWriter m_RentActionQueue;
            public float m_DynamicFindHomeMaxCost;

            /// <summary>
            /// 🧳 构造寻找新家的参数（起点、方法、权重及目标分）并发出寻路请求。
            /// </summary>
            private void StartHomeFinding(Entity householdEntity, Entity commuteCitizen, Entity targetLocation,
                Entity oldHome, float minimumScore, bool targetIsOrigin,
                DynamicBuffer<HouseholdCitizen> householdCitizens)
            {
                this.m_CommandBuffer.AddComponent(householdEntity, new PathInformation
                {
                    m_State = PathFlags.Pending
                });
                Household householdData = this.m_Households[householdEntity];
                PathfindWeights weights = default(PathfindWeights);

                // 🚶‍♂️ 如果有指定的通勤者（比如第一份工作的成年人或上学的孩子）
                if (this.m_Citizens.TryGetComponent(commuteCitizen, out var commuteCitizenData))
                {
                    weights = CitizenUtils.GetPathfindWeights(commuteCitizenData, householdData,
                        householdCitizens.Length);
                }
                else
                {
                    // 👨‍👩‍👧‍👦 如果没有工作的人（例如纯老人家庭），则综合计算所有家庭成员的通勤权重偏好
                    for (int i = 0; i < householdCitizens.Length; i++)
                    {
                        // [BUGFIX] 反编译原有 bug 修改：使用 m_Citizens[householdCitizens[i].m_Citizen] 而不是使用未被赋值的跳出作用域的 citizenData 属性
                        Citizen citizenDataObj = this.m_Citizens[householdCitizens[i].m_Citizen];
                        weights.m_Value += CitizenUtils
                            .GetPathfindWeights(citizenDataObj, householdData, householdCitizens.Length)
                            .m_Value;
                    }

                    weights.m_Value *= 1f / householdCitizens.Length;
                }

                PathfindParameters parameters = new PathfindParameters
                {
                    m_MaxSpeed = 111.111115f,
                    m_WalkSpeed = 1.6666667f,
                    m_Weights = weights,
                    m_Methods = (PathMethod.Pedestrian | PathMethod.PublicTransportDay),
                    m_MaxCost = m_DynamicFindHomeMaxCost,
                    m_PathfindFlags = (PathfindFlags.Simplified | PathfindFlags.IgnorePath)
                };

                // 📍 A点：通勤地点（工作或学校所在地）
                SetupQueueTarget targetLocationTarget = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = targetLocation
                };

                // 📍 B点：未知的找房目标
                SetupQueueTarget findHomeTarget = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.FindHome,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = householdEntity,
                    m_Entity2 = oldHome,
                    m_Value2 = minimumScore
                };

                // 🚗 如果家庭拥有车辆，允许通过道路通行
                if (this.m_OwnedVehicles.TryGetBuffer(householdEntity, out var ownedVehiclesBuffer) &&
                    ownedVehiclesBuffer.Length != 0)
                {
                    parameters.m_Methods |= (PathMethod)(targetIsOrigin ? 8194 : 8198);
                    parameters.m_ParkingSize = float.MinValue;
                    parameters.m_IgnoredRules |= RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic |
                                                 RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles;
                    targetLocationTarget.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    targetLocationTarget.m_RoadTypes |= RoadTypes.Car;
                    findHomeTarget.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    findHomeTarget.m_RoadTypes |= RoadTypes.Car;
                }

                if (targetIsOrigin)
                {
                    parameters.m_MaxSpeed.y = 277.77777f;
                    parameters.m_Methods |= PathMethod.Taxi | PathMethod.PublicTransportNight;
                    parameters.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                }
                else
                {
                    CommonUtils.Swap(ref targetLocationTarget, ref findHomeTarget);
                }

                parameters.m_MaxResultCount = 10;
                parameters.m_PathfindFlags |= (PathfindFlags)(targetIsOrigin ? 256 : 128);
                this.m_CommandBuffer.AddBuffer<PathInformations>(householdEntity).Add(new PathInformations
                {
                    m_State = PathFlags.Pending
                });
                SetupQueueItem queueItem =
                    new SetupQueueItem(householdEntity, parameters, targetLocationTarget, findHomeTarget);
                this.m_PathfindQueue.Enqueue(queueItem);
            }

            /// <summary>
            /// 🏢 寻找该家庭中第一个有工作地点或学校地点的成员。
            /// 其地点会被当作评价住宅地段优劣时的通勤参考中心 (Commute Center)。
            /// </summary>
            private Entity GetFirstWorkplaceOrSchool(DynamicBuffer<HouseholdCitizen> householdCitizens,
                ref Entity commuteCitizen)
            {
                for (int i = 0; i < householdCitizens.Length; i++)
                {
                    commuteCitizen = householdCitizens[i].m_Citizen;
                    if (this.m_Workers.HasComponent(commuteCitizen))
                    {
                        return this.m_Workers[commuteCitizen].m_Workplace;
                    }

                    if (this.m_Students.HasComponent(commuteCitizen))
                    {
                        return this.m_Students[commuteCitizen].m_School;
                    }
                }

                return Entity.Null;
            }

            /// <summary>
            /// 🧍 获取成员目前的实际物理地点。
            /// </summary>
            private Entity GetCurrentLocation(DynamicBuffer<HouseholdCitizen> householdCitizens)
            {
                for (int i = 0; i < householdCitizens.Length; i++)
                {
                    if (this.m_CurrentBuildings.TryGetComponent(householdCitizens[i].m_Citizen,
                            out var currentBuildingData))
                    {
                        return currentBuildingData.m_CurrentBuilding;
                    }

                    if (this.m_CurrentTransports.TryGetComponent(householdCitizens[i].m_Citizen,
                            out var currentTransportData))
                    {
                        return currentTransportData.m_CurrentTransport;
                    }
                }

                return Entity.Null;
            }

            /// <summary>
            /// IJob 只有一个总控制流。
            /// 顺序处理最多 {kMaxProcessHomelessHouseholdPerUpdate} 户流浪家庭，
            /// 以及 {kMaxProcessNormalHouseholdPerUpdate} 户常规搬家家庭。
            /// </summary>
            public void Execute()
            {
                int count = 0;
                for (int i = 0; i < this.m_HomelessHouseholdEntities.Length; i++)
                {
                    Entity householdEntity = this.m_HomelessHouseholdEntities[i];
                    if (this.ProcessFindHome(householdEntity))
                    {
                        count++;
                    }

                    if (count >= kMaxProcessHomelessHouseholdPerUpdate)
                    {
                        break;
                    }
                }

                count = 0;
                for (int j = 0; j < this.m_MovedInHouseholdEntities.Length; j++)
                {
                    Entity householdEntity2 = this.m_MovedInHouseholdEntities[j];
                    if (this.ProcessFindHome(householdEntity2))
                    {
                        count++;
                    }

                    if (count >= kMaxProcessNormalHouseholdPerUpdate)
                    {
                        break;
                    }
                }
            }

            /// <summary>
            /// 🚦 单个家庭的寻房状态判定与启动逻辑。
            /// </summary>
            private bool ProcessFindHome(Entity householdEntity)
            {
                DynamicBuffer<HouseholdCitizen> householdCitizens = this.m_CitizenBuffers[householdEntity];
                if (householdCitizens.Length == 0)
                {
                    return false;
                }

                PropertySeeker propertySeeker = this.m_PropertySeekers[householdEntity];
                int householdIncome = EconomyUtils.GetHouseholdIncome(householdCitizens, ref this.m_Workers,
                    ref this.m_Citizens,
                    ref this.m_HealthProblems, ref this.m_EconomyParameters, this.m_TaxRates);

                // 1. 📬 如果已经申请寻路且寻路引擎有返回结果，则前往处理
                if (this.m_PathInformationBuffers.TryGetBuffer(householdEntity, out var pathInformations))
                {
                    this.ProcessPathInformations(householdEntity, pathInformations, propertySeeker, householdCitizens,
                        householdIncome);
                    return false;
                }

                Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity,
                    ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);

                // --- 📉 2. 预估当前住房分数 ---
                // 使用复杂的 GetPropertyScore 判断现在的居住条件的各项指标基线。
                float bestPropertyScore = ((householdHomeBuilding != Entity.Null)
                    ? XCellMapSystemRe.GetPropertyScore(householdHomeBuilding, householdEntity,
                        householdCitizens, ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings,
                        ref this.m_BuildingDatas, ref this.m_Households, ref this.m_Citizens, ref this.m_Students,
                        ref this.m_Workers, ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages,
                        ref this.m_Lockeds, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers,
                        ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms,
                        ref this.m_Abandoneds, ref this.m_Parks, ref this.m_Availabilities, this.m_TaxRates,
                        this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap, this.m_TelecomCoverages,
                        this.m_CityModifiers[this.m_City], this.m_HealthcareParameters.m_HealthcareServicePrefab,
                        this.m_ParkParameters.m_ParkServicePrefab, this.m_EducationParameters.m_EducationServicePrefab,
                        this.m_TelecomParameters.m_TelecomServicePrefab,
                        this.m_GarbageParameters.m_GarbageServicePrefab, this.m_PoliceParameters.m_PoliceServicePrefab,
                        this.m_CitizenHappinessParameterData, this.m_GarbageParameters)
                    : float.NegativeInfinity);

                // 😿 3. 流浪汉大逃亡逻辑：
                if (householdHomeBuilding == Entity.Null &&
                    propertySeeker.m_LastPropertySeekFrame +
                    kFindPropertyCoolDown > this.m_SimulationFrame)
                {
                    // 若彻底找不到家且冷却结束，同时如果城市中几乎没有可用空房子 (<10)，此人直接搬离城市
                    if (this.m_PathInformations[householdEntity].m_State != PathFlags.Pending &&
                        math.csum(this.m_ResidentialPropertyData.m_FreeProperties) < 10)
                    {
                        CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                    }

                    return false;
                }

                // 🗺️ 4. 寻找起点：用于测算去新家的通勤距离
                Entity commuteCitizen = Entity.Null;
                Entity firstWorkplaceOrSchool = this.GetFirstWorkplaceOrSchool(householdCitizens, ref commuteCitizen);
                bool isWorkplaceNull = firstWorkplaceOrSchool == Entity.Null;

                // 如果没有工作，就从当前物理站点的地点开始寻找计算
                Entity targetLocation =
                    (isWorkplaceNull ? this.GetCurrentLocation(householdCitizens) : firstWorkplaceOrSchool);
                if (householdHomeBuilding == Entity.Null && targetLocation == Entity.Null)
                {
                    CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                    return false;
                }

                // 🚀 5. 设置参数并启动寻找
                propertySeeker.m_TargetProperty = firstWorkplaceOrSchool;
                propertySeeker.m_BestProperty = householdHomeBuilding;
                propertySeeker.m_BestPropertyScore = bestPropertyScore;
                propertySeeker.m_LastPropertySeekFrame = this.m_SimulationFrame;
                this.m_PropertySeekers[householdEntity] = propertySeeker;
                this.StartHomeFinding(householdEntity, commuteCitizen, targetLocation, householdHomeBuilding,
                    propertySeeker.m_BestPropertyScore, isWorkplaceNull, householdCitizens);
                return true;
            }

            /// <summary>
            /// 🏁 收到底层 A* 引擎的寻路结果后，处理最终入住 (RentAction)
            /// 或判定寻路彻底失败并让由于极度困顿或绝望之极的家庭离开该座城市。
            /// </summary>
            private void ProcessPathInformations(Entity householdEntity,
                DynamicBuffer<PathInformations> pathInformations, PropertySeeker propertySeeker,
                DynamicBuffer<HouseholdCitizen> householdCitizens, int income)
            {
                int pathIndex = 0;
                PathInformations pathInfo = pathInformations[pathIndex];
                if ((pathInfo.m_State & PathFlags.Pending) != 0)
                {
                    return; // ⏳ 正在寻找中，直接跳过等这一帧结束
                }

                this.m_CommandBuffer.RemoveComponent<PathInformations>(householdEntity);
                bool hasTargetProperty = propertySeeker.m_TargetProperty != Entity.Null;
                Entity candidateProperty = (hasTargetProperty ? pathInfo.m_Origin : pathInfo.m_Destination);
                bool noFreeProperty = false;

                // 🔍 阶段1：验证寻路结果是否有效
                // A* 返回的结果是从 CustomSetupFindHomeJob 中挑选的最多10个距离最优解
                // 需要再次查验这套房子现在究竟是否仍旧真的还有空位，因为可能就在这几帧间被别人捷足先登了
                while (!this.m_CachedPropertyInfo.ContainsKey(candidateProperty) ||
                       this.m_CachedPropertyInfo[candidateProperty].free <= 0)
                {
                    pathIndex++;
                    if (pathInformations.Length > pathIndex)
                    {
                        pathInfo = pathInformations[pathIndex];
                        candidateProperty = (hasTargetProperty ? pathInfo.m_Origin : pathInfo.m_Destination);
                        continue;
                    }

                    candidateProperty = Entity.Null;
                    noFreeProperty = true;
                    break;
                }

                // ⛔ 如果找到的全满或者无有效目标，意味着本次寻路扑空，暂时罢手
                if (noFreeProperty && pathInformations.Length != 0 && pathInformations[0].m_Destination != Entity.Null)
                {
                    return;
                }

                // ⚖️ 阶段2：解析最终目标的完整总分（此时系统再一次调用 GetPropertyScore的庞大开销），
                // 在这里也会调用到我们在 PrepareJob 中缓存过的 GenericApartmentQuality 数据
                // 以综合计算是否值得真正下发搬入这套房子。
                float targetPropertyScore = float.NegativeInfinity;
                if (candidateProperty != Entity.Null && this.m_CachedPropertyInfo.ContainsKey(candidateProperty) &&
                    this.m_CachedPropertyInfo[candidateProperty].free > 0)
                {
                    targetPropertyScore = XCellMapSystemRe.GetPropertyScore(candidateProperty, householdEntity,
                        householdCitizens, ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings,
                        ref this.m_BuildingDatas, ref this.m_Households, ref this.m_Citizens, ref this.m_Students,
                        ref this.m_Workers, ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages,
                        ref this.m_Lockeds, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers,
                        ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms,
                        ref this.m_Abandoneds, ref this.m_Parks, ref this.m_Availabilities, this.m_TaxRates,
                        this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap, this.m_TelecomCoverages,
                        this.m_CityModifiers[this.m_City], this.m_HealthcareParameters.m_HealthcareServicePrefab,
                        this.m_ParkParameters.m_ParkServicePrefab, this.m_EducationParameters.m_EducationServicePrefab,
                        this.m_TelecomParameters.m_TelecomServicePrefab,
                        this.m_GarbageParameters.m_GarbageServicePrefab, this.m_PoliceParameters.m_PoliceServicePrefab,
                        this.m_CitizenHappinessParameterData, this.m_GarbageParameters);
                }

                // ❌ 阶段3：权衡比对阶段
                // 候选目标分数如果还不如自己现在房子舒服（也可能是分数过低为负无穷），就放弃本次找房结果继续留在现有的家
                if (targetPropertyScore < propertySeeker.m_BestPropertyScore)
                {
                    candidateProperty = propertySeeker.m_BestProperty;
                }

                bool isMovedIn = (this.m_Households[householdEntity].m_Flags & HouseholdFlags.MovedIn) != 0;
                bool isHomelessShelter = candidateProperty != Entity.Null &&
                                         BuildingUtils.IsHomelessShelterBuilding(candidateProperty, ref this.m_Parks,
                                             ref this.m_Abandoneds);
                bool needSupport =
                    CitizenUtils.IsHouseholdNeedSupport(householdCitizens, ref this.m_Citizens, ref this.m_Students);
                bool isAffordableProperty = this.m_PropertiesOnMarket.HasComponent(candidateProperty) &&
                                            (needSupport || this.m_PropertiesOnMarket[candidateProperty].m_AskingRent <
                                                income);
                bool isNotCurrentHome = !this.m_PropertyRenters.HasComponent(householdEntity) ||
                                        !this.m_PropertyRenters[householdEntity].m_Property.Equals(candidateProperty);
                Entity currentHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity,
                    ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);

                // --- 4. 判决执行阶段 ---

                // 情况 1：放弃了新选择，决定继续住在老的家里（或者依旧无家可归没有找到）
                if (currentHomeBuilding != Entity.Null && currentHomeBuilding == candidateProperty)
                {
                    // 若彻底破产无法支付自己现在的房子租金，滚出城市
                    if (!this.m_HomelessHouseholds.HasComponent(householdEntity) && !needSupport &&
                        income < this.m_PropertyRenters[householdEntity].m_Rent)
                    {
                        CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity, MoveAwayReason.NoMoney);
                    }
                    else
                    {
                        // 移除找房组件，消停一会不要再一直找了（进入冷却周期）
                        this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
                    }
                }
                // 情况 2：找到了一套租得起的新房 且 不是同一套老房子，或者被收容所接纳了：喜气洋洋乔迁新居！
                else if ((isAffordableProperty && isNotCurrentHome) || (isMovedIn && isHomelessShelter))
                {
                    this.m_RentActionQueue.Enqueue(new RentAction
                    {
                        m_Property = candidateProperty,
                        m_Renter = householdEntity
                    });

                    // 扣除掉缓存容量里的空位，防止下一个人以为还能住进来
                    if (this.m_CachedPropertyInfo.ContainsKey(candidateProperty))
                    {
                        CachedPropertyInformation cachedInfo = this.m_CachedPropertyInfo[candidateProperty];
                        cachedInfo.free--;
                        this.m_CachedPropertyInfo[candidateProperty] = cachedInfo;
                    }

                    this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
                }
                // 情况 3：依然无家可归，并且在收容所里也没有哪怕一个临时安身之所 -> 离开城市
                else if (candidateProperty == Entity.Null &&
                         (!this.m_HomelessHouseholds.HasComponent(householdEntity) ||
                          this.m_HomelessHouseholds[householdEntity].m_TempHome ==
                          Entity.Null))
                {
                    CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                }
                // 情况 4：没有通过验证，什么也没发生成为过眼烟云。刷新 PropertySeeker 不断重试。
                else
                {
                    propertySeeker.m_BestProperty = default(Entity);
                    propertySeeker.m_BestPropertyScore = float.NegativeInfinity;
                    this.m_PropertySeekers[householdEntity] = propertySeeker;

                    // [MOD FIX: 斩断原版的无限重试死循环]
                    // 如果不是无家可归者（说明在试图搬家改善环境），并且本次寻路失败（由于满员、太贵或确实比现在差），
                    // 强制关闭其找房意向组件，迫使其进入引擎原装系统级的冷却池中等待唤醒。
                    // 否则这个家庭会在下一帧无视一切 Cooldown 瞬间发起下一轮全城最高级 A* 暴搜。
                    if (currentHomeBuilding != Entity.Null)
                    {
                        this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
                    }
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 用于在 PreparePropertyJob 中通过并发计算而缓存的所有空房基础属性，
        /// 以便于后续的单条业务线（找房/入住判决）中复用无需重算。
        /// </summary>
        public struct CachedPropertyInformation
        {
            public XCellMapSystemRe.GenericApartmentQuality quality;
            public int free;
        }

        // public struct GenericApartmentQuality
        // {
        //     public float apartmentSize;
        //     public float2 educationBonus;
        //     public float welfareBonus;
        //     public float score;
        //     public int level;
        // }

        #endregion
    }

    #region CustomSetupFindHomeJob RePatcher

    // PartA: Job
    /// <summary>
    /// [Job] CustomSetupFindHomeJob: 核心由于原版没有距离限制而导致算力灾难的寻路下推逻辑。
    /// <para>
    /// 核心职责：遍历地图上的**所有**挂有 PropertyOnMarket 的空房子（无论是住宅还是收容所），
    /// 判定家庭是否能负担得起。如果可以，利用极度消耗性能的 PropertyUtils.GetPropertyScore 算出目标分数。
    /// 然后反转为寻路 Cost，通过 targetSeeker.FindTargets() 将这个建筑候选推送给 A* 寻路引擎作为搜索目标。
    /// </para>
    /// <para>
    /// 【问题根源】：该 Job 的原版实现**没有任何空间距离裁剪（Distance Culling）**！
    /// 找房者直接对全图符合租金的所有建筑发起了 O(N) 遍历和极为昂贵的 GetPropertyScore 计算，
    /// 这不仅导致大型地图 CPU 占用直接爆表，而且因为距离太长，寻路引擎必定因为节点数耗尽而失败（PathFlags.Failed），
    /// 从而导致这户家庭在下一次冷却时又重头走一遍这套死循环。
    /// </para>
    /// </summary>
    [BurstCompile]
    public struct CustomSetupFindHomeJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle m_EntityType;
        [ReadOnly] public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly] public ComponentLookup<Building> m_Buildings;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneDatas;
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;
        [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> m_Coverages;
        public PathfindSetupSystem.SetupData m_SetupData;
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
        [ReadOnly] public ComponentLookup<Game.Objects.Transform> m_Transforms;
        [ReadOnly] public ComponentLookup<Locked> m_Lockeds;
        [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
        [ReadOnly] public ComponentLookup<Game.Buildings.Park> m_Parks;
        [ReadOnly] public ComponentLookup<Abandoned> m_Abandoneds;
        [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
        [ReadOnly] public BufferLookup<Game.Economy.Resources> m_ResourcesBufs;
        [ReadOnly] public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;
        [ReadOnly] public ComponentLookup<WaterConsumer> m_WaterConsumers;
        [ReadOnly] public ComponentLookup<GarbageProducer> m_GarbageProducers;
        [ReadOnly] public ComponentLookup<MailProducer> m_MailProducers;
        [ReadOnly] public NativeArray<int> m_TaxRates;
        [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
        [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
        [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
        [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;
        [ReadOnly] public ComponentLookup<CompanyData> m_Companies;

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

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabType);
            BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor(ref m_RenterType);

            // --- 遍历每个被分配到当前 Task 的找房者家庭（SetupData.Length 通常是 1） ---
            for (int i = 0; i < m_SetupData.Length; i++)
            {
                m_SetupData.GetItem(i, out var _, out var targetSeeker);
                Unity.Mathematics.Random random = targetSeeker.m_RandomSeed.GetRandom(unfilteredChunkIndex);
                // 🧑‍🤝‍🧑 获取传入的起点实体 (原家、工作地或自身当前位置) 以备后用
                Entity originEntity = targetSeeker.m_SetupQueueTarget.m_Entity;
                if (!m_HouseholdCitizens.TryGetBuffer(originEntity, out var citizensBuffer))
                {
                    continue;
                }

                // ⚠️ 判断是否是已有住所但当前正在无家可归（比如刚被强拆）的临时状态
                bool isTempHomeless = m_HomelessHouseholds.HasComponent(originEntity) &&
                                      m_HomelessHouseholds[originEntity].m_TempHome != Entity.Null;

                // === 🌟 [性能优化 (Q3)] 提取在内层大循环里不变的计算结果 ===
                int householdIncome = EconomyUtils.GetHouseholdIncome(citizensBuffer, ref m_Workers, ref m_Citizens,
                    ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);
                bool isHouseholdNeedSupport =
                    CitizenUtils.IsHouseholdNeedSupport(citizensBuffer, ref m_Citizens, ref m_Students);

                // === 🚨 [HOTFIX: Early Exit Optimization - 次优目标提前退出] ===
                // 🔢 局部计数器：记录当前家庭已经成功收集了几个合格的候选房屋。
                // 🎯 我们不再追求“全图最优解”，只要找到足够多的备选项组合，就直接抛给 A* 引擎。
                int candidatesFound = 0;
                const int kMaxCandidatesToFind = 5;

                // --- 🔄 核心耗时循环：遍历本 Chunk 内的所有售/租建筑 (O(N) 全图遍历) ---
                for (int j = 0; j < nativeArray.Length; j++)
                {
                    Entity candidateProperty = nativeArray[j];
                    Entity prefab = nativeArray2[j].m_Prefab;
                    Building building = m_Buildings[candidateProperty];
                    if (!(building.m_RoadEdge != Entity.Null) || !m_Coverages.HasBuffer(building.m_RoadEdge) ||
                        !m_BuildingDatas.HasComponent(prefab))
                    {
                        continue;
                    }

                    // ⛺ 1. 分支一：如果这是一栋收容所 (Homeless Shelter)
                    if (BuildingUtils.IsHomelessShelterBuilding(candidateProperty, ref m_Parks, ref m_Abandoneds))
                    {
                        if (!isTempHomeless)
                        {
                            float serviceCoverage = NetUtils.GetServiceCoverage(m_Coverages[building.m_RoadEdge],
                                CoverageService.Police, building.m_CurvePosition);
                            int shelterHomelessCapacity =
                                BuildingUtils.GetShelterHomelessCapacity(prefab, ref m_BuildingDatas,
                                    ref m_BuildingProperties);
                            // ⚡ 如果收容所没住满，无需复杂打分，基于警力覆盖和拥挤度给出一个快速基础分。
                            // 📈 分数越高越靠后寻找。收容所因为没有 GetPropertyScore，这部分计算远快于普通住宅
                            if (bufferAccessor[j].Length < shelterHomelessCapacity)
                            {
                                targetSeeker.FindTargets(candidateProperty,
                                    100f * serviceCoverage + 1000f * (float)bufferAccessor[j].Length /
                                    (float)shelterHomelessCapacity + 10000f);

                                // === 🏁 [Early Exit Optimization] ===
                                // ➕ 找到一个合格收容所，计入候选
                                candidatesFound++;
                            }
                        }

                        continue;
                    }

                    // 🏠 2. 分支二：常规住宅处理 (Regular Property)
                    int askingRent = m_PropertiesOnMarket[candidateProperty].m_AskingRent;
                    int residentialCapacity = 0;
                    int maxPropertiesInBuilding = 1;
                    int nonResidentialCapacity = 0;

                    if (m_BuildingProperties.HasComponent(prefab))
                    {
                        maxPropertiesInBuilding = m_BuildingProperties[prefab].CountProperties();
                        residentialCapacity = m_BuildingProperties[prefab].CountProperties(Game.Zones.AreaType.Residential);
                        nonResidentialCapacity += m_BuildingProperties[prefab].CountProperties(Game.Zones.AreaType.Commercial);
                        nonResidentialCapacity += m_BuildingProperties[prefab].CountProperties(Game.Zones.AreaType.Industrial);
                    }

                    // 🚫 快速筛选：房子租客要是满了直接不要
                    if (bufferAccessor[j].Length >= maxPropertiesInBuilding)
                    {
                        continue;
                    }

                    // 🏢 混合建筑筛选：剔除公司租客，校验住宅空位
                    if (nonResidentialCapacity == 0)
                    {
                        if (bufferAccessor[j].Length >= residentialCapacity)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        int companyCount = 0;
                        for (int k = 0; k < bufferAccessor[j].Length; k++)
                        {
                            if (m_Companies.HasComponent(bufferAccessor[j][k].m_Renter))
                            {
                                companyCount++;
                            }
                        }
                        if (bufferAccessor[j].Length - companyCount >= residentialCapacity)
                        {
                            continue;
                        }
                    }

                    // 💰 3. 租金预判 (注意：垃圾费依旧以总户数 maxPropertiesInBuilding 分摊)
                    int garbageFeePerProperty = m_ServiceFeeParameterData.m_GarbageFeeRCIO.x / maxPropertiesInBuilding;
                    // householdIncome 已经提取到外层循环
                    // isHouseholdNeedSupport 已经提取到外层循环
                    Entity zonePrefab = m_SpawnableDatas[prefab].m_ZonePrefab;
                    if (m_ZonePropertiesDatas.TryGetComponent(zonePrefab, out var componentData))
                    {
                        float zoneDensityMultiplier =
                            PropertyUtils.GetZoneDensity(m_ZoneDatas[zonePrefab], componentData) switch
                            {
                                ZoneDensity.Medium => 0.7f,
                                ZoneDensity.Low => 0.5f,
                                _ => 1f,
                            };
                        // 🩺 只有在家庭确实急需救助 (isHouseholdNeedSupport) 或者 能够承担该公寓地段租金 的情况下
                        if (isHouseholdNeedSupport || !((float)(askingRent + garbageFeePerProperty) >
                                                        (float)householdIncome * zoneDensityMultiplier))
                        {
                            // === 💀 🚨 全系统最大的算力黑洞：极其昂贵的一对一定向打分 ===
                            // ⚠️ 注意：这里没有引入到目标住宅的空间距离 (Distance)，只要买得起，南极的雪屋也会在北极打工人的考虑之中！
                            float propertyScore = XCellMapSystemRe.GetPropertyScore(candidateProperty, originEntity,
                                citizensBuffer,
                                ref m_PrefabRefs, ref m_BuildingProperties, ref m_Buildings, ref m_BuildingDatas,
                                ref m_Households, ref m_Citizens, ref m_Students, ref m_Workers, ref m_SpawnableDatas,
                                ref m_Crimes, ref m_ServiceCoverages, ref m_Lockeds, ref m_ElectricityConsumers,
                                ref m_WaterConsumers, ref m_GarbageProducers, ref m_MailProducers, ref m_Transforms,
                                ref m_Abandoneds, ref m_Parks, ref m_Availabilities, m_TaxRates, m_PollutionMap,
                                m_AirPollutionMap, m_NoiseMap, m_TelecomCoverages, m_CityModifiers[m_City],
                                m_HealthcareParameters.m_HealthcareServicePrefab, m_ParkParameters.m_ParkServicePrefab,
                                m_EducationParameters.m_EducationServicePrefab,
                                m_TelecomParameters.m_TelecomServicePrefab, m_GarbageParameters.m_GarbageServicePrefab,
                                m_PoliceParameters.m_PoliceServicePrefab, m_CitizenHappinessParameterData,
                                m_GarbageParameters);

                            // 🧮 转化分数为 Cost（因为 A* Pathfinding 是最小堆，Cost 越小越好）。
                            // 📉 综合分数取负后，再叠加拥挤度惩罚因子和随机抖动因子，发往寻路队列。
                            targetSeeker.FindTargets(candidateProperty,
                                0f - propertyScore + 1000f * (float)bufferAccessor[j].Length /
                                (float)maxPropertiesInBuilding +
                                (float)random.NextInt(500));

                            // === 🏁 [Early Exit Optimization] ===
                            // 找到一套付得起且已完成计分的备选房，计入候选
                            candidatesFound++;
                        }
                    }

                    // === [HOTFIX: Early Exit Optimization - 斩断性能黑洞] ===
                    // 如果已经收集到了足够多的候选目标（哪怕是次优解），立刻无条件跳出内层遍历循环！
                    // 此举直接将百万城市下 O(N * M) 复杂度的全城搜图强行降级为 O(N * 常数)，极大拯救 CPU！
                    if (candidatesFound >= kMaxCandidatesToFind)
                    {
                        break;
                    }
                }
            }
        }
    }

    // =========================================================
    // PartB: Harmony RePatcher
    // =========================================================
    [HarmonyPatch]
    public static class PathfindSetupSystem_FindTargets_Patch
    {
#if DEBUG
        private static int _callCount = 0;
        private static bool _hasLoggedSuccess = false; 
#endif

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return typeof(PathfindSetupSystem).GetMethod("FindTargets",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(SetupTargetType), typeof(PathfindSetupSystem.SetupData).MakeByRefType() },
                null);
        }

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

        private static Func<SystemBase, JobHandle> _getDependencyAccessor;

        private static void EnsureInitialized(PathfindSetupSystem system)
        {
            // 1.  ( Dependency )
            if (_getDependencyAccessor == null)
            {
                MethodInfo dependencyGetter = AccessTools.PropertyGetter(typeof(SystemBase), "Dependency");
                _getDependencyAccessor =
                    (Func<SystemBase, JobHandle>)Delegate.CreateDelegate(typeof(Func<SystemBase, JobHandle>),
                        dependencyGetter);
            }

            // 2.  Queries ( CS1503 )
            if (_findHomeQuery != default) return;


            var desc1 = new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Building>() },
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<Abandoned>(),
                    ComponentType.ReadOnly<Game.Buildings.Park>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>()
                }
            };
            var desc2 = new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PropertyOnMarket>(), ComponentType.ReadOnly<ResidentialProperty>(),
                    ComponentType.ReadOnly<Building>()
                },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Condemned>()
                }
            };
            _findHomeQuery = system.GetSetupQuery(desc1, desc2);

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
            ref PathfindSetupSystem.SetupData setupData,
            ref JobHandle __result)
        {
            if (targetType != SetupTargetType.FindHome)
            {
                return true;
            }

#if DEBUG
            _callCount++;
            if (!_hasLoggedSuccess || _callCount % 600 == 0)
            {
                Mod.Logger.Info($"[SetupFindHomeJob] FindHome Patch Triggered! Count: {_callCount}");
                _hasLoggedSuccess = true;
            } 
#endif

            EnsureInitialized(__instance);

            var world = __instance.World;
            var taxSystem = world.GetOrCreateSystemManaged<TaxSystem>();
            var groundPollutionSystem = world.GetOrCreateSystemManaged<GroundPollutionSystem>();
            var airPollutionSystem = world.GetOrCreateSystemManaged<AirPollutionSystem>();
            var noisePollutionSystem = world.GetOrCreateSystemManaged<NoisePollutionSystem>();
            var telecomCoverageSystem = world.GetOrCreateSystemManaged<TelecomCoverageSystem>();
            var citySystem = world.GetOrCreateSystemManaged<CitySystem>();

            // HarmonySystemAPI__instance
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
                m_Companies = __instance.GetComponentLookup<CompanyData>(true),

                m_TaxRates = taxSystem.GetTaxRates(),
                m_PollutionMap = groundPollutionSystem.GetMap(true, out var dep1),
                m_AirPollutionMap = airPollutionSystem.GetMap(true, out var dep2),
                m_NoiseMap = noisePollutionSystem.GetMap(true, out var dep3),
                m_TelecomCoverages = telecomCoverageSystem.GetData(true, out var dep4),

                m_HealthcareParameters = _healthcareParamQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = _parkParamQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = _educationParamQuery.GetSingleton<EducationParameterData>(),
                m_EconomyParameters = _economyParamQuery.GetSingleton<EconomyParameterData>(),
                m_TelecomParameters = _telecomParamQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = _garbageParamQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = _policeParamQuery.GetSingleton<PoliceConfigurationData>(),
                m_ServiceFeeParameterData = _serviceFeeParamQuery.GetSingleton<ServiceFeeParameterData>(),
                m_CitizenHappinessParameterData =
                    _citizenHappinessParamQuery.GetSingleton<CitizenHappinessParameterData>(),

                m_City = citySystem.City,
                m_SetupData = setupData
            };

            JobHandle inputDeps = _getDependencyAccessor(__instance);

            JobHandle combinedDeps = JobUtils.CombineDependencies(inputDeps, dep1, dep2, dep3, dep4);

            JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, _findHomeQuery, combinedDeps);

            groundPollutionSystem.AddReader(jobHandle);
            airPollutionSystem.AddReader(jobHandle);
            noisePollutionSystem.AddReader(jobHandle);
            telecomCoverageSystem.AddReader(jobHandle);
            taxSystem.AddReader(jobHandle);

            __result = jobHandle;
            return false;
        }
    }

    #endregion
}




