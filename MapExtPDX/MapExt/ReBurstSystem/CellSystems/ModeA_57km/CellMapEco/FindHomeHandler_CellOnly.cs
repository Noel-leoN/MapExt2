// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

// 原版来源：Game.Simulation.CitizenPathfindSetup（SetupFindHomeJob / SetupFindHome / m_FreePropertyQuery）
//
// CellOnly 配置（经济修复总开关或找房组关闭）下替原版处理 SetupTargetType.FindHome。
// 原版 Job 是游戏出厂的 Burst 代码，PropertyUtils.GetPropertyScore 里的污染与噪音采样已按原版 14336／256 折叠成常量，
// 扩展模式下读到的格子与建筑位置对不上。本文件复制原版，只改两处：
//   1. 评分改调本模式的 XCellMapSystemRe.GetPropertyScore（与原版同签名，三张污染图按本模式尺寸采样）；
//   2. 排程后给三张污染图、电信与税率登记读者（与 C1 一致；原版不登记）。
// 不带 C1 的候选上限、随机起点与垃圾费负担判定：CellOnly 只修尺寸。
// C1 完整版的 FindHome 由 PathfindSetupSystem_FindTargets_Patch 接管，与本 handler 按配置二选一（见 ModeSystems.Declare）。

using System;
using Colossal.Entities;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using MapExtPDX.EcoShared;
using MapExtPDX.MapExt.Core;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace MapExtPDX.ModeA
{
    /// <summary>
    /// CellOnly 配置的找房寻路 Setup：挂在 <see cref="PathfindSetupDispatcher"/> 上，接管 <see cref="SetupTargetType.FindHome"/>。
    /// 排程与原版 <c>CitizenPathfindSetup.SetupFindHome</c> 一致，评分改用本模式的 <see cref="XCellMapSystemRe"/>。
    /// </summary>
    public sealed class FindHomeHandler_CellOnly : IFindTargetsHandler
    {
        private const string Tag = nameof(FindHomeHandler_CellOnly);

        // === Fields ===

        #region Fields

        private bool _initialized;
        private World _cachedWorld;

        // PathfindSetupSystem.Dependency 是 protected，经反射读取（与 C1、LeisureHandler 同法）
        private static Func<SystemBase, JobHandle> _getDependencyAccessor;

        private EntityQuery _findHomeQuery;
        private EntityQuery _economyParameterQuery;
        private EntityQuery _healthcareParameterQuery;
        private EntityQuery _parkParameterQuery;
        private EntityQuery _educationParameterQuery;
        private EntityQuery _telecomParameterQuery;
        private EntityQuery _garbageParameterQuery;
        private EntityQuery _policeParameterQuery;
        private EntityQuery _citizenHappinessParameterQuery;

        // 原版系统引用：三张污染图的 GetMap／AddReader 会转发到本模式的替换系统
        private GroundPollutionSystem _groundPollutionSystem;
        private AirPollutionSystem _airPollutionSystem;
        private NoisePollutionSystem _noisePollutionSystem;
        private TelecomCoverageSystem _telecomCoverageSystem;
        private TaxSystem _taxSystem;
        private CitySystem _citySystem;

        // --- 类型句柄 ---
        private EntityTypeHandle _entityType;
        private BufferTypeHandle<Renter> _renterType;
        private ComponentTypeHandle<PrefabRef> _prefabRefType;

        // --- Component / Buffer Lookup ---
        private ComponentLookup<Building> _buildings;
        private ComponentLookup<Household> _households;
        private ComponentLookup<HomelessHousehold> _homelessHouseholds;
        private ComponentLookup<Worker> _workers;
        private ComponentLookup<Game.Citizens.Student> _students;
        private ComponentLookup<Citizen> _citizens;
        private ComponentLookup<HealthProblem> _healthProblems;
        private ComponentLookup<Game.Objects.Transform> _transforms;
        private ComponentLookup<PropertyOnMarket> _propertiesOnMarket;
        private ComponentLookup<CrimeProducer> _crimes;
        private ComponentLookup<Game.Buildings.Park> _parks;
        private ComponentLookup<Abandoned> _abandoneds;
        private ComponentLookup<ElectricityConsumer> _electricityConsumers;
        private ComponentLookup<WaterConsumer> _waterConsumers;
        private ComponentLookup<GarbageProducer> _garbageProducers;
        private ComponentLookup<MailProducer> _mailProducers;
        private ComponentLookup<PrefabRef> _prefabs;
        private ComponentLookup<BuildingData> _prefabBuildingDatas;
        private ComponentLookup<BuildingPropertyData> _buildingProperties;
        private ComponentLookup<SpawnableBuildingData> _spawnableDatas;
        private ComponentLookup<ZoneData> _zoneDatas;
        private ComponentLookup<ZonePropertiesData> _zonePropertiesDatas;
        private ComponentLookup<Locked> _lockeds;
        private ComponentLookup<CompanyData> _companies;
        private BufferLookup<Game.Economy.Resources> _resources;
        private BufferLookup<ResourceAvailability> _availabilities;
        private BufferLookup<Game.Net.ServiceCoverage> _serviceCoverages;
        private BufferLookup<CityModifier> _cityModifiers;
        private BufferLookup<HouseholdCitizen> _householdCitizens;

        #endregion

        // === Lifecycle ===

        #region Lifecycle

        /// <summary>读本会话的配置快照，与 ModeSystems.Declare 的 CellOnly 分支同一条件：经济总开关与找房组没有同时开启。</summary>
        public bool Enabled => AppliedConfig.Current is { } config && !(config.EconomyFix && config.HouseholdProperty);

        public JobHandle Schedule(PathfindSetupSystem system, ref PathfindSetupSystem.SetupData setupData)
        {
            EnsureInitialized(system);

            // 对齐原版 SetupFindHome 的 .Update(system)，只列本 Job 用到的
            _entityType.Update(system);
            _renterType.Update(system);
            _prefabRefType.Update(system);
            _buildings.Update(system);
            _households.Update(system);
            _homelessHouseholds.Update(system);
            _prefabBuildingDatas.Update(system);
            _serviceCoverages.Update(system);
            _propertiesOnMarket.Update(system);
            _availabilities.Update(system);
            _spawnableDatas.Update(system);
            _zoneDatas.Update(system);
            _zonePropertiesDatas.Update(system);
            _buildingProperties.Update(system);
            _prefabs.Update(system);
            _workers.Update(system);
            _students.Update(system);
            _citizens.Update(system);
            _crimes.Update(system);
            _lockeds.Update(system);
            _transforms.Update(system);
            _cityModifiers.Update(system);
            _healthProblems.Update(system);
            _householdCitizens.Update(system);
            _abandoneds.Update(system);
            _parks.Update(system);
            _electricityConsumers.Update(system);
            _waterConsumers.Update(system);
            _garbageProducers.Update(system);
            _mailProducers.Update(system);
            _resources.Update(system);
            _companies.Update(system);

            JobHandle inputDeps = _getDependencyAccessor(system);

            var job = new SetupFindHomeJob
            {
                m_EntityType = _entityType,
                m_RenterType = _renterType,
                m_PrefabType = _prefabRefType,
                m_Buildings = _buildings,
                m_Households = _households,
                m_HomelessHouseholds = _homelessHouseholds,
                m_BuildingDatas = _prefabBuildingDatas,
                m_Coverages = _serviceCoverages,
                m_PropertiesOnMarket = _propertiesOnMarket,
                m_Availabilities = _availabilities,
                m_SpawnableDatas = _spawnableDatas,
                m_BuildingProperties = _buildingProperties,
                m_PrefabRefs = _prefabs,
                m_ServiceCoverages = _serviceCoverages,
                m_Citizens = _citizens,
                m_Crimes = _crimes,
                m_Lockeds = _lockeds,
                m_Companies = _companies,
                m_Transforms = _transforms,
                m_CityModifiers = _cityModifiers,
                m_HouseholdCitizens = _householdCitizens,
                m_Abandoneds = _abandoneds,
                m_Parks = _parks,
                m_ElectricityConsumers = _electricityConsumers,
                m_WaterConsumers = _waterConsumers,
                m_GarbageProducers = _garbageProducers,
                m_MailProducers = _mailProducers,
                m_HealthProblems = _healthProblems,
                m_Workers = _workers,
                m_Students = _students,
                m_ResourcesBufs = _resources,
                m_ZoneDatas = _zoneDatas,
                m_ZonePropertiesDatas = _zonePropertiesDatas,
                m_TaxRates = _taxSystem.GetTaxRates(),
                m_PollutionMap = _groundPollutionSystem.GetMap(readOnly: true, out JobHandle groundPollutionDeps),
                m_AirPollutionMap = _airPollutionSystem.GetMap(readOnly: true, out JobHandle airPollutionDeps),
                m_NoiseMap = _noisePollutionSystem.GetMap(readOnly: true, out JobHandle noisePollutionDeps),
                m_TelecomCoverages = _telecomCoverageSystem.GetData(readOnly: true, out JobHandle telecomCoverageDeps),
                m_HealthcareParameters = _healthcareParameterQuery.GetSingleton<HealthcareParameterData>(),
                m_ParkParameters = _parkParameterQuery.GetSingleton<ParkParameterData>(),
                m_EducationParameters = _educationParameterQuery.GetSingleton<EducationParameterData>(),
                m_EconomyParameters = _economyParameterQuery.GetSingleton<EconomyParameterData>(),
                m_TelecomParameters = _telecomParameterQuery.GetSingleton<TelecomParameterData>(),
                m_GarbageParameters = _garbageParameterQuery.GetSingleton<GarbageParameterData>(),
                m_PoliceParameters = _policeParameterQuery.GetSingleton<PoliceConfigurationData>(),
                m_CitizenHappinessParameterData = _citizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
                m_City = _citySystem.City,
                m_SetupData = setupData
            };

            JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(job, _findHomeQuery,
                JobUtils.CombineDependencies(inputDeps, groundPollutionDeps, airPollutionDeps, noisePollutionDeps, telecomCoverageDeps));

            // 原版不登记读者；与 C1 一致补上，之后的写入方会等本 Job 读完
            _groundPollutionSystem.AddReader(jobHandle);
            _airPollutionSystem.AddReader(jobHandle);
            _noisePollutionSystem.AddReader(jobHandle);
            _telecomCoverageSystem.AddReader(jobHandle);
            _taxSystem.AddReader(jobHandle);
            return jobHandle;
        }

        /// <summary>首次排程或 World 重建后，按原版 CitizenPathfindSetup.OnCreate 建查询、句柄、Lookup 与系统引用。</summary>
        private void EnsureInitialized(PathfindSetupSystem system)
        {
            if (_getDependencyAccessor == null)
            {
                var dependencyGetter = AccessTools.PropertyGetter(typeof(SystemBase), "Dependency");
                _getDependencyAccessor = (Func<SystemBase, JobHandle>)Delegate.CreateDelegate(
                    typeof(Func<SystemBase, JobHandle>), dependencyGetter);
            }

            World world = system.World;
            if (_initialized && _cachedWorld == world && _cachedWorld.IsCreated)
                return;
            _initialized = true;
            _cachedWorld = world;

            // 与原版 m_FindHomeQuery 一致：收容所（Abandoned／Park）与在售住宅两段
            _findHomeQuery = system.GetSetupQuery(
                new EntityQueryDesc
                {
                    All = new[] { ComponentType.ReadOnly<Building>() },
                    Any = new[] { ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Game.Buildings.Park>() },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(), ComponentType.ReadOnly<Temp>()
                    }
                },
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<PropertyOnMarket>(), ComponentType.ReadOnly<ResidentialProperty>(),
                        ComponentType.ReadOnly<Building>()
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Abandoned>(), ComponentType.ReadOnly<Deleted>(), ComponentType.ReadOnly<Destroyed>(),
                        ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Condemned>()
                    }
                });
            _economyParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<EconomyParameterData>());
            _healthcareParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<HealthcareParameterData>());
            _parkParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<ParkParameterData>());
            _educationParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<EducationParameterData>());
            _telecomParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<TelecomParameterData>());
            _garbageParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<GarbageParameterData>());
            _policeParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            _citizenHappinessParameterQuery = system.GetSetupQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());

            _groundPollutionSystem = world.GetOrCreateSystemManaged<GroundPollutionSystem>();
            _airPollutionSystem = world.GetOrCreateSystemManaged<AirPollutionSystem>();
            _noisePollutionSystem = world.GetOrCreateSystemManaged<NoisePollutionSystem>();
            _telecomCoverageSystem = world.GetOrCreateSystemManaged<TelecomCoverageSystem>();
            _taxSystem = world.GetOrCreateSystemManaged<TaxSystem>();
            _citySystem = world.GetOrCreateSystemManaged<CitySystem>();

            _entityType = system.GetEntityTypeHandle();
            _renterType = system.GetBufferTypeHandle<Renter>(isReadOnly: true);
            _prefabRefType = system.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);

            _buildings = system.GetComponentLookup<Building>(isReadOnly: true);
            _households = system.GetComponentLookup<Household>(isReadOnly: true);
            _homelessHouseholds = system.GetComponentLookup<HomelessHousehold>(isReadOnly: true);
            _workers = system.GetComponentLookup<Worker>(isReadOnly: true);
            _students = system.GetComponentLookup<Game.Citizens.Student>(isReadOnly: true);
            _citizens = system.GetComponentLookup<Citizen>(isReadOnly: true);
            _healthProblems = system.GetComponentLookup<HealthProblem>(isReadOnly: true);
            _transforms = system.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true);
            _propertiesOnMarket = system.GetComponentLookup<PropertyOnMarket>(isReadOnly: true);
            _crimes = system.GetComponentLookup<CrimeProducer>(isReadOnly: true);
            _parks = system.GetComponentLookup<Game.Buildings.Park>(isReadOnly: true);
            _abandoneds = system.GetComponentLookup<Abandoned>(isReadOnly: true);
            _electricityConsumers = system.GetComponentLookup<ElectricityConsumer>(isReadOnly: true);
            _waterConsumers = system.GetComponentLookup<WaterConsumer>(isReadOnly: true);
            _garbageProducers = system.GetComponentLookup<GarbageProducer>(isReadOnly: true);
            _mailProducers = system.GetComponentLookup<MailProducer>(isReadOnly: true);
            _prefabs = system.GetComponentLookup<PrefabRef>(isReadOnly: true);
            _prefabBuildingDatas = system.GetComponentLookup<BuildingData>(isReadOnly: true);
            _buildingProperties = system.GetComponentLookup<BuildingPropertyData>(isReadOnly: true);
            _spawnableDatas = system.GetComponentLookup<SpawnableBuildingData>(isReadOnly: true);
            _zoneDatas = system.GetComponentLookup<ZoneData>(isReadOnly: true);
            _zonePropertiesDatas = system.GetComponentLookup<ZonePropertiesData>(isReadOnly: true);
            _lockeds = system.GetComponentLookup<Locked>(isReadOnly: true);
            _companies = system.GetComponentLookup<CompanyData>(isReadOnly: true);
            _resources = system.GetBufferLookup<Game.Economy.Resources>(isReadOnly: true);
            _availabilities = system.GetBufferLookup<ResourceAvailability>(isReadOnly: true);
            _serviceCoverages = system.GetBufferLookup<Game.Net.ServiceCoverage>(isReadOnly: true);
            _cityModifiers = system.GetBufferLookup<CityModifier>(isReadOnly: true);
            _householdCitizens = system.GetBufferLookup<HouseholdCitizen>(isReadOnly: true);

            ModLog.Ok(Tag, $"已初始化（World={world.Name}）");
        }

        #endregion

        // === Jobs ===

        #region Jobs

        /// <summary>
        /// 原版 <c>SetupFindHomeJob</c> 的副本：字段名、顺序与逻辑不变，评分改调本模式的 <see cref="XCellMapSystemRe.GetPropertyScore"/>。
        /// </summary>
        [BurstCompile]
        private struct SetupFindHomeJob : IJobChunk
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
            [ReadOnly] public ComponentLookup<CompanyData> m_Companies;
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
            public CitizenHappinessParameterData m_CitizenHappinessParameterData;
            [ReadOnly] public Entity m_City;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabType);
                BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor(ref m_RenterType);
                for (int i = 0; i < m_SetupData.Length; i++)
                {
                    m_SetupData.GetItem(i, out var _, out var targetSeeker);
                    Unity.Mathematics.Random random = targetSeeker.m_RandomSeed.GetRandom(unfilteredChunkIndex);
                    Entity entity2 = targetSeeker.m_SetupQueueTarget.m_Entity;
                    if (!m_HouseholdCitizens.TryGetBuffer(entity2, out var bufferData))
                    {
                        continue;
                    }
                    bool flag = m_HomelessHouseholds.HasComponent(entity2) && m_HomelessHouseholds[entity2].m_TempHome != Entity.Null;
                    for (int j = 0; j < nativeArray.Length; j++)
                    {
                        Entity entity3 = nativeArray[j];
                        Entity prefab = nativeArray2[j].m_Prefab;
                        Building building = m_Buildings[entity3];
                        if (!(building.m_RoadEdge != Entity.Null) || !m_Coverages.HasBuffer(building.m_RoadEdge) || !m_BuildingDatas.HasComponent(prefab))
                        {
                            continue;
                        }
                        if (BuildingUtils.IsHomelessShelterBuilding(entity3, ref m_Parks, ref m_Abandoneds))
                        {
                            if (!flag)
                            {
                                float serviceCoverage = NetUtils.GetServiceCoverage(m_Coverages[building.m_RoadEdge], CoverageService.Police, building.m_CurvePosition);
                                int shelterHomelessCapacity = BuildingUtils.GetShelterHomelessCapacity(prefab, ref m_BuildingDatas, ref m_BuildingProperties);
                                if (bufferAccessor[j].Length < shelterHomelessCapacity)
                                {
                                    targetSeeker.FindTargets(entity3, 100f * serviceCoverage + 1000f * (float)bufferAccessor[j].Length / (float)shelterHomelessCapacity + 10000f);
                                }
                            }
                            continue;
                        }
                        int askingRent = m_PropertiesOnMarket[entity3].m_AskingRent;
                        int num = 0;
                        int num2 = 1;
                        int num3 = 0;
                        if (m_BuildingProperties.HasComponent(prefab))
                        {
                            num2 = m_BuildingProperties[prefab].CountProperties();
                            num = m_BuildingProperties[prefab].CountProperties(Game.Zones.AreaType.Residential);
                            num3 += m_BuildingProperties[prefab].CountProperties(Game.Zones.AreaType.Commercial);
                            num3 += m_BuildingProperties[prefab].CountProperties(Game.Zones.AreaType.Industrial);
                        }
                        if (bufferAccessor[j].Length >= num2)
                        {
                            continue;
                        }
                        if (num3 == 0)
                        {
                            if (bufferAccessor[j].Length >= num)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            int num4 = 0;
                            for (int k = 0; k < bufferAccessor[j].Length; k++)
                            {
                                if (m_Companies.HasComponent(bufferAccessor[j][k]))
                                {
                                    num4++;
                                }
                            }
                            if (bufferAccessor[j].Length - num4 >= num)
                            {
                                continue;
                            }
                        }
                        int householdIncome = EconomyUtils.GetHouseholdIncome(bufferData, ref m_Workers, ref m_Citizens, ref m_HealthProblems, ref m_EconomyParameters, m_TaxRates);
                        bool flag2 = CitizenUtils.IsHouseholdNeedSupport(bufferData, ref m_Citizens, ref m_Students);
                        Entity zonePrefab = m_SpawnableDatas[prefab].m_ZonePrefab;
                        if (m_ZonePropertiesDatas.TryGetComponent(zonePrefab, out var componentData))
                        {
                            float num5 = PropertyUtils.GetZoneDensity(m_ZoneDatas[zonePrefab], componentData) switch
                            {
                                ZoneDensity.Low => 0.5f,
                                ZoneDensity.Medium => 0.7f,
                                _ => 1f,
                            };
                            if (flag2 || !((float)askingRent > (float)householdIncome * num5))
                            {
                                // [MOD] 唯一改动：PropertyUtils.GetPropertyScore 换成本模式的 XCellMapSystemRe.GetPropertyScore（同签名）
                                float propertyScore = XCellMapSystemRe.GetPropertyScore(entity3, entity2, bufferData, ref m_PrefabRefs, ref m_BuildingProperties, ref m_Buildings, ref m_BuildingDatas, ref m_Households, ref m_Citizens, ref m_Students, ref m_Workers, ref m_SpawnableDatas, ref m_Crimes, ref m_ServiceCoverages, ref m_Lockeds, ref m_ElectricityConsumers, ref m_WaterConsumers, ref m_GarbageProducers, ref m_MailProducers, ref m_Transforms, ref m_Abandoneds, ref m_Parks, ref m_Availabilities, m_TaxRates, m_PollutionMap, m_AirPollutionMap, m_NoiseMap, m_TelecomCoverages, m_CityModifiers[m_City], m_HealthcareParameters.m_HealthcareServicePrefab, m_ParkParameters.m_ParkServicePrefab, m_EducationParameters.m_EducationServicePrefab, m_TelecomParameters.m_TelecomServicePrefab, m_GarbageParameters.m_GarbageServicePrefab, m_PoliceParameters.m_PoliceServicePrefab, m_CitizenHappinessParameterData, m_GarbageParameters);
                                targetSeeker.FindTargets(entity3, 0f - propertyScore + 1000f * (float)bufferAccessor[j].Length / (float)num2 + (float)random.NextInt(500));
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }
}
