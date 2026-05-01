using Game;
using Game.Simulation;
using Colossal.Entities;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Rendering;
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

namespace EconomyEX.Systems
{
	public partial class TaxiAISystemMod : GameSystemBase
	{
		#region Constants

		public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;
		public override int GetUpdateOffset(SystemUpdatePhase phase) => 6;

		#endregion

		#region Fields

		private EndFrameBarrier m_EndFrameBarrier;
		private PathfindSetupSystem m_PathfindSetupSystem;
		private TimeSystem m_TimeSystem;
		private SimulationSystem m_SimulationSystem;
		private EntityQuery m_VehicleQuery;
		private EntityArchetype m_TaxiRequestArchetype;
		private EntityArchetype m_HandleRequestArchetype;
		private ComponentTypeSet m_MovingToParkedCarRemoveTypes;
		private ComponentTypeSet m_MovingToParkedAddTypes;

		#endregion

		#region Lifecycle

		protected override void OnCreate()
		{
			base.OnCreate();
			m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
			m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			m_VehicleQuery = GetEntityQuery(ComponentType.ReadWrite<CarCurrentLane>(), ComponentType.ReadOnly<Owner>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadWrite<PathOwner>(), ComponentType.ReadWrite<Game.Vehicles.Taxi>(), ComponentType.ReadWrite<Target>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<TripSource>(), ComponentType.Exclude<OutOfControl>());
			m_TaxiRequestArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<ServiceRequest>(), ComponentType.ReadWrite<TaxiRequest>(), ComponentType.ReadWrite<RequestGroup>());
			m_HandleRequestArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<HandleRequest>(), ComponentType.ReadWrite<Game.Common.Event>());
			m_MovingToParkedCarRemoveTypes = new ComponentTypeSet(new[]
			{
				ComponentType.ReadWrite<Moving>(),
				ComponentType.ReadWrite<TransformFrame>(),
				ComponentType.ReadWrite<InterpolatedTransform>(),
				ComponentType.ReadWrite<Swaying>(),
				ComponentType.ReadWrite<CarNavigation>(),
				ComponentType.ReadWrite<CarNavigationLane>(),
				ComponentType.ReadWrite<CarCurrentLane>(),
				ComponentType.ReadWrite<PathOwner>(),
				ComponentType.ReadWrite<Target>(),
				ComponentType.ReadWrite<Blocker>(),
				ComponentType.ReadWrite<PathElement>(),
				ComponentType.ReadWrite<ServiceDispatch>()
			});
			m_MovingToParkedAddTypes = new ComponentTypeSet(ComponentType.ReadWrite<ParkedCar>(), ComponentType.ReadWrite<Stopped>(), ComponentType.ReadWrite<Updated>());
			RequireForUpdate(m_VehicleQuery);
		}

		protected override void OnUpdate()
		{
			NativeQueue<RouteVehicleUpdate> routeVehicleQueue = new NativeQueue<RouteVehicleUpdate>(Allocator.Persistent);
			TaxiTickJob jobData = new TaxiTickJob
			{
				m_EntityType = SystemAPI.GetEntityTypeHandle(),
				m_OwnerType = SystemAPI.GetComponentTypeHandle<Owner>(isReadOnly: true),
				m_UnspawnedType = SystemAPI.GetComponentTypeHandle<Unspawned>(isReadOnly: true),
				m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(isReadOnly: true),
				m_CurrentRouteType = SystemAPI.GetComponentTypeHandle<CurrentRoute>(isReadOnly: true),
				m_StoppedType = SystemAPI.GetComponentTypeHandle<Stopped>(isReadOnly: true),
				m_OdometerType = SystemAPI.GetComponentTypeHandle<Odometer>(isReadOnly: true),
				m_PassengerType = SystemAPI.GetBufferTypeHandle<Passenger>(isReadOnly: true),
				m_TaxiType = SystemAPI.GetComponentTypeHandle<Game.Vehicles.Taxi>(isReadOnly: false),
				m_CarType = SystemAPI.GetComponentTypeHandle<Car>(isReadOnly: false),
				m_CurrentLaneType = SystemAPI.GetComponentTypeHandle<CarCurrentLane>(isReadOnly: false),
				m_CarNavigationLaneType = SystemAPI.GetBufferTypeHandle<CarNavigationLane>(isReadOnly: false),
				m_ServiceDispatchType = SystemAPI.GetBufferTypeHandle<ServiceDispatch>(isReadOnly: false),
				m_EntityLookup = SystemAPI.GetEntityStorageInfoLookup(),
				m_OwnerData = SystemAPI.GetComponentLookup<Owner>(isReadOnly: true),
				m_PathInformationData = SystemAPI.GetComponentLookup<PathInformation>(isReadOnly: true),
				m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(isReadOnly: true),
				m_PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(isReadOnly: true),
				m_BlockerData = SystemAPI.GetComponentLookup<Blocker>(isReadOnly: true),
				m_SpawnLocationData = SystemAPI.GetComponentLookup<Game.Objects.SpawnLocation>(isReadOnly: true),
				m_StoppedData = SystemAPI.GetComponentLookup<Stopped>(isReadOnly: true),
				m_UnspawnedData = SystemAPI.GetComponentLookup<Unspawned>(isReadOnly: true),
				m_RouteLaneData = SystemAPI.GetComponentLookup<RouteLane>(isReadOnly: true),
				m_BoardingVehicleData = SystemAPI.GetComponentLookup<BoardingVehicle>(isReadOnly: true),
				m_TaxiStandData = SystemAPI.GetComponentLookup<TaxiStand>(isReadOnly: true),
				m_WaitingPassengersData = SystemAPI.GetComponentLookup<WaitingPassengers>(isReadOnly: true),
				m_LaneData = SystemAPI.GetComponentLookup<Lane>(isReadOnly: true),
				m_CarLaneData = SystemAPI.GetComponentLookup<Game.Net.CarLane>(isReadOnly: true),
				m_ParkingLaneData = SystemAPI.GetComponentLookup<Game.Net.ParkingLane>(isReadOnly: true),
				m_GarageLaneData = SystemAPI.GetComponentLookup<GarageLane>(isReadOnly: true),
				m_ConnectionLaneData = SystemAPI.GetComponentLookup<Game.Net.ConnectionLane>(isReadOnly: true),
				m_CurveData = SystemAPI.GetComponentLookup<Curve>(isReadOnly: true),
				m_SlaveLaneData = SystemAPI.GetComponentLookup<SlaveLane>(isReadOnly: true),
				m_PropertyRenterData = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
				m_TransportDepotData = SystemAPI.GetComponentLookup<Game.Buildings.TransportDepot>(isReadOnly: true),
				m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(isReadOnly: true),
				m_PrefabTaxiData = SystemAPI.GetComponentLookup<TaxiData>(isReadOnly: true),
				m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
				m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(isReadOnly: true),
				m_PrefabParkingLaneData = SystemAPI.GetComponentLookup<ParkingLaneData>(isReadOnly: true),
				m_PrefabCreatureData = SystemAPI.GetComponentLookup<CreatureData>(isReadOnly: true),
				m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(isReadOnly: true),
				m_PrefabSpawnLocationData = SystemAPI.GetComponentLookup<SpawnLocationData>(isReadOnly: true),
				m_TaxiRequestData = SystemAPI.GetComponentLookup<TaxiRequest>(isReadOnly: true),
				m_ResidentData = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(isReadOnly: true),
				m_DivertData = SystemAPI.GetComponentLookup<Divert>(isReadOnly: true),
				m_CurrentVehicleData = SystemAPI.GetComponentLookup<CurrentVehicle>(isReadOnly: true),
				m_RideNeederData = SystemAPI.GetComponentLookup<RideNeeder>(isReadOnly: true),
				m_CitizenData = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: true),
				m_CarKeeperData = SystemAPI.GetComponentLookup<CarKeeper>(isReadOnly: true),
				m_BicycleOwnerData = SystemAPI.GetComponentLookup<BicycleOwner>(isReadOnly: true),
				m_HouseholdMemberData = SystemAPI.GetComponentLookup<HouseholdMember>(isReadOnly: true),
				m_HouseholdData = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
				m_WorkerData = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
				m_TravelPurposeData = SystemAPI.GetComponentLookup<TravelPurpose>(isReadOnly: true),
				m_SubLanes = SystemAPI.GetBufferLookup<Game.Net.SubLane>(isReadOnly: true),
				m_LaneObjects = SystemAPI.GetBufferLookup<LaneObject>(isReadOnly: true),
				m_LaneOverlaps = SystemAPI.GetBufferLookup<LaneOverlap>(isReadOnly: true),
				m_RouteVehicles = SystemAPI.GetBufferLookup<RouteVehicle>(isReadOnly: true),
				m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
				m_TargetData = SystemAPI.GetComponentLookup<Target>(isReadOnly: false),
				m_PathOwnerData = SystemAPI.GetComponentLookup<PathOwner>(isReadOnly: false),
				m_PathElements = SystemAPI.GetBufferLookup<PathElement>(isReadOnly: false),
				m_RandomSeed = RandomSeed.Next(),
				m_TaxiRequestArchetype = m_TaxiRequestArchetype,
				m_HandleRequestArchetype = m_HandleRequestArchetype,
				m_MovingToParkedCarRemoveTypes = m_MovingToParkedCarRemoveTypes,
				m_MovingToParkedAddTypes = m_MovingToParkedAddTypes,
				m_TimeOfDay = m_TimeSystem.normalizedTime,
				m_SimulationFrameIndex = m_SimulationSystem.frameIndex,
				m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
				m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64, 16).AsParallelWriter(),
				m_RouteVehicleQueue = routeVehicleQueue.AsParallelWriter()
			};
			UpdateRouteVehiclesJob jobData2 = new UpdateRouteVehiclesJob
			{
				m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
				m_RouteVehicleQueue = routeVehicleQueue,
				m_TaxiData = SystemAPI.GetComponentLookup<Game.Vehicles.Taxi>(isReadOnly: false),
				m_BoardingVehicleData = SystemAPI.GetComponentLookup<BoardingVehicle>(isReadOnly: false)
			};
			JobHandle jobHandle = jobData.ScheduleParallel(m_VehicleQuery, Dependency);
			JobHandle jobHandle2 = jobData2.Schedule(jobHandle);
			routeVehicleQueue.Dispose(jobHandle2);
			m_PathfindSetupSystem.AddQueueWriter(jobHandle);
			m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
			Dependency = jobHandle2;
		}

		#endregion

		#region Jobs

		[BurstCompile]
		private struct TaxiTickJob : IJobChunk
		{
			[ReadOnly] public EntityTypeHandle m_EntityType;
			[ReadOnly] public ComponentTypeHandle<Owner> m_OwnerType;
			[ReadOnly] public ComponentTypeHandle<Unspawned> m_UnspawnedType;
			[ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
			[ReadOnly] public ComponentTypeHandle<CurrentRoute> m_CurrentRouteType;
			[ReadOnly] public ComponentTypeHandle<Stopped> m_StoppedType;
			[ReadOnly] public ComponentTypeHandle<Odometer> m_OdometerType;
			[ReadOnly] public BufferTypeHandle<Passenger> m_PassengerType;
			public ComponentTypeHandle<Game.Vehicles.Taxi> m_TaxiType;
			public ComponentTypeHandle<Car> m_CarType;
			public ComponentTypeHandle<CarCurrentLane> m_CurrentLaneType;
			public BufferTypeHandle<CarNavigationLane> m_CarNavigationLaneType;
			public BufferTypeHandle<ServiceDispatch> m_ServiceDispatchType;
			[ReadOnly] public EntityStorageInfoLookup m_EntityLookup;
			[ReadOnly] public ComponentLookup<Owner> m_OwnerData;
			[ReadOnly] public ComponentLookup<PathInformation> m_PathInformationData;
			[ReadOnly] public ComponentLookup<ParkedCar> m_ParkedCarData;
			[ReadOnly] public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;
			[ReadOnly] public ComponentLookup<Blocker> m_BlockerData;
			[ReadOnly] public ComponentLookup<Game.Objects.SpawnLocation> m_SpawnLocationData;
			[ReadOnly] public ComponentLookup<Stopped> m_StoppedData;
			[ReadOnly] public ComponentLookup<Unspawned> m_UnspawnedData;
			[ReadOnly] public ComponentLookup<RouteLane> m_RouteLaneData;
			[ReadOnly] public ComponentLookup<BoardingVehicle> m_BoardingVehicleData;
			[ReadOnly] public ComponentLookup<TaxiStand> m_TaxiStandData;
			[ReadOnly] public ComponentLookup<WaitingPassengers> m_WaitingPassengersData;
			[ReadOnly] public ComponentLookup<Lane> m_LaneData;
			[ReadOnly] public ComponentLookup<Game.Net.CarLane> m_CarLaneData;
			[ReadOnly] public ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneData;
			[ReadOnly] public ComponentLookup<GarageLane> m_GarageLaneData;
			[ReadOnly] public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;
			[ReadOnly] public ComponentLookup<Curve> m_CurveData;
			[ReadOnly] public ComponentLookup<SlaveLane> m_SlaveLaneData;
			[ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenterData;
			[ReadOnly] public ComponentLookup<Game.Buildings.TransportDepot> m_TransportDepotData;
			[ReadOnly] public ComponentLookup<CarData> m_PrefabCarData;
			[ReadOnly] public ComponentLookup<TaxiData> m_PrefabTaxiData;
			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;
			[ReadOnly] public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
			[ReadOnly] public ComponentLookup<ParkingLaneData> m_PrefabParkingLaneData;
			[ReadOnly] public ComponentLookup<CreatureData> m_PrefabCreatureData;
			[ReadOnly] public ComponentLookup<HumanData> m_PrefabHumanData;
			[ReadOnly] public ComponentLookup<SpawnLocationData> m_PrefabSpawnLocationData;
			[ReadOnly] public ComponentLookup<TaxiRequest> m_TaxiRequestData;
			[ReadOnly] public ComponentLookup<Game.Creatures.Resident> m_ResidentData;
			[ReadOnly] public ComponentLookup<Divert> m_DivertData;
			[ReadOnly] public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;
			[ReadOnly] public ComponentLookup<RideNeeder> m_RideNeederData;
			[ReadOnly] public ComponentLookup<Citizen> m_CitizenData;
			[ReadOnly] public ComponentLookup<CarKeeper> m_CarKeeperData;
			[ReadOnly] public ComponentLookup<BicycleOwner> m_BicycleOwnerData;
			[ReadOnly] public ComponentLookup<HouseholdMember> m_HouseholdMemberData;
			[ReadOnly] public ComponentLookup<Household> m_HouseholdData;
			[ReadOnly] public ComponentLookup<Worker> m_WorkerData;
			[ReadOnly] public ComponentLookup<TravelPurpose> m_TravelPurposeData;
			[ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
			[ReadOnly] public BufferLookup<Game.Net.SubLane> m_SubLanes;
			[ReadOnly] public BufferLookup<LaneObject> m_LaneObjects;
			[ReadOnly] public BufferLookup<LaneOverlap> m_LaneOverlaps;
			[ReadOnly] public BufferLookup<RouteVehicle> m_RouteVehicles;
			[NativeDisableParallelForRestriction] public ComponentLookup<Target> m_TargetData;
			[NativeDisableParallelForRestriction] public ComponentLookup<PathOwner> m_PathOwnerData;
			[NativeDisableParallelForRestriction] public BufferLookup<PathElement> m_PathElements;
			[ReadOnly] public RandomSeed m_RandomSeed;
			[ReadOnly] public EntityArchetype m_TaxiRequestArchetype;
			[ReadOnly] public EntityArchetype m_HandleRequestArchetype;
			[ReadOnly] public ComponentTypeSet m_MovingToParkedCarRemoveTypes;
			[ReadOnly] public ComponentTypeSet m_MovingToParkedAddTypes;
			[ReadOnly] public float m_TimeOfDay;
			[ReadOnly] public uint m_SimulationFrameIndex;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
			public NativeQueue<RouteVehicleUpdate>.ParallelWriter m_RouteVehicleQueue;
            
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
				NativeArray<Owner> owners = chunk.GetNativeArray(ref m_OwnerType);
				NativeArray<PrefabRef> prefabRefs = chunk.GetNativeArray(ref m_PrefabRefType);
				NativeArray<CurrentRoute> currentRoutes = chunk.GetNativeArray(ref m_CurrentRouteType);
				NativeArray<CarCurrentLane> currentLanes = chunk.GetNativeArray(ref m_CurrentLaneType);
				NativeArray<Game.Vehicles.Taxi> taxis = chunk.GetNativeArray(ref m_TaxiType);
				NativeArray<Car> cars = chunk.GetNativeArray(ref m_CarType);
				NativeArray<Odometer> odometers = chunk.GetNativeArray(ref m_OdometerType);
				BufferAccessor<Passenger> passengerBuffers = chunk.GetBufferAccessor(ref m_PassengerType);
				BufferAccessor<CarNavigationLane> navigationLaneBuffers = chunk.GetBufferAccessor(ref m_CarNavigationLaneType);
				BufferAccessor<ServiceDispatch> serviceDispatchBuffers = chunk.GetBufferAccessor(ref m_ServiceDispatchType);
				bool isStopped = chunk.Has(ref m_StoppedType);
				bool isUnspawned = chunk.Has(ref m_UnspawnedType);
				for (int index = 0; index < entities.Length; index++)
				{
					Entity entity = entities[index];
					Owner owner = owners[index];
					PrefabRef prefabRef = prefabRefs[index];
					Game.Vehicles.Taxi taxi = taxis[index];
					Car car = cars[index];
					CarCurrentLane currentLane = currentLanes[index];
					Odometer odometer = odometers[index];
					DynamicBuffer<Passenger> passengers = passengerBuffers[index];
					DynamicBuffer<CarNavigationLane> navigationLanes = navigationLaneBuffers[index];
					DynamicBuffer<ServiceDispatch> serviceDispatches = serviceDispatchBuffers[index];
					CurrentRoute currentRoute = default(CurrentRoute);
					if (currentRoutes.Length != 0)
					{
						currentRoute = currentRoutes[index];
					}
					Target target = m_TargetData[entity];
					PathOwner pathOwner = m_PathOwnerData[entity];
					VehicleUtils.CheckUnspawned(unfilteredChunkIndex, entity, currentLane, isUnspawned, m_CommandBuffer);
					Tick(unfilteredChunkIndex, entity, owner, odometer, prefabRef, currentRoute, passengers, navigationLanes, serviceDispatches, isStopped, ref taxi, ref car, ref currentLane, ref pathOwner, ref target);
					m_TargetData[entity] = target;
					m_PathOwnerData[entity] = pathOwner;
					taxis[index] = taxi;
					cars[index] = car;
					currentLanes[index] = currentLane;
				}
			}

			private void Tick(int jobIndex, Entity entity, Owner owner, Odometer odometer, PrefabRef prefabRef, CurrentRoute currentRoute, DynamicBuffer<Passenger> passengers, DynamicBuffer<CarNavigationLane> navigationLanes, DynamicBuffer<ServiceDispatch> serviceDispatches, bool isStopped, ref Game.Vehicles.Taxi taxi, ref Car car, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				Unity.Mathematics.Random random = m_RandomSeed.GetRandom(entity.Index);
				bool isBoarding = (taxi.m_State & TaxiFlags.Boarding) != 0;
				if (m_PrefabTaxiData.TryGetComponent(prefabRef.m_Prefab, out var componentData) && odometer.m_Distance >= componentData.m_MaintenanceRange && componentData.m_MaintenanceRange > 0.1f)
				{
					taxi.m_State |= TaxiFlags.RequiresMaintenance;
				}
				CheckServiceDispatches(entity, serviceDispatches, ref taxi);
				if ((taxi.m_State & (TaxiFlags.Requested | TaxiFlags.RequiresMaintenance | TaxiFlags.Dispatched | TaxiFlags.Disabled)) == 0 && !m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
				{
					RequestTargetIfNeeded(jobIndex, entity, ref taxi);
				}
				if (VehicleUtils.ResetUpdatedPath(ref pathOwner))
				{
					CarData prefabCarData = m_PrefabCarData[prefabRef.m_Prefab];
					ResetPath(jobIndex, ref random, entity, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, prefabCarData);
				}
				if (((taxi.m_State & (TaxiFlags.Disembarking | TaxiFlags.Transporting)) == 0 && !m_EntityLookup.Exists(target.m_Target)) || VehicleUtils.PathfindFailed(pathOwner))
				{
					if ((taxi.m_State & TaxiFlags.Boarding) != 0)
					{
						isBoarding = false;
						if (m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
						{
							m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Remove(currentRoute.m_Route, entity));
						}
						else
						{
							taxi.m_State &= ~TaxiFlags.Boarding;
						}
					}
					if (VehicleUtils.IsStuck(pathOwner) || (taxi.m_State & TaxiFlags.Returning) != 0)
					{
						m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
						return;
					}
					ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
				}
				else if (VehicleUtils.PathEndReached(currentLane) || VehicleUtils.ParkingSpaceReached(currentLane, pathOwner) || (taxi.m_State & (TaxiFlags.Boarding | TaxiFlags.Disembarking)) != 0)
				{
					if ((taxi.m_State & TaxiFlags.Disembarking) != 0)
					{
						if (StopDisembarking(entity, passengers, ref taxi, ref pathOwner) && !SelectDispatch(jobIndex, entity, currentRoute, navigationLanes, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, ref target))
						{
							if ((taxi.m_State & TaxiFlags.Returning) != 0)
							{
								if (VehicleUtils.ParkingSpaceReached(currentLane, pathOwner))
								{
									ParkCar(jobIndex, entity, owner, ref taxi, ref car, ref currentLane);
								}
								else
								{
									m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
								}
								return;
							}
							ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
						}
					}
					else if ((taxi.m_State & TaxiFlags.Boarding) != 0)
					{
						if (StopBoarding(jobIndex, entity, ref random, odometer, prefabRef, currentRoute, passengers, navigationLanes, serviceDispatches, ref taxi, ref currentLane, ref pathOwner, ref target))
						{
							isBoarding = false;
							if ((taxi.m_State & TaxiFlags.Transporting) == 0 && !SelectDispatch(jobIndex, entity, currentRoute, navigationLanes, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, ref target))
							{
								ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
							}
						}
						else if (!isStopped && ShouldStop(currentRoute, passengers, ref taxi))
						{
							StopVehicle(jobIndex, entity, ref car, ref currentLane);
						}
					}
					else if (!StartDisembarking(odometer, passengers, ref taxi) && ((taxi.m_State & TaxiFlags.Dispatched) != 0 || !SelectDispatch(jobIndex, entity, currentRoute, navigationLanes, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, ref target)))
					{
						if ((taxi.m_State & TaxiFlags.Returning) != 0)
						{
							if (VehicleUtils.ParkingSpaceReached(currentLane, pathOwner))
							{
								ParkCar(jobIndex, entity, owner, ref taxi, ref car, ref currentLane);
							}
							else
							{
								m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
							}
							return;
						}
						if (StartBoarding(jobIndex, entity, currentRoute, serviceDispatches, ref taxi, ref target))
						{
							isBoarding = true;
							if (!isStopped && ShouldStop(currentRoute, passengers, ref taxi))
							{
								StopVehicle(jobIndex, entity, ref car, ref currentLane);
							}
						}
						else if (!SelectDispatch(jobIndex, entity, currentRoute, navigationLanes, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, ref target))
						{
							ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
						}
					}
				}
				else if (VehicleUtils.QueueReached(currentLane))
				{
					if ((taxi.m_State & (TaxiFlags.Returning | TaxiFlags.Dispatched)) != 0)
					{
						currentLane.m_LaneFlags &= ~Game.Vehicles.CarLaneFlags.Queue;
					}
					else if (SelectDispatch(jobIndex, entity, currentRoute, navigationLanes, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, ref target))
					{
						currentLane.m_LaneFlags &= ~Game.Vehicles.CarLaneFlags.Queue;
					}
					else if ((taxi.m_State & TaxiFlags.Disabled) != 0)
					{
						ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
					}
					else if (!isStopped)
					{
						if (CanQueue(currentRoute, out var shouldStop))
						{
							if (shouldStop)
							{
								StopVehicle(jobIndex, entity, ref car, ref currentLane);
							}
						}
						else
						{
							ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
						}
					}
				}
				else if (VehicleUtils.WaypointReached(currentLane))
				{
					currentLane.m_LaneFlags &= ~Game.Vehicles.CarLaneFlags.Waypoint;
					pathOwner.m_State &= ~PathFlags.Failed;
					pathOwner.m_State |= PathFlags.Obsolete;
				}
				else
				{
					if ((taxi.m_State & (TaxiFlags.Returning | TaxiFlags.Transporting | TaxiFlags.Disabled)) == TaxiFlags.Disabled)
					{
						ReturnToDepot(jobIndex, entity, owner, serviceDispatches, ref taxi, ref car, ref pathOwner, ref target);
					}
					if (isStopped)
					{
						StartVehicle(jobIndex, entity, ref car, ref currentLane);
					}
				}
				if ((taxi.m_State & TaxiFlags.Disembarking) == 0 && !isBoarding)
				{
					if ((taxi.m_State & (TaxiFlags.Transporting | TaxiFlags.Dispatched)) == 0)
					{
						SelectDispatch(jobIndex, entity, currentRoute, navigationLanes, serviceDispatches, ref taxi, ref car, ref currentLane, ref pathOwner, ref target);
					}
					if ((taxi.m_State & TaxiFlags.Arriving) == 0)
					{
						CheckNavigationLanes(navigationLanes, ref taxi, ref currentLane, ref target);
					}
					if (VehicleUtils.RequireNewPath(pathOwner))
					{
						if (isStopped && (currentLane.m_LaneFlags & Game.Vehicles.CarLaneFlags.ParkingSpace) == 0)
						{
							currentLane.m_LaneFlags &= ~Game.Vehicles.CarLaneFlags.EndOfPath;
						}
						else
						{
							FindNewPath(entity, prefabRef, passengers, ref taxi, ref currentLane, ref pathOwner, ref target);
						}
					}
					else if ((pathOwner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Stuck)) == 0)
					{
						CheckParkingSpace(entity, ref random, ref taxi, ref currentLane, ref pathOwner, navigationLanes);
					}
				}
				if ((taxi.m_State & (TaxiFlags.Disembarking | TaxiFlags.Transporting | TaxiFlags.RequiresMaintenance | TaxiFlags.Dispatched | TaxiFlags.FromOutside | TaxiFlags.Disabled)) == 0)
				{
					car.m_Flags |= CarFlags.Sign;
				}
				else
				{
					car.m_Flags &= ~CarFlags.Sign;
				}
			}

			private bool CanQueue(CurrentRoute currentRoute, out bool shouldStop)
			{
				if (m_WaitingPassengersData.TryGetComponent(currentRoute.m_Route, out var componentData) && m_RouteVehicles.TryGetBuffer(currentRoute.m_Route, out var bufferData))
				{
					int stoppedCount = 0;
					for (int i = 0; i < bufferData.Length; i++)
					{
						if (m_StoppedData.HasComponent(bufferData[i].m_Vehicle))
						{
							stoppedCount++;
						}
					}
					int maxTaxiCount = RouteUtils.GetMaxTaxiCount(componentData);
					shouldStop = componentData.m_Count == 0;
					return stoppedCount < maxTaxiCount;
				}
				shouldStop = false;
				return false;
			}

			private bool ShouldStop(CurrentRoute currentRoute, DynamicBuffer<Passenger> passengers, ref Game.Vehicles.Taxi taxi)
			{
				if ((taxi.m_State & TaxiFlags.Dispatched) == 0 && passengers.Length == 0 && m_WaitingPassengersData.TryGetComponent(currentRoute.m_Route, out var componentData))
				{
					return componentData.m_Count == 0;
				}
				return false;
			}

			private void ParkCar(int jobIndex, Entity entity, Owner owner, ref Game.Vehicles.Taxi taxi, ref Car car, ref CarCurrentLane currentLane)
			{
				car.m_Flags &= ~CarFlags.Sign;
				taxi.m_State &= TaxiFlags.RequiresMaintenance;
				if (m_TransportDepotData.TryGetComponent(owner.m_Owner, out var componentData) && (componentData.m_Flags & TransportDepotFlags.HasAvailableVehicles) == 0)
				{
					taxi.m_State |= TaxiFlags.Disabled;
				}
				m_CommandBuffer.RemoveComponent(jobIndex, entity, in m_MovingToParkedCarRemoveTypes);
				m_CommandBuffer.AddComponent(jobIndex, entity, in m_MovingToParkedAddTypes);
				m_CommandBuffer.SetComponent(jobIndex, entity, new ParkedCar(currentLane.m_Lane, currentLane.m_CurvePosition.x));
				if (m_ParkingLaneData.HasComponent(currentLane.m_Lane) && currentLane.m_ChangeLane == Entity.Null)
				{
					m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, currentLane.m_Lane);
				}
				else if (m_GarageLaneData.HasComponent(currentLane.m_Lane))
				{
					m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, currentLane.m_Lane);
					m_CommandBuffer.AddComponent(jobIndex, entity, new FixParkingLocation(currentLane.m_ChangeLane, entity));
				}
				else
				{
					m_CommandBuffer.AddComponent(jobIndex, entity, new FixParkingLocation(currentLane.m_ChangeLane, entity));
				}
			}

			private void StopVehicle(int jobIndex, Entity entity, ref Car car, ref CarCurrentLane currentLaneData)
			{
				m_CommandBuffer.RemoveComponent<Moving>(jobIndex, entity);
				m_CommandBuffer.RemoveComponent<TransformFrame>(jobIndex, entity);
				m_CommandBuffer.RemoveComponent<InterpolatedTransform>(jobIndex, entity);
				m_CommandBuffer.RemoveComponent<Swaying>(jobIndex, entity);
				m_CommandBuffer.AddComponent(jobIndex, entity, default(Stopped));
				m_CommandBuffer.AddComponent(jobIndex, entity, default(Updated));
				if (m_CarLaneData.HasComponent(currentLaneData.m_Lane))
				{
					m_CommandBuffer.AddComponent(jobIndex, currentLaneData.m_Lane, default(PathfindUpdated));
				}
				if (m_CarLaneData.HasComponent(currentLaneData.m_ChangeLane))
				{
					m_CommandBuffer.AddComponent(jobIndex, currentLaneData.m_ChangeLane, default(PathfindUpdated));
				}
				if ((currentLaneData.m_LaneFlags & Game.Vehicles.CarLaneFlags.Queue) != 0)
				{
					car.m_Flags |= CarFlags.Queueing;
				}
			}

			private void StartVehicle(int jobIndex, Entity entity, ref Car car, ref CarCurrentLane currentLaneData)
			{
				m_CommandBuffer.RemoveComponent<Stopped>(jobIndex, entity);
				m_CommandBuffer.AddComponent(jobIndex, entity, default(Moving));
				m_CommandBuffer.AddBuffer<TransformFrame>(jobIndex, entity);
				m_CommandBuffer.AddComponent(jobIndex, entity, default(InterpolatedTransform));
				m_CommandBuffer.AddComponent(jobIndex, entity, default(Swaying));
				m_CommandBuffer.AddComponent(jobIndex, entity, default(Updated));
				if (m_CarLaneData.HasComponent(currentLaneData.m_Lane))
				{
					m_CommandBuffer.AddComponent(jobIndex, currentLaneData.m_Lane, default(PathfindUpdated));
				}
				if (m_CarLaneData.HasComponent(currentLaneData.m_ChangeLane))
				{
					m_CommandBuffer.AddComponent(jobIndex, currentLaneData.m_ChangeLane, default(PathfindUpdated));
				}
				car.m_Flags &= ~CarFlags.Queueing;
			}

			private void CheckNavigationLanes(DynamicBuffer<CarNavigationLane> navigationLanes, ref Game.Vehicles.Taxi taxi, ref CarCurrentLane currentLane, ref Target target)
			{
				if (navigationLanes.Length == 0 || navigationLanes.Length == 8)
				{
					return;
				}
				CarNavigationLane value = navigationLanes[^1];
				if ((value.m_Flags & Game.Vehicles.CarLaneFlags.EndOfPath) == 0)
				{
					return;
				}
				taxi.m_State |= TaxiFlags.Arriving;
				if (!m_RouteLaneData.HasComponent(target.m_Target))
				{
					return;
				}
				RouteLane routeLane = m_RouteLaneData[target.m_Target];
				if (routeLane.m_StartLane != routeLane.m_EndLane)
				{
					value.m_CurvePosition.y = 1f;
					CarNavigationLane elem = new CarNavigationLane
					{
						m_Lane = value.m_Lane
					};
					if (FindNextLane(ref elem.m_Lane))
					{
						value.m_Flags &= ~Game.Vehicles.CarLaneFlags.EndOfPath;
						value.m_Flags |= Game.Vehicles.CarLaneFlags.Queue;
						navigationLanes[^1] = value;
						elem.m_Flags |= Game.Vehicles.CarLaneFlags.EndOfPath | Game.Vehicles.CarLaneFlags.FixedLane | Game.Vehicles.CarLaneFlags.Queue;
						elem.m_CurvePosition = new float2(0f, routeLane.m_EndCurvePos);
						navigationLanes.Add(elem);
					}
					else
					{
						navigationLanes[^1] = value;
					}
				}
				else
				{
					value.m_Flags |= Game.Vehicles.CarLaneFlags.Queue;
					value.m_CurvePosition.y = routeLane.m_EndCurvePos;
					navigationLanes[^1] = value;
				}
			}

			private bool FindNextLane(ref Entity lane)
			{
				if (!m_OwnerData.HasComponent(lane) || !m_LaneData.HasComponent(lane))
				{
					return false;
				}
				Owner owner = m_OwnerData[lane];
				Lane lane2 = m_LaneData[lane];
				if (!m_SubLanes.HasBuffer(owner.m_Owner))
				{
					return false;
				}
				DynamicBuffer<Game.Net.SubLane> dynamicBuffer = m_SubLanes[owner.m_Owner];
				for (int i = 0; i < dynamicBuffer.Length; i++)
				{
					Entity subLane = dynamicBuffer[i].m_SubLane;
					Lane lane3 = m_LaneData[subLane];
					if (lane2.m_EndNode.Equals(lane3.m_StartNode))
					{
						lane = subLane;
						return true;
					}
				}
				return false;
			}

			private void FindNewPath(Entity vehicleEntity, PrefabRef prefabRef, DynamicBuffer<Passenger> passengers, ref Game.Vehicles.Taxi taxi, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				CarData carData = m_PrefabCarData[prefabRef.m_Prefab];
				pathOwner.m_State &= ~(PathFlags.AddDestination | PathFlags.Divert);
				bool isHomeTarget = false;
				PathfindParameters parameters;
				SetupQueueTarget origin;
				SetupQueueTarget destination;
				if ((taxi.m_State & (TaxiFlags.Returning | TaxiFlags.Transporting)) == TaxiFlags.Transporting)
				{
					parameters = new PathfindParameters
					{
						m_MaxSpeed = carData.m_MaxSpeed,
						m_WalkSpeed = 5.555556f,
						m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
						m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi),
						m_TaxiIgnoredRules = (RuleFlags.ForbidPrivateTraffic | VehicleUtils.GetIgnoredPathfindRules(carData))
					};
					origin = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_Methods = (VehicleUtils.GetPathMethods(carData) | PathMethod.Boarding),
						m_RoadTypes = RoadTypes.Car,
						m_Flags = SetupTargetFlags.SecondaryPath
					};
					destination = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_Methods = PathMethod.Pedestrian,
						m_Entity = target.m_Target,
						m_RandomCost = 30f
					};
					Entity entity = FindLeader(passengers);
					if (m_ResidentData.HasComponent(entity))
					{
						PrefabRef prefabRef2 = m_PrefabRefData[entity];
						Game.Creatures.Resident resident = m_ResidentData[entity];
						CreatureData creatureData = m_PrefabCreatureData[prefabRef2.m_Prefab];
						parameters.m_WalkSpeed = m_PrefabHumanData[prefabRef2.m_Prefab].m_WalkSpeed;
						parameters.m_Methods |= RouteUtils.GetPublicTransportMethods(resident, m_TimeOfDay);
						parameters.m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost;
						destination.m_ActivityMask = creatureData.m_SupportedActivities;
						if (m_HouseholdMemberData.TryGetComponent(resident.m_Citizen, out var componentData))
						{
							Entity household = componentData.m_Household;
							if (m_PropertyRenterData.TryGetComponent(household, out var componentData2))
							{
								parameters.m_Authorization1 = componentData2.m_Property;
								isHomeTarget |= componentData2.m_Property == target.m_Target;
							}
						}
						if (m_WorkerData.HasComponent(resident.m_Citizen))
						{
							Worker worker = m_WorkerData[resident.m_Citizen];
							parameters.m_Authorization2 = m_PropertyRenterData.HasComponent(worker.m_Workplace) ? m_PropertyRenterData[worker.m_Workplace].m_Property : worker.m_Workplace;
						}
						if (m_CitizenData.HasComponent(resident.m_Citizen))
						{
							Citizen citizen = m_CitizenData[resident.m_Citizen];
							Entity household2 = m_HouseholdMemberData[resident.m_Citizen].m_Household;
							Household household3 = m_HouseholdData[household2];
							parameters.m_Weights = CitizenUtils.GetPathfindWeights(citizen, household3, m_HouseholdCitizens[household2].Length);
						}
						BicycleOwner component2;
						PrefabRef componentData4;
						CarData componentData5;
						ObjectGeometryData componentData6;
						if (m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
						{
							if (m_ParkedCarData.HasComponent(component.m_Car))
							{
								PrefabRef prefabRef3 = m_PrefabRefData[component.m_Car];
								ParkedCar parkedCar = m_ParkedCarData[component.m_Car];
								CarData carData2 = m_PrefabCarData[prefabRef3.m_Prefab];
								parameters.m_MaxSpeed.x = carData2.m_MaxSpeed;
								parameters.m_ParkingTarget = parkedCar.m_Lane;
								parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
								parameters.m_ParkingSize = VehicleUtils.GetParkingSize(component.m_Car, ref m_PrefabRefData, ref m_PrefabObjectGeometryData);
								parameters.m_Methods |= VehicleUtils.GetPathMethods(carData2) | PathMethod.Parking;
								parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData2);
								if (m_PersonalCarData.TryGetComponent(component.m_Car, out var componentData3) && (componentData3.m_State & PersonalCarFlags.HomeTarget) == 0)
								{
									parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
								}
							}
						}
						else if (m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2) && m_PrefabRefData.TryGetComponent(component2.m_Bicycle, out componentData4) && m_PrefabCarData.TryGetComponent(componentData4.m_Prefab, out componentData5) && m_PrefabObjectGeometryData.TryGetComponent(componentData4.m_Prefab, out componentData6))
						{
							parameters.m_MaxSpeed.x = componentData5.m_MaxSpeed;
							parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData6, out var _);
							parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
							parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
							if (m_ParkedCarData.TryGetComponent(component2.m_Bicycle, out var componentData7))
							{
								parameters.m_ParkingTarget = componentData7.m_Lane;
								parameters.m_ParkingDelta = componentData7.m_CurvePosition;
								if (m_PersonalCarData.TryGetComponent(component2.m_Bicycle, out var componentData8) && (componentData8.m_State & PersonalCarFlags.HomeTarget) == 0)
								{
									parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
								}
							}
						}
						if (m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData9))
						{
							switch (componentData9.m_Purpose)
							{
							case Purpose.EmergencyShelter:
								parameters.m_Weights = new PathfindWeights(1f, 0.2f, 0f, 0.1f);
								break;
							case Purpose.GoingHome:
							case Purpose.GoingToWork:
							case Purpose.GoingToSchool:
							case Purpose.Shopping:
							case Purpose.Leisure:
							case Purpose.MovingAway:
								// [MapExt2-MaxCost] Allow massive distance taxi rides unconditionally
								parameters.m_MaxCost = CitizenBehaviorSystem.kMaxMovingAwayCost;
								break;
							}
						}
						if (m_DivertData.TryGetComponent(entity, out var componentData10))
						{
							CreatureUtils.DivertDestination(ref destination, ref pathOwner, componentData10);
							isHomeTarget &= componentData10.m_Purpose == Purpose.None;
						}
						if ((parameters.m_Methods & PathMethod.Bicycle) != 0 && isHomeTarget)
						{
							destination.m_Methods |= PathMethod.Bicycle;
							destination.m_RoadTypes |= RoadTypes.Bicycle;
						}
					}
				}
				else
				{
					parameters = new PathfindParameters
					{
						m_MaxSpeed = carData.m_MaxSpeed,
						m_WalkSpeed = 5.555556f,
						m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
						m_Methods = (VehicleUtils.GetPathMethods(carData) | PathMethod.Boarding),
						m_ParkingTarget = VehicleUtils.GetParkingSource(vehicleEntity, currentLane, ref m_ParkingLaneData, ref m_ConnectionLaneData),
						m_ParkingDelta = currentLane.m_CurvePosition.z,
						m_ParkingSize = VehicleUtils.GetParkingSize(vehicleEntity, ref m_PrefabRefData, ref m_PrefabObjectGeometryData),
						m_IgnoredRules = (RuleFlags.ForbidPrivateTraffic | VehicleUtils.GetIgnoredPathfindRules(carData))
					};
					origin = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_Methods = (VehicleUtils.GetPathMethods(carData) | PathMethod.Boarding),
						m_RoadTypes = RoadTypes.Car
					};
					destination = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_RoadTypes = RoadTypes.Car,
						m_Entity = target.m_Target
					};
					if ((taxi.m_State & TaxiFlags.Dispatched) != 0)
					{
						destination.m_Methods = PathMethod.Boarding;
					}
					else if ((taxi.m_State & TaxiFlags.Returning) != 0)
					{
						parameters.m_Methods |= PathMethod.SpecialParking;
						destination.m_Methods = VehicleUtils.GetPathMethods(carData) | PathMethod.SpecialParking;
						destination.m_RandomCost = 30f;
					}
					else
					{
						destination.m_Methods = VehicleUtils.GetPathMethods(carData);
					}
				}
				VehicleUtils.SetupPathfind(item: new SetupQueueItem(vehicleEntity, parameters, origin, destination), currentLane: ref currentLane, pathOwner: ref pathOwner, queue: m_PathfindQueue);
			}

			private void CheckServiceDispatches(Entity vehicleEntity, DynamicBuffer<ServiceDispatch> serviceDispatches, ref Game.Vehicles.Taxi taxi)
			{
				if ((taxi.m_State & TaxiFlags.Dispatched) != 0)
				{
					if (serviceDispatches.Length > 1)
					{
						serviceDispatches.RemoveRange(1, serviceDispatches.Length - 1);
					}
					else if (serviceDispatches.Length == 0)
					{
						taxi.m_State &= ~(TaxiFlags.Requested | TaxiFlags.Dispatched);
					}
					return;
				}
				TaxiRequestType taxiRequestType = TaxiRequestType.Stand;
				int maxPriority = -1;
				Entity entity = Entity.Null;
				for (int i = 0; i < serviceDispatches.Length; i++)
				{
					Entity request = serviceDispatches[i].m_Request;
					if (m_TaxiRequestData.TryGetComponent(request, out var componentData) && m_PrefabRefData.HasComponent(componentData.m_Seeker) && ((int)componentData.m_Type > (int)taxiRequestType || (componentData.m_Type == taxiRequestType && componentData.m_Priority > maxPriority)))
					{
						taxiRequestType = componentData.m_Type;
						maxPriority = componentData.m_Priority;
						entity = request;
					}
				}
				if (entity != Entity.Null)
				{
					serviceDispatches[0] = new ServiceDispatch(entity);
					if (serviceDispatches.Length > 1)
					{
						serviceDispatches.RemoveRange(1, serviceDispatches.Length - 1);
					}
					taxi.m_State |= TaxiFlags.Requested;
				}
				else
				{
					serviceDispatches.Clear();
					taxi.m_State &= ~TaxiFlags.Requested;
				}
			}

			private void RequestTargetIfNeeded(int jobIndex, Entity entity, ref Game.Vehicles.Taxi taxi)
			{
				if (!m_TaxiRequestData.HasComponent(taxi.m_TargetRequest))
				{
					uint updateMask = math.max(256u, 16u);
					if ((m_SimulationFrameIndex & (updateMask - 1)) == 6)
					{
						Entity e = m_CommandBuffer.CreateEntity(jobIndex, m_TaxiRequestArchetype);
						m_CommandBuffer.SetComponent(jobIndex, e, new ServiceRequest(reversed: true));
						m_CommandBuffer.SetComponent(jobIndex, e, new TaxiRequest(entity, Entity.Null, Entity.Null, ((taxi.m_State & TaxiFlags.FromOutside) != 0) ? TaxiRequestType.Outside : TaxiRequestType.None, 1));
						m_CommandBuffer.SetComponent(jobIndex, e, new RequestGroup(16u));
					}
				}
			}

			private bool SelectDispatch(int jobIndex, Entity entity, CurrentRoute currentRoute, DynamicBuffer<CarNavigationLane> navigationLanes, DynamicBuffer<ServiceDispatch> serviceDispatches, ref Game.Vehicles.Taxi taxi, ref Car car, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				taxi.m_State &= ~TaxiFlags.Dispatched;
				if (serviceDispatches.Length == 0 || (taxi.m_State & TaxiFlags.Requested) == 0)
				{
					taxi.m_State &= ~TaxiFlags.Requested;
					serviceDispatches.Clear();
					return false;
				}
				Entity request = serviceDispatches[0].m_Request;
				taxi.m_State &= ~TaxiFlags.Requested;
				if ((taxi.m_State & (TaxiFlags.RequiresMaintenance | TaxiFlags.Disabled)) != 0)
				{
					serviceDispatches.Clear();
					return false;
				}
				if (!m_TaxiRequestData.HasComponent(request))
				{
					serviceDispatches.Clear();
					return false;
				}
				TaxiRequest taxiRequest = m_TaxiRequestData[request];
				if (!m_PrefabRefData.HasComponent(taxiRequest.m_Seeker))
				{
					serviceDispatches.Clear();
					return false;
				}
				taxi.m_State &= ~TaxiFlags.Returning;
				if (taxiRequest.m_Type == TaxiRequestType.Customer || taxiRequest.m_Type == TaxiRequestType.Outside)
				{
					taxi.m_State |= TaxiFlags.Dispatched;
					m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, entity);
					Entity e = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
					m_CommandBuffer.SetComponent(jobIndex, e, new HandleRequest(request, entity, completed: false, pathConsumed: true));
				}
				else
				{
					serviceDispatches.Clear();
					if (m_BoardingVehicleData.HasComponent(taxiRequest.m_Seeker))
					{
						if (currentRoute.m_Route != taxiRequest.m_Seeker)
						{
							m_CommandBuffer.AddComponent(jobIndex, entity, new CurrentRoute(taxiRequest.m_Seeker));
							m_CommandBuffer.AppendToBuffer(jobIndex, taxiRequest.m_Seeker, new RouteVehicle(entity));
						}
					}
					else
					{
						m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, entity);
					}
					Entity e2 = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
					m_CommandBuffer.SetComponent(jobIndex, e2, new HandleRequest(request, entity, completed: true));
				}
				car.m_Flags |= CarFlags.StayOnRoad | CarFlags.UsePublicTransportLanes;
				if (m_TaxiRequestData.HasComponent(taxi.m_TargetRequest))
				{
					Entity e3 = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);
					m_CommandBuffer.SetComponent(jobIndex, e3, new HandleRequest(taxi.m_TargetRequest, Entity.Null, completed: true));
				}
				if (m_PathElements.HasBuffer(request))
				{
					DynamicBuffer<PathElement> appendPath = m_PathElements[request];
					if (appendPath.Length != 0)
					{
						DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
						PathUtils.TrimPath(dynamicBuffer, ref pathOwner);
						float newPathTime = taxi.m_PathElementTime * dynamicBuffer.Length + m_PathInformationData[request].m_Duration;
						if (PathUtils.TryAppendPath(ref currentLane, navigationLanes, dynamicBuffer, appendPath, m_SlaveLaneData, m_OwnerData, m_SubLanes))
						{
							taxi.m_PathElementTime = newPathTime / math.max(1, dynamicBuffer.Length);
							taxi.m_ExtraPathElementCount = 0;
							taxi.m_State &= ~TaxiFlags.Arriving;
							target.m_Target = taxiRequest.m_Seeker;
							VehicleUtils.ClearEndOfPath(ref currentLane, navigationLanes);
							VehicleUtils.ResetParkingLaneStatus(entity, ref currentLane, ref pathOwner, dynamicBuffer, ref m_EntityLookup, ref m_CurveData, ref m_ParkingLaneData, ref m_CarLaneData, ref m_ConnectionLaneData, ref m_SpawnLocationData, ref m_PrefabRefData, ref m_PrefabSpawnLocationData);
							return true;
						}
					}
				}
				VehicleUtils.SetTarget(ref pathOwner, ref target, taxiRequest.m_Seeker);
				return true;
			}

			private void ReturnToDepot(int jobIndex, Entity vehicleEntity, Owner ownerData, DynamicBuffer<ServiceDispatch> serviceDispatches, ref Game.Vehicles.Taxi taxi, ref Car car, ref PathOwner pathOwner, ref Target target)
			{
				serviceDispatches.Clear();
				taxi.m_State &= ~(TaxiFlags.Requested | TaxiFlags.Disembarking | TaxiFlags.Dispatched);
				taxi.m_State |= TaxiFlags.Returning;
				m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
				VehicleUtils.SetTarget(ref pathOwner, ref target, ownerData.m_Owner);
			}

			private void CheckParkingSpace(Entity entity, ref Unity.Mathematics.Random random, ref Game.Vehicles.Taxi taxi, ref CarCurrentLane currentLane, ref PathOwner pathOwner, DynamicBuffer<CarNavigationLane> navigationLanes)
			{
				DynamicBuffer<PathElement> path = m_PathElements[entity];
				bool allowPlanning = (taxi.m_State & TaxiFlags.Returning) == 0;
				Entity entity2 = VehicleUtils.ValidateParkingSpace(entity, ref random, ref currentLane, ref pathOwner, navigationLanes, path, ref m_ParkedCarData, ref m_BlockerData, ref m_CurveData, ref m_UnspawnedData, ref m_ParkingLaneData, ref m_GarageLaneData, ref m_ConnectionLaneData, ref m_PrefabRefData, ref m_PrefabParkingLaneData, ref m_PrefabObjectGeometryData, ref m_LaneObjects, ref m_LaneOverlaps, allowPlanning, ignoreDisabled: false, allowPlanning);
				if (entity2 != Entity.Null && m_ParkingLaneData.TryGetComponent(entity2, out var componentData))
				{
					taxi.m_NextStartingFee = componentData.m_TaxiFee;
				}
			}

			private void ResetPath(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, DynamicBuffer<ServiceDispatch> serviceDispatches, ref Game.Vehicles.Taxi taxi, ref Car car, ref CarCurrentLane currentLane, ref PathOwner pathOwner, CarData prefabCarData)
			{
				taxi.m_NextStartingFee = 0;
				taxi.m_State &= ~TaxiFlags.Arriving;
				if ((taxi.m_State & TaxiFlags.Returning) != 0)
				{
					car.m_Flags &= ~CarFlags.StayOnRoad;
					car.m_Flags |= CarFlags.UsePublicTransportLanes;
				}
				else
				{
					car.m_Flags |= CarFlags.StayOnRoad | CarFlags.UsePublicTransportLanes;
				}
				DynamicBuffer<PathElement> path = m_PathElements[entity];
				VehicleUtils.ResetParkingLaneStatus(entity, ref currentLane, ref pathOwner, path, ref m_EntityLookup, ref m_CurveData, ref m_ParkingLaneData, ref m_CarLaneData, ref m_ConnectionLaneData, ref m_SpawnLocationData, ref m_PrefabRefData, ref m_PrefabSpawnLocationData);
				ResetPath(ref random, ref taxi, entity, ref currentLane, ref pathOwner, prefabCarData);
			}

			private void ResetPath(ref Unity.Mathematics.Random random, ref Game.Vehicles.Taxi taxi, Entity entity, ref CarCurrentLane currentLane, ref PathOwner pathOwner, CarData prefabCarData)
			{
				DynamicBuffer<PathElement> path = m_PathElements[entity];
				PathUtils.ResetPath(ref currentLane, path, m_SlaveLaneData, m_OwnerData, m_SubLanes);
				bool ignoreDriveways = (taxi.m_State & TaxiFlags.Returning) == 0;
				int parkingLinkIndex = VehicleUtils.SetParkingCurvePos(entity, ref random, currentLane, pathOwner, path, ref m_ParkedCarData, ref m_UnspawnedData, ref m_CurveData, ref m_ParkingLaneData, ref m_ConnectionLaneData, ref m_PrefabRefData, ref m_PrefabObjectGeometryData, ref m_PrefabParkingLaneData, ref m_LaneObjects, ref m_LaneOverlaps, ignoreDriveways);
				taxi.m_ExtraPathElementCount = math.max(0, path.Length - (parkingLinkIndex + 1));
				taxi.m_PathElementTime = 0f;
				int laneCount = 0;
				for (int i = pathOwner.m_ElementIndex; i < parkingLinkIndex; i++)
				{
					PathElement pathElement = path[i];
					if (m_CarLaneData.TryGetComponent(pathElement.m_Target, out var componentData))
					{
						Curve curve = m_CurveData[pathElement.m_Target];
						taxi.m_PathElementTime += curve.m_Length / math.min(prefabCarData.m_MaxSpeed, componentData.m_SpeedLimit);
						laneCount++;
					}
				}
				if (laneCount != 0)
				{
					taxi.m_PathElementTime /= laneCount;
				}
			}

			private bool StartBoarding(int jobIndex, Entity vehicleEntity, CurrentRoute currentRoute, DynamicBuffer<ServiceDispatch> serviceDispatches, ref Game.Vehicles.Taxi taxi, ref Target target)
			{
				if ((taxi.m_State & TaxiFlags.Dispatched) != 0)
				{
					if (serviceDispatches.Length == 0)
					{
						taxi.m_State &= ~TaxiFlags.Dispatched;
						return false;
					}
					Entity request = serviceDispatches[0].m_Request;
					if (m_TaxiRequestData.HasComponent(request))
					{
						taxi.m_State |= TaxiFlags.Boarding;
						taxi.m_MaxBoardingDistance = 0f;
						taxi.m_MinWaitingDistance = float.MaxValue;
						return true;
					}
					taxi.m_State &= ~TaxiFlags.Dispatched;
					serviceDispatches.Clear();
				}
				else
				{
					if (m_TaxiStandData.HasComponent(currentRoute.m_Route))
					{
						taxi.m_NextStartingFee = m_TaxiStandData[currentRoute.m_Route].m_StartingFee;
					}
					if (m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
					{
						m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Add(currentRoute.m_Route, vehicleEntity));
						return true;
					}
				}
				return false;
			}

			private Entity FindLeader(DynamicBuffer<Passenger> passengers)
			{
				for (int i = 0; i < passengers.Length; i++)
				{
					Entity passenger = passengers[i].m_Passenger;
					if (m_CurrentVehicleData.HasComponent(passenger) && (m_CurrentVehicleData[passenger].m_Flags & CreatureVehicleFlags.Leader) != 0)
					{
						return passenger;
					}
				}
				return Entity.Null;
			}

			private bool StopBoarding(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, Odometer odometer, PrefabRef prefabRef, CurrentRoute currentRoute, DynamicBuffer<Passenger> passengers, DynamicBuffer<CarNavigationLane> navigationLanes, DynamicBuffer<ServiceDispatch> serviceDispatches, ref Game.Vehicles.Taxi taxi, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				taxi.m_MaxBoardingDistance = taxi.m_MinWaitingDistance > 1000000f ? float.MaxValue : taxi.m_MinWaitingDistance + 0.5f;
				taxi.m_MinWaitingDistance = float.MaxValue;
				Entity entity2 = Entity.Null;
				for (int i = 0; i < passengers.Length; i++)
				{
					Entity passenger = passengers[i].m_Passenger;
					Game.Creatures.Resident componentData2;
					if (m_CurrentVehicleData.TryGetComponent(passenger, out var componentData))
					{
						if ((componentData.m_Flags & CreatureVehicleFlags.Ready) == 0)
						{
							return false;
						}
						if ((componentData.m_Flags & CreatureVehicleFlags.Leader) != 0)
						{
							entity2 = passenger;
						}
					}
					else if (m_ResidentData.TryGetComponent(passenger, out componentData2) && (componentData2.m_Flags & ResidentFlags.InVehicle) != ResidentFlags.None)
					{
						return false;
					}
				}
				if (entity2 == Entity.Null)
				{
					if ((taxi.m_State & TaxiFlags.Dispatched) != 0)
					{
						if (serviceDispatches.Length != 0)
						{
							Entity request = serviceDispatches[0].m_Request;
							if (m_TaxiRequestData.HasComponent(request))
							{
								TaxiRequest taxiRequest = m_TaxiRequestData[request];
								if (m_RideNeederData.HasComponent(taxiRequest.m_Seeker) && m_RideNeederData[taxiRequest.m_Seeker].m_RideRequest == request)
								{
									return false;
								}
							}
						}
						serviceDispatches.Clear();
						taxi.m_State &= ~TaxiFlags.Dispatched;
						if (m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
						{
							m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Remove(currentRoute.m_Route, entity));
						}
						else
						{
							taxi.m_State &= ~TaxiFlags.Boarding;
						}
						m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, entity);
						return true;
					}
					if ((taxi.m_State & (TaxiFlags.Requested | TaxiFlags.Disabled)) != 0)
					{
						if (m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
						{
							m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Remove(currentRoute.m_Route, entity));
						}
						else
						{
							taxi.m_State &= ~TaxiFlags.Boarding;
						}
						m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, entity);
						return true;
					}
					if (VehicleUtils.RequireNewPath(pathOwner))
					{
						if (m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
						{
							m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Remove(currentRoute.m_Route, entity));
						}
						else
						{
							taxi.m_State &= ~TaxiFlags.Boarding;
						}
						return true;
					}
					if (m_BoardingVehicleData.TryGetComponent(currentRoute.m_Route, out var componentData3) && componentData3.m_Vehicle != entity)
					{
						m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Remove(currentRoute.m_Route, entity));
						m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, entity);
						return true;
					}
					return false;
				}
				if (m_BoardingVehicleData.HasComponent(currentRoute.m_Route))
				{
					m_RouteVehicleQueue.Enqueue(RouteVehicleUpdate.Remove(currentRoute.m_Route, entity));
				}
				else
				{
					taxi.m_State &= ~TaxiFlags.Boarding;
				}
				m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, entity);
				DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[entity];
				DynamicBuffer<PathElement> sourceElements = m_PathElements[entity2];
				PathOwner sourceOwner = m_PathOwnerData[entity2];
				Target target2 = m_TargetData[entity2];
				PathUtils.CopyPath(sourceElements, sourceOwner, 1, dynamicBuffer);
				pathOwner.m_ElementIndex = 0;
				serviceDispatches.Clear();
				taxi.m_State &= ~(TaxiFlags.Arriving | TaxiFlags.Dispatched);
				taxi.m_State |= TaxiFlags.Transporting;
				taxi.m_StartDistance = odometer.m_Distance;
				taxi.m_CurrentFee = taxi.m_NextStartingFee;
				target.m_Target = target2.m_Target;
				VehicleUtils.ClearEndOfPath(ref currentLane, navigationLanes);
				CarData prefabCarData = m_PrefabCarData[prefabRef.m_Prefab];
				VehicleUtils.ResetParkingLaneStatus(entity, ref currentLane, ref pathOwner, dynamicBuffer, ref m_EntityLookup, ref m_CurveData, ref m_ParkingLaneData, ref m_CarLaneData, ref m_ConnectionLaneData, ref m_SpawnLocationData, ref m_PrefabRefData, ref m_PrefabSpawnLocationData);
				ResetPath(ref random, ref taxi, entity, ref currentLane, ref pathOwner, prefabCarData);
				return true;
			}

			private bool StartDisembarking(Odometer odometer, DynamicBuffer<Passenger> passengers, ref Game.Vehicles.Taxi taxi)
			{
				if ((taxi.m_State & TaxiFlags.Transporting) != 0 && passengers.Length != 0)
				{
					taxi.m_State &= ~TaxiFlags.Transporting;
					taxi.m_State |= TaxiFlags.Disembarking;
					int distanceFee = Mathf.RoundToInt(math.max(0f, odometer.m_Distance - taxi.m_StartDistance) * 0.03f);
					taxi.m_CurrentFee = (ushort)math.clamp(taxi.m_CurrentFee + distanceFee, 0, 65535);
					return true;
				}
				taxi.m_State &= ~TaxiFlags.Transporting;
				return false;
			}

			private bool StopDisembarking(Entity vehicleEntity, DynamicBuffer<Passenger> passengers, ref Game.Vehicles.Taxi taxi, ref PathOwner pathOwner)
			{
				if (passengers.Length == 0)
				{
					m_PathElements[vehicleEntity].Clear();
					pathOwner.m_ElementIndex = 0;
					taxi.m_State &= ~TaxiFlags.Disembarking;
					return true;
				}
				return false;
			}

		}

		[BurstCompile]
		private struct UpdateRouteVehiclesJob : IJob
		{
			[ReadOnly] public ComponentLookup<PrefabRef> m_PrefabRefData;
			public NativeQueue<RouteVehicleUpdate> m_RouteVehicleQueue;
			public ComponentLookup<Game.Vehicles.Taxi> m_TaxiData;
			public ComponentLookup<BoardingVehicle> m_BoardingVehicleData;
			public void Execute()
			{
				RouteVehicleUpdate item;
				while (m_RouteVehicleQueue.TryDequeue(out item))
				{
					if (item.m_RemoveVehicle != Entity.Null)
					{
						RemoveVehicle(item.m_RemoveVehicle, item.m_Route);
					}
					if (item.m_AddVehicle != Entity.Null)
					{
						AddVehicle(item.m_AddVehicle, item.m_Route);
					}
				}
			}

			private void RemoveVehicle(Entity vehicle, Entity route)
			{
				if (m_BoardingVehicleData.HasComponent(route))
				{
					BoardingVehicle value = m_BoardingVehicleData[route];
					if (value.m_Vehicle == vehicle)
					{
						value.m_Vehicle = Entity.Null;
						m_BoardingVehicleData[route] = value;
					}
				}
				Game.Vehicles.Taxi value2 = m_TaxiData[vehicle];
				if ((value2.m_State & TaxiFlags.Boarding) != 0)
				{
					value2.m_State &= ~TaxiFlags.Boarding;
					m_TaxiData[vehicle] = value2;
				}
			}

			private void AddVehicle(Entity vehicle, Entity route)
			{
				if (m_BoardingVehicleData.HasComponent(route))
				{
					BoardingVehicle value = m_BoardingVehicleData[route];
					if (value.m_Vehicle != vehicle)
					{
						if (value.m_Vehicle != Entity.Null && m_TaxiData.TryGetComponent(value.m_Vehicle, out var componentData) && (componentData.m_State & TaxiFlags.Boarding) != 0)
						{
							return;
						}
						value.m_Vehicle = vehicle;
						m_BoardingVehicleData[route] = value;
					}
				}
				Game.Vehicles.Taxi value2 = m_TaxiData[vehicle];
				if ((value2.m_State & TaxiFlags.Boarding) == 0)
				{
					value2.m_State |= TaxiFlags.Boarding;
					value2.m_MaxBoardingDistance = 0f;
					value2.m_MinWaitingDistance = float.MaxValue;
					m_TaxiData[vehicle] = value2;
				}
			}
		}

		#endregion

		#region Helpers

		private struct RouteVehicleUpdate
		{
			public Entity m_Route;
			public Entity m_AddVehicle;
			public Entity m_RemoveVehicle;

			public static RouteVehicleUpdate Remove(Entity route, Entity vehicle)
			{
				return new RouteVehicleUpdate
				{
					m_Route = route,
					m_RemoveVehicle = vehicle
				};
			}

			public static RouteVehicleUpdate Add(Entity route, Entity vehicle)
			{
				return new RouteVehicleUpdate
				{
					m_Route = route,
					m_AddVehicle = vehicle
				};
			}
		}

		#endregion
	}
}
