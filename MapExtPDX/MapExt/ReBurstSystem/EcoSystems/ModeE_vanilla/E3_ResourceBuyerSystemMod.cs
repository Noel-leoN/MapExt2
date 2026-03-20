using System;
using System.Runtime.CompilerServices;
using Game;
using Game.Simulation;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.ModeE
{
	public partial class ResourceBuyerSystemMod : GameSystemBase
	{
		#region Constants

		private const int UPDATE_INTERVAL = 16;
		public override int GetUpdateInterval(SystemUpdatePhase phase) => UPDATE_INTERVAL;

		#endregion

		#region Fields

		[Flags]
		private enum SaleFlags : byte
		{
			None = 0,
			CommercialSeller = 1,
			ImportFromOC = 2,
			Virtual = 4
		}

		private EntityQuery m_BuyerQuery;
		private EntityQuery m_CarPrefabQuery;
		private EntityQuery m_EconomyParameterQuery;
		private EntityQuery m_ResidentPrefabQuery;
		private EntityQuery m_PopulationQuery;
		private ComponentTypeSet m_PathfindTypes;
		private EndFrameBarrier m_EndFrameBarrier;
		private PathfindSetupSystem m_PathfindSetupSystem;
		private ResourceSystem m_ResourceSystem;
		private SimulationSystem m_SimulationSystem;
		private TaxSystem m_TaxSystem;
		private TimeSystem m_TimeSystem;
		private CityConfigurationSystem m_CityConfigurationSystem;
		private PersonalCarSelectData m_PersonalCarSelectData;
		private CitySystem m_CitySystem;
		private CityProductionStatisticSystem m_CityProductionStatisticSystem;
		private NativeQueue<SalesEvent> m_SalesQueue;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_PathfindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
			m_TaxSystem = base.World.GetOrCreateSystemManaged<TaxSystem>();
			m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			m_CityConfigurationSystem = base.World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			m_PersonalCarSelectData = new PersonalCarSelectData(this);
			m_CitySystem = base.World.GetOrCreateSystemManaged<CitySystem>();
			m_CityProductionStatisticSystem = base.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
			m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			m_SalesQueue = new NativeQueue<SalesEvent>(Allocator.Persistent);
			m_BuyerQuery = GetEntityQuery(new EntityQueryDesc
			{
				All = new ComponentType[2]
				{
					ComponentType.ReadWrite<ResourceBuyer>(),
					ComponentType.ReadWrite<TripNeeded>()
				},
				None = new ComponentType[3]
				{
					ComponentType.ReadOnly<TravelPurpose>(),
					ComponentType.ReadOnly<Deleted>(),
					ComponentType.ReadOnly<Temp>()
				}
			}, new EntityQueryDesc
			{
				All = new ComponentType[1] { ComponentType.ReadOnly<ResourceBought>() },
				None = new ComponentType[2]
				{
					ComponentType.ReadOnly<Deleted>(),
					ComponentType.ReadOnly<Temp>()
				}
			});
			m_CarPrefabQuery = GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());
			m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
			m_PopulationQuery = GetEntityQuery(ComponentType.ReadOnly<Population>());
			m_ResidentPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<ObjectData>(),
				ComponentType.ReadOnly<HumanData>(), ComponentType.ReadOnly<ResidentData>(),
				ComponentType.ReadOnly<PrefabData>());
			m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(),
				ComponentType.ReadWrite<PathElement>());
			RequireForUpdate(m_BuyerQuery);
			RequireForUpdate(m_EconomyParameterQuery);
			RequireForUpdate(m_PopulationQuery);
		}

		protected override void OnDestroy()
		{
			m_SalesQueue.Dispose();
			base.OnDestroy();
		}

		protected override void OnStopRunning()
		{
			base.OnStopRunning();
		}

		protected override void OnUpdate()
		{
			if (m_BuyerQuery.CalculateEntityCount() > 0)
			{
				m_PersonalCarSelectData.PreUpdate(this, m_CityConfigurationSystem, m_CarPrefabQuery, Allocator.TempJob,
					out var jobHandle);
				JobHandle outJobHandle;
				HandleBuyersJob jobData = new HandleBuyersJob
				{
					m_EntityType = SystemAPI.GetEntityTypeHandle(),
					m_BuyerType = SystemAPI.GetComponentTypeHandle<ResourceBuyer>(isReadOnly: true),
					m_BoughtType = SystemAPI.GetComponentTypeHandle<ResourceBought>(isReadOnly: true),
					m_TripType = SystemAPI.GetBufferTypeHandle<TripNeeded>(isReadOnly: false),
					m_CitizenType = SystemAPI.GetComponentTypeHandle<Citizen>(isReadOnly: true),
					m_CreatureDataType = SystemAPI.GetComponentTypeHandle<CreatureData>(isReadOnly: true),
					m_ResidentDataType = SystemAPI.GetComponentTypeHandle<ResidentData>(isReadOnly: true),
					m_AttendingMeetingType = SystemAPI.GetComponentTypeHandle<AttendingMeeting>(isReadOnly: true),
					m_ServiceAvailables = SystemAPI.GetComponentLookup<ServiceAvailable>(isReadOnly: true),
					m_PathInformation = SystemAPI.GetComponentLookup<PathInformation>(isReadOnly: true),
					m_Properties = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
					m_CarKeepers = SystemAPI.GetComponentLookup<CarKeeper>(isReadOnly: true),
					m_BicycleOwners = SystemAPI.GetComponentLookup<BicycleOwner>(isReadOnly: true),
					m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(isReadOnly: true),
					m_PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(isReadOnly: true),
					m_Targets = SystemAPI.GetComponentLookup<Target>(isReadOnly: true),
					m_CurrentBuildings = SystemAPI.GetComponentLookup<CurrentBuilding>(isReadOnly: true),
					m_OutsideConnections =
						SystemAPI.GetComponentLookup<Game.Objects.OutsideConnection>(isReadOnly: true),
					m_HouseholdMembers = SystemAPI.GetComponentLookup<HouseholdMember>(isReadOnly: true),
					m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
					m_TouristHouseholds = SystemAPI.GetComponentLookup<TouristHousehold>(isReadOnly: true),
					m_CommuterHouseholds = SystemAPI.GetComponentLookup<CommuterHousehold>(isReadOnly: true),
					m_Workers = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
					m_DeliveryTrucks = SystemAPI.GetComponentLookup<Game.Vehicles.DeliveryTruck>(isReadOnly: true),
					m_StorageCompanies = SystemAPI.GetComponentLookup<Game.Companies.StorageCompany>(isReadOnly: true),
					m_Resources = SystemAPI.GetBufferLookup<Game.Economy.Resources>(isReadOnly: true),
					m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
					m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(isReadOnly: true),
					m_GuestVehicles = SystemAPI.GetBufferLookup<GuestVehicle>(isReadOnly: true),
					m_LayoutElements = SystemAPI.GetBufferLookup<LayoutElement>(isReadOnly: true),
					m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),
					m_ResourceDatas = SystemAPI.GetComponentLookup<ResourceData>(isReadOnly: true),
					m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(isReadOnly: true),
					m_ObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(isReadOnly: true),
					m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(isReadOnly: true),
					m_CoordinatedMeetings = SystemAPI.GetComponentLookup<CoordinatedMeeting>(isReadOnly: false),
					m_HaveCoordinatedMeetingDatas =
						SystemAPI.GetBufferLookup<HaveCoordinatedMeetingData>(isReadOnly: true),
					m_OutsideConnectionDatas = SystemAPI.GetComponentLookup<OutsideConnectionData>(isReadOnly: true),
					m_Populations = SystemAPI.GetComponentLookup<Population>(isReadOnly: false),
					m_TimeOfDay = m_TimeSystem.normalizedTime,
					m_FrameIndex = m_SimulationSystem.frameIndex,
					m_RandomSeed = RandomSeed.Next(),
					m_PathfindTypes = m_PathfindTypes,
					m_HumanChunks =
						m_ResidentPrefabQuery.ToArchetypeChunkListAsync(base.World.UpdateAllocator.ToAllocator,
							out outJobHandle),
					m_PersonalCarSelectData = m_PersonalCarSelectData,
					m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter(),
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
					m_EconomyParameterData = m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
					m_City = m_CitySystem.City,
					m_SalesQueue = m_SalesQueue.AsParallelWriter(),
					// [MOD EXT]
					m_DynamicShoppingMaxCost = MapExtPDX.Mod.Instance.CurrentSettings.ShoppingMaxCost,
					m_CompanyShoppingMaxCost = MapExtPDX.Mod.Instance.CurrentSettings.CompanyShoppingMaxCost
				};
				base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, m_BuyerQuery,
					JobHandle.CombineDependencies(base.Dependency, outJobHandle, jobHandle));
				m_ResourceSystem.AddPrefabsReader(base.Dependency);
				m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
				m_PathfindSetupSystem.AddQueueWriter(base.Dependency);
				JobHandle deps;
				BuyJob jobData2 = new BuyJob
				{
					m_Resources = SystemAPI.GetBufferLookup<Game.Economy.Resources>(isReadOnly: false),
					m_SalesQueue = m_SalesQueue,
					m_Services = SystemAPI.GetComponentLookup<ServiceAvailable>(isReadOnly: false),
					m_TransformDatas = SystemAPI.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true),
					m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
					m_OwnedVehicles = SystemAPI.GetBufferLookup<OwnedVehicle>(isReadOnly: true),
					m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
					m_HouseholdAnimals = SystemAPI.GetBufferLookup<HouseholdAnimal>(isReadOnly: true),
					m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_ServiceCompanies = SystemAPI.GetComponentLookup<ServiceCompanyData>(isReadOnly: true),
					m_Storages = SystemAPI.GetComponentLookup<Game.Companies.StorageCompany>(isReadOnly: true),
					m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: false),
					m_BuyingCompanies = SystemAPI.GetComponentLookup<BuyingCompany>(isReadOnly: false),
					m_ResourceDatas = SystemAPI.GetComponentLookup<ResourceData>(isReadOnly: true),
					m_TradeCosts = SystemAPI.GetBufferLookup<TradeCost>(isReadOnly: false),
					m_CompanyStatistics = SystemAPI.GetComponentLookup<CompanyStatisticData>(isReadOnly: false),
					m_OutsideConnections =
						SystemAPI.GetComponentLookup<Game.Objects.OutsideConnection>(isReadOnly: true),
					m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),
					m_RandomSeed = RandomSeed.Next(),
					m_FrameIndex = m_SimulationSystem.frameIndex,
					m_PersonalCarSelectData = m_PersonalCarSelectData,
					m_PopulationData = SystemAPI.GetComponentLookup<Population>(isReadOnly: true),
					m_PopulationEntity = m_PopulationQuery.GetSingletonEntity(),
					m_CitizenConsumptionAccumulator =
						m_CityProductionStatisticSystem.GetCityResourceUsageAccumulator(
							CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, out deps),
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
				};
				base.Dependency =
					IJobExtensions.Schedule(jobData2, JobHandle.CombineDependencies(base.Dependency, deps));
				m_PersonalCarSelectData.PostUpdate(base.Dependency);
				m_ResourceSystem.AddPrefabsReader(base.Dependency);
				m_TaxSystem.AddReader(base.Dependency);
				m_CityProductionStatisticSystem.AddCityUsageAccumulatorWriter(
					CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, base.Dependency);
				m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
			}
		}

		#endregion

		#region Jobs

		[BurstCompile]
		private struct BuyJob : IJob
		{
			public NativeQueue<SalesEvent> m_SalesQueue;
			public BufferLookup<Game.Economy.Resources> m_Resources;

			public ComponentLookup<ServiceAvailable> m_Services;

			[NativeDisableParallelForRestriction] public ComponentLookup<Household> m_Households;
			[NativeDisableParallelForRestriction] public ComponentLookup<BuyingCompany> m_BuyingCompanies;

			[ReadOnly] public ComponentLookup<Game.Objects.Transform> m_TransformDatas;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;

			[ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
			[ReadOnly] public ComponentLookup<ServiceCompanyData> m_ServiceCompanies;

			[ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles;
			[ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			[ReadOnly] public BufferLookup<HouseholdAnimal> m_HouseholdAnimals;
			[ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;

			[ReadOnly] public ComponentLookup<Game.Companies.StorageCompany> m_Storages;
			public ComponentLookup<CompanyStatisticData> m_CompanyStatistics;

			public BufferLookup<TradeCost> m_TradeCosts;

			[ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;
			[ReadOnly] public ResourcePrefabs m_ResourcePrefabs;

			[ReadOnly] public PersonalCarSelectData m_PersonalCarSelectData;
			[ReadOnly] public ComponentLookup<Population> m_PopulationData;
			public NativeArray<int> m_CitizenConsumptionAccumulator;

			public Entity m_PopulationEntity;
			public RandomSeed m_RandomSeed;

			public EntityCommandBuffer m_CommandBuffer;

			[ReadOnly] public uint m_FrameIndex;

			public void Execute()
			{
				Unity.Mathematics.Random random = m_RandomSeed.GetRandom(0);
				_ = m_PopulationData[m_PopulationEntity];
				SalesEvent item;
				while (m_SalesQueue.TryDequeue(out item))
				{
					if (!m_Resources.HasBuffer(item.m_Buyer) || item.m_Amount == 0)
					{
						continue;
					}

					bool isCommercial = (item.m_Flags & SaleFlags.CommercialSeller) != 0;
					bool isVirtual = (item.m_Flags & SaleFlags.Virtual) != 0;
					float transactionPrice =
						(isCommercial
							? EconomyUtils.GetMarketPrice(item.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas)
							: EconomyUtils.GetIndustrialPrice(item.m_Resource, m_ResourcePrefabs,
								ref m_ResourceDatas)) * (float)item.m_Amount;
					if (m_TradeCosts.HasBuffer(item.m_Seller))
					{
						DynamicBuffer<TradeCost> costs = m_TradeCosts[item.m_Seller];
						TradeCost tradeCost = EconomyUtils.GetTradeCost(item.m_Resource, costs);
						transactionPrice += (float)item.m_Amount * tradeCost.m_BuyCost;
						float weight = EconomyUtils.GetWeight(item.m_Resource, m_ResourcePrefabs, ref m_ResourceDatas);
						Assert.IsTrue(item.m_Amount != -1);
						float unitTransportCost =
							(float)EconomyUtils.GetTransportCost(item.m_Distance, item.m_Resource, item.m_Amount,
								weight) / (1f + (float)item.m_Amount);
						TradeCost newcost = default(TradeCost);
						if (m_TradeCosts.HasBuffer(item.m_Buyer))
						{
							newcost = EconomyUtils.GetTradeCost(item.m_Resource, m_TradeCosts[item.m_Buyer]);
						}

						if (!m_OutsideConnections.HasComponent(item.m_Seller) && !isVirtual)
						{
							tradeCost.m_SellCost = math.lerp(tradeCost.m_SellCost,
								unitTransportCost + newcost.m_SellCost, 0.5f);
							EconomyUtils.SetTradeCost(item.m_Resource, tradeCost, costs, keepLastTime: true);
						}

						if (m_TradeCosts.HasBuffer(item.m_Buyer) && !m_OutsideConnections.HasComponent(item.m_Buyer))
						{
							if (unitTransportCost + tradeCost.m_BuyCost < newcost.m_BuyCost)
							{
								newcost.m_BuyCost = unitTransportCost + tradeCost.m_BuyCost;
							}
							else
							{
								newcost.m_BuyCost = math.lerp(newcost.m_BuyCost,
									unitTransportCost + tradeCost.m_BuyCost, 0.5f);
							}

							EconomyUtils.SetTradeCost(item.m_Resource, newcost, m_TradeCosts[item.m_Buyer],
								keepLastTime: true);
						}
					}

					if (m_Resources.HasBuffer(item.m_Seller) &&
					    EconomyUtils.GetResources(item.m_Resource, m_Resources[item.m_Seller]) <= 0)
					{
						continue;
					}

					if (isCommercial && m_Services.HasComponent(item.m_Seller) &&
					    m_PropertyRenters.HasComponent(item.m_Seller))
					{
						Entity prefab = m_Prefabs[item.m_Seller].m_Prefab;
						ServiceAvailable value = m_Services[item.m_Seller];
						ServiceCompanyData serviceCompanyData = m_ServiceCompanies[prefab];
						transactionPrice *= EconomyUtils.GetServicePriceMultiplier(value.m_ServiceAvailable,
							serviceCompanyData.m_MaxService);
						value.m_ServiceAvailable =
							math.max(0, Mathf.RoundToInt(value.m_ServiceAvailable - item.m_Amount));
						if (value.m_MeanPriority > 0f)
						{
							value.m_MeanPriority = math.min(1f,
								math.lerp(value.m_MeanPriority,
									(float)value.m_ServiceAvailable / (float)serviceCompanyData.m_MaxService, 0.1f));
						}
						else
						{
							value.m_MeanPriority = math.min(1f,
								(float)value.m_ServiceAvailable / (float)serviceCompanyData.m_MaxService);
						}

						m_Services[item.m_Seller] = value;
					}

					if (m_Resources.HasBuffer(item.m_Seller) && !m_Storages.HasComponent(item.m_Seller))
					{
						DynamicBuffer<Game.Economy.Resources> resources = m_Resources[item.m_Seller];
						int currentResources = EconomyUtils.GetResources(item.m_Resource, resources);
						EconomyUtils.AddResources(item.m_Resource,
							-math.min(currentResources, Mathf.RoundToInt(item.m_Amount)), resources);
					}

					EconomyUtils.AddResources(Resource.Money, -Mathf.RoundToInt(transactionPrice),
						m_Resources[item.m_Buyer]);
					if (m_Households.HasComponent(item.m_Buyer))
					{
						Household value2 = m_Households[item.m_Buyer];
						value2.m_Resources = (int)math.clamp((long)((float)value2.m_Resources + transactionPrice),
							-2147483648L, 2147483647L);
						value2.m_ShoppedValuePerDay += (uint)transactionPrice;
						m_Households[item.m_Buyer] = value2;
						int resourceIndex = EconomyUtils.GetResourceIndex(item.m_Resource);
						m_CitizenConsumptionAccumulator[resourceIndex] += item.m_Amount;
					}
					else if (m_BuyingCompanies.HasComponent(item.m_Buyer))
					{
						BuyingCompany value3 = m_BuyingCompanies[item.m_Buyer];
						value3.m_LastTradePartner = item.m_Seller;
						m_BuyingCompanies[item.m_Buyer] = value3;
						if ((item.m_Flags & SaleFlags.Virtual) != SaleFlags.None)
						{
							EconomyUtils.AddResources(item.m_Resource, item.m_Amount, m_Resources[item.m_Buyer]);
						}
					}

					if (!m_Storages.HasComponent(item.m_Seller) && m_PropertyRenters.HasComponent(item.m_Seller))
					{
						DynamicBuffer<Game.Economy.Resources> resources3 = m_Resources[item.m_Seller];
						EconomyUtils.AddResources(Resource.Money, Mathf.RoundToInt(transactionPrice), resources3);
					}

					if (m_CompanyStatistics.HasComponent(item.m_Seller))
					{
						CompanyStatisticData value4 = m_CompanyStatistics[item.m_Seller];
						value4.m_CurrentNumberOfCustomers++;
						m_CompanyStatistics[item.m_Seller] = value4;
					}

					if (m_CompanyStatistics.HasComponent(item.m_Buyer))
					{
						CompanyStatisticData value5 = m_CompanyStatistics[item.m_Buyer];
						value5.m_CurrentCostOfBuyingResources += math.abs((int)transactionPrice);
						m_CompanyStatistics[item.m_Buyer] = value5;
					}

					if (item.m_Resource != Resource.Vehicles || item.m_Amount != HouseholdBehaviorSystem.kCarAmount ||
					    !m_PropertyRenters.HasComponent(item.m_Seller))
					{
						continue;
					}

					Entity property = m_PropertyRenters[item.m_Seller].m_Property;
					if (!m_TransformDatas.HasComponent(property) || !m_HouseholdCitizens.HasBuffer(item.m_Buyer))
					{
						continue;
					}

					Entity entity = item.m_Buyer;
					Game.Objects.Transform transform = m_TransformDatas[property];
					int length = m_HouseholdCitizens[entity].Length;
					int animalCount = (m_HouseholdAnimals.HasBuffer(entity) ? m_HouseholdAnimals[entity].Length : 0);
					int passengerAmount;
					int requiredCapacity;
					if (m_OwnedVehicles.HasBuffer(entity) && m_OwnedVehicles[entity].Length >= 1)
					{
						passengerAmount = random.NextInt(1, 1 + length);
						requiredCapacity = random.NextInt(1, 2 + animalCount);
					}
					else
					{
						passengerAmount = length;
						requiredCapacity = 1 + animalCount;
					}

					if (random.NextInt(20) == 0)
					{
						requiredCapacity += 5;
					}

					Entity entity2 = m_PersonalCarSelectData.CreateVehicle(m_CommandBuffer, ref random, passengerAmount,
						requiredCapacity, avoidTrailers: true, noSlowVehicles: false, bicycle: false, transform,
						property, Entity.Null, (PersonalCarFlags)0u, stopped: true);
					if (entity2 != Entity.Null)
					{
						m_CommandBuffer.AddComponent(entity2, new Owner(entity));
						if (!m_OwnedVehicles.HasBuffer(entity))
						{
							m_CommandBuffer.AddBuffer<OwnedVehicle>(entity);
						}
					}
				}
			}
		}

		[BurstCompile]
		private struct HandleBuyersJob : IJobChunk
		{
			[ReadOnly] public ComponentTypeHandle<ResourceBuyer> m_BuyerType;
			[ReadOnly] public ComponentTypeHandle<ResourceBought> m_BoughtType;

			[ReadOnly] public EntityTypeHandle m_EntityType;
			public BufferTypeHandle<TripNeeded> m_TripType;

			[ReadOnly] public ComponentTypeHandle<Citizen> m_CitizenType;
			[ReadOnly] public ComponentTypeHandle<CreatureData> m_CreatureDataType;

			[ReadOnly] public ComponentTypeHandle<ResidentData> m_ResidentDataType;
			[ReadOnly] public ComponentTypeHandle<AttendingMeeting> m_AttendingMeetingType;

			[ReadOnly] public ComponentLookup<PathInformation> m_PathInformation;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_Properties;

			[ReadOnly] public ComponentLookup<ServiceAvailable> m_ServiceAvailables;
			[ReadOnly] public ComponentLookup<CarKeeper> m_CarKeepers;

			[ReadOnly] public ComponentLookup<BicycleOwner> m_BicycleOwners;
			[ReadOnly] public ComponentLookup<ParkedCar> m_ParkedCarData;

			[ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;
			[ReadOnly] public ComponentLookup<Target> m_Targets;

			[ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildings;
			[ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			[ReadOnly] public ComponentLookup<HouseholdMember> m_HouseholdMembers;
			[ReadOnly] public ComponentLookup<Household> m_Households;

			[ReadOnly] public ComponentLookup<TouristHousehold> m_TouristHouseholds;
			[ReadOnly] public ComponentLookup<CommuterHousehold> m_CommuterHouseholds;

			[ReadOnly] public ComponentLookup<Worker> m_Workers;
			[ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;

			[ReadOnly] public ComponentLookup<Game.Companies.StorageCompany> m_StorageCompanies;
			[ReadOnly] public BufferLookup<Game.Economy.Resources> m_Resources;

			[ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
			[ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			[ReadOnly] public BufferLookup<GuestVehicle> m_GuestVehicles;
			[ReadOnly] public BufferLookup<LayoutElement> m_LayoutElements;

			[NativeDisableParallelForRestriction] public ComponentLookup<CoordinatedMeeting> m_CoordinatedMeetings;
			[ReadOnly] public BufferLookup<HaveCoordinatedMeetingData> m_HaveCoordinatedMeetingDatas;

			[ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
			[ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;

			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;
			[ReadOnly] public ComponentLookup<CarData> m_PrefabCarData;

			[ReadOnly] public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;
			[ReadOnly] public ComponentLookup<OutsideConnectionData> m_OutsideConnectionDatas;

			[ReadOnly] public ComponentLookup<HumanData> m_PrefabHumanData;
			[ReadOnly] public ComponentLookup<Population> m_Populations;

			[ReadOnly] public float m_TimeOfDay;
			[ReadOnly] public uint m_FrameIndex;

			[ReadOnly] public RandomSeed m_RandomSeed;
			[ReadOnly] public ComponentTypeSet m_PathfindTypes;

			[ReadOnly] public NativeList<ArchetypeChunk> m_HumanChunks;
			[ReadOnly] public PersonalCarSelectData m_PersonalCarSelectData;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
			public EconomyParameterData m_EconomyParameterData;

			public Entity m_City;
			public NativeQueue<SalesEvent>.ParallelWriter m_SalesQueue;

			// [MOD EXT]
			public float m_DynamicShoppingMaxCost;
			public float m_CompanyShoppingMaxCost;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
				NativeArray<ResourceBuyer> nativeArray2 = chunk.GetNativeArray(ref m_BuyerType);
				NativeArray<ResourceBought> nativeArray3 = chunk.GetNativeArray(ref m_BoughtType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor(ref m_TripType);
				NativeArray<Citizen> nativeArray4 = chunk.GetNativeArray(ref m_CitizenType);
				NativeArray<AttendingMeeting> nativeArray5 = chunk.GetNativeArray(ref m_AttendingMeetingType);
				Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
				ProcessResourceBought(unfilteredChunkIndex, nativeArray3, nativeArray);
				ProcessResourceBuyer(chunk, unfilteredChunkIndex, nativeArray2, nativeArray, bufferAccessor,
					nativeArray4, random, nativeArray5);
			}

			private void ProcessResourceBought(int unfilteredChunkIndex,
				NativeArray<ResourceBought> resourceBuyingWithTargets, NativeArray<Entity> entities)
			{
				for (int i = 0; i < resourceBuyingWithTargets.Length; i++)
				{
					Entity e = entities[i];
					ResourceBought resourceBought = resourceBuyingWithTargets[i];
					if (m_PrefabRefData.HasComponent(resourceBought.m_Payer) &&
					    m_PrefabRefData.HasComponent(resourceBought.m_Seller))
					{
						SalesEvent value = new SalesEvent
						{
							m_Amount = resourceBought.m_Amount,
							m_Buyer = resourceBought.m_Payer,
							m_Seller = resourceBought.m_Seller,
							m_Resource = resourceBought.m_Resource,
							m_Flags = SaleFlags.None,
							m_Distance = resourceBought.m_Distance
						};
						m_SalesQueue.Enqueue(value);
					}

					m_CommandBuffer.RemoveComponent<ResourceBought>(unfilteredChunkIndex, e);
				}
			}

			private void ProcessResourceBuyer(ArchetypeChunk chunk, int unfilteredChunkIndex,
				NativeArray<ResourceBuyer> resourceBuyingRequests, NativeArray<Entity> entities,
				BufferAccessor<TripNeeded> tripBuffers, NativeArray<Citizen> citizens, Unity.Mathematics.Random random,
				NativeArray<AttendingMeeting> meetings)
			{
				for (int i = 0; i < resourceBuyingRequests.Length; i++)
				{
					ResourceBuyer resourceBuyer = resourceBuyingRequests[i];
					Entity entity = entities[i];
					DynamicBuffer<TripNeeded> dynamicBuffer = tripBuffers[i];
					bool flag = false;
					Entity entity2 = m_ResourcePrefabs[resourceBuyer.m_ResourceNeeded];
					if (m_ResourceDatas.HasComponent(entity2))
					{
						flag = EconomyUtils.GetWeight(resourceBuyer.m_ResourceNeeded, m_ResourcePrefabs,
							ref m_ResourceDatas) == 0f;
					}

					if (m_PathInformation.HasComponent(entity))
					{
						PathInformation pathInformation = m_PathInformation[entity];
						if ((pathInformation.m_State & PathFlags.Pending) != 0)
						{
							continue;
						}

						Entity destination = pathInformation.m_Destination;
						bool flag2 = m_OutsideConnections.HasComponent(destination);
						if (m_Properties.HasComponent(destination) || flag2)
						{
							DynamicBuffer<Game.Economy.Resources> resources = m_Resources[destination];
							int num = EconomyUtils.GetResources(resourceBuyer.m_ResourceNeeded, resources);
							if (m_StorageCompanies.HasComponent(destination))
							{
								int allBuyingResourcesTrucks = VehicleUtils.GetAllBuyingResourcesTrucks(destination,
									resourceBuyer.m_ResourceNeeded, ref m_DeliveryTrucks, ref m_GuestVehicles,
									ref m_LayoutElements);
								num -= allBuyingResourcesTrucks;
							}

							if (num <= 0 || (!flag2 && num < resourceBuyer.m_AmountNeeded / 2))
							{
								m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
								// [MOD EXT] BUG FIX: Vanilla fails to remove the ResourceBuyer component when stock is insufficient, leading to an infinite pathfinding loop.
								m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
								continue;
							}

							resourceBuyer.m_AmountNeeded = math.min(resourceBuyer.m_AmountNeeded, num);
							bool num2 = m_ServiceAvailables.HasComponent(destination);
							bool flag3 = m_StorageCompanies.HasComponent(destination);
							SaleFlags saleFlags = (num2 ? SaleFlags.CommercialSeller : SaleFlags.None);
							if (flag)
							{
								saleFlags |= SaleFlags.Virtual;
							}

							if (flag2)
							{
								saleFlags |= SaleFlags.ImportFromOC;
							}

							if (m_Households.HasComponent(resourceBuyer.m_Payer) &&
							    m_Resources.HasBuffer(resourceBuyer.m_Payer))
							{
								int num3 = math.max(0,
									EconomyUtils.GetResources(Resource.Money, m_Resources[resourceBuyer.m_Payer]) -
									HouseholdBehaviorSystem.kMinimumShoppingMoney);
								float marketPrice = EconomyUtils.GetMarketPrice(resourceBuyer.m_ResourceNeeded,
									m_ResourcePrefabs, ref m_ResourceDatas);
								float num4 = 1.4f;
								int y = (((float)num3 > 0f) ? ((int)((float)num3 / (marketPrice * num4))) : 0);
								resourceBuyer.m_AmountNeeded = math.min(resourceBuyer.m_AmountNeeded, y);
							}

							bool flag4 = resourceBuyer.m_AmountNeeded > 0;
							if (flag4)
							{
								SalesEvent value = new SalesEvent
								{
									m_Amount = resourceBuyer.m_AmountNeeded,
									m_Buyer = resourceBuyer.m_Payer,
									m_Seller = destination,
									m_Resource = resourceBuyer.m_ResourceNeeded,
									m_Flags = saleFlags,
									m_Distance = pathInformation.m_Distance
								};
								m_SalesQueue.Enqueue(value);
							}

							m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
							m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
							int population = m_Populations[m_City].m_Population;
							bool flag5 = citizens.Length > 0 && random.NextInt(100) <
								100 - Mathf.RoundToInt(100f / math.max(1f,
									math.sqrt(m_EconomyParameterData.m_TrafficReduction * (float)population * 0.1f)));
							if (!flag && !flag5 && flag4)
							{
								m_CommandBuffer.AddBuffer<CurrentTrading>(unfilteredChunkIndex, entity).Add(
									new CurrentTrading
									{
										m_TradingResource = resourceBuyer.m_ResourceNeeded,
										m_TradingResourceAmount = resourceBuyer.m_AmountNeeded,
										m_OutsideConnectionType = (m_OutsideConnections.HasComponent(destination)
											? BuildingUtils.GetOutsideConnectionType(destination, ref m_PrefabRefData,
												ref m_OutsideConnectionDatas)
											: OutsideConnectionTransferType.None),
										m_TradingStartFrameIndex = m_FrameIndex
									});
								dynamicBuffer.Add(new TripNeeded
								{
									m_TargetAgent = destination,
									m_Purpose = ((!flag3) ? Purpose.Shopping : Purpose.CompanyShopping),
									m_Data = resourceBuyer.m_AmountNeeded,
									m_Resource = resourceBuyer.m_ResourceNeeded
								});
								if (!m_Targets.HasComponent(entities[i]))
								{
									m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new Target
									{
										m_Target = destination
									});
								}
							}

							continue;
						}

						m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
						m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
						if (meetings.IsCreated)
						{
							AttendingMeeting attendingMeeting = meetings[i];
							Entity prefab = m_PrefabRefData[attendingMeeting.m_Meeting].m_Prefab;
							CoordinatedMeeting value2 = m_CoordinatedMeetings[attendingMeeting.m_Meeting];
							if (m_HaveCoordinatedMeetingDatas[prefab][value2.m_Phase].m_TravelPurpose.m_Purpose ==
							    Purpose.Shopping)
							{
								value2.m_Status = MeetingStatus.Done;
								m_CoordinatedMeetings[attendingMeeting.m_Meeting] = value2;
							}
						}
					}
					else if ((!m_HouseholdMembers.HasComponent(entity) ||
					          (!m_TouristHouseholds.HasComponent(m_HouseholdMembers[entity].m_Household) &&
					           !m_CommuterHouseholds.HasComponent(m_HouseholdMembers[entity].m_Household))) &&
					         m_CurrentBuildings.HasComponent(entity) &&
					         m_OutsideConnections.HasComponent(m_CurrentBuildings[entity].m_CurrentBuilding) &&
					         !meetings.IsCreated)
					{
						SaleFlags flags = SaleFlags.ImportFromOC;
						SalesEvent value3 = new SalesEvent
						{
							m_Amount = resourceBuyer.m_AmountNeeded,
							m_Buyer = resourceBuyer.m_Payer,
							m_Seller = m_CurrentBuildings[entity].m_CurrentBuilding,
							m_Resource = resourceBuyer.m_ResourceNeeded,
							m_Flags = flags,
							m_Distance = 0f
						};
						m_SalesQueue.Enqueue(value3);
						m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
					}
					else
					{
						// [MOD EXT] Tick 节流阀：将购物寻路请求均匀分散到多个 tick 内
						// 不移除 ResourceBuyer，保留购物意图到下一个 ResourceBuyerSystem tick (16帧)
						// 市民：每 4 tick 处理一次 (4 × 16帧 = 64帧周期)
						if (citizens.Length > 0 && ((entity.Index + (int)(m_FrameIndex / 16)) % 4 != 0))
						{
							continue;
						}
						// 企业采购节流：每 2 tick 处理一次 (2 × 16帧 = 32帧周期)
						if (citizens.Length == 0 && ((entity.Index + (int)(m_FrameIndex / 16)) % 2 != 0))
						{
							continue;
						}

						Citizen citizen = default(Citizen);
						if (citizens.Length > 0)
						{
							citizen = citizens[i];
							Entity household = m_HouseholdMembers[entity].m_Household;
							Household householdData = m_Households[household];
							DynamicBuffer<HouseholdCitizen> dynamicBuffer2 = m_HouseholdCitizens[household];
							FindShopForCitizen(ref random, chunk, unfilteredChunkIndex, entity,
								resourceBuyer.m_ResourceNeeded, resourceBuyer.m_AmountNeeded, resourceBuyer.m_Flags,
								citizen, householdData, dynamicBuffer2.Length, flag);
						}
						else
						{
							FindShopForCompany(chunk, unfilteredChunkIndex, entity, resourceBuyer.m_ResourceNeeded,
								resourceBuyer.m_AmountNeeded, resourceBuyer.m_Flags, flag);
						}
					}
				}
			}

			private void FindShopForCitizen(ref Unity.Mathematics.Random random, ArchetypeChunk chunk, int index,
				Entity buyer, Resource resource, int amount, SetupTargetFlags flags, Citizen citizenData,
				Household householdData, int householdCitizenCount, bool virtualGood)
			{
				m_CommandBuffer.AddComponent(index, buyer, in m_PathfindTypes);
				m_CommandBuffer.SetComponent(index, buyer, new PathInformation
				{
					m_State = PathFlags.Pending
				});
				CreatureData creatureData;
				PseudoRandomSeed randomSeed;
				Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks, m_EntityType,
					ref m_CreatureDataType, ref m_ResidentDataType, out creatureData, out randomSeed);
				HumanData humanData = default(HumanData);
				if (entity != Entity.Null)
				{
					humanData = m_PrefabHumanData[entity];
				}

				// [MOD EXT] 缩减购物寻路的最大代价，避免市民跨越大半个地图去找一家商店
				// 原版 kMaxPathfindCost = 17000f，对于 57km 地图上的购物行为过于宽泛
				// const float kShoppingMaxCost = 8000f;
				PathfindParameters parameters = new PathfindParameters
				{
					m_MaxSpeed = 277.77777f,
					m_WalkSpeed = humanData.m_WalkSpeed,
					m_Weights = CitizenUtils.GetPathfindWeights(citizenData, householdData, householdCitizenCount),
					m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi |
					             RouteUtils.GetPublicTransportMethods(m_TimeOfDay)),
					m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
					m_MaxCost = m_DynamicShoppingMaxCost
				};
				SetupQueueTarget origin = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = PathMethod.Pedestrian,
					m_RandomCost = 30f
				};
				SetupQueueTarget destination = new SetupQueueTarget
				{
					m_Type = SetupTargetType.ResourceSeller,
					m_Methods = PathMethod.Pedestrian,
					m_Resource = resource,
					m_Value = amount,
					m_Flags = flags,
					m_RandomCost = 30f,
					m_ActivityMask = creatureData.m_SupportedActivities
				};
				if (virtualGood)
				{
					parameters.m_PathfindFlags |= PathfindFlags.SkipPathfind;
				}

				Entity entity2 = Entity.Null;
				if (m_HouseholdMembers.TryGetComponent(buyer, out var componentData) &&
				    m_Properties.TryGetComponent(componentData.m_Household, out var componentData2))
				{
					entity2 = componentData2.m_Property;
					parameters.m_Authorization1 = componentData2.m_Property;
				}

				if (m_Workers.HasComponent(buyer))
				{
					Worker worker = m_Workers[buyer];
					if (m_Properties.HasComponent(worker.m_Workplace))
					{
						parameters.m_Authorization2 = m_Properties[worker.m_Workplace].m_Property;
					}
					else
					{
						parameters.m_Authorization2 = worker.m_Workplace;
					}
				}

				bool flag = random.NextFloat(100f) < 20f;
				if (m_CarKeepers.IsComponentEnabled(buyer))
				{
					Entity car = m_CarKeepers[buyer].m_Car;
					if (m_ParkedCarData.HasComponent(car))
					{
						PrefabRef prefabRef = m_PrefabRefData[car];
						ParkedCar parkedCar = m_ParkedCarData[car];
						CarData carData = m_PrefabCarData[prefabRef.m_Prefab];
						parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
						parameters.m_ParkingTarget = parkedCar.m_Lane;
						parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
						parameters.m_ParkingSize =
							VehicleUtils.GetParkingSize(car, ref m_PrefabRefData, ref m_ObjectGeometryData);
						parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
						if (m_PersonalCarData.TryGetComponent(car, out var componentData3) &&
						    (componentData3.m_State & PersonalCarFlags.HomeTarget) == 0)
						{
							parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
						}
					}
				}
				else if (m_BicycleOwners.IsComponentEnabled(buyer) && flag)
				{
					Entity bicycle = m_BicycleOwners[buyer].m_Bicycle;
					if (!m_PrefabRefData.TryGetComponent(bicycle, out var componentData4) &&
					    m_CurrentBuildings.TryGetComponent(buyer, out var componentData5) &&
					    componentData5.m_CurrentBuilding == entity2)
					{
						Unity.Mathematics.Random
							random2 = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
						componentData4.m_Prefab = m_PersonalCarSelectData.SelectVehiclePrefab(ref random2, 1, 0,
							avoidTrailers: true, noSlowVehicles: false, bicycle: true, out var _);
					}

					if (m_PrefabCarData.TryGetComponent(componentData4.m_Prefab, out var componentData6) &&
					    m_ObjectGeometryData.TryGetComponent(componentData4.m_Prefab, out var componentData7))
					{
						parameters.m_MaxSpeed.x = componentData6.m_MaxSpeed;
						parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData7, out var _);
						parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
						if (m_ParkedCarData.TryGetComponent(bicycle, out var componentData8))
						{
							parameters.m_ParkingTarget = componentData8.m_Lane;
							parameters.m_ParkingDelta = componentData8.m_CurvePosition;
							if (m_PersonalCarData.TryGetComponent(bicycle, out var componentData9) &&
							    (componentData9.m_State & PersonalCarFlags.HomeTarget) == 0)
							{
								parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
							}
						}
						else
						{
							origin.m_Methods |= PathMethod.Bicycle;
							origin.m_RoadTypes |= RoadTypes.Bicycle;
						}
					}
				}

				SetupQueueItem value = new SetupQueueItem(buyer, parameters, origin, destination);
				m_PathfindQueue.Enqueue(value);
			}

			private void FindShopForCompany(ArchetypeChunk chunk, int index, Entity buyer, Resource resource,
				int amount, SetupTargetFlags flags, bool virtualGood)
			{
				m_CommandBuffer.AddComponent(index, buyer, in m_PathfindTypes);
				m_CommandBuffer.SetComponent(index, buyer, new PathInformation
				{
					m_State = PathFlags.Pending
				});
				float transportCost = EconomyUtils.GetTransportCost(100f, amount,
					m_ResourceDatas[m_ResourcePrefabs[resource]].m_Weight, StorageTransferFlags.Car);
				PathfindParameters parameters = new PathfindParameters
				{
					m_MaxSpeed = 111.111115f,
					m_WalkSpeed = 5.555556f,
					m_Weights = new PathfindWeights(1f, 1f, transportCost, 1f),
					m_Methods = (PathMethod.Road | PathMethod.CargoLoading),
					m_IgnoredRules = (RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles),
					m_MaxCost = m_CompanyShoppingMaxCost // [MOD EXT] Give company trucks their own custom configurable max cost
				};
				SetupQueueTarget origin = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = (PathMethod.Road | PathMethod.CargoLoading),
					m_RoadTypes = RoadTypes.Car
				};
				SetupQueueTarget destination = new SetupQueueTarget
				{
					m_Type = SetupTargetType.ResourceSeller,
					m_Methods = (PathMethod.Road | PathMethod.CargoLoading),
					m_RoadTypes = RoadTypes.Car,
					m_Resource = resource,
					m_Value = amount,
					m_Flags = flags
				};
				if (virtualGood)
				{
					parameters.m_PathfindFlags |= PathfindFlags.SkipPathfind;
				}

				SetupQueueItem value = new SetupQueueItem(buyer, parameters, origin, destination);
				m_PathfindQueue.Enqueue(value);
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		#endregion

		#region Helpers

		private struct SalesEvent
		{
			public SaleFlags m_Flags;
			public Entity m_Buyer;

			public Entity m_Seller;
			public Resource m_Resource;

			public int m_Amount;
			public float m_Distance;
		}

		#endregion
	}
}

