using Colossal.Entities;
using Game;
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
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.ModeC
{
	public partial class RentAdjustSystemMod_CellOnly : GameSystemBase
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
				RentAdjustSystemMod_CellOnly.kUpdatesPerDay, 16);
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
					m_IconCommandBuffer = this.m_IconCommandSystem.CreateCommandBuffer(),
					// [MOD] 构建租金调节参数
					m_RentTuning = BuildRentTuningParams()
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
			[ReadOnly] public ComponentLookup<Game.Objects.Transform> m_Transforms;
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
			/// </summary>
			private static int GetModdedRentPerRenter(
				BuildingPropertyData prop, int level, int lotSize,
				float landValue, Game.Zones.AreaType area,
				ref EconomyParameterData econ, bool ignoreLV,
				in RentTuningParams tuning)
			{
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

			private bool CanDisplayHighRentWarnIcon(DynamicBuffer<Renter> renters)
			{
				bool result = true;
				for (int i = 0; i < renters.Length; i++)
				{
					Entity renter = renters[i].m_Renter;
					if (this.m_CompanyNotifications.HasComponent(renter))
					{
						CompanyNotifications companyNotifications = this.m_CompanyNotifications[renter];
						if (companyNotifications.m_NoCustomersEntity != Entity.Null ||
						    companyNotifications.m_NoInputEntity != Entity.Null)
						{
							result = false;
							break;
						}
					}

					if (this.m_WorkProviders.HasComponent(renter))
					{
						WorkProvider workProvider = this.m_WorkProviders[renter];
						if (workProvider.m_EducatedNotificationEntity != Entity.Null ||
						    workProvider.m_UneducatedNotificationEntity != Entity.Null)
						{
							result = false;
							break;
						}
					}

					if (!this.m_HouseholdCitizenBufs.HasBuffer(renter))
					{
						continue;
					}

					DynamicBuffer<HouseholdCitizen> dynamicBuffer = this.m_HouseholdCitizenBufs[renter];
					result = false;
					for (int j = 0; j < dynamicBuffer.Length; j++)
					{
						if (!CitizenUtils.IsDead(dynamicBuffer[j].m_Citizen, ref this.m_HealthProblems))
						{
							result = true;
							break;
						}
					}
				}

				return result;
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}

				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor(ref this.m_RenterType);
				DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					Entity prefab = this.m_Prefabs[entity].m_Prefab;
					if (!this.m_BuildingProperties.HasComponent(prefab))
					{
						continue;
					}

					BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[prefab];
					Building building = this.m_Buildings[entity];
					DynamicBuffer<Renter> renters = bufferAccessor[i];
					BuildingData buildingData = this.m_BuildingDatas[prefab];
					int lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
					float landValueBase = 0f;
					if (this.m_LandValues.HasComponent(building.m_RoadEdge))
					{
						landValueBase = this.m_LandValues[building.m_RoadEdge].m_LandValue;
					}

					Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
					int buildingLevel = PropertyUtils.GetBuildingLevel(prefab, this.m_SpawnableBuildingData);
					bool ignoreLandValue = false;
					bool isOffice = false;
					if (this.m_SpawnableBuildingData.HasComponent(prefab))
					{
						SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingData[prefab];
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

					this.ProcessPollutionNotification(areaType, entity, cityModifiers);
					int buildingGarbageFeePerDay = this.m_FeeParameters.GetBuildingGarbageFeePerDay(areaType, isOffice);
					int rentPricePerRenter = GetModdedRentPerRenter(buildingPropertyData, buildingLevel,
						lotSize, landValueBase, areaType, ref this.m_EconomyParameterData, ignoreLandValue, in this.m_RentTuning);
					if (this.m_OnMarkets.HasComponent(entity))
					{
						PropertyOnMarket onMarket = this.m_OnMarkets[entity];
						onMarket.m_AskingRent = rentPricePerRenter;
						this.m_OnMarkets[entity] = onMarket;
					}

					int maxPropertiesCount = buildingPropertyData.CountProperties();
					bool hasRemovedRenters = false;
					int2 renterPovertyStats = default(int2);
					bool isExtractor = this.m_ExtractorProperties.HasComponent(entity);
					for (int renterIndex = renters.Length - 1; renterIndex >= 0; renterIndex--)
					{
						Entity renter = renters[renterIndex].m_Renter;
						if (this.m_PropertyRenters.HasComponent(renter))
						{
							PropertyRenter propertyRenter = this.m_PropertyRenters[renter];
							if (!this.m_ResourcesBuf.HasBuffer(renter))
							{
								UnityEngine.Debug.Log($"no resources:{renter.Index}");
								continue;
							}

							int renterWealth = 0;
							bool hasHousehold = this.m_HouseholdCitizenBufs.HasBuffer(renter);
							if (hasHousehold)
							{
								renterWealth =
									EconomyUtils.GetHouseholdIncome(this.m_HouseholdCitizenBufs[renter],
										ref this.m_Workers,
										ref this.m_Citizens, ref this.m_HealthProblems, ref this.m_EconomyParameterData,
										this.m_TaxRates) + math.max(0,
										EconomyUtils.GetResources(Resource.Money, this.m_ResourcesBuf[renter]));
							}
							else
							{
								Entity prefab2 = this.m_Prefabs[renter].m_Prefab;
								if (!this.m_ProcessDatas.HasComponent(prefab2) ||
								    !this.m_WorkProviders.HasComponent(renter) ||
								    !this.m_WorkplaceDatas.HasComponent(prefab2))
								{
									continue;
								}

								IndustrialProcessData industrialProcessData = this.m_ProcessDatas[prefab2];
								bool isIndustrial = !this.m_ServiceAvailables.HasComponent(renter);
								int companyMaxProfitPerDay = EconomyUtils.GetCompanyMaxProfitPerDay(
									this.m_WorkProviders[renter], areaType == Game.Zones.AreaType.Industrial,
									buildingLevel,
									this.m_ProcessDatas[prefab2], this.m_ResourcePrefabs,
									this.m_WorkplaceDatas[prefab2],
									ref this.m_ResourceDatas, ref this.m_EconomyParameterData);
								renterWealth = ((companyMaxProfitPerDay >= renterWealth)
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

							propertyRenter.m_Rent = rentPricePerRenter;
							this.m_PropertyRenters[renter] = propertyRenter;
							if (rentPricePerRenter + buildingGarbageFeePerDay > renterWealth || (hasHousehold &&
								    rentPricePerRenter + buildingGarbageFeePerDay < renterWealth / 2))
							{
								this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, renter,
									value: true);
							}

							renterPovertyStats.y++;
							if (rentPricePerRenter > renterWealth)
							{
								renterPovertyStats.x++;
							}
						}
						else
						{
							renters.RemoveAt(renterIndex);
							hasRemovedRenters = true;
						}
					}

					if (!(renterPovertyStats.x / math.max(1f, renterPovertyStats.y) > 0.7f) ||
					    !this.CanDisplayHighRentWarnIcon(renters))
					{
						this.m_IconCommandBuffer.Remove(entity,
							this.m_BuildingConfigurationData.m_HighRentNotification);
						building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
						this.m_Buildings[entity] = building;
					}
					else if (renters.Length > 0 && !isExtractor && maxPropertiesCount > renters.Length &&
					         (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) == 0)
					{
						this.m_IconCommandBuffer.Add(entity, this.m_BuildingConfigurationData.m_HighRentNotification,
							IconPriority.Problem);
						building.m_Flags |= Game.Buildings.BuildingFlags.HighRentWarning;
						this.m_Buildings[entity] = building;
					}

					if (renters.Length > maxPropertiesCount &&
					    this.m_PropertyRenters.HasComponent(renters[renters.Length - 1].m_Renter))
					{
						this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex,
							renters[renters.Length - 1].m_Renter);
						renters.RemoveAt(renters.Length - 1);
						UnityEngine.Debug.LogWarning($"Removed extra renter from building:{entity.Index}");
					}

					if (renters.Length == 0 && (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) !=
					    Game.Buildings.BuildingFlags.None)
					{
						this.m_IconCommandBuffer.Remove(entity,
							this.m_BuildingConfigurationData.m_HighRentNotification);
						building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
						this.m_Buildings[entity] = building;
					}

					if (this.m_Prefabs.HasComponent(entity) && !this.m_Abandoned.HasComponent(entity) &&
					    !this.m_Destroyed.HasComponent(entity) && hasRemovedRenters &&
					    maxPropertiesCount > renters.Length)
					{
						this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new PropertyOnMarket
						{
							m_AskingRent = rentPricePerRenter
						});
					}
				}
			}

			private void ProcessPollutionNotification(Game.Zones.AreaType areaType, Entity buildingEntity,
				DynamicBuffer<CityModifier> cityModifiers)
			{
				if (areaType == Game.Zones.AreaType.Residential)
				{
					int2 groundPollutionBonuses = XCellMapSystemRe.GetGroundPollutionBonuses(
						buildingEntity, ref this.m_Transforms, this.m_PollutionMap, cityModifiers,
						in this.m_CitizenHappinessParameterData);
					int2 noiseBonuses = XCellMapSystemRe.GetNoiseBonuses(buildingEntity,
						ref this.m_Transforms, this.m_NoiseMap, in this.m_CitizenHappinessParameterData);
					int2 airPollutionBonuses = XCellMapSystemRe.GetAirPollutionBonuses(buildingEntity,
						ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers,
						in this.m_CitizenHappinessParameterData);
					bool isUnderConstruction = this.m_UnderConstructions.HasComponent(buildingEntity);
					bool groundPollutionWarning = !isUnderConstruction &&
					                              groundPollutionBonuses.x + groundPollutionBonuses.y <
					                              2 * this.m_PollutionParameters.m_GroundPollutionNotificationLimit;
					bool isExtractor = !isUnderConstruction && airPollutionBonuses.x + airPollutionBonuses.y <
						2 * this.m_PollutionParameters.m_AirPollutionNotificationLimit;
					bool hasHousehold = !isUnderConstruction && noiseBonuses.x + noiseBonuses.y <
						2 * this.m_PollutionParameters.m_NoisePollutionNotificationLimit;
					BuildingNotifications notifications = this.m_BuildingNotifications[buildingEntity];
					if (groundPollutionWarning && !notifications.HasNotification(BuildingNotification.GroundPollution))
					{
						this.m_IconCommandBuffer.Add(buildingEntity,
							this.m_PollutionParameters.m_GroundPollutionNotification, IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.GroundPollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!groundPollutionWarning &&
					         notifications.HasNotification(BuildingNotification.GroundPollution))
					{
						this.m_IconCommandBuffer.Remove(buildingEntity,
							this.m_PollutionParameters.m_GroundPollutionNotification);
						notifications.m_Notifications &= ~BuildingNotification.GroundPollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}

					if (isExtractor && !notifications.HasNotification(BuildingNotification.AirPollution))
					{
						this.m_IconCommandBuffer.Add(buildingEntity,
							this.m_PollutionParameters.m_AirPollutionNotification,
							IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.AirPollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!isExtractor && notifications.HasNotification(BuildingNotification.AirPollution))
					{
						this.m_IconCommandBuffer.Remove(buildingEntity,
							this.m_PollutionParameters.m_AirPollutionNotification);
						notifications.m_Notifications &= ~BuildingNotification.AirPollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}

					if (hasHousehold && !notifications.HasNotification(BuildingNotification.NoisePollution))
					{
						this.m_IconCommandBuffer.Add(buildingEntity,
							this.m_PollutionParameters.m_NoisePollutionNotification, IconPriority.Problem);
						notifications.m_Notifications |= BuildingNotification.NoisePollution;
						this.m_BuildingNotifications[buildingEntity] = notifications;
					}
					else if (!hasHousehold && notifications.HasNotification(BuildingNotification.NoisePollution))
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
