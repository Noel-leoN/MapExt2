using System.Runtime.CompilerServices;
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
using Game.Events;
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
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace Game.Simulation
{
	[CompilerGenerated]
	public class TripNeededSystem : GameSystemBase
	{
		[BurstCompile]
		private struct CompanyJob : IJobChunk
		{
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> m_PrefabType;

			public BufferTypeHandle<TripNeeded> m_TripNeededType;

			[ReadOnly]
			public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;

			[ReadOnly]
			public ComponentTypeHandle<CreatureData> m_CreatureDataType;

			[ReadOnly]
			public ComponentTypeHandle<ResidentData> m_ResidentDataType;

			[ReadOnly]
			public BufferTypeHandle<OwnedVehicle> m_VehicleType;

			public BufferTypeHandle<Game.Economy.Resources> m_ResourceType;

			[ReadOnly]
			public ComponentLookup<TransportCompanyData> m_TransportCompanyDatas;

			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			[ReadOnly]
			public ComponentLookup<DeliveryTruckData> m_PrefabDeliveryTruckData;

			[ReadOnly]
			public ComponentLookup<ObjectData> m_PrefabObjectData;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_Transforms;

			[ReadOnly]
			public ComponentLookup<ServiceRequest> m_ServiceRequestData;

			[ReadOnly]
			public ComponentLookup<PathInformation> m_PathInformationData;

			[ReadOnly]
			public ComponentLookup<UnderConstruction> m_UnderConstructionData;

			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenterData;

			[ReadOnly]
			public BufferLookup<PathElement> m_PathElements;

			[ReadOnly]
			public BufferLookup<ActivityLocationElement> m_ActivityLocationElements;

			[ReadOnly]
			public BufferLookup<Efficiency> m_EfficiencyBufs;

			[ReadOnly]
			public BufferLookup<InstalledUpgrade> m_InstalledUpgradeBufs;

			[ReadOnly]
			public NativeList<ArchetypeChunk> m_HumanChunks;

			[ReadOnly]
			public bool m_LeftHandTraffic;

			[ReadOnly]
			public RandomSeed m_RandomSeed;

			[ReadOnly]
			public EntityArchetype m_HandleRequestArchetype;

			[ReadOnly]
			public DeliveryTruckSelectData m_DeliveryTruckSelectData;

			[ReadOnly]
			public ComponentTypeSet m_CurrentLaneTypesRelative;

			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			public bool m_DebugDisableSpawning;

			private void SpawnDeliveryTruck(int index, Entity owner, Entity from, ref Game.Objects.Transform transform, TripNeeded trip)
			{
				Entity entity;
				Entity entity2;
				if (this.m_ServiceRequestData.HasComponent(trip.m_TargetAgent))
				{
					if (!this.m_PathInformationData.TryGetComponent(trip.m_TargetAgent, out var componentData))
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
				if (!this.m_Prefabs.HasComponent(entity))
				{
					return;
				}
				Entity entity3 = entity;
				if (this.m_PropertyRenterData.TryGetComponent(entity3, out var componentData2))
				{
					entity3 = componentData2.m_Property;
				}
				uint num = 0u;
				if (this.m_UnderConstructionData.TryGetComponent(entity3, out var componentData3) && componentData3.m_NewPrefab == Entity.Null)
				{
					this.m_PathInformationData.TryGetComponent(entity2, out var componentData4);
					num = ObjectUtils.GetTripDelayFrames(componentData3, componentData4);
				}
				if (this.m_UnderConstructionData.TryGetComponent(from, out componentData3) && componentData3.m_NewPrefab == Entity.Null)
				{
					num = math.max(num, ObjectUtils.GetRemainingConstructionFrames(componentData3));
				}
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(owner.Index);
				DeliveryTruckFlags deliveryTruckFlags = (DeliveryTruckFlags)0u;
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
					deliveryTruckFlags |= DeliveryTruckFlags.Loaded | DeliveryTruckFlags.Delivering | DeliveryTruckFlags.UpkeepDelivery;
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
					deliveryTruckFlags = ((trip.m_Data <= 0) ? (deliveryTruckFlags | (DeliveryTruckFlags.Buying | DeliveryTruckFlags.StorageTransfer)) : (deliveryTruckFlags | (DeliveryTruckFlags.Loaded | DeliveryTruckFlags.StorageTransfer)));
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
				Resource resources = resource | resource2;
				int capacity = math.max(amount, returnAmount);
				if (!this.m_DeliveryTruckSelectData.TrySelectItem(ref random, resources, capacity, out var item))
				{
					return;
				}
				Entity entity4 = this.m_DeliveryTruckSelectData.CreateVehicle(this.m_CommandBuffer, index, ref random, ref this.m_PrefabDeliveryTruckData, ref this.m_PrefabObjectData, item, resource, resource2, ref amount, ref returnAmount, transform, from, deliveryTruckFlags, num);
				int maxCount = 1;
				if (this.CreatePassengers(index, entity4, item.m_Prefab1, transform, driver: true, ref maxCount, ref random) > 0)
				{
					this.m_CommandBuffer.AddBuffer<Passenger>(index, entity4);
				}
				this.m_CommandBuffer.SetComponent(index, entity4, new Target(entity));
				this.m_CommandBuffer.AddComponent(index, entity4, new Owner(owner));
				if (!(entity2 != Entity.Null))
				{
					return;
				}
				Entity e = this.m_CommandBuffer.CreateEntity(index, this.m_HandleRequestArchetype);
				this.m_CommandBuffer.SetComponent(index, e, new HandleRequest(entity2, entity4, completed: true));
				if (this.m_PathElements.HasBuffer(entity2))
				{
					DynamicBuffer<PathElement> sourceElements = this.m_PathElements[entity2];
					if (sourceElements.Length != 0)
					{
						DynamicBuffer<PathElement> targetElements = this.m_CommandBuffer.SetBuffer<PathElement>(index, entity4);
						PathUtils.CopyPath(sourceElements, default(PathOwner), 0, targetElements);
						this.m_CommandBuffer.SetComponent(index, entity4, new PathOwner(PathFlags.Updated));
						this.m_CommandBuffer.SetComponent(index, entity4, this.m_PathInformationData[entity2]);
					}
				}
			}

			private int CreatePassengers(int jobIndex, Entity vehicleEntity, Entity vehiclePrefab, Game.Objects.Transform transform, bool driver, ref int maxCount, ref Unity.Mathematics.Random random)
			{
				int num = 0;
				if (maxCount > 0 && this.m_ActivityLocationElements.TryGetBuffer(vehiclePrefab, out var bufferData))
				{
					ActivityMask activityMask = new ActivityMask(ActivityType.Driving);
					activityMask.m_Mask |= new ActivityMask(ActivityType.Biking).m_Mask;
					int num2 = 0;
					int num3 = -1;
					float num4 = float.MinValue;
					for (int i = 0; i < bufferData.Length; i++)
					{
						ActivityLocationElement activityLocationElement = bufferData[i];
						if ((activityLocationElement.m_ActivityMask.m_Mask & activityMask.m_Mask) != 0)
						{
							num2++;
							bool test = ((activityLocationElement.m_ActivityFlags & ActivityFlags.InvertLefthandTraffic) != 0 && this.m_LeftHandTraffic) || ((activityLocationElement.m_ActivityFlags & ActivityFlags.InvertRighthandTraffic) != 0 && !this.m_LeftHandTraffic);
							activityLocationElement.m_Position.x = math.select(activityLocationElement.m_Position.x, 0f - activityLocationElement.m_Position.x, test);
							if ((!(math.abs(activityLocationElement.m_Position.x) >= 0.5f) || activityLocationElement.m_Position.x >= 0f == this.m_LeftHandTraffic) && activityLocationElement.m_Position.z > num4)
							{
								num3 = i;
								num4 = activityLocationElement.m_Position.z;
							}
						}
					}
					int num5 = 100;
					if (driver && num3 != -1)
					{
						maxCount--;
						num2--;
					}
					if (num2 > maxCount)
					{
						num5 = maxCount * 100 / num2;
					}
					Relative component = default(Relative);
					for (int j = 0; j < bufferData.Length; j++)
					{
						ActivityLocationElement activityLocationElement2 = bufferData[j];
						if ((activityLocationElement2.m_ActivityMask.m_Mask & activityMask.m_Mask) != 0 && ((driver && j == num3) || random.NextInt(100) >= num5))
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
							CreatureData creatureData;
							PseudoRandomSeed randomSeed;
							Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out creatureData, out randomSeed);
							ObjectData objectData = this.m_PrefabObjectData[entity];
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
							if (driver && j == num3)
							{
								component4.m_Flags |= CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver;
							}
							Entity e = this.m_CommandBuffer.CreateEntity(jobIndex, objectData.m_Archetype);
							this.m_CommandBuffer.RemoveComponent(jobIndex, e, in this.m_CurrentLaneTypesRelative);
							this.m_CommandBuffer.SetComponent(jobIndex, e, transform);
							this.m_CommandBuffer.SetComponent(jobIndex, e, component2);
							this.m_CommandBuffer.SetComponent(jobIndex, e, component3);
							this.m_CommandBuffer.SetComponent(jobIndex, e, randomSeed);
							this.m_CommandBuffer.AddComponent(jobIndex, e, component4);
							this.m_CommandBuffer.AddComponent(jobIndex, e, component);
							num++;
						}
					}
				}
				return num;
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabType);
				BufferAccessor<OwnedVehicle> bufferAccessor = chunk.GetBufferAccessor(ref this.m_VehicleType);
				BufferAccessor<TripNeeded> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_TripNeededType);
				BufferAccessor<Game.Economy.Resources> bufferAccessor3 = chunk.GetBufferAccessor(ref this.m_ResourceType);
				NativeArray<PropertyRenter> nativeArray3 = chunk.GetNativeArray(ref this.m_PropertyRenterType);
				for (int i = 0; i < chunk.Count; i++)
				{
					Entity entity = nativeArray[i];
					Entity prefab = nativeArray2[i].m_Prefab;
					DynamicBuffer<TripNeeded> dynamicBuffer = bufferAccessor2[i];
					if (this.m_TransportCompanyDatas.HasComponent(prefab))
					{
						int transportCompanyAvailableVehicles = VehicleUtils.GetTransportCompanyAvailableVehicles(entity, ref this.m_EfficiencyBufs, ref this.m_Prefabs, ref this.m_TransportCompanyDatas, ref this.m_InstalledUpgradeBufs);
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
					if (!this.m_DebugDisableSpawning)
					{
						_ = bufferAccessor3[i];
						Entity entity2 = ((!chunk.Has(ref this.m_PropertyRenterType)) ? entity : nativeArray3[i].m_Property);
						if (this.m_Transforms.HasComponent(entity2))
						{
							Game.Objects.Transform transform = this.m_Transforms[entity2];
							this.SpawnDeliveryTruck(unfilteredChunkIndex, entity, entity2, ref transform, trip);
						}
					}
				}
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		private struct AnimalTargetInfo
		{
			public Entity m_Animal;

			public Entity m_Source;

			public Entity m_Target;
		}

		[BurstCompile]
		private struct PetTargetJob : IJob
		{
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			public NativeQueue<AnimalTargetInfo> m_AnimalQueue;

			public EntityCommandBuffer m_CommandBuffer;

			public void Execute()
			{
				int count = this.m_AnimalQueue.Count;
				if (count == 0)
				{
					return;
				}
				NativeParallelHashSet<Entity> nativeParallelHashSet = new NativeParallelHashSet<Entity>(count, Allocator.Temp);
				for (int i = 0; i < count; i++)
				{
					AnimalTargetInfo animalTargetInfo = this.m_AnimalQueue.Dequeue();
					if (this.m_CurrentBuildingData.HasComponent(animalTargetInfo.m_Animal) && !(this.m_CurrentBuildingData[animalTargetInfo.m_Animal].m_CurrentBuilding != animalTargetInfo.m_Source) && nativeParallelHashSet.Add(animalTargetInfo.m_Animal))
					{
						this.m_CommandBuffer.AddComponent(animalTargetInfo.m_Animal, new Target(animalTargetInfo.m_Target));
					}
				}
				nativeParallelHashSet.Dispose();
			}
		}

		[BurstCompile]
		private struct CitizeLeaveJob : IJob
		{
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			public ComponentLookup<CitizenPresence> m_CitizenPresenceData;

			public NativeQueue<Entity> m_LeaveQueue;

			public void Execute()
			{
				Entity item;
				while (this.m_LeaveQueue.TryDequeue(out item))
				{
					if (this.m_CurrentBuildingData.HasComponent(item))
					{
						CurrentBuilding currentBuilding = this.m_CurrentBuildingData[item];
						if (this.m_CitizenPresenceData.HasComponent(currentBuilding.m_CurrentBuilding))
						{
							CitizenPresence value = this.m_CitizenPresenceData[currentBuilding.m_CurrentBuilding];
							value.m_Delta = (sbyte)math.max(-127, value.m_Delta - 1);
							this.m_CitizenPresenceData[currentBuilding.m_CurrentBuilding] = value;
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

			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			public BufferTypeHandle<TripNeeded> m_TripNeededType;

			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			[ReadOnly]
			public ComponentTypeHandle<CurrentTransport> m_CurrentTransportType;

			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

			[ReadOnly]
			public ComponentTypeHandle<MailSender> m_MailSenderType;

			[ReadOnly]
			public ComponentTypeHandle<Citizen> m_CitizenType;

			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			[ReadOnly]
			public ComponentTypeHandle<AttendingMeeting> m_AttendingMeetingType;

			[ReadOnly]
			public ComponentTypeHandle<CreatureData> m_CreatureDataType;

			[ReadOnly]
			public ComponentTypeHandle<ResidentData> m_ResidentDataType;

			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_Properties;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_Transforms;

			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

			[ReadOnly]
			public ComponentLookup<ObjectData> m_ObjectDatas;

			[ReadOnly]
			public ComponentLookup<CarData> m_PrefabCarData;

			[ReadOnly]
			public ComponentLookup<HumanData> m_PrefabHumanData;

			[ReadOnly]
			public ComponentLookup<PathInformation> m_PathInfos;

			[ReadOnly]
			public ComponentLookup<ParkedCar> m_ParkedCarData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.Ambulance> m_AmbulanceData;

			[ReadOnly]
			public ComponentLookup<Game.Net.ConnectionLane> m_ConnectionLaneData;

			[ReadOnly]
			public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;

			[ReadOnly]
			public ComponentLookup<Target> m_Targets;

			[ReadOnly]
			public ComponentLookup<Deleted> m_Deleteds;

			[ReadOnly]
			public BufferLookup<PathElement> m_PathElements;

			[ReadOnly]
			public ComponentLookup<CarKeeper> m_CarKeepers;

			[ReadOnly]
			public ComponentLookup<BicycleOwner> m_BicycleOwners;

			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			[ReadOnly]
			public ComponentLookup<UnderConstruction> m_UnderConstructionData;

			[ReadOnly]
			public BufferLookup<CoordinatedMeetingAttendee> m_Attendees;

			[ReadOnly]
			public BufferLookup<HouseholdAnimal> m_HouseholdAnimals;

			[ReadOnly]
			public ComponentLookup<TravelPurpose> m_TravelPurposes;

			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> m_HaveCoordinatedMeetingDatas;

			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			[ReadOnly]
			public ComponentLookup<Criminal> m_CriminalData;

			[NativeDisableParallelForRestriction]
			public ComponentLookup<CoordinatedMeeting> m_Meetings;

			[NativeDisableParallelForRestriction]
			public ComponentLookup<Worker> m_Workers;

			[NativeDisableParallelForRestriction]
			public ComponentLookup<Game.Citizens.Student> m_Students;

			[ReadOnly]
			public NativeList<ArchetypeChunk> m_HumanChunks;

			[ReadOnly]
			public RandomSeed m_RandomSeed;

			[ReadOnly]
			public float m_TimeOfDay;

			[ReadOnly]
			public EntityArchetype m_ResetTripArchetype;

			[ReadOnly]
			public ComponentTypeSet m_HumanSpawnTypes;

			[ReadOnly]
			public ComponentTypeSet m_PathfindTypes;

			[ReadOnly]
			public PersonalCarSelectData m_PersonalCarSelectData;

			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathQueue;

			public NativeQueue<AnimalTargetInfo>.ParallelWriter m_AnimalQueue;

			public NativeQueue<Entity>.ParallelWriter m_LeaveQueue;

			public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;

			public bool m_DebugDisableSpawning;

			private void GetResidentFlags(Entity citizen, Entity currentBuilding, bool isMailSender, bool pathFailed, ref Target target, ref Purpose purpose, ref Purpose divertPurpose, ref uint timer, ref bool hasDivertPath)
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
					if (this.m_TravelPurposes.HasComponent(citizen))
					{
						purpose = this.m_TravelPurposes[citizen].m_Purpose;
					}
					else
					{
						purpose = Purpose.None;
					}
					timer = 0u;
					hasDivertPath = true;
					break;
				case Purpose.Hospital:
					if (this.m_AmbulanceData.HasComponent(target.m_Target))
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

			private Entity SpawnResident(int index, Entity citizen, Entity fromBuilding, Citizen citizenData, Target target, ResidentFlags flags, Purpose divertPurpose, uint timer, bool hasDivertPath, bool isDead, bool isCarried)
			{
				CreatureData creatureData;
				PseudoRandomSeed randomSeed;
				Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out creatureData, out randomSeed);
				ObjectData objectData = this.m_ObjectDatas[entity];
				PrefabRef component = new PrefabRef
				{
					m_Prefab = entity
				};
				Game.Objects.Transform transform = ((!this.m_Transforms.HasComponent(fromBuilding)) ? new Game.Objects.Transform
				{
					m_Position = default(float3),
					m_Rotation = new quaternion(0f, 0f, 0f, 1f)
				} : this.m_Transforms[fromBuilding]);
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
				Entity entity2 = this.m_CommandBuffer.CreateEntity(index, objectData.m_Archetype);
				Entity entity3 = Entity.Null;
				HumanCurrentLane component6 = default(HumanCurrentLane);
				if (this.m_PathElements.TryGetBuffer(citizen, out var bufferData) && bufferData.Length > 0)
				{
					PathElement pathElement = bufferData[0];
					CreatureLaneFlags creatureLaneFlags = (CreatureLaneFlags)0u;
					if ((pathElement.m_Flags & PathElementFlags.Secondary) != 0)
					{
						Unity.Mathematics.Random random = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
						entity3 = this.m_PersonalCarSelectData.CreateVehicle(this.m_CommandBuffer, index, ref random, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, transform, fromBuilding, citizen, PersonalCarFlags.Boarding, stopped: false);
						if (entity3 != Entity.Null)
						{
							DynamicBuffer<PathElement> targetElements = this.m_CommandBuffer.SetBuffer<PathElement>(index, entity2);
							PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements);
							Game.Vehicles.CarLaneFlags carLaneFlags = Game.Vehicles.CarLaneFlags.EndOfPath | Game.Vehicles.CarLaneFlags.EndReached | Game.Vehicles.CarLaneFlags.FixedLane;
							if (this.m_ConnectionLaneData.TryGetComponent(pathElement.m_Target, out var componentData))
							{
								carLaneFlags = (((componentData.m_Flags & ConnectionLaneFlags.Area) == 0) ? (carLaneFlags | Game.Vehicles.CarLaneFlags.Connection) : (carLaneFlags | Game.Vehicles.CarLaneFlags.Area));
							}
							this.m_CommandBuffer.SetComponent(index, entity3, new CarCurrentLane(pathElement, carLaneFlags));
							this.m_CommandBuffer.SetComponent(index, citizen, new BicycleOwner
							{
								m_Bicycle = entity3
							});
							component2.m_Flags |= ResidentFlags.InVehicle;
							creatureLaneFlags |= CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached;
						}
					}
					DynamicBuffer<PathElement> targetElements2 = this.m_CommandBuffer.SetBuffer<PathElement>(index, entity2);
					PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements2);
					component6 = new HumanCurrentLane(pathElement, creatureLaneFlags);
					component4.m_State |= PathFlags.Updated;
				}
				this.m_CommandBuffer.AddComponent(index, entity2, in this.m_HumanSpawnTypes);
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
					this.m_CommandBuffer.AddComponent(index, entity2, new Divert
					{
						m_Purpose = divertPurpose
					});
				}
				this.m_CommandBuffer.SetComponent(index, entity2, transform);
				this.m_CommandBuffer.SetComponent(index, entity2, component);
				this.m_CommandBuffer.SetComponent(index, entity2, target);
				this.m_CommandBuffer.SetComponent(index, entity2, component2);
				this.m_CommandBuffer.SetComponent(index, entity2, component3);
				this.m_CommandBuffer.SetComponent(index, entity2, component4);
				this.m_CommandBuffer.SetComponent(index, entity2, randomSeed);
				this.m_CommandBuffer.SetComponent(index, entity2, component6);
				this.m_CommandBuffer.SetComponent(index, entity2, component5);
				if (entity3 != Entity.Null)
				{
					this.m_CommandBuffer.RemoveComponent<TripSource>(index, entity2);
					this.m_CommandBuffer.AddComponent(index, entity2, new CurrentVehicle(entity3, CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver | CreatureVehicleFlags.Entering));
				}
				return entity2;
			}

			private void ResetTrip(int index, Entity creature, Entity citizen, Entity fromBuilding, Target target, ResidentFlags flags, Purpose divertPurpose, uint timer, bool hasDivertPath)
			{
				Entity e = this.m_CommandBuffer.CreateEntity(index, this.m_ResetTripArchetype);
				this.m_CommandBuffer.SetComponent(index, e, new ResetTrip
				{
					m_Creature = creature,
					m_Source = fromBuilding,
					m_Target = target.m_Target,
					m_ResidentFlags = flags,
					m_DivertPurpose = divertPurpose,
					m_Delay = timer,
					m_HasDivertPath = hasDivertPath
				});
				if (this.m_PathElements.TryGetBuffer(citizen, out var bufferData) && bufferData.Length > 0)
				{
					DynamicBuffer<PathElement> targetElements = this.m_CommandBuffer.AddBuffer<PathElement>(index, e);
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
				for (int num = trips.Length - 1; num >= 0; num--)
				{
					if (trips[num].m_Purpose == purpose)
					{
						trips.RemoveAt(num);
					}
				}
			}

			private Entity FindDistrict(Entity building)
			{
				if (this.m_CurrentDistrictData.HasComponent(building))
				{
					return this.m_CurrentDistrictData[building].m_District;
				}
				return Entity.Null;
			}

			private void AddPetTargets(Entity household, Entity source, Entity target)
			{
				if (this.m_HouseholdAnimals.HasBuffer(household))
				{
					DynamicBuffer<HouseholdAnimal> dynamicBuffer = this.m_HouseholdAnimals[household];
					for (int i = 0; i < dynamicBuffer.Length; i++)
					{
						HouseholdAnimal householdAnimal = dynamicBuffer[i];
						this.m_AnimalQueue.Enqueue(new AnimalTargetInfo
						{
							m_Animal = householdAnimal.m_HouseholdPet,
							m_Source = source,
							m_Target = target
						});
					}
				}
			}

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor(ref this.m_TripNeededType);
				NativeArray<HouseholdMember> nativeArray2 = chunk.GetNativeArray(ref this.m_HouseholdMemberType);
				NativeArray<CurrentBuilding> nativeArray3 = chunk.GetNativeArray(ref this.m_CurrentBuildingType);
				NativeArray<CurrentTransport> nativeArray4 = chunk.GetNativeArray(ref this.m_CurrentTransportType);
				NativeArray<Citizen> nativeArray5 = chunk.GetNativeArray(ref this.m_CitizenType);
				NativeArray<HealthProblem> nativeArray6 = chunk.GetNativeArray(ref this.m_HealthProblemType);
				NativeArray<AttendingMeeting> nativeArray7 = chunk.GetNativeArray(ref this.m_AttendingMeetingType);
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
					bool flag = trips[0].m_Purpose == Purpose.MovingAway;
					bool flag2 = trips[0].m_Purpose == Purpose.Safety || trips[0].m_Purpose == Purpose.Escape;
					bool isMailSender = chunk.IsComponentEnabled(ref this.m_MailSenderType, i);
					bool flag3 = false;
					bool flag4 = false;
					PathInformation componentData;
					bool flag5 = this.m_PathInfos.TryGetComponent(entity, out componentData);
					Criminal componentData2;
					bool flag6 = this.m_CriminalData.TryGetComponent(entity, out componentData2) && (componentData2.m_Flags & (CriminalFlags.Prisoner | CriminalFlags.Arrested | CriminalFlags.Sentenced)) != 0;
					if (nativeArray6.Length != 0 && !flag6)
					{
						HealthProblem healthProblem = nativeArray6[i];
						if ((healthProblem.m_Flags & (HealthProblemFlags.Dead | HealthProblemFlags.RequireTransport | HealthProblemFlags.InDanger | HealthProblemFlags.Trapped)) != HealthProblemFlags.None)
						{
							flag3 = (healthProblem.m_Flags & HealthProblemFlags.Dead) != 0;
							flag4 = (healthProblem.m_Flags & HealthProblemFlags.RequireTransport) != 0;
							if (!(flag3 || flag4))
							{
								if (flag5)
								{
									this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								}
								continue;
							}
							while (trips.Length > 0 && trips[0].m_Purpose != Purpose.Deathcare && trips[0].m_Purpose != Purpose.Hospital)
							{
								trips.RemoveAt(0);
							}
							if (trips.Length == 0)
							{
								if (flag5)
								{
									this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								}
								continue;
							}
						}
					}
					if (!flag && nativeArray7.Length != 0 && !flag6)
					{
						Entity meeting = nativeArray7[i].m_Meeting;
						if (this.m_PrefabRefData.HasComponent(meeting))
						{
							Entity prefab = this.m_PrefabRefData[meeting].m_Prefab;
							CoordinatedMeeting coordinatedMeeting = this.m_Meetings[meeting];
							if (this.m_HaveCoordinatedMeetingDatas.HasBuffer(prefab))
							{
								DynamicBuffer<HaveCoordinatedMeetingData> dynamicBuffer = this.m_HaveCoordinatedMeetingDatas[prefab];
								if (dynamicBuffer.Length > coordinatedMeeting.m_Phase)
								{
									HaveCoordinatedMeetingData haveCoordinatedMeetingData = dynamicBuffer[coordinatedMeeting.m_Phase];
									while (trips.Length > 0 && trips[0].m_Purpose != haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose)
									{
										trips.RemoveAt(0);
									}
									if (trips.Length == 0)
									{
										if (flag5)
										{
											this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
										}
										continue;
									}
								}
							}
						}
					}
					if ((nativeArray5[i].m_State & CitizenFlags.MovingAwayReachOC) != CitizenFlags.None)
					{
						if (flag5)
						{
							this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
						}
						continue;
					}
					if (flag5)
					{
						if ((componentData.m_State & PathFlags.Pending) != 0)
						{
							continue;
						}
						if ((((componentData.m_Origin != Entity.Null && componentData.m_Origin == componentData.m_Destination) || nativeArray3[i].m_CurrentBuilding == componentData.m_Destination) && !flag2) || !this.m_Targets.HasComponent(entity))
						{
							this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
							this.RemoveAllTrips(trips);
							continue;
						}
					}
					if (this.m_DebugDisableSpawning)
					{
						continue;
					}
					PseudoRandomSeed randomSeed;
					Entity trailerPrefab;
					float offset;
					if (this.m_Targets.HasComponent(entity))
					{
						Target target = this.m_Targets[entity];
						if (target.m_Target == Entity.Null)
						{
							if (!flag5)
							{
								this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
								continue;
							}
							Entity destination = componentData.m_Destination;
							if (destination == Entity.Null)
							{
								this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
								this.RemoveAllTrips(trips);
								this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								continue;
							}
							target.m_Target = destination;
						}
						Entity entity2 = target.m_Target;
						if (this.m_Properties.TryGetComponent(entity2, out var componentData3))
						{
							entity2 = componentData3.m_Property;
						}
						if (currentBuilding == entity2 && !flag2)
						{
							this.m_CommandBuffer.SetComponentEnabled<Arrived>(unfilteredChunkIndex, entity, value: true);
							this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new TravelPurpose
							{
								m_Data = trips[0].m_Data,
								m_Purpose = trips[0].m_Purpose,
								m_Resource = trips[0].m_Resource
							});
							this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
							if (flag5)
							{
								this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
							}
							this.RemoveAllTrips(trips);
							continue;
						}
						bool flag7 = (flag3 && trips[0].m_Purpose == Purpose.Deathcare) || (flag4 && trips[0].m_Purpose == Purpose.Hospital);
						if (!flag5 && !flag7)
						{
							this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
							this.m_CommandBuffer.SetComponent(unfilteredChunkIndex, entity, new PathInformation
							{
								m_State = PathFlags.Pending
							});
							Citizen citizen = nativeArray5[i];
							CreatureData creatureData;
							Entity entity3 = ObjectEmergeSystem.SelectResidentPrefab(citizen, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out creatureData, out randomSeed);
							HumanData humanData = default(HumanData);
							if (entity3 != Entity.Null)
							{
								humanData = this.m_PrefabHumanData[entity3];
							}
							Household household2 = this.m_Households[household];
							DynamicBuffer<HouseholdCitizen> dynamicBuffer2 = this.m_HouseholdCitizens[household];
							PathfindParameters parameters = new PathfindParameters
							{
								m_MaxSpeed = 277.77777f,
								m_WalkSpeed = humanData.m_WalkSpeed,
								m_Weights = CitizenUtils.GetPathfindWeights(citizen, household2, dynamicBuffer2.Length),
								m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(this.m_TimeOfDay)),
								m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
								m_MaxCost = math.select(CitizenBehaviorSystem.kMaxPathfindCost, CitizenBehaviorSystem.kMaxMovingAwayCost, flag)
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
							if (this.m_PropertyRenters.TryGetComponent(household, out var componentData4))
							{
								parameters.m_Authorization1 = componentData4.m_Property;
							}
							if (this.m_Workers.HasComponent(entity))
							{
								Worker worker = this.m_Workers[entity];
								if (this.m_PropertyRenters.HasComponent(worker.m_Workplace))
								{
									parameters.m_Authorization2 = this.m_PropertyRenters[worker.m_Workplace].m_Property;
								}
								else
								{
									parameters.m_Authorization2 = worker.m_Workplace;
								}
							}
							if (this.m_CarKeepers.IsComponentEnabled(entity))
							{
								Entity car = this.m_CarKeepers[entity].m_Car;
								if (this.m_ParkedCarData.HasComponent(car))
								{
									PrefabRef prefabRef = this.m_PrefabRefData[car];
									ParkedCar parkedCar = this.m_ParkedCarData[car];
									CarData carData = this.m_PrefabCarData[prefabRef.m_Prefab];
									parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
									parameters.m_ParkingTarget = parkedCar.m_Lane;
									parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
									parameters.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref this.m_PrefabRefData, ref this.m_ObjectGeometryData);
									parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
									parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
									if (this.m_PersonalCarData.TryGetComponent(car, out var componentData5) && (componentData5.m_State & PersonalCarFlags.HomeTarget) == 0)
									{
										parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
									}
								}
							}
							else if (this.m_BicycleOwners.IsComponentEnabled(entity))
							{
								Entity bicycle = this.m_BicycleOwners[entity].m_Bicycle;
								if (!this.m_PrefabRefData.TryGetComponent(bicycle, out var componentData6) && currentBuilding == componentData4.m_Property)
								{
									Unity.Mathematics.Random random = citizen.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
									componentData6.m_Prefab = this.m_PersonalCarSelectData.SelectVehiclePrefab(ref random, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, out trailerPrefab);
								}
								if (this.m_PrefabCarData.TryGetComponent(componentData6.m_Prefab, out var componentData7) && this.m_ObjectGeometryData.TryGetComponent(componentData6.m_Prefab, out var componentData8))
								{
									parameters.m_MaxSpeed.x = componentData7.m_MaxSpeed;
									parameters.m_ParkingSize = VehicleUtils.GetParkingSize(componentData8, out offset);
									parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
									parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
									CurrentTransport value;
									if (this.m_ParkedCarData.TryGetComponent(bicycle, out var componentData9))
									{
										parameters.m_ParkingTarget = componentData9.m_Lane;
										parameters.m_ParkingDelta = componentData9.m_CurvePosition;
										if (this.m_PersonalCarData.TryGetComponent(bicycle, out var componentData10) && (componentData10.m_State & PersonalCarFlags.HomeTarget) == 0)
										{
											parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
										}
									}
									else if (!CollectionUtils.TryGet(nativeArray4, i, out value) || !this.m_PrefabRefData.HasComponent(value.m_CurrentTransport) || this.m_Deleteds.HasComponent(value.m_CurrentTransport))
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
							this.m_PathQueue.Enqueue(value2);
							continue;
						}
						DynamicBuffer<PathElement> dynamicBuffer3 = default(DynamicBuffer<PathElement>);
						if (!flag7)
						{
							dynamicBuffer3 = this.m_PathElements[entity];
						}
						TripNeeded tripNeeded = trips[0];
						if ((!flag7 && dynamicBuffer3.Length > 0) || this.m_PrefabRefData.HasComponent(tripNeeded.m_TargetAgent))
						{
							Entity currentBuilding2 = nativeArray3[i].m_CurrentBuilding;
							Entity entity4 = Entity.Null;
							PropertyRenter componentData11;
							bool flag8 = this.m_PropertyRenters.TryGetComponent(household, out componentData11);
							if (!flag7 && flag8 && currentBuilding2.Equals(componentData11.m_Property))
							{
								if (componentData.m_Destination != Entity.Null)
								{
									if ((componentData.m_Methods & (PathMethod.PublicTransportDay | PathMethod.Taxi | PathMethod.PublicTransportNight)) != 0)
									{
										if (this.m_DebugPathQueuePublic.IsCreated)
										{
											this.m_DebugPathQueuePublic.Enqueue(Mathf.RoundToInt(componentData.m_TotalCost));
										}
										if ((componentData.m_Methods & PathMethod.Taxi) != 0)
										{
											if (this.m_DebugTaxiDuration.IsCreated)
											{
												this.m_DebugTaxiDuration.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
											}
										}
										else if (this.m_DebugPublicTransportDuration.IsCreated)
										{
											this.m_DebugPublicTransportDuration.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
										}
									}
									else if ((componentData.m_Methods & (PathMethod.Road | PathMethod.MediumRoad)) != 0)
									{
										if (this.m_DebugPathQueueCar.IsCreated)
										{
											this.m_DebugPathQueueCar.Enqueue(Mathf.RoundToInt(componentData.m_TotalCost));
										}
										if (this.m_DebugCarDuration.IsCreated)
										{
											this.m_DebugCarDuration.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
										}
									}
									else if ((componentData.m_Methods & PathMethod.Pedestrian) != 0)
									{
										if (componentData.m_Distance > 3000f)
										{
											if (this.m_DebugPathQueuePedestrian.IsCreated)
											{
												this.m_DebugPathQueuePedestrian.Enqueue(Mathf.RoundToInt(componentData.m_TotalCost));
											}
											if (this.m_DebugPedestrianDuration.IsCreated)
											{
												this.m_DebugPedestrianDuration.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
											}
										}
										else
										{
											if (this.m_DebugPathQueuePedestrianShort.IsCreated)
											{
												this.m_DebugPathQueuePedestrianShort.Enqueue(Mathf.RoundToInt(componentData.m_TotalCost));
											}
											if (this.m_DebugPedestrianDurationShort.IsCreated)
											{
												this.m_DebugPedestrianDurationShort.Enqueue(Mathf.RoundToInt(componentData.m_Duration));
											}
										}
									}
								}
								if (tripNeeded.m_Purpose == Purpose.GoingToWork && this.m_Workers.HasComponent(entity))
								{
									Worker value3 = this.m_Workers[entity];
									if (componentData.m_Destination == Entity.Null)
									{
										this.m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, entity);
										this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenBecameUnemployed, Entity.Null, entity, value3.m_Workplace));
									}
									else
									{
										value3.m_LastCommuteTime = componentData.m_Duration;
										this.m_Workers[entity] = value3;
									}
								}
								else if (tripNeeded.m_Purpose == Purpose.GoingToSchool && this.m_Students.HasComponent(entity))
								{
									if (componentData.m_Destination == Entity.Null)
									{
										this.m_CommandBuffer.AddComponent<StudentsRemoved>(unfilteredChunkIndex, this.m_Students[entity].m_School);
										this.m_CommandBuffer.RemoveComponent<Game.Citizens.Student>(unfilteredChunkIndex, entity);
									}
									else
									{
										Game.Citizens.Student value4 = this.m_Students[entity];
										value4.m_LastCommuteTime = componentData.m_Duration;
										this.m_Students[entity] = value4;
									}
								}
							}
							ResidentFlags residentFlags = ResidentFlags.None;
							if (nativeArray7.Length > 0)
							{
								Entity meeting2 = nativeArray7[i].m_Meeting;
								if (this.m_PrefabRefData.HasComponent(meeting2))
								{
									CoordinatedMeeting value5 = this.m_Meetings[meeting2];
									DynamicBuffer<HaveCoordinatedMeetingData> dynamicBuffer4 = this.m_HaveCoordinatedMeetingDatas[this.m_PrefabRefData[meeting2].m_Prefab];
									if (value5.m_Status == MeetingStatus.Done)
									{
										continue;
									}
									HaveCoordinatedMeetingData haveCoordinatedMeetingData2 = dynamicBuffer4[value5.m_Phase];
									if (tripNeeded.m_Purpose == haveCoordinatedMeetingData2.m_TravelPurpose.m_Purpose && (haveCoordinatedMeetingData2.m_TravelPurpose.m_Resource == Resource.NoResource || haveCoordinatedMeetingData2.m_TravelPurpose.m_Resource == tripNeeded.m_Resource) && value5.m_Target == Entity.Null)
									{
										DynamicBuffer<CoordinatedMeetingAttendee> dynamicBuffer5 = this.m_Attendees[meeting2];
										if (dynamicBuffer5.Length <= 0 || !(dynamicBuffer5[0].m_Attendee == entity))
										{
											continue;
										}
										value5.m_Target = target.m_Target;
										this.m_Meetings[meeting2] = value5;
										residentFlags |= ResidentFlags.PreferredLeader;
									}
								}
							}
							if (this.m_Workers.HasComponent(entity))
							{
								Worker worker2 = this.m_Workers[entity];
								entity4 = ((!this.m_PropertyRenters.HasComponent(worker2.m_Workplace)) ? worker2.m_Workplace : this.m_PropertyRenters[worker2.m_Workplace].m_Property);
							}
							if (currentBuilding2.Equals(componentData11.m_Property) || currentBuilding2.Equals(entity4))
							{
								this.m_LeaveQueue.Enqueue(entity);
							}
							Entity entity5 = Entity.Null;
							if (nativeArray4.Length != 0)
							{
								entity5 = nativeArray4[i].m_CurrentTransport;
							}
							uint timer = 512u;
							Purpose divertPurpose = Purpose.None;
							bool pathFailed = !flag7 && dynamicBuffer3.Length == 0;
							bool hasDivertPath = false;
							this.GetResidentFlags(entity, currentBuilding2, isMailSender, pathFailed, ref target, ref tripNeeded.m_Purpose, ref divertPurpose, ref timer, ref hasDivertPath);
							if (this.m_UnderConstructionData.TryGetComponent(entity2, out var componentData12) && componentData12.m_NewPrefab == Entity.Null)
							{
								timer = math.max(timer, ObjectUtils.GetTripDelayFrames(componentData12, componentData));
							}
							if (this.m_PrefabRefData.HasComponent(entity5) && !this.m_Deleteds.HasComponent(entity5))
							{
								this.ResetTrip(unfilteredChunkIndex, entity5, entity, currentBuilding, target, residentFlags, divertPurpose, timer, hasDivertPath);
							}
							else
							{
								Citizen citizenData = nativeArray5[i];
								entity5 = this.SpawnResident(unfilteredChunkIndex, entity, currentBuilding, citizenData, target, residentFlags, divertPurpose, timer, hasDivertPath, flag3, flag7);
								this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new CurrentTransport(entity5));
							}
							if ((tripNeeded.m_Purpose != Purpose.GoingToWork && tripNeeded.m_Purpose != Purpose.GoingToSchool) || currentBuilding != componentData11.m_Property)
							{
								this.AddPetTargets(household, currentBuilding, target.m_Target);
							}
							this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new TravelPurpose
							{
								m_Data = tripNeeded.m_Data,
								m_Purpose = tripNeeded.m_Purpose,
								m_Resource = tripNeeded.m_Resource
							});
							this.m_CommandBuffer.RemoveComponent<CurrentBuilding>(unfilteredChunkIndex, entity);
						}
						else if ((this.m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0)
						{
							CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, unfilteredChunkIndex, household, MoveAwayReason.TripNeedNotMovedIn);
						}
						this.RemoveAllTrips(trips);
						this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
						this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, entity);
					}
					else
					{
						if (flag5 || this.m_HumanChunks.Length == 0)
						{
							continue;
						}
						if (!this.m_Transforms.HasComponent(currentBuilding))
						{
							this.RemoveAllTrips(trips);
						}
						else if (trips[0].m_TargetAgent != Entity.Null)
						{
							this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new Target
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
									this.RemoveAllTrips(trips);
									continue;
								}
								if (this.m_OutsideConnections.HasComponent(nativeArray3[i].m_CurrentBuilding))
								{
									this.RemoveAllTrips(trips);
									continue;
								}
							}
							this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
							this.m_CommandBuffer.SetComponent(unfilteredChunkIndex, entity, new PathInformation
							{
								m_State = PathFlags.Pending
							});
							CreatureData creatureData2;
							Entity entity6 = ObjectEmergeSystem.SelectResidentPrefab(citizen2, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out creatureData2, out randomSeed);
							HumanData humanData2 = default(HumanData);
							if (entity6 != Entity.Null)
							{
								humanData2 = this.m_PrefabHumanData[entity6];
							}
							Household household3 = this.m_Households[household];
							DynamicBuffer<HouseholdCitizen> dynamicBuffer6 = this.m_HouseholdCitizens[household];
							PathfindParameters parameters2 = new PathfindParameters
							{
								m_MaxSpeed = 277.77777f,
								m_WalkSpeed = humanData2.m_WalkSpeed,
								m_Weights = CitizenUtils.GetPathfindWeights(citizen2, household3, dynamicBuffer6.Length),
								m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(this.m_TimeOfDay)),
								m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
								m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost
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
								destination3.m_Entity = this.FindDistrict(currentBuilding);
								destination3.m_Type = SetupTargetType.Hospital;
								break;
							case Purpose.Safety:
							case Purpose.Escape:
								destination3.m_Type = SetupTargetType.Safety;
								break;
							case Purpose.EmergencyShelter:
								parameters2.m_Weights = new PathfindWeights(1f, 0f, 0f, 0f);
								destination3.m_Entity = this.FindDistrict(currentBuilding);
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
							if (this.m_PropertyRenters.TryGetComponent(household, out var componentData13))
							{
								parameters2.m_Authorization1 = componentData13.m_Property;
							}
							if (this.m_Workers.HasComponent(entity))
							{
								Worker worker3 = this.m_Workers[entity];
								if (this.m_PropertyRenters.HasComponent(worker3.m_Workplace))
								{
									parameters2.m_Authorization2 = this.m_PropertyRenters[worker3.m_Workplace].m_Property;
								}
								else
								{
									parameters2.m_Authorization2 = worker3.m_Workplace;
								}
							}
							if (this.m_CarKeepers.IsComponentEnabled(entity))
							{
								Entity car2 = this.m_CarKeepers[entity].m_Car;
								if (this.m_ParkedCarData.HasComponent(car2))
								{
									PrefabRef prefabRef2 = this.m_PrefabRefData[car2];
									ParkedCar parkedCar2 = this.m_ParkedCarData[car2];
									CarData carData2 = this.m_PrefabCarData[prefabRef2.m_Prefab];
									parameters2.m_MaxSpeed.x = carData2.m_MaxSpeed;
									parameters2.m_ParkingTarget = parkedCar2.m_Lane;
									parameters2.m_ParkingDelta = parkedCar2.m_CurvePosition;
									parameters2.m_ParkingSize = VehicleUtils.GetParkingSize(car2, ref this.m_PrefabRefData, ref this.m_ObjectGeometryData);
									parameters2.m_Methods |= VehicleUtils.GetPathMethods(carData2) | PathMethod.Parking;
									parameters2.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData2);
									if (this.m_PersonalCarData.TryGetComponent(car2, out var componentData14) && (componentData14.m_State & PersonalCarFlags.HomeTarget) == 0)
									{
										parameters2.m_PathfindFlags |= PathfindFlags.ParkingReset;
									}
								}
							}
							else if (this.m_BicycleOwners.IsComponentEnabled(entity))
							{
								Entity bicycle2 = this.m_BicycleOwners[entity].m_Bicycle;
								if (!this.m_PrefabRefData.TryGetComponent(bicycle2, out var componentData15) && currentBuilding == componentData13.m_Property)
								{
									Unity.Mathematics.Random random2 = citizen2.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
									componentData15.m_Prefab = this.m_PersonalCarSelectData.SelectVehiclePrefab(ref random2, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, out trailerPrefab);
								}
								if (this.m_PrefabCarData.TryGetComponent(componentData15.m_Prefab, out var componentData16) && this.m_ObjectGeometryData.TryGetComponent(componentData15.m_Prefab, out var componentData17))
								{
									parameters2.m_MaxSpeed.x = componentData16.m_MaxSpeed;
									parameters2.m_ParkingSize = VehicleUtils.GetParkingSize(componentData17, out offset);
									parameters2.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
									parameters2.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
									CurrentTransport value6;
									if (this.m_ParkedCarData.TryGetComponent(bicycle2, out var componentData18))
									{
										parameters2.m_ParkingTarget = componentData18.m_Lane;
										parameters2.m_ParkingDelta = componentData18.m_CurvePosition;
										if (this.m_PersonalCarData.TryGetComponent(bicycle2, out var componentData19) && (componentData19.m_State & PersonalCarFlags.HomeTarget) == 0)
										{
											parameters2.m_PathfindFlags |= PathfindFlags.ParkingReset;
										}
									}
									else if (!CollectionUtils.TryGet(nativeArray4, i, out value6) || !this.m_PrefabRefData.HasComponent(value6.m_CurrentTransport) || this.m_Deleteds.HasComponent(value6.m_CurrentTransport))
									{
										origin2.m_Methods |= PathMethod.Bicycle;
										origin2.m_RoadTypes |= RoadTypes.Bicycle;
									}
								}
							}
							SetupQueueItem value7 = new SetupQueueItem(entity, parameters2, origin2, destination3);
							this.m_PathQueue.Enqueue(value7);
							this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new Target
							{
								m_Target = Entity.Null
							});
						}
						else
						{
							this.RemoveAllTrips(trips);
						}
					}
				}
			}

			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}
		}

		private struct TypeHandle
		{
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<MailSender> __Game_Citizens_MailSender_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<CurrentTransport> __Game_Citizens_CurrentTransport_RO_ComponentTypeHandle;

			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RW_ComponentTypeHandle;

			public BufferTypeHandle<TripNeeded> __Game_Citizens_TripNeeded_RW_BufferTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<AttendingMeeting> __Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<CreatureData> __Game_Prefabs_CreatureData_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<ResidentData> __Game_Prefabs_ResidentData_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentLookup<ParkedCar> __Game_Vehicles_ParkedCar_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> __Game_Vehicles_PersonalCar_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Vehicles.Ambulance> __Game_Vehicles_Ambulance_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Net.ConnectionLane> __Game_Net_ConnectionLane_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<CurrentDistrict> __Game_Areas_CurrentDistrict_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<PathInformation> __Game_Pathfind_PathInformation_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Target> __Game_Common_Target_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Deleted> __Game_Common_Deleted_RO_ComponentLookup;

			[ReadOnly]
			public BufferLookup<PathElement> __Game_Pathfind_PathElement_RO_BufferLookup;

			[ReadOnly]
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<BicycleOwner> __Game_Citizens_BicycleOwner_RO_ComponentLookup;

			public ComponentLookup<Worker> __Game_Citizens_Worker_RW_ComponentLookup;

			public ComponentLookup<Game.Citizens.Student> __Game_Citizens_Student_RW_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<ObjectData> __Game_Prefabs_ObjectData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> __Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<CarData> __Game_Prefabs_CarData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<HumanData> __Game_Prefabs_HumanData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<UnderConstruction> __Game_Objects_UnderConstruction_RO_ComponentLookup;

			public ComponentLookup<CoordinatedMeeting> __Game_Citizens_CoordinatedMeeting_RW_ComponentLookup;

			[ReadOnly]
			public BufferLookup<CoordinatedMeetingAttendee> __Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup;

			[ReadOnly]
			public BufferLookup<HouseholdAnimal> __Game_Citizens_HouseholdAnimal_RO_BufferLookup;

			[ReadOnly]
			public ComponentLookup<TravelPurpose> __Game_Citizens_TravelPurpose_RO_ComponentLookup;

			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> __Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup;

			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			[ReadOnly]
			public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;

			[ReadOnly]
			public ComponentLookup<Criminal> __Game_Citizens_Criminal_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentLookup;

			public ComponentLookup<CitizenPresence> __Game_Buildings_CitizenPresence_RW_ComponentLookup;

			[ReadOnly]
			public ComponentTypeHandle<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentTypeHandle;

			[ReadOnly]
			public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

			[ReadOnly]
			public BufferTypeHandle<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferTypeHandle;

			public BufferTypeHandle<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferTypeHandle;

			[ReadOnly]
			public ComponentLookup<DeliveryTruckData> __Game_Prefabs_DeliveryTruckData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<TransportCompanyData> __Game_Companies_TransportCompanyData_RO_ComponentLookup;

			[ReadOnly]
			public ComponentLookup<ServiceRequest> __Game_Simulation_ServiceRequest_RO_ComponentLookup;

			[ReadOnly]
			public BufferLookup<ActivityLocationElement> __Game_Prefabs_ActivityLocationElement_RO_BufferLookup;

			[ReadOnly]
			public BufferLookup<Efficiency> __Game_Buildings_Efficiency_RO_BufferLookup;

			[ReadOnly]
			public BufferLookup<InstalledUpgrade> __Game_Buildings_InstalledUpgrade_RO_BufferLookup;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Citizen_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(isReadOnly: true);
				this.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HealthProblem>(isReadOnly: true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdMember>(isReadOnly: true);
				this.__Game_Citizens_MailSender_RO_ComponentTypeHandle = state.GetComponentTypeHandle<MailSender>(isReadOnly: true);
				this.__Game_Citizens_CurrentTransport_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentTransport>(isReadOnly: true);
				this.__Game_Citizens_CurrentBuilding_RW_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>();
				this.__Game_Citizens_TripNeeded_RW_BufferTypeHandle = state.GetBufferTypeHandle<TripNeeded>();
				this.__Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle = state.GetComponentTypeHandle<AttendingMeeting>(isReadOnly: true);
				this.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CreatureData>(isReadOnly: true);
				this.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ResidentData>(isReadOnly: true);
				this.__Game_Vehicles_ParkedCar_RO_ComponentLookup = state.GetComponentLookup<ParkedCar>(isReadOnly: true);
				this.__Game_Vehicles_PersonalCar_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PersonalCar>(isReadOnly: true);
				this.__Game_Vehicles_Ambulance_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.Ambulance>(isReadOnly: true);
				this.__Game_Net_ConnectionLane_RO_ComponentLookup = state.GetComponentLookup<Game.Net.ConnectionLane>(isReadOnly: true);
				this.__Game_Areas_CurrentDistrict_RO_ComponentLookup = state.GetComponentLookup<CurrentDistrict>(isReadOnly: true);
				this.__Game_Pathfind_PathInformation_RO_ComponentLookup = state.GetComponentLookup<PathInformation>(isReadOnly: true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(isReadOnly: true);
				this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(isReadOnly: true);
				this.__Game_Common_Target_RO_ComponentLookup = state.GetComponentLookup<Target>(isReadOnly: true);
				this.__Game_Common_Deleted_RO_ComponentLookup = state.GetComponentLookup<Deleted>(isReadOnly: true);
				this.__Game_Pathfind_PathElement_RO_BufferLookup = state.GetBufferLookup<PathElement>(isReadOnly: true);
				this.__Game_Citizens_CarKeeper_RO_ComponentLookup = state.GetComponentLookup<CarKeeper>(isReadOnly: true);
				this.__Game_Citizens_BicycleOwner_RO_ComponentLookup = state.GetComponentLookup<BicycleOwner>(isReadOnly: true);
				this.__Game_Citizens_Worker_RW_ComponentLookup = state.GetComponentLookup<Worker>();
				this.__Game_Citizens_Student_RW_ComponentLookup = state.GetComponentLookup<Game.Citizens.Student>();
				this.__Game_Prefabs_ObjectData_RO_ComponentLookup = state.GetComponentLookup<ObjectData>(isReadOnly: true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(isReadOnly: true);
				this.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup = state.GetComponentLookup<ObjectGeometryData>(isReadOnly: true);
				this.__Game_Prefabs_CarData_RO_ComponentLookup = state.GetComponentLookup<CarData>(isReadOnly: true);
				this.__Game_Prefabs_HumanData_RO_ComponentLookup = state.GetComponentLookup<HumanData>(isReadOnly: true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(isReadOnly: true);
				this.__Game_Objects_UnderConstruction_RO_ComponentLookup = state.GetComponentLookup<UnderConstruction>(isReadOnly: true);
				this.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup = state.GetComponentLookup<CoordinatedMeeting>();
				this.__Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup = state.GetBufferLookup<CoordinatedMeetingAttendee>(isReadOnly: true);
				this.__Game_Citizens_HouseholdAnimal_RO_BufferLookup = state.GetBufferLookup<HouseholdAnimal>(isReadOnly: true);
				this.__Game_Citizens_TravelPurpose_RO_ComponentLookup = state.GetComponentLookup<TravelPurpose>(isReadOnly: true);
				this.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup = state.GetBufferLookup<HaveCoordinatedMeetingData>(isReadOnly: true);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(isReadOnly: true);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(isReadOnly: true);
				this.__Game_Citizens_Criminal_RO_ComponentLookup = state.GetComponentLookup<Criminal>(isReadOnly: true);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentLookup = state.GetComponentLookup<CurrentBuilding>(isReadOnly: true);
				this.__Game_Buildings_CitizenPresence_RW_ComponentLookup = state.GetComponentLookup<CitizenPresence>();
				this.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PropertyRenter>(isReadOnly: true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
				this.__Game_Vehicles_OwnedVehicle_RO_BufferTypeHandle = state.GetBufferTypeHandle<OwnedVehicle>(isReadOnly: true);
				this.__Game_Economy_Resources_RW_BufferTypeHandle = state.GetBufferTypeHandle<Game.Economy.Resources>();
				this.__Game_Prefabs_DeliveryTruckData_RO_ComponentLookup = state.GetComponentLookup<DeliveryTruckData>(isReadOnly: true);
				this.__Game_Companies_TransportCompanyData_RO_ComponentLookup = state.GetComponentLookup<TransportCompanyData>(isReadOnly: true);
				this.__Game_Simulation_ServiceRequest_RO_ComponentLookup = state.GetComponentLookup<ServiceRequest>(isReadOnly: true);
				this.__Game_Prefabs_ActivityLocationElement_RO_BufferLookup = state.GetBufferLookup<ActivityLocationElement>(isReadOnly: true);
				this.__Game_Buildings_Efficiency_RO_BufferLookup = state.GetBufferLookup<Efficiency>(isReadOnly: true);
				this.__Game_Buildings_InstalledUpgrade_RO_BufferLookup = state.GetBufferLookup<InstalledUpgrade>(isReadOnly: true);
			}
		}

		private const int UPDATE_INTERVAL = 16;

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

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPathCostsCar;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPathCostsPublic;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPathCostsPedestrian;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPathCostsCarShort;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPathCostsPublicShort;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPathCostsPedestrianShort;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPublicTransportDuration;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugTaxiDuration;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPedestrianDuration;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugCarDuration;

		[DebugWatchValue]
		private DebugWatchDistribution m_DebugPedestrianDurationShort;

		private TypeHandle __TypeHandle;

		public bool debugDisableSpawning { get; set; }

		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_DebugPathCostsCar = new DebugWatchDistribution(persistent: true);
			this.m_DebugPathCostsPublic = new DebugWatchDistribution(persistent: true);
			this.m_DebugPathCostsPedestrian = new DebugWatchDistribution(persistent: true);
			this.m_DebugPathCostsCarShort = new DebugWatchDistribution(persistent: true);
			this.m_DebugPathCostsPublicShort = new DebugWatchDistribution(persistent: true);
			this.m_DebugPathCostsPedestrianShort = new DebugWatchDistribution(persistent: true);
			this.m_DebugPublicTransportDuration = new DebugWatchDistribution(persistent: true);
			this.m_DebugTaxiDuration = new DebugWatchDistribution(persistent: true);
			this.m_DebugPedestrianDuration = new DebugWatchDistribution(persistent: true);
			this.m_DebugCarDuration = new DebugWatchDistribution(persistent: true);
			this.m_DebugPedestrianDurationShort = new DebugWatchDistribution(persistent: true);
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_CityConfigurationSystem = base.World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			this.m_VehicleCapacitySystem = base.World.GetOrCreateSystemManaged<VehicleCapacitySystem>();
			this.m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
			this.m_PersonalCarSelectData = new PersonalCarSelectData(this);
			this.m_CitizenGroup = base.GetEntityQuery(ComponentType.ReadOnly<Citizen>(), ComponentType.ReadOnly<HouseholdMember>(), ComponentType.ReadWrite<TripNeeded>(), ComponentType.Exclude<TravelPurpose>(), ComponentType.ReadOnly<CurrentBuilding>(), ComponentType.Exclude<ResourceBuyer>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
			this.m_ResidentPrefabGroup = base.GetEntityQuery(ComponentType.ReadOnly<ObjectData>(), ComponentType.ReadOnly<HumanData>(), ComponentType.ReadOnly<ResidentData>(), ComponentType.ReadOnly<PrefabData>());
			this.m_CompanyGroup = base.GetEntityQuery(ComponentType.ReadWrite<TripNeeded>(), ComponentType.ReadOnly<PrefabRef>(), ComponentType.ReadWrite<Game.Economy.Resources>(), ComponentType.ReadOnly<OwnedVehicle>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
			this.m_CarPrefabQuery = base.GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());
			this.m_HandleRequestArchetype = base.EntityManager.CreateArchetype(ComponentType.ReadWrite<HandleRequest>(), ComponentType.ReadWrite<Game.Events.Event>());
			this.m_ResetTripArchetype = base.EntityManager.CreateArchetype(ComponentType.ReadWrite<Game.Common.Event>(), ComponentType.ReadWrite<ResetTrip>());
			this.m_HumanSpawnTypes = new ComponentTypeSet(ComponentType.ReadWrite<HumanCurrentLane>(), ComponentType.ReadWrite<TripSource>(), ComponentType.ReadWrite<Unspawned>());
			this.m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(), ComponentType.ReadWrite<PathElement>());
			this.m_CurrentLaneTypesRelative = new ComponentTypeSet(new ComponentType[5]
			{
				ComponentType.ReadWrite<Moving>(),
				ComponentType.ReadWrite<TransformFrame>(),
				ComponentType.ReadWrite<HumanNavigation>(),
				ComponentType.ReadWrite<HumanCurrentLane>(),
				ComponentType.ReadWrite<Blocker>()
			});
			this.m_PathfindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			base.RequireAnyForUpdate(this.m_CitizenGroup, this.m_CompanyGroup);
		}

		[Preserve]
		protected override void OnDestroy()
		{
			this.m_DebugPathCostsCar.Dispose();
			this.m_DebugPathCostsPublic.Dispose();
			this.m_DebugPathCostsPedestrian.Dispose();
			this.m_DebugPathCostsCarShort.Dispose();
			this.m_DebugPathCostsPublicShort.Dispose();
			this.m_DebugPathCostsPedestrianShort.Dispose();
			this.m_DebugPublicTransportDuration.Dispose();
			this.m_DebugTaxiDuration.Dispose();
			this.m_DebugCarDuration.Dispose();
			this.m_DebugPedestrianDuration.Dispose();
			this.m_DebugPedestrianDurationShort.Dispose();
			base.OnDestroy();
		}

		[Preserve]
		protected override void OnUpdate()
		{
			JobHandle outJobHandle;
			NativeList<ArchetypeChunk> humanChunks = this.m_ResidentPrefabGroup.ToArchetypeChunkListAsync(Allocator.TempJob, out outJobHandle);
			this.m_PersonalCarSelectData.PreUpdate(this, this.m_CityConfigurationSystem, this.m_CarPrefabQuery, Allocator.TempJob, out var jobHandle);
			JobHandle jobHandle2 = JobHandle.CombineDependencies(base.Dependency, outJobHandle, jobHandle);
			JobHandle jobHandle3 = default(JobHandle);
			if (!this.m_CitizenGroup.IsEmptyIgnoreFilter)
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
				JobHandle deps = default(JobHandle);
				if (this.m_DebugPathCostsCar.IsEnabled)
				{
					debugPathQueueCar = this.m_DebugPathCostsCar.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPathCostsPublic.IsEnabled)
				{
					debugPathQueuePublic = this.m_DebugPathCostsPublic.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPathCostsPedestrian.IsEnabled)
				{
					debugPathQueuePedestrian = this.m_DebugPathCostsPedestrian.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPathCostsCarShort.IsEnabled)
				{
					debugPathQueueCarShort = this.m_DebugPathCostsCarShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPathCostsPublicShort.IsEnabled)
				{
					debugPathQueuePublicShort = this.m_DebugPathCostsPublicShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPathCostsPedestrianShort.IsEnabled)
				{
					debugPathQueuePedestrianShort = this.m_DebugPathCostsPedestrianShort.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPublicTransportDuration.IsEnabled)
				{
					debugPublicTransportDuration = this.m_DebugPublicTransportDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugTaxiDuration.IsEnabled)
				{
					debugTaxiDuration = this.m_DebugTaxiDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugCarDuration.IsEnabled)
				{
					debugCarDuration = this.m_DebugCarDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPedestrianDuration.IsEnabled)
				{
					debugPedestrianDuration = this.m_DebugPedestrianDuration.GetQueue(clear: false, out deps);
					deps.Complete();
				}
				if (this.m_DebugPedestrianDurationShort.IsEnabled)
				{
					debugPedestrianDurationShort = this.m_DebugPedestrianDurationShort.GetQueue(clear: false, out deps);
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
					m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef),
					m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_HouseholdMemberType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_MailSenderType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_MailSender_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_CurrentTransportType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_CurrentTransport_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RW_ComponentTypeHandle, ref base.CheckedStateRef),
					m_TripNeededType = InternalCompilerInterface.GetBufferTypeHandle(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef),
					m_AttendingMeetingType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_CreatureDataType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_ResidentDataType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_ParkedCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_ParkedCar_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PersonalCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RO_ComponentLookup, ref base.CheckedStateRef),
					m_AmbulanceData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Vehicles_Ambulance_RO_ComponentLookup, ref base.CheckedStateRef),
					m_ConnectionLaneData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Net_ConnectionLane_RO_ComponentLookup, ref base.CheckedStateRef),
					m_CurrentDistrictData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Areas_CurrentDistrict_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PathInfos = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Pathfind_PathInformation_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Properties = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Transforms = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Targets = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Target_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Deleteds = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Common_Deleted_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PathElements = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Pathfind_PathElement_RO_BufferLookup, ref base.CheckedStateRef),
					m_CarKeepers = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentLookup, ref base.CheckedStateRef),
					m_BicycleOwners = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_BicycleOwner_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PropertyRenters = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Workers = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Worker_RW_ComponentLookup, ref base.CheckedStateRef),
					m_Students = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Student_RW_ComponentLookup, ref base.CheckedStateRef),
					m_ObjectDatas = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_ObjectData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabRefData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef),
					m_ObjectGeometryData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabCarData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_CarData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabHumanData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_HumanData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_OutsideConnections = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef),
					m_UnderConstructionData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_UnderConstruction_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Meetings = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup, ref base.CheckedStateRef),
					m_Attendees = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup, ref base.CheckedStateRef),
					m_HouseholdAnimals = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdAnimal_RO_BufferLookup, ref base.CheckedStateRef),
					m_TravelPurposes = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_TravelPurpose_RO_ComponentLookup, ref base.CheckedStateRef),
					m_HaveCoordinatedMeetingDatas = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup, ref base.CheckedStateRef),
					m_Households = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef),
					m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef),
					m_CriminalData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_Criminal_RO_ComponentLookup, ref base.CheckedStateRef),
					m_HumanChunks = humanChunks,
					m_RandomSeed = RandomSeed.Next(),
					m_TimeOfDay = this.m_TimeSystem.normalizedTime,
					m_ResetTripArchetype = this.m_ResetTripArchetype,
					m_HumanSpawnTypes = this.m_HumanSpawnTypes,
					m_PathfindTypes = this.m_PathfindTypes,
					m_PersonalCarSelectData = this.m_PersonalCarSelectData,
					m_PathQueue = this.m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter(),
					m_AnimalQueue = animalQueue.AsParallelWriter(),
					m_LeaveQueue = leaveQueue.AsParallelWriter(),
					m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
					m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer().AsParallelWriter(),
					m_DebugDisableSpawning = this.debugDisableSpawning
				};
				PetTargetJob jobData2 = new PetTargetJob
				{
					m_CurrentBuildingData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef),
					m_AnimalQueue = animalQueue,
					m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer()
				};
				CitizeLeaveJob jobData3 = new CitizeLeaveJob
				{
					m_CurrentBuildingData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef),
					m_CitizenPresenceData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Buildings_CitizenPresence_RW_ComponentLookup, ref base.CheckedStateRef),
					m_LeaveQueue = leaveQueue
				};
				jobHandle2 = JobChunkExtensions.ScheduleParallel(jobData, this.m_CitizenGroup, jobHandle2);
				JobHandle jobHandle4 = IJobExtensions.Schedule(jobData2, jobHandle2);
				JobHandle jobHandle5 = IJobExtensions.Schedule(jobData3, jobHandle2);
				jobHandle3 = JobHandle.CombineDependencies(jobHandle4, jobHandle5);
				animalQueue.Dispose(jobHandle4);
				leaveQueue.Dispose(jobHandle5);
				this.m_PathfindSetupSystem.AddQueueWriter(jobHandle2);
				this.m_TriggerSystem.AddActionBufferWriter(jobHandle2);
				this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle3);
			}
			this.m_PersonalCarSelectData.PostUpdate(jobHandle2);
			if (!this.m_CompanyGroup.IsEmptyIgnoreFilter)
			{
				jobHandle2 = JobChunkExtensions.ScheduleParallel(new CompanyJob
				{
					m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef),
					m_PropertyRenterType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_CreatureDataType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_ResidentDataType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_PrefabType = InternalCompilerInterface.GetComponentTypeHandle(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle, ref base.CheckedStateRef),
					m_TripNeededType = InternalCompilerInterface.GetBufferTypeHandle(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef),
					m_VehicleType = InternalCompilerInterface.GetBufferTypeHandle(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferTypeHandle, ref base.CheckedStateRef),
					m_ResourceType = InternalCompilerInterface.GetBufferTypeHandle(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferTypeHandle, ref base.CheckedStateRef),
					m_PrefabDeliveryTruckData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_DeliveryTruckData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PrefabObjectData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_ObjectData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Prefabs = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef),
					m_Transforms = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef),
					m_TransportCompanyDatas = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Companies_TransportCompanyData_RO_ComponentLookup, ref base.CheckedStateRef),
					m_ServiceRequestData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Simulation_ServiceRequest_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PathInformationData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Pathfind_PathInformation_RO_ComponentLookup, ref base.CheckedStateRef),
					m_UnderConstructionData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Objects_UnderConstruction_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PropertyRenterData = InternalCompilerInterface.GetComponentLookup(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef),
					m_PathElements = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Pathfind_PathElement_RO_BufferLookup, ref base.CheckedStateRef),
					m_ActivityLocationElements = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Prefabs_ActivityLocationElement_RO_BufferLookup, ref base.CheckedStateRef),
					m_EfficiencyBufs = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferLookup, ref base.CheckedStateRef),
					m_InstalledUpgradeBufs = InternalCompilerInterface.GetBufferLookup(ref this.__TypeHandle.__Game_Buildings_InstalledUpgrade_RO_BufferLookup, ref base.CheckedStateRef),
					m_HumanChunks = humanChunks,
					m_LeftHandTraffic = this.m_CityConfigurationSystem.leftHandTraffic,
					m_RandomSeed = RandomSeed.Next(),
					m_HandleRequestArchetype = this.m_HandleRequestArchetype,
					m_DeliveryTruckSelectData = this.m_VehicleCapacitySystem.GetDeliveryTruckSelectData(),
					m_CurrentLaneTypesRelative = this.m_CurrentLaneTypesRelative,
					m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
					m_DebugDisableSpawning = this.debugDisableSpawning
				}, this.m_CompanyGroup, jobHandle2);
				this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
				jobHandle3 = JobHandle.CombineDependencies(jobHandle3, jobHandle2);
			}
			humanChunks.Dispose(jobHandle2);
			base.Dependency = jobHandle3;
		}

	}
}
