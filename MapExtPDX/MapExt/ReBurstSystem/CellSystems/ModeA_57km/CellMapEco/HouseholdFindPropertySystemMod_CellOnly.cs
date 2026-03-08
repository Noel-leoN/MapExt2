using Colossal.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Debug;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Game.Zones;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Game;
using Game.Simulation;

namespace MapExtPDX.ModeA
{
    public partial class HouseholdFindPropertySystemMod_CellOnly : GameSystemBase
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
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
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

            private int CalculateFree(Entity property)
            {
                Entity prefab = this.m_Prefabs[property].m_Prefab;
                int count = 0;
                if (this.m_BuildingDatas.HasComponent(prefab) && (this.m_Abandoneds.HasComponent(property) ||
                                                                  (this.m_Parks.HasComponent(property) &&
                                                                   this.m_ParkDatas[prefab].m_AllowHomeless)))
                {
                    int shelterCapacity =
                        BuildingUtils.GetShelterHomelessCapacity(prefab, ref this.m_BuildingDatas,
                            ref this.m_BuildingPropertyData) - this.m_Renters[property].Length;
                }
                else if (this.m_BuildingProperties.HasComponent(prefab))
                {
                    BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[prefab];
                    DynamicBuffer<Renter> dynamicBuffer = this.m_Renters[property];
                    count = buildingPropertyData.CountProperties(AreaType.Residential);
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Entity renter = dynamicBuffer[i].m_Renter;
                        if (this.m_Households.HasComponent(renter))
                        {
                            count--;
                        }
                    }
                }

                return count;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    Entity entity = nativeArray[i];
                    int freeCapacity = this.CalculateFree(entity);
                    if (freeCapacity > 0)
                    {
                        Entity prefab = this.m_Prefabs[entity].m_Prefab;
                        Building buildingData = this.m_Buildings[entity];
                        Entity healthcareServicePrefab = this.m_HealthcareParameters.m_HealthcareServicePrefab;
                        Entity parkServicePrefab = this.m_ParkParameters.m_ParkServicePrefab;
                        Entity educationServicePrefab = this.m_EducationParameters.m_EducationServicePrefab;
                        Entity telecomServicePrefab = this.m_TelecomParameters.m_TelecomServicePrefab;
                        Entity garbageServicePrefab = this.m_GarbageParameters.m_GarbageServicePrefab;
                        Entity policeServicePrefab = this.m_PoliceParameters.m_PoliceServicePrefab;
                        DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
                        XCellMapSystemRe.GenericApartmentQuality genericApartmentQuality =
                            XCellMapSystemRe.GetGenericApartmentQuality(entity, prefab,
                                ref buildingData, ref this.m_BuildingProperties, ref this.m_BuildingDatas,
                                ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages,
                                ref this.m_Locked, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers,
                                ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms,
                                ref this.m_Abandoneds, this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap,
                                this.m_TelecomCoverages, cityModifiers, healthcareServicePrefab, parkServicePrefab,
                                educationServicePrefab, telecomServicePrefab, garbageServicePrefab, policeServicePrefab,
                                this.m_CitizenHappinessParameterData, this.m_GarbageParameters);
                        this.m_PropertyData.TryAdd(entity, new CachedPropertyInformation
                        {
                            free = freeCapacity,
                            quality = genericApartmentQuality
                        });
                    }
                }
            }
        }

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

            private void StartHomeFinding(Entity household, Entity commuteCitizen, Entity targetLocation,
                Entity oldHome, float minimumScore, bool targetIsOrigin, DynamicBuffer<HouseholdCitizen> citizens)
            {
                this.m_CommandBuffer.AddComponent(household, new PathInformation
                {
                    m_State = PathFlags.Pending
                });
                Household household2 = this.m_Households[household];
                PathfindWeights weights = default(PathfindWeights);
                if (this.m_Citizens.TryGetComponent(commuteCitizen, out var componentData))
                {
                    weights = CitizenUtils.GetPathfindWeights(componentData, household2, citizens.Length);
                }
                else
                {
                    for (int i = 0; i < citizens.Length; i++)
                    {
                        weights.m_Value += CitizenUtils.GetPathfindWeights(componentData, household2, citizens.Length)
                            .m_Value;
                    }

                    weights.m_Value *= 1f / citizens.Length;
                }

                PathfindParameters parameters = new PathfindParameters
                {
                    m_MaxSpeed = 111.111115f,
                    m_WalkSpeed = 1.6666667f,
                    m_Weights = weights,
                    m_Methods = (PathMethod.Pedestrian | PathMethod.PublicTransportDay),
                    m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost,
                    m_PathfindFlags = (PathfindFlags.Simplified | PathfindFlags.IgnorePath)
                };
                SetupQueueTarget a = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = targetLocation
                };
                SetupQueueTarget b = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.FindHome,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = household,
                    m_Entity2 = oldHome,
                    m_Value2 = minimumScore
                };
                if (this.m_OwnedVehicles.TryGetBuffer(household, out var bufferData) && bufferData.Length != 0)
                {
                    parameters.m_Methods |= (PathMethod)(targetIsOrigin ? 8194 : 8198);
                    parameters.m_ParkingSize = float.MinValue;
                    parameters.m_IgnoredRules |= RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic |
                                                 RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles;
                    a.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    a.m_RoadTypes |= RoadTypes.Car;
                    b.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    b.m_RoadTypes |= RoadTypes.Car;
                }

                if (targetIsOrigin)
                {
                    parameters.m_MaxSpeed.y = 277.77777f;
                    parameters.m_Methods |= PathMethod.Taxi | PathMethod.PublicTransportNight;
                    parameters.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                }
                else
                {
                    CommonUtils.Swap(ref a, ref b);
                }

                parameters.m_MaxResultCount = 10;
                parameters.m_PathfindFlags |= (PathfindFlags)(targetIsOrigin ? 256 : 128);
                this.m_CommandBuffer.AddBuffer<PathInformations>(household).Add(new PathInformations
                {
                    m_State = PathFlags.Pending
                });
                SetupQueueItem queueItem = new SetupQueueItem(household, parameters, a, b);
                this.m_PathfindQueue.Enqueue(queueItem);
            }

            private Entity GetFirstWorkplaceOrSchool(DynamicBuffer<HouseholdCitizen> citizens, ref Entity citizen)
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    citizen = citizens[i].m_Citizen;
                    if (this.m_Workers.HasComponent(citizen))
                    {
                        return this.m_Workers[citizen].m_Workplace;
                    }

                    if (this.m_Students.HasComponent(citizen))
                    {
                        return this.m_Students[citizen].m_School;
                    }
                }

                return Entity.Null;
            }

            private Entity GetCurrentLocation(DynamicBuffer<HouseholdCitizen> citizens)
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    if (this.m_CurrentBuildings.TryGetComponent(citizens[i].m_Citizen, out var componentData))
                    {
                        return componentData.m_CurrentBuilding;
                    }

                    if (this.m_CurrentTransports.TryGetComponent(citizens[i].m_Citizen, out var componentData2))
                    {
                        return componentData2.m_CurrentTransport;
                    }
                }

                return Entity.Null;
            }

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

                    if (count >= HouseholdFindPropertySystemMod_CellOnly.kMaxProcessHomelessHouseholdPerUpdate)
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

                    if (count >= HouseholdFindPropertySystemMod_CellOnly.kMaxProcessNormalHouseholdPerUpdate)
                    {
                        break;
                    }
                }
            }

            private bool ProcessFindHome(Entity householdEntity)
            {
                DynamicBuffer<HouseholdCitizen> citizens = this.m_CitizenBuffers[householdEntity];
                if (citizens.Length == 0)
                {
                    return false;
                }

                PropertySeeker propertySeeker = this.m_PropertySeekers[householdEntity];
                int householdIncome = EconomyUtils.GetHouseholdIncome(citizens, ref this.m_Workers, ref this.m_Citizens,
                    ref this.m_HealthProblems, ref this.m_EconomyParameters, this.m_TaxRates);
                if (this.m_PathInformationBuffers.TryGetBuffer(householdEntity, out var pathInformations))
                {
                    this.ProcessPathInformations(householdEntity, pathInformations, propertySeeker, citizens,
                        householdIncome);
                    return false;
                }

                Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity,
                    ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);
                float bestPropertyScore = ((householdHomeBuilding != Entity.Null)
                    ? XCellMapSystemRe.GetPropertyScore(householdHomeBuilding, householdEntity,
                        citizens, ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings,
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
                if (householdHomeBuilding == Entity.Null &&
                    propertySeeker.m_LastPropertySeekFrame +
                    HouseholdFindPropertySystemMod_CellOnly.kFindPropertyCoolDown > this.m_SimulationFrame)
                {
                    if (this.m_PathInformations[householdEntity].m_State != PathFlags.Pending &&
                        math.csum(this.m_ResidentialPropertyData.m_FreeProperties) < 10)
                    {
                        CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                    }

                    return false;
                }

                Entity citizen = Entity.Null;
                Entity firstWorkplaceOrSchool = this.GetFirstWorkplaceOrSchool(citizens, ref citizen);
                bool isWorkplaceNull = firstWorkplaceOrSchool == Entity.Null;
                Entity entity = (isWorkplaceNull ? this.GetCurrentLocation(citizens) : firstWorkplaceOrSchool);
                if (householdHomeBuilding == Entity.Null && entity == Entity.Null)
                {
                    CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                    return false;
                }

                propertySeeker.m_TargetProperty = firstWorkplaceOrSchool;
                propertySeeker.m_BestProperty = householdHomeBuilding;
                propertySeeker.m_BestPropertyScore = bestPropertyScore;
                propertySeeker.m_LastPropertySeekFrame = this.m_SimulationFrame;
                this.m_PropertySeekers[householdEntity] = propertySeeker;
                this.StartHomeFinding(householdEntity, citizen, entity, householdHomeBuilding,
                    propertySeeker.m_BestPropertyScore, isWorkplaceNull, citizens);
                return true;
            }

            private void ProcessPathInformations(Entity householdEntity,
                DynamicBuffer<PathInformations> pathInformations, PropertySeeker propertySeeker,
                DynamicBuffer<HouseholdCitizen> citizens, int income)
            {
                int pathIndex = 0;
                PathInformations pathInfo = pathInformations[pathIndex];
                if ((pathInfo.m_State & PathFlags.Pending) != 0)
                {
                    return;
                }

                this.m_CommandBuffer.RemoveComponent<PathInformations>(householdEntity);
                bool hasTarget = propertySeeker.m_TargetProperty != Entity.Null;
                Entity entity = (hasTarget ? pathInfo.m_Origin : pathInfo.m_Destination);
                bool noFreeProperty = false;
                while (!this.m_CachedPropertyInfo.ContainsKey(entity) || this.m_CachedPropertyInfo[entity].free <= 0)
                {
                    pathIndex++;
                    if (pathInformations.Length > pathIndex)
                    {
                        pathInfo = pathInformations[pathIndex];
                        entity = (hasTarget ? pathInfo.m_Origin : pathInfo.m_Destination);
                        continue;
                    }

                    entity = Entity.Null;
                    noFreeProperty = true;
                    break;
                }

                if (noFreeProperty && pathInformations.Length != 0 && pathInformations[0].m_Destination != Entity.Null)
                {
                    return;
                }

                float tempBestScore = float.NegativeInfinity;
                if (entity != Entity.Null && this.m_CachedPropertyInfo.ContainsKey(entity) &&
                    this.m_CachedPropertyInfo[entity].free > 0)
                {
                    tempBestScore = XCellMapSystemRe.GetPropertyScore(entity, householdEntity,
                        citizens, ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings,
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

                if (tempBestScore < propertySeeker.m_BestPropertyScore)
                {
                    entity = propertySeeker.m_BestProperty;
                }

                bool isMovedIn = (this.m_Households[householdEntity].m_Flags & HouseholdFlags.MovedIn) != 0;
                bool isHomelessShelter = entity != Entity.Null &&
                                         BuildingUtils.IsHomelessShelterBuilding(entity, ref this.m_Parks,
                                             ref this.m_Abandoneds);
                bool needSupport =
                    CitizenUtils.IsHouseholdNeedSupport(citizens, ref this.m_Citizens, ref this.m_Students);
                bool isAffordableProperty = this.m_PropertiesOnMarket.HasComponent(entity) &&
                                            (needSupport || this.m_PropertiesOnMarket[entity].m_AskingRent < income);
                bool isNotCurrentHome = !this.m_PropertyRenters.HasComponent(householdEntity) ||
                                        !this.m_PropertyRenters[householdEntity].m_Property.Equals(entity);
                Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity,
                    ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);
                if (householdHomeBuilding != Entity.Null && householdHomeBuilding == entity)
                {
                    if (!this.m_HomelessHouseholds.HasComponent(householdEntity) && !needSupport &&
                        income < this.m_PropertyRenters[householdEntity].m_Rent)
                    {
                        CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity, MoveAwayReason.NoMoney);
                    }
                    else
                    {
                        this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
                    }
                }
                else if ((isAffordableProperty && isNotCurrentHome) || (isMovedIn && isHomelessShelter))
                {
                    this.m_RentActionQueue.Enqueue(new RentAction
                    {
                        m_Property = entity,
                        m_Renter = householdEntity
                    });
                    if (this.m_CachedPropertyInfo.ContainsKey(entity))
                    {
                        CachedPropertyInformation value = this.m_CachedPropertyInfo[entity];
                        value.free--;
                        this.m_CachedPropertyInfo[entity] = value;
                    }

                    this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
                }
                else if (entity == Entity.Null && (!this.m_HomelessHouseholds.HasComponent(householdEntity) ||
                                                   this.m_HomelessHouseholds[householdEntity].m_TempHome ==
                                                   Entity.Null))
                {
                    CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                }
                else
                {
                    propertySeeker.m_BestProperty = default(Entity);
                    propertySeeker.m_BestPropertyScore = float.NegativeInfinity;
                    this.m_PropertySeekers[householdEntity] = propertySeeker;
                }
            }
        }

        #endregion

        #region Helpers

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
}
