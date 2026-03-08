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
	/*
	 * 🟢 [Mod Modifications Summary]
	 * 1. 🔧 破产驱逐线修复: 恢复原版的 renterUpkeepCapacity = Income + Savings 作为绝对破产生死线，避免大规模死循环驱逐。
	 * 2. 🎲 住房改善逻辑平滑化: 家庭租金花销低于收入 15% 时，仅有 30% 的概率触发升级寻房，减少全城无意义搬家引发的CPU负担。
	 * 3. ⚠️ 高租金警告提示优化: 即使建筑满员，当绝大多数租户面临经济压力但未破产时，也会显示高租金警告，给玩家提供真实的经济反馈。
	 * 4. 🗑️ 异常清理与调度还原: 清理导致跳帧的错误降频逻辑，恢复原生 ECS UpdateFrame 调度；移除无效实体残留。
	 */

	/// <summary>
	/// 🔧 [MOD] 租金定期调整系统 (RentAdjustSystem) 的重写版本
	/// 负责处理全图建筑的租金计算、租户破产驱逐以及公司经营预警更新。
	/// </summary>
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
			/// 判断是否应当显示高租金警告图标。
			/// 若建筑内的公司或居民存在更严重的致命问题（如无货可卖、全家灭门），则优先显示严重问题，不报高租金警告。
			/// </summary>
			private bool CanDisplayHighRentWarnIcon(DynamicBuffer<Renter> renters)
			{
				bool canDisplay = true;

				for (int i = 0; i < renters.Length; i++)
				{
					Entity renter = renters[i].m_Renter;

					// --- 检查公司状态 ---
					if (this.m_CompanyNotifications.HasComponent(renter))
					{
						CompanyNotifications companyNotifications = this.m_CompanyNotifications[renter];
						if (companyNotifications.m_NoCustomersEntity != Entity.Null ||
						    companyNotifications.m_NoInputEntity != Entity.Null)
						{
							canDisplay = false;
							break;
						}
					}

					// --- 检查员工状态 ---
					if (this.m_WorkProviders.HasComponent(renter))
					{
						WorkProvider workProvider = this.m_WorkProviders[renter];
						if (workProvider.m_EducatedNotificationEntity != Entity.Null ||
						    workProvider.m_UneducatedNotificationEntity != Entity.Null)
						{
							canDisplay = false;
							break;
						}
					}

					if (!this.m_HouseholdCitizenBufs.HasBuffer(renter))
					{
						continue;
					}

					// --- 检查家庭生命体征 ---
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

			// === 核心执行逻辑 ===
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				// --- 性能优化：原生更新帧限制 ---
				if (chunk.GetSharedComponent(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}

				NativeArray<Entity> buildingEntities = chunk.GetNativeArray(this.m_EntityType);
				BufferAccessor<Renter> renterBuffers = chunk.GetBufferAccessor(ref this.m_RenterType);
				DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];

				// --- 遍历 Chunk 中的实体 ---
				for (int i = 0; i < buildingEntities.Length; i++)
				{
					Entity buildingEntity = buildingEntities[i];
					Entity buildingPrefab = this.m_Prefabs[buildingEntity].m_Prefab;

					if (!this.m_BuildingProperties.HasComponent(buildingPrefab))
					{
						continue;
					}

					// ====== 1. 获取建筑基础数据 ======
					BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[buildingPrefab];
					Building building = this.m_Buildings[buildingEntity];
					DynamicBuffer<Renter> renters = renterBuffers[i];
					BuildingData buildingData = this.m_BuildingDatas[buildingPrefab];

					int lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
					float landValueBase = 0f;

					if (this.m_LandValues.HasComponent(building.m_RoadEdge))
					{
						landValueBase = this.m_LandValues[building.m_RoadEdge].m_LandValue;
					}

					// ====== 2. 确定区域类型 ======
					Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
					int buildingLevel = PropertyUtils.GetBuildingLevel(buildingPrefab, this.m_SpawnableBuildingData);
					bool ignoreLandValue = false;
					bool isOffice = false;

					if (this.m_SpawnableBuildingData.HasComponent(buildingPrefab))
					{
						SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingData[buildingPrefab];
						areaType = this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_AreaType;

						if (this.m_ZonePropertiesDatas.TryGetComponent(spawnableBuildingData.m_ZonePrefab,
							    out var componentData))
						{
							ignoreLandValue = componentData.m_IgnoreLandValue;
						}

						isOffice =
							(this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_ZoneFlags & ZoneFlags.Office) !=
							0;
					}

					// ====== 3. 处理污染通知 ======
					this.ProcessPollutionNotification(areaType, buildingEntity, cityModifiers);

					// ====== 4. 计算地租与市场挂牌价 ======
					int buildingGarbageFeePerDay = this.m_FeeParameters.GetBuildingGarbageFeePerDay(areaType, isOffice);
					int rentPerRenter = PropertyUtils.GetRentPricePerRenter(buildingPropertyData, buildingLevel,
						lotSize,
						landValueBase, areaType, ref this.m_EconomyParameterData, ignoreLandValue);

					if (this.m_OnMarkets.HasComponent(buildingEntity))
					{
						PropertyOnMarket onMarketData = this.m_OnMarkets[buildingEntity];
						onMarketData.m_AskingRent = rentPerRenter;
						this.m_OnMarkets[buildingEntity] = onMarketData;
					}

					int propertyCount = buildingPropertyData.CountProperties();
					bool rentersWereRemoved = false;
					int2 affordabilityStats = default(int2);
					bool isExtractorBuilding = this.m_ExtractorProperties.HasComponent(buildingEntity);

					// ====== 5. 结算建筑内的所有租户 ======
					for (int renterIndex = renters.Length - 1; renterIndex >= 0; renterIndex--)
					{
						Entity renter = renters[renterIndex].m_Renter;
						if (this.m_PropertyRenters.HasComponent(renter))
						{
							PropertyRenter propertyRenterData = this.m_PropertyRenters[renter];

							// 🗑️ [MOD功能] 异常清理防御：资源缓冲缺失的关联实体予以直接拆除，防止游戏报错奔溃。
							if (!this.m_ResourcesBuf.HasBuffer(renter))
							{
								this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex, renter);
								continue;
							}

							// --- 5a. 计算租户支付能力 ---
							int renterUpkeepCapacity = 0;
							int householdIncome = 0;
							bool isHousehold = this.m_HouseholdCitizenBufs.HasBuffer(renter);

							if (isHousehold)
							{
								householdIncome = EconomyUtils.GetHouseholdIncome(
									this.m_HouseholdCitizenBufs[renter],
									ref this.m_Workers, ref this.m_Citizens, ref this.m_HealthProblems,
									ref this.m_EconomyParameterData, this.m_TaxRates);
								int householdSavings = math.max(0,
									EconomyUtils.GetResources(Game.Economy.Resource.Money,
										this.m_ResourcesBuf[renter]));
								// 🔧 [MOD修复] 恢复原版 "Income + Savings" 作为绝对破产生死线，有效阻止由于小人不合理存款引发的死循环驱离。
								renterUpkeepCapacity = householdIncome + householdSavings;
							}
							else
							{
								Entity renterPrefab = this.m_Prefabs[renter].m_Prefab;
								if (!this.m_ProcessDatas.HasComponent(renterPrefab) ||
								    !this.m_WorkProviders.HasComponent(renter) ||
								    !this.m_WorkplaceDatas.HasComponent(renterPrefab))
								{
									continue;
								}

								IndustrialProcessData industrialProcessData = this.m_ProcessDatas[renterPrefab];
								bool isIndustrial = !this.m_ServiceAvailables.HasComponent(renter);

								int companyMaxProfitPerDay = EconomyUtils.GetCompanyMaxProfitPerDay(
									this.m_WorkProviders[renter], areaType == Game.Zones.AreaType.Industrial,
									buildingLevel,
									this.m_ProcessDatas[renterPrefab], this.m_ResourcePrefabs,
									this.m_WorkplaceDatas[renterPrefab], ref this.m_ResourceDatas,
									ref this.m_EconomyParameterData);

								renterUpkeepCapacity = ((companyMaxProfitPerDay >= renterUpkeepCapacity)
									? companyMaxProfitPerDay
									: ((!this.m_OwnedVehicles.HasBuffer(renter))
										? EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData,
											this.m_ResourcesBuf[renter], this.m_ResourcePrefabs,
											ref this.m_ResourceDatas)
										: EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData,
											this.m_ResourcesBuf[renter], this.m_OwnedVehicles[renter],
											ref this.m_LayoutElements, ref this.m_DeliveryTrucks,
											this.m_ResourcePrefabs,
											ref this.m_ResourceDatas)));
							}

							propertyRenterData.m_Rent = rentPerRenter;
							this.m_PropertyRenters[renter] = propertyRenterData;

							int totalCostPerDay = rentPerRenter + buildingGarbageFeePerDay;

							// --- 5b. 结算驱逐/升迁状态 ---
							if (isHousehold)
							{
								// 🔧 [MOD修复] 彻底分离强迫驱离与自主改善房屋的逻辑：
								if (totalCostPerDay > renterUpkeepCapacity)
								{
									// 此家庭已被榨干最后一丝潜能，挂起待赶指令 (PropertySeeker)
									this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
										renter, value: true);
								}
								// 🎲 [MOD平滑设定] 改善住房：当生活成本 < 家庭收入 15% 时表明手头极其充裕。
								// 以 renter Index 平抑曲线并加入 30% 几率寻求改善，避免全城搬迁拥堵寻路网。
								else if (totalCostPerDay < householdIncome * 0.15f)
								{
									if (((uint)renter.Index + m_UpdateFrameIndex) % 10 < 3)
									{
										this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
											renter, value: true);
									}
								}
							}
							else
							{
								// 公司单位统一以自身估值和流水维持破产线运转
								if (totalCostPerDay > renterUpkeepCapacity)
								{
									this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
										renter, value: true);
								}
							}

							// --- 5c. 统计支付能力数据 ---
							affordabilityStats.y++;
							if (rentPerRenter > renterUpkeepCapacity)
							{
								affordabilityStats.x++;
							}
						}
						else
						{
							renters.RemoveAt(renterIndex);
							rentersWereRemoved = true;
						}
					}

					// ====== 6. 界面警告与通知 ======
					float highRentRatio = affordabilityStats.x / math.max(1f, affordabilityStats.y);

					// 若破产或绝收率 <= 70% 或这栋楼其实有更惨痛的问题（断货/无人工等），则撤底清除租金警告以防碍眼
					if (highRentRatio <= 0.7f || !this.CanDisplayHighRentWarnIcon(renters))
					{
						this.m_IconCommandBuffer.Remove(buildingEntity,
							this.m_BuildingConfigurationData.m_HighRentNotification);
						building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
						this.m_Buildings[buildingEntity] = building;
					}
					// ⚠️ [MOD功能] 高级提示策略：即使目前房间全满 (将 > 置换为 >=), 只要超过 70% 的居民哀鸿遍野叫苦连天，照样显示高级报警！
					// 给玩家提供全图最直观的租差反馈体系
					else if (renters.Length > 0 && !isExtractorBuilding && propertyCount >= renters.Length &&
					         (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) == 0)
					{
						this.m_IconCommandBuffer.Add(buildingEntity,
							this.m_BuildingConfigurationData.m_HighRentNotification, IconPriority.Problem);
						building.m_Flags |= Game.Buildings.BuildingFlags.HighRentWarning;
						this.m_Buildings[buildingEntity] = building;
					}

					// ====== 7. 结算与收尾清理工作 ======

					// 清理超员租客：比如建筑升级、或被拆毁等导致人口容积暴减并挤压现有住户，进行最后一单剪辑驱赶。
					if (renters.Length > 0 && renters.Length > propertyCount)
					{
						Entity lastRenter = renters[^1].m_Renter;
						if (this.m_PropertyRenters.HasComponent(lastRenter))
						{
							this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex,
								renters[^1].m_Renter);
							renters.RemoveAt(renters.Length - 1);
						}
					}

					// 建筑空置：若全部人员已跑尽，务必连带着移除遗留警告指令。
					if (renters.Length == 0 && (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) !=
					    Game.Buildings.BuildingFlags.None)
					{
						this.m_IconCommandBuffer.Remove(buildingEntity,
							this.m_BuildingConfigurationData.m_HighRentNotification);
						building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
						this.m_Buildings[buildingEntity] = building;
					}

					// 更新挂牌与重投机制：若有租客迁出后空开房源并且没遭废弃，则重新上架待租
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

			// === 综合污染警示系统 ===
			private void ProcessPollutionNotification(Game.Zones.AreaType areaType, Entity buildingEntity,
				DynamicBuffer<CityModifier> cityModifiers)
			{
				if (areaType == Game.Zones.AreaType.Residential)
				{
					// 🔧 [MOD] 静态类桥接，无封缝引入最新 XCellMapSystemRe 内方法处理各修正算法逻辑
					int2 groundPollutionBonuses = XCellMapSystemRe.GetGroundPollutionBonuses(buildingEntity,
						ref this.m_Transforms, this.m_PollutionMap, cityModifiers,
						in this.m_CitizenHappinessParameterData);
					int2 noiseBonuses = XCellMapSystemRe.GetNoiseBonuses(buildingEntity, ref this.m_Transforms,
						this.m_NoiseMap, in this.m_CitizenHappinessParameterData);
					int2 airPollutionBonuses = XCellMapSystemRe.GetAirPollutionBonuses(buildingEntity,
						ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers,
						in this.m_CitizenHappinessParameterData);

					bool isUnderConstruction = this.m_UnderConstructions.HasComponent(buildingEntity);

					bool isGroundPollutionBad = !isUnderConstruction &&
					                            groundPollutionBonuses.x + groundPollutionBonuses.y <
					                            2 * this.m_PollutionParameters.m_GroundPollutionNotificationLimit;
					bool isAirPollutionBad = !isUnderConstruction && airPollutionBonuses.x + airPollutionBonuses.y <
						2 * this.m_PollutionParameters.m_AirPollutionNotificationLimit;
					bool isNoisePollutionBad = !isUnderConstruction && noiseBonuses.x + noiseBonuses.y <
						2 * this.m_PollutionParameters.m_NoisePollutionNotificationLimit;

					BuildingNotifications notifications = this.m_BuildingNotifications[buildingEntity];

					// --- 地面污染 ---
					if (isGroundPollutionBad && !notifications.HasNotification(BuildingNotification.GroundPollution))
					{
						this.m_IconCommandBuffer.Add(buildingEntity,
							this.m_PollutionParameters.m_GroundPollutionNotification, IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.GroundPollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!isGroundPollutionBad &&
					         notifications.HasNotification(BuildingNotification.GroundPollution))
					{
						this.m_IconCommandBuffer.Remove(buildingEntity,
							this.m_PollutionParameters.m_GroundPollutionNotification);
						notifications.m_Notifications &= ~BuildingNotification.GroundPollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}

					// --- 空气污染 ---
					if (isAirPollutionBad && !notifications.HasNotification(BuildingNotification.AirPollution))
					{
						this.m_IconCommandBuffer.Add(buildingEntity,
							this.m_PollutionParameters.m_AirPollutionNotification,
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

					// --- 废渣噪音 ---
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

