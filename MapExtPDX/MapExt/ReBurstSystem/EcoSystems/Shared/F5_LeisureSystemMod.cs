using Game;
using Game.Simulation;
using Colossal.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Events;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
// ReSharper disable InlineOutVariableDeclaration

namespace MapExtPDX.EcoShared
{
	public partial class LeisureSystemMod : GameSystemBase
	{
		#region Constants

		public static readonly float kUpdateInterval = 5f;
		public static readonly int kUpdatePerDay = 4096;

		public override int GetUpdateInterval(SystemUpdatePhase phase)
			=> 262144 / kUpdatePerDay;

		#endregion

		#region Fields

		private SimulationSystem m_SimulationSystem;
		private EndFrameBarrier m_EndFrameBarrier;
		private PathfindSetupSystem m_PathFindSetupSystem;
		private TimeSystem m_TimeSystem;
		private ResourceSystem m_ResourceSystem;
		private ClimateSystem m_ClimateSystem;
		private AddMeetingSystem m_AddMeetingSystem;
		private CityProductionStatisticSystem m_CityProductionStatisticSystem;
		private CityConfigurationSystem m_CityConfigurationSystem;
		private EntityQuery m_LeisureQuery;
		private EntityQuery m_EconomyParameterQuery;
		private EntityQuery m_LeisureParameterQuery;
		private EntityQuery m_ResidentPrefabQuery;
		private EntityQuery m_TimeDataQuery;
		private EntityQuery m_PopulationQuery;
		private EntityQuery m_CarPrefabQuery;
		private ComponentTypeSet m_PathfindTypes;
		private NativeQueue<LeisureEvent> m_LeisureQueue;
		private PersonalCarSelectData m_PersonalCarSelectData;

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_PathFindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
			m_ResourceSystem = World.GetOrCreateSystemManaged<ResourceSystem>();
			m_ClimateSystem = World.GetOrCreateSystemManaged<ClimateSystem>();
			m_AddMeetingSystem = World.GetOrCreateSystemManaged<AddMeetingSystem>();
			m_CityProductionStatisticSystem = World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
			m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			m_PersonalCarSelectData = new PersonalCarSelectData(this);
			m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadOnly<EconomyParameterData>());
			m_LeisureParameterQuery = GetEntityQuery(ComponentType.ReadOnly<LeisureParametersData>());
			m_LeisureQuery = GetEntityQuery(ComponentType.ReadWrite<Citizen>(), ComponentType.ReadWrite<Leisure>(),
				ComponentType.ReadWrite<TripNeeded>(), ComponentType.ReadWrite<CurrentBuilding>(),
				ComponentType.Exclude<HealthProblem>(), ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>());
			m_ResidentPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<ObjectData>(),
				ComponentType.ReadOnly<HumanData>(), ComponentType.ReadOnly<ResidentData>(),
				ComponentType.ReadOnly<PrefabData>());
			m_TimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<TimeData>());
			m_PopulationQuery = GetEntityQuery(ComponentType.ReadOnly<Population>());
			m_CarPrefabQuery = GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());
			m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(),
				ComponentType.ReadWrite<PathElement>());
			m_LeisureQueue = new NativeQueue<LeisureEvent>(Allocator.Persistent);
			RequireForUpdate(m_LeisureQuery);
			RequireForUpdate(m_EconomyParameterQuery);
			RequireForUpdate(m_LeisureParameterQuery);
		}

		protected override void OnDestroy()
		{
			m_LeisureQueue.Dispose();
			base.OnDestroy();
		}

		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(m_SimulationSystem.frameIndex,
				(uint)GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			float value = m_ClimateSystem.precipitation.value;
			m_PersonalCarSelectData.PreUpdate(this, m_CityConfigurationSystem, m_CarPrefabQuery, Allocator.TempJob,
				out var jobHandle);
			JobHandle jobHandle2 = new LeisureJob
			{
				m_EntityType = SystemAPI.GetEntityTypeHandle(),
				m_LeisureType = SystemAPI.GetComponentTypeHandle<Leisure>(isReadOnly: false),
				m_HouseholdMemberType = SystemAPI.GetComponentTypeHandle<HouseholdMember>(isReadOnly: true),
				m_UpdateFrameType = SystemAPI.GetSharedComponentTypeHandle<UpdateFrame>(),
				m_TripType = SystemAPI.GetBufferTypeHandle<TripNeeded>(isReadOnly: false),
				m_CreatureDataType = SystemAPI.GetComponentTypeHandle<CreatureData>(isReadOnly: true),
				m_ResidentDataType = SystemAPI.GetComponentTypeHandle<ResidentData>(isReadOnly: true),
				m_PathInfos = SystemAPI.GetComponentLookup<PathInformation>(isReadOnly: true),
				m_CurrentBuildings = SystemAPI.GetComponentLookup<CurrentBuilding>(isReadOnly: true),
				m_BuildingData = SystemAPI.GetComponentLookup<Building>(isReadOnly: true),
				m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
				m_CarKeepers = SystemAPI.GetComponentLookup<CarKeeper>(isReadOnly: true),
				m_BicycleOwners = SystemAPI.GetComponentLookup<BicycleOwner>(isReadOnly: true),
				m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(isReadOnly: true),
				m_PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(isReadOnly: true),
				m_Targets = SystemAPI.GetComponentLookup<Target>(isReadOnly: true),
				m_PrefabRefs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
				m_LeisureProviderDatas = SystemAPI.GetComponentLookup<LeisureProviderData>(isReadOnly: true),
				m_Students = SystemAPI.GetComponentLookup<Game.Citizens.Student>(isReadOnly: true),
				m_Workers = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
				m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
				m_Resources = SystemAPI.GetBufferLookup<Game.Economy.Resources>(isReadOnly: true),
				m_CitizenDatas = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: false),
				m_Renters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
				m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(isReadOnly: true),
				m_ObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(isReadOnly: true),
				m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(isReadOnly: true),
				m_Purposes = SystemAPI.GetComponentLookup<TravelPurpose>(isReadOnly: true),
				m_OutsideConnectionDatas = SystemAPI.GetComponentLookup<OutsideConnectionData>(isReadOnly: true),
				m_TouristHouseholds = SystemAPI.GetComponentLookup<TouristHousehold>(isReadOnly: true),
				m_IndustrialProcesses = SystemAPI.GetComponentLookup<IndustrialProcessData>(isReadOnly: true),
				m_ServiceAvailables = SystemAPI.GetComponentLookup<ServiceAvailable>(isReadOnly: true),
				m_PopulationData = SystemAPI.GetComponentLookup<Population>(isReadOnly: true),
				m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
				m_RenterBufs = SystemAPI.GetBufferLookup<Renter>(isReadOnly: true),
				m_ConsumptionDatas = SystemAPI.GetComponentLookup<ConsumptionData>(isReadOnly: true),
				m_EconomyParameters = m_EconomyParameterQuery.GetSingleton<EconomyParameterData>(),
				// [MapExt2-MaxCost] Bind the dynamic slider setting from mod config
				m_DynamicLeisureMaxCost = Mod.Instance.Settings.LeisureMaxCost,
				m_SimulationFrame = m_SimulationSystem.frameIndex,
				m_TimeOfDay = m_TimeSystem.normalizedTime,
				m_UpdateFrameIndex = updateFrameWithInterval,
				m_Weather = value,
				m_Temperature = m_ClimateSystem.temperature,
				m_RandomSeed = RandomSeed.Next(),
				m_PathfindTypes = m_PathfindTypes,
				m_HumanChunks =
					m_ResidentPrefabQuery.ToArchetypeChunkListAsync(World.UpdateAllocator.ToAllocator,
						out var outJobHandle),
				m_PersonalCarSelectData = m_PersonalCarSelectData,
				m_PathfindQueue = m_PathFindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
				m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
				m_MeetingQueue = m_AddMeetingSystem.GetMeetingQueue(out var deps).AsParallelWriter(),
				m_LeisureQueue = m_LeisureQueue.AsParallelWriter(),
				m_TimeData = m_TimeDataQuery.GetSingleton<TimeData>(),
				m_PopulationEntity = m_PopulationQuery.GetSingletonEntity()
			}.ScheduleParallel(m_LeisureQuery, JobUtils.CombineDependencies(Dependency, outJobHandle, deps, jobHandle));
			m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
			m_PathFindSetupSystem.AddQueueWriter(jobHandle2);
			m_PersonalCarSelectData.PostUpdate(jobHandle2);
			JobHandle jobHandle3 = new SpendLeisureJob
			{
				m_ServiceAvailables = SystemAPI.GetComponentLookup<ServiceAvailable>(isReadOnly: false),
				m_CompanyStatisticDatas = SystemAPI.GetComponentLookup<CompanyStatisticData>(isReadOnly: false),
				m_Resources = SystemAPI.GetBufferLookup<Game.Economy.Resources>(isReadOnly: false),
				m_CitizenDatas = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: false),
				m_HouseholdMembers = SystemAPI.GetComponentLookup<HouseholdMember>(isReadOnly: true),
				m_IndustrialProcesses = SystemAPI.GetComponentLookup<IndustrialProcessData>(isReadOnly: true),
				m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
				m_ResourceDatas = SystemAPI.GetComponentLookup<ResourceData>(isReadOnly: true),
				m_ServiceCompanyDatas = SystemAPI.GetComponentLookup<ServiceCompanyData>(isReadOnly: true),
				m_ResourcePrefabs = m_ResourceSystem.GetPrefabs(),
				m_CitizensConsumptionAccumulator =
					m_CityProductionStatisticSystem.GetCityResourceUsageAccumulator(
						CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, out var deps2),
				m_LeisureQueue = m_LeisureQueue
			}.Schedule(JobHandle.CombineDependencies(jobHandle2, deps2));
			m_ResourceSystem.AddPrefabsReader(jobHandle3);
			m_CityProductionStatisticSystem.AddCityUsageAccumulatorWriter(
				CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, jobHandle3);
			Dependency = jobHandle3;
		}

		#endregion

		#region Jobs

		[BurstCompile]
		private struct SpendLeisureJob : IJob
		{
			public NativeQueue<LeisureEvent> m_LeisureQueue;
			public ComponentLookup<ServiceAvailable> m_ServiceAvailables;
			public ComponentLookup<CompanyStatisticData> m_CompanyStatisticDatas;
			public BufferLookup<Game.Economy.Resources> m_Resources;
			public ComponentLookup<Citizen> m_CitizenDatas;
			[ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
			[ReadOnly] public ComponentLookup<IndustrialProcessData> m_IndustrialProcesses;
			[ReadOnly] public ComponentLookup<HouseholdMember> m_HouseholdMembers;
			[ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
			[ReadOnly] public ComponentLookup<ServiceCompanyData> m_ServiceCompanyDatas;
			[ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
			public NativeArray<int> m_CitizensConsumptionAccumulator;

			public void Execute()
			{
				LeisureEvent item;
				while (m_LeisureQueue.TryDequeue(out item))
				{
					if (m_CitizenDatas.HasComponent(item.m_Citizen))
					{
						Citizen value = m_CitizenDatas[item.m_Citizen];
						int leisureAmount = (int)math.ceil(item.m_Efficiency / kUpdateInterval);
						value.m_LeisureCounter = (byte)math.min(255, value.m_LeisureCounter + leisureAmount);
						m_CitizenDatas[item.m_Citizen] = value;
					}

					if (!m_HouseholdMembers.HasComponent(item.m_Citizen) || !m_Prefabs.HasComponent(item.m_Provider))
					{
						continue;
					}

					Entity household = m_HouseholdMembers[item.m_Citizen].m_Household;
					Entity prefab = m_Prefabs[item.m_Provider].m_Prefab;
					if (!m_IndustrialProcesses.HasComponent(prefab))
					{
						continue;
					}

					Resource resource = m_IndustrialProcesses[prefab].m_Output.m_Resource;
					if (resource == Resource.NoResource || !m_Resources.HasBuffer(item.m_Provider) ||
					    !m_Resources.HasBuffer(household))
					{
						continue;
					}

					bool isDepleted = false;
					float marketPrice = EconomyUtils.GetMarketPrice(resource, m_ResourcePrefabs, ref m_ResourceDatas);
					int resourceAmount = 0;
					float priceMultiplier = 1f;
					if (m_ServiceAvailables.HasComponent(item.m_Provider) && m_ServiceCompanyDatas.HasComponent(prefab))
					{
						ServiceAvailable value2 = m_ServiceAvailables[item.m_Provider];
						ServiceCompanyData serviceCompanyData = m_ServiceCompanyDatas[prefab];
						resourceAmount = math.max((int)(serviceCompanyData.m_ServiceConsuming / kUpdateInterval), 1);
						if (value2.m_ServiceAvailable > 0)
						{
							value2.m_ServiceAvailable -= resourceAmount;
							value2.m_MeanPriority = math.lerp(value2.m_MeanPriority,
								(float)value2.m_ServiceAvailable / serviceCompanyData.m_MaxService, 0.1f);
							m_ServiceAvailables[item.m_Provider] = value2;
							priceMultiplier = EconomyUtils.GetServicePriceMultiplier(value2.m_ServiceAvailable,
								serviceCompanyData.m_MaxService);
							if (m_CompanyStatisticDatas.HasComponent(item.m_Provider))
							{
								CompanyStatisticData value3 = m_CompanyStatisticDatas[item.m_Provider];
								value3.m_CurrentNumberOfCustomers++;
								m_CompanyStatisticDatas[item.m_Provider] = value3;
							}
						}
						else
						{
							isDepleted = true;
						}
					}

					if (!isDepleted)
					{
						DynamicBuffer<Game.Economy.Resources> resources = m_Resources[item.m_Provider];
						resourceAmount = math.min(EconomyUtils.GetResources(resource, resources), resourceAmount);
						int moneyAmount = (int)(resourceAmount * marketPrice * priceMultiplier);
						DynamicBuffer<Game.Economy.Resources> resources2 = m_Resources[household];
						EconomyUtils.AddResources(resource, -resourceAmount, resources);
						EconomyUtils.AddResources(Resource.Money, Mathf.RoundToInt(moneyAmount), resources);
						EconomyUtils.AddResources(Resource.Money, -Mathf.RoundToInt(moneyAmount), resources2);
						m_CitizensConsumptionAccumulator[EconomyUtils.GetResourceIndex(resource)] += resourceAmount;
					}
				}
			}
		}

		[BurstCompile]
		private struct LeisureJob : IJobChunk
		{
			public ComponentTypeHandle<Leisure> m_LeisureType;
			[ReadOnly] public EntityTypeHandle m_EntityType;
			[ReadOnly] public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;
			[ReadOnly] public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;
			public BufferTypeHandle<TripNeeded> m_TripType;
			[ReadOnly] public ComponentTypeHandle<CreatureData> m_CreatureDataType;
			[ReadOnly] public ComponentTypeHandle<ResidentData> m_ResidentDataType;
			[ReadOnly] public ComponentLookup<PathInformation> m_PathInfos;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
			[ReadOnly] public ComponentLookup<Target> m_Targets;
			[ReadOnly] public ComponentLookup<CarKeeper> m_CarKeepers;
			[ReadOnly] public ComponentLookup<BicycleOwner> m_BicycleOwners;
			[ReadOnly] public ComponentLookup<ParkedCar> m_ParkedCarData;
			[ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;
			[ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildings;
			[ReadOnly] public ComponentLookup<Building> m_BuildingData;
			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefs;
			[ReadOnly] public ComponentLookup<LeisureProviderData> m_LeisureProviderDatas;
			[ReadOnly] public ComponentLookup<Worker> m_Workers;
			[ReadOnly] public ComponentLookup<Game.Citizens.Student> m_Students;
			[ReadOnly] public BufferLookup<Game.Economy.Resources> m_Resources;
			[ReadOnly] public ComponentLookup<Household> m_Households;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_Renters;
			[NativeDisableParallelForRestriction] public ComponentLookup<Citizen> m_CitizenDatas;
			[ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
			[ReadOnly] public ComponentLookup<CarData> m_PrefabCarData;
			[ReadOnly] public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;
			[ReadOnly] public ComponentLookup<HumanData> m_PrefabHumanData;
			[ReadOnly] public ComponentLookup<TravelPurpose> m_Purposes;
			[ReadOnly] public ComponentLookup<OutsideConnectionData> m_OutsideConnectionDatas;
			[ReadOnly] public ComponentLookup<TouristHousehold> m_TouristHouseholds;
			[ReadOnly] public ComponentLookup<IndustrialProcessData> m_IndustrialProcesses;
			[ReadOnly] public ComponentLookup<ServiceAvailable> m_ServiceAvailables;
			[ReadOnly] public ComponentLookup<Population> m_PopulationData;
			[ReadOnly] public BufferLookup<Renter> m_RenterBufs;
			[ReadOnly] public ComponentLookup<ConsumptionData> m_ConsumptionDatas;
			[ReadOnly] public RandomSeed m_RandomSeed;
			[ReadOnly] public ComponentTypeSet m_PathfindTypes;
			[ReadOnly] public NativeList<ArchetypeChunk> m_HumanChunks;
			[ReadOnly] public PersonalCarSelectData m_PersonalCarSelectData;
			public EconomyParameterData m_EconomyParameters;
			// [MapExt2-MaxCost] Storage for dynamic slider
			public float m_DynamicLeisureMaxCost;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
			public NativeQueue<LeisureEvent>.ParallelWriter m_LeisureQueue;
			public NativeQueue<AddMeetingSystem.AddMeeting>.ParallelWriter m_MeetingQueue;
			public uint m_SimulationFrame;
			public uint m_UpdateFrameIndex;
			public float m_TimeOfDay;
			public float m_Weather;
			public float m_Temperature;
			public Entity m_PopulationEntity;
			public TimeData m_TimeData;

			private void SpendLeisure(int index, Entity entity, ref Citizen citizen, ref Leisure leisure,
				Entity providerEntity, LeisureProviderData provider)
			{
				bool isInactiveOrDepleted = m_BuildingData.HasComponent(providerEntity) &&
					BuildingUtils.CheckOption(m_BuildingData[providerEntity],
						BuildingOption.Inactive) || m_ServiceAvailables.HasComponent(providerEntity) &&
					m_ServiceAvailables[providerEntity].m_ServiceAvailable <= 0;

				Entity prefab = m_PrefabRefs[providerEntity].m_Prefab;
				if (!isInactiveOrDepleted && m_IndustrialProcesses.HasComponent(prefab))
				{
					Resource resource = m_IndustrialProcesses[prefab].m_Output.m_Resource;
					if (resource != Resource.NoResource && m_Resources.HasBuffer(providerEntity) &&
					    EconomyUtils.GetResources(resource, m_Resources[providerEntity]) <= 0)
					{
						isInactiveOrDepleted = true;
					}
				}

				if (!isInactiveOrDepleted)
				{
					m_LeisureQueue.Enqueue(new LeisureEvent
					{
						m_Citizen = entity,
						m_Provider = providerEntity,
						m_Efficiency = provider.m_Efficiency
					});
				}

				if (citizen.m_LeisureCounter > 255f - provider.m_Efficiency / kUpdateInterval ||
				    m_SimulationFrame >= leisure.m_LastPossibleFrame || isInactiveOrDepleted)
				{
					m_CommandBuffer.RemoveComponent<Leisure>(index, entity);
				}
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
				{
					return;
				}

				NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
				NativeArray<Leisure> nativeArray2 = chunk.GetNativeArray(ref m_LeisureType);
				NativeArray<HouseholdMember> nativeArray3 = chunk.GetNativeArray(ref m_HouseholdMemberType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor(ref m_TripType);
				int population = m_PopulationData[m_PopulationEntity].m_Population;
				Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					Leisure leisure = nativeArray2[i];
					DynamicBuffer<TripNeeded> dynamicBuffer = bufferAccessor[i];
					Citizen citizen = m_CitizenDatas[entity];
					bool isTraveling = m_Purposes.HasComponent(entity) &&
					                   m_Purposes[entity].m_Purpose == Purpose.Traveling;
					Entity providerEntity = leisure.m_TargetAgent;
					Entity providerPrefab = Entity.Null;
					LeisureProviderData provider = default(LeisureProviderData);
					if (leisure.m_TargetAgent != Entity.Null && m_CurrentBuildings.HasComponent(entity))
					{
						Entity currentBuilding = m_CurrentBuildings[entity].m_CurrentBuilding;
						if (m_PropertyRenters.HasComponent(leisure.m_TargetAgent) &&
						    m_PropertyRenters[leisure.m_TargetAgent].m_Property == currentBuilding &&
						    m_PrefabRefs.HasComponent(leisure.m_TargetAgent))
						{
							Entity prefab = m_PrefabRefs[leisure.m_TargetAgent].m_Prefab;
							if (m_LeisureProviderDatas.HasComponent(prefab))
							{
								providerPrefab = prefab;
								provider = m_LeisureProviderDatas[providerPrefab];
							}
						}
						else if (m_PrefabRefs.HasComponent(currentBuilding))
						{
							Entity prefab2 = m_PrefabRefs[currentBuilding].m_Prefab;
							providerEntity = currentBuilding;
							if (m_LeisureProviderDatas.HasComponent(prefab2))
							{
								providerPrefab = prefab2;
								provider = m_LeisureProviderDatas[providerPrefab];
							}
							else if (isTraveling && m_OutsideConnectionDatas.HasComponent(prefab2))
							{
								providerPrefab = prefab2;
								provider = new LeisureProviderData
								{
									m_Efficiency = 20,
									m_LeisureType = LeisureType.Travel,
									m_Resources = Resource.NoResource
								};
							}
						}
					}

					if (providerPrefab != Entity.Null)
					{
						SpendLeisure(unfilteredChunkIndex, entity, ref citizen, ref leisure, providerEntity, provider);
						nativeArray2[i] = leisure;
						m_CitizenDatas[entity] = citizen;
					}
					else if (!isTraveling && m_PathInfos.HasComponent(entity))
					{
						PathInformation pathInformation = m_PathInfos[entity];
						if ((pathInformation.m_State & PathFlags.Pending) != 0)
						{
							continue;
						}

						Entity destination = pathInformation.m_Destination;
						if ((m_PropertyRenters.HasComponent(destination) || m_PrefabRefs.HasComponent(destination)) &&
						    !m_Targets.HasComponent(entity))
						{
							if ((!m_Workers.HasComponent(entity) ||
							     WorkerSystem.IsTodayOffDay(citizen, ref m_EconomyParameters, m_SimulationFrame,
								     m_TimeData, population) ||
							     !WorkerSystem.IsTimeToWork(citizen, m_Workers[entity], ref m_EconomyParameters,
								     m_TimeOfDay)) && (!m_Students.HasComponent(entity) ||
								                       StudentSystem.IsTimeToStudy(citizen, m_Students[entity],
									                       ref m_EconomyParameters, m_TimeOfDay, m_SimulationFrame,
									                       m_TimeData, population)))
							{
								Entity prefab3 = m_PrefabRefs[destination].m_Prefab;
								if (m_LeisureProviderDatas[prefab3].m_Efficiency == 0)
								{
									UnityEngine.Debug.LogWarning(
										$"Warning: Leisure provider {destination.Index} has zero efficiency");
								}

								leisure.m_TargetAgent = destination;
								nativeArray2[i] = leisure;
								dynamicBuffer.Add(new TripNeeded
								{
									m_TargetAgent = destination,
									m_Purpose = Purpose.Leisure
								});
								m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new Target
								{
									m_Target = destination
								});
							}
							else
							{
								if (m_Purposes.HasComponent(entity) &&
								    (m_Purposes[entity].m_Purpose == Purpose.Leisure ||
								     m_Purposes[entity].m_Purpose == Purpose.Traveling))
								{
									m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
								}

								m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity);
								m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
							}
						}
						else if (!m_Targets.HasComponent(entity))
						{
							if (m_Purposes.HasComponent(entity) && (m_Purposes[entity].m_Purpose == Purpose.Leisure ||
							                                        m_Purposes[entity].m_Purpose == Purpose.Traveling))
							{
								m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}

							m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity);
							m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
						}
					}
					else if (!m_Purposes.HasComponent(entity))
					{
						Entity household = nativeArray3[i].m_Household;
						FindLeisure(unfilteredChunkIndex, entity, household, citizen, ref random,
							m_TouristHouseholds.HasComponent(household));
						nativeArray2[i] = leisure;
					}
				}
			}

			private float GetWeight(LeisureType type, int wealth, CitizenAge age)
			{
				float num = 1f;
				float num2;
				float xMin;
				float num3;
				switch (type)
				{
					case LeisureType.Meals:
						num2 = 10f;
						xMin = 0.2f;
						num3 = age switch
						{
							CitizenAge.Child => 10f,
							CitizenAge.Teen => 25f,
							_ => 35f,
						};
						break;
					case LeisureType.Entertainment:
						num2 = 10f;
						xMin = 0.3f;
						num3 = age switch
						{
							CitizenAge.Child => 0f,
							CitizenAge.Elderly => 10f,
							_ => 45f,
						};
						break;
					case LeisureType.Commercial:
						num2 = 10f;
						xMin = 0.4f;
						num3 = age switch
						{
							CitizenAge.Child => 20f,
							CitizenAge.Teen => 25f,
							CitizenAge.Elderly => 25f,
							_ => 30f,
						};
						break;
					case LeisureType.CityIndoors:
					case LeisureType.CityPark:
					case LeisureType.CityBeach:
						num2 = 10f;
						xMin = 0f;
						num3 = age switch
						{
							CitizenAge.Teen => 25f,
							CitizenAge.Elderly => 15f,
							_ => 30f,
						};
						num = type switch
						{
							LeisureType.CityIndoors => 1f,
							LeisureType.CityPark => 2f * (1f - 0.95f * m_Weather),
							_ => 0.05f + 4f * math.saturate(0.35f - m_Weather) *
								math.saturate((m_Temperature - 20f) / 30f),
						};
						break;
					case LeisureType.Travel:
						num2 = 1f;
						xMin = 0.5f;
						num = 0.5f + math.saturate((30f - m_Temperature) / 50f);
						num3 = age switch
						{
							CitizenAge.Child => 15f,
							CitizenAge.Teen => 15f,
							CitizenAge.Elderly => 30f,
							_ => 40f,
						};
						break;
					default:
						num2 = 0f;
						xMin = 0f;
						num3 = 0f;
						num = 0f;
						break;
				}

				return num3 * num * num2 * math.smoothstep(xMin, 1f, (wealth + 5000f) / 10000f);
			}

			private LeisureType SelectLeisureType(Entity household, bool tourist, Citizen citizenData,
				ref Unity.Mathematics.Random random)
			{
				PropertyRenter propertyRenter =
					(m_Renters.HasComponent(household) ? m_Renters[household] : default(PropertyRenter));
				if (tourist && random.NextFloat() < 0.3f)
				{
					return LeisureType.Attractions;
				}

				if (m_Households.HasComponent(household) && m_Resources.HasBuffer(household) &&
				    m_HouseholdCitizens.HasBuffer(household))
				{
					int wealth = ((!tourist)
						? EconomyUtils.GetHouseholdSpendableMoney(m_Households[household], m_Resources[household],
							ref m_RenterBufs, ref m_ConsumptionDatas, ref m_PrefabRefs, propertyRenter)
						: EconomyUtils.GetResources(Resource.Money, m_Resources[household]));
					float num = 0f;
					CitizenAge age = citizenData.GetAge();
					for (int i = 0; i < 10; i++)
					{
						num += GetWeight((LeisureType)i, wealth, age);
					}

					float num2 = num * random.NextFloat();
					for (int j = 0; j < 10; j++)
					{
						num2 -= GetWeight((LeisureType)j, wealth, age);
						if (num2 <= 0.001f)
						{
							return (LeisureType)j;
						}
					}
				}

				UnityEngine.Debug.LogWarning("Leisure type randomization failed");
				return LeisureType.Count;
			}

			private void FindLeisure(int chunkIndex, Entity citizen, Entity household, Citizen citizenData,
				ref Unity.Mathematics.Random random, bool tourist)
			{
				LeisureType leisureType = SelectLeisureType(household, tourist, citizenData, ref random);
				float value = 255f - citizenData.m_LeisureCounter;
				if (leisureType == LeisureType.Travel || leisureType == LeisureType.Sightseeing ||
				    leisureType == LeisureType.Attractions)
				{
					if (m_Purposes.HasComponent(citizen))
					{
						m_CommandBuffer.RemoveComponent<TravelPurpose>(chunkIndex, citizen);
					}

					m_MeetingQueue.Enqueue(new AddMeetingSystem.AddMeeting
					{
						m_Household = household,
						m_Type = leisureType
					});
					return;
				}

				m_CommandBuffer.AddComponent(chunkIndex, citizen, in m_PathfindTypes);
				m_CommandBuffer.SetComponent(chunkIndex, citizen, new PathInformation
				{
					m_State = PathFlags.Pending
				});
				CreatureData creatureData;
				Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks, m_EntityType,
					ref m_CreatureDataType, ref m_ResidentDataType, out creatureData, out _);
				HumanData humanData = default(HumanData);
				if (entity != Entity.Null)
				{
					humanData = m_PrefabHumanData[entity];
				}

				Household household2 = m_Households[household];
				DynamicBuffer<HouseholdCitizen> dynamicBuffer = m_HouseholdCitizens[household];
				PathfindParameters parameters = new PathfindParameters
				{
					m_MaxSpeed = 277.77777f,
					m_WalkSpeed = humanData.m_WalkSpeed,
					m_Weights = CitizenUtils.GetPathfindWeights(citizenData, household2, dynamicBuffer.Length),
					m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi |
					             RouteUtils.GetPublicTransportMethods(m_TimeOfDay)),
					m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
					m_MaxCost = math.max(CitizenBehaviorSystem.kMaxPathfindCost, m_DynamicLeisureMaxCost)
				};
				SetupQueueTarget origin = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = PathMethod.Pedestrian,
					m_RandomCost = 30f
				};
				SetupQueueTarget destination = new SetupQueueTarget
				{
					m_Type = SetupTargetType.Leisure,
					m_Methods = PathMethod.Pedestrian,
					m_Value = (int)leisureType,
					m_Value2 = value,
					m_RandomCost = 30f,
					m_ActivityMask = creatureData.m_SupportedActivities
				};
				if (m_PropertyRenters.TryGetComponent(household, out var componentData))
				{
					parameters.m_Authorization1 = componentData.m_Property;
				}

				if (m_Workers.HasComponent(citizen))
				{
					Worker worker = m_Workers[citizen];
					parameters.m_Authorization2 = m_PropertyRenters.HasComponent(worker.m_Workplace) ? m_PropertyRenters[worker.m_Workplace].m_Property : worker.m_Workplace;
				}

				bool isBicycleProbable = random.NextFloat(100f) < 20f;
				if (m_CarKeepers.IsComponentEnabled(citizen))
				{
					Entity car = m_CarKeepers[citizen].m_Car;
					if (m_ParkedCarData.HasComponent(car))
					{
						PrefabRef prefabRef = m_PrefabRefs[car];
						ParkedCar parkedCar = m_ParkedCarData[car];
						CarData carData = m_PrefabCarData[prefabRef.m_Prefab];
						parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
						parameters.m_ParkingTarget = parkedCar.m_Lane;
						parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
						parameters.m_ParkingSize =
							VehicleUtils.GetParkingSize(car, ref m_PrefabRefs, ref m_ObjectGeometryData);
						parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
						if (m_PersonalCarData.TryGetComponent(car, out var componentData2) &&
						    (componentData2.m_State & PersonalCarFlags.HomeTarget) == 0)
						{
							parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
						}
					}
				}
				else if (m_BicycleOwners.IsComponentEnabled(citizen) && isBicycleProbable)
				{
					Entity bicycle = m_BicycleOwners[citizen].m_Bicycle;
					if (!m_PrefabRefs.TryGetComponent(bicycle, out var componentData3) &&
					    m_CurrentBuildings.TryGetComponent(citizen, out var componentData4) &&
					    componentData4.m_CurrentBuilding == componentData.m_Property)
					{
						Unity.Mathematics.Random
							random2 = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
						componentData3.m_Prefab = m_PersonalCarSelectData.SelectVehiclePrefab(ref random2, 1, 0,
							avoidTrailers: true, noSlowVehicles: false, bicycle: true, out var _);
					}

					if (m_PrefabCarData.TryGetComponent(componentData3.m_Prefab, out var componentData5) &&
					    m_ObjectGeometryData.TryGetComponent(componentData3.m_Prefab, out var componentData6))
					{
						parameters.m_MaxSpeed.x = componentData5.m_MaxSpeed;
						parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData6, out var _);
						parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
						if (m_ParkedCarData.TryGetComponent(bicycle, out var componentData7))
						{
							parameters.m_ParkingTarget = componentData7.m_Lane;
							parameters.m_ParkingDelta = componentData7.m_CurvePosition;
							if (m_PersonalCarData.TryGetComponent(bicycle, out var componentData8) &&
							    (componentData8.m_State & PersonalCarFlags.HomeTarget) == 0)
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

				SetupQueueItem value2 = new SetupQueueItem(citizen, parameters, origin, destination);
				m_PathfindQueue.Enqueue(value2);
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		#endregion

		#region Helpers

		public static void AddToTempList(NativeList<LeisureProviderData> tempProviderList,
			LeisureProviderData providerToAdd)
		{
			for (int i = 0; i < tempProviderList.Length; i++)
			{
				LeisureProviderData value = tempProviderList[i];
				if (value.m_LeisureType == providerToAdd.m_LeisureType &&
				    value.m_Resources == providerToAdd.m_Resources)
				{
					value.m_Efficiency += providerToAdd.m_Efficiency;
					tempProviderList[i] = value;
					return;
				}
			}

			tempProviderList.Add(in providerToAdd);
		}

		#endregion
	}
}
