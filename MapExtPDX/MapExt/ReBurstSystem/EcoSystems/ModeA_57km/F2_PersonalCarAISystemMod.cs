using Game;
using Game.Simulation;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Creatures;
using Game.Economy;
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

namespace MapExtPDX.ModeA
{
	public partial class PersonalCarAISystemMod : GameSystemBase
	{
		public partial class ActionsMod : GameSystemBase
		{

			public JobHandle m_Dependency;

			public NativeQueue<MoneyTransfer> m_MoneyTransferQueue;

			protected override void OnUpdate()
			{
				JobHandle dependsOn = JobHandle.CombineDependencies(Dependency, m_Dependency);
				JobHandle jobHandle = new TransferMoneyJob
				{
					m_Resources = SystemAPI.GetBufferLookup<Resources>(isReadOnly: false),
					m_MoneyTransferQueue = m_MoneyTransferQueue
				}.Schedule(dependsOn);
				m_MoneyTransferQueue.Dispose(jobHandle);
				Dependency = jobHandle;
			}

			}

		public struct MoneyTransfer
		{
			public Entity m_Payer;

			public Entity m_Recipient;

			public int m_Amount;
		}

		[BurstCompile]
		private struct PersonalCarTickJob : IJobChunk
		{
			[ReadOnly]
			public EntityTypeHandle m_EntityType;
			[ReadOnly]
			public ComponentTypeHandle<Unspawned> m_UnspawnedType;
			[ReadOnly]
			public ComponentTypeHandle<Bicycle> m_BicycleType;
			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
			[ReadOnly]
			public BufferTypeHandle<LayoutElement> m_LayoutElementType;
			public ComponentTypeHandle<Game.Vehicles.PersonalCar> m_PersonalCarType;
			public ComponentTypeHandle<Car> m_CarType;
			public ComponentTypeHandle<CarCurrentLane> m_CurrentLaneType;
			public BufferTypeHandle<CarNavigationLane> m_CarNavigationLaneType;
			[ReadOnly]
			public EntityStorageInfoLookup m_EntityLookup;
			[ReadOnly]
			public ComponentLookup<ParkedCar> m_ParkedCarData;
			[ReadOnly]
			public ComponentLookup<Owner> m_OwnerData;
			[ReadOnly]
			public ComponentLookup<Game.Objects.SpawnLocation> m_SpawnLocationData;
			[ReadOnly]
			public ComponentLookup<Unspawned> m_UnspawnedData;
			[ReadOnly]
			public ComponentLookup<CarData> m_PrefabCarData;
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;
			[ReadOnly]
			public ComponentLookup<ParkingLaneData> m_PrefabParkingLaneData;
			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;
			[ReadOnly]
			public ComponentLookup<CreatureData> m_PrefabCreatureData;
			[ReadOnly]
			public ComponentLookup<HumanData> m_PrefabHumanData;
			[ReadOnly]
			public ComponentLookup<SpawnLocationData> m_PrefabSpawnLocationData;
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenterData;
			[ReadOnly]
			public ComponentLookup<Game.Net.CarLane> m_CarLaneData;
			[ReadOnly]
			public ComponentLookup<Game.Net.PedestrianLane> m_PedestrianLaneData;
			[ReadOnly]
			public ComponentLookup<Game.Net.ParkingLane> m_ParkingLaneData;
			[ReadOnly]
			public ComponentLookup<GarageLane> m_GarageLaneData;
			[ReadOnly]
			public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;
			[ReadOnly]
			public ComponentLookup<Curve> m_CurveData;
			[ReadOnly]
			public ComponentLookup<SlaveLane> m_SlaveLaneData;
			[ReadOnly]
			public ComponentLookup<Game.Creatures.Resident> m_ResidentData;
			[ReadOnly]
			public ComponentLookup<Divert> m_DivertData;
			[ReadOnly]
			public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;
			[ReadOnly]
			public ComponentLookup<Citizen> m_CitizenData;
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMemberData;
			[ReadOnly]
			public ComponentLookup<Household> m_HouseholdData;
			[ReadOnly]
			public ComponentLookup<Worker> m_WorkerData;
			[ReadOnly]
			public ComponentLookup<TravelPurpose> m_TravelPurposeData;
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAwayData;
			[ReadOnly]
			public BufferLookup<Passenger> m_Passengers;
			[ReadOnly]
			public BufferLookup<Game.Net.SubLane> m_SubLanes;
			[ReadOnly]
			public BufferLookup<LaneObject> m_LaneObjects;
			[ReadOnly]
			public BufferLookup<LaneOverlap> m_LaneOverlaps;
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;
			[NativeDisableParallelForRestriction]
			public ComponentLookup<Target> m_TargetData;
			[NativeDisableParallelForRestriction]
			public ComponentLookup<PathOwner> m_PathOwnerData;
			[NativeDisableParallelForRestriction]
			public BufferLookup<PathElement> m_PathElements;
			[ReadOnly]
			public RandomSeed m_RandomSeed;
			[ReadOnly]
			public Entity m_City;
			[ReadOnly]
			public float m_TimeOfDay;
			[ReadOnly]
			public ComponentTypeSet m_MovingToParkedCarRemoveTypes;
			[ReadOnly]
			public ComponentTypeSet m_MovingToParkedCarAddTypes;
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
			public NativeQueue<MoneyTransfer>.ParallelWriter m_MoneyTransferQueue;
			public NativeQueue<ServiceFeeSystem.FeeEvent>.ParallelWriter m_FeeQueue;
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
				NativeArray<PrefabRef> prefabRefs = chunk.GetNativeArray(ref m_PrefabRefType);
				NativeArray<CarCurrentLane> currentLanes = chunk.GetNativeArray(ref m_CurrentLaneType);
				NativeArray<Game.Vehicles.PersonalCar> personalCars = chunk.GetNativeArray(ref m_PersonalCarType);
				NativeArray<Car> cars = chunk.GetNativeArray(ref m_CarType);
				BufferAccessor<LayoutElement> layoutBuffers = chunk.GetBufferAccessor(ref m_LayoutElementType);
				BufferAccessor<CarNavigationLane> navigationLaneBuffers = chunk.GetBufferAccessor(ref m_CarNavigationLaneType);
				bool isUnspawned = chunk.Has(ref m_UnspawnedType);
				bool isBicycle = chunk.Has(ref m_BicycleType);
				for (int index = 0; index < entities.Length; index++)
				{
					Entity entity = entities[index];
					PrefabRef prefabRef = prefabRefs[index];
					Game.Vehicles.PersonalCar personalCar = personalCars[index];
					Car car = cars[index];
					CarCurrentLane currentLane = currentLanes[index];
					DynamicBuffer<CarNavigationLane> navigationLanes = navigationLaneBuffers[index];
					Target target = m_TargetData[entity];
					PathOwner pathOwner = m_PathOwnerData[entity];
					DynamicBuffer<LayoutElement> layout = default(DynamicBuffer<LayoutElement>);
					if (layoutBuffers.Length != 0)
					{
						layout = layoutBuffers[index];
					}
					VehicleUtils.CheckUnspawned(unfilteredChunkIndex, entity, currentLane, isUnspawned, m_CommandBuffer);
					Tick(unfilteredChunkIndex, entity, prefabRef, layout, navigationLanes, isBicycle, ref personalCar, ref car, ref currentLane, ref pathOwner, ref target);
					m_TargetData[entity] = target;
					m_PathOwnerData[entity] = pathOwner;
					personalCars[index] = personalCar;
					cars[index] = car;
					currentLanes[index] = currentLane;
				}
			}

			private void Tick(int jobIndex, Entity entity, PrefabRef prefabRef, DynamicBuffer<LayoutElement> layout, DynamicBuffer<CarNavigationLane> navigationLanes, bool isBicycle, ref Game.Vehicles.PersonalCar personalCar, ref Car car, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				Random random = m_RandomSeed.GetRandom(entity.Index);
				if (VehicleUtils.ResetUpdatedPath(ref pathOwner))
				{
					ResetPath(entity, isBicycle, ref random, ref personalCar, ref car, ref currentLane, ref pathOwner);
				}
				if (((personalCar.m_State & (PersonalCarFlags.Transporting | PersonalCarFlags.Boarding | PersonalCarFlags.Disembarking)) == 0 && !m_EntityLookup.Exists(target.m_Target)) || VehicleUtils.PathfindFailed(pathOwner))
				{
					RemovePath(entity, ref pathOwner);
					if ((personalCar.m_State & PersonalCarFlags.Disembarking) != 0)
					{
						if (StopDisembarking(entity, layout, ref personalCar, ref pathOwner))
						{
							ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: true, ref personalCar, ref currentLane);
						}
						return;
					}
					if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0)
					{
						if (!StartDisembarking(jobIndex, entity, layout, ref personalCar, ref currentLane, ref pathOwner))
						{
							ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: true, ref personalCar, ref currentLane);
						}
						return;
					}
					if ((personalCar.m_State & PersonalCarFlags.Boarding) == 0)
					{
						ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: false, ref personalCar, ref currentLane);
						return;
					}
					if (!StopBoarding(entity, layout, navigationLanes, ref personalCar, ref currentLane, ref pathOwner, ref target))
					{
						return;
					}
					if ((personalCar.m_State & PersonalCarFlags.Transporting) == 0)
					{
						ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: false, ref personalCar, ref currentLane);
						return;
					}
				}
				else if (VehicleUtils.ParkingSpaceReached(currentLane, pathOwner) || (personalCar.m_State & (PersonalCarFlags.Boarding | PersonalCarFlags.Disembarking)) != 0)
				{
					if ((personalCar.m_State & PersonalCarFlags.Disembarking) != 0)
					{
						if (StopDisembarking(entity, layout, ref personalCar, ref pathOwner))
						{
							ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: false, ref personalCar, ref currentLane);
						}
						return;
					}
					if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0)
					{
						if (!StartDisembarking(jobIndex, entity, layout, ref personalCar, ref currentLane, ref pathOwner))
						{
							ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: false, ref personalCar, ref currentLane);
						}
						return;
					}
					if ((personalCar.m_State & PersonalCarFlags.Boarding) == 0)
					{
						if (!StartBoarding(entity, ref personalCar, ref car, ref target))
						{
							VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, entity, layout);
						}
						return;
					}
					if (!StopBoarding(entity, layout, navigationLanes, ref personalCar, ref currentLane, ref pathOwner, ref target))
					{
						return;
					}
					if ((personalCar.m_State & PersonalCarFlags.Transporting) == 0)
					{
						ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: false, ref personalCar, ref currentLane);
						return;
					}
				}
				else
				{
					if (VehicleUtils.PathEndReached(currentLane))
					{
						if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0)
						{
							if (!StartDisembarking(jobIndex, entity, layout, ref personalCar, ref currentLane, ref pathOwner))
							{
								ParkCar(jobIndex, entity, layout, isBicycle, resetLocation: false, ref personalCar, ref currentLane);
							}
						}
						else
						{
							VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, entity, layout);
						}
						return;
					}
					if (VehicleUtils.WaypointReached(currentLane))
					{
						currentLane.m_LaneFlags &= ~Game.Vehicles.CarLaneFlags.Waypoint;
						pathOwner.m_State &= ~PathFlags.Failed;
						pathOwner.m_State |= PathFlags.Obsolete;
					}
				}
				if ((personalCar.m_State & PersonalCarFlags.Disembarking) == 0)
				{
					if (VehicleUtils.RequireNewPath(pathOwner))
					{
						FindNewPath(entity, prefabRef, layout, isBicycle, ref personalCar, ref currentLane, ref pathOwner, ref target);
					}
					else if ((pathOwner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Stuck)) == 0)
					{
						CheckParkingSpace(entity, ref random, ref currentLane, ref pathOwner, navigationLanes);
					}
				}
			}

			private void CheckParkingSpace(Entity entity, ref Random random, ref CarCurrentLane currentLane, ref PathOwner pathOwner, DynamicBuffer<CarNavigationLane> navigationLanes)
			{
				DynamicBuffer<PathElement> path = m_PathElements[entity];
				ComponentLookup<Blocker> blockerData = default(ComponentLookup<Blocker>);
				if (VehicleUtils.ValidateParkingSpace(entity, ref random, ref currentLane, ref pathOwner, navigationLanes, path, ref m_ParkedCarData, ref blockerData, ref m_CurveData, ref m_UnspawnedData, ref m_ParkingLaneData, ref m_GarageLaneData, ref m_ConnectionLaneData, ref m_PrefabRefData, ref m_PrefabParkingLaneData, ref m_PrefabObjectGeometryData, ref m_LaneObjects, ref m_LaneOverlaps, ignoreDriveways: false, ignoreDisabled: true, boardingOnly: false) != Entity.Null)
				{
					return;
				}
				int checkCount = math.min(40000, path.Length - pathOwner.m_ElementIndex);
				if (checkCount <= 0 || navigationLanes.Length <= 0)
				{
					return;
				}
				int randomIndex = random.NextInt(checkCount) * (random.NextInt(checkCount) + 1) / checkCount;
				PathElement pathElement = path[pathOwner.m_ElementIndex + randomIndex];
				if (m_ParkingLaneData.HasComponent(pathElement.m_Target))
				{
					float minT;
					if (randomIndex == 0)
					{
						CarNavigationLane carNavigationLane = navigationLanes[^1];
						minT = (((carNavigationLane.m_Flags & Game.Vehicles.CarLaneFlags.Reserved) == 0) ? carNavigationLane.m_CurvePosition.x : carNavigationLane.m_CurvePosition.y);
					}
					else
					{
						minT = path[pathOwner.m_ElementIndex + randomIndex - 1].m_TargetDelta.x;
					}

					float y = VehicleUtils.GetParkingSize(entity, ref m_PrefabRefData, ref m_PrefabObjectGeometryData, out var offset).y;
					float curvePos = pathElement.m_TargetDelta.x;
					if (VehicleUtils.FindFreeParkingSpace(ref random, pathElement.m_Target, minT, y, offset, ref curvePos, ref m_ParkedCarData, ref m_CurveData, ref m_UnspawnedData, ref m_ParkingLaneData, ref m_PrefabRefData, ref m_PrefabParkingLaneData, ref m_PrefabObjectGeometryData, ref m_LaneObjects, ref m_LaneOverlaps, ignoreDriveways: false, ignoreDisabled: true))
					{
						return;
					}
				}
				else
				{
					if (!m_GarageLaneData.HasComponent(pathElement.m_Target))
					{
						return;
					}
					GarageLane garageLane = m_GarageLaneData[pathElement.m_Target];
					Game.Net.ConnectionLane connectionLane = m_ConnectionLaneData[pathElement.m_Target];
					if (garageLane.m_VehicleCount < garageLane.m_VehicleCapacity && (connectionLane.m_Flags & ConnectionLaneFlags.Disabled) == 0)
					{
						return;
					}
				}
				for (int i = 0; i < randomIndex; i++)
				{
					if (VehicleUtils.IsParkingLane(path[pathOwner.m_ElementIndex + i].m_Target, ref m_ParkingLaneData, ref m_ConnectionLaneData))
					{
						return;
					}
				}
				pathOwner.m_State |= PathFlags.Obsolete;
			}

			private void ResetPath(Entity entity, bool isBicycle, ref Random random, ref Game.Vehicles.PersonalCar personalCar, ref Car car, ref CarCurrentLane currentLane, ref PathOwner pathOwner)
			{
				if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0 && !isBicycle)
				{
					car.m_Flags |= CarFlags.StayOnRoad;
				}
				else
				{
					car.m_Flags &= ~CarFlags.StayOnRoad;
				}
				DynamicBuffer<PathElement> path = m_PathElements[entity];
				PathUtils.ResetPath(ref currentLane, path, m_SlaveLaneData, m_OwnerData, m_SubLanes);
				VehicleUtils.ResetParkingLaneStatus(entity, isBicycle, ref currentLane, ref pathOwner, path, ref m_EntityLookup, ref m_CurveData, ref m_ParkingLaneData, ref m_CarLaneData, ref m_PedestrianLaneData, ref m_ConnectionLaneData, ref m_SpawnLocationData, ref m_PrefabRefData, ref m_PrefabSpawnLocationData);
				VehicleUtils.SetParkingCurvePos(entity, ref random, currentLane, pathOwner, path, ref m_ParkedCarData, ref m_UnspawnedData, ref m_CurveData, ref m_ParkingLaneData, ref m_ConnectionLaneData, ref m_PrefabRefData, ref m_PrefabObjectGeometryData, ref m_PrefabParkingLaneData, ref m_LaneObjects, ref m_LaneOverlaps, ignoreDriveways: false);
			}

			private void RemovePath(Entity entity, ref PathOwner pathOwner)
			{
				m_PathElements[entity].Clear();
				pathOwner.m_ElementIndex = 0;
			}

			private bool StartBoarding(Entity vehicleEntity, ref Game.Vehicles.PersonalCar personalCar, ref Car car, ref Target target)
			{
				if ((personalCar.m_State & PersonalCarFlags.DummyTraffic) == 0)
				{
					personalCar.m_State |= PersonalCarFlags.Boarding;
					return true;
				}
				return false;
			}

			private bool HasPassengers(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout)
			{
				if (layout.IsCreated && layout.Length != 0)
				{
					for (int i = 0; i < layout.Length; i++)
					{
						if (m_Passengers[layout[i].m_Vehicle].Length != 0)
						{
							return true;
						}
					}
				}
				else if (m_Passengers[vehicleEntity].Length != 0)
				{
					return true;
				}
				return false;
			}

			private Entity FindLeader(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout)
			{
				if (layout.IsCreated && layout.Length != 0)
				{
					for (int i = 0; i < layout.Length; i++)
					{
						Entity entity = FindLeader(m_Passengers[layout[i].m_Vehicle]);
						if (entity != Entity.Null)
						{
							return entity;
						}
					}
					return Entity.Null;
				}
				return FindLeader(m_Passengers[vehicleEntity]);
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

			private bool PassengersReady(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout, out Entity leader)
			{
				leader = Entity.Null;
				if (layout.IsCreated && layout.Length != 0)
				{
					for (int i = 0; i < layout.Length; i++)
					{
						if (!PassengersReady(m_Passengers[layout[i].m_Vehicle], ref leader))
						{
							return false;
						}
					}
					return true;
				}
				return PassengersReady(m_Passengers[vehicleEntity], ref leader);
			}

			private bool PassengersReady(DynamicBuffer<Passenger> passengers, ref Entity leader)
			{
				for (int i = 0; i < passengers.Length; i++)
				{
					Entity passenger = passengers[i].m_Passenger;
					if (m_CurrentVehicleData.HasComponent(passenger))
					{
						CurrentVehicle currentVehicle = m_CurrentVehicleData[passenger];
						if ((currentVehicle.m_Flags & CreatureVehicleFlags.Ready) == 0)
						{
							return false;
						}
						if ((currentVehicle.m_Flags & CreatureVehicleFlags.Leader) != 0)
						{
							leader = passenger;
						}
					}
				}
				return true;
			}

			private bool StopBoarding(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout, DynamicBuffer<CarNavigationLane> navigationLanes, ref Game.Vehicles.PersonalCar personalCar, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				if (!PassengersReady(vehicleEntity, layout, out var leader))
				{
					return false;
				}
				if (leader == Entity.Null)
				{
					personalCar.m_State &= ~PersonalCarFlags.Boarding;
					return true;
				}
				DynamicBuffer<PathElement> targetElements = m_PathElements[vehicleEntity];
				DynamicBuffer<PathElement> sourceElements = m_PathElements[leader];
				PathOwner sourceOwner = m_PathOwnerData[leader];
				Target target2 = m_TargetData[leader];
				PathUtils.CopyPath(sourceElements, sourceOwner, 0, targetElements);
				pathOwner.m_ElementIndex = 0;
				pathOwner.m_State |= PathFlags.Updated;
				personalCar.m_State &= ~PersonalCarFlags.Boarding;
				personalCar.m_State |= PersonalCarFlags.Transporting;
				bool isHomeTarget = false;
				target.m_Target = target2.m_Target;
				if (m_ResidentData.TryGetComponent(leader, out var componentData) && m_HouseholdMemberData.TryGetComponent(componentData.m_Citizen, out var componentData2) && m_PropertyRenterData.TryGetComponent(componentData2.m_Household, out var componentData3))
				{
					isHomeTarget |= componentData3.m_Property == target.m_Target;
				}
				if (m_DivertData.TryGetComponent(leader, out var componentData4))
				{
					isHomeTarget &= componentData4.m_Purpose == Purpose.None;
				}
				if (isHomeTarget)
				{
					personalCar.m_State |= PersonalCarFlags.HomeTarget;
				}
				else
				{
					personalCar.m_State &= ~PersonalCarFlags.HomeTarget;
				}
				VehicleUtils.ClearEndOfPath(ref currentLane, navigationLanes);
				return true;
			}

			private bool StartDisembarking(int jobIndex, Entity vehicleEntity, DynamicBuffer<LayoutElement> layout, ref Game.Vehicles.PersonalCar personalCar, ref CarCurrentLane currentLane, ref PathOwner pathOwner)
			{
				if (!HasPassengers(vehicleEntity, layout))
				{
					return false;
				}
				personalCar.m_State &= ~PersonalCarFlags.Transporting;
				personalCar.m_State |= PersonalCarFlags.Disembarking;
				int parkingFee = 0;
				if (m_ParkingLaneData.HasComponent(currentLane.m_Lane))
				{
					Game.Net.ParkingLane parkingLane = m_ParkingLaneData[currentLane.m_Lane];
					if (parkingLane.m_ParkingFee > 0)
					{
						parkingFee = parkingLane.m_ParkingFee;
					}
				}
				else if (VehicleUtils.PathEndReached(currentLane) && !VehicleUtils.PathfindFailed(pathOwner) && m_SpawnLocationData.HasComponent(currentLane.m_Lane))
				{
					DynamicBuffer<PathElement> dynamicBuffer = m_PathElements[vehicleEntity];
					if (dynamicBuffer.Length <= pathOwner.m_ElementIndex)
					{
						pathOwner.m_ElementIndex = 0;
						dynamicBuffer.Clear();
						dynamicBuffer.Add(new PathElement(currentLane.m_Lane, currentLane.m_CurvePosition.zz));
					}
				}
				if (parkingFee == 0 && m_GarageLaneData.HasComponent(currentLane.m_Lane))
				{
					GarageLane garageLane = m_GarageLaneData[currentLane.m_Lane];
					if (garageLane.m_ParkingFee > 0)
					{
						parkingFee = garageLane.m_ParkingFee;
					}
				}
				if (parkingFee > 0)
				{
					Entity entity = FindLeader(vehicleEntity, layout);
					if (m_ResidentData.HasComponent(entity))
					{
						Game.Creatures.Resident resident = m_ResidentData[entity];
						if (m_HouseholdMemberData.HasComponent(resident.m_Citizen))
						{
							HouseholdMember householdMember = m_HouseholdMemberData[resident.m_Citizen];
							m_MoneyTransferQueue.Enqueue(new MoneyTransfer
							{
								m_Payer = householdMember.m_Household,
								m_Recipient = m_City,
								m_Amount = parkingFee
							});
							m_FeeQueue.Enqueue(new ServiceFeeSystem.FeeEvent
							{
								m_Amount = 1f,
								m_Cost = parkingFee,
								m_Resource = PlayerResource.Parking,
								m_Outside = false
							});
						}
					}
				}
				return true;
			}

			private bool StopDisembarking(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout, ref Game.Vehicles.PersonalCar personalCar, ref PathOwner pathOwner)
			{
				if (HasPassengers(vehicleEntity, layout))
				{
					return false;
				}
				m_PathElements[vehicleEntity].Clear();
				pathOwner.m_ElementIndex = 0;
				personalCar.m_State &= ~PersonalCarFlags.Disembarking;
				return true;
			}

			private void ParkCar(int jobIndex, Entity entity, DynamicBuffer<LayoutElement> layout, bool isBicycle, bool resetLocation, ref Game.Vehicles.PersonalCar personalCar, ref CarCurrentLane currentLane)
			{
				if ((isBicycle && (resetLocation || ((!m_ParkingLaneData.HasComponent(currentLane.m_Lane) || !(currentLane.m_ChangeLane == Entity.Null)) && !m_GarageLaneData.HasComponent(currentLane.m_Lane)))) || (personalCar.m_State & PersonalCarFlags.DummyTraffic) != 0)
				{
					VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, entity, layout);
					return;
				}
				personalCar.m_State &= ~(PersonalCarFlags.Transporting | PersonalCarFlags.Boarding | PersonalCarFlags.Disembarking);
				if (layout.IsCreated)
				{
					for (int i = 0; i < layout.Length; i++)
					{
						Entity vehicle = layout[i].m_Vehicle;
						if (!(vehicle == entity))
						{
							m_CommandBuffer.AddComponent<Deleted>(jobIndex, vehicle);
						}
					}
					m_CommandBuffer.RemoveComponent<LayoutElement>(jobIndex, entity);
				}
				m_CommandBuffer.RemoveComponent(jobIndex, entity, in m_MovingToParkedCarRemoveTypes);
				m_CommandBuffer.AddComponent(jobIndex, entity, in m_MovingToParkedCarAddTypes);
				m_CommandBuffer.SetComponent(jobIndex, entity, new ParkedCar(currentLane.m_Lane, currentLane.m_CurvePosition.x));
				if (resetLocation)
				{
					Entity resetLocation2 = Entity.Null;
					if (m_HouseholdMemberData.TryGetComponent(personalCar.m_Keeper, out var componentData) && m_PropertyRenterData.TryGetComponent(componentData.m_Household, out var componentData2) && (m_HouseholdData[componentData.m_Household].m_Flags & HouseholdFlags.MovedIn) != HouseholdFlags.None && !m_MovingAwayData.HasComponent(componentData.m_Household))
					{
						resetLocation2 = componentData2.m_Property;
					}
					m_CommandBuffer.AddComponent(jobIndex, entity, new FixParkingLocation(currentLane.m_ChangeLane, resetLocation2));
				}
				else if (m_ParkingLaneData.HasComponent(currentLane.m_Lane) && currentLane.m_ChangeLane == Entity.Null)
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

			private void FindNewPath(Entity entity, PrefabRef prefabRef, DynamicBuffer<LayoutElement> layout, bool isBicycle, ref Game.Vehicles.PersonalCar personalCar, ref CarCurrentLane currentLane, ref PathOwner pathOwner, ref Target target)
			{
				CarData carData = m_PrefabCarData[prefabRef.m_Prefab];
				pathOwner.m_State &= ~(PathFlags.AddDestination | PathFlags.Divert);
				bool isHomeTarget = false;
				PathfindParameters parameters;
				SetupQueueTarget origin;
				SetupQueueTarget destination;
				if ((personalCar.m_State & PersonalCarFlags.Transporting) != 0)
				{
					parameters = new PathfindParameters
					{
						m_MaxSpeed = new float2(carData.m_MaxSpeed, 277.77777f),
						m_WalkSpeed = 5.555556f,
						m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
						m_ParkingTarget = VehicleUtils.GetParkingSource(entity, currentLane, ref m_ParkingLaneData, ref m_ConnectionLaneData),
						m_ParkingDelta = currentLane.m_CurvePosition.z,
						m_ParkingSize = VehicleUtils.GetParkingSize(entity, ref m_PrefabRefData, ref m_PrefabObjectGeometryData),
						m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults()
					};
					origin = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation
					};
					destination = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_Methods = PathMethod.Pedestrian,
						m_Entity = target.m_Target,
						m_RandomCost = 30f
					};
					if (isBicycle)
					{
						parameters.m_Methods = PathMethod.Pedestrian | PathMethod.Bicycle | PathMethod.BicycleParking;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
						origin.m_Methods = PathMethod.Bicycle | PathMethod.BicycleParking;
						origin.m_RoadTypes = RoadTypes.Bicycle;
					}
					else
					{
						parameters.m_Methods = VehicleUtils.GetPathMethods(carData) | PathMethod.Parking | PathMethod.Pedestrian;
						parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
						origin.m_Methods = VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
						origin.m_RoadTypes = RoadTypes.Car;
					}
					Entity entity2 = FindLeader(entity, layout);
					if (m_ResidentData.HasComponent(entity2))
					{
						PrefabRef prefabRef2 = m_PrefabRefData[entity2];
						Game.Creatures.Resident resident = m_ResidentData[entity2];
						CreatureData creatureData = m_PrefabCreatureData[prefabRef2.m_Prefab];
						HumanData humanData = m_PrefabHumanData[prefabRef2.m_Prefab];
						parameters.m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost;
						parameters.m_WalkSpeed = humanData.m_WalkSpeed;
						parameters.m_Methods |= RouteUtils.GetTaxiMethods(resident) | RouteUtils.GetPublicTransportMethods(resident, m_TimeOfDay);
						destination.m_ActivityMask = creatureData.m_SupportedActivities;
						if (m_HouseholdMemberData.TryGetComponent(resident.m_Citizen, out var componentData) && m_PropertyRenterData.TryGetComponent(componentData.m_Household, out var componentData2))
						{
							parameters.m_Authorization1 = componentData2.m_Property;
							isHomeTarget |= componentData2.m_Property == target.m_Target;
						}
						if (m_WorkerData.HasComponent(resident.m_Citizen))
						{
							Worker worker = m_WorkerData[resident.m_Citizen];
							parameters.m_Authorization2 = m_PropertyRenterData.HasComponent(worker.m_Workplace) ? m_PropertyRenterData[worker.m_Workplace].m_Property : worker.m_Workplace;
						}
						if (m_CitizenData.HasComponent(resident.m_Citizen))
						{
							Citizen citizen = m_CitizenData[resident.m_Citizen];
							Entity household = m_HouseholdMemberData[resident.m_Citizen].m_Household;
							Household household2 = m_HouseholdData[household];
							parameters.m_Weights = CitizenUtils.GetPathfindWeights(citizen, household2, m_HouseholdCitizens[household].Length);
						}
						if (m_TravelPurposeData.TryGetComponent(resident.m_Citizen, out var componentData3))
						{
							switch (componentData3.m_Purpose)
							{
							case Purpose.EmergencyShelter:
								parameters.m_Weights = new PathfindWeights(1f, 0.2f, 0f, 0.1f);
								break;
							case Purpose.MovingAway:
								parameters.m_MaxCost = CitizenBehaviorSystem.kMaxMovingAwayCost;
								break;
							}
						}
						if (m_DivertData.TryGetComponent(entity2, out var componentData4))
						{
							CreatureUtils.DivertDestination(ref destination, ref pathOwner, componentData4);
							isHomeTarget &= componentData4.m_Purpose == Purpose.None;
						}
						if (isBicycle && isHomeTarget)
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
						m_Methods = VehicleUtils.GetPathMethods(carData),
						m_ParkingTarget = VehicleUtils.GetParkingSource(entity, currentLane, ref m_ParkingLaneData, ref m_ConnectionLaneData),
						m_ParkingDelta = currentLane.m_CurvePosition.z,
						m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData)
					};
					origin = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_Methods = (VehicleUtils.GetPathMethods(carData) | PathMethod.Parking),
						m_RoadTypes = RoadTypes.Car
					};
					destination = new SetupQueueTarget
					{
						m_Type = SetupTargetType.CurrentLocation,
						m_Methods = VehicleUtils.GetPathMethods(carData),
						m_RoadTypes = RoadTypes.Car,
						m_Entity = target.m_Target
					};
				}
				if (isHomeTarget)
				{
					personalCar.m_State |= PersonalCarFlags.HomeTarget;
				}
				else
				{
					personalCar.m_State &= ~PersonalCarFlags.HomeTarget;
				}
				VehicleUtils.SetupPathfind(item: new SetupQueueItem(entity, parameters, origin, destination), currentLane: ref currentLane, pathOwner: ref pathOwner, queue: m_PathfindQueue);
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		[BurstCompile]
		private struct TransferMoneyJob : IJob
		{
			public BufferLookup<Resources> m_Resources;
			public NativeQueue<MoneyTransfer> m_MoneyTransferQueue;
			public void Execute()
			{
				MoneyTransfer item;
				while (m_MoneyTransferQueue.TryDequeue(out item))
				{
					if (m_Resources.HasBuffer(item.m_Payer) && m_Resources.HasBuffer(item.m_Recipient))
					{
						DynamicBuffer<Resources> resources = m_Resources[item.m_Payer];
						DynamicBuffer<Resources> resources2 = m_Resources[item.m_Recipient];
						EconomyUtils.AddResources(Resource.Money, -item.m_Amount, resources);
						EconomyUtils.AddResources(Resource.Money, item.m_Amount, resources2);
					}
				}
			}
		}

		private EndFrameBarrier m_EndFrameBarrier;

		private SimulationSystem m_SimulationSystem;

		private PathfindSetupSystem m_PathfindSetupSystem;

		private CitySystem m_CitySystem;

		private TimeSystem m_TimeSystem;

		private ServiceFeeSystem m_ServiceFeeSystem;

		private ActionsMod m_Actions;

		private EntityQuery m_VehicleQuery;

		private ComponentTypeSet m_MovingToParkedCarRemoveTypes;

		private ComponentTypeSet m_MovingToParkedCarAddTypes;

		protected override void OnCreate()
		{
			base.OnCreate();
			m_EndFrameBarrier = World.GetOrCreateSystemManaged<EndFrameBarrier>();
			m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
			m_PathfindSetupSystem = World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
			m_TimeSystem = World.GetOrCreateSystemManaged<TimeSystem>();
			m_ServiceFeeSystem = World.GetOrCreateSystemManaged<ServiceFeeSystem>();
			m_Actions = World.GetOrCreateSystemManaged<ActionsMod>();
			m_VehicleQuery = GetEntityQuery(ComponentType.ReadWrite<Game.Vehicles.PersonalCar>(), ComponentType.ReadOnly<CarCurrentLane>(), ComponentType.ReadOnly<UpdateFrame>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<TripSource>(), ComponentType.Exclude<OutOfControl>(), ComponentType.Exclude<Destroyed>());
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
				ComponentType.ReadWrite<PathElement>()
			});
			m_MovingToParkedCarAddTypes = new ComponentTypeSet(ComponentType.ReadWrite<ParkedCar>(), ComponentType.ReadWrite<Stopped>(), ComponentType.ReadWrite<Updated>());
		}

		protected override void OnUpdate()
		{
			uint index = m_SimulationSystem.frameIndex % 16;
			m_VehicleQuery.ResetFilter();
			m_VehicleQuery.SetSharedComponentFilter(new UpdateFrame(index));
			m_Actions.m_MoneyTransferQueue = new NativeQueue<MoneyTransfer>(Allocator.TempJob);
			JobHandle deps;
			JobHandle jobHandle = new PersonalCarTickJob
			{
				m_EntityType = SystemAPI.GetEntityTypeHandle(),
				m_UnspawnedType = SystemAPI.GetComponentTypeHandle<Unspawned>(isReadOnly: true),
				m_BicycleType = SystemAPI.GetComponentTypeHandle<Bicycle>(isReadOnly: true),
				m_PrefabRefType = SystemAPI.GetComponentTypeHandle<PrefabRef>(isReadOnly: true),
				m_LayoutElementType = SystemAPI.GetBufferTypeHandle<LayoutElement>(isReadOnly: true),
				m_PersonalCarType = SystemAPI.GetComponentTypeHandle<Game.Vehicles.PersonalCar>(isReadOnly: false),
				m_CarType = SystemAPI.GetComponentTypeHandle<Car>(isReadOnly: false),
				m_CurrentLaneType = SystemAPI.GetComponentTypeHandle<CarCurrentLane>(isReadOnly: false),
				m_CarNavigationLaneType = SystemAPI.GetBufferTypeHandle<CarNavigationLane>(isReadOnly: false),
				m_EntityLookup = SystemAPI.GetEntityStorageInfoLookup(),
				m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(isReadOnly: true),
				m_OwnerData = SystemAPI.GetComponentLookup<Owner>(isReadOnly: true),
				m_SpawnLocationData = SystemAPI.GetComponentLookup<Game.Objects.SpawnLocation>(isReadOnly: true),
				m_UnspawnedData = SystemAPI.GetComponentLookup<Unspawned>(isReadOnly: true),
				m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(isReadOnly: true),
				m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true),
				m_PrefabParkingLaneData = SystemAPI.GetComponentLookup<ParkingLaneData>(isReadOnly: true),
				m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(isReadOnly: true),
				m_PrefabCreatureData = SystemAPI.GetComponentLookup<CreatureData>(isReadOnly: true),
				m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(isReadOnly: true),
				m_PrefabSpawnLocationData = SystemAPI.GetComponentLookup<SpawnLocationData>(isReadOnly: true),
				m_PropertyRenterData = SystemAPI.GetComponentLookup<PropertyRenter>(isReadOnly: true),
				m_CarLaneData = SystemAPI.GetComponentLookup<Game.Net.CarLane>(isReadOnly: true),
				m_PedestrianLaneData = SystemAPI.GetComponentLookup<Game.Net.PedestrianLane>(isReadOnly: true),
				m_ParkingLaneData = SystemAPI.GetComponentLookup<Game.Net.ParkingLane>(isReadOnly: true),
				m_GarageLaneData = SystemAPI.GetComponentLookup<GarageLane>(isReadOnly: true),
				m_ConnectionLaneData = SystemAPI.GetComponentLookup<Game.Net.ConnectionLane>(isReadOnly: true),
				m_CurveData = SystemAPI.GetComponentLookup<Curve>(isReadOnly: true),
				m_SlaveLaneData = SystemAPI.GetComponentLookup<SlaveLane>(isReadOnly: true),
				m_ResidentData = SystemAPI.GetComponentLookup<Game.Creatures.Resident>(isReadOnly: true),
				m_DivertData = SystemAPI.GetComponentLookup<Divert>(isReadOnly: true),
				m_CurrentVehicleData = SystemAPI.GetComponentLookup<CurrentVehicle>(isReadOnly: true),
				m_CitizenData = SystemAPI.GetComponentLookup<Citizen>(isReadOnly: true),
				m_HouseholdMemberData = SystemAPI.GetComponentLookup<HouseholdMember>(isReadOnly: true),
				m_HouseholdData = SystemAPI.GetComponentLookup<Household>(isReadOnly: true),
				m_WorkerData = SystemAPI.GetComponentLookup<Worker>(isReadOnly: true),
				m_TravelPurposeData = SystemAPI.GetComponentLookup<TravelPurpose>(isReadOnly: true),
				m_MovingAwayData = SystemAPI.GetComponentLookup<MovingAway>(isReadOnly: true),
				m_Passengers = SystemAPI.GetBufferLookup<Passenger>(isReadOnly: true),
				m_LaneObjects = SystemAPI.GetBufferLookup<LaneObject>(isReadOnly: true),
				m_LaneOverlaps = SystemAPI.GetBufferLookup<LaneOverlap>(isReadOnly: true),
				m_SubLanes = SystemAPI.GetBufferLookup<Game.Net.SubLane>(isReadOnly: true),
				m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(isReadOnly: true),
				m_TargetData = SystemAPI.GetComponentLookup<Target>(isReadOnly: false),
				m_PathOwnerData = SystemAPI.GetComponentLookup<PathOwner>(isReadOnly: false),
				m_PathElements = SystemAPI.GetBufferLookup<PathElement>(isReadOnly: false),
				m_RandomSeed = RandomSeed.Next(),
				m_City = m_CitySystem.City,
				m_TimeOfDay = m_TimeSystem.normalizedTime,
				m_MovingToParkedCarRemoveTypes = m_MovingToParkedCarRemoveTypes,
				m_MovingToParkedCarAddTypes = m_MovingToParkedCarAddTypes,
				m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
				m_PathfindQueue = m_PathfindSetupSystem.GetQueue(this, 64).AsParallelWriter(),
				m_MoneyTransferQueue = m_Actions.m_MoneyTransferQueue.AsParallelWriter(),
				m_FeeQueue = m_ServiceFeeSystem.GetFeeQueue(out deps).AsParallelWriter()
			}.ScheduleParallel(m_VehicleQuery, JobHandle.CombineDependencies(Dependency, deps));
			m_PathfindSetupSystem.AddQueueWriter(jobHandle);
			m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
			m_ServiceFeeSystem.AddQueueWriter(jobHandle);
			m_Actions.m_Dependency = jobHandle;
			Dependency = jobHandle;
		}

	}
}
