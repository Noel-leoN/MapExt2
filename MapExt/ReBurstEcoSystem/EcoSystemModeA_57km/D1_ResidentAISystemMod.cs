using System.Runtime.CompilerServices;
using System.Threading;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
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
using Game.Events;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Reflection;
using Game.Rendering;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;

namespace MapExtPDX.MapExt.ReBurstEcoSystemModeA
{

	public partial class ResidentAISystem : GameSystemBase
	{

		public partial class Actions : GameSystemBase
		{
			private struct TypeHandle
			{
				[ReadOnly]
				public ComponentLookup<Unspawned> __Game_Objects_Unspawned_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<ObjectGeometryData> __Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<TaxiData> __Game_Prefabs_TaxiData_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<PublicTransportVehicleData> __Game_Prefabs_PublicTransportVehicleData_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<PersonalCarData> __Game_Prefabs_PersonalCarData_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<Target> __Game_Common_Target_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<Connected> __Game_Routes_Connected_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<Owner> __Game_Common_Owner_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<HumanCurrentLane> __Game_Creatures_HumanCurrentLane_RO_ComponentLookup;

				[ReadOnly]
				public ComponentLookup<Bicycle> __Game_Vehicles_Bicycle_RO_ComponentLookup;

				[ReadOnly]
				public BufferLookup<GroupCreature> __Game_Creatures_GroupCreature_RO_BufferLookup;

				[ReadOnly]
				public BufferLookup<LayoutElement> __Game_Vehicles_LayoutElement_RO_BufferLookup;

				[ReadOnly]
				public BufferLookup<ActivityLocationElement> __Game_Prefabs_ActivityLocationElement_RO_BufferLookup;

				public ComponentLookup<Citizen> __Game_Citizens_Citizen_RW_ComponentLookup;

				public ComponentLookup<Game.Creatures.Resident> __Game_Creatures_Resident_RW_ComponentLookup;

				public ComponentLookup<Creature> __Game_Creatures_Creature_RW_ComponentLookup;

				public ComponentLookup<Game.Vehicles.Taxi> __Game_Vehicles_Taxi_RW_ComponentLookup;

				public ComponentLookup<Game.Vehicles.PublicTransport> __Game_Vehicles_PublicTransport_RW_ComponentLookup;

				public ComponentLookup<WaitingPassengers> __Game_Routes_WaitingPassengers_RW_ComponentLookup;

				public BufferLookup<Queue> __Game_Creatures_Queue_RW_BufferLookup;

				public BufferLookup<Passenger> __Game_Vehicles_Passenger_RW_BufferLookup;

				public BufferLookup<LaneObject> __Game_Net_LaneObject_RW_BufferLookup;

				public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferLookup;

				public ComponentLookup<PlayerMoney> __Game_City_PlayerMoney_RW_ComponentLookup;

				public BufferLookup<PlaybackLayer> __Game_Rendering_PlaybackLayer_RW_BufferLookup;

				[ReadOnly]
				public ComponentLookup<MailBoxData> __Game_Prefabs_MailBoxData_RO_ComponentLookup;

				public ComponentLookup<HouseholdNeed> __Game_Citizens_HouseholdNeed_RW_ComponentLookup;

				public ComponentLookup<Game.Routes.MailBox> __Game_Routes_MailBox_RW_ComponentLookup;

				public ComponentLookup<MailSender> __Game_Citizens_MailSender_RW_ComponentLookup;

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				public void __AssignHandles(ref SystemState state)
				{
					this.__Game_Objects_Unspawned_RO_ComponentLookup = state.GetComponentLookup<Unspawned>(isReadOnly: true);
					this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true);
					this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(isReadOnly: true);
					this.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup = state.GetComponentLookup<ObjectGeometryData>(isReadOnly: true);
					this.__Game_Prefabs_TaxiData_RO_ComponentLookup = state.GetComponentLookup<TaxiData>(isReadOnly: true);
					this.__Game_Prefabs_PublicTransportVehicleData_RO_ComponentLookup = state.GetComponentLookup<PublicTransportVehicleData>(isReadOnly: true);
					this.__Game_Prefabs_PersonalCarData_RO_ComponentLookup = state.GetComponentLookup<PersonalCarData>(isReadOnly: true);
					this.__Game_Common_Target_RO_ComponentLookup = state.GetComponentLookup<Target>(isReadOnly: true);
					this.__Game_Routes_Connected_RO_ComponentLookup = state.GetComponentLookup<Connected>(isReadOnly: true);
					this.__Game_Common_Owner_RO_ComponentLookup = state.GetComponentLookup<Owner>(isReadOnly: true);
					this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(isReadOnly: true);
					this.__Game_Creatures_HumanCurrentLane_RO_ComponentLookup = state.GetComponentLookup<HumanCurrentLane>(isReadOnly: true);
					this.__Game_Vehicles_Bicycle_RO_ComponentLookup = state.GetComponentLookup<Bicycle>(isReadOnly: true);
					this.__Game_Creatures_GroupCreature_RO_BufferLookup = state.GetBufferLookup<GroupCreature>(isReadOnly: true);
					this.__Game_Vehicles_LayoutElement_RO_BufferLookup = state.GetBufferLookup<LayoutElement>(isReadOnly: true);
					this.__Game_Prefabs_ActivityLocationElement_RO_BufferLookup = state.GetBufferLookup<ActivityLocationElement>(isReadOnly: true);
					this.__Game_Citizens_Citizen_RW_ComponentLookup = state.GetComponentLookup<Citizen>();
					this.__Game_Creatures_Resident_RW_ComponentLookup = state.GetComponentLookup<Game.Creatures.Resident>();
					this.__Game_Creatures_Creature_RW_ComponentLookup = state.GetComponentLookup<Creature>();
					this.__Game_Vehicles_Taxi_RW_ComponentLookup = state.GetComponentLookup<Game.Vehicles.Taxi>();
					this.__Game_Vehicles_PublicTransport_RW_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PublicTransport>();
					this.__Game_Routes_WaitingPassengers_RW_ComponentLookup = state.GetComponentLookup<WaitingPassengers>();
					this.__Game_Creatures_Queue_RW_BufferLookup = state.GetBufferLookup<Queue>();
					this.__Game_Vehicles_Passenger_RW_BufferLookup = state.GetBufferLookup<Passenger>();
					this.__Game_Net_LaneObject_RW_BufferLookup = state.GetBufferLookup<LaneObject>();
					this.__Game_Economy_Resources_RW_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>();
					this.__Game_City_PlayerMoney_RW_ComponentLookup = state.GetComponentLookup<PlayerMoney>();
					this.__Game_Rendering_PlaybackLayer_RW_BufferLookup = state.GetBufferLookup<PlaybackLayer>();
					this.__Game_Prefabs_MailBoxData_RO_ComponentLookup = state.GetComponentLookup<MailBoxData>(isReadOnly: true);
					this.__Game_Citizens_HouseholdNeed_RW_ComponentLookup = state.GetComponentLookup<HouseholdNeed>();
					this.__Game_Routes_MailBox_RW_ComponentLookup = state.GetComponentLookup<Game.Routes.MailBox>();
					this.__Game_Citizens_MailSender_RW_ComponentLookup = state.GetComponentLookup<MailSender>();
				}
			}

			private EndFrameBarrier m_EndFrameBarrier;

			private Game.Objects.SearchSystem m_ObjectSearchSystem;

			private CityStatisticsSystem m_CityStatisticsSystem;

			private TransportUsageTrackSystem m_TransportUsageTrackSystem;

			private CitySystem m_CitySystem;

			private ServiceFeeSystem m_ServiceFeeSystem;

			private ComponentTypeSet m_CurrentLaneTypes;

			private ComponentTypeSet m_CurrentLaneTypesRelative;

			public NativeQueue<Boarding> m_BoardingQueue;

			public NativeQueue<ResidentAction> m_ActionQueue;

			public JobHandle m_Dependency;

			private TypeHandle __TypeHandle;

			[Preserve]
			protected override void OnCreate()
			{
				base.OnCreate();
				this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
				this.m_ObjectSearchSystem = base.World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
				this.m_CityStatisticsSystem = base.World.GetOrCreateSystemManaged<CityStatisticsSystem>();
				this.m_TransportUsageTrackSystem = base.World.GetOrCreateSystemManaged<TransportUsageTrackSystem>();
				this.m_CitySystem = base.World.GetOrCreateSystemManaged<CitySystem>();
				this.m_ServiceFeeSystem = base.World.GetOrCreateSystemManaged<ServiceFeeSystem>();
				this.m_CurrentLaneTypes = new ComponentTypeSet(new ComponentType[6]
				{
					ComponentType.ReadWrite<Moving>(),
					ComponentType.ReadWrite<TransformFrame>(),
					ComponentType.ReadWrite<InterpolatedTransform>(),
					ComponentType.ReadWrite<HumanNavigation>(),
					ComponentType.ReadWrite<HumanCurrentLane>(),
					ComponentType.ReadWrite<Blocker>()
				});
				this.m_CurrentLaneTypesRelative = new ComponentTypeSet(new ComponentType[5]
				{
					ComponentType.ReadWrite<Moving>(),
					ComponentType.ReadWrite<TransformFrame>(),
					ComponentType.ReadWrite<HumanNavigation>(),
					ComponentType.ReadWrite<HumanCurrentLane>(),
					ComponentType.ReadWrite<Blocker>()
				});
			}

			[Preserve]
			protected override void OnUpdate()
			{
				JobHandle jobHandle = JobHandle.CombineDependencies(base.Dependency, this.m_Dependency);
				JobHandle dependencies;
				JobHandle deps;
				JobHandle deps2;
				JobHandle deps3;
				BoardingJob jobData = new BoardingJob
				{
					m_Unspawneds = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Unspawned_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Transforms = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabRefData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef),
					m_ObjectGeometryData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_TaxiData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_TaxiData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PublicTransportVehicleData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PublicTransportVehicleData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabPersonalCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PersonalCarData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Targets = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Target_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Connecteds = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_Connected_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Owners = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Owner_RO_ComponentLookup, ref base.CheckedStateRef),
					m_OutsideConnections = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef),
					m_HumanCurrentLanes = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_HumanCurrentLane_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Bicycles = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Bicycle_RO_ComponentLookup, ref base.CheckedStateRef),
					m_GroupCreatures = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Creatures_GroupCreature_RO_BufferLookup, ref base.CheckedStateRef),
					m_VehicleLayouts = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Vehicles_LayoutElement_RO_BufferLookup, ref base.CheckedStateRef),
					m_ActivityLocations = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Prefabs_ActivityLocationElement_RO_BufferLookup, ref base.CheckedStateRef),
					m_Citizens = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentLookup, ref base.CheckedStateRef),
					m_Residents = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_Resident_RW_ComponentLookup, ref base.CheckedStateRef),
					m_Creatures = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_Creature_RW_ComponentLookup, ref base.CheckedStateRef),
					m_Taxis = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Taxi_RW_ComponentLookup, ref base.CheckedStateRef),
					m_PublicTransports = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_PublicTransport_RW_ComponentLookup, ref base.CheckedStateRef),
					m_WaitingPassengers = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_WaitingPassengers_RW_ComponentLookup, ref base.CheckedStateRef),
					m_Queues = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Creatures_Queue_RW_BufferLookup, ref base.CheckedStateRef),
					m_Passengers = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Vehicles_Passenger_RW_BufferLookup, ref base.CheckedStateRef),
					m_LaneObjects = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Net_LaneObject_RW_BufferLookup, ref base.CheckedStateRef),
					m_Resources = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferLookup, ref base.CheckedStateRef),
					m_PlayerMoney = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_City_PlayerMoney_RW_ComponentLookup, ref base.CheckedStateRef),
					m_PlaybackLayers = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Rendering_PlaybackLayer_RW_BufferLookup, ref base.CheckedStateRef),
					m_City = this.m_CitySystem.City,
					m_CurrentLaneTypes = this.m_CurrentLaneTypes,
					m_CurrentLaneTypesRelative = this.m_CurrentLaneTypesRelative,
					m_BoardingQueue = this.m_BoardingQueue,
					m_SearchTree = this.m_ObjectSearchSystem.GetMovingSearchTree(readOnly: false, out dependencies),
					m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer(),
					m_StatisticsEventQueue = this.m_CityStatisticsSystem.GetStatisticsEventQueue(out deps).AsParallelWriter(),
					m_TransportUsageQueue = this.m_TransportUsageTrackSystem.GetQueue(out deps2),
					m_FeeQueue = this.m_ServiceFeeSystem.GetFeeQueue(out deps3)
				};
				ResidentActionJob jobData2 = new ResidentActionJob
				{
					m_PrefabRefData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabMailBoxData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_MailBoxData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_HouseholdNeedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdNeed_RW_ComponentLookup, ref base.CheckedStateRef),
					m_MailBoxData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_MailBox_RW_ComponentLookup, ref base.CheckedStateRef),
					m_MailSenderData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_MailSender_RW_ComponentLookup, ref base.CheckedStateRef),
					m_ActionQueue = this.m_ActionQueue,
					m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer()
				};
				JobHandle jobHandle2 = IJobExtensions.Schedule(jobData, JobUtils.CombineDependencies(jobHandle, dependencies, deps, deps3, deps2));
				JobHandle jobHandle3 = IJobExtensions.Schedule(jobData2, jobHandle);
				this.m_BoardingQueue.Dispose(jobHandle2);
				this.m_ActionQueue.Dispose(jobHandle3);
				this.m_CityStatisticsSystem.AddWriter(jobHandle2);
				this.m_TransportUsageTrackSystem.AddQueueWriter(jobHandle2);
				this.m_ObjectSearchSystem.AddMovingSearchTreeWriter(jobHandle2);
				this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
				this.m_ServiceFeeSystem.AddQueueWriter(jobHandle2);
				this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle3);
				base.Dependency = JobHandle.CombineDependencies(jobHandle2, jobHandle3);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private void __AssignQueries(ref SystemState state)
			{
				new EntityQueryBuilder(Allocator.Temp).Dispose();
			}

		}

		public struct Boarding
		{
			public Entity m_Passenger;

			public Entity m_Leader;

			public Entity m_Household;

			public Entity m_Vehicle;

			public Entity m_LeaderVehicle;

			public Entity m_Waypoint;

			public HumanCurrentLane m_CurrentLane;

			public CreatureVehicleFlags m_Flags;

			public float3 m_Position;

			public quaternion m_Rotation;

			public int m_TicketPrice;

			public BoardingType m_Type;

			public static Boarding ExitVehicle(Entity passenger, Entity household, Entity leader, Entity vehicle, HumanCurrentLane newCurrentLane, float3 position, quaternion rotation, int ticketPrice)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Household = household,
					m_Leader = leader,
					m_Vehicle = vehicle,
					m_CurrentLane = newCurrentLane,
					m_Position = position,
					m_Rotation = rotation,
					m_TicketPrice = ticketPrice,
					m_Type = BoardingType.Exit
				};
			}

			public static Boarding TryEnterVehicle(Entity passenger, Entity leader, Entity vehicle, Entity leaderVehicle, Entity waypoint, float3 position, CreatureVehicleFlags flags)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Leader = leader,
					m_Vehicle = vehicle,
					m_LeaderVehicle = leaderVehicle,
					m_Waypoint = waypoint,
					m_Position = position,
					m_Flags = flags,
					m_Type = BoardingType.TryEnter
				};
			}

			public static Boarding FinishEnterVehicle(Entity passenger, Entity household, Entity vehicle, Entity controllerVehicle, HumanCurrentLane oldCurrentLane, int ticketPrice)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Household = household,
					m_Vehicle = vehicle,
					m_LeaderVehicle = controllerVehicle,
					m_CurrentLane = oldCurrentLane,
					m_TicketPrice = ticketPrice,
					m_Type = BoardingType.FinishEnter
				};
			}

			public static Boarding CancelEnterVehicle(Entity passenger, Entity vehicle)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Vehicle = vehicle,
					m_Type = BoardingType.CancelEnter
				};
			}

			public static Boarding RequireStop(Entity passenger, Entity vehicle, float3 position)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Vehicle = vehicle,
					m_Position = position,
					m_Type = BoardingType.RequireStop
				};
			}

			public static Boarding WaitTimeExceeded(Entity passenger, Entity waypoint)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Waypoint = waypoint,
					m_Type = BoardingType.WaitTimeExceeded
				};
			}

			public static Boarding WaitTimeEstimate(Entity waypoint, int seconds)
			{
				return new Boarding
				{
					m_Waypoint = waypoint,
					m_TicketPrice = seconds,
					m_Type = BoardingType.WaitTimeEstimate
				};
			}

			public static Boarding FinishExitVehicle(Entity passenger, Entity vehicle)
			{
				return new Boarding
				{
					m_Passenger = passenger,
					m_Vehicle = vehicle,
					m_Type = BoardingType.FinishExit
				};
			}
		}

		public struct ResidentAction
		{
			public Entity m_Citizen;

			public Entity m_Target;

			public Entity m_Household;

			public Resource m_Resource;

			public ResidentActionType m_Type;

			public int m_Amount;

			public float m_Distance;
		}

		public enum BoardingType
		{
			Exit = 0,
			TryEnter = 1,
			FinishEnter = 2,
			CancelEnter = 3,
			RequireStop = 4,
			WaitTimeExceeded = 5,
			WaitTimeEstimate = 6,
			FinishExit = 7
		}

		public enum ResidentActionType
		{
			SendMail = 0,
			GoShopping = 1
		}

		private enum DeletedResidentType
		{
			StuckLoop = 0,
			NoPathToHome = 1,
			NoPathToHome_AlreadyOutside = 2,
			WaitingHome_AlreadyOutside = 3,
			NoPath_AlreadyMovingAway = 4,
			InvalidVehicleTarget = 5,
			Dead = 6,
			Count = 7
		}

		[BurstCompile]
		private struct ResidentTickJob : IJobChunk
		{
			private struct TransportEstimateBuffer : RouteUtils.ITransportEstimateBuffer
			{
				public NativeQueue<Boarding>.ParallelWriter m_BoardingQueue;

				public void AddWaitEstimate(Entity waypoint, int seconds)
				{
					this.m_BoardingQueue.Enqueue(Boarding.WaitTimeEstimate(waypoint, seconds));
				}
			}

			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			[ReadOnly]
			public ComponentTypeHandle<CurrentVehicle> m_CurrentVehicleType;

			[ReadOnly]
			public ComponentTypeHandle<GroupMember> m_GroupMemberType;

			[ReadOnly]
			public ComponentTypeHandle<Unspawned> m_UnspawnedType;

			[ReadOnly]
			public ComponentTypeHandle<HumanNavigation> m_HumanNavigationType;

			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

			[ReadOnly]
			public BufferTypeHandle<GroupCreature> m_GroupCreatureType;

			public ComponentTypeHandle<Game.Creatures.Resident> m_ResidentType;

			public ComponentTypeHandle<Creature> m_CreatureType;

			[NativeDisableContainerSafetyRestriction]
			public ComponentTypeHandle<Human> m_HumanType;

			public ComponentTypeHandle<HumanCurrentLane> m_CurrentLaneType;

			[NativeDisableContainerSafetyRestriction]
			public ComponentTypeHandle<Target> m_TargetType;

			public ComponentTypeHandle<Divert> m_DivertType;

			[ReadOnly]
			public EntityStorageInfoLookup m_EntityLookup;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_TransformData;

			[ReadOnly]
			public ComponentLookup<Owner> m_OwnerData;

			[NativeDisableContainerSafetyRestriction]
			[ReadOnly]
			public ComponentLookup<Target> m_TargetData;

			[ReadOnly]
			public ComponentLookup<PseudoRandomSeed> m_PseudoRandomSeedData;

			[ReadOnly]
			public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;

			[ReadOnly]
			public ComponentLookup<Destroyed> m_DestroyedData;

			[ReadOnly]
			public ComponentLookup<Deleted> m_DeletedData;

			[ReadOnly]
			public ComponentLookup<Unspawned> m_UnspawnedData;

			[ReadOnly]
			public ComponentLookup<RideNeeder> m_RideNeederData;

			[ReadOnly]
			public ComponentLookup<Dispatched> m_Dispatched;

			[ReadOnly]
			public ComponentLookup<ServiceRequest> m_ServiceRequestData;

			[ReadOnly]
			public ComponentLookup<Moving> m_MovingData;

			[ReadOnly]
			public ComponentLookup<Game.Objects.SpawnLocation> m_SpawnLocation;

			[ReadOnly]
			public ComponentLookup<Animal> m_AnimalData;

			[ReadOnly]
			public ComponentLookup<OnFire> m_OnFireData;

			[ReadOnly]
			public ComponentLookup<Game.Net.Edge> m_EdgeData;

			[ReadOnly]
			public ComponentLookup<Curve> m_CurveData;

			[ReadOnly]
			public ComponentLookup<Lane> m_LaneData;

			[ReadOnly]
			public ComponentLookup<EdgeLane> m_EdgeLaneData;

			[ReadOnly]
			public ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneData;

			[ReadOnly]
			public ComponentLookup<GarageLane> m_GarageLaneData;

			[ReadOnly]
			public ComponentLookup<Game.Net.PedestrianLane> m_PedestrianLaneData;

			[ReadOnly]
			public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;

			[ReadOnly]
			public ComponentLookup<LaneSignal> m_LaneSignalData;

			[ReadOnly]
			public ComponentLookup<HangaroundLocation> m_HangaroundLocationData;

			[ReadOnly]
			public ComponentLookup<Citizen> m_CitizenData;

			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			[ReadOnly]
			public ComponentLookup<Household> m_HouseholdData;

			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			[ReadOnly]
			public ComponentLookup<CurrentTransport> m_CurrentTransportData;

			[ReadOnly]
			public ComponentLookup<Worker> m_WorkerData;

			[ReadOnly]
			public ComponentLookup<CarKeeper> m_CarKeeperData;

			[ReadOnly]
			public ComponentLookup<BicycleOwner> m_BicycleOwnerData;

			[ReadOnly]
			public ComponentLookup<HealthProblem> m_HealthProblemData;

			[ReadOnly]
			public ComponentLookup<TravelPurpose> m_TravelPurposeData;

			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			[ReadOnly]
			public ComponentLookup<HomelessHousehold> m_HomelessHouseholdData;

			[ReadOnly]
			public ComponentLookup<HouseholdNeed> m_HouseholdNeedData;

			[ReadOnly]
			public ComponentLookup<AttendingMeeting> m_AttendingMeetingData;

			[ReadOnly]
			public ComponentLookup<CoordinatedMeeting> m_CoordinatedMeetingData;

			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAwayData;

			[ReadOnly]
			public ComponentLookup<ServiceAvailable> m_ServiceAvailableData;

			[ReadOnly]
			public ComponentLookup<ParkedCar> m_ParkedCarData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.Taxi> m_TaxiData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransportData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PoliceCar> m_PoliceCarData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.Hearse> m_HearseData;

			[ReadOnly]
			public ComponentLookup<Controller> m_ControllerData;

			[ReadOnly]
			public ComponentLookup<Vehicle> m_VehicleData;

			[ReadOnly]
			public ComponentLookup<Train> m_TrainData;

			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			[ReadOnly]
			public ComponentLookup<AttractivenessProvider> m_AttractivenessProviderData;

			[ReadOnly]
			public ComponentLookup<Connected> m_RouteConnectedData;

			[ReadOnly]
			public ComponentLookup<BoardingVehicle> m_BoardingVehicleData;

			[ReadOnly]
			public ComponentLookup<CurrentRoute> m_CurrentRouteData;

			[ReadOnly]
			public ComponentLookup<TransportLine> m_TransportLineData;

			[ReadOnly]
			public ComponentLookup<AccessLane> m_AccessLaneLaneData;

			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			[ReadOnly]
			public ComponentLookup<CreatureData> m_PrefabCreatureData;

			[ReadOnly]
			public ComponentLookup<HumanData> m_PrefabHumanData;

			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;

			[ReadOnly]
			public ComponentLookup<CarData> m_PrefabCarData;

			[ReadOnly]
			public ComponentLookup<IndustrialProcessData> m_PrefabIndustrialProcessData;

			[ReadOnly]
			public ComponentLookup<TransportStopData> m_PrefabTransportStopData;

			[ReadOnly]
			public ComponentLookup<SpawnLocationData> m_PrefabSpawnLocationData;

			[ReadOnly]
			public BufferLookup<HouseholdAnimal> m_HouseholdAnimals;

			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			[ReadOnly]
			public BufferLookup<ConnectedRoute> m_ConnectedRoutes;

			[ReadOnly]
			public BufferLookup<RouteWaypoint> m_RouteWaypoints;

			[ReadOnly]
			public BufferLookup<LayoutElement> m_VehicleLayouts;

			[ReadOnly]
			public BufferLookup<CarNavigationLane> m_CarNavigationLanes;

			[ReadOnly]
			public BufferLookup<ConnectedEdge> m_ConnectedEdges;

			[ReadOnly]
			public BufferLookup<Game.Net.SubLane> m_SubLanes;

			[ReadOnly]
			public BufferLookup<Game.Areas.Node> m_AreaNodes;

			[ReadOnly]
			public BufferLookup<Triangle> m_AreaTriangles;

			[ReadOnly]
			public BufferLookup<ConnectedBuilding> m_ConnectedBuildings;

			[ReadOnly]
			public BufferLookup<Renter> m_Renters;

			[ReadOnly]
			public BufferLookup<SpawnLocationElement> m_SpawnLocationElements;

			[ReadOnly]
			public BufferLookup<Game.Economy.Resources> m_Resources;

			[ReadOnly]
			public BufferLookup<ServiceDispatch> m_ServiceDispatches;

			[ReadOnly]
			public BufferLookup<ActivityLocationElement> m_PrefabActivityLocationElements;

			[NativeDisableContainerSafetyRestriction]
			public ComponentLookup<Human> m_HumanData;

			[NativeDisableParallelForRestriction]
			public ComponentLookup<PathOwner> m_PathOwnerData;

			[NativeDisableParallelForRestriction]
			public BufferLookup<PathElement> m_PathElements;

			[ReadOnly]
			public float m_TimeOfDay;

			[ReadOnly]
			public RandomSeed m_RandomSeed;

			[ReadOnly]
			public uint m_SimulationFrameIndex;

			[ReadOnly]
			public bool m_LefthandTraffic;

			[ReadOnly]
			public bool m_GroupMember;

			[ReadOnly]
			public PersonalCarSelectData m_PersonalCarSelectData;

			[ReadOnly]
			public EntityArchetype m_ResetTripArchetype;

			[ReadOnly]
			public ComponentTypeSet m_ParkedToMovingCarRemoveTypes;

			[ReadOnly]
			public ComponentTypeSet m_ParkedToMovingCarAddTypes;

			[ReadOnly]
			public ComponentTypeSet m_ParkedToMovingTrailerAddTypes;

			[ReadOnly]
			public NativeArray<int> m_DeletedResidents;

			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

			public NativeQueue<Boarding>.ParallelWriter m_BoardingQueue;

			public NativeQueue<ResidentAction>.ParallelWriter m_ActionQueue;

			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabRefType);
				NativeArray<Creature> nativeArray3 = chunk.GetNativeArray(ref this.m_CreatureType);
				NativeArray<Game.Creatures.Resident> nativeArray4 = chunk.GetNativeArray(ref this.m_ResidentType);
				NativeArray<Target> nativeArray5 = chunk.GetNativeArray(ref this.m_TargetType);
				NativeArray<CurrentVehicle> nativeArray6 = chunk.GetNativeArray(ref this.m_CurrentVehicleType);
				NativeArray<HumanCurrentLane> nativeArray7 = chunk.GetNativeArray(ref this.m_CurrentLaneType);
				NativeArray<HumanNavigation> nativeArray8 = chunk.GetNativeArray(ref this.m_HumanNavigationType);
				NativeArray<Divert> nativeArray9 = chunk.GetNativeArray(ref this.m_DivertType);
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				if (this.m_GroupMember)
				{
					NativeArray<GroupMember> nativeArray10 = chunk.GetNativeArray(ref this.m_GroupMemberType);
					RefRW<Human> refRW;
					if (nativeArray6.Length != 0)
					{
						for (int i = 0; i < nativeArray.Length; i++)
						{
							Entity entity = nativeArray[i];
							PrefabRef prefabRef = nativeArray2[i];
							Game.Creatures.Resident resident = nativeArray4[i];
							Creature creature = nativeArray3[i];
							CurrentVehicle currentVehicle = nativeArray6[i];
							Target target = nativeArray5[i];
							GroupMember groupMember = nativeArray10[i];
							CollectionUtils.TryGet(nativeArray7, i, out var value);
							CollectionUtils.TryGet(nativeArray8, i, out var value2);
							CollectionUtils.TryGet(nativeArray9, i, out var value3);
							PathOwner pathOwner = this.m_PathOwnerData[entity];
							refRW = this.m_HumanData.GetRefRW(entity);
							ref Human valueRW = ref refRW.ValueRW;
							this.TickGroupMemberInVehicle(unfilteredChunkIndex, ref random, entity, prefabRef, value2, groupMember, currentVehicle, nativeArray7.Length != 0, ref resident, ref valueRW, ref value, ref pathOwner, ref target, ref value3);
							this.TickQueue(ref random, ref resident, ref creature, ref value);
							this.m_PathOwnerData[entity] = pathOwner;
							nativeArray4[i] = resident;
							nativeArray3[i] = creature;
							nativeArray5[i] = target;
							CollectionUtils.TrySet(nativeArray7, i, value);
							CollectionUtils.TrySet(nativeArray9, i, value3);
						}
						return;
					}
					bool isUnspawned = chunk.Has(ref this.m_UnspawnedType);
					for (int j = 0; j < nativeArray.Length; j++)
					{
						Entity entity2 = nativeArray[j];
						PrefabRef prefabRef2 = nativeArray2[j];
						Game.Creatures.Resident resident2 = nativeArray4[j];
						Creature creature2 = nativeArray3[j];
						HumanNavigation navigation = nativeArray8[j];
						Target target2 = nativeArray5[j];
						GroupMember groupMember2 = nativeArray10[j];
						CollectionUtils.TryGet(nativeArray7, j, out var value4);
						CollectionUtils.TryGet(nativeArray9, j, out var value5);
						PathOwner pathOwner2 = this.m_PathOwnerData[entity2];
						refRW = this.m_HumanData.GetRefRW(entity2);
						ref Human valueRW2 = ref refRW.ValueRW;
						CreatureUtils.CheckUnspawned(unfilteredChunkIndex, entity2, value4, valueRW2, isUnspawned, this.m_CommandBuffer);
						this.TickGroupMemberWalking(unfilteredChunkIndex, ref random, entity2, prefabRef2, navigation, groupMember2, ref resident2, ref creature2, ref valueRW2, ref value4, ref pathOwner2, ref target2, ref value5);
						this.TickQueue(ref random, ref resident2, ref creature2, ref value4);
						this.m_PathOwnerData[entity2] = pathOwner2;
						nativeArray4[j] = resident2;
						nativeArray3[j] = creature2;
						nativeArray5[j] = target2;
						CollectionUtils.TrySet(nativeArray7, j, value4);
						CollectionUtils.TrySet(nativeArray9, j, value5);
					}
					return;
				}
				NativeArray<Human> nativeArray11 = chunk.GetNativeArray(ref this.m_HumanType);
				BufferAccessor<GroupCreature> bufferAccessor = chunk.GetBufferAccessor(ref this.m_GroupCreatureType);
				if (nativeArray6.Length != 0)
				{
					for (int k = 0; k < nativeArray.Length; k++)
					{
						Entity entity3 = nativeArray[k];
						PrefabRef prefabRef3 = nativeArray2[k];
						Game.Creatures.Resident resident3 = nativeArray4[k];
						Creature creature3 = nativeArray3[k];
						Human human = nativeArray11[k];
						CurrentVehicle currentVehicle2 = nativeArray6[k];
						Target target3 = nativeArray5[k];
						CollectionUtils.TryGet(nativeArray7, k, out var value6);
						CollectionUtils.TryGet(nativeArray8, k, out var value7);
						CollectionUtils.TryGet(nativeArray9, k, out var value8);
						CollectionUtils.TryGet(bufferAccessor, k, out var value9);
						PathOwner pathOwner3 = this.m_PathOwnerData[entity3];
						this.TickInVehicle(unfilteredChunkIndex, ref random, entity3, prefabRef3, value7, currentVehicle2, nativeArray7.Length != 0, ref resident3, ref creature3, ref human, ref value6, ref pathOwner3, ref target3, ref value8, value9);
						this.TickQueue(ref random, ref resident3, ref creature3, ref value6);
						this.m_PathOwnerData[entity3] = pathOwner3;
						nativeArray4[k] = resident3;
						nativeArray3[k] = creature3;
						nativeArray11[k] = human;
						nativeArray5[k] = target3;
						CollectionUtils.TrySet(nativeArray7, k, value6);
						CollectionUtils.TrySet(nativeArray9, k, value8);
					}
					return;
				}
				bool isUnspawned2 = chunk.Has(ref this.m_UnspawnedType);
				for (int l = 0; l < nativeArray.Length; l++)
				{
					Entity entity4 = nativeArray[l];
					PrefabRef prefabRef4 = nativeArray2[l];
					Game.Creatures.Resident resident4 = nativeArray4[l];
					Creature creature4 = nativeArray3[l];
					Human human2 = nativeArray11[l];
					HumanNavigation navigation2 = nativeArray8[l];
					Target target4 = nativeArray5[l];
					CollectionUtils.TryGet(nativeArray7, l, out var value10);
					CollectionUtils.TryGet(nativeArray9, l, out var value11);
					CollectionUtils.TryGet(bufferAccessor, l, out var value12);
					PathOwner pathOwner4 = this.m_PathOwnerData[entity4];
					CreatureUtils.CheckUnspawned(unfilteredChunkIndex, entity4, value10, human2, isUnspawned2, this.m_CommandBuffer);
					this.TickWalking(unfilteredChunkIndex, ref random, entity4, prefabRef4, navigation2, isUnspawned2, ref resident4, ref creature4, ref human2, ref value10, ref pathOwner4, ref target4, ref value11, value12);
					this.TickQueue(ref random, ref resident4, ref creature4, ref value10);
					this.m_PathOwnerData[entity4] = pathOwner4;
					nativeArray4[l] = resident4;
					nativeArray3[l] = creature4;
					nativeArray11[l] = human2;
					nativeArray5[l] = target4;
					CollectionUtils.TrySet(nativeArray7, l, value10);
					CollectionUtils.TrySet(nativeArray9, l, value11);
				}
			}

			private void TickGroupMemberInVehicle(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, PrefabRef prefabRef, HumanNavigation navigation, GroupMember groupMember, CurrentVehicle currentVehicle, bool hasCurrentLane, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target, ref Divert divert)
			{
				if (!this.m_EntityLookup.Exists(currentVehicle.m_Vehicle))
				{
					this.AddDeletedResident(DeletedResidentType.InvalidVehicleTarget);
					this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
					return;
				}
				Entity entity2 = currentVehicle.m_Vehicle;
				if (this.m_ControllerData.TryGetComponent(currentVehicle.m_Vehicle, out var componentData) && componentData.m_Controller != Entity.Null)
				{
					entity2 = componentData.m_Controller;
				}
				if ((currentVehicle.m_Flags & CreatureVehicleFlags.Ready) == 0)
				{
					if (hasCurrentLane)
					{
						if (CreatureUtils.IsStuck(pathOwner))
						{
							this.AddDeletedResident(DeletedResidentType.StuckLoop);
							this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
							return;
						}
						if (!this.m_CurrentVehicleData.HasComponent(groupMember.m_Leader))
						{
							this.CancelEnterVehicle(entity, currentVehicle.m_Vehicle, ref resident, ref human, ref currentLane, ref pathOwner);
							return;
						}
						if (this.m_PublicTransportData.TryGetComponent(entity2, out var componentData2))
						{
							if (this.m_SimulationFrameIndex >= componentData2.m_DepartureFrame)
							{
								human.m_Flags |= HumanFlags.Run;
							}
							if ((componentData2.m_State & PublicTransportFlags.Boarding) == 0 && currentLane.m_Lane == currentVehicle.m_Vehicle)
							{
								currentLane.m_Flags |= CreatureLaneFlags.EndReached;
							}
						}
						if (CreatureUtils.ParkingSpaceReached(currentLane) || CreatureUtils.TransportStopReached(currentLane))
						{
							this.SetEnterVehiclePath(entity, currentVehicle.m_Vehicle, groupMember, ref random, ref currentLane, ref pathOwner);
						}
						else if (CreatureUtils.PathEndReached(currentLane) || CreatureUtils.RequireNewPath(pathOwner) || resident.m_Timer >= 250)
						{
							if (ResidentTickJob.ShouldFinishEnterVehicle(navigation))
							{
								this.FinishEnterVehicle(entity, currentVehicle.m_Vehicle, entity2, ref resident, ref human, ref currentLane);
								hasCurrentLane = false;
							}
							else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Entering) == 0)
							{
								currentVehicle.m_Flags |= CreatureVehicleFlags.Entering;
								this.m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
								this.m_CommandBuffer.AddComponent(jobIndex, entity, default(BatchesUpdated));
							}
						}
						else if (CreatureUtils.ActionLocationReached(currentLane) && this.ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
						{
							return;
						}
					}
					if (!hasCurrentLane)
					{
						currentVehicle.m_Flags &= ~CreatureVehicleFlags.Entering;
						currentVehicle.m_Flags |= CreatureVehicleFlags.Ready;
						this.m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
					}
				}
				else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Exiting) != 0)
				{
					if (ResidentTickJob.ShouldFinishExitVehicle(navigation))
					{
						this.FinishExitVehicle(entity, currentVehicle.m_Vehicle, ref currentLane);
					}
				}
				else
				{
					if ((resident.m_Flags & ResidentFlags.Disembarking) == 0 && !this.m_CurrentVehicleData.HasComponent(groupMember.m_Leader))
					{
						this.GroupLeaderDisembarking(entity, ref resident, ref pathOwner);
					}
					if ((resident.m_Flags & ResidentFlags.Disembarking) != ResidentFlags.None)
					{
						this.ExitVehicle(entity, jobIndex, ref random, entity2, prefabRef, currentVehicle, groupMember, ref resident, ref human, ref divert, ref pathOwner);
					}
				}
				this.UpdateMoodFlags(ref random, navigation, hasCurrentLane, ref resident, ref human, ref divert);
			}

			private void TickInVehicle(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, PrefabRef prefabRef, HumanNavigation navigation, CurrentVehicle currentVehicle, bool hasCurrentLane, ref Game.Creatures.Resident resident, ref Creature creature, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target, ref Divert divert, DynamicBuffer<GroupCreature> groupCreatures)
			{
				if (!this.m_PrefabRefData.HasComponent(currentVehicle.m_Vehicle))
				{
					this.AddDeletedResident(DeletedResidentType.InvalidVehicleTarget);
					this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
					return;
				}
				if (CreatureUtils.ResetUpdatedPath(ref pathOwner) && this.CheckPath(jobIndex, entity, prefabRef, ref random, ref creature, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident))
				{
					this.FindNewPath(entity, prefabRef, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
					return;
				}
				Entity entity2 = currentVehicle.m_Vehicle;
				if (this.m_ControllerData.TryGetComponent(currentVehicle.m_Vehicle, out var componentData) && componentData.m_Controller != Entity.Null)
				{
					entity2 = componentData.m_Controller;
				}
				if ((currentVehicle.m_Flags & CreatureVehicleFlags.Ready) == 0)
				{
					if (hasCurrentLane)
					{
						if (CreatureUtils.IsStuck(pathOwner))
						{
							this.AddDeletedResident(DeletedResidentType.StuckLoop);
							this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
							return;
						}
						if (this.m_PublicTransportData.TryGetComponent(entity2, out var componentData2))
						{
							if (this.m_SimulationFrameIndex >= componentData2.m_DepartureFrame)
							{
								human.m_Flags |= HumanFlags.Run;
							}
							if ((componentData2.m_State & PublicTransportFlags.Boarding) == 0)
							{
								if (!(currentLane.m_Lane == currentVehicle.m_Vehicle))
								{
									this.CancelEnterVehicle(entity, currentVehicle.m_Vehicle, ref resident, ref human, ref currentLane, ref pathOwner);
									return;
								}
								currentLane.m_Flags |= CreatureLaneFlags.EndReached;
							}
						}
						if (CreatureUtils.ParkingSpaceReached(currentLane) || CreatureUtils.TransportStopReached(currentLane))
						{
							this.SetEnterVehiclePath(entity, currentVehicle.m_Vehicle, default(GroupMember), ref random, ref currentLane, ref pathOwner);
						}
						else if (CreatureUtils.PathEndReached(currentLane) || CreatureUtils.RequireNewPath(pathOwner) || resident.m_Timer >= 250)
						{
							if (ResidentTickJob.ShouldFinishEnterVehicle(navigation))
							{
								this.FinishEnterVehicle(entity, currentVehicle.m_Vehicle, entity2, ref resident, ref human, ref currentLane);
								hasCurrentLane = false;
							}
							else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Entering) == 0)
							{
								currentVehicle.m_Flags |= CreatureVehicleFlags.Entering;
								this.m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
								this.m_CommandBuffer.AddComponent(jobIndex, entity, default(BatchesUpdated));
							}
						}
						else if (CreatureUtils.ActionLocationReached(currentLane) && this.ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
						{
							return;
						}
					}
					if (!hasCurrentLane && this.HasEveryoneBoarded(groupCreatures))
					{
						currentVehicle.m_Flags &= ~CreatureVehicleFlags.Entering;
						currentVehicle.m_Flags |= CreatureVehicleFlags.Ready;
						this.m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
					}
				}
				else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Exiting) != 0)
				{
					if (ResidentTickJob.ShouldFinishExitVehicle(navigation))
					{
						this.FinishExitVehicle(entity, currentVehicle.m_Vehicle, ref currentLane);
					}
				}
				else
				{
					if ((resident.m_Flags & ResidentFlags.Disembarking) == 0)
					{
						if (this.m_DestroyedData.HasComponent(entity2))
						{
							if (!this.m_MovingData.HasComponent(entity2))
							{
								resident.m_Flags |= ResidentFlags.Disembarking;
								pathOwner.m_State &= ~PathFlags.Failed;
								pathOwner.m_State |= PathFlags.Obsolete;
							}
						}
						else if (this.m_PersonalCarData.HasComponent(entity2))
						{
							Game.Vehicles.PersonalCar personalCar = this.m_PersonalCarData[entity2];
							if ((personalCar.m_State & PersonalCarFlags.Disembarking) != 0)
							{
								this.CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
							else if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0)
							{
								this.CurrentVehicleTransporting(entity, entity2, ref pathOwner);
							}
						}
						else if (this.m_PublicTransportData.HasComponent(entity2))
						{
							Game.Vehicles.PublicTransport publicTransport = this.m_PublicTransportData[entity2];
							if ((publicTransport.m_State & PublicTransportFlags.Boarding) != 0)
							{
								this.CurrentVehicleBoarding(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
							else if ((publicTransport.m_State & (PublicTransportFlags.Testing | PublicTransportFlags.RequireStop)) == PublicTransportFlags.Testing)
							{
								this.CurrentVehicleTesting(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
						}
						else if (this.m_TaxiData.HasComponent(entity2))
						{
							Game.Vehicles.Taxi taxi = this.m_TaxiData[entity2];
							if ((taxi.m_State & TaxiFlags.Disembarking) != 0)
							{
								this.CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
							else if ((taxi.m_State & TaxiFlags.Transporting) != 0)
							{
								this.CurrentVehicleTransporting(entity, entity2, ref pathOwner);
							}
						}
						else if (this.m_PoliceCarData.HasComponent(entity2))
						{
							if ((this.m_PoliceCarData[entity2].m_State & PoliceCarFlags.Disembarking) != 0)
							{
								this.CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
							else
							{
								this.CurrentVehicleTransporting(entity, entity2, ref pathOwner);
							}
						}
						else if (this.m_AmbulanceData.HasComponent(entity2))
						{
							if ((this.m_AmbulanceData[entity2].m_State & AmbulanceFlags.Disembarking) != 0)
							{
								this.CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
							else
							{
								this.CurrentVehicleTransporting(entity, entity2, ref pathOwner);
							}
						}
						else if (this.m_HearseData.HasComponent(entity2))
						{
							if ((this.m_HearseData[entity2].m_State & HearseFlags.Disembarking) != 0)
							{
								this.CurrentVehicleDisembarking(jobIndex, entity, entity2, ref resident, ref pathOwner, ref target);
							}
							else
							{
								this.CurrentVehicleTransporting(entity, entity2, ref pathOwner);
							}
						}
					}
					if ((resident.m_Flags & ResidentFlags.Disembarking) != ResidentFlags.None)
					{
						this.ExitVehicle(entity, jobIndex, ref random, entity2, prefabRef, currentVehicle, default(GroupMember), ref resident, ref human, ref divert, ref pathOwner);
					}
					else if ((currentVehicle.m_Flags & CreatureVehicleFlags.Leader) == 0)
					{
						currentVehicle.m_Flags |= CreatureVehicleFlags.Leader;
						this.m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
					}
				}
				this.UpdateMoodFlags(ref random, navigation, hasCurrentLane, ref resident, ref human, ref divert);
			}

			private static bool ShouldFinishEnterVehicle(HumanNavigation humanNavigation)
			{
				if (humanNavigation.m_TargetActivity == 10)
				{
					if (humanNavigation.m_LastActivity == humanNavigation.m_TargetActivity)
					{
						return humanNavigation.m_TransformState != TransformState.Action;
					}
					return false;
				}
				return true;
			}

			private static bool ShouldFinishExitVehicle(HumanNavigation humanNavigation)
			{
				if (humanNavigation.m_TargetActivity == 11)
				{
					if (humanNavigation.m_LastActivity == humanNavigation.m_TargetActivity)
					{
						return humanNavigation.m_TransformState != TransformState.Action;
					}
					return false;
				}
				return true;
			}

			private void TickGroupMemberWalking(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, PrefabRef prefabRef, HumanNavigation navigation, GroupMember groupMember, ref Game.Creatures.Resident resident, ref Creature creature, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target, ref Divert divert)
			{
				if ((resident.m_Flags & ResidentFlags.Disembarking) != ResidentFlags.None)
				{
					resident.m_Flags &= ~ResidentFlags.Disembarking;
				}
				else if (divert.m_Purpose == Purpose.None && !this.m_EntityLookup.Exists(target.m_Target))
				{
					if (this.m_HealthProblemData.TryGetComponent(resident.m_Citizen, out var componentData) && (componentData.m_Flags & HealthProblemFlags.RequireTransport) != HealthProblemFlags.None && (componentData.m_Flags & (HealthProblemFlags.Dead | HealthProblemFlags.Injured)) != HealthProblemFlags.None)
					{
						if ((componentData.m_Flags & HealthProblemFlags.Dead) != HealthProblemFlags.None)
						{
							this.AddDeletedResident(DeletedResidentType.Dead);
							this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
						}
						else
						{
							this.SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.None, Entity.Null);
							this.WaitHere(entity, ref currentLane, ref pathOwner);
						}
						return;
					}
					if (this.ReturnHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner))
					{
						return;
					}
				}
				else
				{
					if (CreatureUtils.IsStuck(pathOwner))
					{
						this.AddDeletedResident(DeletedResidentType.StuckLoop);
						this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
						return;
					}
					if (CreatureUtils.ActionLocationReached(currentLane) && this.ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
					{
						return;
					}
				}
				if ((resident.m_Flags & ResidentFlags.Arrived) == 0 && this.m_HumanData.TryGetComponent(groupMember.m_Leader, out var componentData2))
				{
					human.m_Flags |= componentData2.m_Flags & (HumanFlags.Run | HumanFlags.Emergency);
				}
				this.UpdateMoodFlags(ref random, navigation, hasCurrentLane: true, ref resident, ref human, ref divert);
				if (!this.m_CurrentVehicleData.HasComponent(groupMember.m_Leader) || (currentLane.m_Flags & CreatureLaneFlags.EndReached) == 0)
				{
					return;
				}
				CurrentVehicle currentVehicle = this.m_CurrentVehicleData[groupMember.m_Leader];
				Game.Objects.Transform transform = this.m_TransformData[entity];
				Entity vehicle = currentVehicle.m_Vehicle;
				if (this.m_ControllerData.HasComponent(currentVehicle.m_Vehicle))
				{
					Controller controller = this.m_ControllerData[currentVehicle.m_Vehicle];
					if (controller.m_Controller != Entity.Null)
					{
						vehicle = controller.m_Controller;
					}
				}
				this.m_BoardingQueue.Enqueue(Boarding.TryEnterVehicle(entity, groupMember.m_Leader, vehicle, currentVehicle.m_Vehicle, Entity.Null, transform.m_Position, (CreatureVehicleFlags)0u));
			}

			private void TickWalking(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, PrefabRef prefabRef, HumanNavigation navigation, bool isUnspawned, ref Game.Creatures.Resident resident, ref Creature creature, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target, ref Divert divert, DynamicBuffer<GroupCreature> groupCreatures)
			{
				if (CreatureUtils.ResetUpdatedPath(ref pathOwner) && this.CheckPath(jobIndex, entity, prefabRef, ref random, ref creature, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident))
				{
					this.FindNewPath(entity, prefabRef, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
					return;
				}
				if (CreatureUtils.ResetUncheckedLane(ref currentLane) && this.CheckLane(jobIndex, entity, prefabRef, ref random, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident))
				{
					this.FindNewPath(entity, prefabRef, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
					return;
				}
				this.UpdateMoodFlags(ref random, navigation, hasCurrentLane: true, ref resident, ref human, ref divert);
				if ((resident.m_Flags & ResidentFlags.Disembarking) != ResidentFlags.None)
				{
					resident.m_Flags &= ~ResidentFlags.Disembarking;
				}
				else if (divert.m_Purpose == Purpose.None && !this.m_EntityLookup.Exists(target.m_Target))
				{
					if (this.HandleHealthProblem(jobIndex, entity, ref resident, ref currentLane, ref pathOwner) || this.ReturnHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner))
					{
						return;
					}
				}
				else if (CreatureUtils.PathfindFailed(pathOwner))
				{
					if (CreatureUtils.IsStuck(pathOwner))
					{
						this.AddDeletedResident(DeletedResidentType.StuckLoop);
						this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
						return;
					}
					if (divert.m_Purpose != Purpose.None)
					{
						this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.None, Entity.Null, 0, Resource.NoResource);
					}
					else if (this.ReturnHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner))
					{
						return;
					}
				}
				else if (CreatureUtils.EndReached(currentLane))
				{
					if (CreatureUtils.PathEndReached(currentLane))
					{
						if (this.PathEndReached(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner))
						{
							return;
						}
					}
					else if (CreatureUtils.ParkingSpaceReached(currentLane))
					{
						if (this.ParkingSpaceReached(jobIndex, ref random, entity, ref resident, ref currentLane, ref pathOwner, ref target, groupCreatures))
						{
							return;
						}
					}
					else if (CreatureUtils.TransportStopReached(currentLane))
					{
						if (this.TransportStopReached(jobIndex, ref random, entity, prefabRef, isUnspawned, ref resident, ref currentLane, ref pathOwner, ref target))
						{
							return;
						}
					}
					else if (CreatureUtils.ActionLocationReached(currentLane) && this.ActionLocationReached(entity, ref resident, ref human, ref currentLane, ref pathOwner))
					{
						return;
					}
				}
				else if (currentLane.m_QueueArea.radius > 0f)
				{
					this.QueueReached(entity, ref resident, ref currentLane, ref pathOwner);
				}
				if (CreatureUtils.RequireNewPath(pathOwner))
				{
					this.FindNewPath(entity, prefabRef, ref resident, ref human, ref currentLane, ref pathOwner, ref target, ref divert);
				}
			}

			private bool HandleHealthProblem(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
			{
				if (!this.m_HealthProblemData.TryGetComponent(resident.m_Citizen, out var componentData))
				{
					return false;
				}
				if ((componentData.m_Flags & HealthProblemFlags.RequireTransport) == 0)
				{
					return false;
				}
				if ((componentData.m_Flags & HealthProblemFlags.Dead) != HealthProblemFlags.None)
				{
					this.AddDeletedResident(DeletedResidentType.Dead);
					this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
					return true;
				}
				if ((componentData.m_Flags & HealthProblemFlags.Injured) != HealthProblemFlags.None)
				{
					this.WaitHere(entity, ref currentLane, ref pathOwner);
					return true;
				}
				if ((componentData.m_Flags & HealthProblemFlags.Sick) != HealthProblemFlags.None && this.m_CurrentBuildingData.TryGetComponent(resident.m_Citizen, out var componentData2) && componentData2.m_CurrentBuilding != Entity.Null)
				{
					this.WaitHere(entity, ref currentLane, ref pathOwner);
					return true;
				}
				return false;
			}

			private void UpdateMoodFlags(ref Unity.Mathematics.Random random, HumanNavigation navigation, bool hasCurrentLane, ref Game.Creatures.Resident resident, ref Human human, ref Divert divert)
			{
				if (hasCurrentLane && (resident.m_Flags & ResidentFlags.Arrived) == 0 && navigation.m_MaxSpeed < 0.1f)
				{
					if ((human.m_Flags & HumanFlags.Waiting) == 0 && random.NextInt(10) == 0)
					{
						human.m_Flags |= HumanFlags.Waiting;
					}
				}
				else
				{
					human.m_Flags &= ~HumanFlags.Waiting;
				}
				if (divert.m_Purpose == Purpose.Safety)
				{
					human.m_Flags |= HumanFlags.Angry;
				}
				else if ((human.m_Flags & HumanFlags.Angry) != 0 && random.NextInt(10) == 0)
				{
					human.m_Flags &= ~HumanFlags.Angry;
				}
				if (random.NextInt(100) == 0)
				{
					int num = random.NextInt(20, 40);
					int num2 = random.NextInt(60, 80);
					Citizen componentData;
					int num3 = ((!this.m_CitizenData.TryGetComponent(resident.m_Citizen, out componentData)) ? random.NextInt(101) : componentData.Happiness);
					if (num3 < num)
					{
						human.m_Flags &= ~HumanFlags.Happy;
						human.m_Flags |= HumanFlags.Sad;
					}
					else if (componentData.Happiness > num2)
					{
						human.m_Flags &= ~HumanFlags.Sad;
						human.m_Flags |= HumanFlags.Happy;
					}
					else
					{
						human.m_Flags &= ~(HumanFlags.Sad | HumanFlags.Happy);
					}
				}
			}

			private void SetEnterVehiclePath(Entity entity, Entity vehicle, GroupMember groupMember, ref Unity.Mathematics.Random random, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
			{
				currentLane.m_Flags &= ~(CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Transport | CreatureLaneFlags.Taxi);
				DynamicBuffer<PathElement> path = this.m_PathElements[entity];
				if (groupMember.m_Leader != Entity.Null)
				{
					path.Clear();
					path.Add(new PathElement(vehicle, 0f));
					pathOwner.m_ElementIndex = 0;
				}
				else
				{
					if (path.Length > pathOwner.m_ElementIndex && path[pathOwner.m_ElementIndex].m_Target == vehicle)
					{
						return;
					}
					if (pathOwner.m_ElementIndex > 0)
					{
						path[--pathOwner.m_ElementIndex] = new PathElement(vehicle, 0f);
					}
					else
					{
						path.Insert(pathOwner.m_ElementIndex, new PathElement(vehicle, 0f));
					}
				}
				if (this.m_TransformData.HasComponent(vehicle) && this.m_LaneData.HasComponent(currentLane.m_Lane))
				{
					float3 position = this.m_TransformData[vehicle].m_Position;
					if (pathOwner.m_ElementIndex > 0)
					{
						path[--pathOwner.m_ElementIndex] = new PathElement(currentLane.m_Lane, currentLane.m_CurvePosition.y);
					}
					else
					{
						path.Insert(pathOwner.m_ElementIndex, new PathElement(currentLane.m_Lane, currentLane.m_CurvePosition.y));
					}
					CreatureUtils.FixEnterPath(ref random, position, pathOwner.m_ElementIndex, path, ref this.m_OwnerData, ref this.m_LaneData, ref this.m_EdgeLaneData, ref this.m_ConnectionLaneData, ref this.m_CurveData, ref this.m_SubLanes, ref this.m_AreaNodes, ref this.m_AreaTriangles);
				}
			}

			private unsafe void AddDeletedResident(DeletedResidentType type)
			{
				Interlocked.Increment(ref UnsafeUtility.ArrayElementAsRef<int>(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(this.m_DeletedResidents), (int)type));
			}

			private void TickQueue(ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref Creature creature, ref HumanCurrentLane currentLane)
			{
				resident.m_Timer += random.NextInt(1, 3);
				if (currentLane.m_QueueArea.radius > 0f)
				{
					if ((creature.m_QueueArea.radius == 0f || currentLane.m_QueueEntity != creature.m_QueueEntity) && (this.m_RouteConnectedData.HasComponent(currentLane.m_QueueEntity) || this.m_BoardingVehicleData.HasComponent(currentLane.m_QueueEntity)) && (resident.m_Flags & ResidentFlags.WaitingTransport) == 0)
					{
						resident.m_Flags |= ResidentFlags.WaitingTransport;
						resident.m_Timer = 0;
					}
					creature.m_QueueEntity = currentLane.m_QueueEntity;
					creature.m_QueueArea = currentLane.m_QueueArea;
				}
				else
				{
					creature.m_QueueEntity = Entity.Null;
					creature.m_QueueArea = default(Sphere3);
				}
			}

			private void QueueReached(Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
			{
				if ((resident.m_Flags & ResidentFlags.WaitingTransport) != ResidentFlags.None && resident.m_Timer >= 5000)
				{
					this.m_BoardingQueue.Enqueue(Boarding.WaitTimeExceeded(entity, currentLane.m_QueueEntity));
					if (this.m_BoardingVehicleData.HasComponent(currentLane.m_QueueEntity))
					{
						resident.m_Flags |= ResidentFlags.IgnoreTaxi;
					}
					else
					{
						resident.m_Flags |= ResidentFlags.IgnoreTransport;
					}
					pathOwner.m_State &= ~PathFlags.Failed;
					pathOwner.m_State |= PathFlags.Obsolete;
				}
			}

			private void WaitHere(Entity entity, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
			{
				currentLane.m_CurvePosition.y = currentLane.m_CurvePosition.x;
				pathOwner.m_ElementIndex = 0;
				this.m_PathElements[entity].Clear();
			}

			private void ExitVehicle(Entity entity, int jobIndex, ref Unity.Mathematics.Random random, Entity controllerVehicle, PrefabRef prefabRef, CurrentVehicle currentVehicle, GroupMember groupMember, ref Game.Creatures.Resident resident, ref Human human, ref Divert divert, ref PathOwner pathOwner)
			{
				Entity household = this.GetHousehold(resident);
				int ticketPrice = 0;
				if ((currentVehicle.m_Flags & CreatureVehicleFlags.Leader) != 0 && this.m_TaxiData.HasComponent(controllerVehicle))
				{
					Game.Vehicles.Taxi taxi = this.m_TaxiData[controllerVehicle];
					ticketPrice = math.select(taxi.m_CurrentFee, -taxi.m_CurrentFee, (taxi.m_State & TaxiFlags.FromOutside) != 0);
				}
				if (this.m_TransformData.HasComponent(currentVehicle.m_Vehicle))
				{
					Game.Objects.Transform transform = this.m_TransformData[currentVehicle.m_Vehicle];
					Game.Objects.Transform transform2 = transform;
					DynamicBuffer<PathElement> path = this.m_PathElements[entity];
					HumanCurrentLane newCurrentLane = default(HumanCurrentLane);
					float3 targetPosition = transform2.m_Position;
					if (pathOwner.m_ElementIndex < path.Length && (pathOwner.m_State & PathFlags.Obsolete) == 0)
					{
						PathElement pathElement = path[pathOwner.m_ElementIndex];
						Game.Objects.Transform componentData2;
						if (this.m_CurveData.TryGetComponent(pathElement.m_Target, out var componentData))
						{
							targetPosition = MathUtils.Position(componentData.m_Bezier, pathElement.m_TargetDelta.x);
						}
						else if (this.m_TransformData.TryGetComponent(pathElement.m_Target, out componentData2))
						{
							targetPosition = componentData2.m_Position;
						}
					}
					BufferLookup<SubMeshGroup> subMeshGroupBuffers = default(BufferLookup<SubMeshGroup>);
					BufferLookup<CharacterElement> characterElementBuffers = default(BufferLookup<CharacterElement>);
					BufferLookup<SubMesh> subMeshBuffers = default(BufferLookup<SubMesh>);
					BufferLookup<AnimationClip> animationClipBuffers = default(BufferLookup<AnimationClip>);
					BufferLookup<AnimationMotion> animationMotionBuffers = default(BufferLookup<AnimationMotion>);
					bool isDriver = (currentVehicle.m_Flags & CreatureVehicleFlags.Driver) != 0;
					ActivityCondition conditions = CreatureUtils.GetConditions(human);
					this.m_PseudoRandomSeedData.TryGetComponent(entity, out var componentData3);
					transform2 = CreatureUtils.GetVehicleDoorPosition(ref random, ActivityType.Exit, conditions, transform, componentData3, targetPosition, isDriver, this.m_LefthandTraffic, prefabRef.m_Prefab, currentVehicle.m_Vehicle, default(DynamicBuffer<MeshGroup>), ref this.m_PublicTransportData, ref this.m_TrainData, ref this.m_ControllerData, ref this.m_PrefabRefData, ref this.m_PrefabCarData, ref this.m_PrefabActivityLocationElements, ref subMeshGroupBuffers, ref characterElementBuffers, ref subMeshBuffers, ref animationClipBuffers, ref animationMotionBuffers, out var activityMask, out var _);
					if (pathOwner.m_ElementIndex < path.Length && (pathOwner.m_State & PathFlags.Obsolete) == 0)
					{
						CreatureUtils.FixPathStart(ref random, transform2.m_Position, pathOwner.m_ElementIndex, path, ref this.m_OwnerData, ref this.m_LaneData, ref this.m_EdgeLaneData, ref this.m_ConnectionLaneData, ref this.m_CurveData, ref this.m_SubLanes, ref this.m_AreaNodes, ref this.m_AreaTriangles);
						PathElement pathElement2 = path[pathOwner.m_ElementIndex];
						CreatureLaneFlags creatureLaneFlags = (CreatureLaneFlags)0u;
						if (pathElement2.m_TargetDelta.y < pathElement2.m_TargetDelta.x)
						{
							creatureLaneFlags |= CreatureLaneFlags.Backward;
						}
						if (this.m_PedestrianLaneData.HasComponent(pathElement2.m_Target))
						{
							newCurrentLane = new HumanCurrentLane(pathElement2, creatureLaneFlags);
						}
						else if (this.m_ConnectionLaneData.HasComponent(pathElement2.m_Target))
						{
							Game.Net.ConnectionLane connectionLane = this.m_ConnectionLaneData[pathElement2.m_Target];
							if ((connectionLane.m_Flags & ConnectionLaneFlags.Area) != 0)
							{
								creatureLaneFlags |= CreatureLaneFlags.Area;
								if (this.m_OwnerData.HasComponent(pathElement2.m_Target))
								{
									Owner owner = this.m_OwnerData[pathElement2.m_Target];
									if (this.m_HangaroundLocationData.HasComponent(owner.m_Owner))
									{
										creatureLaneFlags |= CreatureLaneFlags.Hangaround;
									}
								}
							}
							else
							{
								creatureLaneFlags |= CreatureLaneFlags.Connection;
							}
							if ((connectionLane.m_Flags & ConnectionLaneFlags.Parking) == 0)
							{
								newCurrentLane = new HumanCurrentLane(pathElement2, creatureLaneFlags);
							}
						}
						else if (this.m_SpawnLocation.HasComponent(pathElement2.m_Target))
						{
							creatureLaneFlags |= CreatureLaneFlags.TransformTarget;
							if (++pathOwner.m_ElementIndex >= path.Length)
							{
								creatureLaneFlags |= CreatureLaneFlags.EndOfPath;
							}
							newCurrentLane = new HumanCurrentLane(pathElement2, creatureLaneFlags);
						}
						else if (this.m_PrefabRefData.HasComponent(pathElement2.m_Target))
						{
							creatureLaneFlags |= CreatureLaneFlags.FindLane;
							newCurrentLane = new HumanCurrentLane(creatureLaneFlags);
						}
					}
					if (newCurrentLane.m_Lane == Entity.Null)
					{
						if (this.m_UnspawnedData.HasComponent(currentVehicle.m_Vehicle))
						{
							newCurrentLane.m_Flags |= CreatureLaneFlags.EmergeUnspawned;
						}
						if (this.m_PathOwnerData.TryGetComponent(controllerVehicle, out var componentData4) && VehicleUtils.PathfindFailed(componentData4))
						{
							newCurrentLane.m_Flags |= CreatureLaneFlags.EmergeUnspawned;
							pathOwner.m_State |= PathFlags.Stuck;
						}
					}
					ActivityMask activityMask2 = new ActivityMask(ActivityType.Driving);
					activityMask2.m_Mask |= new ActivityMask(ActivityType.Biking).m_Mask;
					if ((activityMask.m_Mask & activityMask2.m_Mask) != 0)
					{
						newCurrentLane.m_Flags |= CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached;
					}
					this.m_BoardingQueue.Enqueue(Boarding.ExitVehicle(entity, household, groupMember.m_Leader, currentVehicle.m_Vehicle, newCurrentLane, transform2.m_Position, transform2.m_Rotation, ticketPrice));
				}
				else
				{
					Game.Objects.Transform transform3 = this.m_TransformData[entity];
					this.m_BoardingQueue.Enqueue(Boarding.ExitVehicle(entity, household, groupMember.m_Leader, currentVehicle.m_Vehicle, default(HumanCurrentLane), transform3.m_Position, transform3.m_Rotation, ticketPrice));
					pathOwner.m_State &= ~PathFlags.Failed;
					pathOwner.m_State |= PathFlags.Obsolete;
				}
				currentVehicle.m_Flags |= CreatureVehicleFlags.Exiting;
				this.m_CommandBuffer.SetComponent(jobIndex, entity, currentVehicle);
				switch (divert.m_Purpose)
				{
				case Purpose.None:
				{
					if (this.m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData5) && componentData5.m_Purpose == Purpose.EmergencyShelter)
					{
						human.m_Flags |= HumanFlags.Run | HumanFlags.Emergency;
					}
					break;
				}
				case Purpose.Safety:
				case Purpose.Escape:
					human.m_Flags |= HumanFlags.Run;
					break;
				}
			}

			private bool HasEveryoneBoarded(DynamicBuffer<GroupCreature> group)
			{
				if (group.IsCreated)
				{
					for (int i = 0; i < group.Length; i++)
					{
						Entity creature = group[i].m_Creature;
						if (!this.m_CurrentVehicleData.HasComponent(creature))
						{
							return false;
						}
						if ((this.m_CurrentVehicleData[creature].m_Flags & CreatureVehicleFlags.Ready) == 0)
						{
							return false;
						}
					}
				}
				return true;
			}

			private bool CheckLane(int jobIndex, Entity entity, PrefabRef prefabRef, ref Unity.Mathematics.Random random, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner, ref Game.Creatures.Resident resident)
			{
				Entity entity2 = Entity.Null;
				if (this.m_OwnerData.HasComponent(currentLane.m_Lane))
				{
					entity2 = this.m_OwnerData[currentLane.m_Lane].m_Owner;
				}
				DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
				if (dynamicBuffer.Length > pathOwner.m_ElementIndex)
				{
					PathElement pathElement = dynamicBuffer[pathOwner.m_ElementIndex];
					if (this.m_OwnerData.HasComponent(pathElement.m_Target))
					{
						Entity owner = this.m_OwnerData[pathElement.m_Target].m_Owner;
						if (owner != entity2)
						{
							return this.FindDivertTargets(jobIndex, entity, prefabRef, ref random, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident, owner, entity2);
						}
					}
				}
				return false;
			}

			private bool GetDivertNeeds(PrefabRef prefabRef, ref Unity.Mathematics.Random random, ref Human human, ref Game.Creatures.Resident resident, ref Divert divert, out ActivityMask actionMask, out HouseholdNeed householdNeed)
			{
				CreatureData creatureData = this.m_PrefabCreatureData[prefabRef.m_Prefab];
				householdNeed = default(HouseholdNeed);
				actionMask = default(ActivityMask);
				if ((human.m_Flags & HumanFlags.Selfies) == 0)
				{
					actionMask.m_Mask |= creatureData.m_SupportedActivities.m_Mask & new ActivityMask(ActivityType.Selfies).m_Mask;
				}
				if ((resident.m_Flags & ResidentFlags.ActivityDone) != ResidentFlags.None)
				{
					if (random.NextInt(3) != 0)
					{
						actionMask = default(ActivityMask);
					}
					resident.m_Flags &= ~ResidentFlags.ActivityDone;
				}
				bool flag = actionMask.m_Mask != 0;
				if (divert.m_Purpose != Purpose.None)
				{
					return flag;
				}
				if (this.m_AttendingMeetingData.HasComponent(resident.m_Citizen))
				{
					AttendingMeeting attendingMeeting = this.m_AttendingMeetingData[resident.m_Citizen];
					if (this.m_PrefabRefData.HasComponent(attendingMeeting.m_Meeting))
					{
						return flag;
					}
				}
				if (this.m_CitizenData.HasComponent(resident.m_Citizen))
				{
					CitizenAge age = this.m_CitizenData[resident.m_Citizen].GetAge();
					if ((age == CitizenAge.Adult || age == CitizenAge.Elderly) && this.m_HouseholdMembers.HasComponent(resident.m_Citizen))
					{
						HouseholdMember householdMember = this.m_HouseholdMembers[resident.m_Citizen];
						if (this.m_HouseholdNeedData.HasComponent(householdMember.m_Household))
						{
							householdNeed = this.m_HouseholdNeedData[householdMember.m_Household];
							flag |= householdNeed.m_Resource != Resource.NoResource;
						}
					}
				}
				return flag;
			}

			private bool FindDivertTargets(int jobIndex, Entity entity, PrefabRef prefabRef, ref Unity.Mathematics.Random random, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner, ref Game.Creatures.Resident resident, Entity element, Entity ignoreElement)
			{
				if (!this.GetDivertNeeds(prefabRef, ref random, ref human, ref resident, ref divert, out var actionMask, out var householdNeed))
				{
					return false;
				}
				if (this.m_ConnectedEdges.HasBuffer(element))
				{
					DynamicBuffer<ConnectedEdge> dynamicBuffer = this.m_ConnectedEdges[element];
					for (int i = 0; i < dynamicBuffer.Length; i++)
					{
						Entity edge = dynamicBuffer[i].m_Edge;
						if (!(edge == ignoreElement) && this.FindDivertTargets(jobIndex, entity, ref random, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident, edge, ref actionMask, householdNeed))
						{
							return true;
						}
					}
					return false;
				}
				if (this.m_ConnectedEdges.HasBuffer(ignoreElement))
				{
					DynamicBuffer<ConnectedEdge> dynamicBuffer2 = this.m_ConnectedEdges[ignoreElement];
					for (int j = 0; j < dynamicBuffer2.Length; j++)
					{
						if (dynamicBuffer2[j].m_Edge == element)
						{
							return false;
						}
					}
				}
				return this.FindDivertTargets(jobIndex, entity, ref random, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident, element, ref actionMask, householdNeed);
			}

			private HumanFlags SelectAttractionFlags(ref Unity.Mathematics.Random random, ActivityMask actionMask)
			{
				HumanFlags result = (HumanFlags)0u;
				int count = 0;
				this.CheckActionFlags(ref result, ref count, ref random, actionMask, ActivityType.Selfies, HumanFlags.Selfies);
				return result;
			}

			private void CheckActionFlags(ref HumanFlags result, ref int count, ref Unity.Mathematics.Random random, ActivityMask actionMask, ActivityType activityType, HumanFlags flags)
			{
				if ((actionMask.m_Mask & new ActivityMask(activityType).m_Mask) != 0 && random.NextInt(++count) == 0)
				{
					result = flags;
				}
			}

			private bool FindDivertTargets(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner, ref Game.Creatures.Resident resident, Entity element, ref ActivityMask actionMask, HouseholdNeed householdNeed)
			{
				if (this.m_ConnectedBuildings.HasBuffer(element))
				{
					DynamicBuffer<ConnectedBuilding> dynamicBuffer = this.m_ConnectedBuildings[element];
					int num = random.NextInt(dynamicBuffer.Length);
					bool flag = actionMask.m_Mask != 0;
					bool flag2 = householdNeed.m_Resource != Resource.NoResource;
					for (int i = 0; i < dynamicBuffer.Length; i++)
					{
						int num2 = num + i;
						num2 = math.select(num2, num2 - dynamicBuffer.Length, num2 >= dynamicBuffer.Length);
						Entity building = dynamicBuffer[num2].m_Building;
						if (flag)
						{
							int num3 = 0;
							if (this.m_AttractivenessProviderData.HasComponent(building))
							{
								num3 += this.m_AttractivenessProviderData[building].m_Attractiveness;
							}
							if (this.m_OnFireData.HasComponent(building))
							{
								num3 += Mathf.RoundToInt(this.m_OnFireData[building].m_Intensity);
							}
							if (random.NextInt(10) < num3 && this.AddPathAction(entity, ref random, ref currentLane, ref pathOwner, building))
							{
								human.m_Flags |= this.SelectAttractionFlags(ref random, actionMask);
								actionMask = default(ActivityMask);
								flag = false;
							}
						}
						if (building == target.m_Target || !flag2 || !this.m_Renters.HasBuffer(building))
						{
							continue;
						}
						DynamicBuffer<Renter> dynamicBuffer2 = this.m_Renters[building];
						for (int j = 0; j < dynamicBuffer2.Length; j++)
						{
							Entity renter = dynamicBuffer2[j].m_Renter;
							if (renter == target.m_Target || !this.m_ServiceAvailableData.HasComponent(renter))
							{
								continue;
							}
							PrefabRef prefabRef = this.m_PrefabRefData[renter];
							if (this.m_PrefabIndustrialProcessData.HasComponent(prefabRef.m_Prefab) && (this.m_PrefabIndustrialProcessData[prefabRef.m_Prefab].m_Output.m_Resource & householdNeed.m_Resource) != Resource.NoResource)
							{
								ServiceAvailable serviceAvailable = this.m_ServiceAvailableData[renter];
								DynamicBuffer<Game.Economy.Resources> resources = this.m_Resources[renter];
								if (math.min(EconomyUtils.GetResources(householdNeed.m_Resource, resources), serviceAvailable.m_ServiceAvailable) >= householdNeed.m_Amount)
								{
									this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Shopping, renter, householdNeed.m_Amount, householdNeed.m_Resource);
									return true;
								}
							}
						}
					}
				}
				return false;
			}

			private bool AddPathAction(Entity entity, ref Unity.Mathematics.Random random, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, Entity actionTarget)
			{
				Game.Objects.Transform transform = this.m_TransformData[actionTarget];
				PrefabRef prefabRef = this.m_PrefabRefData[actionTarget];
				float3 position;
				if (this.m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab))
				{
					ObjectGeometryData objectGeometryData = this.m_PrefabObjectGeometryData[prefabRef.m_Prefab];
					position = ObjectUtils.LocalToWorld(transform, random.NextFloat3(objectGeometryData.m_Bounds.min, objectGeometryData.m_Bounds.max));
				}
				else
				{
					position = transform.m_Position + random.NextFloat3(-10f, 10f);
				}
				float num = float.MaxValue;
				float t = 0f;
				int num2 = -2;
				if (currentLane.m_CurvePosition.y != currentLane.m_CurvePosition.x && this.m_PedestrianLaneData.HasComponent(currentLane.m_Lane))
				{
					num = MathUtils.Distance(this.m_CurveData[currentLane.m_Lane].m_Bezier, position, currentLane.m_CurvePosition, out t);
					num2 = -1;
				}
				DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
				int num3 = math.min(dynamicBuffer.Length, pathOwner.m_ElementIndex + 8);
				for (int i = pathOwner.m_ElementIndex; i < num3; i++)
				{
					PathElement pathElement = dynamicBuffer[i];
					if (pathElement.m_TargetDelta.y != pathElement.m_TargetDelta.x && this.m_PedestrianLaneData.HasComponent(pathElement.m_Target))
					{
						float t2;
						float num4 = MathUtils.Distance(this.m_CurveData[pathElement.m_Target].m_Bezier, position, pathElement.m_TargetDelta, out t2);
						if (num4 < num)
						{
							num = num4;
							t = t2;
							num2 = i;
						}
					}
				}
				Entity target;
				float y;
				switch (num2)
				{
				case -2:
					return false;
				case -1:
					target = currentLane.m_Lane;
					y = currentLane.m_CurvePosition.y;
					currentLane.m_CurvePosition.y = t;
					num2 = pathOwner.m_ElementIndex;
					break;
				default:
				{
					PathElement value = dynamicBuffer[num2];
					target = value.m_Target;
					y = value.m_TargetDelta.y;
					value.m_TargetDelta.y = t;
					dynamicBuffer[num2++] = value;
					break;
				}
				}
				dynamicBuffer.Insert(num2, new PathElement
				{
					m_Target = target,
					m_TargetDelta = new float2(t, y)
				});
				dynamicBuffer.Insert(num2, new PathElement
				{
					m_Target = actionTarget,
					m_Flags = PathElementFlags.Action
				});
				return true;
			}

			private bool CheckPath(int jobIndex, Entity entity, PrefabRef prefabRef, ref Unity.Mathematics.Random random, ref Creature creature, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner, ref Game.Creatures.Resident resident)
			{
				DynamicBuffer<PathElement> path = this.m_PathElements[entity];
				human.m_Flags &= ~(HumanFlags.Selfies | HumanFlags.Carried);
				resident.m_Flags &= ~(ResidentFlags.WaitingTransport | ResidentFlags.NoLateDeparture);
				resident.m_Timer = 0;
				creature.m_QueueEntity = Entity.Null;
				creature.m_QueueArea = default(Sphere3);
				Purpose purpose = divert.m_Purpose;
				switch (purpose)
				{
				case Purpose.Shopping:
					if (purpose == Purpose.Shopping && divert.m_Data != 0 && this.m_HouseholdMembers.HasComponent(resident.m_Citizen))
					{
						this.m_ActionQueue.Enqueue(new ResidentAction
						{
							m_Type = ResidentActionType.GoShopping,
							m_Citizen = resident.m_Citizen,
							m_Household = this.m_HouseholdMembers[resident.m_Citizen].m_Household,
							m_Resource = divert.m_Resource,
							m_Target = divert.m_Target,
							m_Amount = divert.m_Data,
							m_Distance = 100f
						});
						divert.m_Data = 0;
					}
					break;
				case Purpose.None:
				{
					if (this.m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData) && componentData.m_Purpose == Purpose.EmergencyShelter)
					{
						human.m_Flags |= HumanFlags.Run | HumanFlags.Emergency;
					}
					break;
				}
				case Purpose.Safety:
				case Purpose.Escape:
					human.m_Flags |= HumanFlags.Run;
					break;
				case Purpose.Disappear:
					if (divert.m_Target == Entity.Null && path.Length >= 1)
					{
						divert.m_Target = path[path.Length - 1].m_Target;
						path.RemoveAt(path.Length - 1);
					}
					break;
				}
				ParkedCar componentData2 = default(ParkedCar);
				BicycleOwner component2;
				if (this.m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
				{
					this.m_ParkedCarData.TryGetComponent(component.m_Car, out componentData2);
				}
				else if (this.m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2))
				{
					this.m_ParkedCarData.TryGetComponent(component2.m_Bicycle, out componentData2);
				}
				int length = path.Length;
				for (int i = 0; i < path.Length; i++)
				{
					PathElement pathElement = path[i];
					if (pathElement.m_Target == componentData2.m_Lane)
					{
						VehicleUtils.SetParkingCurvePos(path, pathOwner, i, currentLane.m_Lane, componentData2.m_CurvePosition, ref this.m_CurveData);
						length = i;
						break;
					}
					if (this.m_ParkingLaneData.HasComponent(pathElement.m_Target))
					{
						float curvePos = random.NextFloat(0.05f, 0.95f);
						VehicleUtils.SetParkingCurvePos(path, pathOwner, i, currentLane.m_Lane, curvePos, ref this.m_CurveData);
						length = i;
						break;
					}
					if (this.m_ConnectionLaneData.HasComponent(pathElement.m_Target))
					{
						Game.Net.ConnectionLane connectionLane = this.m_ConnectionLaneData[pathElement.m_Target];
						if ((connectionLane.m_Flags & ConnectionLaneFlags.Parking) != 0)
						{
							float curvePos2 = random.NextFloat(0.05f, 0.95f);
							VehicleUtils.SetParkingCurvePos(path, pathOwner, i, currentLane.m_Lane, curvePos2, ref this.m_CurveData);
							length = i;
							break;
						}
						if ((connectionLane.m_Flags & ConnectionLaneFlags.Area) != 0 && i == path.Length - 1)
						{
							CreatureUtils.SetRandomAreaTarget(ref random, i, path, this.m_OwnerData, this.m_CurveData, this.m_LaneData, this.m_ConnectionLaneData, this.m_SubLanes, this.m_AreaNodes, this.m_AreaTriangles);
							length = path.Length;
							break;
						}
					}
					else if (i == path.Length - 1 && this.m_SpawnLocation.HasComponent(pathElement.m_Target))
					{
						PrefabRef prefabRef2 = this.m_PrefabRefData[pathElement.m_Target];
						if (i != 0 && this.m_PrefabSpawnLocationData.TryGetComponent(prefabRef2, out var componentData3) && componentData3.m_HangaroundOnLane)
						{
							ref PathElement reference = ref path.ElementAt(i - 1);
							reference.m_TargetDelta.y = random.NextFloat();
							reference.m_Flags |= PathElementFlags.Hangaround;
							path.RemoveAt(i);
							length = path.Length;
							break;
						}
					}
				}
				TransportEstimateBuffer transportEstimateBuffer = new TransportEstimateBuffer
				{
					m_BoardingQueue = this.m_BoardingQueue
				};
				RouteUtils.StripTransportSegments(ref random, length, path, this.m_RouteConnectedData, this.m_BoardingVehicleData, this.m_OwnerData, this.m_LaneData, this.m_ConnectionLaneData, this.m_CurveData, this.m_PrefabRefData, this.m_PrefabTransportStopData, this.m_SubLanes, this.m_AreaNodes, this.m_AreaTriangles, transportEstimateBuffer);
				if (this.m_OwnerData.HasComponent(currentLane.m_Lane))
				{
					Entity owner = this.m_OwnerData[currentLane.m_Lane].m_Owner;
					return this.FindDivertTargets(jobIndex, entity, prefabRef, ref random, ref human, ref currentLane, ref target, ref divert, ref pathOwner, ref resident, owner, Entity.Null);
				}
				return false;
			}

			private void CurrentVehicleBoarding(int jobIndex, Entity entity, Entity controllerVehicle, ref Game.Creatures.Resident resident, ref PathOwner pathOwner, ref Target target)
			{
				Game.Vehicles.PublicTransport publicTransport = this.m_PublicTransportData[controllerVehicle];
				if ((publicTransport.m_State & (PublicTransportFlags.Evacuating | PublicTransportFlags.PrisonerTransport)) != 0)
				{
					if ((publicTransport.m_State & PublicTransportFlags.Returning) == 0)
					{
						return;
					}
				}
				else
				{
					DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
					if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 2)
					{
						Entity target2 = dynamicBuffer[pathOwner.m_ElementIndex + 1].m_Target;
						Entity nextLane = Entity.Null;
						if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 3)
						{
							nextLane = dynamicBuffer[pathOwner.m_ElementIndex + 2].m_Target;
						}
						if (!RouteUtils.ShouldExitVehicle(nextLane, target2, controllerVehicle, ref this.m_OwnerData, ref this.m_RouteConnectedData, ref this.m_BoardingVehicleData, ref this.m_CurrentRouteData, ref this.m_AccessLaneLaneData, ref this.m_PublicTransportData, ref this.m_ConnectedRoutes, testing: false, out var obsolete))
						{
							return;
						}
						pathOwner.m_ElementIndex += 2;
						if (!obsolete)
						{
							resident.m_Flags |= ResidentFlags.Disembarking;
							return;
						}
					}
				}
				pathOwner.m_State &= ~PathFlags.Failed;
				pathOwner.m_State |= PathFlags.Obsolete;
				resident.m_Flags |= ResidentFlags.Disembarking;
			}

			private void CurrentVehicleTesting(int jobIndex, Entity entity, Entity controllerVehicle, ref Game.Creatures.Resident resident, ref PathOwner pathOwner, ref Target target)
			{
				Game.Vehicles.PublicTransport publicTransport = this.m_PublicTransportData[controllerVehicle];
				if ((publicTransport.m_State & (PublicTransportFlags.Evacuating | PublicTransportFlags.PrisonerTransport)) != 0)
				{
					if ((publicTransport.m_State & PublicTransportFlags.Returning) == 0)
					{
						return;
					}
				}
				else
				{
					DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
					if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 2)
					{
						Entity target2 = dynamicBuffer[pathOwner.m_ElementIndex + 1].m_Target;
						Entity nextLane = Entity.Null;
						if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 3)
						{
							nextLane = dynamicBuffer[pathOwner.m_ElementIndex + 2].m_Target;
						}
						if (!RouteUtils.ShouldExitVehicle(nextLane, target2, controllerVehicle, ref this.m_OwnerData, ref this.m_RouteConnectedData, ref this.m_BoardingVehicleData, ref this.m_CurrentRouteData, ref this.m_AccessLaneLaneData, ref this.m_PublicTransportData, ref this.m_ConnectedRoutes, testing: true, out var _))
						{
							return;
						}
					}
				}
				this.m_BoardingQueue.Enqueue(Boarding.RequireStop(Entity.Null, controllerVehicle, default(float3)));
			}

			private void CurrentVehicleDisembarking(int jobIndex, Entity entity, Entity controllerVehicle, ref Game.Creatures.Resident resident, ref PathOwner pathOwner, ref Target target)
			{
				DynamicBuffer<PathElement> targetElements = this.m_PathElements[entity];
				DynamicBuffer<PathElement> sourceElements = this.m_PathElements[controllerVehicle];
				PathOwner sourceOwner = this.m_PathOwnerData[controllerVehicle];
				if (sourceElements.Length > sourceOwner.m_ElementIndex)
				{
					PathUtils.CopyPath(sourceElements, sourceOwner, 0, targetElements);
					pathOwner.m_ElementIndex = 0;
					pathOwner.m_State |= PathFlags.Updated;
				}
				else
				{
					pathOwner.m_State &= ~PathFlags.Failed;
					pathOwner.m_State |= PathFlags.Obsolete;
				}
				resident.m_Flags |= ResidentFlags.Disembarking;
			}

			private void CurrentVehicleTransporting(Entity entity, Entity controllerVehicle, ref PathOwner pathOwner)
			{
				this.m_PathElements[entity].Clear();
				pathOwner.m_ElementIndex = 0;
			}

			private void GroupLeaderDisembarking(Entity entity, ref Game.Creatures.Resident resident, ref PathOwner pathOwner)
			{
				this.m_PathElements[entity].Clear();
				pathOwner.m_ElementIndex = 0;
				resident.m_Flags |= ResidentFlags.Disembarking;
			}

			private bool PathEndReached(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				return divert.m_Purpose switch
				{
					Purpose.None => this.ReachTarget(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner, new ResetTrip
					{
						m_Target = target.m_Target
					}), 
					Purpose.SendMail => this.ReachSendMail(jobIndex, entity, ref resident, ref currentLane, ref target, ref divert, ref pathOwner), 
					Purpose.Safety => this.ReachSafety(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner), 
					Purpose.Escape => this.ReachEscape(jobIndex, entity, ref resident, ref currentLane, ref target, ref divert, ref pathOwner), 
					Purpose.WaitingHome => this.ReachWaitingHome(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner), 
					Purpose.PathFailed => this.ReachPathFailed(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner), 
					_ => this.ReachDivert(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner), 
				};
			}

			private bool ActionLocationReached(Entity entity, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
			{
				if ((currentLane.m_Flags & CreatureLaneFlags.ActivityDone) != 0)
				{
					resident.m_Flags |= ResidentFlags.ActivityDone;
					human.m_Flags &= ~HumanFlags.Selfies;
					DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
					pathOwner.m_ElementIndex += math.select(0, 1, pathOwner.m_ElementIndex < dynamicBuffer.Length);
					return false;
				}
				return true;
			}

			private bool ReachTarget(int jobIndex, Entity creatureEntity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner, ResetTrip resetTrip)
			{
				if (this.m_VehicleData.HasComponent(target.m_Target))
				{
					return this.ReachVehicle(jobIndex, creatureEntity, ref resident, ref currentLane, ref target, ref pathOwner);
				}
				Entity entity = target.m_Target;
				if (this.m_PropertyRenters.HasComponent(entity))
				{
					entity = this.m_PropertyRenters[entity].m_Property;
				}
				if (this.m_OnFireData.HasComponent(entity) || this.m_DestroyedData.HasComponent(entity))
				{
					this.SetDivert(jobIndex, creatureEntity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Safety, Entity.Null, 0, Resource.NoResource);
					return false;
				}
				if (this.CanHangAround(resident.m_Citizen))
				{
					if ((currentLane.m_Flags & CreatureLaneFlags.Hangaround) != 0)
					{
						if ((currentLane.m_Flags & (CreatureLaneFlags.TransformTarget | CreatureLaneFlags.ActivityDone)) == (CreatureLaneFlags.TransformTarget | CreatureLaneFlags.ActivityDone) || random.NextInt(2500) == 0)
						{
							ResidentFlags ignoreFlags = this.GetIgnoreFlags(entity, ref resident, ref currentLane);
							if (ignoreFlags != ResidentFlags.None)
							{
								if ((ignoreFlags & ~resident.m_Flags) != ResidentFlags.None)
								{
									resident.m_Flags |= ignoreFlags;
									pathOwner.m_State &= ~PathFlags.Failed;
									pathOwner.m_State |= PathFlags.Obsolete;
									resident.m_Flags &= ~ResidentFlags.Hangaround;
									if ((resident.m_Flags & ResidentFlags.Arrived) != ResidentFlags.None)
									{
										resetTrip.m_Source = target.m_Target;
										resetTrip.m_Target = target.m_Target;
										bool flag = false;
										if (this.m_HouseholdMembers.TryGetComponent(resident.m_Citizen, out var componentData) && this.m_HouseholdAnimals.TryGetBuffer(componentData.m_Household, out var bufferData))
										{
											for (int i = 0; i < bufferData.Length; i++)
											{
												HouseholdAnimal householdAnimal = bufferData[i];
												if (this.m_CurrentBuildingData.TryGetComponent(householdAnimal.m_HouseholdPet, out var componentData2) && this.m_CurrentTransportData.TryGetComponent(householdAnimal.m_HouseholdPet, out var componentData3) && componentData2.m_CurrentBuilding == entity)
												{
													Entity e = this.m_CommandBuffer.CreateEntity(jobIndex, this.m_ResetTripArchetype);
													resetTrip.m_Creature = componentData3.m_CurrentTransport;
													this.m_CommandBuffer.SetComponent(jobIndex, e, resetTrip);
													flag = true;
												}
											}
										}
										if (flag)
										{
											Entity e2 = this.m_CommandBuffer.CreateEntity(jobIndex, this.m_ResetTripArchetype);
											resetTrip.m_Creature = creatureEntity;
											this.m_CommandBuffer.SetComponent(jobIndex, e2, resetTrip);
										}
									}
								}
								return false;
							}
						}
						resident.m_Flags |= ResidentFlags.Hangaround;
					}
				}
				else
				{
					resident.m_Flags &= ~ResidentFlags.Hangaround;
				}
				human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
				resident.m_Flags &= ~(ResidentFlags.IgnoreTaxi | ResidentFlags.IgnoreTransport);
				if ((resident.m_Flags & ResidentFlags.Arrived) == 0)
				{
					resetTrip.m_Creature = creatureEntity;
					resetTrip.m_Arrived = entity;
					Entity e3 = this.m_CommandBuffer.CreateEntity(jobIndex, this.m_ResetTripArchetype);
					this.m_CommandBuffer.SetComponent(jobIndex, e3, resetTrip);
					resident.m_Flags |= ResidentFlags.Arrived;
					if (this.m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData4) && componentData4.m_Purpose == Purpose.GoingHome && this.m_BicycleOwnerData.TryGetComponent(resident.m_Citizen, out var componentData5) && this.m_PersonalCarData.TryGetComponent(componentData5.m_Bicycle, out var componentData6) && (componentData6.m_State & PersonalCarFlags.HomeTarget) == 0)
					{
						this.m_VehicleLayouts.TryGetBuffer(componentData5.m_Bicycle, out var bufferData2);
						VehicleUtils.DeleteVehicle(this.m_CommandBuffer, jobIndex, componentData5.m_Bicycle, bufferData2);
					}
					return false;
				}
				if ((resident.m_Flags & ResidentFlags.Hangaround) != ResidentFlags.None)
				{
					return false;
				}
				if (this.m_EntityLookup.Exists(resident.m_Citizen))
				{
					this.m_CommandBuffer.RemoveComponent<CurrentTransport>(jobIndex, resident.m_Citizen);
				}
				this.m_CommandBuffer.AddComponent(jobIndex, creatureEntity, default(Deleted));
				return true;
			}

			private bool CanHangAround(Entity citizenEntity)
			{
				if (this.m_TravelPurposeData.TryGetComponent(citizenEntity, out var componentData) && componentData.m_Purpose == Purpose.Sleeping)
				{
					return false;
				}
				return true;
			}

			private ResidentFlags GetIgnoreFlags(Entity building, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane)
			{
				if ((resident.m_Flags & ResidentFlags.CannotIgnore) != ResidentFlags.None)
				{
					return ResidentFlags.None;
				}
				ResidentFlags residentFlags = ResidentFlags.None;
				Owner componentData3;
				PrefabRef componentData4;
				SpawnLocationData componentData5;
				if ((currentLane.m_Flags & CreatureLaneFlags.TransformTarget) != 0)
				{
					if (this.m_PrefabRefData.TryGetComponent(currentLane.m_Lane, out var componentData) && this.m_PrefabSpawnLocationData.TryGetComponent(componentData.m_Prefab, out var componentData2) && ((componentData2.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.BenchSitting).m_Mask) != 0 || (componentData2.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PullUps).m_Mask) != 0 || (componentData2.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Reading).m_Mask) != 0))
					{
						residentFlags |= ResidentFlags.IgnoreBenches;
					}
				}
				else if ((currentLane.m_Flags & CreatureLaneFlags.Area) != 0 && this.m_OwnerData.TryGetComponent(currentLane.m_Lane, out componentData3) && this.m_PrefabRefData.TryGetComponent(componentData3.m_Owner, out componentData4) && this.m_PrefabSpawnLocationData.TryGetComponent(componentData4.m_Prefab, out componentData5) && ((componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Standing).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundLaying).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundSitting).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PushUps).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.SitUps).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingJacks).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingLunges).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Squats).m_Mask) != 0 || (componentData5.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Yoga).m_Mask) != 0))
				{
					residentFlags |= ResidentFlags.IgnoreAreas;
				}
				if ((residentFlags & ~resident.m_Flags) != ResidentFlags.None)
				{
					ResidentFlags residentFlags2 = ~(resident.m_Flags | residentFlags);
					bool flag = false;
					if (this.m_SpawnLocationElements.TryGetBuffer(building, out var bufferData))
					{
						for (int i = 0; i < bufferData.Length; i++)
						{
							SpawnLocationElement spawnLocationElement = bufferData[i];
							if (spawnLocationElement.m_Type == SpawnLocationType.SpawnLocation || spawnLocationElement.m_Type == SpawnLocationType.HangaroundLocation)
							{
								PrefabRef prefabRef = this.m_PrefabRefData[spawnLocationElement.m_SpawnLocation];
								SpawnLocationData spawnLocationData = this.m_PrefabSpawnLocationData[prefabRef.m_Prefab];
								ResidentFlags residentFlags3 = ResidentFlags.None;
								if ((spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.BenchSitting).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PullUps).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Reading).m_Mask) != 0)
								{
									residentFlags3 |= ResidentFlags.IgnoreBenches;
								}
								if ((spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Standing).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundLaying).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.GroundSitting).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.PushUps).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.SitUps).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingJacks).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.JumpingLunges).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Squats).m_Mask) != 0 || (spawnLocationData.m_ActivityMask.m_Mask & new ActivityMask(ActivityType.Yoga).m_Mask) != 0)
								{
									residentFlags3 |= ResidentFlags.IgnoreAreas;
								}
								if (residentFlags3 == ResidentFlags.None || (residentFlags3 & residentFlags2) != ResidentFlags.None)
								{
									return residentFlags;
								}
								flag = flag || (residentFlags3 & ~residentFlags) != 0;
							}
						}
					}
					if (!flag)
					{
						resident.m_Flags |= ResidentFlags.CannotIgnore;
						return ResidentFlags.None;
					}
					resident.m_Flags &= (ResidentFlags)(0xFFFFF3FFu | (uint)residentFlags);
				}
				return residentFlags;
			}

			private bool ReachVehicle(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref PathOwner pathOwner)
			{
				Entity entity2 = target.m_Target;
				if (this.m_ControllerData.HasComponent(target.m_Target))
				{
					Controller controller = this.m_ControllerData[target.m_Target];
					if (controller.m_Controller != Entity.Null)
					{
						entity2 = controller.m_Controller;
					}
				}
				if (this.m_PublicTransportData.HasComponent(entity2))
				{
					if ((this.m_PublicTransportData[entity2].m_State & PublicTransportFlags.Boarding) != 0 && this.m_OwnerData.HasComponent(entity2))
					{
						Owner owner = this.m_OwnerData[entity2];
						if (this.m_PrefabRefData.HasComponent(owner.m_Owner))
						{
							this.TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
							target.m_Target = owner.m_Owner;
							pathOwner.m_State &= ~PathFlags.Failed;
							pathOwner.m_State |= PathFlags.Obsolete;
							return true;
						}
					}
				}
				else if (this.m_PoliceCarData.HasComponent(entity2))
				{
					if ((this.m_PoliceCarData[entity2].m_State & PoliceCarFlags.AtTarget) != 0 && this.m_OwnerData.HasComponent(entity2))
					{
						Owner owner2 = this.m_OwnerData[entity2];
						if (this.m_PrefabRefData.HasComponent(owner2.m_Owner))
						{
							this.TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
							target.m_Target = owner2.m_Owner;
							pathOwner.m_State &= ~PathFlags.Failed;
							pathOwner.m_State |= PathFlags.Obsolete;
							return true;
						}
					}
				}
				else if (this.m_AmbulanceData.HasComponent(entity2))
				{
					if ((this.m_AmbulanceData[entity2].m_State & AmbulanceFlags.AtTarget) != 0 && this.m_OwnerData.HasComponent(entity2))
					{
						Owner owner3 = this.m_OwnerData[entity2];
						if (this.m_PrefabRefData.HasComponent(owner3.m_Owner))
						{
							this.TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
							target.m_Target = owner3.m_Owner;
							pathOwner.m_State &= ~PathFlags.Failed;
							pathOwner.m_State |= PathFlags.Obsolete;
							return true;
						}
					}
				}
				else if (this.m_HearseData.HasComponent(entity2) && (this.m_HearseData[entity2].m_State & HearseFlags.AtTarget) != 0 && this.m_OwnerData.HasComponent(entity2))
				{
					Owner owner4 = this.m_OwnerData[entity2];
					if (this.m_PrefabRefData.HasComponent(owner4.m_Owner))
					{
						this.TryEnterVehicle(entity, target.m_Target, Entity.Null, ref resident, ref currentLane);
						target.m_Target = owner4.m_Owner;
						pathOwner.m_State &= ~PathFlags.Failed;
						pathOwner.m_State |= PathFlags.Obsolete;
						return true;
					}
				}
				this.AddDeletedResident(DeletedResidentType.InvalidVehicleTarget);
				this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
				return true;
			}

			private bool ReachDivert(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				ResetTrip resetTrip = new ResetTrip
				{
					m_Target = divert.m_Target,
					m_TravelPurpose = divert.m_Purpose,
					m_TravelData = divert.m_Data,
					m_TravelResource = divert.m_Resource
				};
				if (this.m_TravelPurposeData.HasComponent(resident.m_Citizen) && this.m_PrefabRefData.HasComponent(target.m_Target))
				{
					TravelPurpose travelPurpose = this.m_TravelPurposeData[resident.m_Citizen];
					resetTrip.m_NextTarget = target.m_Target;
					resetTrip.m_NextPurpose = travelPurpose.m_Purpose;
					resetTrip.m_NextData = travelPurpose.m_Data;
					resetTrip.m_NextResource = travelPurpose.m_Resource;
				}
				target.m_Target = divert.m_Target;
				divert = default(Divert);
				this.m_CommandBuffer.RemoveComponent<Divert>(jobIndex, entity);
				pathOwner.m_State &= ~PathFlags.CachedObsolete;
				return this.ReachTarget(jobIndex, entity, ref random, ref resident, ref human, ref currentLane, ref target, ref divert, ref pathOwner, resetTrip);
			}

			private bool ReachSendMail(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				this.m_ActionQueue.Enqueue(new ResidentAction
				{
					m_Type = ResidentActionType.SendMail,
					m_Citizen = resident.m_Citizen,
					m_Target = currentLane.m_Lane
				});
				this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.None, Entity.Null, 0, Resource.NoResource);
				return false;
			}

			private bool ReachSafety(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
				if (!this.m_PrefabRefData.HasComponent(target.m_Target) || this.m_DestroyedData.HasComponent(target.m_Target))
				{
					bool movingAway;
					Entity homeBuilding = this.GetHomeBuilding(ref resident, out movingAway);
					if (homeBuilding != Entity.Null && !this.m_DestroyedData.HasComponent(homeBuilding))
					{
						if (this.m_OnFireData.HasComponent(homeBuilding))
						{
							this.FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
							return false;
						}
						this.SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
						return false;
					}
					if (movingAway)
					{
						target.m_Target = Entity.Null;
						this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Disappear, Entity.Null, 0, Resource.NoResource);
						return false;
					}
					this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.WaitingHome, Entity.Null, 0, Resource.NoResource);
					return false;
				}
				if (this.m_OnFireData.HasComponent(target.m_Target))
				{
					this.FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
					return false;
				}
				Purpose purpose = Purpose.None;
				if (this.m_TravelPurposeData.HasComponent(resident.m_Citizen))
				{
					purpose = this.m_TravelPurposeData[resident.m_Citizen].m_Purpose;
				}
				this.SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, purpose, target.m_Target);
				return false;
			}

			private bool ReachEscape(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				bool movingAway;
				Entity homeBuilding = this.GetHomeBuilding(ref resident, out movingAway);
				if (homeBuilding == Entity.Null || this.m_OnFireData.HasComponent(homeBuilding) || this.m_DestroyedData.HasComponent(homeBuilding))
				{
					this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Disappear, Entity.Null, 0, Resource.NoResource);
					return false;
				}
				this.SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
				return false;
			}

			private bool ReachWaitingHome(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				bool movingAway;
				Entity homeBuilding = this.GetHomeBuilding(ref resident, out movingAway);
				if (homeBuilding == Entity.Null || this.m_DestroyedData.HasComponent(homeBuilding))
				{
					if (movingAway)
					{
						this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.Disappear, Entity.Null, 0, Resource.NoResource);
						return false;
					}
					divert.m_Data += random.NextInt(1, 3);
					bool flag = divert.m_Data <= 2500;
					if (!flag)
					{
						flag = (currentLane.m_Flags & CreatureLaneFlags.Connection) == 0 || !this.m_ConnectionLaneData.TryGetComponent(currentLane.m_Lane, out var componentData) || (componentData.m_Flags & ConnectionLaneFlags.Outside) == 0;
					}
					if (flag)
					{
						this.FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
						return false;
					}
					if (this.m_CitizenData.HasComponent(resident.m_Citizen))
					{
						this.m_CommandBuffer.AddComponent(jobIndex, resident.m_Citizen, default(Deleted));
					}
					this.AddDeletedResident(DeletedResidentType.WaitingHome_AlreadyOutside);
					this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
					return true;
				}
				this.SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
				return false;
			}

			private bool ReachPathFailed(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				divert.m_Data += random.NextInt(1, 3);
				if (divert.m_Data <= 2500)
				{
					this.FindWaitingPosition(jobIndex, entity, ref random, ref resident, ref currentLane, ref target, ref divert, ref pathOwner);
					return false;
				}
				this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.None, Entity.Null, 0, Resource.NoResource);
				return false;
			}

			private bool ReturnHome(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				if (this.m_AttendingMeetingData.HasComponent(resident.m_Citizen))
				{
					AttendingMeeting attendingMeeting = this.m_AttendingMeetingData[resident.m_Citizen];
					if (this.m_CoordinatedMeetingData.HasComponent(attendingMeeting.m_Meeting) && this.m_CoordinatedMeetingData[attendingMeeting.m_Meeting].m_Target == target.m_Target)
					{
						this.m_CommandBuffer.RemoveComponent<AttendingMeeting>(jobIndex, resident.m_Citizen);
					}
				}
				bool movingAway;
				Entity homeBuilding = this.GetHomeBuilding(ref resident, out movingAway);
				if (homeBuilding != Entity.Null && homeBuilding != target.m_Target)
				{
					this.SetTarget(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, ref target, Purpose.GoingHome, homeBuilding);
					return false;
				}
				if (homeBuilding == Entity.Null && currentLane.m_Lane != Entity.Null)
				{
					if (movingAway)
					{
						this.AddDeletedResident(DeletedResidentType.NoPath_AlreadyMovingAway);
						if (this.m_CitizenData.HasComponent(resident.m_Citizen))
						{
							this.m_CommandBuffer.AddComponent(jobIndex, resident.m_Citizen, default(Deleted));
						}
						this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
						return true;
					}
					this.SetDivert(jobIndex, entity, ref resident, ref currentLane, ref divert, ref pathOwner, Purpose.WaitingHome, Entity.Null, 0, Resource.NoResource);
					return false;
				}
				if ((currentLane.m_Flags & CreatureLaneFlags.Connection) != 0 && this.m_ConnectionLaneData.TryGetComponent(currentLane.m_Lane, out var componentData) && (componentData.m_Flags & ConnectionLaneFlags.Outside) != 0)
				{
					if (this.m_CitizenData.HasComponent(resident.m_Citizen))
					{
						this.m_CommandBuffer.AddComponent(jobIndex, resident.m_Citizen, default(Deleted));
					}
					this.AddDeletedResident(DeletedResidentType.NoPathToHome_AlreadyOutside);
				}
				else
				{
					this.AddDeletedResident(DeletedResidentType.NoPathToHome);
				}
				this.m_CommandBuffer.AddComponent(jobIndex, entity, default(Deleted));
				return true;
			}

			private void FindWaitingPosition(int jobIndex, Entity entity, ref Unity.Mathematics.Random random, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Target target, ref Divert divert, ref PathOwner pathOwner)
			{
				if (this.m_PedestrianLaneData.HasComponent(currentLane.m_Lane))
				{
					if ((this.m_PedestrianLaneData[currentLane.m_Lane].m_Flags & PedestrianLaneFlags.Crosswalk) == 0 && !this.m_LaneSignalData.HasComponent(currentLane.m_Lane))
					{
						return;
					}
					pathOwner.m_ElementIndex = 0;
					DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
					dynamicBuffer.Clear();
					NativeParallelHashSet<Entity> ignoreLanes = new NativeParallelHashSet<Entity>(16, Allocator.Temp) { currentLane.m_Lane };
					Entity lane = currentLane.m_Lane;
					float2 yy = currentLane.m_CurvePosition.yy;
					if (yy.y >= 0.5f)
					{
						if (yy.y != 1f)
						{
							yy.y = 1f;
							dynamicBuffer.Add(new PathElement(currentLane.m_Lane, yy));
						}
					}
					else if (yy.y != 0f)
					{
						yy.y = 0f;
						dynamicBuffer.Add(new PathElement(currentLane.m_Lane, yy));
					}
					while (this.TryFindNextLane(ignoreLanes, ref lane, ref yy.y))
					{
						ignoreLanes.Add(lane);
						yy.x = yy.y;
						if ((this.m_PedestrianLaneData[lane].m_Flags & PedestrianLaneFlags.Crosswalk) == 0 && !this.m_LaneSignalData.HasComponent(lane))
						{
							yy.y = random.NextFloat(0f, 1f);
							dynamicBuffer.Add(new PathElement(lane, yy));
							break;
						}
						yy.y = math.select(0f, 1f, yy.x < 0.5f);
						dynamicBuffer.Add(new PathElement(lane, yy));
					}
					ignoreLanes.Dispose();
					if (dynamicBuffer.Length != 0)
					{
						currentLane.m_Flags &= ~(CreatureLaneFlags.EndOfPath | CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Transport | CreatureLaneFlags.Taxi | CreatureLaneFlags.Action);
					}
				}
				else if (this.m_ConnectionLaneData.HasComponent(currentLane.m_Lane) && (currentLane.m_Flags & CreatureLaneFlags.WaitPosition) == 0 && (this.m_ConnectionLaneData[currentLane.m_Lane].m_Flags & ConnectionLaneFlags.Outside) != 0)
				{
					currentLane.m_Flags &= ~CreatureLaneFlags.EndReached;
					currentLane.m_Flags |= CreatureLaneFlags.WaitPosition;
					currentLane.m_CurvePosition.y = random.NextFloat(0f, 1f);
				}
			}

			private bool TryFindNextLane(NativeParallelHashSet<Entity> ignoreLanes, ref Entity lane, ref float curveDelta)
			{
				if (!this.m_OwnerData.HasComponent(lane))
				{
					return false;
				}
				Owner owner = this.m_OwnerData[lane];
				if (this.TryFindNextLane(ignoreLanes, owner.m_Owner, ref lane, ref curveDelta))
				{
					return true;
				}
				if (this.m_ConnectedEdges.HasBuffer(owner.m_Owner))
				{
					DynamicBuffer<ConnectedEdge> dynamicBuffer = this.m_ConnectedEdges[owner.m_Owner];
					for (int i = 0; i < dynamicBuffer.Length; i++)
					{
						if (this.TryFindNextLane(ignoreLanes, dynamicBuffer[i].m_Edge, ref lane, ref curveDelta))
						{
							return true;
						}
					}
				}
				else if (this.m_EdgeData.HasComponent(owner.m_Owner))
				{
					Game.Net.Edge edge = this.m_EdgeData[owner.m_Owner];
					if (this.TryFindNextLane(ignoreLanes, edge.m_Start, ref lane, ref curveDelta))
					{
						return true;
					}
					if (this.TryFindNextLane(ignoreLanes, edge.m_End, ref lane, ref curveDelta))
					{
						return true;
					}
				}
				return false;
			}

			private bool TryFindNextLane(NativeParallelHashSet<Entity> ignoreLanes, Entity owner, ref Entity lane, ref float curveDelta)
			{
				if (!this.m_SubLanes.HasBuffer(owner))
				{
					return false;
				}
				DynamicBuffer<Game.Net.SubLane> dynamicBuffer = this.m_SubLanes[owner];
				Lane lane2 = this.m_LaneData[lane];
				PathNode other = ((curveDelta == 0f) ? lane2.m_StartNode : ((curveDelta != 1f) ? lane2.m_MiddleNode : lane2.m_EndNode));
				for (int i = 0; i < dynamicBuffer.Length; i++)
				{
					Entity subLane = dynamicBuffer[i].m_SubLane;
					if (!ignoreLanes.Contains(subLane) && this.m_PedestrianLaneData.HasComponent(subLane))
					{
						Lane lane3 = this.m_LaneData[subLane];
						if (lane3.m_StartNode.EqualsIgnoreCurvePos(other))
						{
							lane = subLane;
							curveDelta = 0f;
							return true;
						}
						if (lane3.m_EndNode.EqualsIgnoreCurvePos(other))
						{
							lane = subLane;
							curveDelta = 1f;
							return true;
						}
						if (lane3.m_MiddleNode.EqualsIgnoreCurvePos(other))
						{
							lane = subLane;
							curveDelta = lane3.m_MiddleNode.GetCurvePos();
							return true;
						}
					}
				}
				return false;
			}

			private void SetDivert(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Divert divert, ref PathOwner pathOwner, Purpose purpose, Entity targetEntity, int data = 0, Resource resource = Resource.NoResource)
			{
				if (purpose != Purpose.None)
				{
					bool num = divert.m_Purpose == Purpose.None;
					divert = new Divert
					{
						m_Purpose = purpose,
						m_Target = targetEntity,
						m_Data = data,
						m_Resource = resource
					};
					if (num)
					{
						this.m_CommandBuffer.AddComponent(jobIndex, entity, divert);
					}
					pathOwner.m_State |= PathFlags.DivertObsolete;
				}
				else if (divert.m_Purpose != Purpose.None)
				{
					divert = default(Divert);
					this.m_CommandBuffer.RemoveComponent<Divert>(jobIndex, entity);
					if ((pathOwner.m_State & PathFlags.CachedObsolete) != 0)
					{
						pathOwner.m_State &= ~PathFlags.CachedObsolete;
						pathOwner.m_State |= PathFlags.Obsolete;
					}
				}
				if ((resident.m_Flags & ResidentFlags.Arrived) != ResidentFlags.None && this.m_PrefabRefData.HasComponent(resident.m_Citizen))
				{
					this.m_CommandBuffer.RemoveComponent<CurrentBuilding>(jobIndex, resident.m_Citizen);
				}
				currentLane.m_Flags &= ~CreatureLaneFlags.EndOfPath;
				resident.m_Flags &= ~(ResidentFlags.Arrived | ResidentFlags.Hangaround | ResidentFlags.IgnoreBenches | ResidentFlags.IgnoreAreas | ResidentFlags.CannotIgnore);
				pathOwner.m_State &= ~PathFlags.Failed;
			}

			private void SetTarget(int jobIndex, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref Divert divert, ref PathOwner pathOwner, ref Target target, Purpose purpose, Entity targetEntity)
			{
				Entity source = Entity.Null;
				if ((resident.m_Flags & ResidentFlags.Arrived) != ResidentFlags.None)
				{
					if (this.m_PrefabRefData.HasComponent(resident.m_Citizen))
					{
						this.m_CommandBuffer.RemoveComponent<CurrentBuilding>(jobIndex, resident.m_Citizen);
					}
					source = target.m_Target;
				}
				Entity e = this.m_CommandBuffer.CreateEntity(jobIndex, this.m_ResetTripArchetype);
				this.m_CommandBuffer.SetComponent(jobIndex, e, new ResetTrip
				{
					m_Creature = entity,
					m_Source = source,
					m_Target = targetEntity,
					m_TravelPurpose = purpose
				});
			}

			private bool ParkingSpaceReached(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target, DynamicBuffer<GroupCreature> groupCreatures)
			{
				if ((currentLane.m_Flags & CreatureLaneFlags.Taxi) != 0)
				{
					if (this.m_RideNeederData.TryGetComponent(entity, out var componentData))
					{
						if (this.m_Dispatched.TryGetComponent(componentData.m_RideRequest, out var componentData2))
						{
							if (this.m_TaxiData.TryGetComponent(componentData2.m_Handler, out var componentData3) && this.m_ServiceDispatches.TryGetBuffer(componentData2.m_Handler, out var bufferData) && (componentData3.m_State & TaxiFlags.Dispatched) != 0 && bufferData.Length >= 1 && bufferData[0].m_Request == componentData.m_RideRequest)
							{
								if (this.m_CarNavigationLanes.HasBuffer(componentData2.m_Handler))
								{
									DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
									DynamicBuffer<CarNavigationLane> dynamicBuffer2 = this.m_CarNavigationLanes[componentData2.m_Handler];
									if (dynamicBuffer2.Length > 0 && dynamicBuffer.Length > pathOwner.m_ElementIndex)
									{
										PathElement value = dynamicBuffer[pathOwner.m_ElementIndex];
										CarNavigationLane carNavigationLane = dynamicBuffer2[dynamicBuffer2.Length - 1];
										if (carNavigationLane.m_Lane == value.m_Target && carNavigationLane.m_CurvePosition.y != value.m_TargetDelta.y && this.m_CurveData.HasComponent(currentLane.m_Lane) && this.m_CurveData.HasComponent(value.m_Target))
										{
											value.m_TargetDelta = carNavigationLane.m_CurvePosition.y;
											dynamicBuffer[pathOwner.m_ElementIndex] = value;
											float3 position = MathUtils.Position(this.m_CurveData[value.m_Target].m_Bezier, value.m_TargetDelta.y);
											MathUtils.Distance(this.m_CurveData[currentLane.m_Lane].m_Bezier, position, out var t);
											if (t != currentLane.m_CurvePosition.y)
											{
												currentLane.m_CurvePosition.y = t;
												currentLane.m_Flags &= ~CreatureLaneFlags.EndReached;
												return true;
											}
										}
									}
								}
								if ((componentData3.m_State & TaxiFlags.Boarding) != 0)
								{
									Game.Objects.Transform transform = this.m_TransformData[entity];
									this.m_BoardingQueue.Enqueue(Boarding.TryEnterVehicle(entity, Entity.Null, componentData2.m_Handler, Entity.Null, Entity.Null, transform.m_Position, CreatureVehicleFlags.Leader));
								}
							}
						}
						else if (this.m_ServiceRequestData.HasComponent(componentData.m_RideRequest) && this.m_ServiceRequestData[componentData.m_RideRequest].m_FailCount >= 3)
						{
							resident.m_Flags |= ResidentFlags.IgnoreTaxi;
							currentLane.m_Flags &= ~(CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Taxi);
							pathOwner.m_State &= ~PathFlags.Failed;
							pathOwner.m_State |= PathFlags.Obsolete;
							this.m_CommandBuffer.RemoveComponent<RideNeeder>(jobIndex, entity);
							return false;
						}
						return true;
					}
					this.m_CommandBuffer.AddComponent(jobIndex, entity, default(RideNeeder));
					return true;
				}
				BicycleOwner component2;
				if (this.m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
				{
					if (this.m_ParkedCarData.HasComponent(component.m_Car))
					{
						this.ActivateParkedCar(jobIndex, ref random, entity, component.m_Car, ref resident, ref pathOwner, ref target, groupCreatures);
						Game.Objects.Transform transform2 = this.m_TransformData[entity];
						this.m_BoardingQueue.Enqueue(Boarding.TryEnterVehicle(entity, Entity.Null, component.m_Car, Entity.Null, Entity.Null, transform2.m_Position, CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver));
						return true;
					}
				}
				else if (this.m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2) && this.m_ParkedCarData.HasComponent(component2.m_Bicycle))
				{
					this.ActivateParkedCar(jobIndex, ref random, entity, component2.m_Bicycle, ref resident, ref pathOwner, ref target, groupCreatures);
					Game.Objects.Transform transform3 = this.m_TransformData[entity];
					this.m_BoardingQueue.Enqueue(Boarding.TryEnterVehicle(entity, Entity.Null, component2.m_Bicycle, Entity.Null, Entity.Null, transform3.m_Position, CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver));
					return true;
				}
				currentLane.m_Flags &= ~(CreatureLaneFlags.ParkingSpace | CreatureLaneFlags.Taxi);
				pathOwner.m_State &= ~PathFlags.Failed;
				pathOwner.m_State |= PathFlags.Obsolete;
				return false;
			}

			private bool TransportStopReached(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, PrefabRef prefabRef, bool isUnspawned, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
				if (dynamicBuffer.Length >= pathOwner.m_ElementIndex + 2)
				{
					Entity target2 = dynamicBuffer[pathOwner.m_ElementIndex].m_Target;
					Entity target3 = dynamicBuffer[pathOwner.m_ElementIndex + 1].m_Target;
					if ((resident.m_Flags & ResidentFlags.WaitingTransport) == 0)
					{
						resident.m_Flags |= ResidentFlags.NoLateDeparture;
					}
					uint minDeparture = math.select(0u, this.m_SimulationFrameIndex, (resident.m_Flags & ResidentFlags.NoLateDeparture) != 0);
					if (RouteUtils.GetBoardingVehicle(currentLane.m_Lane, target2, target3, minDeparture, ref this.m_OwnerData, ref this.m_TargetData, ref this.m_RouteConnectedData, ref this.m_BoardingVehicleData, ref this.m_CurrentRouteData, ref this.m_AccessLaneLaneData, ref this.m_PublicTransportData, ref this.m_TaxiData, ref this.m_ConnectedRoutes, ref this.m_RouteWaypoints, out var vehicle, out var testing, out var obsolete))
					{
						this.TryEnterVehicle(entity, vehicle, target2, ref resident, ref currentLane);
						this.SetQueuePosition(entity, prefabRef, target2, ref currentLane);
						return true;
					}
					if (!obsolete)
					{
						if ((resident.m_Flags & ResidentFlags.WaitingTransport) == 0 || resident.m_Timer < 5000)
						{
							if (testing)
							{
								Game.Objects.Transform transform = this.m_TransformData[entity];
								this.m_BoardingQueue.Enqueue(Boarding.RequireStop(entity, vehicle, transform.m_Position));
							}
							if (isUnspawned && (currentLane.m_Flags & (CreatureLaneFlags.TransformTarget | CreatureLaneFlags.Connection)) != 0 && (currentLane.m_Flags & CreatureLaneFlags.WaitPosition) == 0)
							{
								currentLane.m_Flags &= ~CreatureLaneFlags.EndReached;
								currentLane.m_Flags |= CreatureLaneFlags.WaitPosition;
								currentLane.m_CurvePosition.y = random.NextFloat(0f, 1f);
							}
							this.SetQueuePosition(entity, prefabRef, target2, ref currentLane);
							return true;
						}
						this.m_BoardingQueue.Enqueue(Boarding.WaitTimeExceeded(entity, target2));
						if (this.m_BoardingVehicleData.HasComponent(target2))
						{
							resident.m_Flags |= ResidentFlags.IgnoreTaxi;
						}
						else
						{
							resident.m_Flags |= ResidentFlags.IgnoreTransport;
						}
					}
				}
				currentLane.m_Flags &= ~CreatureLaneFlags.Transport;
				pathOwner.m_State &= ~PathFlags.Failed;
				pathOwner.m_State |= PathFlags.Obsolete;
				return false;
			}

			private void SetQueuePosition(Entity entity, PrefabRef prefabRef, Entity targetEntity, ref HumanCurrentLane currentLane)
			{
				Game.Objects.Transform transform = this.m_TransformData[entity];
				Sphere3 queueArea = CreatureUtils.GetQueueArea(this.m_PrefabObjectGeometryData[prefabRef.m_Prefab], transform.m_Position);
				CreatureUtils.SetQueue(ref currentLane.m_QueueEntity, ref currentLane.m_QueueArea, targetEntity, queueArea);
			}

			private Entity GetHomeBuilding(ref Game.Creatures.Resident resident, out bool movingAway)
			{
				movingAway = false;
				if (this.m_HouseholdMembers.TryGetComponent(resident.m_Citizen, out var componentData))
				{
					if (this.m_MovingAwayData.HasComponent(componentData.m_Household))
					{
						movingAway = true;
						return Entity.Null;
					}
					if (this.m_PropertyRenters.TryGetComponent(componentData.m_Household, out var componentData2) && this.m_EntityLookup.Exists(componentData2.m_Property) && !this.m_DeletedData.HasComponent(componentData2.m_Property))
					{
						return componentData2.m_Property;
					}
					if (this.m_TouristHouseholds.TryGetComponent(componentData.m_Household, out var componentData3) && this.m_PropertyRenters.TryGetComponent(componentData3.m_Hotel, out componentData2) && this.m_EntityLookup.Exists(componentData2.m_Property) && !this.m_DeletedData.HasComponent(componentData2.m_Property))
					{
						return componentData2.m_Property;
					}
					if (this.m_HomelessHouseholdData.TryGetComponent(componentData.m_Household, out var componentData4) && this.m_EntityLookup.Exists(componentData4.m_TempHome) && !this.m_DeletedData.HasComponent(componentData4.m_TempHome))
					{
						return componentData4.m_TempHome;
					}
					if (this.m_CitizenData.TryGetComponent(resident.m_Citizen, out var componentData5))
					{
						movingAway = (componentData5.m_State & CitizenFlags.Commuter) != CitizenFlags.None || !this.m_EntityLookup.Exists(componentData.m_Household);
						return Entity.Null;
					}
				}
				movingAway = true;
				return Entity.Null;
			}

			private void TryEnterVehicle(Entity entity, Entity vehicle, Entity waypoint, ref Game.Creatures.Resident resident, ref HumanCurrentLane currentLane)
			{
				Game.Objects.Transform transform = this.m_TransformData[entity];
				this.m_BoardingQueue.Enqueue(Boarding.TryEnterVehicle(entity, Entity.Null, vehicle, Entity.Null, waypoint, transform.m_Position, CreatureVehicleFlags.Leader));
			}

			private void FinishEnterVehicle(Entity entity, Entity vehicle, Entity controllerVehicle, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane)
			{
				Entity household = this.GetHousehold(resident);
				int ticketPrice = this.GetTicketPrice(vehicle);
				this.m_BoardingQueue.Enqueue(Boarding.FinishEnterVehicle(entity, household, vehicle, controllerVehicle, currentLane, ticketPrice));
				human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
			}

			private void FinishExitVehicle(Entity entity, Entity vehicle, ref HumanCurrentLane currentLane)
			{
				currentLane.m_Flags &= ~(CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached);
				this.m_BoardingQueue.Enqueue(Boarding.FinishExitVehicle(entity, vehicle));
			}

			private void CancelEnterVehicle(Entity entity, Entity vehicle, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner)
			{
				this.m_BoardingQueue.Enqueue(Boarding.CancelEnterVehicle(entity, vehicle));
				human.m_Flags &= ~(HumanFlags.Run | HumanFlags.Emergency);
				DynamicBuffer<PathElement> dynamicBuffer = this.m_PathElements[entity];
				for (int i = pathOwner.m_ElementIndex; i < dynamicBuffer.Length; i++)
				{
					if (dynamicBuffer[i].m_Target == vehicle)
					{
						dynamicBuffer.RemoveRange(0, i + 1);
						pathOwner.m_ElementIndex = 0;
						return;
					}
				}
				dynamicBuffer.Clear();
				pathOwner.m_ElementIndex = 0;
				pathOwner.m_State &= ~PathFlags.Failed;
				pathOwner.m_State |= PathFlags.Obsolete;
			}

			private Entity GetHousehold(Game.Creatures.Resident resident)
			{
				if (this.m_HouseholdMembers.HasComponent(resident.m_Citizen))
				{
					return this.m_HouseholdMembers[resident.m_Citizen].m_Household;
				}
				return Entity.Null;
			}

			private int GetTicketPrice(Entity vehicle)
			{
				if (this.m_CurrentRouteData.HasComponent(vehicle))
				{
					CurrentRoute currentRoute = this.m_CurrentRouteData[vehicle];
					if (this.m_TransportLineData.HasComponent(currentRoute.m_Route))
					{
						return this.m_TransportLineData[currentRoute.m_Route].m_TicketPrice;
					}
				}
				return 0;
			}

			private void FindNewPath(Entity entity, PrefabRef prefabRef, ref Game.Creatures.Resident resident, ref Human human, ref HumanCurrentLane currentLane, ref PathOwner pathOwner, ref Target target, ref Divert divert)
			{
				CreatureData creatureData = this.m_PrefabCreatureData[prefabRef.m_Prefab];
				HumanData humanData = this.m_PrefabHumanData[prefabRef.m_Prefab];
				pathOwner.m_State &= ~(PathFlags.AddDestination | PathFlags.Divert);
				PathfindParameters parameters = new PathfindParameters
				{
					m_MaxSpeed = 277.77777f,
					m_WalkSpeed = humanData.m_WalkSpeed,
					m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
					m_Methods = (PathMethod.Pedestrian | RouteUtils.GetTaxiMethods(resident) | RouteUtils.GetPublicTransportMethods(resident, this.m_TimeOfDay)),
					m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
					m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost
				};
				SetupQueueTarget origin = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = PathMethod.Pedestrian,
					m_RandomCost = 30f
				};
				SetupQueueTarget destination = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = PathMethod.Pedestrian,
					m_Entity = target.m_Target,
					m_RandomCost = 30f,
					m_ActivityMask = creatureData.m_SupportedActivities
				};
				Entity entity2 = Entity.Null;
				Entity entity3 = target.m_Target;
				if (this.m_PropertyRenters.TryGetComponent(target.m_Target, out var componentData))
				{
					entity3 = componentData.m_Property;
				}
				if (this.m_HouseholdMembers.TryGetComponent(resident.m_Citizen, out var componentData2) && this.m_PropertyRenters.TryGetComponent(componentData2.m_Household, out componentData))
				{
					entity2 = componentData.m_Property;
					parameters.m_Authorization1 = componentData.m_Property;
				}
				if (this.m_CitizenData.TryGetComponent(resident.m_Citizen, out var componentData3))
				{
					Household household = this.m_HouseholdData[componentData2.m_Household];
					DynamicBuffer<HouseholdCitizen> dynamicBuffer = this.m_HouseholdCitizens[componentData2.m_Household];
					parameters.m_Weights = CitizenUtils.GetPathfindWeights(componentData3, household, dynamicBuffer.Length);
				}
				if (this.m_WorkerData.HasComponent(resident.m_Citizen))
				{
					Worker worker = this.m_WorkerData[resident.m_Citizen];
					if (this.m_PropertyRenters.HasComponent(worker.m_Workplace))
					{
						parameters.m_Authorization2 = this.m_PropertyRenters[worker.m_Workplace].m_Property;
					}
					else
					{
						parameters.m_Authorization2 = worker.m_Workplace;
					}
				}
				BicycleOwner component2;
				if (this.m_CarKeeperData.TryGetEnabledComponent(resident.m_Citizen, out var component))
				{
					if (this.m_ParkedCarData.TryGetComponent(component.m_Car, out var componentData4))
					{
						PrefabRef prefabRef2 = this.m_PrefabRefData[component.m_Car];
						CarData carData = this.m_PrefabCarData[prefabRef2.m_Prefab];
						parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
						parameters.m_ParkingTarget = componentData4.m_Lane;
						parameters.m_ParkingDelta = componentData4.m_CurvePosition;
						parameters.m_ParkingSize = VehicleUtils.GetParkingSize(component.m_Car, ref this.m_PrefabRefData, ref this.m_PrefabObjectGeometryData);
						parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
						if (this.m_PersonalCarData.TryGetComponent(component.m_Car, out var componentData5) && (componentData5.m_State & PersonalCarFlags.HomeTarget) == 0)
						{
							parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
						}
					}
				}
				else if (this.m_BicycleOwnerData.TryGetEnabledComponent(resident.m_Citizen, out component2))
				{
					if (!this.m_PrefabRefData.TryGetComponent(component2.m_Bicycle, out var componentData6) && this.m_CurrentBuildingData.TryGetComponent(resident.m_Citizen, out var componentData7) && componentData7.m_CurrentBuilding == entity2)
					{
						Unity.Mathematics.Random random = componentData3.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
						componentData6.m_Prefab = this.m_PersonalCarSelectData.SelectVehiclePrefab(ref random, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, out var _);
					}
					if (this.m_PrefabCarData.TryGetComponent(componentData6.m_Prefab, out var componentData8) && this.m_PrefabObjectGeometryData.TryGetComponent(componentData6.m_Prefab, out var componentData9))
					{
						parameters.m_MaxSpeed.x = componentData8.m_MaxSpeed;
						parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData9, out var _);
						parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
						if (this.m_ParkedCarData.TryGetComponent(component2.m_Bicycle, out var componentData10))
						{
							parameters.m_ParkingTarget = componentData10.m_Lane;
							parameters.m_ParkingDelta = componentData10.m_CurvePosition;
							if (this.m_PersonalCarData.TryGetComponent(component2.m_Bicycle, out var componentData11) && (componentData11.m_State & PersonalCarFlags.HomeTarget) == 0)
							{
								parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
							}
						}
						if (entity3 == entity2 && divert.m_Purpose == Purpose.None)
						{
							destination.m_Methods |= PathMethod.Bicycle;
							destination.m_RoadTypes |= RoadTypes.Bicycle;
						}
					}
				}
				bool flag = false;
				if (this.m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData12))
				{
					switch (componentData12.m_Purpose)
					{
					case Purpose.EmergencyShelter:
						parameters.m_Weights = new PathfindWeights(1f, 0.2f, 0f, 0.1f);
						break;
					case Purpose.Hospital:
					case Purpose.Deathcare:
					{
						flag = this.m_HealthProblemData.TryGetComponent(resident.m_Citizen, out var componentData13) && (componentData13.m_Flags & HealthProblemFlags.RequireTransport) != 0;
						break;
					}
					case Purpose.MovingAway:
						parameters.m_MaxCost = CitizenBehaviorSystem.kMaxMovingAwayCost;
						break;
					}
				}
				if ((resident.m_Flags & ResidentFlags.IgnoreBenches) != ResidentFlags.None)
				{
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.BenchSitting).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.PullUps).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Reading).m_Mask;
				}
				if ((resident.m_Flags & ResidentFlags.IgnoreAreas) != ResidentFlags.None)
				{
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Standing).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.GroundLaying).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.GroundSitting).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.PushUps).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.SitUps).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.JumpingJacks).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.JumpingLunges).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Squats).m_Mask;
					destination.m_ActivityMask.m_Mask &= ~new ActivityMask(ActivityType.Yoga).m_Mask;
				}
				if (flag)
				{
					human.m_Flags |= HumanFlags.Carried;
					currentLane.m_CurvePosition.y = currentLane.m_CurvePosition.x;
					pathOwner.m_ElementIndex = 0;
					pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Obsolete | PathFlags.DivertObsolete | PathFlags.CachedObsolete);
					this.m_PathElements[entity].Clear();
				}
				else if (CreatureUtils.DivertDestination(ref destination, ref pathOwner, divert))
				{
					CreatureUtils.SetupPathfind(item: new SetupQueueItem(entity, parameters, origin, destination), currentLane: ref currentLane, pathOwner: ref pathOwner, queue: this.m_PathfindQueue);
				}
				else
				{
					currentLane.m_CurvePosition.y = currentLane.m_CurvePosition.x;
					pathOwner.m_ElementIndex = 0;
					pathOwner.m_State |= PathFlags.CachedObsolete;
					pathOwner.m_State &= ~(PathFlags.Failed | PathFlags.Obsolete | PathFlags.DivertObsolete);
					this.m_PathElements[entity].Clear();
				}
			}

			private void ActivateParkedCar(int jobIndex, ref Unity.Mathematics.Random random, Entity entity, Entity carEntity, ref Game.Creatures.Resident resident, ref PathOwner pathOwner, ref Target target, DynamicBuffer<GroupCreature> groupCreatures)
			{
				ParkedCar parkedCar = this.m_ParkedCarData[carEntity];
				Game.Vehicles.CarLaneFlags carLaneFlags = Game.Vehicles.CarLaneFlags.EndReached | Game.Vehicles.CarLaneFlags.ParkingSpace | Game.Vehicles.CarLaneFlags.FixedLane;
				DynamicBuffer<LayoutElement> dynamicBuffer = default(DynamicBuffer<LayoutElement>);
				if (this.m_VehicleLayouts.HasBuffer(carEntity))
				{
					dynamicBuffer = this.m_VehicleLayouts[carEntity];
				}
				if (parkedCar.m_Lane == Entity.Null)
				{
					DynamicBuffer<PathElement> dynamicBuffer2 = this.m_PathElements[entity];
					if (dynamicBuffer2.Length > pathOwner.m_ElementIndex)
					{
						PathElement pathElement = dynamicBuffer2[pathOwner.m_ElementIndex];
						if (this.m_CurveData.HasComponent(pathElement.m_Target))
						{
							parkedCar.m_Lane = pathElement.m_Target;
							Curve curve = this.m_CurveData[parkedCar.m_Lane];
							Game.Objects.Transform transform = this.m_TransformData[entity];
							MathUtils.Distance(curve.m_Bezier, transform.m_Position, out parkedCar.m_CurvePosition);
							Game.Objects.Transform component = VehicleUtils.CalculateTransform(curve, parkedCar.m_CurvePosition);
							bool flag = false;
							if (this.m_ConnectionLaneData.HasComponent(parkedCar.m_Lane))
							{
								Game.Net.ConnectionLane connectionLane = this.m_ConnectionLaneData[parkedCar.m_Lane];
								if ((connectionLane.m_Flags & ConnectionLaneFlags.Parking) != 0)
								{
									parkedCar.m_CurvePosition = random.NextFloat(0f, 1f);
									component.m_Position = VehicleUtils.GetConnectionParkingPosition(connectionLane, curve.m_Bezier, parkedCar.m_CurvePosition);
								}
								flag = true;
							}
							this.m_CommandBuffer.SetComponent(jobIndex, carEntity, component);
							if (flag)
							{
								this.m_CommandBuffer.AddComponent(jobIndex, carEntity, default(Unspawned));
							}
							if (dynamicBuffer.IsCreated)
							{
								for (int i = 1; i < dynamicBuffer.Length; i++)
								{
									Entity vehicle = dynamicBuffer[i].m_Vehicle;
									this.m_CommandBuffer.SetComponent(jobIndex, vehicle, component);
									if (flag)
									{
										this.m_CommandBuffer.AddComponent(jobIndex, vehicle, default(Unspawned));
									}
								}
							}
						}
					}
				}
				if (this.m_ConnectionLaneData.HasComponent(parkedCar.m_Lane))
				{
					carLaneFlags = (((this.m_ConnectionLaneData[parkedCar.m_Lane].m_Flags & ConnectionLaneFlags.Area) == 0) ? (carLaneFlags | Game.Vehicles.CarLaneFlags.Connection) : (carLaneFlags | Game.Vehicles.CarLaneFlags.Area));
				}
				Game.Vehicles.PersonalCar component2 = this.m_PersonalCarData[carEntity];
				component2.m_State |= PersonalCarFlags.Boarding;
				this.m_CommandBuffer.RemoveComponent(jobIndex, carEntity, in this.m_ParkedToMovingCarRemoveTypes);
				this.m_CommandBuffer.AddComponent(jobIndex, carEntity, in this.m_ParkedToMovingCarAddTypes);
				this.m_CommandBuffer.SetComponent(jobIndex, carEntity, component2);
				this.m_CommandBuffer.SetComponent(jobIndex, carEntity, new CarCurrentLane(parkedCar, carLaneFlags));
				if (this.m_ParkingLaneData.HasComponent(parkedCar.m_Lane) || this.m_GarageLaneData.HasComponent(parkedCar.m_Lane))
				{
					this.m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, parkedCar.m_Lane);
				}
				if (dynamicBuffer.IsCreated)
				{
					for (int j = 1; j < dynamicBuffer.Length; j++)
					{
						Entity vehicle2 = dynamicBuffer[j].m_Vehicle;
						ParkedCar parkedCar2 = this.m_ParkedCarData[vehicle2];
						if (parkedCar2.m_Lane == Entity.Null)
						{
							parkedCar2.m_Lane = parkedCar.m_Lane;
							parkedCar2.m_CurvePosition = parkedCar.m_CurvePosition;
						}
						this.m_CommandBuffer.RemoveComponent(jobIndex, vehicle2, in this.m_ParkedToMovingCarRemoveTypes);
						this.m_CommandBuffer.AddComponent(jobIndex, vehicle2, in this.m_ParkedToMovingTrailerAddTypes);
						this.m_CommandBuffer.SetComponent(jobIndex, vehicle2, new CarTrailerLane(parkedCar2));
						if ((this.m_ParkingLaneData.HasComponent(parkedCar2.m_Lane) || this.m_GarageLaneData.HasComponent(parkedCar2.m_Lane)) && parkedCar2.m_Lane != parkedCar.m_Lane)
						{
							this.m_CommandBuffer.AddComponent<PathfindUpdated>(jobIndex, parkedCar2.m_Lane);
						}
					}
				}
				if (dynamicBuffer.IsCreated && dynamicBuffer.Length > 1)
				{
					return;
				}
				int num = 1;
				int num2 = 0;
				if (groupCreatures.IsCreated)
				{
					for (int k = 0; k < groupCreatures.Length; k++)
					{
						if (this.m_AnimalData.HasComponent(groupCreatures[k].m_Creature))
						{
							num2++;
						}
						else
						{
							num++;
						}
					}
				}
				int num3 = num;
				int num4 = 1 + num2;
				if (this.m_TravelPurposeData.HasComponent(resident.m_Citizen))
				{
					switch (this.m_TravelPurposeData[resident.m_Citizen].m_Purpose)
					{
					case Purpose.MovingAway:
						if (random.NextInt(20) == 0)
						{
							num3 += 5;
							num4 += 5;
						}
						else if (random.NextInt(10) == 0)
						{
							num4 += 5;
							if (random.NextInt(10) == 0)
							{
								num4 += 5;
							}
						}
						break;
					case Purpose.Leisure:
						if (random.NextInt(20) == 0)
						{
							num3 += 5;
							num4 += 5;
						}
						break;
					case Purpose.Shopping:
						if (random.NextInt(10) == 0)
						{
							num4 += 5;
							if (random.NextInt(10) == 0)
							{
								num4 += 5;
							}
						}
						break;
					}
				}
				Game.Objects.Transform tractorTransform = this.m_TransformData[carEntity];
				PrefabRef prefabRef = this.m_PrefabRefData[carEntity];
				Entity entity2 = this.m_PersonalCarSelectData.CreateTrailer(this.m_CommandBuffer, jobIndex, ref random, num3, num4, noSlowVehicles: false, prefabRef.m_Prefab, tractorTransform, (PersonalCarFlags)0u, stopped: false);
				if (entity2 != Entity.Null)
				{
					DynamicBuffer<LayoutElement> dynamicBuffer3 = ((!dynamicBuffer.IsCreated) ? this.m_CommandBuffer.AddBuffer<LayoutElement>(jobIndex, carEntity) : this.m_CommandBuffer.SetBuffer<LayoutElement>(jobIndex, carEntity));
					dynamicBuffer3.Add(new LayoutElement(carEntity));
					dynamicBuffer3.Add(new LayoutElement(entity2));
					this.m_CommandBuffer.SetComponent(jobIndex, entity2, new Controller(carEntity));
					this.m_CommandBuffer.SetComponent(jobIndex, entity2, new CarTrailerLane(parkedCar));
				}
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		[BurstCompile]
		private struct BoardingJob : IJob
		{
			[ReadOnly]
			public ComponentLookup<Unspawned> m_Unspawneds;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_Transforms;

			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

			[ReadOnly]
			public ComponentLookup<TaxiData> m_TaxiData;

			[ReadOnly]
			public ComponentLookup<PublicTransportVehicleData> m_PublicTransportVehicleData;

			[ReadOnly]
			public ComponentLookup<PersonalCarData> m_PrefabPersonalCarData;

			[ReadOnly]
			public ComponentLookup<Target> m_Targets;

			[ReadOnly]
			public ComponentLookup<Connected> m_Connecteds;

			[ReadOnly]
			public ComponentLookup<Owner> m_Owners;

			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			[ReadOnly]
			public ComponentLookup<Bicycle> m_Bicycles;

			[ReadOnly]
			public ComponentLookup<HumanCurrentLane> m_HumanCurrentLanes;

			[ReadOnly]
			public BufferLookup<GroupCreature> m_GroupCreatures;

			[ReadOnly]
			public BufferLookup<LayoutElement> m_VehicleLayouts;

			[ReadOnly]
			public BufferLookup<ActivityLocationElement> m_ActivityLocations;

			[NativeDisableParallelForRestriction]
			public BufferLookup<PlaybackLayer> m_PlaybackLayers;

			public ComponentLookup<Citizen> m_Citizens;

			public ComponentLookup<Game.Creatures.Resident> m_Residents;

			public ComponentLookup<Creature> m_Creatures;

			public ComponentLookup<Game.Vehicles.Taxi> m_Taxis;

			public ComponentLookup<Game.Vehicles.PublicTransport> m_PublicTransports;

			public ComponentLookup<WaitingPassengers> m_WaitingPassengers;

			public BufferLookup<Queue> m_Queues;

			public BufferLookup<Passenger> m_Passengers;

			public BufferLookup<LaneObject> m_LaneObjects;

			public BufferLookup<Game.Economy.Resources> m_Resources;

			public ComponentLookup<PlayerMoney> m_PlayerMoney;

			[ReadOnly]
			public Entity m_City;

			[ReadOnly]
			public ComponentTypeSet m_CurrentLaneTypes;

			[ReadOnly]
			public ComponentTypeSet m_CurrentLaneTypesRelative;

			public NativeQueue<Boarding> m_BoardingQueue;

			public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_SearchTree;

			public EntityCommandBuffer m_CommandBuffer;

			public NativeQueue<StatisticsEvent>.ParallelWriter m_StatisticsEventQueue;

			public NativeQueue<TransportUsageEvent> m_TransportUsageQueue;

			public NativeQueue<ServiceFeeSystem.FeeEvent> m_FeeQueue;

			public void Execute()
			{
				NativeParallelHashMap<Entity, int3> freeSpaceMap = default(NativeParallelHashMap<Entity, int3>);
				Boarding item;
				while (this.m_BoardingQueue.TryDequeue(out item))
				{
					switch (item.m_Type)
					{
					case BoardingType.Exit:
						this.ExitVehicle(ref freeSpaceMap, item.m_Passenger, item.m_Household, item.m_Leader, item.m_Vehicle, item.m_CurrentLane, item.m_Position, item.m_Rotation, item.m_TicketPrice);
						break;
					case BoardingType.TryEnter:
						this.TryEnterVehicle(ref freeSpaceMap, item.m_Passenger, item.m_Leader, item.m_Vehicle, item.m_LeaderVehicle, item.m_Waypoint, item.m_Position, item.m_Flags);
						break;
					case BoardingType.FinishEnter:
						this.FinishEnterVehicle(item.m_Passenger, item.m_Household, item.m_Vehicle, item.m_LeaderVehicle, item.m_CurrentLane, item.m_TicketPrice);
						break;
					case BoardingType.CancelEnter:
						this.CancelEnterVehicle(ref freeSpaceMap, item.m_Passenger, item.m_Vehicle);
						break;
					case BoardingType.RequireStop:
						this.RequireStop(ref freeSpaceMap, item.m_Passenger, item.m_Vehicle, item.m_Position);
						break;
					case BoardingType.WaitTimeExceeded:
						this.WaitTimeExceeded(item.m_Passenger, item.m_Waypoint);
						break;
					case BoardingType.WaitTimeEstimate:
						this.WaitTimeEstimate(item.m_Waypoint, item.m_TicketPrice);
						break;
					case BoardingType.FinishExit:
						this.FinishExitVehicle(ref freeSpaceMap, item.m_Passenger, item.m_Vehicle);
						break;
					}
				}
				if (freeSpaceMap.IsCreated)
				{
					freeSpaceMap.Dispose();
				}
			}

			private void ExitVehicle(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity passenger, Entity household, Entity leader, Entity vehicle, HumanCurrentLane newCurrentLane, float3 position, quaternion rotation, int ticketPrice)
			{
				this.m_CommandBuffer.RemoveComponent<Relative>(passenger);
				Game.Creatures.Resident value = this.m_Residents[passenger];
				value.m_Flags &= ~ResidentFlags.InVehicle;
				value.m_Timer = 0;
				this.m_Residents[passenger] = value;
				if (this.m_PlaybackLayers.TryGetBuffer(vehicle, out var bufferData) && bufferData.IsCreated)
				{
					int length = bufferData.Length;
					for (int i = 0; i < length; i++)
					{
						bufferData.ElementAt(i).m_RelativeClipTime = 0f;
					}
				}
				if (newCurrentLane.m_Lane == Entity.Null && this.m_HumanCurrentLanes.TryGetComponent(leader, out var componentData))
				{
					newCurrentLane.m_Lane = componentData.m_Lane;
					newCurrentLane.m_CurvePosition = componentData.m_CurvePosition;
					newCurrentLane.m_Flags |= (CreatureLaneFlags)(((uint)componentData.m_Flags & 0xFFDFFFFFu) | 0x100000);
				}
				if (this.m_LaneObjects.HasBuffer(newCurrentLane.m_Lane))
				{
					NetUtils.AddLaneObject(this.m_LaneObjects[newCurrentLane.m_Lane], passenger, newCurrentLane.m_CurvePosition.x);
				}
				else
				{
					PrefabRef prefabRef = this.m_PrefabRefData[passenger];
					ObjectGeometryData geometryData = this.m_ObjectGeometryData[prefabRef.m_Prefab];
					Bounds3 bounds = ObjectUtils.CalculateBounds(position, quaternion.identity, geometryData);
					this.m_SearchTree.Add(passenger, new QuadTreeBoundsXZ(bounds));
				}
				this.m_CommandBuffer.AddComponent(passenger, in this.m_CurrentLaneTypes);
				this.m_CommandBuffer.AddComponent(passenger, default(Updated));
				this.m_CommandBuffer.SetComponent(passenger, newCurrentLane);
				this.m_CommandBuffer.SetComponent(passenger, new Game.Objects.Transform(position, rotation));
				if (ticketPrice != 0 && this.m_Resources.HasBuffer(household) && this.m_PlayerMoney.HasComponent(this.m_City))
				{
					DynamicBuffer<Game.Economy.Resources> resources = this.m_Resources[household];
					if (ticketPrice > 0)
					{
						PlayerMoney value2 = this.m_PlayerMoney[this.m_City];
						value2.Add(ticketPrice);
						this.m_PlayerMoney[this.m_City] = value2;
						this.m_FeeQueue.Enqueue(new ServiceFeeSystem.FeeEvent
						{
							m_Amount = 1f,
							m_Cost = ticketPrice,
							m_Resource = PlayerResource.PublicTransport,
							m_Outside = false
						});
						ticketPrice = -ticketPrice;
					}
					EconomyUtils.AddResources(Resource.Money, ticketPrice, resources);
				}
			}

			private void FinishExitVehicle(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity passenger, Entity vehicle)
			{
				if (this.m_Passengers.HasBuffer(vehicle))
				{
					if (CollectionUtils.RemoveValue(this.m_Passengers[vehicle], new Passenger(passenger)))
					{
						int3 freeSpace = this.GetFreeSpace(ref freeSpaceMap, vehicle);
						freeSpace.x++;
						freeSpaceMap[vehicle] = freeSpace;
					}
					PrefabRef prefabRef = this.m_PrefabRefData[vehicle];
					if (this.m_PublicTransportVehicleData.HasComponent(prefabRef.m_Prefab) && this.m_PublicTransports.HasComponent(vehicle))
					{
						TransportType transportType = this.m_PublicTransportVehicleData[prefabRef.m_Prefab].m_TransportType;
						if ((transportType == TransportType.Airplane || transportType == TransportType.Train || transportType == TransportType.Ship) && this.m_Targets.TryGetComponent(vehicle, out var componentData) && this.m_Connecteds.TryGetComponent(componentData.m_Target, out var componentData2) && !this.m_OutsideConnections.HasComponent(componentData2.m_Connected))
						{
							Entity topOwner = BuildingUtils.GetTopOwner(componentData2.m_Connected, ref this.m_Owners);
							this.m_TransportUsageQueue.Enqueue(new TransportUsageEvent
							{
								m_Building = topOwner,
								m_TransportedPassenger = 1,
								m_TransportType = transportType
							});
						}
					}
				}
				this.m_CommandBuffer.RemoveComponent<CurrentVehicle>(passenger);
				this.m_CommandBuffer.AddComponent(passenger, default(BatchesUpdated));
			}

			private Entity TryFindVehicle(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity vehicle, Entity leaderVehicle, float3 position, bool isLeader, int requiredSpace, out float distance)
			{
				Entity result = vehicle;
				int3 @int = 0;
				if (this.m_VehicleLayouts.TryGetBuffer(vehicle, out var bufferData))
				{
					distance = float.MaxValue;
					int num = 0;
					for (int i = 0; i < bufferData.Length; i++)
					{
						Entity vehicle2 = bufferData[i].m_Vehicle;
						float num2 = math.distancesq(position, this.m_Transforms[vehicle2].m_Position);
						int3 freeSpace = this.GetFreeSpace(ref freeSpaceMap, vehicle2);
						if (isLeader)
						{
							@int.xy += freeSpace.xy;
							@int.z |= freeSpace.z;
							freeSpace.x = math.min(freeSpace.x, requiredSpace);
						}
						else
						{
							freeSpace.x += math.select(0, requiredSpace, vehicle2 == leaderVehicle);
							@int.xy += freeSpace.xy;
							@int.z |= freeSpace.z;
							freeSpace.x = math.min(freeSpace.x, 1) * 2;
							freeSpace.x += math.select(0, 1, vehicle2 == leaderVehicle);
						}
						if ((freeSpace.x > num) | ((freeSpace.x == num) & (num2 < distance)))
						{
							distance = num2;
							num = freeSpace.x;
							result = vehicle2;
							if ((freeSpace.z & 4) != 0 && isLeader)
							{
								break;
							}
						}
					}
					distance = math.sqrt(distance);
				}
				else
				{
					@int = this.GetFreeSpace(ref freeSpaceMap, vehicle);
					distance = math.distance(position, this.m_Transforms[vehicle].m_Position);
				}
				if (isLeader)
				{
					if ((@int.z & 1) != 0 && @int.x >= requiredSpace)
					{
						return result;
					}
					if ((@int.z & 6) != 0 && @int.x == @int.y)
					{
						return result;
					}
					return Entity.Null;
				}
				return result;
			}

			private int3 GetFreeSpace(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity vehicle)
			{
				if (!freeSpaceMap.IsCreated)
				{
					freeSpaceMap = new NativeParallelHashMap<Entity, int3>(20, Allocator.Temp);
				}
				if (freeSpaceMap.TryGetValue(vehicle, out var item))
				{
					return item;
				}
				if (this.m_Passengers.TryGetBuffer(vehicle, out var bufferData))
				{
					item = 0;
					for (int i = 0; i < bufferData.Length; i++)
					{
						Passenger passenger = bufferData[i];
						if (this.m_Residents.TryGetComponent(passenger.m_Passenger, out var componentData) && (componentData.m_Flags & ResidentFlags.InVehicle) != ResidentFlags.None)
						{
							item.x -= 1 + this.GetPendingGroupMemberCount(passenger.m_Passenger);
						}
					}
					PrefabRef prefabRef = this.m_PrefabRefData[vehicle];
					TaxiData componentData3;
					PersonalCarData componentData4;
					if (this.m_PublicTransportVehicleData.TryGetComponent(prefabRef.m_Prefab, out var componentData2))
					{
						item.xy += componentData2.m_PassengerCapacity;
						item.z |= 1;
					}
					else if (this.m_TaxiData.TryGetComponent(prefabRef.m_Prefab, out componentData3))
					{
						item.xy += componentData3.m_PassengerCapacity;
						item.z |= 2;
					}
					else if (this.m_PrefabPersonalCarData.TryGetComponent(prefabRef.m_Prefab, out componentData4))
					{
						item.xy += componentData4.m_PassengerCapacity;
						item.z |= 4;
					}
					else
					{
						item.xy += 1000000;
						item.z |= 1;
					}
					freeSpaceMap.Add(vehicle, item);
					return item;
				}
				freeSpaceMap.Add(vehicle, 0);
				return 0;
			}

			private int GetPendingGroupMemberCount(Entity leader)
			{
				int num = 0;
				if (this.m_GroupCreatures.TryGetBuffer(leader, out var bufferData))
				{
					for (int i = 0; i < bufferData.Length; i++)
					{
						GroupCreature groupCreature = bufferData[i];
						if (this.m_Residents.TryGetComponent(groupCreature.m_Creature, out var componentData) && (componentData.m_Flags & ResidentFlags.InVehicle) == 0)
						{
							num++;
						}
					}
				}
				return num;
			}

			private void TryEnterVehicle(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity passenger, Entity leader, Entity vehicle, Entity leaderVehicle, Entity waypoint, float3 position, CreatureVehicleFlags flags)
			{
				int num;
				if ((flags & CreatureVehicleFlags.Leader) != 0)
				{
					Entity entity = vehicle;
					num = 1 + this.GetPendingGroupMemberCount(passenger);
					vehicle = this.TryFindVehicle(ref freeSpaceMap, vehicle, Entity.Null, position, isLeader: true, num, out var distance);
					if (vehicle == Entity.Null)
					{
						return;
					}
					if (this.m_Taxis.TryGetComponent(entity, out var componentData) && distance > componentData.m_MaxBoardingDistance)
					{
						componentData.m_MinWaitingDistance = math.min(componentData.m_MinWaitingDistance, distance);
						this.m_Taxis[entity] = componentData;
						return;
					}
					if (this.m_PublicTransports.TryGetComponent(entity, out var componentData2) && distance > componentData2.m_MaxBoardingDistance)
					{
						componentData2.m_MinWaitingDistance = math.min(componentData2.m_MinWaitingDistance, distance);
						this.m_PublicTransports[entity] = componentData2;
						return;
					}
					int3 value = freeSpaceMap[vehicle];
					value.x -= num;
					freeSpaceMap[vehicle] = value;
				}
				else
				{
					num = this.GetPendingGroupMemberCount(leader);
					vehicle = this.TryFindVehicle(ref freeSpaceMap, vehicle, leaderVehicle, position, isLeader: false, num, out var _);
					if (vehicle == Entity.Null)
					{
						return;
					}
					if (vehicle != leaderVehicle)
					{
						int3 value2 = freeSpaceMap[leaderVehicle];
						value2.x++;
						freeSpaceMap[leaderVehicle] = value2;
						value2 = freeSpaceMap[vehicle];
						value2.x--;
						freeSpaceMap[vehicle] = value2;
					}
				}
				this.m_Passengers[vehicle].Add(new Passenger(passenger));
				ref Game.Creatures.Resident valueRW = ref this.m_Residents.GetRefRW(passenger).ValueRW;
				if ((flags & CreatureVehicleFlags.Leader) != 0 && this.m_WaitingPassengers.HasComponent(waypoint))
				{
					ref WaitingPassengers valueRW2 = ref this.m_WaitingPassengers.GetRefRW(waypoint).ValueRW;
					int num2 = (int)((float)(valueRW.m_Timer * num) * (2f / 15f));
					valueRW2.m_ConcludedAccumulation += num2;
					valueRW2.m_SuccessAccumulation = (ushort)math.min(65535, valueRW2.m_SuccessAccumulation + num);
				}
				valueRW.m_Flags &= ~(ResidentFlags.WaitingTransport | ResidentFlags.NoLateDeparture);
				valueRW.m_Flags |= ResidentFlags.InVehicle;
				valueRW.m_Timer = 0;
				this.m_Queues[passenger].Clear();
				this.m_CommandBuffer.AddComponent(passenger, new CurrentVehicle(vehicle, flags));
				if (this.m_Unspawneds.HasComponent(vehicle) && !this.m_Unspawneds.HasComponent(passenger))
				{
					this.m_CommandBuffer.AddComponent(passenger, default(Unspawned));
					this.m_CommandBuffer.AddComponent(passenger, default(BatchesUpdated));
				}
			}

			private void CancelEnterVehicle(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity passenger, Entity vehicle)
			{
				if (this.m_Passengers.HasBuffer(vehicle) && CollectionUtils.RemoveValue(this.m_Passengers[vehicle], new Passenger(passenger)))
				{
					int3 freeSpace = this.GetFreeSpace(ref freeSpaceMap, vehicle);
					freeSpace.x++;
					freeSpaceMap[vehicle] = freeSpace;
				}
				this.m_CommandBuffer.RemoveComponent<CurrentVehicle>(passenger);
				ref Game.Creatures.Resident valueRW = ref this.m_Residents.GetRefRW(passenger).ValueRW;
				valueRW.m_Flags &= ~ResidentFlags.InVehicle;
				valueRW.m_Timer = 0;
			}

			private void FinishEnterVehicle(Entity passenger, Entity household, Entity vehicle, Entity controllerVehicle, HumanCurrentLane oldCurrentLane, int ticketPrice)
			{
				TransportType transportType = TransportType.None;
				PrefabRef prefabRef = this.m_PrefabRefData[vehicle];
				if (this.m_TaxiData.HasComponent(prefabRef.m_Prefab))
				{
					transportType = TransportType.Taxi;
				}
				else if (this.m_PublicTransportVehicleData.HasComponent(prefabRef.m_Prefab))
				{
					transportType = this.m_PublicTransportVehicleData[prefabRef.m_Prefab].m_TransportType;
					if (this.m_PublicTransports.TryGetComponent(controllerVehicle, out var componentData))
					{
						if ((componentData.m_State & (PublicTransportFlags.Evacuating | PublicTransportFlags.PrisonerTransport)) != 0)
						{
							transportType = TransportType.None;
						}
						if ((transportType == TransportType.Airplane || transportType == TransportType.Train || transportType == TransportType.Ship) && this.m_Targets.TryGetComponent(controllerVehicle, out var componentData2) && this.m_Connecteds.TryGetComponent(componentData2.m_Target, out var componentData3) && !this.m_OutsideConnections.HasComponent(componentData3.m_Connected))
						{
							Entity topOwner = BuildingUtils.GetTopOwner(componentData3.m_Connected, ref this.m_Owners);
							this.m_TransportUsageQueue.Enqueue(new TransportUsageEvent
							{
								m_Building = topOwner,
								m_TransportedPassenger = 1,
								m_TransportType = transportType
							});
						}
					}
				}
				Creature value = this.m_Creatures[passenger];
				value.m_QueueEntity = Entity.Null;
				value.m_QueueArea = default(Sphere3);
				this.m_Creatures[passenger] = value;
				this.m_Queues[passenger].Clear();
				Game.Creatures.Resident resident = this.m_Residents[passenger];
				if (this.m_Citizens.TryGetComponent(resident.m_Citizen, out var componentData4))
				{
					PassengerType parameter = (((componentData4.m_State & CitizenFlags.Tourist) != CitizenFlags.None) ? PassengerType.Tourist : PassengerType.Citizen);
					switch (transportType)
					{
					case TransportType.Bus:
						this.EnqueueStat(StatisticType.PassengerCountBus, 1, (int)parameter);
						break;
					case TransportType.Subway:
						this.EnqueueStat(StatisticType.PassengerCountSubway, 1, (int)parameter);
						break;
					case TransportType.Tram:
						this.EnqueueStat(StatisticType.PassengerCountTram, 1, (int)parameter);
						break;
					case TransportType.Train:
						this.EnqueueStat(StatisticType.PassengerCountTrain, 1, (int)parameter);
						break;
					case TransportType.Ship:
						this.EnqueueStat(StatisticType.PassengerCountShip, 1, (int)parameter);
						break;
					case TransportType.Ferry:
						this.EnqueueStat(StatisticType.PassengerCountFerry, 1, (int)parameter);
						break;
					case TransportType.Airplane:
						this.EnqueueStat(StatisticType.PassengerCountAirplane, 1, (int)parameter);
						break;
					case TransportType.Taxi:
						this.EnqueueStat(StatisticType.PassengerCountTaxi, 1, (int)parameter);
						break;
					}
					if (this.m_PrefabPersonalCarData.HasComponent(prefabRef.m_Prefab))
					{
						if (this.m_Bicycles.HasComponent(vehicle))
						{
							componentData4.m_State |= CitizenFlags.BicycleUser;
						}
						else
						{
							componentData4.m_State &= ~CitizenFlags.BicycleUser;
						}
						this.m_Citizens[resident.m_Citizen] = componentData4;
					}
				}
				if (this.m_LaneObjects.HasBuffer(oldCurrentLane.m_Lane))
				{
					NetUtils.RemoveLaneObject(this.m_LaneObjects[oldCurrentLane.m_Lane], passenger);
				}
				else
				{
					this.m_SearchTree.TryRemove(passenger);
				}
				if (this.TryGetRelativeLocation(prefabRef.m_Prefab, out var relative))
				{
					this.m_CommandBuffer.RemoveComponent(passenger, in this.m_CurrentLaneTypesRelative);
					this.m_CommandBuffer.AddComponent(passenger, relative);
				}
				else
				{
					this.m_CommandBuffer.RemoveComponent(passenger, in this.m_CurrentLaneTypes);
					this.m_CommandBuffer.AddComponent(passenger, default(Unspawned));
				}
				this.m_CommandBuffer.AddComponent(passenger, default(Updated));
				if (ticketPrice != 0 && this.m_Resources.HasBuffer(household) && this.m_PlayerMoney.HasComponent(this.m_City))
				{
					DynamicBuffer<Game.Economy.Resources> resources = this.m_Resources[household];
					EconomyUtils.AddResources(Resource.Money, -ticketPrice, resources);
					PlayerMoney value2 = this.m_PlayerMoney[this.m_City];
					value2.Add(ticketPrice);
					this.m_PlayerMoney[this.m_City] = value2;
					this.m_FeeQueue.Enqueue(new ServiceFeeSystem.FeeEvent
					{
						m_Amount = 1f,
						m_Cost = ticketPrice,
						m_Resource = PlayerResource.PublicTransport,
						m_Outside = false
					});
				}
			}

			private bool TryGetRelativeLocation(Entity prefab, out Relative relative)
			{
				relative = default(Relative);
				if (this.m_ActivityLocations.TryGetBuffer(prefab, out var bufferData))
				{
					ActivityMask activityMask = new ActivityMask(ActivityType.Driving);
					activityMask.m_Mask |= new ActivityMask(ActivityType.Biking).m_Mask;
					for (int i = 0; i < bufferData.Length; i++)
					{
						ActivityLocationElement activityLocationElement = bufferData[i];
						if ((activityLocationElement.m_ActivityMask.m_Mask & activityMask.m_Mask) != 0)
						{
							relative.m_Position = activityLocationElement.m_Position;
							relative.m_Rotation = activityLocationElement.m_Rotation;
							relative.m_BoneIndex = new int3(0, -1, -1);
							return true;
						}
					}
				}
				return false;
			}

			private void RequireStop(ref NativeParallelHashMap<Entity, int3> freeSpaceMap, Entity passenger, Entity vehicle, float3 position)
			{
				if (passenger != Entity.Null)
				{
					int requiredSpace = 1 + this.GetPendingGroupMemberCount(passenger);
					if (this.TryFindVehicle(ref freeSpaceMap, vehicle, Entity.Null, position, isLeader: true, requiredSpace, out var _) == Entity.Null)
					{
						return;
					}
				}
				if (this.m_PublicTransports.TryGetComponent(vehicle, out var componentData))
				{
					componentData.m_State |= PublicTransportFlags.RequireStop;
					this.m_PublicTransports[vehicle] = componentData;
				}
			}

			private void WaitTimeExceeded(Entity passenger, Entity waypoint)
			{
				if (this.m_WaitingPassengers.HasComponent(waypoint))
				{
					int num = 1 + this.GetPendingGroupMemberCount(passenger);
					ref WaitingPassengers valueRW = ref this.m_WaitingPassengers.GetRefRW(waypoint).ValueRW;
					int num2 = (int)((float)(5000 * num) * (2f / 15f));
					valueRW.m_ConcludedAccumulation += num2;
				}
			}

			private void WaitTimeEstimate(Entity waypoint, int seconds)
			{
				if (this.m_WaitingPassengers.HasComponent(waypoint))
				{
					this.m_WaitingPassengers.GetRefRW(waypoint).ValueRW.m_ConcludedAccumulation += seconds;
				}
			}

			private void EnqueueStat(StatisticType statisticType, int change, int parameter)
			{
				this.m_StatisticsEventQueue.Enqueue(new StatisticsEvent
				{
					m_Statistic = statisticType,
					m_Change = change,
					m_Parameter = parameter
				});
			}
		}

		[BurstCompile]
		private struct ResidentActionJob : IJob
		{
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			[ReadOnly]
			public ComponentLookup<MailBoxData> m_PrefabMailBoxData;

			public ComponentLookup<MailSender> m_MailSenderData;

			public ComponentLookup<HouseholdNeed> m_HouseholdNeedData;

			public ComponentLookup<Game.Routes.MailBox> m_MailBoxData;

			public NativeQueue<ResidentAction> m_ActionQueue;

			public EntityCommandBuffer m_CommandBuffer;

			public void Execute()
			{
				int count = this.m_ActionQueue.Count;
				for (int i = 0; i < count; i++)
				{
					ResidentAction action = this.m_ActionQueue.Dequeue();
					switch (action.m_Type)
					{
					case ResidentActionType.SendMail:
						this.SendMail(action);
						break;
					case ResidentActionType.GoShopping:
						this.GoShopping(action);
						break;
					}
				}
			}

			private void SendMail(ResidentAction action)
			{
				if (!this.m_MailSenderData.TryGetEnabledComponent(action.m_Citizen, out var component) || !this.m_MailBoxData.TryGetComponent(action.m_Target, out var componentData) || !this.m_PrefabRefData.TryGetComponent(action.m_Target, out var componentData2) || !this.m_PrefabMailBoxData.TryGetComponent(componentData2.m_Prefab, out var componentData3))
				{
					return;
				}
				int num = math.min(component.m_Amount, componentData3.m_MailCapacity - componentData.m_MailAmount);
				if (num > 0)
				{
					component.m_Amount = (ushort)(component.m_Amount - num);
					componentData.m_MailAmount += num;
					this.m_MailSenderData[action.m_Citizen] = component;
					this.m_MailBoxData[action.m_Target] = componentData;
					if (component.m_Amount == 0)
					{
						this.m_CommandBuffer.SetComponentEnabled<MailSender>(action.m_Citizen, value: false);
					}
				}
			}

			private void GoShopping(ResidentAction action)
			{
				if (this.m_HouseholdNeedData.HasComponent(action.m_Household))
				{
					HouseholdNeed value = this.m_HouseholdNeedData[action.m_Household];
					value.m_Resource = Resource.NoResource;
					this.m_HouseholdNeedData[action.m_Household] = value;
				}
				this.m_CommandBuffer.AddComponent(action.m_Citizen, new ResourceBought
				{
					m_Seller = action.m_Target,
					m_Payer = action.m_Household,
					m_Resource = action.m_Resource,
					m_Amount = action.m_Amount,
					m_Distance = action.m_Distance
				});
			}
		}


		private EndFrameBarrier m_EndFrameBarrier;

		private SimulationSystem m_SimulationSystem;

		private PathfindSetupSystem m_PathfindSetupSystem;

		private TimeSystem m_TimeSystem;

		private CityConfigurationSystem m_CityConfigurationSystem;

		private Actions m_Actions;

		private PersonalCarSelectData m_PersonalCarSelectData;

		private EntityQuery m_CreatureQuery;

		private EntityQuery m_GroupCreatureQuery;

		private EntityQuery m_CarPrefabQuery;

		private EntityArchetype m_ResetTripArchetype;

		private ComponentTypeSet m_ParkedToMovingCarRemoveTypes;

		private ComponentTypeSet m_ParkedToMovingCarAddTypes;

		private ComponentTypeSet m_ParkedToMovingTrailerAddTypes;

		[DebugWatchValue]
		[EnumArray(typeof(DeletedResidentType))]
		private NativeArray<int> m_DeletedResidents;


		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_PathfindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_CityConfigurationSystem = base.World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			this.m_Actions = base.World.GetOrCreateSystemManaged<Actions>();
			this.m_PersonalCarSelectData = new PersonalCarSelectData(this);
			this.m_CreatureQuery = base.GetEntityQuery(ComponentType.ReadWrite<Game.Creatures.Resident>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.Exclude<GroupMember>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<Stumbling>());
			this.m_GroupCreatureQuery = base.GetEntityQuery(ComponentType.ReadWrite<Game.Creatures.Resident>(), ComponentType.ReadOnly<GroupMember>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<Stumbling>());
			this.m_CarPrefabQuery = base.GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());
			this.m_ResetTripArchetype = base.EntityManager.CreateArchetype(ComponentType.ReadWrite<Game.Common.Event>(), ComponentType.ReadWrite<ResetTrip>());
			this.m_ParkedToMovingCarRemoveTypes = new ComponentTypeSet(ComponentType.ReadWrite<ParkedCar>(), ComponentType.ReadWrite<Stopped>());
			this.m_ParkedToMovingCarAddTypes = new ComponentTypeSet(new ComponentType[12]
			{
				ComponentType.ReadWrite<Moving>(),
				ComponentType.ReadWrite<TransformFrame>(),
				ComponentType.ReadWrite<InterpolatedTransform>(),
				ComponentType.ReadWrite<CarNavigation>(),
				ComponentType.ReadWrite<CarNavigationLane>(),
				ComponentType.ReadWrite<CarCurrentLane>(),
				ComponentType.ReadWrite<PathOwner>(),
				ComponentType.ReadWrite<Target>(),
				ComponentType.ReadWrite<Blocker>(),
				ComponentType.ReadWrite<PathElement>(),
				ComponentType.ReadWrite<Swaying>(),
				ComponentType.ReadWrite<Updated>()
			});
			this.m_ParkedToMovingTrailerAddTypes = new ComponentTypeSet(new ComponentType[6]
			{
				ComponentType.ReadWrite<Moving>(),
				ComponentType.ReadWrite<TransformFrame>(),
				ComponentType.ReadWrite<InterpolatedTransform>(),
				ComponentType.ReadWrite<CarTrailerLane>(),
				ComponentType.ReadWrite<Swaying>(),
				ComponentType.ReadWrite<Updated>()
			});
			this.m_DeletedResidents = new NativeArray<int>(7, Allocator.Persistent);
		}

		protected override void OnDestroy()
		{
			this.m_DeletedResidents.Dispose();
			base.OnDestroy();
		}

		protected override void OnUpdate()
		{
			uint index = this.m_SimulationSystem.frameIndex % 16;
			this.m_CreatureQuery.SetSharedComponentFilter(new UpdateFrame(index));
			this.m_GroupCreatureQuery.SetSharedComponentFilter(new UpdateFrame(index));
			this.m_Actions.m_BoardingQueue = new NativeQueue<Boarding>(Allocator.TempJob);
			this.m_Actions.m_ActionQueue = new NativeQueue<ResidentAction>(Allocator.TempJob);
			this.m_PersonalCarSelectData.PreUpdate(this, this.m_CityConfigurationSystem, this.m_CarPrefabQuery, Allocator.TempJob, out var jobHandle);
			ResidentTickJob jobData = new ResidentTickJob
			{
				m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef),
				m_CurrentVehicleType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_CurrentVehicle_RO_ComponentTypeHandle, ref base.CheckedStateRef),
				m_GroupMemberType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_GroupMember_RO_ComponentTypeHandle, ref base.CheckedStateRef),
				m_UnspawnedType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Objects_Unspawned_RO_ComponentTypeHandle, ref base.CheckedStateRef),
				m_HumanNavigationType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_HumanNavigation_RO_ComponentTypeHandle, ref base.CheckedStateRef),
				m_PrefabRefType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle, ref base.CheckedStateRef),
				m_GroupCreatureType = InternalCompilerInterface.GetBufferTypeHandle(ref this.__TypeHandle.__Game_Creatures_GroupCreature_RO_BufferTypeHandle, ref base.CheckedStateRef),
				m_CreatureType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_Creature_RW_ComponentTypeHandle, ref base.CheckedStateRef),
				m_HumanType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_Human_RW_ComponentTypeHandle, ref base.CheckedStateRef),
				m_ResidentType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_Resident_RW_ComponentTypeHandle, ref base.CheckedStateRef),
				m_CurrentLaneType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_HumanCurrentLane_RW_ComponentTypeHandle, ref base.CheckedStateRef),
				m_TargetType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Common_Target_RW_ComponentTypeHandle, ref base.CheckedStateRef),
				m_DivertType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Creatures_Divert_RW_ComponentTypeHandle, ref base.CheckedStateRef),
				m_EntityLookup = InternalCompilerInterface.GetEntityStorageInfoLookup(ref this.__TypeHandle.__EntityStorageInfoLookup, ref base.CheckedStateRef),
				m_HumanData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_Human_RW_ComponentLookup, ref base.CheckedStateRef),
				m_TransformData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef),
				m_OwnerData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Owner_RO_ComponentLookup, ref base.CheckedStateRef),
				m_TargetData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Target_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PseudoRandomSeedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_PseudoRandomSeed_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CurrentVehicleData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_CurrentVehicle_RO_ComponentLookup, ref base.CheckedStateRef),
				m_DestroyedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Destroyed_RO_ComponentLookup, ref base.CheckedStateRef),
				m_DeletedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Deleted_RO_ComponentLookup, ref base.CheckedStateRef),
				m_UnspawnedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Unspawned_RO_ComponentLookup, ref base.CheckedStateRef),
				m_RideNeederData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_RideNeeder_RO_ComponentLookup, ref base.CheckedStateRef),
				m_MovingData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Moving_RO_ComponentLookup, ref base.CheckedStateRef),
				m_SpawnLocation = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_SpawnLocation_RO_ComponentLookup, ref base.CheckedStateRef),
				m_AnimalData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Creatures_Animal_RO_ComponentLookup, ref base.CheckedStateRef),
				m_Dispatched = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Simulation_Dispatched_RO_ComponentLookup, ref base.CheckedStateRef),
				m_ServiceRequestData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Simulation_ServiceRequest_RO_ComponentLookup, ref base.CheckedStateRef),
				m_OnFireData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Events_OnFire_RO_ComponentLookup, ref base.CheckedStateRef),
				m_EdgeData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_Edge_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CurveData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_Curve_RO_ComponentLookup, ref base.CheckedStateRef),
				m_LaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_Lane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_EdgeLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_EdgeLane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_ParkingLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_ParkingLane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_GarageLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_GarageLane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PedestrianLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_PedestrianLane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_ConnectionLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_ConnectionLane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_LaneSignalData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_LaneSignal_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HangaroundLocationData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Areas_HangaroundLocation_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CitizenData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HouseholdData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CurrentBuildingData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CurrentTransportData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CurrentTransport_RO_ComponentLookup, ref base.CheckedStateRef),
				m_WorkerData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CarKeeperData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentLookup, ref base.CheckedStateRef),
				m_BicycleOwnerData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_BicycleOwner_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HealthProblemData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentLookup, ref base.CheckedStateRef),
				m_TravelPurposeData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_TravelPurpose_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HomelessHouseholdData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef),
				m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HouseholdNeedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdNeed_RO_ComponentLookup, ref base.CheckedStateRef),
				m_AttendingMeetingData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CoordinatedMeetingData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeeting_RO_ComponentLookup, ref base.CheckedStateRef),
				m_MovingAwayData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef),
				m_ServiceAvailableData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentLookup, ref base.CheckedStateRef),
				m_ParkedCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_ParkedCar_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PersonalCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RO_ComponentLookup, ref base.CheckedStateRef),
				m_TaxiData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Taxi_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PublicTransportData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_PublicTransport_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PoliceCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_PoliceCar_RO_ComponentLookup, ref base.CheckedStateRef),
				m_AmbulanceData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Ambulance_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HearseData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Hearse_RO_ComponentLookup, ref base.CheckedStateRef),
				m_ControllerData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Controller_RO_ComponentLookup, ref base.CheckedStateRef),
				m_VehicleData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Vehicle_RO_ComponentLookup, ref base.CheckedStateRef),
				m_TrainData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Train_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PropertyRenters = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef),
				m_AttractivenessProviderData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Buildings_AttractivenessProvider_RO_ComponentLookup, ref base.CheckedStateRef),
				m_RouteConnectedData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_Connected_RO_ComponentLookup, ref base.CheckedStateRef),
				m_BoardingVehicleData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_BoardingVehicle_RO_ComponentLookup, ref base.CheckedStateRef),
				m_CurrentRouteData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_CurrentRoute_RO_ComponentLookup, ref base.CheckedStateRef),
				m_TransportLineData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_TransportLine_RO_ComponentLookup, ref base.CheckedStateRef),
				m_AccessLaneLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Routes_AccessLane_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabRefData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabCreatureData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_CreatureData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabHumanData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_HumanData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabObjectGeometryData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_CarData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabIndustrialProcessData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabTransportStopData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_TransportStopData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_PrefabSpawnLocationData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_SpawnLocationData_RO_ComponentLookup, ref base.CheckedStateRef),
				m_HouseholdAnimals = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdAnimal_RO_BufferLookup, ref base.CheckedStateRef),
				m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef),
				m_ConnectedRoutes = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Routes_ConnectedRoute_RO_BufferLookup, ref base.CheckedStateRef),
				m_RouteWaypoints = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Routes_RouteWaypoint_RO_BufferLookup, ref base.CheckedStateRef),
				m_VehicleLayouts = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Vehicles_LayoutElement_RO_BufferLookup, ref base.CheckedStateRef),
				m_CarNavigationLanes = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Vehicles_CarNavigationLane_RO_BufferLookup, ref base.CheckedStateRef),
				m_ConnectedEdges = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Net_ConnectedEdge_RO_BufferLookup, ref base.CheckedStateRef),
				m_SubLanes = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Net_SubLane_RO_BufferLookup, ref base.CheckedStateRef),
				m_AreaNodes = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Areas_Node_RO_BufferLookup, ref base.CheckedStateRef),
				m_AreaTriangles = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Areas_Triangle_RO_BufferLookup, ref base.CheckedStateRef),
				m_ConnectedBuildings = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Buildings_ConnectedBuilding_RO_BufferLookup, ref base.CheckedStateRef),
				m_Renters = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup, ref base.CheckedStateRef),
				m_SpawnLocationElements = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Buildings_SpawnLocationElement_RO_BufferLookup, ref base.CheckedStateRef),
				m_Resources = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup, ref base.CheckedStateRef),
				m_ServiceDispatches = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Simulation_ServiceDispatch_RO_BufferLookup, ref base.CheckedStateRef),
				m_PrefabActivityLocationElements = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Prefabs_ActivityLocationElement_RO_BufferLookup, ref base.CheckedStateRef),
				m_PathOwnerData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Pathfind_PathOwner_RW_ComponentLookup, ref base.CheckedStateRef),
				m_PathElements = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Pathfind_PathElement_RW_BufferLookup, ref base.CheckedStateRef),
				m_RandomSeed = RandomSeed.Next(),
				m_SimulationFrameIndex = this.m_SimulationSystem.frameIndex,
				m_LefthandTraffic = this.m_CityConfigurationSystem.leftHandTraffic,
				m_GroupMember = false,
				m_PersonalCarSelectData = this.m_PersonalCarSelectData,
				m_ResetTripArchetype = this.m_ResetTripArchetype,
				m_ParkedToMovingCarRemoveTypes = this.m_ParkedToMovingCarRemoveTypes,
				m_ParkedToMovingCarAddTypes = this.m_ParkedToMovingCarAddTypes,
				m_ParkedToMovingTrailerAddTypes = this.m_ParkedToMovingTrailerAddTypes,
				m_DeletedResidents = this.m_DeletedResidents,
				m_TimeOfDay = this.m_TimeSystem.normalizedTime,
				m_PathfindQueue = this.m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
				m_BoardingQueue = this.m_Actions.m_BoardingQueue.AsParallelWriter(),
				m_ActionQueue = this.m_Actions.m_ActionQueue.AsParallelWriter(),
				m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter()
			};
			JobHandle dependsOn = JobChunkExtensions.ScheduleParallel(jobData, this.m_CreatureQuery, JobHandle.CombineDependencies(base.Dependency, jobHandle));
			jobData.m_GroupMember = true;
			JobHandle jobHandle2 = JobChunkExtensions.ScheduleParallel(jobData, this.m_GroupCreatureQuery, dependsOn);
			this.m_PersonalCarSelectData.PostUpdate(jobHandle2);
			this.m_PathfindSetupSystem.AddQueueWriter(jobHandle2);
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
			this.m_Actions.m_Dependency = jobHandle2;
			base.Dependency = jobHandle2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			new EntityQueryBuilder(Allocator.Temp).Dispose();
		}


	}
}
