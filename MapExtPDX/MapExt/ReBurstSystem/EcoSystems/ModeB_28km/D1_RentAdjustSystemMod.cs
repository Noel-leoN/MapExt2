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
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
			m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_ResourceSystem = World.GetOrCreateSystemManaged<ResourceSystem>();
			m_GroundPollutionSystem = World.GetOrCreateSystemManaged<GroundPollutionSystem>();
			m_AirPollutionSystem = World.GetOrCreateSystemManaged<AirPollutionSystem>();
			m_NoisePollutionSystem = World.GetOrCreateSystemManaged<NoisePollutionSystem>();
			m_TelecomCoverageSystem = World.GetOrCreateSystemManaged<TelecomCoverageSystem>();
			m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
			m_TaxSystem = World.GetOrCreateSystemManaged<TaxSystem>();
			m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
			m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
			m_DemandParameterQuery = GetEntityQuery(ComponentType.ReadOnly<DemandParameterData>());
			m_BuildingParameterQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
			m_BuildingQuery = GetEntityQuery(ComponentType.ReadOnly<Building>(),
				ComponentType.ReadOnly<UpdateFrame>(), ComponentType.ReadWrite<Renter>(),
				ComponentType.Exclude<StorageProperty>(), ComponentType.Exclude<Temp>(),
				ComponentType.Exclude<Deleted>());
			m_ExtractorParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ExtractorParameterData>());
			m_HealthcareParameterQuery = GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>());
			m_ParkParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ParkParameterData>());
			m_EducationParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EducationParameterData>());
			m_TelecomParameterQuery = GetEntityQuery(ComponentType.ReadOnly<TelecomParameterData>());
			m_GarbageParameterQuery = GetEntityQuery(ComponentType.ReadOnly<GarbageParameterData>());
			m_PoliceParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
			m_CitizenHappinessParameterQuery =
				GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
			m_PollutionParameterQuery = GetEntityQuery(ComponentType.ReadOnly<PollutionParameterData>());
			m_FeeParameterQuery = GetEntityQuery(ComponentType.ReadOnly<ServiceFeeParameterData>());
			RequireForUpdate(m_EconomyParameterQuery);
			RequireForUpdate(m_DemandParameterQuery);
			RequireForUpdate(m_HealthcareParameterQuery);
			RequireForUpdate(m_ParkParameterQuery);
			RequireForUpdate(m_EducationParameterQuery);
			RequireForUpdate(m_TelecomParameterQuery);
			RequireForUpdate(m_GarbageParameterQuery);
			RequireForUpdate(m_PoliceParameterQuery);
			RequireForUpdate(m_FeeParameterQuery);
			RequireForUpdate(m_BuildingQuery);
		}

		protected override void OnUpdate()
		{
			uint updateFrame = SimulationUtils.GetUpdateFrame(m_SimulationSystem.frameIndex,
				kUpdatesPerDay, 16);
			JobHandle groundPollutionDependencies;
			JobHandle airPollutionDependencies;
			JobHandle noisePollutionDependencies;
			JobHandle rentAdjustJobHandle = new AdjustRentJob
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
				m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),
				m_ResourceDatas = SystemAPI.GetComponentLookup<ResourceData>(isReadOnly: true),
				m_TaxRates = m_TaxSystem.GetTaxRates(),
				m_PollutionMap =
					m_GroundPollutionSystem.GetMap(readOnly: true, out groundPollutionDependencies),
				m_AirPollutionMap = m_AirPollutionSystem.GetMap(readOnly: true, out airPollutionDependencies),
				m_NoiseMap = m_NoisePollutionSystem.GetMap(readOnly: true, out noisePollutionDependencies),
				m_CitizenHappinessParameterData =
					m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>(),
				m_BuildingConfigurationData =
					m_BuildingParameterQuery.GetSingleton<BuildingConfigurationData>(),
				m_PollutionParameters = m_PollutionParameterQuery.GetSingleton<PollutionParameterData>(),
				m_FeeParameters = m_FeeParameterQuery.GetSingleton<ServiceFeeParameterData>(),
				m_DeliveryTrucks = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(isReadOnly: true),
				m_ZonePropertiesDatas = SystemAPI.GetComponentLookup<ZonePropertiesData>(isReadOnly: true),
				m_ServiceAvailables = SystemAPI.GetComponentLookup<ServiceAvailable>(isReadOnly: true),
				m_UnderConstructions = SystemAPI.GetComponentLookup<UnderConstruction>(isReadOnly: true),
				m_EconomyParameterData = m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
				m_City = m_CitySystem.City,
				m_UpdateFrameIndex = updateFrame,
				m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
				m_IconCommandBuffer = m_IconCommandSystem.CreateCommandBuffer(),
				// [MOD] 构建租金调节参数
				m_RentTuning = BuildRentTuningParams()
			}.ScheduleParallel(m_BuildingQuery,
				JobUtils.CombineDependencies(groundPollutionDependencies, airPollutionDependencies,
					noisePollutionDependencies, Dependency));
			m_EndFrameBarrier.AddJobHandleForProducer(rentAdjustJobHandle);
			m_ResourceSystem.AddPrefabsReader(rentAdjustJobHandle);
			m_GroundPollutionSystem.AddReader(rentAdjustJobHandle);
			m_AirPollutionSystem.AddReader(rentAdjustJobHandle);
			m_NoisePollutionSystem.AddReader(rentAdjustJobHandle);
			m_TelecomCoverageSystem.AddReader(rentAdjustJobHandle);
			m_TaxSystem.AddReader(rentAdjustJobHandle);
			m_IconCommandSystem.AddCommandBufferWriter(rentAdjustJobHandle);
			Dependency = rentAdjustJobHandle;
		}

		#endregion

		#region Helpers

		/// <summary>从 ModSettings 构建租金调节参数 (Burst 兼容)</summary>
		private static RentTuningParams BuildRentTuningParams()
		{
			var s = Mod.Instance?.Settings;
			if (s == null) return RentTuningParams.Default;
			return new RentTuningParams
			{
				RentMultiplier = new float3(
					s.RentMultiplierResidential / 100f,
					s.RentMultiplierCommercial / 100f,
					s.RentMultiplierIndustrial / 100f),
				LandValueFactor = new float3(
					s.LandValueFactorResidential / 100f,
					s.LandValueFactorCommercial / 100f,
					s.LandValueFactorIndustrial / 100f),
				LevelFactor = new float3(
					s.LevelFactorResidential / 100f,
					s.LevelFactorCommercial / 100f,
					s.LevelFactorIndustrial / 100f),
			};
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
			// [MOD] 租金调节参数 (从 ModSettings 构建)
			public RentTuningParams m_RentTuning;

			/// <summary>
			/// [MOD] 可调租金计算，替代 PropertyUtils.GetRentPricePerRenter。
			/// 分区域应用地价贡献、等级贡献、租金乘数。
			/// </summary>
			private static int GetModdedRentPerRenter(
				BuildingPropertyData prop, int level, int lotSize,
				float landValue, Game.Zones.AreaType area,
				ref EconomyParameterData econ, bool ignoreLV,
				in RentTuningParams tuning)
			{
				// 区域索引: x=住宅(0), y=商业(1), z=工业(2)
				int idx;
				switch (area)
				{
					case Game.Zones.AreaType.Commercial: idx = 1; break;
					case Game.Zones.AreaType.Industrial:  idx = 2; break;
					default: idx = 0; break;
				}

				float zoneBase = econ.m_RentPriceBuildingZoneTypeBase[idx];
				float lvMod    = econ.m_LandValueModifier[idx];
				float lvFactor  = tuning.LandValueFactor[idx];
				float levelFact = tuning.LevelFactor[idx];
				float rentMult  = tuning.RentMultiplier[idx];

				float total = ignoreLV
					? zoneBase * level * levelFact * lotSize * prop.m_SpaceMultiplier
					: (landValue * lvMod * lvFactor + zoneBase * level * levelFact)
					  * lotSize * prop.m_SpaceMultiplier;

				total *= rentMult;

				int renterCount = PropertyUtils.IsMixedBuilding(prop)
					? UnityEngine.Mathf.RoundToInt(prop.m_ResidentialProperties / (1f - econ.m_MixedBuildingCompanyRentPercentage))
					: prop.CountProperties();

				return UnityEngine.Mathf.RoundToInt(total / math.max(1, renterCount));
			}

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
					if (m_CompanyNotifications.HasComponent(renter))
					{
						CompanyNotifications companyNotifications = m_CompanyNotifications[renter];
						if (companyNotifications.m_NoCustomersEntity != Entity.Null ||
						    companyNotifications.m_NoInputEntity != Entity.Null)
						{
							canDisplay = false;
							break;
						}
					}

					// --- 检查员工状态 ---
					if (m_WorkProviders.HasComponent(renter))
					{
						WorkProvider workProvider = m_WorkProviders[renter];
						if (workProvider.m_EducatedNotificationEntity != Entity.Null ||
						    workProvider.m_UneducatedNotificationEntity != Entity.Null)
						{
							canDisplay = false;
							break;
						}
					}

					if (!m_HouseholdCitizenBufs.HasBuffer(renter))
					{
						continue;
					}

					// --- 检查家庭生命体征 ---
					DynamicBuffer<HouseholdCitizen> householdCitizens = m_HouseholdCitizenBufs[renter];
					canDisplay = false;
					for (int j = 0; j < householdCitizens.Length; j++)
					{
						if (!CitizenUtils.IsDead(householdCitizens[j].m_Citizen, ref m_HealthProblems))
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
				if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
				{
					return;
				}

				NativeArray<Entity> buildingEntities = chunk.GetNativeArray(m_EntityType);
				BufferAccessor<Renter> renterBuffers = chunk.GetBufferAccessor(ref m_RenterType);
				DynamicBuffer<CityModifier> cityModifiers = m_CityModifiers[m_City];

				// --- 遍历 Chunk 中的实体 ---
				for (int i = 0; i < buildingEntities.Length; i++)
				{
					Entity buildingEntity = buildingEntities[i];
					Entity buildingPrefab = m_Prefabs[buildingEntity].m_Prefab;

					if (!m_BuildingProperties.HasComponent(buildingPrefab))
					{
						continue;
					}

					// ====== 1. 获取建筑基础数据 ======
					BuildingPropertyData buildingPropertyData = m_BuildingProperties[buildingPrefab];
					Building building = m_Buildings[buildingEntity];
					DynamicBuffer<Renter> renters = renterBuffers[i];
					BuildingData buildingData = m_BuildingDatas[buildingPrefab];

					int lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
					float landValueBase = 0f;

					if (m_LandValues.HasComponent(building.m_RoadEdge))
					{
						landValueBase = m_LandValues[building.m_RoadEdge].m_LandValue;
					}

					// ====== 2. 确定区域类型 ======
					Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
					int buildingLevel = PropertyUtils.GetBuildingLevel(buildingPrefab, m_SpawnableBuildingData);
					bool ignoreLandValue = false;
					bool isOffice = false;

					if (m_SpawnableBuildingData.HasComponent(buildingPrefab))
					{
						SpawnableBuildingData spawnableBuildingData = m_SpawnableBuildingData[buildingPrefab];
						areaType = m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_AreaType;

						if (m_ZonePropertiesDatas.TryGetComponent(spawnableBuildingData.m_ZonePrefab,
							    out var componentData))
						{
							ignoreLandValue = componentData.m_IgnoreLandValue;
						}

						isOffice =
							(m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_ZoneFlags & ZoneFlags.Office) !=
							0;
					}

					// ====== 3. 处理污染通知 ======
					ProcessPollutionNotification(areaType, buildingEntity, cityModifiers);

					// ====== 4. 计算地租与市场挂牌价 ======
					int buildingGarbageFeePerDay = m_FeeParameters.GetBuildingGarbageFeePerDay(areaType, isOffice);
					// [MOD] 使用可调租金公式替代原版 PropertyUtils.GetRentPricePerRenter
					int rentPerRenter = GetModdedRentPerRenter(buildingPropertyData, buildingLevel,
						lotSize, landValueBase, areaType, ref m_EconomyParameterData, ignoreLandValue, in m_RentTuning);

					if (m_OnMarkets.HasComponent(buildingEntity))
					{
						PropertyOnMarket onMarketData = m_OnMarkets[buildingEntity];
						onMarketData.m_AskingRent = rentPerRenter;
						m_OnMarkets[buildingEntity] = onMarketData;
					}

					int propertyCount = buildingPropertyData.CountProperties();
					bool rentersWereRemoved = false;
					int2 affordabilityStats = default(int2);
					bool isExtractorBuilding = m_ExtractorProperties.HasComponent(buildingEntity);

					// ====== 5. 结算建筑内的所有租户 ======
					for (int renterIndex = renters.Length - 1; renterIndex >= 0; renterIndex--)
					{
						Entity renter = renters[renterIndex].m_Renter;
						if (m_PropertyRenters.HasComponent(renter))
						{
							PropertyRenter propertyRenterData = m_PropertyRenters[renter];

							// 🗑️ [MOD功能] 异常清理防御：资源缓冲缺失的关联实体予以直接拆除，防止游戏报错奔溃。
							if (!m_ResourcesBuf.HasBuffer(renter))
							{
								m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex, renter);
								continue;
							}

							// --- 5a. 计算租户支付能力 ---
							int renterUpkeepCapacity = 0;
							int householdIncome = 0;
							bool isHousehold = m_HouseholdCitizenBufs.HasBuffer(renter);

							if (isHousehold)
							{
								householdIncome = EconomyUtils.GetHouseholdIncome(
									m_HouseholdCitizenBufs[renter],
									ref m_Workers, ref m_Citizens, ref m_HealthProblems,
									ref m_EconomyParameterData, m_TaxRates);
								int householdSavings = math.max(0,
									EconomyUtils.GetResources(Game.Economy.Resource.Money,
										m_ResourcesBuf[renter]));
								// 🔧 [MOD修复] 恢复原版 "Income + Savings" 作为绝对破产生死线，有效阻止由于小人不合理存款引发的死循环驱离。
								renterUpkeepCapacity = householdIncome + householdSavings;
							}
							else
							{
								Entity renterPrefab = m_Prefabs[renter].m_Prefab;
								if (!m_ProcessDatas.HasComponent(renterPrefab) ||
								    !m_WorkProviders.HasComponent(renter) ||
								    !m_WorkplaceDatas.HasComponent(renterPrefab))
								{
									continue;
								}

								IndustrialProcessData industrialProcessData = m_ProcessDatas[renterPrefab];
								bool isIndustrial = !m_ServiceAvailables.HasComponent(renter);

								int companyMaxProfitPerDay = EconomyUtils.GetCompanyMaxProfitPerDay(
									m_WorkProviders[renter], areaType == Game.Zones.AreaType.Industrial,
									buildingLevel,
									m_ProcessDatas[renterPrefab], m_ResourcePrefabs,
									m_WorkplaceDatas[renterPrefab], ref m_ResourceDatas,
									ref m_EconomyParameterData);

								renterUpkeepCapacity = ((companyMaxProfitPerDay >= renterUpkeepCapacity)
									? companyMaxProfitPerDay
									: ((!m_OwnedVehicles.HasBuffer(renter))
										? EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData,
											m_ResourcesBuf[renter], m_ResourcePrefabs,
											ref m_ResourceDatas)
										: EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData,
											m_ResourcesBuf[renter], m_OwnedVehicles[renter],
											ref m_LayoutElements, ref m_DeliveryTrucks,
											m_ResourcePrefabs,
											ref m_ResourceDatas)));
							}

							propertyRenterData.m_Rent = rentPerRenter;
							m_PropertyRenters[renter] = propertyRenterData;

							int totalCostPerDay = rentPerRenter + buildingGarbageFeePerDay;

							// --- 5b. 结算驱逐/升迁状态 ---
							if (isHousehold)
							{
								// 🔧 [MOD修复] 彻底分离强迫驱离与自主改善房屋的逻辑：
								if (totalCostPerDay > renterUpkeepCapacity)
								{
									// 此家庭已被榨干最后一丝潜能，挂起待赶指令 (PropertySeeker)
									m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
										renter, value: true);
								}
								// 🎲 [MOD平滑设定] 改善住房：当生活成本 < 家庭收入 15% 时表明手头极其充裕。
								// 以 renter Index 平抑曲线并加入 30% 几率寻求改善，避免全城搬迁拥堵寻路网。
								else if (totalCostPerDay < householdIncome * 0.15f)
								{
									if (((uint)renter.Index + m_UpdateFrameIndex) % 10 < 3)
									{
										m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
											renter, value: true);
									}
								}
							}
							else
							{
								// 公司单位统一以自身估值和流水维持破产线运转
								if (totalCostPerDay > renterUpkeepCapacity)
								{
									m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex,
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
					if (highRentRatio <= 0.7f || !CanDisplayHighRentWarnIcon(renters))
					{
						m_IconCommandBuffer.Remove(buildingEntity,
							m_BuildingConfigurationData.m_HighRentNotification);
						building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
						m_Buildings[buildingEntity] = building;
					}
					// ⚠️ [MOD功能] 高级提示策略：即使目前房间全满 (将 > 置换为 >=), 只要超过 70% 的居民哀鸿遍野叫苦连天，照样显示高级报警！
					// 给玩家提供全图最直观的租差反馈体系
					else if (renters.Length > 0 && !isExtractorBuilding && propertyCount >= renters.Length &&
					         (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) == 0)
					{
						m_IconCommandBuffer.Add(buildingEntity,
							m_BuildingConfigurationData.m_HighRentNotification, IconPriority.Problem);
						building.m_Flags |= Game.Buildings.BuildingFlags.HighRentWarning;
						m_Buildings[buildingEntity] = building;
					}

					// ====== 7. 结算与收尾清理工作 ======

					// 清理超员租客：比如建筑升级、或被拆毁等导致人口容积暴减并挤压现有住户，进行最后一单剪辑驱赶。
					if (renters.Length > 0 && renters.Length > propertyCount)
					{
						Entity lastRenter = renters[^1].m_Renter;
						if (m_PropertyRenters.HasComponent(lastRenter))
						{
							m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex,
								renters[^1].m_Renter);
							renters.RemoveAt(renters.Length - 1);
						}
					}

					// 建筑空置：若全部人员已跑尽，务必连带着移除遗留警告指令。
					if (renters.Length == 0 && (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) !=
					    Game.Buildings.BuildingFlags.None)
					{
						m_IconCommandBuffer.Remove(buildingEntity,
							m_BuildingConfigurationData.m_HighRentNotification);
						building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
						m_Buildings[buildingEntity] = building;
					}

					// 更新挂牌与重投机制：若有租客迁出后空开房源并且没遭废弃，则重新上架待租
					if (m_Prefabs.HasComponent(buildingEntity) && !m_Abandoned.HasComponent(buildingEntity) &&
					    !m_Destroyed.HasComponent(buildingEntity) && rentersWereRemoved &&
					    propertyCount > renters.Length)
					{
						m_CommandBuffer.AddComponent(unfilteredChunkIndex, buildingEntity, new PropertyOnMarket
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
						ref m_Transforms, m_PollutionMap, cityModifiers,
						in m_CitizenHappinessParameterData);
					int2 noiseBonuses = XCellMapSystemRe.GetNoiseBonuses(buildingEntity, ref m_Transforms,
						m_NoiseMap, in m_CitizenHappinessParameterData);
					int2 airPollutionBonuses = XCellMapSystemRe.GetAirPollutionBonuses(buildingEntity,
						ref m_Transforms, m_AirPollutionMap, cityModifiers,
						in m_CitizenHappinessParameterData);

					bool isUnderConstruction = m_UnderConstructions.HasComponent(buildingEntity);

					bool isGroundPollutionBad = !isUnderConstruction &&
					                            groundPollutionBonuses.x + groundPollutionBonuses.y <
					                            2 * m_PollutionParameters.m_GroundPollutionNotificationLimit;
					bool isAirPollutionBad = !isUnderConstruction && airPollutionBonuses.x + airPollutionBonuses.y <
						2 * m_PollutionParameters.m_AirPollutionNotificationLimit;
					bool isNoisePollutionBad = !isUnderConstruction && noiseBonuses.x + noiseBonuses.y <
						2 * m_PollutionParameters.m_NoisePollutionNotificationLimit;

					BuildingNotifications notifications = m_BuildingNotifications[buildingEntity];

					// --- 地面污染 ---
					if (isGroundPollutionBad && !notifications.HasNotification(BuildingNotification.GroundPollution))
					{
						m_IconCommandBuffer.Add(buildingEntity,
							m_PollutionParameters.m_GroundPollutionNotification, IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.GroundPollution;
						m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!isGroundPollutionBad &&
					         notifications.HasNotification(BuildingNotification.GroundPollution))
					{
						m_IconCommandBuffer.Remove(buildingEntity,
							m_PollutionParameters.m_GroundPollutionNotification);
						notifications.m_Notifications &= ~BuildingNotification.GroundPollution;
						m_BuildingNotifications[buildingEntity] = notifications;
					}

					// --- 空气污染 ---
					if (isAirPollutionBad && !notifications.HasNotification(BuildingNotification.AirPollution))
					{
						m_IconCommandBuffer.Add(buildingEntity,
							m_PollutionParameters.m_AirPollutionNotification,
							IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.AirPollution;
						m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!isAirPollutionBad && notifications.HasNotification(BuildingNotification.AirPollution))
					{
						m_IconCommandBuffer.Remove(buildingEntity,
							m_PollutionParameters.m_AirPollutionNotification);
						notifications.m_Notifications &= ~BuildingNotification.AirPollution;
						m_BuildingNotifications[buildingEntity] = notifications;
					}

					// --- 废渣噪音 ---
					if (isNoisePollutionBad && !notifications.HasNotification(BuildingNotification.NoisePollution))
					{
						m_IconCommandBuffer.Add(buildingEntity,
							m_PollutionParameters.m_NoisePollutionNotification, IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.NoisePollution;
						m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!isNoisePollutionBad && notifications.HasNotification(BuildingNotification.NoisePollution))
					{
						m_IconCommandBuffer.Remove(buildingEntity,
							m_PollutionParameters.m_NoisePollutionNotification);
						notifications.m_Notifications &= ~BuildingNotification.NoisePollution;
						m_BuildingNotifications[buildingEntity] = notifications;
					}
				}
			}
		}
	}

	#endregion
}
