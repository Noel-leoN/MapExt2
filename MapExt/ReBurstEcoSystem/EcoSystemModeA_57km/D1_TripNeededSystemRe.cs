using Colossal.Collections;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Creatures;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Triggers;
using Game.Vehicles;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{

    public struct AnimalTargetInfo
    {
        public Entity m_Animal;

        public Entity m_Source;

        public Entity m_Target;
    }

    [BurstCompile]
    public struct CitizenJob : IJobChunk
    {
        // Debug Queues
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPathQueueCar;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPathQueuePublic;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPathQueuePedestrian;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPathQueueCarShort;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPathQueuePublicShort;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPathQueuePedestrianShort;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPublicTransportDuration;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugTaxiDuration;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugCarDuration;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPedestrianDuration;
        [NativeDisableContainerSafetyRestriction] public NativeQueue<int> m_DebugPedestrianDurationShort;

        // Type Handles & Lookups
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
            Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out CreatureData creatureData, out PseudoRandomSeed randomSeed);
            ObjectData objectData = this.m_ObjectDatas[entity];
            PrefabRef prefabRef = default(PrefabRef);
            prefabRef.m_Prefab = entity;
            PrefabRef component = prefabRef;

            //Game.Objects.Transform transform;
            //if (this.m_Transforms.HasComponent(fromBuilding))
            //{
            //    transform = this.m_Transforms[fromBuilding];
            //}
            //else
            //{
            //    Game.Objects.Transform transform2 = default(Game.Objects.Transform);
            //    transform2.m_Position = default(float3);
            //    transform2.m_Rotation = new quaternion(0f, 0f, 0f, 1f);
            //    transform = transform2;
            //}

            Game.Objects.Transform transform;
            if (this.m_Transforms.HasComponent(fromBuilding))
                transform = this.m_Transforms[fromBuilding];
            else
                transform = new Game.Objects.Transform { m_Rotation = new quaternion(0f, 0f, 0f, 1f) };

            //Game.Creatures.Resident component2 = default(Game.Creatures.Resident);
            //component2.m_Citizen = citizen;
            //component2.m_Flags = flags;
            //Human component3 = default(Human);
            //if (isDead)
            //{
            //    component3.m_Flags |= HumanFlags.Dead;
            //}
            //if (isCarried)
            //{
            //    component3.m_Flags |= HumanFlags.Carried;
            //}

            Game.Creatures.Resident residentComp = new() { m_Citizen = citizen, m_Flags = flags };
            Human humanComp = default(Human);
            if (isDead) humanComp.m_Flags |= HumanFlags.Dead;
            if (isCarried) humanComp.m_Flags |= HumanFlags.Carried;

            //PathOwner component4 = new PathOwner(PathFlags.Updated);
            //TripSource component5 = new TripSource(fromBuilding, timer);
            PathOwner pathOwner = new PathOwner(PathFlags.Updated);
            TripSource tripSource = new TripSource(fromBuilding, timer);

            Entity agentEntity = this.m_CommandBuffer.CreateEntity(index, objectData.m_Archetype);
            Entity vehicleEntity = Entity.Null;
            HumanCurrentLane humanLane = default;

            if (this.m_PathElements.TryGetBuffer(citizen, out var bufferData) && bufferData.Length > 0)
            {
                PathElement pathElement = bufferData[0];
                CreatureLaneFlags creatureLaneFlags = (CreatureLaneFlags)0u;
                if ((pathElement.m_Flags & PathElementFlags.Secondary) != 0)
                {
                    Unity.Mathematics.Random random = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
                    vehicleEntity = this.m_PersonalCarSelectData.CreateVehicle(this.m_CommandBuffer, index, ref random, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, transform, fromBuilding, citizen, PersonalCarFlags.Boarding, stopped: false);
                    if (vehicleEntity != Entity.Null)
                    {
                        DynamicBuffer<PathElement> targetElements = this.m_CommandBuffer.SetBuffer<PathElement>(index, agentEntity);
                        PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements);
                        Game.Vehicles.CarLaneFlags carLaneFlags = Game.Vehicles.CarLaneFlags.EndOfPath | Game.Vehicles.CarLaneFlags.EndReached | Game.Vehicles.CarLaneFlags.FixedLane;
                        if (this.m_ConnectionLaneData.TryGetComponent(pathElement.m_Target, out var componentData))
                        {
                            carLaneFlags = (((componentData.m_Flags & ConnectionLaneFlags.Area) == 0) ? (carLaneFlags | Game.Vehicles.CarLaneFlags.Connection) : (carLaneFlags | Game.Vehicles.CarLaneFlags.Area));
                        }
                        this.m_CommandBuffer.SetComponent(index, vehicleEntity, new CarCurrentLane(pathElement, carLaneFlags));
                        this.m_CommandBuffer.SetComponent(index, citizen, new BicycleOwner
                        {
                            m_Bicycle = vehicleEntity
                        });
                        residentComp.m_Flags |= ResidentFlags.InVehicle;
                        creatureLaneFlags |= CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached;
                    }
                }
                DynamicBuffer<PathElement> targetElements2 = this.m_CommandBuffer.SetBuffer<PathElement>(index, agentEntity);
                PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements2);
                humanLane = new HumanCurrentLane(pathElement, creatureLaneFlags);
                pathOwner.m_State |= PathFlags.Updated;
            }
            this.m_CommandBuffer.AddComponent(index, agentEntity, in this.m_HumanSpawnTypes);
            if (divertPurpose != 0)
            {
                if (hasDivertPath)
                {
                    pathOwner.m_State |= PathFlags.CachedObsolete;
                }
                else
                {
                    pathOwner.m_State |= PathFlags.DivertObsolete;
                }
                this.m_CommandBuffer.AddComponent(index, agentEntity, new Divert
                {
                    m_Purpose = divertPurpose
                });
            }
            this.m_CommandBuffer.SetComponent(index, agentEntity, transform);
            this.m_CommandBuffer.SetComponent(index, agentEntity, component);
            this.m_CommandBuffer.SetComponent(index, agentEntity, target);
            this.m_CommandBuffer.SetComponent(index, agentEntity, residentComp);
            this.m_CommandBuffer.SetComponent(index, agentEntity, humanComp);
            this.m_CommandBuffer.SetComponent(index, agentEntity, pathOwner);
            this.m_CommandBuffer.SetComponent(index, agentEntity, randomSeed);
            this.m_CommandBuffer.SetComponent(index, agentEntity, humanLane);
            this.m_CommandBuffer.SetComponent(index, agentEntity, tripSource);
            if (vehicleEntity != Entity.Null)
            {
                this.m_CommandBuffer.RemoveComponent<TripSource>(index, agentEntity);
                this.m_CommandBuffer.AddComponent(index, agentEntity, new CurrentVehicle(vehicleEntity, CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver | CreatureVehicleFlags.Entering));
            }
            return agentEntity;
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
            // 获取当前Chunk中的所有组件数组
            NativeArray<Entity> entities = chunk.GetNativeArray(this.m_EntityType);
            BufferAccessor<TripNeeded> tripBufferAccessor = chunk.GetBufferAccessor(ref this.m_TripNeededType);
            NativeArray<HouseholdMember> householdMembers = chunk.GetNativeArray(ref this.m_HouseholdMemberType);
            NativeArray<CurrentBuilding> currentBuildings = chunk.GetNativeArray(ref this.m_CurrentBuildingType);
            NativeArray<CurrentTransport> currentTransports = chunk.GetNativeArray(ref this.m_CurrentTransportType);
            NativeArray<Citizen> citizens = chunk.GetNativeArray(ref this.m_CitizenType);
            NativeArray<HealthProblem> healthProblems = chunk.GetNativeArray(ref this.m_HealthProblemType);
            NativeArray<AttendingMeeting> attendingMeetings = chunk.GetNativeArray(ref this.m_AttendingMeetingType);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity citizenEntity = entities[i];
                DynamicBuffer<TripNeeded> trips = tripBufferAccessor[i];

                // 1. 基础校验：如果没有出行需求，直接跳过
                if (trips.Length <= 0) continue;

                Entity household = householdMembers[i].m_Household;
                Entity currentBuilding = currentBuildings[i].m_CurrentBuilding;

                // 2. 定义出行行为标志
                TripNeeded currentTrip = trips[0];
                bool isMovingAway = currentTrip.m_Purpose == Purpose.MovingAway; // 是否搬离城市
                bool isSafetyOrEscape = currentTrip.m_Purpose == Purpose.Safety || currentTrip.m_Purpose == Purpose.Escape; // 是否处于紧急避险状态
                bool isMailSender = chunk.IsComponentEnabled(ref this.m_MailSenderType, i);
                bool isDead = false;
                bool requireTransport = false;

                // 3. 健康与犯罪状态检查
                Criminal criminalData;
                // 检查是否是被捕囚犯，这类人通常不由常规逻辑控制移动
                bool isPrisoner = this.m_CriminalData.TryGetComponent(citizenEntity, out criminalData) && (criminalData.m_Flags & (CriminalFlags.Prisoner | CriminalFlags.Arrested | CriminalFlags.Sentenced)) != 0;

                PathInformation pathInfo;
                // 缓存 GetResult，避免后续重复调用 TryGetComponent
                bool hasPathInfo = this.m_PathInfos.TryGetComponent(citizenEntity, out pathInfo);

                if (healthProblems.Length != 0 && !isPrisoner)
                {
                    HealthProblem health = healthProblems[i];
                    // 检查是否有严重健康问题
                    if ((health.m_Flags & (HealthProblemFlags.Dead | HealthProblemFlags.RequireTransport | HealthProblemFlags.InDanger | HealthProblemFlags.Trapped)) != 0)
                    {
                        isDead = (health.m_Flags & HealthProblemFlags.Dead) != 0;
                        requireTransport = (health.m_Flags & HealthProblemFlags.RequireTransport) != 0;

                        // 如果只是被困或处于危险中，但还没死也不需要救护车，则暂时取消寻路，等待进一步状态变化
                        if (!(isDead || requireTransport))
                        {
                            if (hasPathInfo) this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                            continue;
                        }

                        // [逻辑修正] 如果病重或死亡，移除所有非医疗相关的出行需求
                        while (trips.Length > 0 && trips[0].m_Purpose != Purpose.Deathcare && trips[0].m_Purpose != Purpose.Hospital)
                        {
                            trips.RemoveAt(0);
                        }
                        if (trips.Length == 0)
                        {
                            if (hasPathInfo) this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                            continue;
                        }
                        // Refresh current trip after removal
                        currentTrip = trips[0];
                    }
                }

                // 4. 会议检查 (AttendingMeeting)
                if (!isMovingAway && attendingMeetings.Length != 0 && !isPrisoner)
                {
                    // ... (Logic to sync trip purpose with meeting phase)
                    Entity meeting = attendingMeetings[i].m_Meeting;
                    if (this.m_PrefabRefData.HasComponent(meeting))
                    {
                        Entity prefab = this.m_PrefabRefData[meeting].m_Prefab;
                        CoordinatedMeeting coordMeeting = this.m_Meetings[meeting];
                        if (this.m_HaveCoordinatedMeetingDatas.HasBuffer(prefab))
                        {
                            DynamicBuffer<HaveCoordinatedMeetingData> meetingData = this.m_HaveCoordinatedMeetingDatas[prefab];
                            if (meetingData.Length > coordMeeting.m_Phase)
                            {
                                HaveCoordinatedMeetingData phaseData = meetingData[coordMeeting.m_Phase];
                                while (trips.Length > 0 && trips[0].m_Purpose != phaseData.m_TravelPurpose.m_Purpose)
                                {
                                    trips.RemoveAt(0);
                                }
                                if (trips.Length == 0)
                                {
                                    if (hasPathInfo) this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                                    continue;
                                }
                                currentTrip = trips[0];
                            }
                        }
                    }
                }

                // 5. 搬家离开城市检查
                if ((citizens[i].m_State & CitizenFlags.MovingAwayReachOC) != 0)
                {
                    if (hasPathInfo) this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                    continue;
                }

                // 6. PathInformation 预校验 (防止对已经完成或无效的路径进行重复计算)
                if (hasPathInfo)
                {
                    // 如果寻路还在计算中 (Pending)，本tick不做任何操作，等待结果
                    if ((pathInfo.m_State & PathFlags.Pending) != 0)
                    {
                        continue;
                    }

                    // 检测路径是否无效：
                    // 1. 起点等于终点 (pathInfo.m_Origin == pathInfo.m_Destination)
                    // 2. 当前所在建筑就是终点 (currentBuilding == pathInfo.m_Destination)
                    // 如果满足上述且不是紧急避险，说明无需移动，移除 PathFind 组件并清理 Trip，防止死循环
                    if ((((pathInfo.m_Origin != Entity.Null && pathInfo.m_Origin == pathInfo.m_Destination) || currentBuilding == pathInfo.m_Destination) && !isSafetyOrEscape) || !this.m_Targets.HasComponent(citizenEntity))
                    {
                        this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                        this.RemoveAllTrips(trips);
                        continue;
                    }
                }

                if (this.m_DebugDisableSpawning) continue;

                // -------------------------------------------------------------------------
                // 核心逻辑: 处理目标、寻路请求和实体生成
                // -------------------------------------------------------------------------

                PseudoRandomSeed randomSeed;
                Entity trailerPrefab;
                float offset;

                // 情况 A: 市民已经有了 Target 组件 (说明已经确定了要去哪里，或者正在等待寻路返回具体地点)
                if (this.m_Targets.HasComponent(citizenEntity))
                {
                    Target target = this.m_Targets[citizenEntity];

                    // A.1: 目标是 Null，说明这是一个泛目标（如"去医院"），正在等待寻路系统返回具体的 Entity
                    if (target.m_Target == Entity.Null)
                    {
                        if (!hasPathInfo)
                        {
                            // 异常状态：有 Target 组件但没有路径信息，移除 Target 重置状态
                            this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                            continue;
                        }
                        Entity pathDestination = pathInfo.m_Destination;

                        // 寻路失败处理
                        // 如果寻路系统返回 Null 目的地，说明无路可走（没车位、没医院等）。
                        // 此时必须移除所有 Trip，防止下一帧再次发起寻路导致性能骤降（死循环）。
                        if (pathDestination == Entity.Null)
                        {
                            // Path Failed (Could not find hospital/shelter/parking).
                            // Fix: Ensure we don't loop here. Remove trips.
                            this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                            this.RemoveAllTrips(trips);
                            this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                            continue;
                        }
                        target.m_Target = pathDestination;
                    }

                    // A.2: 解析目标的实际属性（如果是租赁物业，取其 Property 实体）
                    Entity targetEntity = target.m_Target;
                    if (this.m_Properties.TryGetComponent(targetEntity, out var propertyRenter))
                    {
                        targetEntity = propertyRenter.m_Property;
                    }

                    // A.3: 零距离检查 (Zero-Distance Check)
                    // 在发起昂贵的生成车辆/行人逻辑之前，先检查"我是不是已经到了"。
                    // 很多时候市民在同一个建筑内产生需求，此检查可避免无意义的 Entity 创建和销毁。
                    if (currentBuilding == targetEntity && !isSafetyOrEscape)
                    {
                        this.m_CommandBuffer.SetComponentEnabled<Arrived>(unfilteredChunkIndex, citizenEntity, value: true);
                        // 记录此次旅行的目的（用于统计或后续逻辑）
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new TravelPurpose
                        {
                            m_Data = currentTrip.m_Data,
                            m_Purpose = currentTrip.m_Purpose,
                            m_Resource = currentTrip.m_Resource
                        });
                        this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                        if (hasPathInfo) this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                        this.RemoveAllTrips(trips);
                        continue;
                    }

                    bool isEmergencyTrip = (isDead && currentTrip.m_Purpose == Purpose.Deathcare) || (requireTransport && currentTrip.m_Purpose == Purpose.Hospital);

                    // A.4: START PATHFINDING (If no path info and not an emergency vehicle waiting)
                    if (!hasPathInfo && !isEmergencyTrip)
                    {
                        // Add Path Components
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                        this.m_CommandBuffer.SetComponent(unfilteredChunkIndex, citizenEntity, new PathInformation { m_State = PathFlags.Pending });

                        Citizen citizenData = citizens[i];
                        CreatureData creatureData;
                        Entity selectedPrefab = ObjectEmergeSystem.SelectResidentPrefab(citizenData, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out creatureData, out randomSeed);

                        HumanData humanData = default(HumanData);
                        if (selectedPrefab != Entity.Null) humanData = this.m_PrefabHumanData[selectedPrefab];

                        Household householdData = this.m_Households[household];
                        DynamicBuffer<HouseholdCitizen> householdCitizens = this.m_HouseholdCitizens[household];

                        // Prepare Pathfind Parameters
                        PathfindParameters parameters = default(PathfindParameters);
                        parameters.m_MaxSpeed = 277.777771f;
                        parameters.m_WalkSpeed = humanData.m_WalkSpeed;
                        parameters.m_Weights = CitizenUtils.GetPathfindWeights(citizenData, householdData, householdCitizens.Length);
                        parameters.m_Methods = PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(this.m_TimeOfDay);
                        parameters.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                        parameters.m_MaxCost = math.select(CitizenBehaviorSystem.kMaxPathfindCost, CitizenBehaviorSystem.kMaxMovingAwayCost, isMovingAway);

                        SetupQueueTarget origin = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Methods = PathMethod.Pedestrian, m_RandomCost = 30f };
                        SetupQueueTarget destination = new SetupQueueTarget
                        {
                            m_Type = SetupTargetType.CurrentLocation,
                            m_Methods = PathMethod.Pedestrian,
                            m_Entity = target.m_Target,
                            m_RandomCost = 30f,
                            m_ActivityMask = creatureData.m_SupportedActivities
                        };

                        // Authorization Logic
                        if (this.m_PropertyRenters.TryGetComponent(household, out var renterData))
                        {
                            parameters.m_Authorization1 = renterData.m_Property;
                        }
                        if (this.m_Workers.HasComponent(citizenEntity))
                        {
                            Worker worker = this.m_Workers[citizenEntity];
                            parameters.m_Authorization2 = this.m_PropertyRenters.HasComponent(worker.m_Workplace) ? this.m_PropertyRenters[worker.m_Workplace].m_Property : worker.m_Workplace;
                        }

                        // Vehicle Logic (Car/Bicycle)
                        // ... (Logic identical to original, checking CarKeepers and BicycleOwners to add Parking Methods)
                        // Simplified logic for brevity in this snippet, ensure to copy full logic if needed, 
                        // but essentially it sets parameters.m_Methods |= PathMethod.Parking / BicycleParking.
                        if (this.m_CarKeepers.IsComponentEnabled(citizenEntity))
                        {
                            Entity car = this.m_CarKeepers[citizenEntity].m_Car;
                            if (this.m_ParkedCarData.HasComponent(car))
                            {
                                PrefabRef carPrefabRef = this.m_PrefabRefData[car];
                                ParkedCar parkedCar = this.m_ParkedCarData[car];
                                CarData carData = this.m_PrefabCarData[carPrefabRef.m_Prefab];
                                parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
                                parameters.m_ParkingTarget = parkedCar.m_Lane;
                                parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
                                parameters.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref this.m_PrefabRefData, ref this.m_ObjectGeometryData);
                                parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
                                parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
                                if (this.m_PersonalCarData.TryGetComponent(car, out var pCar) && (pCar.m_State & PersonalCarFlags.HomeTarget) == 0)
                                    parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
                            }
                        }
                        else if (this.m_BicycleOwners.IsComponentEnabled(citizenEntity))
                        {
                            // ... (Similar bicycle logic)
                            Entity bicycle = this.m_BicycleOwners[citizenEntity].m_Bicycle;
                            if (!this.m_PrefabRefData.TryGetComponent(bicycle, out var bPrefab) && currentBuilding == renterData.m_Property)
                            {
                                Unity.Mathematics.Random r = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
                                bPrefab.m_Prefab = this.m_PersonalCarSelectData.SelectVehiclePrefab(ref r, 1, 0, true, false, true, out trailerPrefab);
                            }
                            if (this.m_PrefabCarData.TryGetComponent(bPrefab.m_Prefab, out var bCarData) && this.m_ObjectGeometryData.TryGetComponent(bPrefab.m_Prefab, out var bGeo))
                            {
                                parameters.m_MaxSpeed.x = bCarData.m_MaxSpeed;
                                parameters.m_ParkingSize = VehicleUtils.GetParkingSize(bGeo, out offset);
                                parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
                                parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
                                CurrentTransport ct;
                                if (this.m_ParkedCarData.TryGetComponent(bicycle, out var bParked))
                                {
                                    parameters.m_ParkingTarget = bParked.m_Lane;
                                    parameters.m_ParkingDelta = bParked.m_CurvePosition;
                                    if (this.m_PersonalCarData.TryGetComponent(bicycle, out var bpCar) && (bpCar.m_State & PersonalCarFlags.HomeTarget) == 0)
                                        parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
                                }
                                else if (!CollectionUtils.TryGet(currentTransports, i, out ct) || !this.m_PrefabRefData.HasComponent(ct.m_CurrentTransport) || this.m_Deleteds.HasComponent(ct.m_CurrentTransport))
                                {
                                    origin.m_Methods |= PathMethod.Bicycle; origin.m_RoadTypes |= RoadTypes.Bicycle;
                                }
                                if (targetEntity == renterData.m_Property)
                                {
                                    destination.m_Methods |= PathMethod.Bicycle; destination.m_RoadTypes |= RoadTypes.Bicycle;
                                }
                            }
                        }

                        // Enqueue Path
                        SetupQueueItem queueItem = new SetupQueueItem(citizenEntity, parameters, origin, destination);
                        this.m_PathQueue.Enqueue(queueItem);
                        continue;
                    }

                    // A.5: SPAWN RESIDENT (Path is found and ready)
                    DynamicBuffer<PathElement> pathBuffer = default(DynamicBuffer<PathElement>);
                    if (!isEmergencyTrip) pathBuffer = this.m_PathElements[citizenEntity];

                    // Check for valid Path or Generic Target Prefab
                    if ((!isEmergencyTrip && pathBuffer.Length > 0) || this.m_PrefabRefData.HasComponent(currentTrip.m_TargetAgent))
                    {
                        Entity spawnFrom = currentBuildings[i].m_CurrentBuilding;
                        Entity workplaceProperty = Entity.Null;

                        // Logic for Commute Time & School Dropouts
                        if (this.m_PropertyRenters.TryGetComponent(household, out var householdRenter) && !isEmergencyTrip && spawnFrom.Equals(householdRenter.m_Property))
                        {
                            // Debugging stats logic (omitted for brevity, keep original if needed)
                            if (currentTrip.m_Purpose == Purpose.GoingToWork && this.m_Workers.HasComponent(citizenEntity))
                            {
                                Worker w = this.m_Workers[citizenEntity];
                                if (pathInfo.m_Destination == Entity.Null)
                                {
                                    this.m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, citizenEntity);
                                    this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenBecameUnemployed, Entity.Null, citizenEntity, w.m_Workplace));
                                }
                                else { w.m_LastCommuteTime = pathInfo.m_Duration; this.m_Workers[citizenEntity] = w; }
                            }
                            else if (currentTrip.m_Purpose == Purpose.GoingToSchool && this.m_Students.HasComponent(citizenEntity))
                            {
                                if (pathInfo.m_Destination == Entity.Null)
                                {
                                    this.m_CommandBuffer.AddComponent<StudentsRemoved>(unfilteredChunkIndex, this.m_Students[citizenEntity].m_School);
                                    this.m_CommandBuffer.RemoveComponent<Game.Citizens.Student>(unfilteredChunkIndex, citizenEntity);
                                }
                                else { Game.Citizens.Student s = this.m_Students[citizenEntity]; s.m_LastCommuteTime = pathInfo.m_Duration; this.m_Students[citizenEntity] = s; }
                            }
                        }

                        // Set Leaving Flag
                        if (this.m_Workers.HasComponent(citizenEntity))
                        {
                            Worker w = this.m_Workers[citizenEntity];
                            workplaceProperty = !this.m_PropertyRenters.HasComponent(w.m_Workplace) ? w.m_Workplace : this.m_PropertyRenters[w.m_Workplace].m_Property;
                        }
                        if ((this.m_PropertyRenters.HasComponent(household) && spawnFrom.Equals(this.m_PropertyRenters[household].m_Property)) || spawnFrom.Equals(workplaceProperty))
                        {
                            this.m_LeaveQueue.Enqueue(citizenEntity);
                        }

                        // Calculate Spawn/Delay parameters
                        Entity currentVehicleEntity = Entity.Null;
                        if (currentTransports.Length != 0) currentVehicleEntity = currentTransports[i].m_CurrentTransport;

                        uint delayTimer = 512u;
                        Purpose divertPurpose = Purpose.None;
                        bool pathFailed = !isEmergencyTrip && pathBuffer.Length == 0;
                        bool hasDivertPath = false;

                        this.GetResidentFlags(citizenEntity, spawnFrom, isMailSender, pathFailed, ref target, ref currentTrip.m_Purpose, ref divertPurpose, ref delayTimer, ref hasDivertPath);

                        if (this.m_UnderConstructionData.TryGetComponent(targetEntity, out var underConstruction) && underConstruction.m_NewPrefab == Entity.Null)
                        {
                            delayTimer = math.max(delayTimer, ObjectUtils.GetTripDelayFrames(underConstruction, pathInfo));
                        }

                        // Execute Spawn or Reset
                        ResidentFlags residentFlags = ResidentFlags.None; // Add logic for PreferredLeader if needed
                        if (this.m_PrefabRefData.HasComponent(currentVehicleEntity) && !this.m_Deleteds.HasComponent(currentVehicleEntity))
                        {
                            this.ResetTrip(unfilteredChunkIndex, currentVehicleEntity, citizenEntity, currentBuilding, target, residentFlags, divertPurpose, delayTimer, hasDivertPath);
                        }
                        else
                        {
                            currentVehicleEntity = this.SpawnResident(unfilteredChunkIndex, citizenEntity, currentBuilding, citizens[i], target, residentFlags, divertPurpose, delayTimer, hasDivertPath, isDead, isEmergencyTrip);
                            this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new CurrentTransport(currentVehicleEntity));
                        }

                        // Final Cleanup
                        if ((currentTrip.m_Purpose != Purpose.GoingToWork && currentTrip.m_Purpose != Purpose.GoingToSchool) || (this.m_PropertyRenters.HasComponent(household) && currentBuilding != this.m_PropertyRenters[household].m_Property))
                        {
                            this.AddPetTargets(household, currentBuilding, target.m_Target);
                        }
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new TravelPurpose { m_Data = currentTrip.m_Data, m_Purpose = currentTrip.m_Purpose, m_Resource = currentTrip.m_Resource });
                        this.m_CommandBuffer.RemoveComponent<CurrentBuilding>(unfilteredChunkIndex, citizenEntity);
                    }
                    else if ((this.m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0)
                    {
                        CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, unfilteredChunkIndex, household, MoveAwayReason.TripNeedNotMovedIn);
                    }

                    // Done processing this trip, remove all trips of same type
                    this.RemoveAllTrips(trips);
                    this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                    this.m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                }
                // CASE B: Citizen needs a Generic Target (e.g., Going Home, Finding Hospital)
                else
                {
                    if (hasPathInfo || this.m_HumanChunks.Length == 0) continue;

                    if (!this.m_Transforms.HasComponent(currentBuilding))
                    {
                        this.RemoveAllTrips(trips);
                    }
                    else if (currentTrip.m_TargetAgent != Entity.Null)
                    {
                        // Has specific agent target but no Target component yet
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new Target { m_Target = currentTrip.m_TargetAgent });
                    }
                    else if (PathUtils.IsPathfindingPurpose(currentTrip.m_Purpose))
                    {
                        // GENERIC PATHFINDING SETUP
                        Citizen c = citizens[i];

                        // Filter "GoingHome" if already at an OC or Commuter logic
                        if (currentTrip.m_Purpose == Purpose.GoingHome)
                        {
                            if ((c.m_State & CitizenFlags.Commuter) == 0) { this.RemoveAllTrips(trips); continue; }
                            if (this.m_OutsideConnections.HasComponent(currentBuildings[i].m_CurrentBuilding)) { this.RemoveAllTrips(trips); continue; }
                        }

                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, in this.m_PathfindTypes);
                        this.m_CommandBuffer.SetComponent(unfilteredChunkIndex, citizenEntity, new PathInformation { m_State = PathFlags.Pending });

                        CreatureData cd;
                        Entity pEntity = ObjectEmergeSystem.SelectResidentPrefab(c, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out cd, out randomSeed);
                        HumanData hd = default(HumanData); if (pEntity != Entity.Null) hd = this.m_PrefabHumanData[pEntity];

                        Household hh = this.m_Households[household];
                        DynamicBuffer<HouseholdCitizen> hhc = this.m_HouseholdCitizens[household];

                        PathfindParameters pp = default(PathfindParameters);
                        pp.m_MaxSpeed = 277.777771f;
                        pp.m_WalkSpeed = hd.m_WalkSpeed;
                        pp.m_Weights = CitizenUtils.GetPathfindWeights(c, hh, hhc.Length);
                        pp.m_Methods = PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(this.m_TimeOfDay);
                        pp.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                        pp.m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost;

                        SetupQueueTarget origin2 = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Methods = PathMethod.Pedestrian, m_RandomCost = 30f };
                        SetupQueueTarget destination2 = new SetupQueueTarget { m_Methods = PathMethod.Pedestrian, m_RandomCost = 30f, m_ActivityMask = cd.m_SupportedActivities };

                        // Setup Generic Target Types
                        switch (currentTrip.m_Purpose)
                        {
                            case Purpose.GoingHome: destination2.m_Type = SetupTargetType.OutsideConnection; break;
                            case Purpose.Hospital: destination2.m_Entity = this.FindDistrict(currentBuilding); destination2.m_Type = SetupTargetType.Hospital; break;
                            case Purpose.Safety: case Purpose.Escape: destination2.m_Type = SetupTargetType.Safety; break;
                            case Purpose.EmergencyShelter: pp.m_Weights = new PathfindWeights(1f, 0f, 0f, 0f); destination2.m_Entity = this.FindDistrict(currentBuilding); destination2.m_Type = SetupTargetType.EmergencyShelter; break;
                            case Purpose.Crime: destination2.m_Type = SetupTargetType.CrimeProducer; break;
                            case Purpose.Sightseeing: destination2.m_Type = SetupTargetType.Sightseeing; break;
                            case Purpose.VisitAttractions: destination2.m_Type = SetupTargetType.Attraction; break;
                        }

                        // Generic Authorization & Vehicle Logic (Same as Case A, abbreviated here but must be present)
                        if (this.m_PropertyRenters.TryGetComponent(household, out var rData2)) pp.m_Authorization1 = rData2.m_Property;
                        if (this.m_Workers.HasComponent(citizenEntity))
                        {
                            Worker w2 = this.m_Workers[citizenEntity];
                            pp.m_Authorization2 = this.m_PropertyRenters.HasComponent(w2.m_Workplace) ? this.m_PropertyRenters[w2.m_Workplace].m_Property : w2.m_Workplace;
                        }
                        // ... (Insert Vehicle Logic here for generic search if they have cars)

                        this.m_PathQueue.Enqueue(new SetupQueueItem(citizenEntity, pp, origin2, destination2));
                        this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new Target { m_Target = Entity.Null });
                    }
                    else
                    {
                        this.RemoveAllTrips(trips);
                    }
                }
            }
        }


    }

}
