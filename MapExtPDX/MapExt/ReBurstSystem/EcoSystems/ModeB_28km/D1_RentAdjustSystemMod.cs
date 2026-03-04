using Colossal.Entities;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.Vehicles;
using Game.Zones;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Game;
using Game.Simulation;
using Transform = Game.Objects.Transform;

namespace MapExtPDX.ModeB
{
	public partial class RentAdjustSystemMod : GameSystemBase
	{
		#region Constants

		public static readonly int kUpdatesPerDay = 16;

		public override int GetUpdateInterval(SystemUpdatePhase phase) =>
			262144 / (kUpdatesPerDay * 16);

		#endregion

		#region Fields

		private EntityQuery m_EconomyParameterQuery;
		private EntityQuery m_DemandParameterQuery;
		private SimulationSystem m_SimulationSystem;
		private EndFrameBarrier m_EndFrameBarrier;
		private ResourceSystem m_ResourceSystem;
		private GroundPollutionSystem m_GroundPollutionSystem;
		private AirPollutionSystem m_AirPollutionSystem;
		private NoisePollutionSystem m_NoisePollutionSystem;
		private TelecomCoverageSystem m_TelecomCoverageSystem;
		private CitySystem m_CitySystem;
		private TaxSystem m_TaxSystem;
		private IconCommandSystem m_IconCommandSystem;
		private EntityQuery m_HealthcareParameterQuery;
		private EntityQuery m_ExtractorParameterQuery;
		private EntityQuery m_ParkParameterQuery;
		private EntityQuery m_EducationParameterQuery;
		private EntityQuery m_TelecomParameterQuery;
		private EntityQuery m_GarbageParameterQuery;
		private EntityQuery m_PoliceParameterQuery;
		private EntityQuery m_CitizenHappinessParameterQuery;
		private EntityQuery m_BuildingParameterQuery;
		private EntityQuery m_PollutionParameterQuery;
		private EntityQuery m_FeeParameterQuery;
		private EntityQuery m_BuildingQuery;
		protected int cycles;

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_ResourceSystem = World.GetOrCreateSystemManaged<ResourceSystem>();
			this.m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
			this.m_AirPollutionSystem = World.GetOrCreateSystemManaged<AirPollutionSystem>();
			this.m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
			this.m_TelecomCoverageSystem = World.GetOrCreateSystemManaged<TelecomCoverageSystem>();
			this.m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
			this.m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
			this.m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
			this.m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
			this.m_DemandParameterQuery = GetEntityQuery(ComponentType.ReadOnly<DemandParameterData>());
			this.m_BuildingParameterQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
			this.m_BuildingQuery = GetEntityQuery(ComponentType.ReadOnly<Building>(),
				ComponentType.ReadOnly<UpdateFrame>(), ComponentType.ReadWrite<Renter>(),
				ComponentType.Exclude<StorageProperty>(), ComponentType.Exclude<Temp>(),
				ComponentType.Exclude<Deleted>());
			this.m_ExtractorParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ExtractorParameterData>());
			this.m_HealthcareParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>());
			this.m_ParkParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ParkParameterData>());
			this.m_EducationParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EducationParameterData>());
			this.m_TelecomParameterQuery = GetEntityQuery(ComponentType.ReadOnly<TelecomParameterData>());
			this.m_GarbageParameterQuery = GetEntityQuery(ComponentType.ReadOnly<GarbageParameterData>());
			this.m_PoliceParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
			this.m_CitizenHappinessParameterQuery =
				GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
			this.m_PollutionParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PollutionParameterData>());
			this.m_FeeParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ServiceFeeParameterData>());
			RequireForUpdate(this.m_EconomyParameterQuery);
			RequireForUpdate(this.m_DemandParameterQuery);
			RequireForUpdate(this.m_HealthcareParameterQuery);
			RequireForUpdate(this.m_ParkParameterQuery);
			RequireForUpdate(this.m_EducationParameterQuery);
			RequireForUpdate(this.m_TelecomParameterQuery);
			RequireForUpdate(this.m_GarbageParameterQuery);
			RequireForUpdate(this.m_PoliceParameterQuery);
			RequireForUpdate(this.m_FeeParameterQuery);
			RequireForUpdate(this.m_BuildingQuery);
		}

		protected override void OnUpdate()
		{
			uint updateFrame = SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex,
				RentAdjustSystemMod.kUpdatesPerDay, 16);
			JobHandle groundPollutionDependencies;
			JobHandle airPollutionDependencies;
			JobHandle noisePollutionDependencies;
			JobHandle rentAdjustJobHandle = JobChunkExtensions.ScheduleParallel(new AdjustRentJob
				{
					m_EntityType = SystemAPI.GetEntityTypeHandle(),
					m_RenterType = SystemAPI.GetBufferTypeHandle<Renter>(isReadOnly: false),
					m_UpdateFrameType = GetSharedComponentTypeHandle<UpdateFrame>(),
					m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: false),
					m_OnMarkets = SystemAPI.GetComponentLookup<PropertyOnMarket>(isReadOnly: false),
					m_Buildings = SystemAPI.GetComponentLookup<Building>(isReadOnly: false),
					m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_BuildingProperties = SystemAPI.GetComponentLookup<BuildingPropertyData>(isReadOnly: true),
					m_BuildingDatas = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: true),
					m_WorkProviders = SystemAPI.GetComponentLookup<WorkProvider>(isReadOnly: true),
					m_CompanyNotifications = SystemAPI.GetComponentLookup<CompanyNotifications>(isReadOnly: true),
					m_Attached = SystemAPI.GetComponentLookup<Attached>(isReadOnly: true),
					m_Lots = SystemAPI.GetComponentLookup<Game.Areas.Lot>(isReadOnly: true),
					m_Geometries = SystemAPI.GetComponentLookup<Geometry>(isReadOnly: true),
					m_LandValues = SystemAPI.GetComponentLookup<LandValue>(isReadOnly: true),
					m_WorkplaceDatas = SystemAPI.GetComponentLookup<WorkplaceData>(isReadOnly: true),
					m_HouseholdCitizenBufs = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
					m_SubAreas = SystemAPI.GetBufferLookup<Game.Areas.SubArea>(isReadOnly: true),
					m_InstalledUpgrades = SystemAPI.GetBufferLookup<InstalledUpgrade>(isReadOnly: true),
					m_Abandoned = SystemAPI.GetComponentLookup<Abandoned>(isReadOnly: true),
					m_Destroyed = SystemAPI.GetComponentLookup<Destroyed>(isReadOnly: true),
					m_Transforms = SystemAPI.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true),
					m_CityModifiers = SystemAPI.GetBufferLookup<CityModifier>(isReadOnly: true),
					m_HealthProblems = SystemAPI.GetComponentLookup<HealthProblem>(isReadOnly: true),
					m_SpawnableBuildingData = SystemAPI.GetComponentLookup<SpawnableBuildingData>(isReadOnly: true),
					m_ZoneData = SystemAPI.GetComponentLookup<ZoneData>(isReadOnly: true),
					m_BuildingNotifications = SystemAPI.GetComponentLookup<BuildingNotifications>(isReadOnly: false),
					m_ExtractorProperties = SystemAPI.GetComponentLookup<ExtractorProperty>(isReadOnly: true),
					m_ResourcesBuf = SystemAPI.GetBufferLookup<Game.Economy.Resources>(isReadOnly: true),
					m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(isReadOnly: true),
					m_LayoutElements = SystemAPI.GetBufferLookup<LayoutElement>(isReadOnly: true),
					m_Workers = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
					m_Citizens = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: true),
					m_ProcessDatas = SystemAPI.GetComponentLookup<IndustrialProcessData>(isReadOnly: true),
					m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs(),
					m_ResourceDatas = SystemAPI.GetComponentLookup<ResourceData>(isReadOnly: true),
					m_TaxRates = this.m_TaxSystem.GetTaxRates(),
					m_PollutionMap =
						this.m_GroundPollutionSystem.GetMap(readOnly: true, out groundPollutionDependencies),
					m_AirPollutionMap = this.m_AirPollutionSystem.GetMap(readOnly: true, out airPollutionDependencies),
					m_NoiseMap = this.m_NoisePollutionSystem.GetMap(readOnly: true, out noisePollutionDependencies),
					m_CitizenHappinessParameterData =
						this.m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
					m_BuildingConfigurationData =
						this.m_BuildingParameterQuery.GetSingleton<BuildingConfigurationData>(),
					m_PollutionParameters = this.m_PollutionParameterQuery.GetSingleton<PollutionParameterData>(),
					m_FeeParameters = this.m_FeeParameterQuery.GetSingleton<ServiceFeeParameterData>(),
					m_DeliveryTrucks = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(isReadOnly: true),
					m_ZonePropertiesDatas = SystemAPI.GetComponentLookup<ZonePropertiesData>(isReadOnly: true),
					m_ServiceAvailables = SystemAPI.GetComponentLookup<ServiceAvailable>(isReadOnly: true),
					m_UnderConstructions = SystemAPI.GetComponentLookup<UnderConstruction>(isReadOnly: true),
					m_EconomyParameterData = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
					m_City = this.m_CitySystem.City,
					m_UpdateFrameIndex = updateFrame,
					m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
					m_IconCommandBuffer = this.m_IconCommandSystem.CreateCommandBuffer()
				}, this.m_BuildingQuery,
				JobUtils.CombineDependencies(groundPollutionDependencies, airPollutionDependencies,
					noisePollutionDependencies, base.Dependency));
			this.m_EndFrameBarrier.AddJobHandleForProducer(rentAdjustJobHandle);
			this.m_ResourceSystem.AddPrefabsReader(rentAdjustJobHandle);
			this.m_GroundPollutionSystem.AddReader(rentAdjustJobHandle);
			this.m_AirPollutionSystem.AddReader(rentAdjustJobHandle);
			this.m_NoisePollutionSystem.AddReader(rentAdjustJobHandle);
			this.m_TelecomCoverageSystem.AddReader(rentAdjustJobHandle);
			this.m_TaxSystem.AddReader(rentAdjustJobHandle);
			this.m_IconCommandSystem.AddCommandBufferWriter(rentAdjustJobHandle);
			base.Dependency = rentAdjustJobHandle;
		}


		#endregion

		#region Jobs

		[BurstCompile]
    private struct AdjustRentJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle m_EntityType;
        public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly] public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;
        [NativeDisableParallelForRestriction] public ComponentLookup<PropertyRenter> m_PropertyRenters;
        [NativeDisableParallelForRestriction] public ComponentLookup<Building> m_Buildings;
        [ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingProperties;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas;
        [ReadOnly] public ComponentLookup<WorkProvider> m_WorkProviders;
        [ReadOnly] public ComponentLookup<WorkplaceData> m_WorkplaceDatas;
        [ReadOnly] public ComponentLookup<CompanyNotifications> m_CompanyNotifications;
        [ReadOnly] public ComponentLookup<Attached> m_Attached;
        [ReadOnly] public ComponentLookup<Game.Areas.Lot> m_Lots;
        [ReadOnly] public ComponentLookup<Geometry> m_Geometries;
        [ReadOnly] public ComponentLookup<LandValue> m_LandValues;
        [NativeDisableParallelForRestriction] public ComponentLookup<PropertyOnMarket> m_OnMarkets;
        [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizenBufs;
        [ReadOnly] public BufferLookup<Game.Areas.SubArea> m_SubAreas;
        [ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;
        [ReadOnly] public ComponentLookup<Abandoned> m_Abandoned;
        [ReadOnly] public ComponentLookup<Destroyed> m_Destroyed;
        [ReadOnly] public ComponentLookup<Transform> m_Transforms;
        [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
        [ReadOnly] public ComponentLookup<HealthProblem> m_HealthProblems;
        [ReadOnly] public ComponentLookup<Worker> m_Workers;
        [ReadOnly] public ComponentLookup<Citizen> m_Citizens;
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingData;
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneData;
        [ReadOnly] public BufferLookup<Game.Economy.Resources> m_ResourcesBuf;
        [ReadOnly] public ComponentLookup<ExtractorProperty> m_ExtractorProperties;
        [ReadOnly] public ComponentLookup<IndustrialProcessData> m_ProcessDatas;
        [ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles;
        [ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;
        [ReadOnly] public BufferLookup<LayoutElement> m_LayoutElements;
        [ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
        [ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;
        [ReadOnly] public ComponentLookup<ServiceAvailable> m_ServiceAvailables;
        [ReadOnly] public ComponentLookup<UnderConstruction> m_UnderConstructions;
        [NativeDisableParallelForRestriction] public ComponentLookup<BuildingNotifications> m_BuildingNotifications;
        [ReadOnly] public NativeArray<int> m_TaxRates;
        [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap;
        [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
        [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;
        public CitizenHappinessParameterData m_CitizenHappinessParameterData;
        public BuildingConfigurationData m_BuildingConfigurationData;
        public PollutionParameterData m_PollutionParameters;
        public ServiceFeeParameterData m_FeeParameters;
        public IconCommandBuffer m_IconCommandBuffer;
        public uint m_UpdateFrameIndex;
        [ReadOnly] public Entity m_City;
        public EconomyParameterData m_EconomyParameterData;
        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        /// <summary>
        /// 判断是否应该显示“高租金”警告图标
        /// 避免在有更严重问题（如无客户、无员工、全员死亡）时显示租金图标，减少视觉干扰
        /// </summary>
        private bool CanDisplayHighRentWarnIcon(DynamicBuffer<Renter> renters)
        {
            // 默认显示，除非发现更高优先级的严重问
            bool canDisplay = true;

            for (int i = 0; i < renters.Length; i++)
            {
                Entity renter = renters[i].m_Renter;

                // 1. 检查公司是否有更严重的问题（如：没有客户、没有原材料
                if (this.m_CompanyNotifications.HasComponent(renter))
                {
                    CompanyNotifications companyNotifications = this.m_CompanyNotifications[renter];
                    // 如果存在"无客无输的问题，优先显示那些图标，不显示高租金图
                    if (companyNotifications.m_NoCustomersEntity != Entity.Null ||
                        companyNotifications.m_NoInputEntity != Entity.Null)
                    {
                        canDisplay = false;
                        break;
                    }
                }

                // 2. 检查是否有员工教育水平不匹配的问题
                if (this.m_WorkProviders.HasComponent(renter))
                {
                    WorkProvider workProvider = this.m_WorkProviders[renter];
                    if (workProvider.m_EducatedNotificationEntity != Entity.Null ||
                        workProvider.m_UneducatedNotificationEntity != Entity.Null)
                    {
                        canDisplay = false; // A more critical issue exists.
                        break;
                    }
                }

                // 3. 家庭检查：确保至少有一个家庭成员是活着
                if (!this.m_HouseholdCitizenBufs.HasBuffer(renter))
                {
                    continue;
                }

                DynamicBuffer<HouseholdCitizen> householdCitizens = this.m_HouseholdCitizenBufs[renter];
                canDisplay = false;
                for (int j = 0; j < householdCitizens.Length; j++)
                {
                    if (!CitizenUtils.IsDead(householdCitizens[j].m_Citizen, ref this.m_HealthProblems))
                    {
                        canDisplay = true;
                        break;
                    }
                }
            }

            return canDisplay;
        }

        // =========================================================
        // 核心执行逻辑
        // =========================================================
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            // 性能优化：只在特定的模拟帧处理该Chunk，分散负
            if (chunk.GetSharedComponent(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
            {
                return;
            }

            NativeArray<Entity> buildingEntities = chunk.GetNativeArray(this.m_EntityType);
            BufferAccessor<Renter> renterBuffers = chunk.GetBufferAccessor(ref this.m_RenterType);
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];

            // 遍历Chunk中的所有建筑实
            for (int i = 0; i < buildingEntities.Length; i++)
            {
                Entity buildingEntity = buildingEntities[i];
                Entity buildingPrefab = this.m_Prefabs[buildingEntity].m_Prefab;

                // 跳过非租赁类建筑
                if (!this.m_BuildingProperties.HasComponent(buildingPrefab))
                {
                    continue;
                }

                // ====== 1. 获取建筑基础数据 ======
                BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[buildingPrefab];
                Building building = this.m_Buildings[buildingEntity];
                DynamicBuffer<Renter> renters = renterBuffers[i];
                BuildingData buildingData = this.m_BuildingDatas[buildingPrefab];

                // 计算地块大小
                int lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;

                // 获取基础地价 (RoadEdge)
                float landValueBase = 0f;
                if (this.m_LandValues.HasComponent(building.m_RoadEdge))
                {
                    landValueBase = this.m_LandValues[building.m_RoadEdge].m_LandValue;
                }

                // ====== 2. 确定区域类型 (住宅/商业/工业/办公) ======
                Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
                int buildingLevel = PropertyUtils.GetBuildingLevel(buildingPrefab, this.m_SpawnableBuildingData);
                bool ignoreLandValue = false;
                bool isOffice = false;

                if (this.m_SpawnableBuildingData.HasComponent(buildingPrefab))
                {
                    SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingData[buildingPrefab];
                    areaType = this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_AreaType;

                    // 检查区域属性（例如某些低密度区可能忽略地价影响
                    if (this.m_ZonePropertiesDatas.TryGetComponent(spawnableBuildingData.m_ZonePrefab,
                            out var componentData))
                    {
                        ignoreLandValue = componentData.m_IgnoreLandValue;
                    }

                    isOffice = (this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_ZoneFlags & ZoneFlags.Office) !=
                               0;
                }

                // --- 3. 处理污染通知 (仅住 ---
                this.ProcessPollutionNotification(areaType, buildingEntity, cityModifiers);

                // --- 4. 计算每户租金 ---
                int buildingGarbageFeePerDay = this.m_FeeParameters.GetBuildingGarbageFeePerDay(areaType, isOffice);
                int rentPerRenter = PropertyUtils.GetRentPricePerRenter(buildingPropertyData, buildingLevel, lotSize,
                    landValueBase, areaType, ref this.m_EconomyParameterData, ignoreLandValue);

                // 更新市场挂牌价格
                if (this.m_OnMarkets.HasComponent(buildingEntity))
                {
                    PropertyOnMarket onMarketData = this.m_OnMarkets[buildingEntity];
                    onMarketData.m_AskingRent = rentPerRenter;
                    this.m_OnMarkets[buildingEntity] = onMarketData;
                }

                int propertyCount = buildingPropertyData.CountProperties();
                bool rentersWereRemoved = false; // 标记是否有租户被移除
                // x = 付不起的人数, y = 检查的总人
                int2 affordabilityStats = default(int2);
                bool isExtractorBuilding = this.m_ExtractorProperties.HasComponent(buildingEntity); // 是否为采集设施（如油井）

                // --- 5. 遍历该建筑内的所有租(倒序遍历以便移除) ---
                for (int renterIndex = renters.Length - 1; renterIndex >= 0; renterIndex--)
                {
                    Entity renter = renters[renterIndex].m_Renter;
                    if (this.m_PropertyRenters.HasComponent(renter))
                    {
                        PropertyRenter propertyRenterData = this.m_PropertyRenters[renter];

                        if (!this.m_ResourcesBuf.HasBuffer(renter))
                        {
                            // [Mod修改逻辑] 调试代码，绝不应出现在生产版本！应该包装#if UNITY_EDITOR 预处理器指令中或完全删除
                            // UnityEngine.Debug.Log($"no resources:{renter.Index}");
                            // 新增修正：当Resources buffer缺失时，移除该租户
                            this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex, renter);
                            continue;
                        }

                        // --- 5a. 计算租户支付能力 (Income / Budget) ---
                        int renterUpkeepCapacity = 0;
                        bool isHousehold = this.m_HouseholdCitizenBufs.HasBuffer(renter);

                        if (isHousehold) // 家庭实体
                        {
                            // [Mod修改逻辑] 支付能力 = 收入的30% + 存款的10%
                            // 目的：减少因存款过高而触发的不必要升级找房
                            int householdIncome = EconomyUtils.GetHouseholdIncome(this.m_HouseholdCitizenBufs[renter],
                                ref this.m_Workers, ref this.m_Citizens, ref this.m_HealthProblems,
                                ref this.m_EconomyParameterData, this.m_TaxRates);
                            int householdSavings = math.max(0,
                                EconomyUtils.GetResources(Game.Economy.Resource.Money, this.m_ResourcesBuf[renter]));
                            renterUpkeepCapacity = (int)(householdIncome * 0.3f + householdSavings * 0.1f);
                        }
                        else // 公司实体
                        {
                            Entity renterPrefab = this.m_Prefabs[renter].m_Prefab;
                            // 检查公司数据完整
                            if (!this.m_ProcessDatas.HasComponent(renterPrefab) ||
                                !this.m_WorkProviders.HasComponent(renter) ||
                                !this.m_WorkplaceDatas.HasComponent(renterPrefab))
                            {
                                continue; // Skip if company data is missing.
                            }

                            IndustrialProcessData industrialProcessData = this.m_ProcessDatas[renterPrefab];
                            bool isIndustrial = !this.m_ServiceAvailables.HasComponent(renter);

                            // 估算公司最大日利润
                            int companyMaxProfitPerDay = EconomyUtils.GetCompanyMaxProfitPerDay(
                                this.m_WorkProviders[renter], areaType == Game.Zones.AreaType.Industrial, buildingLevel,
                                this.m_ProcessDatas[renterPrefab], this.m_ResourcePrefabs,
                                this.m_WorkplaceDatas[renterPrefab], ref this.m_ResourceDatas,
                                ref this.m_EconomyParameterData);

                            // 公司预算预期利润 公司总资中的较大特定逻辑
                            renterUpkeepCapacity = ((companyMaxProfitPerDay >= renterUpkeepCapacity)
                                ? companyMaxProfitPerDay
                                : ((!this.m_OwnedVehicles.HasBuffer(renter))
                                    ? EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData,
                                        this.m_ResourcesBuf[renter], this.m_ResourcePrefabs, ref this.m_ResourceDatas)
                                    : EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData,
                                        this.m_ResourcesBuf[renter], this.m_OwnedVehicles[renter],
                                        ref this.m_LayoutElements, ref this.m_DeliveryTrucks, this.m_ResourcePrefabs,
                                        ref this.m_ResourceDatas)));
                        }

                        // --- 5b. 更新租金并检查是否需要搬---
                        propertyRenterData.m_Rent = rentPerRenter;
                        this.m_PropertyRenters[renter] = propertyRenterData;

                        int totalCostPerDay = rentPerRenter + buildingGarbageFeePerDay;

                        // [Mod修改逻辑] 分批检查 + 降低升级频率
                        if (isHousehold)
                        {
                            // 利用 Entity.Index 和 m_UpdateFrameIndex 确定性分批
                            // 付不起(高租金)：每2个更新周期检查一次
                            // 改善住房(低租金)：每4个更新周期检查一次，且仅30%概率触发
                            int kUpdatesPerDay = RentAdjustSystem.kUpdatesPerDay; // 16
                            int checkPeriodHigh = 2 * kUpdatesPerDay; // 32帧
                            int checkPeriodLow = 4 * kUpdatesPerDay; // 64帧

                            bool isMyFrameHigh = (m_UpdateFrameIndex % checkPeriodHigh ==
                                                  (uint)renter.Index % (uint)checkPeriodHigh);
                            bool isMyFrameLow = (m_UpdateFrameIndex % checkPeriodLow ==
                                                 (uint)renter.Index % (uint)checkPeriodLow);

                            if (totalCostPerDay > renterUpkeepCapacity && isMyFrameHigh)
                            {
                                this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
                                    renter, value: true);
                            }
                            else if (totalCostPerDay < renterUpkeepCapacity / 2 && isMyFrameLow)
                            {
                                // 30%概率触发升级
                                if (((uint)renter.Index + (uint)(m_UpdateFrameIndex / checkPeriodLow)) % 10 < 3)
                                {
                                    this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
                                        renter, value: true);
                                }
                            }
                        }
                        else
                        {
                            // 公司：与原版一致，直接触发
                            if (totalCostPerDay > renterUpkeepCapacity)
                            {
                                this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, renter,
                                    value: true);
                            }
                        }

                        // --- 5c. 统计支付能力数据 ---
                        affordabilityStats.y++;
                        if (rentPerRenter > renterUpkeepCapacity)
                        {
                            affordabilityStats.x++; // 付不起的人数 +1
                        }
                    }
                    else
                    {
                        // 数据异常：Buffer里有实体但没有PropertyRenter组件，移除之
                        renters.RemoveAt(renterIndex);
                        rentersWereRemoved = true;
                    }
                }

                // ====== 6. 处理“高租金”图标逻辑 ======
                // 计算付不起租金的比例
                float highRentRatio = affordabilityStats.x / math.max(1f, affordabilityStats.y);

                // 如果比例低于70% 有更重要的问题，移除图标
                if (highRentRatio <= 0.7f || !this.CanDisplayHighRentWarnIcon(renters))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity,
                        this.m_BuildingConfigurationData.m_HighRentNotification);
                    building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[buildingEntity] = building;
                }

                // [Mod修改]
                // 原逻辑为仅当建筑物空置时才显示高租金警告，一旦满员就不会显示，这与现实情况不符。可能有意设计为高租金阻止新租户入住的提醒，但这种信息并不完整。现改为当建筑物无论是否有空置房源，只要大部分租户负担不起租金时就显示高租金警告
                // mod：将>改为>=;即使满员也会警告，提示玩家经济压
                else if (renters.Length > 0 && !isExtractorBuilding && propertyCount >= renters.Length &&
                         (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) == 0)
                {
                    this.m_IconCommandBuffer.Add(buildingEntity,
                        this.m_BuildingConfigurationData.m_HighRentNotification, IconPriority.Problem);
                    building.m_Flags |= Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[buildingEntity] = building;
                }

                // ====== 7. 清理与收======

                // 超员处理：如果租户数量超过建筑容量，驱逐最后一
                if (renters.Length > 0 && renters.Length > propertyCount)
                {
                    Entity lastRenter = renters[^1].m_Renter;
                    if (this.m_PropertyRenters.HasComponent(lastRenter))
                    {
                        this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex,
                            renters[^1].m_Renter);
                        renters.RemoveAt(renters.Length - 1);
                    }
                    // 修正5调试代码，绝不应出现在生产版本！
                    // UnityEngine.Debug.LogWarning($"Removed extra renter from building:{buildingEntity.Index}");
                }

                // 如果建筑变空了，确保移除警告
                if (renters.Length == 0 && (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) !=
                    Game.Buildings.BuildingFlags.None)
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity,
                        this.m_BuildingConfigurationData.m_HighRentNotification);
                    building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[buildingEntity] = building;
                }

                // 如果有租户搬被移除，且建筑未废弃/损毁，且还有空位，将其重新挂牌上
                if (this.m_Prefabs.HasComponent(buildingEntity) && !this.m_Abandoned.HasComponent(buildingEntity) &&
                    !this.m_Destroyed.HasComponent(buildingEntity) && rentersWereRemoved &&
                    propertyCount > renters.Length)
                {
                    this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, buildingEntity, new PropertyOnMarket
                    {
                        m_AskingRent = rentPerRenter
                    });
                }
            }
        }

        private void ProcessPollutionNotification(Game.Zones.AreaType areaType, Entity buildingEntity,
            DynamicBuffer<CityModifier> cityModifiers)
        {
            if (areaType == Game.Zones.AreaType.Residential)
            {
                // mod v2.* 修改：GetGroundPollutionBonuses 等静态方法重定向
                int2 groundPollutionBonuses = XCellMapSystemRe.GetGroundPollutionBonuses(buildingEntity,
                    ref this.m_Transforms, this.m_PollutionMap, cityModifiers, in this.m_CitizenHappinessParameterData);
                int2 noiseBonuses = XCellMapSystemRe.GetNoiseBonuses(buildingEntity, ref this.m_Transforms,
                    this.m_NoiseMap, in this.m_CitizenHappinessParameterData);
                int2 airPollutionBonuses = XCellMapSystemRe.GetAirPollutionBonuses(buildingEntity,
                    ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers,
                    in this.m_CitizenHappinessParameterData);

                // v1.4.2新增：如果建筑正在建设中，则不显示污染相关通知
                bool isUnderConstruction = this.m_UnderConstructions.HasComponent(buildingEntity);

                bool isGroundPollutionBad = !isUnderConstruction &&
                                            groundPollutionBonuses.x + groundPollutionBonuses.y <
                                            2 * this.m_PollutionParameters.m_GroundPollutionNotificationLimit;
                bool isAirPollutionBad = !isUnderConstruction && airPollutionBonuses.x + airPollutionBonuses.y <
                    2 * this.m_PollutionParameters.m_AirPollutionNotificationLimit;
                bool isNoisePollutionBad = !isUnderConstruction && noiseBonuses.x + noiseBonuses.y <
                    2 * this.m_PollutionParameters.m_NoisePollutionNotificationLimit;

                BuildingNotifications notifications = this.m_BuildingNotifications[buildingEntity];

                // 处理地面污染图标
                if (isGroundPollutionBad && !notifications.HasNotification(BuildingNotification.GroundPollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity,
                        this.m_PollutionParameters.m_GroundPollutionNotification, IconPriority.Problem);
                    notifications.m_Notifications |= BuildingNotification.GroundPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
                else if (!isGroundPollutionBad && notifications.HasNotification(BuildingNotification.GroundPollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity,
                        this.m_PollutionParameters.m_GroundPollutionNotification);
                    notifications.m_Notifications &= ~BuildingNotification.GroundPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }

                // 处理空气污染图标
                if (isAirPollutionBad && !notifications.HasNotification(BuildingNotification.AirPollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_AirPollutionNotification,
                        IconPriority.Problem);
                    notifications.m_Notifications |= BuildingNotification.AirPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
                else if (!isAirPollutionBad && notifications.HasNotification(BuildingNotification.AirPollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity,
                        this.m_PollutionParameters.m_AirPollutionNotification);
                    notifications.m_Notifications &= ~BuildingNotification.AirPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }

                // 处理噪音污染图标
                if (isNoisePollutionBad && !notifications.HasNotification(BuildingNotification.NoisePollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity,
                        this.m_PollutionParameters.m_NoisePollutionNotification, IconPriority.Problem);
                    notifications.m_Notifications |= BuildingNotification.NoisePollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
                else if (!isNoisePollutionBad && notifications.HasNotification(BuildingNotification.NoisePollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity,
                        this.m_PollutionParameters.m_NoisePollutionNotification);
                    notifications.m_Notifications &= ~BuildingNotification.NoisePollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
            }
        }
    }
	}


	#endregion
}
