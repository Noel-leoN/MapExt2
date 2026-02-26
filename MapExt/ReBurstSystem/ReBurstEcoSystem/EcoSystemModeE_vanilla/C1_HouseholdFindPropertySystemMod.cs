// Game.Simulation.HouseholdFindPropertySystem
// ����ϵͳ���ɽ���ECS�滻(�����޸ĳ�����Loop)

// Game.Simulation.CitizenPathFindSetup + SetupFindHomeJob
// ����װ�����ã�ʹ��HarmonyPrefix�޲���

using System;
using System.Reflection;
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
    /// ��ͥѰ�ҷ���ϵͳ��
    /// ����Ϊ�޼ҿɹ��߻���Ҫ���ҵļ�ͥѰ�Һ��ʵ�ס����
    /// �������㷿��������������Ѱ·�����Լ�ִ�а����߼��� 
    /// </summary>
    // ԭ����Ҫ���̷�Ϊ������
    // 1. PreparePropertyJob���������п��õ�סլ��ҵ��Ԥ�ȼ��㲢�����������÷ֺͿ���λ��������
    // 2. FindPropertyJob��������ҪѰ��ס���ļ�ͥ������Ѱ·��ѯ����Ѱ�Ҹ��������ָߵ�ס����
    // 3. CitizenPathFindSetup.SetupFindHomeJob ����Ѱ·����Ѱ·�������غ���ϵͳ���������ѵķ����������ҵ����ʵģ���Ϊ��ͥ�������ޡ�
    // ԭ���������⣺
    // 1. PreparePropertyJob ����Ч���� (�����������˷�)��ÿ16֡��ϵͳ������ȫͼ���еĿ��з�������������ϸ�� GenericApartmentQuality��������Ⱦ�����񸲸ǡ������Ȱ����ļ��㣩�������� m_PropertyData ��ϣ����99% �ķ�������һ֡�����ᱻ�κμ�ͥѡ�У���������ֱ�ӱ�������SetupFindHomeJob��Ѱ·����ʽ���֣�������ʹ��������ϣ���������Լ����¼�����һ�飡ֻ�е� FindPropertyJob ȷ��·�������ɹ�����Ϊ������У���Ż��õ��������ݡ����ۣ���ȫ����ɾ����Job����Ϊ�������㣨Lazy Evaluation����
    // 2. SetupFindHomeJob ���㷨���Ӷ� (N*M)����ԭ���߼�������һ�� IJobChunk�����������ǡ�����������Chunk����������Job�ڲ������� m_SetupData����������Ѱ�ҷ��ӵļ�ͥ�����б��������˱������ṹ��For Each Seeker(��ͥ) -> For Each Building(����) -> Calculate Score�����⣺GetPropertyScore �ڲ�������ȡ���񸲸��ʡ���Ⱦ��ͼ�����Ȳ���������ͬһ��������������10����ͥ���ҷ�����Щ�����������ظ�������10�Ρ��Ż����������Ļ������֣���̬���֣�����ͥ�ĸ��Ի����֣�ͨ�ھ��롢�������������롣���ڲ�ѭ��ǰ�����������ľ�̬�֡�
    // 3. FindPropertyJob �Ľṹ���߼�ȱ�ݡ�IJob vs IJobChunk��ԭ��ʹ�� IJob ��ͨ�� NativeList ����ʵ���������ڴ�����������ͥʵ��ʱ���������̣߳������߳�Job���޴���Copy�����ͱ���ѹ������Ϊ IJobChunk ���д������߿�����ԭ�򣺴����� askingRent > income ���жϹ�����Ӳ����������������Ҫ����ֱ���������������˼�ʹ�пշ�Ҳס����ȥ�����ձ�Ϊ�޼ҿɹ鲢�뿪���С�Ѱ·ϵͳ��Pathfind���� heuristic������ʽ���׶��������ظ��ֹ��ͣ����ܵ���Ѱ·ֱ��ʧ�ܣ���������������
    // 4. ���������͸���ס����ͥ�������½���ͥ�Ҳ����������Ƽ�ͥƵ��������

    // �İ��߼���
    // 1. ȥ��PreparePropertyJob
    // 2. FindPropertyJob IJobChunk���л�
    // 3. ��ΪHomeless/��������ͥ����/Ѱ������ס����ͥ�ֱ�������֦��Ƶ�ȣ������ҷ��ɹ���
    // 4. SetupFindHomeJob��֦�Ż�

    // =========================================================================================
    // 1. Mod �Զ���ϵͳ���� (��ǰ��)
    using ModSystem = HouseholdFindPropertySystemMod;
    // 2. ԭ��ϵͳ���� (���ڽ��úͶ�λ)
    using TargetSystem = HouseholdFindPropertySystem;
    using MapExtPDX.MapExt.Core;

    // =========================================================================================

    public partial class HouseholdFindPropertySystemMod : GameSystemBase
    {
        #region �ڲ����ݽṹ

        /// <summary>
        /// �����ķ�����Ϣ��������Ѱ�ҹ����п�������
        /// </summary>
        public struct CachedPropertyInformation
        {
            public GenericApartmentQuality quality; // ��Ԣ��������
            public int free; // ʣ����λ����
        }

        /// <summary>
        /// ͨ�ù�Ԣ����ָ��
        /// </summary>
        public struct GenericApartmentQuality
        {
            public float apartmentSize; // ��Ԣ��С
            public float2 educationBonus; // �����ӳ�
            public float welfareBonus; // �����ӳ�
            public float score; // �ۺ�����
            public int level; // �����ȼ�
        }

        #endregion

        #region ����������

        // Job Tick ��������
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;

        public static readonly int kMaxProcessNormalHouseholdPerUpdate = 256; // ÿ֡������������ͨ��ͥ��
        public static readonly int kMaxProcessHomelessHouseholdPerUpdate = 1024; // ÿ֡�����������޼ҿɹ���ͥ��

        // Ѱ����ȴʱ�䣨֡��
        public static readonly int kFindPropertyCoolDown = 2000;

        // ����������ʧ���ߵĳͷ�����ȴ��Լ0.5����Ϸ�죩
        public static readonly int kFailedCoolDown = 15000;

        #endregion

        #region ��ѯ��ϵͳ����

        // ʵ����ѯ
        private EntityQuery m_HouseholdQuery; // ��ͨ��ͥ��ѯ
        private EntityQuery m_HomelessHouseholdQuery; // �޼ҿɹ���ͥ��ѯ
        //private EntityQuery m_FreePropertyQuery;      // ���з�����ѯ

        // ������ѯ�����ڻ�ȡȫ�����õ�����
        private EntityQuery m_EconomyParameterQuery;
        private EntityQuery m_DemandParameterQuery;
        private EntityQuery m_HealthcareParameterQuery;
        private EntityQuery m_ParkParameterQuery;
        private EntityQuery m_EducationParameterQuery;
        private EntityQuery m_TelecomParameterQuery;
        private EntityQuery m_GarbageParameterQuery;
        private EntityQuery m_PoliceParameterQuery;
        private EntityQuery m_CitizenHappinessParameterQuery;

        // ����������ϵͳ
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

        #region �������� (OnCreate, OnUpdate, GetUpdateInterval)

        protected override void OnCreate()
        {
            base.OnCreate();

            // 1.����ԭ��ϵͳ����ȡԭ��ϵͳ����
            // ʹ�� GetExistingSystemManaged �������ⴴ��δ��ʼ����ϵͳ
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                //#if DEBUG
                Mod.Info($"[{typeof(ModSystem).Name}] ����ԭϵͳ: {typeof(TargetSystem).Name}");
                //#endif
            }
            else
            {
                // ���ڵ���ʱ��ʾ��ԭ��ϵͳ�����ѱ�����Mod�Ƴ�����δ����
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] �޷��ҵ��ɽ��õ�ԭϵͳ(��δ���ػ����ܱ�����Mod�Ƴ�): {typeof(TargetSystem).Name}");
#endif
            }

            // ��ȡϵͳ����
            m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
#if DEBUG || true
            // ʹ�� Persistent ���䣬һֱ���ڣ�����Ҫÿ֡ Dispose
            m_DebugStats = new NativeArray<int>((int)DebugStatIndex.Count, Allocator.Persistent);
#endif
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

            // ������ѯ
            // 1. �޼ҿɹ���ͥ������ HomelessHousehold �������ų����ڰ��롢�ο͵�
            m_HomelessHouseholdQuery = SystemAPI.QueryBuilder()
                .WithAllRW<HomelessHousehold, PropertySeeker>()
                .WithAll<HouseholdCitizen>()
                .WithNone<MovingAway, TouristHousehold, CommuterHousehold, CurrentBuilding, Deleted, Temp>()
                .Build();

            // 2. ��ͨ��ͥ������ Household �������ų��޼ҿɹ���
            m_HouseholdQuery = SystemAPI.QueryBuilder()
                .WithAllRW<Household, PropertySeeker>()
                .WithAll<HouseholdCitizen>()
                .WithNone<HomelessHousehold, MovingAway, TouristHousehold, CommuterHousehold, CurrentBuilding, Deleted,
                    Temp>()
                .Build();

            //// 3. ���з�������ѯ���п����п�λ�Ľ�������԰
            //var shelterDesc = new EntityQueryDesc
            //{
            //    All = new ComponentType[] { ComponentType.ReadOnly<Building>() },
            //    Any = new ComponentType[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Game.Buildings.Park>() },
            //    None = new ComponentType[] { ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>() }
            //};
            //var residentialDesc = new EntityQueryDesc
            //{
            //    All = new ComponentType[] { ComponentType.ReadOnly<PropertyOnMarket>(), ComponentType.ReadOnly<ResidentialProperty>(), ComponentType.ReadOnly<Building>() },
            //    None = new ComponentType[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Condemned>() }
            //};
            //m_FreePropertyQuery = GetEntityQuery(shelterDesc, residentialDesc);

            // 4. ������ѯ��ʼ��
            m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
            m_DemandParameterQuery = GetEntityQuery(ComponentType.ReadOnly<DemandParameterData>());
            m_HealthcareParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>());
            m_ParkParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ParkParameterData>());
            m_EducationParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EducationParameterData>());
            m_TelecomParameterQuery = GetEntityQuery(ComponentType.ReadOnly<TelecomParameterData>());
            m_GarbageParameterQuery = GetEntityQuery(ComponentType.ReadOnly<GarbageParameterData>());
            m_PoliceParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            m_CitizenHappinessParameterQuery = GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());

            // ȷ�����Ĳ������ڲŸ���
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
            // 1. ׼����Ⱦ�͸���������
            NativeArray<GroundPollution> groundPollutionMap =
                m_GroundPollutionSystem.GetMap(true, out JobHandle groundDeps);
            NativeArray<AirPollution> airPollutionMap = m_AirPollutionSystem.GetMap(true, out JobHandle airDeps);
            NativeArray<NoisePollution> noiseMap = m_NoisePollutionSystem.GetMap(true, out JobHandle noiseDeps);
            CellMapData<TelecomCoverage> telecomCoverage =
                m_TelecomCoverageSystem.GetData(true, out JobHandle telecomDeps);

            //// 2. ���������� PreparePropertyJob
            //// ��Job�����������п��÷�����������ʣ���ռ䣬д��Hashmap��
            //NativeParallelHashMap<Entity, CachedPropertyInformation> cachedPropertyInfo =
            // new NativeParallelHashMap<Entity, CachedPropertyInformation>(m_FreePropertyQuery.CalculateEntityCount(), Allocator.TempJob);

            //var prepareJob = new PreparePropertyJob
            //{
            //    m_EntityType = SystemAPI.GetEntityTypeHandle(),
            //    m_BuildingProperties = SystemAPI.GetComponentLookup<BuildingPropertyData>(false), // ��Ȼ��Lookup������CountProperties�п�����Ҫ��ȡ
            //    m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(true),
            //    m_BuildingDatas = SystemAPI.GetComponentLookup<BuildingData>(true),
            //    m_ParkDatas = SystemAPI.GetComponentLookup<ParkData>(true),
            //    m_Renters = SystemAPI.GetBufferLookup<Renter>(true),
            //    m_Households = SystemAPI.GetComponentLookup<Household>(true),
            //    m_Abandoneds = SystemAPI.GetComponentLookup<Abandoned>(true),
            //    m_Parks = SystemAPI.GetComponentLookup<Game.Buildings.Park>(true),
            //    m_SpawnableDatas = SystemAPI.GetComponentLookup<SpawnableBuildingData>(true),
            //    m_BuildingPropertyData = SystemAPI.GetComponentLookup<BuildingPropertyData>(true),
            //    m_Buildings = SystemAPI.GetComponentLookup<Building>(true),
            //    m_ServiceCoverages = SystemAPI.GetBufferLookup<Game.Net.ServiceCoverage>(true),
            //    m_Crimes = SystemAPI.GetComponentLookup<CrimeProducer>(true),
            //    m_Locked = SystemAPI.GetComponentLookup<Locked>(true),
            //    m_Transforms = SystemAPI.GetComponentLookup<Transform>(true),
            //    m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(true),
            //    m_ElectricityConsumers = SystemAPI.GetComponentLookup<ElectricityConsumer>(true),
            //    m_WaterConsumers = SystemAPI.GetComponentLookup<WaterConsumer>(true),
            //    m_GarbageProducers = SystemAPI.GetComponentLookup<GarbageProducer>(true),
            //    m_MailProducers = SystemAPI.GetComponentLookup<MailProducer>(true),

            //    m_PollutionMap = groundPollutionMap,
            //    m_AirPollutionMap = airPollutionMap,
            //    m_NoiseMap = noiseMap,
            //    m_TelecomCoverages = telecomCoverage,

            //    // �������ݻ�ȡ
            //    m_HealthcareParameters = m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>(),
            //    m_ParkParameters = m_ParkParameterQuery.GetSingleton<ParkParameterData>(),
            //    m_EducationParameters = m_EducationParameterQuery.GetSingleton<EducationParameterData>(),
            //    m_TelecomParameters = m_TelecomParameterQuery.GetSingleton<TelecomParameterData>(),
            //    m_GarbageParameters = m_GarbageParameterQuery.GetSingleton<GarbageParameterData>(),
            //    m_PoliceParameters = m_PoliceParameterQuery.GetSingleton<PoliceConfigurationData>(),
            //    m_CitizenHappinessParameterData = m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),

            //    m_City = m_CitySystem.City,
            //    m_PropertyData = cachedPropertyInfo.AsParallelWriter()
            //};

            //// �������������� PrepareJob
            //JobHandle prepareJobHandle = JobChunkExtensions.ScheduleParallel(
            //    prepareJob,
            //    m_FreePropertyQuery,
            //    JobUtils.CombineDependencies(Dependency, groundDeps, airDeps, noiseDeps, telecomDeps)
            //);

            // 3. �ؼ��޸ģ��Ƴ� ToEntityListAsync������ IJobChunk ���д���
            // �������Դ��� 200��+ ��ͥ�����������߳̿���

            NativeQueue<RentAction>.ParallelWriter rentActionQueue = m_PropertyProcessingSystem
                .GetRentActionQueue(out JobHandle rentQueueDeps).AsParallelWriter();
            NativeQueue<SetupQueueItem>.ParallelWriter pathfindQueue =
                m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter();

            // 1. �����޼ҿɹ��� (�����ȼ�)
            var findHomelessJob = new FindPropertyJobChunk
            {
                // ��������
                m_EntityType = SystemAPI.GetEntityTypeHandle(),
                m_PropertySeekerType = SystemAPI.GetComponentTypeHandle<PropertySeeker>(false),
                m_HouseholdType = SystemAPI.GetComponentTypeHandle<Household>(true),
                m_HomelessHouseholdType = SystemAPI.GetComponentTypeHandle<HomelessHousehold>(true),
                m_HouseholdCitizenType = SystemAPI.GetBufferTypeHandle<HouseholdCitizen>(true),
                m_PathInformationType =
                    SystemAPI.GetBufferTypeHandle<PathInformations>(true), // ReadOnly? ������ RW ������Ҫ�Ƴ�

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
                m_Renters = SystemAPI.GetBufferLookup<Renter>(true), // ���������ڼ���Ŀ�귿���Ƿ���Ա

                // ����
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
                m_CitizenHappinessParameterData =
                    m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
                m_EconomyParameters = m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),

                m_SimulationFrame = m_SimulationSystem.frameIndex,
                m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                m_City = m_CitySystem.City,

                m_PathfindQueue = pathfindQueue,
                m_RentActionQueue = rentActionQueue,

#if DEBUG || true
                m_DebugStats = m_DebugStats,
                m_EnableDebug = m_EnableDebug
#endif
            };

            JobHandle homelessHandle = findHomelessJob.ScheduleParallel(m_HomelessHouseholdQuery, base.Dependency);

            // 2. ������ͨ��ͥ (�߼�һ����ֻ��Query��ͬ�����д���)
            // ע�⣺Ϊ�˷�ֹJob��ͻ�����Ժϲ����������������ȡ�����ʹ����ͬJob�ṹ�塣
            JobHandle normalHandle = findHomelessJob.ScheduleParallel(m_HouseholdQuery, homelessHandle);

            base.Dependency = normalHandle;
            m_EndFrameBarrier.AddJobHandleForProducer(Dependency);
            m_PathfindSetupSystem.AddQueueWriter(Dependency);

            // ���Ӷ�ȡ��������ֹ��ͼϵͳ�����ͷ�
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
            base.OnDestroy();
#if DEBUG || true
            if (m_DebugStats.IsCreated)
            {
                m_DebugStats.Dispose();
            }
#endif
        }

        #endregion

        #region Jobs

        // ���������Լ�����
        private NativeArray<int> m_DebugStats;
        private bool m_EnableDebug = true; // Ĭ�Ͽ�������ͳ��

        public enum DebugStatIndex
        {
            TotalProcessed = 0,
            HomelessProcessed = 1, // �޼ҿɹ���ͥ
            NewProcessed = 2, // ����/�����ɼ�ͥ (��Homeless�ҷ�Renter)
            ImprovementProcessed = 3, // �����ͼ�ͥ (��Renter����)
            PathfindStarted = 4, // ����Ѱ·
            PathfindResultReceived = 5, // �յ�Ѱ·����
            MovedIn = 6, // �ɹ�����
            FailedNoCandidate = 7, // ����ʧ��(�޽���)
            CooldownSkipped = 8, // ��ȴ����
            Count // ����
        }

        /// <summary>
        /// Ѱ�ҷ���Job
        /// ������ͥ�İ�Ǩ�߼�����������Ѱ·����������Ѱ·�����������Ƿ����롣
        /// </summary>
        [BurstCompile]
        private struct FindPropertyJobChunk : IJobChunk
        {
            // --- ʵ������ ---
            [ReadOnly] public EntityTypeHandle m_EntityType;
            [ReadOnly] public ComponentTypeHandle<Household> m_HouseholdType;
            [ReadOnly] public ComponentTypeHandle<HomelessHousehold> m_HomelessHouseholdType;
            [ReadOnly] public BufferTypeHandle<HouseholdCitizen> m_HouseholdCitizenType;

            // ��д����
            public ComponentTypeHandle<PropertySeeker> m_PropertySeekerType;
            public BufferTypeHandle<PathInformations> m_PathInformationType; // RW����Ҫ�Ƴ�Pending״̬

            // ��������Lookup�������ֺ��ж�
            [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
            [ReadOnly] public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;
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
            [ReadOnly] public ComponentLookup<Household> m_Households; // Lookup needed for other entities
            [ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildings;
            [ReadOnly] public ComponentLookup<CurrentTransport> m_CurrentTransports;

            [ReadOnly] public ComponentLookup<PathInformation> m_PathInformations;

            //[ReadOnly] public BufferLookup<HouseholdCitizen> m_CitizenBuffers;
            // m_CitizenBuffers ����ҪLookup����Chunk��ȡ
            [ReadOnly] public BufferLookup<Renter> m_Renters; // ����

            // ��������
            [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
            [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
            [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
            [ReadOnly] public CellMapData<TelecomCoverage> m_TelecomCoverages;
            [ReadOnly] public NativeArray<int> m_TaxRates;
            [ReadOnly] public CountResidentialPropertySystem.ResidentialPropertyData m_ResidentialPropertyData;

            // ��������
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

            // ����
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
            public NativeQueue<RentAction>.ParallelWriter m_RentActionQueue;

#if DEBUG || true
            [NativeDisableParallelForRestriction] public NativeArray<int> m_DebugStats;
            public bool m_EnableDebug;
#endif

            /// <summary>
            /// ����Ѱ·���󣬰�����ͥѰ����ס��
            /// </summary>
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                NativeArray<Household> householdDatas = chunk.GetNativeArray(ref m_HouseholdType);
                NativeArray<PropertySeeker> seekers = chunk.GetNativeArray(ref m_PropertySeekerType);
                BufferAccessor<HouseholdCitizen> citizenBuffers = chunk.GetBufferAccessor(ref m_HouseholdCitizenType);

#if DEBUG || true
                // ʹ�ð�ȫ�� NativeArray ���������� Debug ������ CS0214 ����
#endif

                // HomelessHousehold��������
                bool isHomelessChunk = chunk.Has(ref m_HomelessHouseholdType);
                NativeArray<HomelessHousehold> homelessDatas =
                    isHomelessChunk ? chunk.GetNativeArray(ref m_HomelessHouseholdType) : default;

                bool hasPathInfos = chunk.Has(ref m_PathInformationType);
                BufferAccessor<PathInformations> pathInfoBuffers =
                    hasPathInfos ? chunk.GetBufferAccessor(ref m_PathInformationType) : default;

                // ����HomelessHouseholdQuery/HouseholdQuery?chunks
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity entity = entities[i];
                    PropertySeeker seeker = seekers[i];
                    DynamicBuffer<HouseholdCitizen> citizens = citizenBuffers[i];

                    // �����ռ�ͥ
                    if (citizens.Length == 0) continue;

#if DEBUG || true
                    if (m_EnableDebug)
                    {
                        m_DebugStats[(int)DebugStatIndex.TotalProcessed] += 1;
                        if (isHomelessChunk)
                        {
                            m_DebugStats[(int)DebugStatIndex.HomelessProcessed] += 1;
                        }
                        else if (m_PropertyRenters.HasComponent(entity))
                        {
                            m_DebugStats[(int)DebugStatIndex.ImprovementProcessed] += 1;
                        }
                        else
                        {
                            m_DebugStats[(int)DebugStatIndex.NewProcessed] += 1;
                        }
                    }
#endif

                    // int householdIncome = EconomyUtils.GetHouseholdIncome(citizens, ref m_Workers, ref m_Citizens, ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);
                    int householdIncome = EconomyUtils.GetHouseholdIncome(citizens, ref m_Workers, ref m_Citizens,
                        ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);
                    bool isHomelessEntity = isHomelessChunk || m_HomelessHouseholds.HasComponent(entity);

                    // 1. �����Ƿ���Ѱ·���ؽ���
                    bool isPathFinding = false;
                    if (hasPathInfos && pathInfoBuffers.Length > i)
                    {
                        DynamicBuffer<PathInformations> pathInfos = pathInfoBuffers[i];
                        if (pathInfos.Length > 0)
                        {
                            // ����״̬�� Pending��˵��Ѱ·���ڽ����У������˼�ͥ���ȴ���һtick
                            if ((pathInfos[0].m_State & PathFlags.Pending) != 0)
                            {
                                isPathFinding = true; // ����Ѱ·�У���Ҫ�ٷ�����������Ҳ��Ҫ������ȴ����
                            }
                            else
                            {
#if DEBUG || true
                                if (m_EnableDebug) m_DebugStats[(int)DebugStatIndex.PathfindResultReceived] += 1;
#endif
                                // Ѱ·�����ɣ���������
                                ProcessPathResult(entity,
                                    pathInfos,
                                    ref seeker,
                                    citizens,
                                    unfilteredChunkIndex,
                                    householdIncome,
                                    isHomelessEntity);
                                seekers[i] = seeker;
                                // �Ƴ�Buffer��Component����������
                                m_CommandBuffer.RemoveComponent<PathInformations>(unfilteredChunkIndex, entity);
                                m_CommandBuffer.RemoveComponent<PathInformation>(unfilteredChunkIndex, entity);
                            }
                        }
                    }

                    // ��������Ѱ·�У�ֱ�����������߼�
                    if (isPathFinding) continue;

                    // =========================================================
                    // 3. ��ȴ���� (Cooldown)
                    // =========================================================
                    // ���˺�(Homeless)ͨ���Ƚϼ����������ý϶̵���ȴ�����ߺ���ͨ��һ��
                    if (m_SimulationFrame < seeker.m_LastPropertySeekFrame + kFindPropertyCoolDown)
                    {
#if DEBUG || true
                        if (m_EnableDebug) m_DebugStats[(int)DebugStatIndex.CooldownSkipped] += 1;
#endif
                        continue;
                    }

                    // =========================================================
                    // 2. ��ȡ��ǰס��
                    // =========================================================
                    Entity currentHome =
                        GetHouseholdHomeBuilding(entity, ref m_PropertyRenters, ref m_HomelessHouseholds);
                    bool isHomeless = currentHome == Entity.Null;

                    // �������� (MoveAway)
                    // �������޼ҿɹ飬�����˺ܾ�(frame > 0)����ȫ�Ǽ���û�� -> �뿪����
                    if (isHomeless && seeker.m_LastPropertySeekFrame > 0 &&
                        math.csum(m_ResidentialPropertyData.m_FreeProperties) < 10)
                    {
                        CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, entity,
                            MoveAwayReason.NoSuitableProperty);
                        continue;
                    }

                    // �����µ�Ѱ·����
                    Entity commuteCitizen = Entity.Null;
                    Entity workplace = GetFirstWorkplaceOrSchool(citizens, ref commuteCitizen);
                    Entity origin = (workplace != Entity.Null) ? workplace : GetCurrentLocation(citizens);

                    if (origin != Entity.Null || currentHome != Entity.Null)
                    {
                        float currentScore = float.NegativeInfinity;
                        if (currentHome != Entity.Null)
                        {
                            // ���㵱ǰ��ס���֣����ڸ����Ի���
                            currentScore = CalculatePropertyScore(currentHome, entity, citizens);
                        }

                        seeker.m_BestProperty = currentHome;
                        seeker.m_BestPropertyScore = currentScore;
                        seeker.m_LastPropertySeekFrame = m_SimulationFrame;
                        seekers[i] = seeker;

                        StartHomeFinding(unfilteredChunkIndex, entity, commuteCitizen, origin, currentHome,
                            currentScore, workplace == Entity.Null, citizens);
#if DEBUG || true
                        if (m_EnableDebug) m_DebugStats[(int)DebugStatIndex.PathfindStarted] += 1;
#endif
                    }
                    else if (currentHome == Entity.Null)
                    {
                        // ��û��Ҳûλ����Ϣ��ֱ���Ƴ�
                        CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, entity,
                            MoveAwayReason.NoSuitableProperty);
                    }
                }
            }

            private bool IsPropertyFree(Entity property)
            {
                if (property == Entity.Null || !m_PrefabRefs.HasComponent(property)) return false;

                // ʵʱ���飬���Ⲣ����ͻ
                Entity prefab = m_PrefabRefs[property].m_Prefab;
                int currentRenters = 0;
                if (m_Renters.TryGetBuffer(property, out var renters)) currentRenters = renters.Length;

                if (m_Abandoneds.HasComponent(property) || m_Parks.HasComponent(property))
                {
                    return BuildingUtils.GetShelterHomelessCapacity(prefab, ref m_BuildingDatas,
                        ref m_BuildingProperties) > currentRenters;
                }

                if (m_BuildingProperties.HasComponent(prefab))
                {
                    // ��ͨסլ
                    return m_BuildingProperties[prefab].CountProperties(AreaType.Residential) > currentRenters;
                }

                return false;
            }

            private void ProcessPathResult(Entity household, DynamicBuffer<PathInformations> pathInfos,
                ref PropertySeeker seeker, DynamicBuffer<HouseholdCitizen> citizens, int jobIndex, int income,
                bool isHomeless)
            {
                Entity bestCandidate = Entity.Null;
                float bestScore = seeker.m_BestPropertyScore;

                // �����Ƿ���"��Ǯ���޼ҿɹ���" (ʧ����)
                // �߼���������Homeless�����������뻹��������Ҫȥס������
                bool isDisplacedNotPoor = isHomeless && income > 500; // 500��һ������ֵ���ɵ���

                for (int k = 0; k < pathInfos.Length; k++)
                {
                    PathInformations info = pathInfos[k];
                    if ((info.m_State & PathFlags.Pending) != 0) return;

                    Entity candidate = (seeker.m_TargetProperty != Entity.Null) ? info.m_Origin : info.m_Destination;
                    if (candidate == Entity.Null) continue;

                    // 1. �������飺�Ƿ�����
                    if (!IsPropertyFree(candidate)) continue;

                    bool isShelter = BuildingUtils.IsHomelessShelterBuilding(candidate, ref m_Parks, ref m_Abandoneds);

                    // 2. �������߼����� (�Ż�����)
                    if (isShelter)
                    {
                        // ��������Ǯ�˱���Ǩ�����ⲻ������һ�γ���(��ȴ�ܾ���)�����ܾ���������ǿ��Ѱ��סլ
                        // ����ʵ���Ҳ���(BestScore����)
                        if (isDisplacedNotPoor && bestScore > -5000f) continue;
                    }
                    else
                    {
                        // 3. סլ�߼����ˣ�����������
                        if (m_PropertiesOnMarket.HasComponent(candidate))
                        {
                            if (m_PropertiesOnMarket[candidate].m_AskingRent > income) continue;
                        }
                    }

                    // 4. �����÷� (Heavy Calculation)
                    // ֻ��ͨ��ǰ�����˵ĲŽ��м���
                    float candidateScore = CalculatePropertyScore(candidate, household, citizens);

                    // �����Ǳ��������ֶ����ͷ���Ȩ�أ�ȷ�������б�������ͨסլ������ѡסլ
                    if (isShelter) candidateScore -= 2000f;

                    if (candidateScore > bestScore)
                    {
                        bestScore = candidateScore;
                        bestCandidate = candidate;
                    }
                }

                if (bestCandidate != Entity.Null && bestCandidate != seeker.m_BestProperty)
                {
                    // �ɹ��ҵ��������ⷿ����
                    m_RentActionQueue.Enqueue(new RentAction
                    {
                        m_Property = bestCandidate,
                        m_Renter = household
                    });
                    // �ر� Seeker ��������ֹ��һ֡������
                    m_CommandBuffer.SetComponentEnabled<PropertySeeker>(jobIndex, household, false);
                }
                else
                {
                    // û�ҵ����ʵģ�����״̬�ȴ���һ�� CoolDown
                    seeker.m_BestProperty = Entity.Null;
                    seeker.m_BestPropertyScore = float.NegativeInfinity;
                }
            }

            // ��װ�����ֺ���
            private float CalculatePropertyScore(Entity property, Entity household,
                DynamicBuffer<HouseholdCitizen> citizens)
            {
                // �����Ǳ�������ʹ�ü������� (�Ż�Q2)
                if (BuildingUtils.IsHomelessShelterBuilding(property, ref m_Parks, ref m_Abandoneds))
                {
                    // ����������Ҫ����Ⱦ�������ӳɣ�ֻҪ�еط�ס����
                    // ����һ�����Թ̶��ĸ��֣������޴���ȥ��
                    return -100f;
                }

                return GetPropertyScore(
                    property, household, citizens,
                    ref m_PrefabRefs, ref m_BuildingProperties, ref m_Buildings, ref m_BuildingDatas,
                    ref m_Households, // ע�⣺ԭ������������ҪHouseholdsLookup������Score��һ��ֻ��Buildings������
                    ref m_Citizens, ref m_Students, ref m_Workers,
                    ref m_SpawnableDatas, ref m_Crimes, ref m_ServiceCoverages, ref m_Lockeds,
                    ref m_ElectricityConsumers, ref m_WaterConsumers, ref m_GarbageProducers, ref m_MailProducers,
                    ref m_Transforms, ref m_Abandoneds, ref m_Parks, ref m_Availabilities,
                    m_TaxRates, m_PollutionMap, m_AirPollutionMap, m_NoiseMap, m_TelecomCoverages,
                    m_CityModifiers[m_City],
                    m_HealthcareParameters.m_HealthcareServicePrefab, m_ParkParameters.m_ParkServicePrefab,
                    m_EducationParameters.m_EducationServicePrefab, m_TelecomParameters.m_TelecomServicePrefab,
                    m_GarbageParameters.m_GarbageServicePrefab, m_PoliceParameters.m_PoliceServicePrefab,
                    m_CitizenHappinessParameterData, m_GarbageParameters
                );
            }

            // ������������ԭ���߼�����
            private Entity GetFirstWorkplaceOrSchool(DynamicBuffer<HouseholdCitizen> citizens,
                ref Entity commuteCitizen)
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    Entity citizen = citizens[i].m_Citizen;
                    if (m_Workers.HasComponent(citizen))
                    {
                        commuteCitizen = citizen;
                        return m_Workers[citizen].m_Workplace;
                    }

                    if (m_Students.HasComponent(citizen))
                    {
                        commuteCitizen = citizen;
                        return m_Students[citizen].m_School;
                    }
                }

                return Entity.Null;
            }

            private Entity GetCurrentLocation(DynamicBuffer<HouseholdCitizen> citizens)
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    if (m_CurrentBuildings.TryGetComponent(citizens[i].m_Citizen, out CurrentBuilding b))
                        return b.m_CurrentBuilding;
                    if (m_CurrentTransports.TryGetComponent(citizens[i].m_Citizen, out CurrentTransport t))
                        return t.m_CurrentTransport;
                }

                return Entity.Null;
            }

            private void StartHomeFinding(int jobIndex, Entity household, Entity commuteCitizen, Entity targetLocation,
                Entity oldHome, float minimumScore, bool targetIsOrigin, DynamicBuffer<HouseholdCitizen> citizens)
            {
                // ���� PathInformation ����
                m_CommandBuffer.AddComponent(jobIndex, household, new PathInformation { m_State = PathFlags.Pending });

                // ����Ȩ��
                PathfindWeights weights = default;
                if (commuteCitizen != Entity.Null && m_Citizens.HasComponent(commuteCitizen))
                    weights = CitizenUtils.GetPathfindWeights(m_Citizens[commuteCitizen], new Household(),
                        citizens.Length); // ����Household����
                else
                    weights.m_Value = 0.5f; // Ĭ��Ȩ��

                PathfindParameters parameters = new PathfindParameters
                {
                    m_MaxSpeed = 111.111f,
                    m_WalkSpeed = 1.667f,
                    m_Weights = weights,
                    m_Methods = PathMethod.Pedestrian | PathMethod.PublicTransportDay,
                    m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost,
                    m_PathfindFlags = PathfindFlags.Simplified | PathfindFlags.IgnorePath,
                    m_MaxResultCount = 16
                };

                parameters.m_PathfindFlags |= (PathfindFlags)(targetIsOrigin ? 256 : 128);

                // ����Ѱ·Ŀ��
                SetupQueueTarget originTarget = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.CurrentLocation, m_Methods = PathMethod.Pedestrian,
                    m_Entity = targetLocation
                };
                SetupQueueTarget destTarget = new SetupQueueTarget
                {
                    m_Type = SetupTargetType.FindHome,
                    m_Methods = PathMethod.Pedestrian,
                    m_Entity = household,
                    m_Entity2 = oldHome,
                    m_Value2 = minimumScore
                };

                // �����߼�
                if (m_OwnedVehicles.TryGetBuffer(household, out var vehicles) && vehicles.Length > 0)
                {
                    parameters.m_Methods |= (PathMethod)(targetIsOrigin ? 8194 : 8198);
                    parameters.m_ParkingSize = float.MinValue;
                    parameters.m_IgnoredRules |= RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic |
                                                 RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles;
                    originTarget.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    originTarget.m_RoadTypes |= RoadTypes.Car;
                    destTarget.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                    destTarget.m_RoadTypes |= RoadTypes.Car;
                }

                if (targetIsOrigin)
                {
                    parameters.m_MaxSpeed.y = 277.777f;
                    parameters.m_Methods |= PathMethod.Taxi | PathMethod.PublicTransportNight;
                    parameters.m_TaxiIgnoredRules = Game.Vehicles.VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                }
                else
                {
                    CommonUtils.Swap(ref originTarget, ref destTarget);
                }

                m_CommandBuffer.AddBuffer<PathInformations>(jobIndex, household)
                    .Add(new PathInformations { m_State = PathFlags.Pending });

                m_PathfindQueue.Enqueue(new SetupQueueItem(household, parameters, originTarget, destTarget));
            }
        }

        #endregion

        #region ������̬����

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
            // ԭPropertyUtils class����
            float kHomelessApartmentSize = 0.01f;

            GenericApartmentQuality result = default(GenericApartmentQuality);

            // ԭ flag: ���ڱ����Ƿ�Ϊ�޼ҿɹ�/������סլ״̬
            bool isHomeless = true;

            BuildingPropertyData buildingPropertyData = default(BuildingPropertyData);
            SpawnableBuildingData spawnableBuildingData = default(SpawnableBuildingData);

            if (buildingProperties.HasComponent(buildingPrefab))
            {
                buildingPropertyData = buildingProperties[buildingPrefab];
                isHomeless = false;
            }

            // ԭ buildingData2: ����������ʱ�� buildingData������Ԥ��������
            BuildingData prefabData = buildingDatas[buildingPrefab];

            if (spawnableDatas.HasComponent(buildingPrefab) && !abandoneds.HasComponent(building))
            {
                spawnableBuildingData = spawnableDatas[buildingPrefab];
            }
            else
            {
                isHomeless = true;
            }

            // ���㹫Ԣ��С
            result.apartmentSize = (isHomeless
                ? kHomelessApartmentSize
                : (buildingPropertyData.m_SpaceMultiplier * (float)prefabData.m_LotSize.x *
                    (float)prefabData.m_LotSize.y / math.max(1f, buildingPropertyData.m_ResidentialProperties)));
            result.level = spawnableBuildingData.m_Level;

            // ԭ @int: �����ۼӸ����Ҹ���/�����ı���
            int2 totalScoreAccumulator = default(int2);

            // ԭ healthcareBonuses: ���������������ڴ洢ÿһ�μ�������ʱ�ӳɣ���ֻ����ҽ��
            int2 currentStepBonus;

            // 1. �������񸲸Ǵ����ļӳɣ�ҽ�ơ����֡�������������
            if (serviceCoverages.HasBuffer(buildingData.m_RoadEdge))
            {
                DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = serviceCoverages[buildingData.m_RoadEdge];

                currentStepBonus = CitizenHappinessSystem.GetHealthcareBonuses(buildingData.m_CurvePosition,
                    serviceCoverage, ref locked, healthcareService, in happinessParameterData);
                totalScoreAccumulator += currentStepBonus;

                currentStepBonus = CitizenHappinessSystem.GetEntertainmentBonuses(buildingData.m_CurvePosition,
                    serviceCoverage, cityModifiers, ref locked, entertainmentService, in happinessParameterData);
                totalScoreAccumulator += currentStepBonus;

                result.welfareBonus = CitizenHappinessSystem.GetWelfareValue(buildingData.m_CurvePosition,
                    serviceCoverage, in happinessParameterData);
                result.educationBonus = CitizenHappinessSystem.GetEducationBonuses(buildingData.m_CurvePosition,
                    serviceCoverage, ref locked, educationService, in happinessParameterData, 1);
            }

            // 2. ���㷸���ӳ�/�ͷ�
            int2 crimeBonuses = CitizenHappinessSystem.GetCrimeBonuses(default(CrimeVictim), building, ref crimes,
                ref locked, policeService, in happinessParameterData);
            // �������޼ҿɹ�״̬��Ӧ���ض��ķ����ͷ��߼�
            currentStepBonus = (isHomeless
                ? new int2(0, -happinessParameterData.m_MaxCrimePenalty - crimeBonuses.y)
                : crimeBonuses);
            totalScoreAccumulator += currentStepBonus;

            // 3. ������Ⱦ
            currentStepBonus = CellMapSystemRe.GetGroundPollutionBonuses(building, ref transforms, pollutionMap,
                cityModifiers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 4. ������Ⱦ
            currentStepBonus = CellMapSystemRe.GetAirPollutionBonuses(building, ref transforms, airPollutionMap,
                cityModifiers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 5. ������Ⱦ
            currentStepBonus =
                CellMapSystemRe.GetNoiseBonuses(building, ref transforms, noiseMap, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 6. ���Ÿ���
            currentStepBonus = CitizenHappinessSystem.GetTelecomBonuses(building, ref transforms, telecomCoverages,
                ref locked, telecomService, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 7. ������Ӧ
            currentStepBonus =
                PropertyUtils.GetElectricityBonusForApartmentQuality(building, ref electricityConsumers,
                    in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 8. ��ˮ��Ӧ
            currentStepBonus =
                PropertyUtils.GetWaterBonusForApartmentQuality(building, ref waterConsumers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 9. ��ˮ����
            currentStepBonus =
                PropertyUtils.GetSewageBonusForApartmentQuality(building, ref waterConsumers,
                    in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 10. ˮ��Ⱦ
            currentStepBonus = CitizenHappinessSystem.GetWaterPollutionBonuses(building, ref waterConsumers,
                cityModifiers, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 11. ��������
            currentStepBonus = CitizenHappinessSystem.GetGarbageBonuses(building, ref garbageProducers, ref locked,
                garbageService, in garbageParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 12. �ʼ�����
            currentStepBonus = CitizenHappinessSystem.GetMailBonuses(building, ref mailProducers, ref locked,
                telecomService, in happinessParameterData);
            totalScoreAccumulator += currentStepBonus;

            // 13. �޼ҿɹ�״̬����
            if (isHomeless)
            {
                currentStepBonus = CitizenHappinessSystem.GetHomelessBonuses(in happinessParameterData);
                totalScoreAccumulator += currentStepBonus;
            }

            result.score = totalScoreAccumulator.x + totalScoreAccumulator.y;
            return result;
        }

        public static float GetPropertyScore(Entity property, Entity household,
            DynamicBuffer<HouseholdCitizen> citizenBuffer, ref ComponentLookup<PrefabRef> prefabRefs,
            ref ComponentLookup<BuildingPropertyData> buildingProperties, ref ComponentLookup<Building> buildings,
            ref ComponentLookup<BuildingData> buildingDatas, ref ComponentLookup<Household> households,
            ref ComponentLookup<Citizen> citizens, ref ComponentLookup<Game.Citizens.Student> students,
            ref ComponentLookup<Worker> workers, ref ComponentLookup<SpawnableBuildingData> spawnableDatas,
            ref ComponentLookup<CrimeProducer> crimes, ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages,
            ref ComponentLookup<Locked> locked, ref ComponentLookup<ElectricityConsumer> electricityConsumers,
            ref ComponentLookup<WaterConsumer> waterConsumers, ref ComponentLookup<GarbageProducer> garbageProducers,
            ref ComponentLookup<MailProducer> mailProducers, ref ComponentLookup<Transform> transforms,
            ref ComponentLookup<Abandoned> abandoneds, ref ComponentLookup<Game.Buildings.Park> parks,
            ref BufferLookup<ResourceAvailability> availabilities, NativeArray<int> taxRates,
            NativeArray<GroundPollution> pollutionMap, NativeArray<AirPollution> airPollutionMap,
            NativeArray<NoisePollution> noiseMap, CellMapData<TelecomCoverage> telecomCoverages,
            DynamicBuffer<CityModifier> cityModifiers, Entity healthcareService, Entity entertainmentService,
            Entity educationService, Entity telecomService, Entity garbageService, Entity policeService,
            CitizenHappinessParameterData citizenHappinessParameterData, GarbageParameterData garbageParameterData)
        {
            // ������ҵ�Ƿ�����
            if (!buildings.HasComponent(property))
            {
                return float.NegativeInfinity;
            }

            // flag -> isAlreadyMovedIn: ������ͥ�Ƿ��Ѿ�����
            bool isAlreadyMovedIn = (households[household].m_Flags & HouseholdFlags.MovedIn) != 0;

            // flag2 -> isHomelessShelter: ��������ҵ�Ƿ�Ϊ���������繫԰������������
            bool isHomelessShelter = IsHomelessShelterBuilding(property, ref parks, ref abandoneds);

            // �����Ǳ���������δ���룬�����������֣�����������ѡ��������������
            if (isHomelessShelter && !isAlreadyMovedIn)
            {
                return float.NegativeInfinity;
            }

            Building buildingInstance = buildings[property];
            Entity prefab = prefabRefs[property].m_Prefab;

            // ��ȡͨ�ù�Ԣ��������
            GenericApartmentQuality genericApartmentQuality = GetGenericApartmentQuality(property, prefab,
                ref buildingInstance, ref buildingProperties, ref buildingDatas, ref spawnableDatas, ref crimes,
                ref serviceCoverages, ref locked, ref electricityConsumers, ref waterConsumers, ref garbageProducers,
                ref mailProducers, ref transforms, ref abandoneds, pollutionMap, airPollutionMap, noiseMap,
                telecomCoverages, cityModifiers, healthcareService, entertainmentService, educationService,
                telecomService, garbageService, policeService, citizenHappinessParameterData, garbageParameterData);

            int totalCitizenCount = citizenBuffer.Length; // length

            // ��ʼ��ͳ�Ʊ���
            float averageCommuteTime = 0f; // num: �ۼ�ͨ��ʱ�䣬������ƽ��ֵ
            int commuterCount = 0; // num2: �ϰ�����ѧ������
            int taxpayerCount = 0; // num3: �Ƕ�ͯ����˰�˿ڣ�����
            int averageHappiness = 0; // num4: �ۼ��Ҹ��ȣ�������ƽ��ֵ
            int childCount = 0; // num5: ��ͯ����
            int averageTaxBonus = 0; // num6: �ۼ�˰�ռӳɣ�������ƽ��ֵ

            // ������ͥ��Ա
            for (int i = 0; i < citizenBuffer.Length; i++)
            {
                Entity citizenEntity = citizenBuffer[i].m_Citizen;
                Citizen citizenData = citizens[citizenEntity];

                // �ۼ��Ҹ���
                averageHappiness += citizenData.Happiness;

                if (citizenData.GetAge() == CitizenAge.Child)
                {
                    childCount++;
                }
                else
                {
                    taxpayerCount++;
                    // �ۼ�˰�մ������Ҹ��ȼӳ�
                    averageTaxBonus += CitizenHappinessSystem.GetTaxBonuses(citizenData.GetEducationLevel(), taxRates,
                        cityModifiers, in citizenHappinessParameterData).y;
                }

                // ����ͨ��ʱ�䣨ѧ�������ˣ�
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

            // ����ƽ��ͨ��ʱ��
            if (commuterCount > 0)
            {
                averageCommuteTime /= (float)commuterCount;
            }

            // ����ƽ���Ҹ��Ⱥ�ƽ��˰�ռӳ�
            if (citizenBuffer.Length > 0)
            {
                averageHappiness /= citizenBuffer.Length;
                if (taxpayerCount > 0)
                {
                    averageTaxBonus /= taxpayerCount;
                }
            }

            // ��ȡ���񸲸�������
            float serviceAvailability = PropertyUtils.GetServiceAvailability(buildingInstance.m_RoadEdge,
                buildingInstance.m_CurvePosition, availabilities);

            // �����˿ڽṹ�ͻ����������㻺���Ĺ�Ԣ����
            float cachedApartmentQuality = GetCachedApartmentQuality(totalCitizenCount, childCount, averageHappiness,
                genericApartmentQuality);

            // num7 -> shelterPenalty: �����Ǳ������������޴������ֳͷ�
            float shelterPenalty = (isHomelessShelter ? (-1000) : 0);

            // �������ֹ�ʽ�������� + ������*10 + ˰�պ���*2 - ͨ��ʱ�� + �������ͷ�
            return serviceAvailability + cachedApartmentQuality * 10f + (float)(2 * averageTaxBonus) -
                averageCommuteTime + shelterPenalty;
        }

        public static float GetCachedApartmentQuality(int familySize, int children, int averageHappiness,
            GenericApartmentQuality quality)
        {
            int2 cachedWelfareBonuses =
                CitizenHappinessSystem.GetCachedWelfareBonuses(quality.welfareBonus, averageHappiness);
            return CitizenHappinessSystem.GetApartmentWellbeing(quality.apartmentSize / (float)familySize,
                       quality.level) + math.sqrt(children) * (quality.educationBonus.x + quality.educationBonus.y) +
                   (float)cachedWelfareBonuses.x + (float)cachedWelfareBonuses.y + quality.score;
        }

        public static bool IsHomelessShelterBuilding(Entity propertyEntity,
            ref ComponentLookup<Game.Buildings.Park> parks, ref ComponentLookup<Abandoned> abandoneds)
        {
            if (!parks.HasComponent(propertyEntity))
            {
                return abandoneds.HasComponent(propertyEntity);
            }

            return true;
        }

        public static Entity GetHouseholdHomeBuilding(Entity householdEntity,
            ref ComponentLookup<PropertyRenter> propertyRenters,
            ref ComponentLookup<HomelessHousehold> homelessHouseholds)
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

        public static bool IsHouseholdNeedSupport(DynamicBuffer<HouseholdCitizen> householdCitizens,
            ref ComponentLookup<Citizen> citizens, ref ComponentLookup<Game.Citizens.Student> students)
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
    /// Harmony�޲�CitizenPathFindSetup.SetupFindHomeJob
    /// </summary>

    // 1. �����Զ����� Job
    // ֱ�Ӹ���ԭJob�������ֶΣ�ȷ�����ݶ��롣
    [BurstCompile]
    public struct CustomSetupFindHomeJob : IJobChunk
    {
        // --- �ֶθ��̿�ʼ ---
        [ReadOnly] public EntityTypeHandle m_EntityType;
        [ReadOnly] public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly] public ComponentLookup<Building> m_Buildings;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneDatas;
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;
        [ReadOnly] public BufferLookup<Game.Net.ServiceCoverage> m_Coverages;

        public PathfindSetupSystem.SetupData m_SetupData; // ע�⣺���� PathfindSetupSystem �ڲ������� struct

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
        // --- �ֶθ��̽��� ---

        // ʵ�� Execute ����
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            NativeArray<Entity> chunkEntities = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<PrefabRef> chunkPrefabRefs = chunk.GetNativeArray(ref this.m_PrefabType);
            BufferAccessor<Renter> chunkRenters = chunk.GetBufferAccessor(ref this.m_RenterType);

            for (int i = 0; i < this.m_SetupData.Length; i++)
            {
                this.m_SetupData.GetItem(i, out var _, out var targetSeeker);
                // �޸�: ʹ�� i ������ chunkIndex ��Ϊ�������ӵ�һ���֣���ֹ��ͬ chunk ������ seeker ��Ϊ��ȫһ��
                Unity.Mathematics.Random random = targetSeeker.m_RandomSeed.GetRandom(i + unfilteredChunkIndex * 1000);

                Entity householdEntity = targetSeeker.m_SetupQueueTarget.m_Entity;
                if (!this.m_HouseholdCitizens.TryGetBuffer(householdEntity, out var householdMembers)) continue;

                bool isAlreadyInShelter = this.m_HomelessHouseholds.HasComponent(householdEntity) &&
                                          this.m_HomelessHouseholds[householdEntity].m_TempHome != Entity.Null;

                // Ԥ�ȼ�����ͥ����״�����������ڲ�ѭ���ظ�����
                int householdIncome = EconomyUtils.GetHouseholdIncome(
                    householdMembers, ref this.m_Workers, ref this.m_Citizens,
                    ref this.m_HealthProblems, ref this.m_EconomyParameters, this.m_TaxRates);
                bool needsWelfare =
                    CitizenUtils.IsHouseholdNeedSupport(householdMembers, ref this.m_Citizens, ref this.m_Students);

                for (int j = 0; j < chunkEntities.Length; j++)
                {
                    Entity buildingEntity = chunkEntities[j];
                    Entity buildingPrefab = chunkPrefabRefs[j].m_Prefab;
                    Building buildingComponent = this.m_Buildings[buildingEntity];

                    // ��������
                    if (buildingComponent.m_RoadEdge == Entity.Null ||
                        !this.m_Coverages.HasBuffer(buildingComponent.m_RoadEdge) ||
                        !this.m_BuildingDatas.HasComponent(buildingPrefab))
                    {
                        continue;
                    }

                    // �������߼� (����ԭ�����ⲿ�ֲ�������ƿ��)
                    if (BuildingUtils.IsHomelessShelterBuilding(buildingEntity, ref this.m_Parks,
                            ref this.m_Abandoneds))
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
                                // ����Cost����: ӵ���ȳͷ���Ӧ���󣬷���������Ը˯����
                                float cost = 10f * policeCoverage +
                                             100f * (float)chunkRenters[j].Length / (float)shelterCapacity +
                                             2000f; // ���ͻ����ͷ����ñ������ȡ��޽���������������
                                targetSeeker.FindTargets(buildingEntity, cost);
                            }
                        }

                        continue;
                    }

                    // === ����ס���߼��Ż� ===

                    // 1. ���ٹ��ˣ�����û�� PropertiesOnMarket ������ֱ������
                    if (!this.m_PropertiesOnMarket.HasComponent(buildingEntity)) continue;

                    int askingRent = this.m_PropertiesOnMarket[buildingEntity].m_AskingRent;

                    // 2. ���ٹ��ˣ���������
                    int totalProperties = 1;
                    if (this.m_BuildingProperties.HasComponent(buildingPrefab))
                    {
                        totalProperties = this.m_BuildingProperties[buildingPrefab].CountProperties();
                    }

                    // �������ˣ����Բ�Ҫ�˷�ʱ������Score
                    if (chunkRenters[j].Length >= totalProperties) continue;

                    // 3. ���ٹ��ˣ��������� (Affordability)
                    int garbageFeePerHousehold = this.m_ServiceFeeParameterData.m_GarbageFeeRCIO.x / totalProperties;

                    Entity zonePrefabEntity = this.m_SpawnableDatas[buildingPrefab].m_ZonePrefab;
                    float rentBudgetFactor = 1f;
                    if (this.m_ZonePropertiesDatas.TryGetComponent(zonePrefabEntity, out var zoneProps))
                    {
                        var density = PropertyUtils.GetZoneDensity(this.m_ZoneDatas[zonePrefabEntity], zoneProps);
                        rentBudgetFactor =
                            density switch { ZoneDensity.Medium => 0.7f, ZoneDensity.Low => 0.5f, _ => 1f };
                    }

                    bool canAfford = needsWelfare || ((float)(askingRent + garbageFeePerHousehold) <=
                                                      (float)householdIncome * rentBudgetFactor);

                    // �����Ż�: ������������ֱ����������Ҫ�� Score ����
                    if (!canAfford) continue;

                    // 4. ��ѡ�Ż������������� (Rent Band Filter)
                    // �������ǵ��ܶ���(ͨ����House)������ͥ�ǳ���������Ǹ��ܶ�(Apartment)������ͥ�ǳ���
                    // ������������ʽ���ˡ����磺�������������𼫵�(askingRent < income * 0.05)�����ܲ����ǡ�
                    // ��ʱ���ӣ�����Ӱ����Ϸ�߼���ȷ�ԡ�

                    // 5. ���������ּ��� (����ֻ�����������п�λ�ķ��ӲŻ��ߵ�����)
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

                    // 6. �������� Cost
                    // propertyScore Խ��Խ�ã�Cost Խ��Խ�á�
                    // ����ӵ���ȳͷ� (prefer empty houses slightly)
                    float finalCost = -propertyScore +
                                      500f * (chunkRenters[j].Length / (float)totalProperties) +
                                      random.NextFloat(0, 100f); // ���������������ø��õķ��Ӹ����ױ�ѡ��

                    targetSeeker.FindTargets(buildingEntity, finalCost);
                }
            }
        }
    }

    // =========================================================
    // 2. Harmony ����
    // =========================================================
    [HarmonyPatch]
    public static class PathfindSetupSystem_FindTargets_Patch
    {
        // ���ϼ�����
        //private static int _callCount = 0;
        //private static bool _hasLoggedSuccess = false;

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // Ŀ�꣺PathfindSetupSystem.FindTargets(SetupTargetType, in SetupData)
            // ע�⣺SetupData �ǽṹ������
            return typeof(PathfindSetupSystem).GetMethod("FindTargets",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[] { typeof(SetupTargetType), typeof(PathfindSetupSystem.SetupData).MakeByRefType() },
                null);
        }

        // ���� EntityQuery
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

        // ���������ܱ��� Dependency ���Ե�ί��
        private static Func<SystemBase, JobHandle> _getDependencyAccessor;

        private static void EnsureInitialized(PathfindSetupSystem system)
        {
            // 1. ��ʼ�������� (���� Dependency ���ɷ��ʵ�����)
            if (_getDependencyAccessor == null)
            {
                // ��ȡ SystemBase.Dependency �� Getter ����
                MethodInfo dependencyGetter = AccessTools.PropertyGetter(typeof(SystemBase), "Dependency");
                // ����ǿ����ί�У����ܱ�ֱ�ӷ��� Invoke ���ö�
                _getDependencyAccessor =
                    (Func<SystemBase, JobHandle>)Delegate.CreateDelegate(typeof(Func<SystemBase, JobHandle>),
                        dependencyGetter);
            }

            // 2. ��ʼ�� Queries (���� CS1503 ����)
            if (_findHomeQuery != default) return;

            // ʹ�� system.GetSetupQuery (���� PathfindSetupSystem ���� Public ������ר��Ϊ�˱�¶�� Setup �ṹ���õ�)
            // �������ܱ����� system.GetEntityQuery

            // �ع� FindHomeQuery �� Desc
            // ����ϵͳ���з�������ʹ��QueryBuilder�������ܿ���
            var desc1 = new EntityQueryDesc
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

            // ��ʼ������ Query��ע������Ҳʹ�� GetSetupQuery
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
            ref PathfindSetupSystem.SetupData setupData, // ʹ�� ref ��Ӧ in/ref
            ref JobHandle __result)
        {
            // 1. ���ٹ���
            // ������������Ҫ�޸ĵ����ͣ�ִ��ԭ���߼�
            if (targetType != SetupTargetType.FindHome)
            {
                return true;
            }

            // 2. ������־ (ÿ 600 �ε��û��״ε��ô�ӡһ�Σ�����ˢ��)
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

            // ���� Job
            // Harmony�޲���ʹ��SystemAPI��ֱ�Ӵ�__instance ��ȡ����ʱ����
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

                // ��ȡ�ⲿ����
                m_TaxRates = taxSystem.GetTaxRates(),
                m_PollutionMap = groundPollutionSystem.GetMap(true, out var dep1),
                m_AirPollutionMap = airPollutionSystem.GetMap(true, out var dep2),
                m_NoiseMap = noisePollutionSystem.GetMap(true, out var dep3),
                m_TelecomCoverages = telecomCoverageSystem.GetData(true, out var dep4),

                // ������������
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

            // ��ȡ������ Dependency
            JobHandle inputDeps = _getDependencyAccessor(__instance);

            // �ϲ�����
            JobHandle combinedDeps = JobUtils.CombineDependencies(inputDeps, dep1, dep2, dep3, dep4);

            // ���� Job
            JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, _findHomeQuery, combinedDeps);

            // ���� Reader ��������ֹ��Դ����������
            groundPollutionSystem.AddReader(jobHandle);
            airPollutionSystem.AddReader(jobHandle);
            noisePollutionSystem.AddReader(jobHandle);
            telecomCoverageSystem.AddReader(jobHandle);
            taxSystem.AddReader(jobHandle);

            // ���÷���ֵ����ֹԭ����ִ��
            __result = jobHandle;
            return false;
        }
    }
}


