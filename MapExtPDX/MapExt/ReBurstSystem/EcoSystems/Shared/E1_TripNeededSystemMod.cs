using Game;
using Game.Simulation;
using Colossal.Collections;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Creatures;
using Game.Debug;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.EcoShared
{
	public partial class TripNeededSystemMod : GameSystemBase
	{
		#region Constants

		private const int UPDATE_INTERVAL = 16;
		public override int GetUpdateInterval(SystemUpdatePhase phase) => UPDATE_INTERVAL;

		#endregion

		#region Fields

		private EntityQuery m_CitizenGroup;
		private EntityQuery m_ResidentPrefabGroup;
		private EntityQuery m_CompanyGroup;
		private EntityQuery m_CarPrefabQuery;
		private EntityArchetype m_HandleRequestArchetype;
		private EntityArchetype m_ResetTripArchetype;
		private ComponentTypeSet m_HumanSpawnTypes;
		private ComponentTypeSet m_PathfindTypes;
		private ComponentTypeSet m_CurrentLaneTypesRelative;
		private PersonalCarSelectData m_PersonalCarSelectData;
		private EndFrameBarrier m_EndFrameBarrier;
		private TimeSystem m_TimeSystem;
		private PathfindSetupSystem m_PathfindSetupSystem;
		private CityConfigurationSystem m_CityConfigurationSystem;
		private VehicleCapacitySystem m_VehicleCapacitySystem;
		private TriggerSystem m_TriggerSystem;

		[DebugWatchValue] private DebugWatchDistribution m_DebugPathCostsCar;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPathCostsPublic;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPathCostsPedestrian;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPathCostsCarShort;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPathCostsPublicShort;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPathCostsPedestrianShort;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPublicTransportDuration;
		[DebugWatchValue] private DebugWatchDistribution m_DebugTaxiDuration;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPedestrianDuration;
		[DebugWatchValue] private DebugWatchDistribution m_DebugCarDuration;
		[DebugWatchValue] private DebugWatchDistribution m_DebugPedestrianDurationShort;

		public bool debugDisableSpawning { get; set; }

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			m_DebugPathCostsCar = new DebugWatchDistribution(persistent: true);
			m_DebugPathCostsPublic = new DebugWatchDistribution(persistent: true);
			m_DebugPathCostsPedestrian = new DebugWatchDistribution(persistent: true);
			m_DebugPathCostsCarShort = new DebugWatchDistribution(persistent: true);
			m_DebugPathCostsPublicShort = new DebugWatchDistribution(persistent: true);
			m_DebugPathCostsPedestrianShort = new DebugWatchDistribution(persistent: true);
			m_DebugPublicTransportDuration = new DebugWatchDistribution(persistent: true);
			m_DebugTaxiDuration = new DebugWatchDistribution(persistent: true);
			m_DebugPedestrianDuration = new DebugWatchDistribution(persistent: true);
			m_DebugCarDuration = new DebugWatchDistribution(persistent: true);
			m_DebugPedestrianDurationShort = new DebugWatchDistribution(persistent: true);

			m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
			m_CityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			m_VehicleCapacitySystem = World.GetOrCreateSystemManaged<VehicleCapacitySystem>();
			m_TriggerSystem = World.GetOrCreateSystemManaged<TriggerSystem>();
			m_PersonalCarSelectData = new PersonalCarSelectData(this);
			m_CitizenGroup = SystemAPI.QueryBuilder()
				.WithAll<Citizen, HouseholdMember, TripNeeded, CurrentBuilding>()
				.WithNone<TravelPurpose, ResourceBuyer, Deleted, Temp>()
				.Build();
			m_ResidentPrefabGroup = SystemAPI.QueryBuilder()
				.WithAll<ObjectData, HumanData, ResidentData, PrefabData>()
				.Build();
			m_CompanyGroup = SystemAPI.QueryBuilder()
				.WithAll<TripNeeded, PrefabRef, Game.Economy.Resources, OwnedVehicle>()
				.WithNone<Deleted, Temp>()
				.Build();
			m_CarPrefabQuery = GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());
			m_HandleRequestArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<HandleRequest>(),
				ComponentType.ReadWrite<Game.Events.Event>());
			m_ResetTripArchetype =
				EntityManager.CreateArchetype(ComponentType.ReadWrite<Event>(), ComponentType.ReadWrite<ResetTrip>());
			m_HumanSpawnTypes = new ComponentTypeSet(ComponentType.ReadWrite<HumanCurrentLane>(),
				ComponentType.ReadWrite<TripSource>(), ComponentType.ReadWrite<Unspawned>());
			m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(),
				ComponentType.ReadWrite<PathElement>());
			m_CurrentLaneTypesRelative = new ComponentTypeSet(new[]
			{
				ComponentType.ReadWrite<Moving>(),
				ComponentType.ReadWrite<TransformFrame>(),
				ComponentType.ReadWrite<HumanNavigation>(),
				ComponentType.ReadWrite<HumanCurrentLane>(),
				ComponentType.ReadWrite<Blocker>()
			});
			m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			RequireAnyForUpdate(m_CitizenGroup, m_CompanyGroup);
		}

		protected override void OnDestroy()
		{
			m_DebugPathCostsCar.Dispose();
			m_DebugPathCostsPublic.Dispose();
			m_DebugPathCostsPedestrian.Dispose();
			m_DebugPathCostsCarShort.Dispose();
			m_DebugPathCostsPublicShort.Dispose();
			m_DebugPathCostsPedestrianShort.Dispose();
			m_DebugPublicTransportDuration.Dispose();
			m_DebugTaxiDuration.Dispose();
			m_DebugCarDuration.Dispose();
			m_DebugPedestrianDuration.Dispose();
			m_DebugPedestrianDurationShort.Dispose();
			base.OnDestroy();
		}

		protected override void OnUpdate()
		{
			NativeList<ArchetypeChunk> humanChunks =
				m_ResidentPrefabGroup.ToArchetypeChunkListAsync(Allocator.TempJob, out var outJobHandle);
			m_PersonalCarSelectData.PreUpdate(this, m_CityConfigurationSystem, m_CarPrefabQuery, Allocator.TempJob,
				out var jobHandle);
			JobHandle jobHandle2 = JobHandle.CombineDependencies(Dependency, outJobHandle, jobHandle);
			JobHandle jobHandle3 = default(JobHandle);
			if (!m_CitizenGroup.IsEmptyIgnoreFilter)
			{
				NativeQueue<AnimalTargetInfo> animalQueue = new NativeQueue<AnimalTargetInfo>(Allocator.TempJob);
				NativeQueue<Entity> leaveQueue = new NativeQueue<Entity>(Allocator.TempJob);
				NativeQueue<int> debugPathQueueCar = default(NativeQueue<int>);
				NativeQueue<int> debugPathQueuePublic = default(NativeQueue<int>);
				NativeQueue<int> debugPathQueuePedestrian = default(NativeQueue<int>);
				NativeQueue<int> debugPathQueueCarShort = default(NativeQueue<int>);
				NativeQueue<int> debugPathQueuePublicShort = default(NativeQueue<int>);
				NativeQueue<int> debugPathQueuePedestrianShort = default(NativeQueue<int>);
				NativeQueue<int> debugPublicTransportDuration = default(NativeQueue<int>);
				NativeQueue<int> debugTaxiDuration = default(NativeQueue<int>);
				NativeQueue<int> debugCarDuration = default(NativeQueue<int>);
				NativeQueue<int> debugPedestrianDuration = default(NativeQueue<int>);
				NativeQueue<int> debugPedestrianDurationShort = default(NativeQueue<int>);
				JobHandle deps;
				if (m_DebugPathCostsCar.IsEnabled)
				{
					debugPathQueueCar = m_DebugPathCostsCar.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPathCostsPublic.IsEnabled)
				{
					debugPathQueuePublic = m_DebugPathCostsPublic.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPathCostsPedestrian.IsEnabled)
				{
					debugPathQueuePedestrian = m_DebugPathCostsPedestrian.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPathCostsCarShort.IsEnabled)
				{
					debugPathQueueCarShort = m_DebugPathCostsCarShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPathCostsPublicShort.IsEnabled)
				{
					debugPathQueuePublicShort = m_DebugPathCostsPublicShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPathCostsPedestrianShort.IsEnabled)
				{
					debugPathQueuePedestrianShort = m_DebugPathCostsPedestrianShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPublicTransportDuration.IsEnabled)
				{
					debugPublicTransportDuration = m_DebugPublicTransportDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugTaxiDuration.IsEnabled)
				{
					debugTaxiDuration = m_DebugTaxiDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugCarDuration.IsEnabled)
				{
					debugCarDuration = m_DebugCarDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPedestrianDuration.IsEnabled)
				{
					debugPedestrianDuration = m_DebugPedestrianDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				if (m_DebugPedestrianDurationShort.IsEnabled)
				{
					debugPedestrianDurationShort = m_DebugPedestrianDurationShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}

				CitizenJob jobData = new CitizenJob
				{
					m_DebugPathQueueCar = debugPathQueueCar,
					m_DebugPathQueuePublic = debugPathQueuePublic,
					m_DebugPathQueuePedestrian = debugPathQueuePedestrian,
					m_DebugPathQueueCarShort = debugPathQueueCarShort,
					m_DebugPathQueuePublicShort = debugPathQueuePublicShort,
					m_DebugPathQueuePedestrianShort = debugPathQueuePedestrianShort,
					m_DebugPublicTransportDuration = debugPublicTransportDuration,
					m_DebugTaxiDuration = debugTaxiDuration,
					m_DebugCarDuration = debugCarDuration,
					m_DebugPedestrianDuration = debugPedestrianDuration,
					m_DebugPedestrianDurationShort = debugPedestrianDurationShort,
					m_EntityType = SystemAPI.GetEntityTypeHandle(),
					m_CitizenType = SystemAPI.GetComponentTypeHandle<Citizen>(isReadOnly: true),
					m_HealthProblemType = SystemAPI.GetComponentTypeHandle<HealthProblem>(isReadOnly: true),
					m_HouseholdMemberType = SystemAPI.GetComponentTypeHandle<HouseholdMember>(isReadOnly: true),
					m_MailSenderType = SystemAPI.GetComponentTypeHandle<MailSender>(isReadOnly: true),
					m_CurrentTransportType = SystemAPI.GetComponentTypeHandle<CurrentTransport>(isReadOnly: true),
					m_CurrentBuildingType = SystemAPI.GetComponentTypeHandle<CurrentBuilding>(isReadOnly: false),
					m_TripNeededType = SystemAPI.GetBufferTypeHandle<TripNeeded>(isReadOnly: false),
					m_AttendingMeetingType = SystemAPI.GetComponentTypeHandle<AttendingMeeting>(isReadOnly: true),
					m_CreatureDataType = SystemAPI.GetComponentTypeHandle<CreatureData>(isReadOnly: true),
					m_ResidentDataType = SystemAPI.GetComponentTypeHandle<ResidentData>(isReadOnly: true),
					m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(isReadOnly: true),
					m_PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(isReadOnly: true),
					m_AmbulanceData = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(isReadOnly: true),
					m_ConnectionLaneData = SystemAPI.GetComponentLookup<Game.Net.ConnectionLane>(isReadOnly: true),
					m_CurrentDistrictData = SystemAPI.GetComponentLookup<CurrentDistrict>(isReadOnly: true),
					m_DistrictModifiers = SystemAPI.GetBufferLookup<DistrictModifier>(isReadOnly: true),
					m_PathInfos = SystemAPI.GetComponentLookup<PathInformation>(isReadOnly: true),
					m_Properties = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
					m_Transforms = SystemAPI.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true),
					m_Targets = SystemAPI.GetComponentLookup<Target>(isReadOnly: true),
					m_Deleteds = SystemAPI.GetComponentLookup<Deleted>(isReadOnly: true),
					m_PathElements = SystemAPI.GetBufferLookup<PathElement>(isReadOnly: true),
					m_CarKeepers = SystemAPI.GetComponentLookup<CarKeeper>(isReadOnly: true),
					m_BicycleOwners = SystemAPI.GetComponentLookup<BicycleOwner>(isReadOnly: true),
					m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
					m_Workers = SystemAPI.GetComponentLookup<Worker>(isReadOnly: false),
					m_Students = SystemAPI.GetComponentLookup<Game.Citizens.Student>(isReadOnly: false),
					m_ObjectDatas = SystemAPI.GetComponentLookup<ObjectData>(isReadOnly: true),
					m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_ObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(isReadOnly: true),
					m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(isReadOnly: true),
					m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(isReadOnly: true),
					m_OutsideConnections =
						SystemAPI.GetComponentLookup<Game.Objects.OutsideConnection>(isReadOnly: true),
					m_UnderConstructionData = SystemAPI.GetComponentLookup<UnderConstruction>(isReadOnly: true),
					m_Meetings = SystemAPI.GetComponentLookup<CoordinatedMeeting>(isReadOnly: false),
					m_Attendees = SystemAPI.GetBufferLookup<CoordinatedMeetingAttendee>(isReadOnly: true),
					m_HouseholdAnimals = SystemAPI.GetBufferLookup<HouseholdAnimal>(isReadOnly: true),
					m_TravelPurposes = SystemAPI.GetComponentLookup<TravelPurpose>(isReadOnly: true),
					m_HaveCoordinatedMeetingDatas =
						SystemAPI.GetBufferLookup<HaveCoordinatedMeetingData>(isReadOnly: true),
					m_Households = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
					m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
					m_CriminalData = SystemAPI.GetComponentLookup<Criminal>(isReadOnly: true),
					m_HumanChunks = humanChunks,
					m_RandomSeed = RandomSeed.Next(),
					m_TimeOfDay = m_TimeSystem.normalizedTime,
					m_ResetTripArchetype = m_ResetTripArchetype,
					m_HumanSpawnTypes = m_HumanSpawnTypes,
					m_PathfindTypes = m_PathfindTypes,
					m_PersonalCarSelectData = m_PersonalCarSelectData,
					m_PathQueue = m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter(),
					m_AnimalQueue = animalQueue.AsParallelWriter(),
					m_LeaveQueue = leaveQueue.AsParallelWriter(),
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
					m_TriggerBuffer = m_TriggerSystem.CreateActionBuffer().AsParallelWriter(),
					m_DebugDisableSpawning = debugDisableSpawning,
					// [MOD EXT]
					m_DynamicLeisureMaxCost = Mod.Instance.Settings.LeisureMaxCost,
					m_DynamicShoppingMaxCost = Mod.Instance.Settings.ShoppingMaxCost,
					m_DynamicEmergencyMaxCost = Mod.Instance.Settings.EmergencyMaxCost
				};
				PetTargetJob jobData2 = new PetTargetJob
				{
					m_CurrentBuildingData = SystemAPI.GetComponentLookup<CurrentBuilding>(isReadOnly: true),
					m_AnimalQueue = animalQueue,
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
				};
				CitizeLeaveJob jobData3 = new CitizeLeaveJob
				{
					m_CurrentBuildingData = SystemAPI.GetComponentLookup<CurrentBuilding>(isReadOnly: true),
					m_CitizenPresenceData = SystemAPI.GetComponentLookup<CitizenPresence>(isReadOnly: false),
					m_LeaveQueue = leaveQueue
				};
				jobHandle2 = jobData.ScheduleParallel(m_CitizenGroup, jobHandle2);
				JobHandle jobHandle4 = jobData2.Schedule(jobHandle2);
				JobHandle jobHandle5 = jobData3.Schedule(jobHandle2);
				jobHandle3 = JobHandle.CombineDependencies(jobHandle4, jobHandle5);
				animalQueue.Dispose(jobHandle4);
				leaveQueue.Dispose(jobHandle5);
				m_PathfindSetupSystem.AddQueueWriter(jobHandle2);
				m_TriggerSystem.AddActionBufferWriter(jobHandle2);
				m_EndFrameBarrier.AddJobHandleForProducer(jobHandle3);
			}

			m_PersonalCarSelectData.PostUpdate(jobHandle2);
			if (!m_CompanyGroup.IsEmptyIgnoreFilter)
			{
				jobHandle2 = new CompanyJob
				{
					m_EntityType = SystemAPI.GetEntityTypeHandle(),
					m_PropertyRenterType = SystemAPI.GetComponentTypeHandle<PropertyRenter>(isReadOnly: true),
					m_CreatureDataType = SystemAPI.GetComponentTypeHandle<CreatureData>(isReadOnly: true),
					m_ResidentDataType = SystemAPI.GetComponentTypeHandle<ResidentData>(isReadOnly: true),
					m_PrefabType = SystemAPI.GetComponentTypeHandle<PrefabRef>(isReadOnly: true),
					m_TripNeededType = SystemAPI.GetBufferTypeHandle<TripNeeded>(isReadOnly: false),
					m_VehicleType = SystemAPI.GetBufferTypeHandle<OwnedVehicle>(isReadOnly: true),
					m_ResourceType = SystemAPI.GetBufferTypeHandle<Game.Economy.Resources>(isReadOnly: false),
					m_PrefabDeliveryTruckData = SystemAPI.GetComponentLookup<DeliveryTruckData>(isReadOnly: true),
					m_PrefabObjectData = SystemAPI.GetComponentLookup<ObjectData>(isReadOnly: true),
					m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
					m_Transforms = SystemAPI.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true),
					m_TransportCompanyDatas = SystemAPI.GetComponentLookup<TransportCompanyData>(isReadOnly: true),
					m_ServiceRequestData = SystemAPI.GetComponentLookup<ServiceRequest>(isReadOnly: true),
					m_PathInformationData = SystemAPI.GetComponentLookup<PathInformation>(isReadOnly: true),
					m_UnderConstructionData = SystemAPI.GetComponentLookup<UnderConstruction>(isReadOnly: true),
					m_PropertyRenterData = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
					m_PathElements = SystemAPI.GetBufferLookup<PathElement>(isReadOnly: true),
					m_ActivityLocationElements = SystemAPI.GetBufferLookup<ActivityLocationElement>(isReadOnly: true),
					m_EfficiencyBufs = SystemAPI.GetBufferLookup<Efficiency>(isReadOnly: true),
					m_InstalledUpgradeBufs = SystemAPI.GetBufferLookup<InstalledUpgrade>(isReadOnly: true),
					m_HumanChunks = humanChunks,
					m_LeftHandTraffic = m_CityConfigurationSystem.leftHandTraffic,
					m_RandomSeed = RandomSeed.Next(),
					m_HandleRequestArchetype = m_HandleRequestArchetype,
					m_DeliveryTruckSelectData = m_VehicleCapacitySystem.GetDeliveryTruckSelectData(),
					m_CurrentLaneTypesRelative = m_CurrentLaneTypesRelative,
					m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
					m_DebugDisableSpawning = debugDisableSpawning
				}.ScheduleParallel(m_CompanyGroup, jobHandle2);
				m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
				jobHandle3 = JobHandle.CombineDependencies(jobHandle3, jobHandle2);
			}

			humanChunks.Dispose(jobHandle2);
			Dependency = jobHandle3;
		}

		#endregion

		#region Jobs

		[BurstCompile]
		private struct CompanyJob : IJobChunk
		{
			[ReadOnly] public EntityTypeHandle m_EntityType;
			[ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
			public BufferTypeHandle<TripNeeded> m_TripNeededType;
			[ReadOnly] public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;
			[ReadOnly] public ComponentTypeHandle<CreatureData> m_CreatureDataType;
			[ReadOnly] public ComponentTypeHandle<ResidentData> m_ResidentDataType;
			[ReadOnly] public BufferTypeHandle<OwnedVehicle> m_VehicleType;
			public BufferTypeHandle<Game.Economy.Resources> m_ResourceType;
			[ReadOnly] public ComponentLookup<TransportCompanyData> m_TransportCompanyDatas;
			[ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
			[ReadOnly] public ComponentLookup<DeliveryTruckData> m_PrefabDeliveryTruckData;
			[ReadOnly] public ComponentLookup<ObjectData> m_PrefabObjectData;
			[ReadOnly] public ComponentLookup<Game.Objects.Transform> m_Transforms;
			[ReadOnly] public ComponentLookup<ServiceRequest> m_ServiceRequestData;
			[ReadOnly] public ComponentLookup<PathInformation> m_PathInformationData;
			[ReadOnly] public ComponentLookup<UnderConstruction> m_UnderConstructionData;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenterData;
			[ReadOnly] public BufferLookup<PathElement> m_PathElements;
			[ReadOnly] public BufferLookup<ActivityLocationElement> m_ActivityLocationElements;
			[ReadOnly] public BufferLookup<Efficiency> m_EfficiencyBufs;
			[ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgradeBufs;
			[ReadOnly] public NativeList<ArchetypeChunk> m_HumanChunks;
			[ReadOnly] public bool m_LeftHandTraffic;
			[ReadOnly] public RandomSeed m_RandomSeed;
			[ReadOnly] public EntityArchetype m_HandleRequestArchetype;
			[ReadOnly] public DeliveryTruckSelectData m_DeliveryTruckSelectData;
			[ReadOnly] public ComponentTypeSet m_CurrentLaneTypesRelative;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
			public bool m_DebugDisableSpawning;

			private void SpawnDeliveryTruck(int index, Entity owner, Entity from, ref Game.Objects.Transform transform,
				TripNeeded trip)
			{
				Entity entity;
				Entity entity2;
				if (m_ServiceRequestData.HasComponent(trip.m_TargetAgent))
				{
					if (!m_PathInformationData.TryGetComponent(trip.m_TargetAgent, out var componentData))
					{
						return;
					}

					entity = componentData.m_Destination;
					entity2 = trip.m_TargetAgent;
				}
				else
				{
					entity = trip.m_TargetAgent;
					entity2 = Entity.Null;
				}

				if (!m_Prefabs.HasComponent(entity))
				{
					return;
				}

				Entity entity3 = entity;
				if (m_PropertyRenterData.TryGetComponent(entity3, out var componentData2))
				{
					entity3 = componentData2.m_Property;
				}

				uint delayFrames = 0u;
				if (m_UnderConstructionData.TryGetComponent(entity3, out var componentData3) &&
				    componentData3.m_NewPrefab == Entity.Null)
				{
					m_PathInformationData.TryGetComponent(entity2, out var componentData4);
					delayFrames = ObjectUtils.GetTripDelayFrames(componentData3, componentData4);
				}

				if (m_UnderConstructionData.TryGetComponent(from, out componentData3) &&
				    componentData3.m_NewPrefab == Entity.Null)
				{
					delayFrames = math.max(delayFrames, ObjectUtils.GetRemainingConstructionFrames(componentData3));
				}

				Unity.Mathematics.Random random = m_RandomSeed.GetRandom(owner.Index);
				DeliveryTruckFlags deliveryTruckFlags = 0;
				Resource resource = trip.m_Resource;
				Resource resource2 = Resource.NoResource;
				int amount = math.abs(trip.m_Data);
				int returnAmount = 0;
				switch (trip.m_Purpose)
				{
					case Purpose.Exporting:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded;
						break;
					case Purpose.Delivery:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded | DeliveryTruckFlags.Delivering;
						break;
					case Purpose.UpkeepDelivery:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded | DeliveryTruckFlags.Delivering |
						                      DeliveryTruckFlags.UpkeepDelivery;
						break;
					case Purpose.Collect:
						deliveryTruckFlags |= DeliveryTruckFlags.Buying;
						break;
					case Purpose.Shopping:
						deliveryTruckFlags |= DeliveryTruckFlags.Buying;
						break;
					case Purpose.CompanyShopping:
						deliveryTruckFlags |= DeliveryTruckFlags.Buying | DeliveryTruckFlags.UpdateSellerQuantity;
						break;
					case Purpose.StorageTransfer:
						deliveryTruckFlags = ((trip.m_Data <= 0)
							? (deliveryTruckFlags | (DeliveryTruckFlags.Buying | DeliveryTruckFlags.StorageTransfer))
							: (deliveryTruckFlags | (DeliveryTruckFlags.Loaded | DeliveryTruckFlags.StorageTransfer)));
						break;
					case Purpose.ReturnUnsortedMail:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded;
						resource2 = Resource.UnsortedMail;
						returnAmount = amount;
						amount = math.select(amount, 0, trip.m_Resource == Resource.NoResource);
						break;
					case Purpose.ReturnLocalMail:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded;
						resource2 = Resource.LocalMail;
						returnAmount = amount;
						amount = math.select(amount, 0, trip.m_Resource == Resource.NoResource);
						break;
					case Purpose.ReturnOutgoingMail:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded;
						resource2 = Resource.OutgoingMail;
						returnAmount = amount;
						amount = math.select(amount, 0, trip.m_Resource == Resource.NoResource);
						break;
					case Purpose.ReturnGarbage:
						deliveryTruckFlags |= DeliveryTruckFlags.Loaded;
						resource2 = Resource.Garbage;
						returnAmount = amount;
						amount = math.select(amount, 0, trip.m_Resource == Resource.NoResource);
						break;
				}

				if (amount > 0)
				{
					deliveryTruckFlags |= DeliveryTruckFlags.UpdateOwnerQuantity;
				}

				Resource resources = (Resource)((long)resource | (long)resource2);
				int capacity = math.max(amount, returnAmount);
				if (!m_DeliveryTruckSelectData.TrySelectItem(ref random, resources, capacity, out var item))
				{
					return;
				}

				Entity entity4 = m_DeliveryTruckSelectData.CreateVehicle(m_CommandBuffer, index, ref random,
					ref m_PrefabDeliveryTruckData, ref m_PrefabObjectData, item, resource, resource2, ref amount,
					ref returnAmount, transform, from, deliveryTruckFlags, delayFrames);
				int maxCount = 1;
				if (CreatePassengers(index, entity4, item.m_Prefab1, transform, driver: true, ref maxCount,
					    ref random) > 0)
				{
					m_CommandBuffer.AddBuffer<Passenger>(index, entity4);
				}

				m_CommandBuffer.SetComponent(index, entity4, new Target(entity));
				m_CommandBuffer.AddComponent(index, entity4, new Owner(owner));
				if (!(entity2 != Entity.Null))
				{
					return;
				}

				Entity e = m_CommandBuffer.CreateEntity(index, m_HandleRequestArchetype);
				m_CommandBuffer.SetComponent(index, e, new HandleRequest(entity2, entity4, completed: true));
				if (m_PathElements.HasBuffer(entity2))
				{
					DynamicBuffer<PathElement> sourceElements = m_PathElements[entity2];
					if (sourceElements.Length != 0)
					{
						DynamicBuffer<PathElement> targetElements =
							m_CommandBuffer.SetBuffer<PathElement>(index, entity4);
						PathUtils.CopyPath(sourceElements, default(PathOwner), 0, targetElements);
						m_CommandBuffer.SetComponent(index, entity4, new PathOwner(PathFlags.Updated));
						m_CommandBuffer.SetComponent(index, entity4, m_PathInformationData[entity2]);
					}
				}
			}

			private int CreatePassengers(int jobIndex, Entity vehicleEntity, Entity vehiclePrefab,
				Game.Objects.Transform transform, bool driver, ref int maxCount, ref Unity.Mathematics.Random random)
			{
				int passengerCount = 0;
				if (maxCount > 0 && m_ActivityLocationElements.TryGetBuffer(vehiclePrefab, out var bufferData))
				{
					ActivityMask activityMask = new ActivityMask(ActivityType.Driving);
					activityMask.m_Mask |= new ActivityMask(ActivityType.Biking).m_Mask;
					int validLocations = 0;
					int driverLocationIndex = -1;
					float maxZPosition = float.MinValue;
					for (int i = 0; i < bufferData.Length; i++)
					{
						ActivityLocationElement activityLocationElement = bufferData[i];
						if ((activityLocationElement.m_ActivityMask.m_Mask & activityMask.m_Mask) != 0)
						{
							validLocations++;
							bool test =
								((activityLocationElement.m_ActivityFlags & ActivityFlags.InvertLefthandTraffic) != 0 &&
								 m_LeftHandTraffic) ||
								((activityLocationElement.m_ActivityFlags & ActivityFlags.InvertRighthandTraffic) !=
									0 && !m_LeftHandTraffic);
							activityLocationElement.m_Position.x = math.select(activityLocationElement.m_Position.x,
								0f - activityLocationElement.m_Position.x, test);
							if ((!(math.abs(activityLocationElement.m_Position.x) >= 0.5f) ||
							     activityLocationElement.m_Position.x >= 0f == m_LeftHandTraffic) &&
							    activityLocationElement.m_Position.z > maxZPosition)
							{
								driverLocationIndex = i;
								maxZPosition = activityLocationElement.m_Position.z;
							}
						}
					}

					int probability = 100;
					if (driver && driverLocationIndex != -1)
					{
						maxCount--;
						validLocations--;
					}

					if (validLocations > maxCount && validLocations > 0)
					{
						probability = maxCount * 100 / validLocations;
					}

					Relative component = default(Relative);
					for (int j = 0; j < bufferData.Length; j++)
					{
						ActivityLocationElement activityLocationElement2 = bufferData[j];
						if ((activityLocationElement2.m_ActivityMask.m_Mask & activityMask.m_Mask) != 0 &&
						    ((driver && j == driverLocationIndex) || random.NextInt(100) >= probability))
						{
							component.m_Position = activityLocationElement2.m_Position;
							component.m_Rotation = activityLocationElement2.m_Rotation;
							component.m_BoneIndex = new int3(0, -1, -1);
							Citizen citizenData = default(Citizen);
							if (random.NextBool())
							{
								citizenData.m_State |= CitizenFlags.Male;
							}

							if (driver)
							{
								citizenData.SetAge(CitizenAge.Adult);
							}
							else
							{
								citizenData.SetAge((CitizenAge)random.NextInt(4));
							}

							citizenData.m_PseudoRandom = (ushort)(random.NextUInt() % 65536);
							Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks,
								m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out _, out PseudoRandomSeed randomSeed);
							ObjectData objectData = m_PrefabObjectData[entity];
							PrefabRef component2 = new PrefabRef
							{
								m_Prefab = entity
							};
							Game.Creatures.Resident component3 = default(Game.Creatures.Resident);
							component3.m_Flags |= ResidentFlags.InVehicle | ResidentFlags.DummyTraffic;
							CurrentVehicle component4 = new CurrentVehicle
							{
								m_Vehicle = vehicleEntity
							};
							component4.m_Flags |= CreatureVehicleFlags.Ready;
							if (driver && j == driverLocationIndex)
							{
								component4.m_Flags |= CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver;
							}

							Entity e = m_CommandBuffer.CreateEntity(jobIndex, objectData.m_Archetype);
							m_CommandBuffer.RemoveComponent(jobIndex, e, in m_CurrentLaneTypesRelative);
							m_CommandBuffer.SetComponent(jobIndex, e, transform);
							m_CommandBuffer.SetComponent(jobIndex, e, component2);
							m_CommandBuffer.SetComponent(jobIndex, e, component3);
							m_CommandBuffer.SetComponent(jobIndex, e, randomSeed);
							m_CommandBuffer.AddComponent(jobIndex, e, component4);
							m_CommandBuffer.AddComponent(jobIndex, e, component);
							passengerCount++;
						}
					}
				}

				return passengerCount;
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
				NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabType);
				BufferAccessor<OwnedVehicle> bufferAccessor = chunk.GetBufferAccessor(ref m_VehicleType);
				BufferAccessor<TripNeeded> bufferAccessor2 = chunk.GetBufferAccessor(ref m_TripNeededType);
				BufferAccessor<Game.Economy.Resources> bufferAccessor3 = chunk.GetBufferAccessor(ref m_ResourceType);
				NativeArray<PropertyRenter> nativeArray3 = chunk.GetNativeArray(ref m_PropertyRenterType);
				for (int i = 0; i < chunk.Count; i++)
				{
					Entity entity = nativeArray[i];
					Entity prefab = nativeArray2[i].m_Prefab;
					DynamicBuffer<TripNeeded> dynamicBuffer = bufferAccessor2[i];
					if (m_TransportCompanyDatas.HasComponent(prefab))
					{
						int transportCompanyAvailableVehicles =
							VehicleUtils.GetTransportCompanyAvailableVehicles(entity, ref m_EfficiencyBufs,
								ref m_Prefabs, ref m_TransportCompanyDatas, ref m_InstalledUpgradeBufs);
						if (transportCompanyAvailableVehicles == 0)
						{
							dynamicBuffer.Clear();
						}

						if (bufferAccessor[i].Length >= transportCompanyAvailableVehicles)
						{
							continue;
						}
					}

					if (dynamicBuffer.Length <= 0)
					{
						continue;
					}

					TripNeeded trip = dynamicBuffer[0];
					dynamicBuffer.RemoveAt(0);
					if (!m_DebugDisableSpawning)
					{
						_ = bufferAccessor3[i];
						Entity entity2 = ((!chunk.Has(ref m_PropertyRenterType)) ? entity : nativeArray3[i].m_Property);
						if (m_Transforms.HasComponent(entity2))
						{
							Game.Objects.Transform transform = m_Transforms[entity2];
							SpawnDeliveryTruck(unfilteredChunkIndex, entity, entity2, ref transform, trip);
						}
					}
				}
			}
		}

		[BurstCompile]
		private struct PetTargetJob : IJob
		{
			[ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;
			public NativeQueue<AnimalTargetInfo> m_AnimalQueue;
			public EntityCommandBuffer m_CommandBuffer;

			public void Execute()
			{
				int count = m_AnimalQueue.Count;
				if (count == 0)
				{
					return;
				}

				NativeParallelHashSet<Entity> nativeParallelHashSet =
					new NativeParallelHashSet<Entity>(count, Allocator.Temp);
				for (int i = 0; i < count; i++)
				{
					AnimalTargetInfo animalTargetInfo = m_AnimalQueue.Dequeue();
					if (m_CurrentBuildingData.HasComponent(animalTargetInfo.m_Animal) &&
					    !(m_CurrentBuildingData[animalTargetInfo.m_Animal].m_CurrentBuilding !=
					      animalTargetInfo.m_Source) && nativeParallelHashSet.Add(animalTargetInfo.m_Animal))
					{
						// 1.5.7f: 传送模式直接设置 CurrentBuilding，否则发起寻路
						if (animalTargetInfo.m_Teleport)
						{
							m_CommandBuffer.SetComponent(animalTargetInfo.m_Animal, new CurrentBuilding
							{
								m_CurrentBuilding = animalTargetInfo.m_Target
							});
						}
						else
						{
							m_CommandBuffer.AddComponent(animalTargetInfo.m_Animal, new Target(animalTargetInfo.m_Target));
						}
					}
				}

				nativeParallelHashSet.Dispose();
			}
		}

		[BurstCompile]
		private struct CitizeLeaveJob : IJob
		{
			[ReadOnly] public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;
			public ComponentLookup<CitizenPresence> m_CitizenPresenceData;
			public NativeQueue<Entity> m_LeaveQueue;

			public void Execute()
			{
				Entity item;
				while (m_LeaveQueue.TryDequeue(out item))
				{
					if (m_CurrentBuildingData.HasComponent(item))
					{
						CurrentBuilding currentBuilding = m_CurrentBuildingData[item];
						if (m_CitizenPresenceData.HasComponent(currentBuilding.m_CurrentBuilding))
						{
							CitizenPresence value = m_CitizenPresenceData[currentBuilding.m_CurrentBuilding];
							value.m_Delta = (sbyte)math.max(-127, value.m_Delta - 1);
							m_CitizenPresenceData[currentBuilding.m_CurrentBuilding] = value;
						}
					}
				}
			}
		}

		[BurstCompile]
		private struct CitizenJob : IJobChunk
		{
			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPathQueueCar;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPathQueuePublic;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPathQueuePedestrian;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPathQueueCarShort;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPathQueuePublicShort;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPathQueuePedestrianShort;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPublicTransportDuration;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugTaxiDuration;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugCarDuration;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPedestrianDuration;

			[NativeDisableContainerSafetyRestriction]
			public NativeQueue<int> m_DebugPedestrianDurationShort;

			[ReadOnly] public EntityTypeHandle m_EntityType;
			public BufferTypeHandle<TripNeeded> m_TripNeededType;
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;
			[ReadOnly] public ComponentTypeHandle<CurrentTransport> m_CurrentTransportType;
			[ReadOnly] public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;
			[ReadOnly] public ComponentTypeHandle<MailSender> m_MailSenderType;
			[ReadOnly] public ComponentTypeHandle<Citizen> m_CitizenType;
			[ReadOnly] public ComponentTypeHandle<HealthProblem> m_HealthProblemType;
			[ReadOnly] public ComponentTypeHandle<AttendingMeeting> m_AttendingMeetingType;
			[ReadOnly] public ComponentTypeHandle<CreatureData> m_CreatureDataType;
			[ReadOnly] public ComponentTypeHandle<ResidentData> m_ResidentDataType;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_Properties;
			[ReadOnly] public ComponentLookup<Game.Objects.Transform> m_Transforms;
			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;
			[ReadOnly] public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;
			[ReadOnly] public ComponentLookup<ObjectData> m_ObjectDatas;
			[ReadOnly] public ComponentLookup<CarData> m_PrefabCarData;
			[ReadOnly] public ComponentLookup<HumanData> m_PrefabHumanData;
			[ReadOnly] public ComponentLookup<PathInformation> m_PathInfos;
			[ReadOnly] public ComponentLookup<ParkedCar> m_ParkedCarData;
			[ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;
			[ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceData;
			[ReadOnly] public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;
			[ReadOnly] public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;
			[ReadOnly] public BufferLookup<DistrictModifier> m_DistrictModifiers;
			[ReadOnly] public ComponentLookup<Target> m_Targets;
			[ReadOnly] public ComponentLookup<Deleted> m_Deleteds;
			[ReadOnly] public BufferLookup<PathElement> m_PathElements;
			[ReadOnly] public ComponentLookup<CarKeeper> m_CarKeepers;
			[ReadOnly] public ComponentLookup<BicycleOwner> m_BicycleOwners;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
			[ReadOnly] public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;
			[ReadOnly] public ComponentLookup<UnderConstruction> m_UnderConstructionData;
			[ReadOnly] public BufferLookup<CoordinatedMeetingAttendee> m_Attendees;
			[ReadOnly] public BufferLookup<HouseholdAnimal> m_HouseholdAnimals;
			[ReadOnly] public ComponentLookup<TravelPurpose> m_TravelPurposes;
			[ReadOnly] public BufferLookup<HaveCoordinatedMeetingData> m_HaveCoordinatedMeetingDatas;
			[ReadOnly] public ComponentLookup<Household> m_Households;
			[ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
			[ReadOnly] public ComponentLookup<Criminal> m_CriminalData;
			[NativeDisableParallelForRestriction] public ComponentLookup<CoordinatedMeeting> m_Meetings;
			[NativeDisableParallelForRestriction] public ComponentLookup<Worker> m_Workers;
			[NativeDisableParallelForRestriction] public ComponentLookup<Game.Citizens.Student> m_Students;
			[ReadOnly] public NativeList<ArchetypeChunk> m_HumanChunks;
			[ReadOnly] public RandomSeed m_RandomSeed;
			[ReadOnly] public float m_TimeOfDay;
			[ReadOnly] public EntityArchetype m_ResetTripArchetype;
			[ReadOnly] public ComponentTypeSet m_HumanSpawnTypes;
			[ReadOnly] public ComponentTypeSet m_PathfindTypes;
			[ReadOnly] public PersonalCarSelectData m_PersonalCarSelectData;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathQueue;
			public NativeQueue<AnimalTargetInfo>.ParallelWriter m_AnimalQueue;
			public NativeQueue<Entity>.ParallelWriter m_LeaveQueue;
			public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;

			public bool m_DebugDisableSpawning;

			// [MOD EXT]
			public float m_DynamicLeisureMaxCost;
			public float m_DynamicShoppingMaxCost;
			public float m_DynamicEmergencyMaxCost;

			private void GetResidentFlags(Entity citizen, Entity currentBuilding, bool isMailSender, bool pathFailed,
				ref Target target, ref Purpose purpose, ref Purpose divertPurpose, ref uint timer,
				ref bool hasDivertPath)
			{
				if (pathFailed)
				{
					divertPurpose = Purpose.PathFailed;
					return;
				}

				switch (purpose)
				{
					case Purpose.Safety:
					case Purpose.Escape:
						target.m_Target = currentBuilding;
						divertPurpose = purpose;
						purpose = m_TravelPurposes.HasComponent(citizen) ? m_TravelPurposes[citizen].m_Purpose : Purpose.None;

						timer = 0u;
						hasDivertPath = true;
						break;
					case Purpose.Hospital:
						if (m_AmbulanceData.HasComponent(target.m_Target))
						{
							timer = 0u;
						}

						break;
					case Purpose.Deathcare:
						timer = 0u;
						break;
					default:
						if (isMailSender)
						{
							divertPurpose = Purpose.SendMail;
						}

						break;
				}
			}

			private Entity SpawnResident(int index, Entity citizen, Entity fromBuilding, Citizen citizenData,
				Target target, ResidentFlags flags, Purpose divertPurpose, uint timer, bool hasDivertPath, bool isDead,
				bool isCarried)
			{
				Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks, m_EntityType,
					ref m_CreatureDataType, ref m_ResidentDataType, out _, out PseudoRandomSeed randomSeed);
				ObjectData objectData = m_ObjectDatas[entity];
				PrefabRef component = new PrefabRef
				{
					m_Prefab = entity
				};
				Game.Objects.Transform transform = ((!m_Transforms.HasComponent(fromBuilding))
					? new Game.Objects.Transform
					{
						m_Position = default(float3),
						m_Rotation = new quaternion(0f, 0f, 0f, 1f)
					}
					: m_Transforms[fromBuilding]);
				Game.Creatures.Resident component2 = new Game.Creatures.Resident
				{
					m_Citizen = citizen,
					m_Flags = flags
				};
				Human component3 = default(Human);
				if (isDead)
				{
					component3.m_Flags |= HumanFlags.Dead;
				}

				if (isCarried)
				{
					component3.m_Flags |= HumanFlags.Carried;
				}

				PathOwner component4 = new PathOwner(PathFlags.Updated);
				TripSource component5 = new TripSource(fromBuilding, timer);
				Entity entity2 = m_CommandBuffer.CreateEntity(index, objectData.m_Archetype);
				Entity entity3 = Entity.Null;
				HumanCurrentLane component6 = default(HumanCurrentLane);
				if (m_PathElements.TryGetBuffer(citizen, out var bufferData) && bufferData.Length > 0)
				{
					PathElement pathElement = bufferData[0];
					CreatureLaneFlags creatureLaneFlags = 0u;
					if ((pathElement.m_Flags & PathElementFlags.Secondary) != 0)
					{
						Unity.Mathematics.Random random = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
						entity3 = m_PersonalCarSelectData.CreateVehicle(m_CommandBuffer, index, ref random, 1, 0,
							avoidTrailers: true, noSlowVehicles: false, bicycle: true, transform, fromBuilding, citizen,
							PersonalCarFlags.Boarding, stopped: false);
						if (entity3 != Entity.Null)
						{
							DynamicBuffer<PathElement> targetElements =
								m_CommandBuffer.SetBuffer<PathElement>(index, entity2);
							PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements);
							Game.Vehicles.CarLaneFlags carLaneFlags = Game.Vehicles.CarLaneFlags.EndOfPath |
							                                          Game.Vehicles.CarLaneFlags.EndReached |
							                                          Game.Vehicles.CarLaneFlags.FixedLane;
							if (m_ConnectionLaneData.TryGetComponent(pathElement.m_Target, out var componentData))
							{
								carLaneFlags = (((componentData.m_Flags & ConnectionLaneFlags.Area) == 0)
									? (carLaneFlags | Game.Vehicles.CarLaneFlags.Connection)
									: (carLaneFlags | Game.Vehicles.CarLaneFlags.Area));
							}

							m_CommandBuffer.SetComponent(index, entity3, new CarCurrentLane(pathElement, carLaneFlags));
							m_CommandBuffer.SetComponent(index, citizen, new BicycleOwner
							{
								m_Bicycle = entity3
							});
							component2.m_Flags |= ResidentFlags.InVehicle;
							creatureLaneFlags |= CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached;
						}
					}

					DynamicBuffer<PathElement> targetElements2 = m_CommandBuffer.SetBuffer<PathElement>(index, entity2);
					PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements2);
					component6 = new HumanCurrentLane(pathElement, creatureLaneFlags);
					component4.m_State |= PathFlags.Updated;
				}

				m_CommandBuffer.AddComponent(index, entity2, in m_HumanSpawnTypes);
				if (divertPurpose != Purpose.None)
				{
					if (hasDivertPath)
					{
						component4.m_State |= PathFlags.CachedObsolete;
					}
					else
					{
						component4.m_State |= PathFlags.DivertObsolete;
					}

					m_CommandBuffer.AddComponent(index, entity2, new Divert
					{
						m_Purpose = divertPurpose
					});
				}

				m_CommandBuffer.SetComponent(index, entity2, transform);
				m_CommandBuffer.SetComponent(index, entity2, component);
				m_CommandBuffer.SetComponent(index, entity2, target);
				m_CommandBuffer.SetComponent(index, entity2, component2);
				m_CommandBuffer.SetComponent(index, entity2, component3);
				m_CommandBuffer.SetComponent(index, entity2, component4);
				m_CommandBuffer.SetComponent(index, entity2, randomSeed);
				m_CommandBuffer.SetComponent(index, entity2, component6);
				m_CommandBuffer.SetComponent(index, entity2, component5);
				if (entity3 != Entity.Null)
				{
					m_CommandBuffer.RemoveComponent<TripSource>(index, entity2);
					m_CommandBuffer.AddComponent(index, entity2,
						new CurrentVehicle(entity3,
							CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver | CreatureVehicleFlags.Entering));
				}

				return entity2;
			}

			private void ResetTrip(int index, Entity creature, Entity citizen, Entity fromBuilding, Target target,
				ResidentFlags flags, Purpose divertPurpose, uint timer, bool hasDivertPath)
			{
				Entity e = m_CommandBuffer.CreateEntity(index, m_ResetTripArchetype);
				m_CommandBuffer.SetComponent(index, e, new ResetTrip
				{
					m_Creature = creature,
					m_Source = fromBuilding,
					m_Target = target.m_Target,
					m_ResidentFlags = flags,
					m_DivertPurpose = divertPurpose,
					m_Delay = timer,
					m_HasDivertPath = hasDivertPath
				});
				if (m_PathElements.TryGetBuffer(citizen, out var bufferData) && bufferData.Length > 0)
				{
					DynamicBuffer<PathElement> targetElements = m_CommandBuffer.AddBuffer<PathElement>(index, e);
					PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements);
				}
			}

			private void RemoveAllTrips(DynamicBuffer<TripNeeded> trips)
			{
				if (trips.Length <= 0)
				{
					return;
				}

				Purpose purpose = trips[0].m_Purpose;
				for (int i = trips.Length - 1; i >= 0; i--)
				{
					if (trips[i].m_Purpose == purpose)
					{
						trips.RemoveAt(i);
					}
				}
			}

			private Entity FindDistrict(Entity building)
			{
				if (m_CurrentDistrictData.HasComponent(building))
				{
					return m_CurrentDistrictData[building].m_District;
				}

				return Entity.Null;
			}

			private void AddPetTargets(Entity household, Entity source, Entity target, bool teleport = false)
			{
				if (m_HouseholdAnimals.HasBuffer(household))
				{
					DynamicBuffer<HouseholdAnimal> dynamicBuffer = m_HouseholdAnimals[household];
					for (int i = 0; i < dynamicBuffer.Length; i++)
					{
						HouseholdAnimal householdAnimal = dynamicBuffer[i];
						m_AnimalQueue.Enqueue(new AnimalTargetInfo
						{
							m_Animal = householdAnimal.m_HouseholdPet,
							m_Source = source,
							m_Target = target,
							m_Teleport = teleport
						});
					}
				}
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask,
				in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor(ref m_TripNeededType);
				NativeArray<HouseholdMember> nativeArray2 = chunk.GetNativeArray(ref m_HouseholdMemberType);
				NativeArray<CurrentBuilding> nativeArray3 = chunk.GetNativeArray(ref m_CurrentBuildingType);
				NativeArray<CurrentTransport> nativeArray4 = chunk.GetNativeArray(ref m_CurrentTransportType);
				NativeArray<Citizen> nativeArray5 = chunk.GetNativeArray(ref m_CitizenType);
				NativeArray<HealthProblem> nativeArray6 = chunk.GetNativeArray(ref m_HealthProblemType);
				NativeArray<AttendingMeeting> nativeArray7 = chunk.GetNativeArray(ref m_AttendingMeetingType);
				Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					DynamicBuffer<TripNeeded> trips = bufferAccessor[i];
					Entity household = nativeArray2[i].m_Household;
					Entity currentBuilding = nativeArray3[i].m_CurrentBuilding;
					if (trips.Length <= 0)
					{
						continue;
					}

					bool isMovingAway = trips[0].m_Purpose == Purpose.MovingAway;
					bool isSafetyOrEscape = trips[0].m_Purpose == Purpose.Safety || trips[0].m_Purpose == Purpose.Escape;
					bool isMailSender = chunk.IsComponentEnabled(ref m_MailSenderType, i);
					bool isDead = false;
					bool isCarried = false;
					PathInformation componentData;
					bool hasPathInfo = m_PathInfos.TryGetComponent(entity, out componentData);
					Criminal componentData2;
					bool isArrested = m_CriminalData.TryGetComponent(entity, out componentData2) &&
					                      (componentData2.m_Flags & (CriminalFlags.Prisoner | CriminalFlags.Arrested |
					                                                 CriminalFlags.Sentenced)) != 0;
					if (nativeArray6.Length != 0 && !isArrested)
					{
						HealthProblem healthProblem = nativeArray6[i];
						if ((healthProblem.m_Flags & (HealthProblemFlags.Dead | HealthProblemFlags.RequireTransport |
						                              HealthProblemFlags.InDanger | HealthProblemFlags.Trapped)) !=
						    HealthProblemFlags.None)
						{
							isDead = (healthProblem.m_Flags & HealthProblemFlags.Dead) != 0;
							isCarried = (healthProblem.m_Flags & HealthProblemFlags.RequireTransport) != 0;
							if (!(isDead || isCarried))
							{
								if (hasPathInfo)
								{
									m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
								}

								continue;
							}

							while (trips.Length > 0 && trips[0].m_Purpose != Purpose.Deathcare &&
							       trips[0].m_Purpose != Purpose.Hospital)
							{
								trips.RemoveAt(0);
							}

							if (trips.Length == 0)
							{
								if (hasPathInfo)
								{
									m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
								}

								continue;
							}
						}
					}

					if (!isMovingAway && nativeArray7.Length != 0 && !isArrested)
					{
						Entity meeting = nativeArray7[i].m_Meeting;
						if (m_PrefabRefData.HasComponent(meeting))
						{
							Entity prefab = m_PrefabRefData[meeting].m_Prefab;
							CoordinatedMeeting coordinatedMeeting = m_Meetings[meeting];
							if (m_HaveCoordinatedMeetingDatas.HasBuffer(prefab))
							{
								DynamicBuffer<HaveCoordinatedMeetingData> dynamicBuffer =
									m_HaveCoordinatedMeetingDatas[prefab];
								if (dynamicBuffer.Length > coordinatedMeeting.m_Phase)
								{
									HaveCoordinatedMeetingData haveCoordinatedMeetingData =
										dynamicBuffer[coordinatedMeeting.m_Phase];
									while (trips.Length > 0 && trips[0].m_Purpose !=
									       haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose)
									{
										trips.RemoveAt(0);
									}

									if (trips.Length == 0)
									{
										if (hasPathInfo)
										{
											m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity,
												in m_PathfindTypes);
										}

										continue;
									}
								}
							}
						}
					}

					if ((nativeArray5[i].m_State & CitizenFlags.MovingAwayReachOC) != CitizenFlags.None)
					{
						if (hasPathInfo)
						{
							m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
						}

						continue;
					}

					if (hasPathInfo)
					{
						if ((componentData.m_State & PathFlags.Pending) != 0)
						{
							continue;
						}

						if ((((componentData.m_Origin != Entity.Null &&
						       componentData.m_Origin == componentData.m_Destination) ||
						      nativeArray3[i].m_CurrentBuilding == componentData.m_Destination) && !isSafetyOrEscape) ||
						    !m_Targets.HasComponent(entity))
						{
							m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
							RemoveAllTrips(trips);
							continue;
						}
					}

					if (m_DebugDisableSpawning)
					{
						continue;
					}

					if (m_Targets.HasComponent(entity))
					{
						Target target = m_Targets[entity];
						if (target.m_Target == Entity.Null)
						{
							if (!hasPathInfo)
							{
								m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
								continue;
							}

							Entity destination = componentData.m_Destination;
							if (destination == Entity.Null)
							{
								m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
								RemoveAllTrips(trips);
								m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
								continue;
							}

							target.m_Target = destination;
						}

						Entity entity2 = target.m_Target;
						if (m_Properties.TryGetComponent(entity2, out var componentData3))
						{
							entity2 = componentData3.m_Property;
						}

						if (currentBuilding == entity2 && !isSafetyOrEscape)
						{
							m_CommandBuffer.SetComponentEnabled<Arrived>(unfilteredChunkIndex, entity, value: true);
							m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new TravelPurpose
							{
								m_Data = trips[0].m_Data,
								m_Purpose = trips[0].m_Purpose,
								m_Resource = trips[0].m_Resource
							});
							m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
							if (hasPathInfo)
							{
								m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
							}

							RemoveAllTrips(trips);
							continue;
						}

						bool needsTransport = (isDead && trips[0].m_Purpose == Purpose.Deathcare) ||
						                      (isCarried && trips[0].m_Purpose == Purpose.Hospital);
						if (!hasPathInfo && !needsTransport)
						{
							m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
							m_CommandBuffer.SetComponent(unfilteredChunkIndex, entity, new PathInformation
							{
								m_State = PathFlags.Pending
							});
							Citizen citizen = nativeArray5[i];
							CreatureData creatureData;
							Entity entity3 = ObjectEmergeSystem.SelectResidentPrefab(citizen, m_HumanChunks,
								m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out creatureData,
								out _);
							HumanData humanData = default(HumanData);
							if (entity3 != Entity.Null)
							{
								humanData = m_PrefabHumanData[entity3];
							}

							Household household2 = m_Households[household];
							DynamicBuffer<HouseholdCitizen> dynamicBuffer2 = m_HouseholdCitizens[household];

							// [MOD EXT] 按出行目的分级设置最大寻路成本
							TripNeeded tripInfo = trips[0];
							float dynamicMaxCost;
							if (isMovingAway)
							{
								// 搬家离城：使用超大范围确保能到达外部连接
								dynamicMaxCost = CitizenBehaviorSystem.kMaxMovingAwayCost;
							}
							else if (tripInfo.m_Purpose == Purpose.GoingHome ||
							         tripInfo.m_Purpose == Purpose.GoingToWork ||
							         tripInfo.m_Purpose == Purpose.GoingToSchool)
							{
								// 刚需出行（回家/上班/上学）：使用全图可达范围，防止大地图远郊市民回不了家
								dynamicMaxCost = CitizenBehaviorSystem.kMaxMovingAwayCost;
							}
							else if (tripInfo.m_Purpose == Purpose.Shopping ||
							         tripInfo.m_Purpose == Purpose.CompanyShopping)
							{
								// 购物出行：与 E3 保持一致，使用用户可调的购物滑块
								dynamicMaxCost = m_DynamicShoppingMaxCost;
							}
							else if (tripInfo.m_Purpose == Purpose.Sightseeing ||
							         tripInfo.m_Purpose == Purpose.VisitAttractions ||
							         tripInfo.m_Purpose == Purpose.Leisure ||
							         tripInfo.m_Purpose == Purpose.Relaxing)
							{
								// 休闲观光出行：使用用户可调的休闲滑块
								dynamicMaxCost = m_DynamicLeisureMaxCost;
							}
							else
							{
								// 其他一般出行：保持原版默认
								dynamicMaxCost = CitizenBehaviorSystem.kMaxPathfindCost;
							}

							PathfindParameters parameters = new PathfindParameters
							{
								m_MaxSpeed = 277.77777f,
								m_WalkSpeed = humanData.m_WalkSpeed,
								m_Weights = CitizenUtils.GetPathfindWeights(citizen, household2, dynamicBuffer2.Length),
								m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi |
								             RouteUtils.GetPublicTransportMethods(m_TimeOfDay)),
								m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
								m_MaxCost = dynamicMaxCost
							};
							SetupQueueTarget origin = new SetupQueueTarget
							{
								m_Type = SetupTargetType.CurrentLocation,
								m_Methods = PathMethod.Pedestrian,
								m_RandomCost = 30f
							};
							SetupQueueTarget destination2 = new SetupQueueTarget
							{
								m_Type = SetupTargetType.CurrentLocation,
								m_Methods = PathMethod.Pedestrian,
								m_Entity = target.m_Target,
								m_RandomCost = 30f,
								m_ActivityMask = creatureData.m_SupportedActivities
							};
							if (m_PropertyRenters.TryGetComponent(household, out var componentData4))
							{
								parameters.m_Authorization1 = componentData4.m_Property;
							}

							if (m_Workers.HasComponent(entity))
							{
								Worker worker = m_Workers[entity];
								parameters.m_Authorization2 = m_PropertyRenters.HasComponent(worker.m_Workplace) ? m_PropertyRenters[worker.m_Workplace].m_Property : worker.m_Workplace;
							}

							float bikeProbability = 20f;
							if (m_CurrentDistrictData.TryGetComponent(componentData4.m_Property, out var districtData) && m_DistrictModifiers.TryGetBuffer(districtData.m_District, out var districtModifiers))
							{
								AreaUtils.ApplyModifier(ref bikeProbability, districtModifiers, DistrictModifierType.BikeProbability);
							}
							bool useBicycle = random.NextFloat(100f) < bikeProbability;
							if (m_CarKeepers.IsComponentEnabled(entity))
							{
								Entity car = m_CarKeepers[entity].m_Car;
								if (m_ParkedCarData.HasComponent(car))
								{
									PrefabRef prefabRef = m_PrefabRefData[car];
									ParkedCar parkedCar = m_ParkedCarData[car];
									CarData carData = m_PrefabCarData[prefabRef.m_Prefab];
									parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
									parameters.m_ParkingTarget = parkedCar.m_Lane;
									parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
									parameters.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref m_PrefabRefData,
										ref m_ObjectGeometryData);
									parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
									parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
									if (m_PersonalCarData.TryGetComponent(car, out var componentData5) &&
									    (componentData5.m_State & PersonalCarFlags.HomeTarget) == 0)
									{
										parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
									}
								}
							}
							else if (m_BicycleOwners.IsComponentEnabled(entity) && useBicycle)
							{
								Entity bicycle = m_BicycleOwners[entity].m_Bicycle;
								if (!m_PrefabRefData.TryGetComponent(bicycle, out var componentData6) &&
								    currentBuilding == componentData4.m_Property)
								{
									Unity.Mathematics.Random random2 =
										citizen.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
									componentData6.m_Prefab = m_PersonalCarSelectData.SelectVehiclePrefab(ref random2,
										1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true,
										out _);
								}

								if (m_PrefabCarData.TryGetComponent(componentData6.m_Prefab, out var componentData7) &&
								    m_ObjectGeometryData.TryGetComponent(componentData6.m_Prefab,
									    out var componentData8))
								{
									parameters.m_MaxSpeed.x = componentData7.m_MaxSpeed;
									parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData8, out _);
									parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
									parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
									CurrentTransport value;
									if (m_ParkedCarData.TryGetComponent(bicycle, out var componentData9))
									{
										parameters.m_ParkingTarget = componentData9.m_Lane;
										parameters.m_ParkingDelta = componentData9.m_CurvePosition;
										if (m_PersonalCarData.TryGetComponent(bicycle, out var componentData10) &&
										    (componentData10.m_State & PersonalCarFlags.HomeTarget) == 0)
										{
											parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
										}
									}
									else if (!CollectionUtils.TryGet(nativeArray4, i, out value) ||
									         !m_PrefabRefData.HasComponent(value.m_CurrentTransport) ||
									         m_Deleteds.HasComponent(value.m_CurrentTransport))
									{
										origin.m_Methods |= PathMethod.Bicycle;
										origin.m_RoadTypes |= RoadTypes.Bicycle;
									}

									if (entity2 == componentData4.m_Property)
									{
										destination2.m_Methods |= PathMethod.Bicycle;
										destination2.m_RoadTypes |= RoadTypes.Bicycle;
									}
								}
							}

							SetupQueueItem value2 = new SetupQueueItem(entity, parameters, origin, destination2);
							m_PathQueue.Enqueue(value2);
							continue;
						}

						DynamicBuffer<PathElement> dynamicBuffer3 = default(DynamicBuffer<PathElement>);
						if (!needsTransport)
						{
							dynamicBuffer3 = m_PathElements[entity];
						}

						TripNeeded tripNeeded = trips[0];
						if ((!needsTransport && dynamicBuffer3.Length > 0) ||
						    m_PrefabRefData.HasComponent(tripNeeded.m_TargetAgent))
						{
							Entity currentBuilding2 = nativeArray3[i].m_CurrentBuilding;
							Entity entity4 = Entity.Null;
							PropertyRenter componentData11;
							bool isRenter = m_PropertyRenters.TryGetComponent(household, out componentData11);
							if (!needsTransport && isRenter && currentBuilding2.Equals(componentData11.m_Property))
							{
								if (componentData.m_Destination != Entity.Null)
								{
									if ((componentData.m_Methods & (PathMethod.PublicTransportDay | PathMethod.Taxi |
									                                PathMethod.PublicTransportNight)) != 0)
									{
										if (m_DebugPathQueuePublic.IsCreated)
										{
											m_DebugPathQueuePublic.Enqueue(Mathf.RoundToInt(componentData.m_TotalCost));
										}

										if ((componentData.m_Methods & PathMethod.Taxi) != 0)
										{
											if (m_DebugTaxiDuration.IsCreated)
											{
												m_DebugTaxiDuration.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
											}
										}
										else if (m_DebugPublicTransportDuration.IsCreated)
										{
											m_DebugPublicTransportDuration.Enqueue(
												Mathf.RoundToInt(componentData.m_Duration));
										}
									}
									else if ((componentData.m_Methods & (PathMethod.Road | PathMethod.MediumRoad)) != 0)
									{
										if (m_DebugPathQueueCar.IsCreated)
										{
											m_DebugPathQueueCar.Enqueue(Mathf.RoundToInt(componentData.m_TotalCost));
										}

										if (m_DebugCarDuration.IsCreated)
										{
											m_DebugCarDuration.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
										}
									}
									else if ((componentData.m_Methods & PathMethod.Pedestrian) != 0)
									{
										if (componentData.m_Distance > 3000f)
										{
											if (m_DebugPathQueuePedestrian.IsCreated)
											{
												m_DebugPathQueuePedestrian.Enqueue(
													Mathf.RoundToInt(componentData.m_TotalCost));
											}

											if (m_DebugPedestrianDuration.IsCreated)
											{
												m_DebugPedestrianDuration.Enqueue(
													Mathf.RoundToInt(componentData.m_Duration));
											}
										}
										else
										{
											if (m_DebugPathQueuePedestrianShort.IsCreated)
											{
												m_DebugPathQueuePedestrianShort.Enqueue(
													Mathf.RoundToInt(componentData.m_TotalCost));
											}

											if (m_DebugPedestrianDurationShort.IsCreated)
											{
												m_DebugPedestrianDurationShort.Enqueue(
													Mathf.RoundToInt(componentData.m_Duration));
											}
										}
									}
								}

								if (tripNeeded.m_Purpose == Purpose.GoingToWork && m_Workers.HasComponent(entity))
								{
									Worker value3 = m_Workers[entity];
									if (componentData.m_Destination == Entity.Null)
									{
										m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, entity);
										m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenBecameUnemployed,
											Entity.Null, entity, value3.m_Workplace));
									}
									else
									{
										value3.m_LastCommuteTime = componentData.m_Duration;
										m_Workers[entity] = value3;
									}
								}
								else if (tripNeeded.m_Purpose == Purpose.GoingToSchool &&
								         m_Students.HasComponent(entity))
								{
									if (componentData.m_Destination == Entity.Null)
									{
										m_CommandBuffer.AddComponent<StudentsRemoved>(unfilteredChunkIndex,
											m_Students[entity].m_School);
										m_CommandBuffer.RemoveComponent<Game.Citizens.Student>(unfilteredChunkIndex,
											entity);
									}
									else
									{
										Game.Citizens.Student value4 = m_Students[entity];
										value4.m_LastCommuteTime = componentData.m_Duration;
										m_Students[entity] = value4;
									}
								}
							}

							ResidentFlags residentFlags = ResidentFlags.None;
							if (nativeArray7.Length > 0)
							{
								Entity meeting2 = nativeArray7[i].m_Meeting;
								if (m_PrefabRefData.HasComponent(meeting2))
								{
									CoordinatedMeeting value5 = m_Meetings[meeting2];
									DynamicBuffer<HaveCoordinatedMeetingData> dynamicBuffer4 =
										m_HaveCoordinatedMeetingDatas[m_PrefabRefData[meeting2].m_Prefab];
									if (value5.m_Status == MeetingStatus.Done)
									{
										continue;
									}

									HaveCoordinatedMeetingData haveCoordinatedMeetingData2 =
										dynamicBuffer4[value5.m_Phase];
									if (tripNeeded.m_Purpose == haveCoordinatedMeetingData2.m_TravelPurpose.m_Purpose &&
									    (haveCoordinatedMeetingData2.m_TravelPurpose.m_Resource ==
									     Resource.NoResource ||
									     haveCoordinatedMeetingData2.m_TravelPurpose.m_Resource ==
									     tripNeeded.m_Resource) && value5.m_Target == Entity.Null)
									{
										DynamicBuffer<CoordinatedMeetingAttendee>
											dynamicBuffer5 = m_Attendees[meeting2];
										if (dynamicBuffer5.Length <= 0 || !(dynamicBuffer5[0].m_Attendee == entity))
										{
											continue;
										}

										value5.m_Target = target.m_Target;
										m_Meetings[meeting2] = value5;
										residentFlags |= ResidentFlags.PreferredLeader;
									}
								}
							}

							if (m_Workers.HasComponent(entity))
							{
								Worker worker2 = m_Workers[entity];
								entity4 = ((!m_PropertyRenters.HasComponent(worker2.m_Workplace))
									? worker2.m_Workplace
									: m_PropertyRenters[worker2.m_Workplace].m_Property);
							}

							if (currentBuilding2.Equals(componentData11.m_Property) || currentBuilding2.Equals(entity4))
							{
								m_LeaveQueue.Enqueue(entity);
							}

							Entity entity5 = Entity.Null;
							if (nativeArray4.Length != 0)
							{
								entity5 = nativeArray4[i].m_CurrentTransport;
							}

							uint timer = 512u;
							Purpose divertPurpose = Purpose.None;
							bool pathFailed = !needsTransport && dynamicBuffer3.Length == 0;
							bool hasDivertPath = false;
							GetResidentFlags(entity, currentBuilding2, isMailSender, pathFailed, ref target,
								ref tripNeeded.m_Purpose, ref divertPurpose, ref timer, ref hasDivertPath);
							if (m_UnderConstructionData.TryGetComponent(entity2, out var componentData12) &&
							    componentData12.m_NewPrefab == Entity.Null)
							{
								timer = math.max(timer, ObjectUtils.GetTripDelayFrames(componentData12, componentData));
							}

							if (m_PrefabRefData.HasComponent(entity5) && !m_Deleteds.HasComponent(entity5))
							{
								ResetTrip(unfilteredChunkIndex, entity5, entity, currentBuilding, target, residentFlags,
									divertPurpose, timer, hasDivertPath);
							}
							else
							{
								Citizen citizenData = nativeArray5[i];
								entity5 = SpawnResident(unfilteredChunkIndex, entity, currentBuilding, citizenData,
									target, residentFlags, divertPurpose, timer, hasDivertPath, isDead, needsTransport);
								m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity,
									new CurrentTransport(entity5));
							}

							if ((tripNeeded.m_Purpose != Purpose.GoingToWork &&
							     tripNeeded.m_Purpose != Purpose.GoingToSchool) ||
							    currentBuilding != componentData11.m_Property)
							{
								AddPetTargets(household, currentBuilding, target.m_Target);
							}

							m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new TravelPurpose
							{
								m_Data = tripNeeded.m_Data,
								m_Purpose = tripNeeded.m_Purpose,
								m_Resource = tripNeeded.m_Resource
							});
							m_CommandBuffer.RemoveComponent<CurrentBuilding>(unfilteredChunkIndex, entity);
						}
						else if ((m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0)
						{
							CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, household,
								MoveAwayReason.TripNeedNotMovedIn);
						}

						RemoveAllTrips(trips);
						m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
						m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
					}
					else
					{
						if (hasPathInfo || m_HumanChunks.Length == 0)
						{
							continue;
						}

						if (!m_Transforms.HasComponent(currentBuilding))
						{
							RemoveAllTrips(trips);
						}
						else if (trips[0].m_TargetAgent != Entity.Null)
						{
							m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new Target
							{
								m_Target = trips[0].m_TargetAgent
							});
						}
						else if (PathUtils.IsPathfindingPurpose(trips[0].m_Purpose))
						{
							Citizen citizen2 = nativeArray5[i];
							if (trips[0].m_Purpose == Purpose.GoingHome)
							{
								if ((citizen2.m_State & CitizenFlags.Commuter) == 0)
								{
									RemoveAllTrips(trips);
									continue;
								}

								if (m_OutsideConnections.HasComponent(nativeArray3[i].m_CurrentBuilding))
								{
									RemoveAllTrips(trips);
									continue;
								}
							}

							m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, in m_PathfindTypes);
							m_CommandBuffer.SetComponent(unfilteredChunkIndex, entity, new PathInformation
							{
								m_State = PathFlags.Pending
							});
							CreatureData creatureData2;
							Entity entity6 = ObjectEmergeSystem.SelectResidentPrefab(citizen2, m_HumanChunks,
								m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out creatureData2,
								out _);
							HumanData humanData2 = default(HumanData);
							if (entity6 != Entity.Null)
							{
								humanData2 = m_PrefabHumanData[entity6];
							}

							Household household3 = m_Households[household];
							DynamicBuffer<HouseholdCitizen> dynamicBuffer6 = m_HouseholdCitizens[household];
							// [MOD] 分支B：按 Purpose 动态分级 MaxCost
							float dynamicMaxCost;
							switch (trips[0].m_Purpose)
							{
								case Purpose.GoingHome:
									// 通勤者回外部连接：必须全范围
									dynamicMaxCost = CitizenBehaviorSystem.kMaxMovingAwayCost;
									break;
								case Purpose.Sightseeing:
								case Purpose.VisitAttractions:
									// 休闲观光：复用用户可调滑块
									dynamicMaxCost = m_DynamicLeisureMaxCost;
									break;
								case Purpose.Hospital:
								case Purpose.Crime:
									// 就近服务/就近犯罪：用户可调独立滑块
									dynamicMaxCost = m_DynamicEmergencyMaxCost;
									break;
								default:
									// Safety / Escape / EmergencyShelter / 其他：保持原版默认
									dynamicMaxCost = CitizenBehaviorSystem.kMaxPathfindCost;
									break;
							}

							PathfindParameters parameters2 = new PathfindParameters
							{
								m_MaxSpeed = 277.77777f,
								m_WalkSpeed = humanData2.m_WalkSpeed,
								m_Weights =
									CitizenUtils.GetPathfindWeights(citizen2, household3, dynamicBuffer6.Length),
								m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi |
								             RouteUtils.GetPublicTransportMethods(m_TimeOfDay)),
								m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
								m_MaxCost = dynamicMaxCost
							};
							SetupQueueTarget origin2 = new SetupQueueTarget
							{
								m_Type = SetupTargetType.CurrentLocation,
								m_Methods = PathMethod.Pedestrian,
								m_RandomCost = 30f
							};
							SetupQueueTarget destination3 = new SetupQueueTarget
							{
								m_Methods = PathMethod.Pedestrian,
								m_RandomCost = 30f,
								m_ActivityMask = creatureData2.m_SupportedActivities
							};
							switch (trips[0].m_Purpose)
							{
								case Purpose.GoingHome:
									destination3.m_Type = SetupTargetType.OutsideConnection;
									break;
								case Purpose.Hospital:
									destination3.m_Entity = FindDistrict(currentBuilding);
									destination3.m_Type = SetupTargetType.Hospital;
									break;
								case Purpose.Safety:
								case Purpose.Escape:
									destination3.m_Type = SetupTargetType.Safety;
									break;
								case Purpose.EmergencyShelter:
									parameters2.m_Weights = new PathfindWeights(1f, 0f, 0f, 0f);
									destination3.m_Entity = FindDistrict(currentBuilding);
									destination3.m_Type = SetupTargetType.EmergencyShelter;
									break;
								case Purpose.Crime:
									destination3.m_Type = SetupTargetType.CrimeProducer;
									break;
								case Purpose.Sightseeing:
									destination3.m_Type = SetupTargetType.Sightseeing;
									break;
								case Purpose.VisitAttractions:
									destination3.m_Type = SetupTargetType.Attraction;
									break;
							}

							if (m_PropertyRenters.TryGetComponent(household, out var componentData13))
							{
								parameters2.m_Authorization1 = componentData13.m_Property;
							}

							if (m_Workers.HasComponent(entity))
							{
								Worker worker3 = m_Workers[entity];
								parameters2.m_Authorization2 = m_PropertyRenters.HasComponent(worker3.m_Workplace) ? m_PropertyRenters[worker3.m_Workplace].m_Property : worker3.m_Workplace;
							}

							float bikeProbability2 = 20f;
							if (m_CurrentDistrictData.TryGetComponent(componentData13.m_Property, out var districtData2) && m_DistrictModifiers.TryGetBuffer(districtData2.m_District, out var districtModifiers2))
							{
								AreaUtils.ApplyModifier(ref bikeProbability2, districtModifiers2, DistrictModifierType.BikeProbability);
							}
							bool useBicycle = random.NextFloat(100f) < bikeProbability2;
							if (m_CarKeepers.IsComponentEnabled(entity))
							{
								Entity car2 = m_CarKeepers[entity].m_Car;
								if (m_ParkedCarData.HasComponent(car2))
								{
									PrefabRef prefabRef2 = m_PrefabRefData[car2];
									ParkedCar parkedCar2 = m_ParkedCarData[car2];
									CarData carData2 = m_PrefabCarData[prefabRef2.m_Prefab];
									parameters2.m_MaxSpeed.x = carData2.m_MaxSpeed;
									parameters2.m_ParkingTarget = parkedCar2.m_Lane;
									parameters2.m_ParkingDelta = parkedCar2.m_CurvePosition;
									parameters2.m_ParkingSize = VehicleUtils.GetParkingSize(car2, ref m_PrefabRefData,
										ref m_ObjectGeometryData);
									parameters2.m_Methods |= VehicleUtils.GetPathMethods(carData2) | PathMethod.Parking;
									parameters2.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData2);
									if (m_PersonalCarData.TryGetComponent(car2, out var componentData14) &&
									    (componentData14.m_State & PersonalCarFlags.HomeTarget) == 0)
									{
										parameters2.m_PathfindFlags |= PathfindFlags.ParkingReset;
									}
								}
							}
							else if (m_BicycleOwners.IsComponentEnabled(entity) && useBicycle)
							{
								Entity bicycle2 = m_BicycleOwners[entity].m_Bicycle;
								if (!m_PrefabRefData.TryGetComponent(bicycle2, out var componentData15) &&
								    currentBuilding == componentData13.m_Property)
								{
									Unity.Mathematics.Random random3 =
										citizen2.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
									componentData15.m_Prefab = m_PersonalCarSelectData.SelectVehiclePrefab(ref random3,
										1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true,
										out _);
								}

								if (m_PrefabCarData.TryGetComponent(componentData15.m_Prefab,
									    out var componentData16) &&
								    m_ObjectGeometryData.TryGetComponent(componentData15.m_Prefab,
									    out var componentData17))
								{
									parameters2.m_MaxSpeed.x = componentData16.m_MaxSpeed;
									parameters2.m_ParkingSize =
										VehicleUtils.GetParkingSize(componentData17, out _);
									parameters2.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
									parameters2.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
									CurrentTransport value6;
									if (m_ParkedCarData.TryGetComponent(bicycle2, out var componentData18))
									{
										parameters2.m_ParkingTarget = componentData18.m_Lane;
										parameters2.m_ParkingDelta = componentData18.m_CurvePosition;
										if (m_PersonalCarData.TryGetComponent(bicycle2, out var componentData19) &&
										    (componentData19.m_State & PersonalCarFlags.HomeTarget) == 0)
										{
											parameters2.m_PathfindFlags |= PathfindFlags.ParkingReset;
										}
									}
									else if (!CollectionUtils.TryGet(nativeArray4, i, out value6) ||
									         !m_PrefabRefData.HasComponent(value6.m_CurrentTransport) ||
									         m_Deleteds.HasComponent(value6.m_CurrentTransport))
									{
										origin2.m_Methods |= PathMethod.Bicycle;
										origin2.m_RoadTypes |= RoadTypes.Bicycle;
									}
								}
							}

							SetupQueueItem value7 = new SetupQueueItem(entity, parameters2, origin2, destination3);
							m_PathQueue.Enqueue(value7);
							m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new Target
							{
								m_Target = Entity.Null
							});
						}
						else
						{
							RemoveAllTrips(trips);
						}
					}
				}
			}
		}

		#endregion

		#region Helpers

		private struct AnimalTargetInfo
		{
			public Entity m_Animal;
			public Entity m_Source;
			public Entity m_Target;
			public bool m_Teleport;
		}

		#endregion
	}
}

