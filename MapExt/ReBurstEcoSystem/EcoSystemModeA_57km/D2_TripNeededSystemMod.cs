// Game.Simulation.TripNeededSystem

using System.Runtime.CompilerServices;
using Colossal;
using Colossal.Collections;
using Game;
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
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using HarmonyLib;
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

namespace MapExtPDX.MapExt.ReBurstEcoSystemModeA
{
    /// <summary>
    /// 处理市民、公司和货车的出行需求系统。
    /// 负责生成寻路请求、生成实体（车辆/行人）以及处理到达逻辑。
    /// </summary>

    // =========================================================================================
    // 1. Mod 自定义系统类型 (当前类)
    using ModSystem = TripNeededSystemMod;
    // 2. 原版系统类型 (用于禁用和定位)
    using TargetSystem = TripNeededSystem;
    // =========================================================================================

    public partial class TripNeededSystemMod : GameSystemBase
    {
        #region Constants & Settings
        // 单例引用，便于Harmony补丁调用
        public static ModSystem Instance { get; private set; }
        // 性能设置：每帧最大允许发起的寻路请求数
        // 限制洪峰，防止早晚高峰导致主线程在 JobHandle.Complete 时卡顿
        private const int MAX_PATHFIND_REQUESTS_PER_FRAME = 4096;
        // 系统更新间隔（原版每16帧更新一次）
        public override int GetUpdateInterval(SystemUpdatePhase phase) => 16;
        // [Colossal API] 新增全局原子计数器，用于 Request Throttling
        private NativeCounter m_PathfindRequestCounter;

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

        // Debug Watchers (保留原版调试工具)
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

        #region System Loop

        protected override void OnCreate()
        {
            base.OnCreate();
            // Harmony 补丁访问单例引用
            Instance = this;

            // 禁用原版系统并获取原版系统引用
            // 使用 GetExistingSystemManaged 避免意外创建未初始化的系统
            var originalSystem = World.GetExistingSystemManaged<TargetSystem>();
            if (originalSystem != null)
            {
                originalSystem.Enabled = false;
                //#if DEBUG
                Mod.Info($"[[ELPO] {typeof(ModSystem).Name}] 禁用原系统: {typeof(TargetSystem).Name}");
                //#endif
            }
            else
            {
                // 仅在调试时提示，原版系统可能已被其他Mod移除或尚未加载
#if DEBUG
                Mod.Error($"[{typeof(ModSystem).Name}] 无法找到可禁用的原系统(尚未加载或可能被其他Mod移除): {typeof(TargetSystem).Name}");
#endif
            }

            // 初始化 Debug Watchers
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

            // 获取子系统引用
            m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
            m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
            m_CityConfigurationSystem = base.World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            m_VehicleCapacitySystem = base.World.GetOrCreateSystemManaged<VehicleCapacitySystem>();
            m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
            m_PathfindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
            // 初始化辅助数据
            m_PersonalCarSelectData = new PersonalCarSelectData(this);

            // [Colossal API] 使用提供的 NativeCounter
            m_PathfindRequestCounter = new NativeCounter(Allocator.Persistent); // 新增

            // 构建查询 (Modern QueryBuilder)
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

            m_CarPrefabQuery = base.GetEntityQuery(PersonalCarSelectData.GetEntityQueryDesc());

            // 创建 Archetypes 和 ComponentSets
            m_HandleRequestArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<HandleRequest>(), ComponentType.ReadWrite<Game.Events.Event>());
            m_ResetTripArchetype = EntityManager.CreateArchetype(ComponentType.ReadWrite<Game.Common.Event>(), ComponentType.ReadWrite<ResetTrip>());

            m_HumanSpawnTypes = new ComponentTypeSet(ComponentType.ReadWrite<HumanCurrentLane>(), ComponentType.ReadWrite<TripSource>(), ComponentType.ReadWrite<Unspawned>());
            m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(), ComponentType.ReadWrite<PathElement>());
            m_CurrentLaneTypesRelative = new ComponentTypeSet(
                ComponentType.ReadWrite<Moving>(),
                ComponentType.ReadWrite<TransformFrame>(),
                ComponentType.ReadWrite<HumanNavigation>(),
                ComponentType.ReadWrite<HumanCurrentLane>(),
                ComponentType.ReadWrite<Blocker>());

            // 设置系统更新依赖
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

            if (m_PathfindRequestCounter.IsCreated)
                m_PathfindRequestCounter.Dispose();

            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // 1. 每帧重置计数器
            m_PathfindRequestCounter.Count = 0;

            // 2. 准备预制体数据
            JobHandle residentPrefabHandle;
            NativeList<ArchetypeChunk> humanChunks = m_ResidentPrefabGroup.ToArchetypeChunkListAsync(Allocator.TempJob, out residentPrefabHandle);

            m_PersonalCarSelectData.PreUpdate(this, m_CityConfigurationSystem, m_CarPrefabQuery, Allocator.TempJob, out var carSelectHandle);
            JobHandle dependency = JobHandle.CombineDependencies(Dependency, residentPrefabHandle, carSelectHandle);

            // 3. 调度 CitizenJob (最复杂的逻辑)
            if (!m_CitizenGroup.IsEmptyIgnoreFilter)
            {
                // 创建临时队列
                NativeQueue<AnimalTargetInfo> animalQueue = new NativeQueue<AnimalTargetInfo>(Allocator.TempJob);
                NativeQueue<Entity> leaveQueue = new NativeQueue<Entity>(Allocator.TempJob);

                // 处理 Debug Queues
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

                // 调度 CitizenJob
                CitizenJob citizenJob = new CitizenJob
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

                    // 数据句柄 (SystemAPI 自动注入最新句柄)
                    m_EntityType = SystemAPI.GetEntityTypeHandle(),
                    m_TripNeededType = SystemAPI.GetBufferTypeHandle<TripNeeded>(),
                    m_CurrentBuildingType = SystemAPI.GetComponentTypeHandle<CurrentBuilding>(),
                    m_CurrentTransportType = SystemAPI.GetComponentTypeHandle<CurrentTransport>(true),
                    m_HouseholdMemberType = SystemAPI.GetComponentTypeHandle<HouseholdMember>(true),
                    m_MailSenderType = SystemAPI.GetComponentTypeHandle<MailSender>(true),
                    m_CitizenType = SystemAPI.GetComponentTypeHandle<Citizen>(true),
                    m_HealthProblemType = SystemAPI.GetComponentTypeHandle<HealthProblem>(true),
                    m_AttendingMeetingType = SystemAPI.GetComponentTypeHandle<AttendingMeeting>(true),
                    m_CreatureDataType = SystemAPI.GetComponentTypeHandle<CreatureData>(true),
                    m_ResidentDataType = SystemAPI.GetComponentTypeHandle<ResidentData>(true),

                    // Lookup Tables
                    m_Properties = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    m_Transforms = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                    m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(true),
                    m_ObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(true),
                    m_ObjectDatas = SystemAPI.GetComponentLookup<ObjectData>(true),
                    m_PrefabCarData = SystemAPI.GetComponentLookup<CarData>(true),
                    m_PrefabHumanData = SystemAPI.GetComponentLookup<HumanData>(true),
                    m_PathInfos = SystemAPI.GetComponentLookup<PathInformation>(true),
                    m_ParkedCarData = SystemAPI.GetComponentLookup<ParkedCar>(true),
                    m_PersonalCarData = SystemAPI.GetComponentLookup<Game.Vehicles.PersonalCar>(true),
                    m_AmbulanceData = SystemAPI.GetComponentLookup<Game.Vehicles.Ambulance>(true),
                    m_ConnectionLaneData = SystemAPI.GetComponentLookup<Game.Net.ConnectionLane>(true),
                    m_CurrentDistrictData = SystemAPI.GetComponentLookup<CurrentDistrict>(true),
                    m_Targets = SystemAPI.GetComponentLookup<Target>(true),
                    m_Deleteds = SystemAPI.GetComponentLookup<Deleted>(true),
                    m_PathElements = SystemAPI.GetBufferLookup<PathElement>(true),
                    m_CarKeepers = SystemAPI.GetComponentLookup<CarKeeper>(true),
                    m_BicycleOwners = SystemAPI.GetComponentLookup<BicycleOwner>(true),
                    m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    m_OutsideConnections = SystemAPI.GetComponentLookup<Game.Objects.OutsideConnection>(true),
                    m_UnderConstructionData = SystemAPI.GetComponentLookup<UnderConstruction>(true),
                    m_Attendees = SystemAPI.GetBufferLookup<CoordinatedMeetingAttendee>(true),
                    m_HouseholdAnimals = SystemAPI.GetBufferLookup<HouseholdAnimal>(true),
                    m_TravelPurposes = SystemAPI.GetComponentLookup<TravelPurpose>(true),
                    m_HaveCoordinatedMeetingDatas = SystemAPI.GetBufferLookup<HaveCoordinatedMeetingData>(true),
                    m_Households = SystemAPI.GetComponentLookup<Household>(true),
                    m_HouseholdCitizens = SystemAPI.GetBufferLookup<HouseholdCitizen>(true),
                    m_CriminalData = SystemAPI.GetComponentLookup<Criminal>(true),

                    // ReadWrite Lookups
                    m_Meetings = SystemAPI.GetComponentLookup<CoordinatedMeeting>(),
                    m_Workers = SystemAPI.GetComponentLookup<Worker>(),
                    m_Students = SystemAPI.GetComponentLookup<Game.Citizens.Student>(),

                    // 其他数据
                    m_HumanChunks = humanChunks,
                    m_RandomSeed = RandomSeed.Next(),
                    m_TimeOfDay = m_TimeSystem.normalizedTime,
                    m_ResetTripArchetype = m_ResetTripArchetype,
                    m_HumanSpawnTypes = m_HumanSpawnTypes,
                    m_PathfindTypes = m_PathfindTypes,
                    m_PersonalCarSelectData = m_PersonalCarSelectData,

                    // 输出队列
                    m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                    m_PathQueue = m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter(),
                    m_AnimalQueue = animalQueue.AsParallelWriter(),
                    m_LeaveQueue = leaveQueue.AsParallelWriter(),
                    m_TriggerBuffer = m_TriggerSystem.CreateActionBuffer().AsParallelWriter(),

                    // 调试与配置
                    m_DebugDisableSpawning = debugDisableSpawning,

                    // [优化] 节流计数器注入
                    m_QueueCounter = m_PathfindRequestCounter.ToConcurrent(),
                    m_MaxPathfindRequestsPerFrame = MAX_PATHFIND_REQUESTS_PER_FRAME

                };

                // 辅助 Jobs
                PetTargetJob petTargetJob = new PetTargetJob
                {
                    m_CurrentBuildingData = SystemAPI.GetComponentLookup<CurrentBuilding>(true),
                    m_AnimalQueue = animalQueue,
                    m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer()
                };

                CitizeLeaveJob citizeLeaveJob = new CitizeLeaveJob
                {
                    m_CurrentBuildingData = SystemAPI.GetComponentLookup<CurrentBuilding>(true),
                    m_CitizenPresenceData = SystemAPI.GetComponentLookup<CitizenPresence>(),
                    m_LeaveQueue = leaveQueue
                };

                // 调度 Jobs
                JobHandle citizenJobHandle = JobChunkExtensions.ScheduleParallel(citizenJob, m_CitizenGroup, dependency);
                JobHandle petJobHandle = IJobExtensions.Schedule(petTargetJob, citizenJobHandle);
                JobHandle leaveJobHandle = IJobExtensions.Schedule(citizeLeaveJob, citizenJobHandle);

                // 清理队列
                dependency = JobHandle.CombineDependencies(petJobHandle, leaveJobHandle);
                animalQueue.Dispose(petJobHandle);
                leaveQueue.Dispose(leaveJobHandle);

                m_PathfindSetupSystem.AddQueueWriter(citizenJobHandle);
                m_TriggerSystem.AddActionBufferWriter(citizenJobHandle);
                m_EndFrameBarrier.AddJobHandleForProducer(dependency);
            }
            // 辅助数据更新
            m_PersonalCarSelectData.PostUpdate(dependency);

            // 4. 调度 CompanyJob
            if (!m_CompanyGroup.IsEmptyIgnoreFilter)
            {
                CompanyJob companyJob = new CompanyJob
                {
                    m_EntityType = SystemAPI.GetEntityTypeHandle(),
                    m_PropertyRenterType = SystemAPI.GetComponentTypeHandle<PropertyRenter>(true),
                    m_CreatureDataType = SystemAPI.GetComponentTypeHandle<CreatureData>(true),
                    m_ResidentDataType = SystemAPI.GetComponentTypeHandle<ResidentData>(true),
                    m_PrefabType = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
                    m_TripNeededType = SystemAPI.GetBufferTypeHandle<TripNeeded>(),
                    m_VehicleType = SystemAPI.GetBufferTypeHandle<OwnedVehicle>(true),
                    m_ResourceType = SystemAPI.GetBufferTypeHandle<Game.Economy.Resources>(),
                    m_PrefabDeliveryTruckData = SystemAPI.GetComponentLookup<DeliveryTruckData>(true),
                    m_PrefabObjectData = SystemAPI.GetComponentLookup<ObjectData>(true),
                    m_Prefabs = SystemAPI.GetComponentLookup<PrefabRef>(true),
                    m_Transforms = SystemAPI.GetComponentLookup<Game.Objects.Transform>(true),
                    m_TransportCompanyDatas = SystemAPI.GetComponentLookup<TransportCompanyData>(true),
                    m_ServiceRequestData = SystemAPI.GetComponentLookup<ServiceRequest>(true),
                    m_PathInformationData = SystemAPI.GetComponentLookup<PathInformation>(true),
                    m_UnderConstructionData = SystemAPI.GetComponentLookup<UnderConstruction>(true),
                    m_PropertyRenterData = SystemAPI.GetComponentLookup<PropertyRenter>(true),
                    m_PathElements = SystemAPI.GetBufferLookup<PathElement>(true),
                    m_ActivityLocationElements = SystemAPI.GetBufferLookup<ActivityLocationElement>(true),
                    m_EfficiencyBufs = SystemAPI.GetBufferLookup<Efficiency>(true),
                    m_InstalledUpgradeBufs = SystemAPI.GetBufferLookup<InstalledUpgrade>(true),
                    m_HumanChunks = humanChunks,
                    m_LeftHandTraffic = m_CityConfigurationSystem.leftHandTraffic,
                    m_RandomSeed = RandomSeed.Next(),
                    m_HandleRequestArchetype = m_HandleRequestArchetype,
                    m_DeliveryTruckSelectData = m_VehicleCapacitySystem.GetDeliveryTruckSelectData(),
                    m_CurrentLaneTypesRelative = m_CurrentLaneTypesRelative,
                    m_CommandBuffer = m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter(),
                    m_DebugDisableSpawning = debugDisableSpawning
                };

                dependency = JobChunkExtensions.ScheduleParallel(companyJob, m_CompanyGroup, dependency);
                m_EndFrameBarrier.AddJobHandleForProducer(dependency);
            }

            humanChunks.Dispose(dependency);
            base.Dependency = dependency;
        }

        #endregion

        #region Jobs

        // 公司货车出行需求处理作业(无需优化)
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

            private void SpawnDeliveryTruck(int index, Entity owner, Entity from, ref Game.Objects.Transform transform, TripNeeded trip)
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
                uint num = 0u;
                if (m_UnderConstructionData.TryGetComponent(entity3, out var componentData3) && componentData3.m_NewPrefab == Entity.Null)
                {
                    m_PathInformationData.TryGetComponent(entity2, out var componentData4);
                    num = ObjectUtils.GetTripDelayFrames(componentData3, componentData4);
                }
                if (m_UnderConstructionData.TryGetComponent(from, out componentData3) && componentData3.m_NewPrefab == Entity.Null)
                {
                    num = math.max(num, ObjectUtils.GetRemainingConstructionFrames(componentData3));
                }
                Unity.Mathematics.Random random = m_RandomSeed.GetRandom(owner.Index);
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
                if (!m_DeliveryTruckSelectData.TrySelectItem(ref random, resources, capacity, out var item))
                {
                    return;
                }
                Entity entity4 = m_DeliveryTruckSelectData.CreateVehicle(m_CommandBuffer, index, ref random, ref m_PrefabDeliveryTruckData, ref m_PrefabObjectData, item, resource, resource2, ref amount, ref returnAmount, transform, from, deliveryTruckFlags, num);
                int maxCount = 1;
                if (CreatePassengers(index, entity4, item.m_Prefab1, transform, driver: true, ref maxCount, ref random) > 0)
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
                        DynamicBuffer<PathElement> targetElements = m_CommandBuffer.SetBuffer<PathElement>(index, entity4);
                        PathUtils.CopyPath(sourceElements, default(PathOwner), 0, targetElements);
                        m_CommandBuffer.SetComponent(index, entity4, new PathOwner(PathFlags.Updated));
                        m_CommandBuffer.SetComponent(index, entity4, m_PathInformationData[entity2]);
                    }
                }
            }

            private int CreatePassengers(int jobIndex, Entity vehicleEntity, Entity vehiclePrefab, Game.Objects.Transform transform, bool driver, ref int maxCount, ref Unity.Mathematics.Random random)
            {
                int num = 0;
                if (maxCount > 0 && m_ActivityLocationElements.TryGetBuffer(vehiclePrefab, out var bufferData))
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
                            bool test = ((activityLocationElement.m_ActivityFlags & ActivityFlags.InvertLefthandTraffic) != 0 && m_LeftHandTraffic) || ((activityLocationElement.m_ActivityFlags & ActivityFlags.InvertRighthandTraffic) != 0 && !m_LeftHandTraffic);
                            activityLocationElement.m_Position.x = math.select(activityLocationElement.m_Position.x, 0f - activityLocationElement.m_Position.x, test);
                            if ((!(math.abs(activityLocationElement.m_Position.x) >= 0.5f) || activityLocationElement.m_Position.x >= 0f == m_LeftHandTraffic) && activityLocationElement.m_Position.z > num4)
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
                            Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks, m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out creatureData, out randomSeed);
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
                            if (driver && j == num3)
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
                            num++;
                        }
                    }
                }
                return num;
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
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
                        int transportCompanyAvailableVehicles = VehicleUtils.GetTransportCompanyAvailableVehicles(entity, ref m_EfficiencyBufs, ref m_Prefabs, ref m_TransportCompanyDatas, ref m_InstalledUpgradeBufs);
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

        // 宠物目标信息结构体
        private struct AnimalTargetInfo
        {
            public Entity m_Animal;
            public Entity m_Source;
            public Entity m_Target;
        }

        // 宠物目标处理作业
        [BurstCompile]
        private struct PetTargetJob : IJob
        {
            [ReadOnly]
            public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;
            public NativeQueue<AnimalTargetInfo> m_AnimalQueue;
            public EntityCommandBuffer m_CommandBuffer;

            public void Execute()
            {
                int count = m_AnimalQueue.Count;
                if (count == 0)
                {
                    return;
                }
                NativeParallelHashSet<Entity> nativeParallelHashSet = new NativeParallelHashSet<Entity>(count, Allocator.Temp);
                for (int i = 0; i < count; i++)
                {
                    AnimalTargetInfo animalTargetInfo = m_AnimalQueue.Dequeue();
                    if (m_CurrentBuildingData.HasComponent(animalTargetInfo.m_Animal) && !(m_CurrentBuildingData[animalTargetInfo.m_Animal].m_CurrentBuilding != animalTargetInfo.m_Source) && nativeParallelHashSet.Add(animalTargetInfo.m_Animal))
                    {
                        m_CommandBuffer.AddComponent(animalTargetInfo.m_Animal, new Target(animalTargetInfo.m_Target));
                    }
                }
                nativeParallelHashSet.Dispose();
            }
        }

        // 市民离开建筑物处理作业
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

        // 市民出行需求处理作业
        [BurstCompile]
        private struct CitizenJob : IJobChunk
        {
            #region Fields & Config

            // [优化] 原子计数器 (Colossal.NativeCounter)
            public NativeCounter.Concurrent m_QueueCounter;
            public int m_MaxPathfindRequestsPerFrame;

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

            // Type Handles (使用 SystemAPI 获取)
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

            // Component Lookups
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

            // ReadWrite Lookups
            [NativeDisableParallelForRestriction] public ComponentLookup<CoordinatedMeeting> m_Meetings;
            [NativeDisableParallelForRestriction] public ComponentLookup<Worker> m_Workers;
            [NativeDisableParallelForRestriction] public ComponentLookup<Game.Citizens.Student> m_Students;

            // Data
            [ReadOnly] public NativeList<ArchetypeChunk> m_HumanChunks;
            [ReadOnly] public RandomSeed m_RandomSeed;
            [ReadOnly] public float m_TimeOfDay;
            [ReadOnly] public EntityArchetype m_ResetTripArchetype;
            [ReadOnly] public ComponentTypeSet m_HumanSpawnTypes;
            [ReadOnly] public ComponentTypeSet m_PathfindTypes;
            [ReadOnly] public PersonalCarSelectData m_PersonalCarSelectData;

            // Queues
            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
            public NativeQueue<SetupQueueItem>.ParallelWriter m_PathQueue;
            public NativeQueue<AnimalTargetInfo>.ParallelWriter m_AnimalQueue;
            public NativeQueue<Entity>.ParallelWriter m_LeaveQueue;
            public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;
            public bool m_DebugDisableSpawning;

            #endregion

            #region Helper Methods

            // [逻辑保持原版] 计算生成时的 ResidentFlags 和 Divert 目的
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
                        if (m_TravelPurposes.HasComponent(citizen))
                        {
                            purpose = m_TravelPurposes[citizen].m_Purpose;
                        }
                        else
                        {
                            purpose = Purpose.None;
                        }
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

            // [逻辑保持原版] 生成居民实体及其附属车辆
            private Entity SpawnResident(int index, Entity citizen, Entity fromBuilding, Citizen citizenData, Target target, ResidentFlags flags, Purpose divertPurpose, uint timer, bool hasDivertPath, bool isDead, bool isCarried)
            {
                CreatureData creatureData;
                PseudoRandomSeed randomSeed;
                Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks, m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out creatureData, out randomSeed);
                ObjectData objectData = m_ObjectDatas[entity];

                PrefabRef prefabRef = new PrefabRef { m_Prefab = entity };

                Game.Objects.Transform transform;
                if (m_Transforms.HasComponent(fromBuilding))
                {
                    transform = m_Transforms[fromBuilding];
                }
                else
                {
                    transform = new Game.Objects.Transform { m_Rotation = new quaternion(0f, 0f, 0f, 1f) };
                }

                Game.Creatures.Resident residentComp = new Game.Creatures.Resident { m_Citizen = citizen, m_Flags = flags };
                Human humanComp = default(Human);
                if (isDead) humanComp.m_Flags |= HumanFlags.Dead;
                if (isCarried) humanComp.m_Flags |= HumanFlags.Carried;

                PathOwner pathOwner = new PathOwner(PathFlags.Updated);
                TripSource tripSource = new TripSource(fromBuilding, timer);

                Entity agentEntity = m_CommandBuffer.CreateEntity(index, objectData.m_Archetype);
                Entity vehicleEntity = Entity.Null;
                HumanCurrentLane humanLane = default(HumanCurrentLane);

                // 复制路径并处理车辆生成 (Process Vehicle/Bicycle)
                if (m_PathElements.TryGetBuffer(citizen, out var bufferData) && bufferData.Length > 0)
                {
                    PathElement pathElement = bufferData[0];
                    CreatureLaneFlags creatureLaneFlags = (CreatureLaneFlags)0u; 

                    if ((pathElement.m_Flags & PathElementFlags.Secondary) != 0)
                    {
                        Unity.Mathematics.Random random = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
                        vehicleEntity = m_PersonalCarSelectData.CreateVehicle(m_CommandBuffer, index, ref random, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, transform, fromBuilding, citizen, PersonalCarFlags.Boarding, stopped: false);

                        if (vehicleEntity != Entity.Null)
                        {
                            DynamicBuffer<PathElement> targetElements = m_CommandBuffer.SetBuffer<PathElement>(index, agentEntity);
                            PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements);

                            Game.Vehicles.CarLaneFlags carLaneFlags = Game.Vehicles.CarLaneFlags.EndOfPath | Game.Vehicles.CarLaneFlags.EndReached | Game.Vehicles.CarLaneFlags.FixedLane;
                            if (m_ConnectionLaneData.TryGetComponent(pathElement.m_Target, out var componentData))
                            {
                                carLaneFlags = (((componentData.m_Flags & ConnectionLaneFlags.Area) == 0) ? (carLaneFlags | Game.Vehicles.CarLaneFlags.Connection) : (carLaneFlags | Game.Vehicles.CarLaneFlags.Area));
                            }

                            m_CommandBuffer.SetComponent(index, vehicleEntity, new CarCurrentLane(pathElement, carLaneFlags));
                            m_CommandBuffer.SetComponent(index, citizen, new BicycleOwner { m_Bicycle = vehicleEntity });
                            residentComp.m_Flags |= ResidentFlags.InVehicle;
                            creatureLaneFlags |= CreatureLaneFlags.EndOfPath | CreatureLaneFlags.EndReached;
                        }
                    }
                    DynamicBuffer<PathElement> targetElements2 = m_CommandBuffer.SetBuffer<PathElement>(index, agentEntity);
                    PathUtils.CopyPath(bufferData, default(PathOwner), 0, targetElements2);
                    humanLane = new HumanCurrentLane(pathElement, creatureLaneFlags);
                    pathOwner.m_State |= PathFlags.Updated;
                }

                m_CommandBuffer.AddComponent(index, agentEntity, in m_HumanSpawnTypes);
                if (divertPurpose != 0)
                {
                    if (hasDivertPath) pathOwner.m_State |= PathFlags.CachedObsolete;
                    else pathOwner.m_State |= PathFlags.DivertObsolete;
                    m_CommandBuffer.AddComponent(index, agentEntity, new Divert { m_Purpose = divertPurpose });
                }

                m_CommandBuffer.SetComponent(index, agentEntity, transform);
                m_CommandBuffer.SetComponent(index, agentEntity, prefabRef);
                m_CommandBuffer.SetComponent(index, agentEntity, target);
                m_CommandBuffer.SetComponent(index, agentEntity, residentComp);
                m_CommandBuffer.SetComponent(index, agentEntity, humanComp);
                m_CommandBuffer.SetComponent(index, agentEntity, pathOwner);
                m_CommandBuffer.SetComponent(index, agentEntity, randomSeed);
                m_CommandBuffer.SetComponent(index, agentEntity, humanLane);
                m_CommandBuffer.SetComponent(index, agentEntity, tripSource);

                if (vehicleEntity != Entity.Null)
                {
                    m_CommandBuffer.RemoveComponent<TripSource>(index, agentEntity);
                    m_CommandBuffer.AddComponent(index, agentEntity, new CurrentVehicle(vehicleEntity, CreatureVehicleFlags.Leader | CreatureVehicleFlags.Driver | CreatureVehicleFlags.Entering));
                }
                return agentEntity;
            }

            private void ResetTrip(int index, Entity creature, Entity citizen, Entity fromBuilding, Target target, ResidentFlags flags, Purpose divertPurpose, uint timer, bool hasDivertPath)
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
                if (trips.Length <= 0) return;
                Purpose purpose = trips[0].m_Purpose;
                for (int num = trips.Length - 1; num >= 0; num--)
                {
                    if (trips[num].m_Purpose == purpose) trips.RemoveAt(num);
                }
            }

            private Entity FindDistrict(Entity building)
            {
                if (m_CurrentDistrictData.HasComponent(building)) return m_CurrentDistrictData[building].m_District;
                return Entity.Null;
            }

            private void AddPetTargets(Entity household, Entity source, Entity target)
            {
                if (m_HouseholdAnimals.HasBuffer(household))
                {
                    DynamicBuffer<HouseholdAnimal> dynamicBuffer = m_HouseholdAnimals[household];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        HouseholdAnimal householdAnimal = dynamicBuffer[i];
                        m_AnimalQueue.Enqueue(new AnimalTargetInfo { m_Animal = householdAnimal.m_HouseholdPet, m_Source = source, m_Target = target });
                    }
                }
            }

            #endregion

            #region Execute

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                BufferAccessor<TripNeeded> tripBufferAccessor = chunk.GetBufferAccessor(ref m_TripNeededType);
                NativeArray<HouseholdMember> householdMembers = chunk.GetNativeArray(ref m_HouseholdMemberType);
                NativeArray<CurrentBuilding> currentBuildings = chunk.GetNativeArray(ref m_CurrentBuildingType);
                NativeArray<CurrentTransport> currentTransports = chunk.GetNativeArray(ref m_CurrentTransportType);
                NativeArray<Citizen> citizens = chunk.GetNativeArray(ref m_CitizenType);
                NativeArray<HealthProblem> healthProblems = chunk.GetNativeArray(ref m_HealthProblemType);
                NativeArray<AttendingMeeting> attendingMeetings = chunk.GetNativeArray(ref m_AttendingMeetingType);

                for (int i = 0; i < entities.Length; i++)
                {
                    Entity citizenEntity = entities[i];
                    DynamicBuffer<TripNeeded> trips = tripBufferAccessor[i];

                    if (trips.Length <= 0) continue;

                    // -----------------------------------------------------------
                    // 1. 数据准备与基础校验
                    // -----------------------------------------------------------
                    Entity currentBuilding = currentBuildings[i].m_CurrentBuilding;
                    Entity household = householdMembers[i].m_Household;

                    TripNeeded currentTrip = trips[0];
                    bool isMovingAway = currentTrip.m_Purpose == Purpose.MovingAway;
                    bool isEmergency = currentTrip.m_Purpose == Purpose.Safety || currentTrip.m_Purpose == Purpose.Escape ||
                                       currentTrip.m_Purpose == Purpose.Hospital || currentTrip.m_Purpose == Purpose.Deathcare;

                    bool isMailSender = chunk.IsComponentEnabled(ref m_MailSenderType, i);

                    bool isPrisoner = false;
                    if (m_CriminalData.TryGetComponent(citizenEntity, out var criminalData))
                        isPrisoner = (criminalData.m_Flags & (CriminalFlags.Prisoner | CriminalFlags.Arrested | CriminalFlags.Sentenced)) != 0;

                    // [优化] 预先获取 PathInformation
                    bool hasPathInfo = m_PathInfos.TryGetComponent(citizenEntity, out PathInformation pathInfo);

                    // 2. 健康状态检查 (原版逻辑)
                    if (healthProblems.Length != 0 && !isPrisoner)
                    {
                        HealthProblem health = healthProblems[i];
                        if ((health.m_Flags & (HealthProblemFlags.Dead | HealthProblemFlags.RequireTransport | HealthProblemFlags.InDanger | HealthProblemFlags.Trapped)) != HealthProblemFlags.None)
                        {
                            bool isDead = (health.m_Flags & HealthProblemFlags.Dead) != 0;
                            bool requireTransport = (health.m_Flags & HealthProblemFlags.RequireTransport) != 0;

                            if (!(isDead || requireTransport))
                            {
                                if (hasPathInfo) m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                                continue;
                            }

                            // 移除除非医疗外的所有 Trip
                            while (trips.Length > 0 && trips[0].m_Purpose != Purpose.Deathcare && trips[0].m_Purpose != Purpose.Hospital)
                            {
                                trips.RemoveAt(0);
                            }

                            if (trips.Length == 0)
                            {
                                if (hasPathInfo) m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                                continue;
                            }
                            currentTrip = trips[0];
                        }
                    }

                    // 3. 会议同步检查 (原版逻辑)
                    if (!isMovingAway && attendingMeetings.Length != 0 && !isPrisoner)
                    {
                        Entity meeting = attendingMeetings[i].m_Meeting;
                        if (m_PrefabRefData.HasComponent(meeting))
                        {
                            Entity prefab = m_PrefabRefData[meeting].m_Prefab;
                            CoordinatedMeeting coordMeeting = m_Meetings[meeting];
                            if (m_HaveCoordinatedMeetingDatas.HasBuffer(prefab))
                            {
                                DynamicBuffer<HaveCoordinatedMeetingData> meetingData = m_HaveCoordinatedMeetingDatas[prefab];
                                if (meetingData.Length > coordMeeting.m_Phase)
                                {
                                    HaveCoordinatedMeetingData phaseData = meetingData[coordMeeting.m_Phase];
                                    while (trips.Length > 0 && trips[0].m_Purpose != phaseData.m_TravelPurpose.m_Purpose)
                                    {
                                        trips.RemoveAt(0);
                                    }
                                    if (trips.Length == 0)
                                    {
                                        if (hasPathInfo) m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                                        continue;
                                    }
                                    currentTrip = trips[0];
                                }
                            }
                        }
                    }

                    // 4. 搬家状态检查
                    if ((citizens[i].m_State & CitizenFlags.MovingAwayReachOC) != 0)
                    {
                        if (hasPathInfo) m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                        continue;
                    }

                    // [优化] PathInfo 深度校验与死循环熔断
                    if (hasPathInfo)
                    {
                        if ((pathInfo.m_State & PathFlags.Pending) != 0) continue; // 计算中，等待

                        // [熔断] 寻路失败 (Target is Null)
                        if (pathInfo.m_Destination == Entity.Null)
                        {
                            m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                            m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                            RemoveAllTrips(trips); // 强制移除以断开循环
                            continue;
                        }

                        // 无效路径 (起点==终点)
                        if ((((pathInfo.m_Origin != Entity.Null && pathInfo.m_Origin == pathInfo.m_Destination) || currentBuilding == pathInfo.m_Destination) && !isEmergency) || !m_Targets.HasComponent(citizenEntity))
                        {
                            m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                            RemoveAllTrips(trips);
                            continue;
                        }
                    }

                    if (m_DebugDisableSpawning) continue;

                    // [优化] 节流控制 (Throttling)
                    // 仅对非紧急的、尚未开始寻路的请求进行限流
                    if (!hasPathInfo && !isEmergency)
                    {
                        if (m_QueueCounter.Increment() >= m_MaxPathfindRequestsPerFrame) continue;
                    }

                    PseudoRandomSeed randomSeed;
                    Entity trailerPrefab;
                    float offset;

                    // --------------------------------------------------------------------------
                    // Case A: 处理明确的目标 (Target Component Exists)
                    // --------------------------------------------------------------------------
                    if (m_Targets.HasComponent(citizenEntity))
                    {
                        Target target = m_Targets[citizenEntity];

                        // 1. Target 为 Null，等待寻路器回填
                        if (target.m_Target == Entity.Null)
                        {
                            if (!hasPathInfo)
                            {
                                m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                                continue;
                            }
                            if (pathInfo.m_Destination == Entity.Null)
                            {
                                m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                                RemoveAllTrips(trips);
                                m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                                continue;
                            }
                            target.m_Target = pathInfo.m_Destination;
                        }

                        Entity targetEntity = target.m_Target;
                        if (m_Properties.TryGetComponent(targetEntity, out var propertyRenter)) targetEntity = propertyRenter.m_Property;

                        // [优化] 极速近距离剔除 (Zero-Distance Culling)
                        if (currentBuilding == targetEntity && !isEmergency)
                        {
                            m_CommandBuffer.SetComponentEnabled<Arrived>(unfilteredChunkIndex, citizenEntity, value: true);
                            m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new TravelPurpose { m_Data = currentTrip.m_Data, m_Purpose = currentTrip.m_Purpose, m_Resource = currentTrip.m_Resource });
                            m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                            if (hasPathInfo) m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                            RemoveAllTrips(trips);
                            continue;
                        }

                        bool isHealthDead = (healthProblems.Length > 0 && (healthProblems[i].m_Flags & HealthProblemFlags.Dead) != 0);

                        // 2. 发起具体路径寻路
                        // 条件：没有路径信息 且 不是等待救护车/灵车的紧急状态
                        if (!hasPathInfo && !isEmergency)
                        {
                            m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                            m_CommandBuffer.SetComponent(unfilteredChunkIndex, citizenEntity, new PathInformation { m_State = PathFlags.Pending });

                            Citizen citizenData = citizens[i];
                            CreatureData creatureData;
                            Entity entity3 = ObjectEmergeSystem.SelectResidentPrefab(citizenData, m_HumanChunks, m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out creatureData, out randomSeed);
                            HumanData humanData = default(HumanData);
                            if (entity3 != Entity.Null) humanData = m_PrefabHumanData[entity3];

                            Household household2 = m_Households[household];
                            DynamicBuffer<HouseholdCitizen> householdCitizens = m_HouseholdCitizens[household];

                            PathfindParameters parameters = default(PathfindParameters);
                            parameters.m_MaxSpeed = 277.777771f;
                            parameters.m_WalkSpeed = humanData.m_WalkSpeed;
                            parameters.m_Weights = CitizenUtils.GetPathfindWeights(citizenData, household2, householdCitizens.Length);
                            parameters.m_Methods = PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(m_TimeOfDay);
                            parameters.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                            parameters.m_MaxCost = math.select(CitizenBehaviorSystem.kMaxPathfindCost, CitizenBehaviorSystem.kMaxMovingAwayCost, isMovingAway);

                            SetupQueueTarget origin = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Methods = PathMethod.Pedestrian, m_RandomCost = 30f };
                            SetupQueueTarget destination = new SetupQueueTarget
                            {
                                m_Type = SetupTargetType.CurrentLocation,
                                m_Methods = PathMethod.Pedestrian,
                                m_Entity = target.m_Target,
                                m_ActivityMask = creatureData.m_SupportedActivities
                            };

                            if (m_PropertyRenters.TryGetComponent(household, out var householdRenter)) parameters.m_Authorization1 = householdRenter.m_Property;
                            if (m_Workers.HasComponent(citizenEntity))
                            {
                                Worker worker = m_Workers[citizenEntity];
                                parameters.m_Authorization2 = m_PropertyRenters.HasComponent(worker.m_Workplace) ? m_PropertyRenters[worker.m_Workplace].m_Property : worker.m_Workplace;
                            }

                            // [优化] 按需读取车辆数据
                            if (m_CarKeepers.IsComponentEnabled(citizenEntity))
                            {
                                Entity car = m_CarKeepers[citizenEntity].m_Car;
                                if (m_ParkedCarData.HasComponent(car))
                                {
                                    PrefabRef carPrefab = m_PrefabRefData[car];
                                    ParkedCar parkedCar = m_ParkedCarData[car];
                                    CarData carData = m_PrefabCarData[carPrefab.m_Prefab];
                                    parameters.m_MaxSpeed.x = carData.m_MaxSpeed;
                                    parameters.m_ParkingTarget = parkedCar.m_Lane;
                                    parameters.m_ParkingDelta = parkedCar.m_CurvePosition;
                                    parameters.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref m_PrefabRefData, ref m_ObjectGeometryData);
                                    parameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
                                    parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
                                    if (m_PersonalCarData.TryGetComponent(car, out var pCar) && (pCar.m_State & PersonalCarFlags.HomeTarget) == 0) parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
                                }
                            }
                            else if (m_BicycleOwners.IsComponentEnabled(citizenEntity))
                            {
                                Entity bicycle = m_BicycleOwners[citizenEntity].m_Bicycle;
                                if (!m_PrefabRefData.TryGetComponent(bicycle, out var bPrefab) && currentBuilding == householdRenter.m_Property)
                                {
                                    Unity.Mathematics.Random r = citizenData.GetPseudoRandom(CitizenPseudoRandom.BicycleModel);
                                    bPrefab.m_Prefab = m_PersonalCarSelectData.SelectVehiclePrefab(ref r, 1, 0, avoidTrailers: true, noSlowVehicles: false, bicycle: true, out trailerPrefab);
                                }
                                if (m_PrefabCarData.TryGetComponent(bPrefab.m_Prefab, out var bCarData) && m_ObjectGeometryData.TryGetComponent(bPrefab.m_Prefab, out var bGeo))
                                {
                                    parameters.m_MaxSpeed.x = bCarData.m_MaxSpeed;
                                    parameters.m_ParkingSize = VehicleUtils.GetParkingSize(bGeo, out offset);
                                    parameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
                                    parameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
                                    if (m_ParkedCarData.TryGetComponent(bicycle, out var bParked))
                                    {
                                        parameters.m_ParkingTarget = bParked.m_Lane;
                                        parameters.m_ParkingDelta = bParked.m_CurvePosition;
                                        if (m_PersonalCarData.TryGetComponent(bicycle, out var bpCar) && (bpCar.m_State & PersonalCarFlags.HomeTarget) == 0) parameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
                                    }
                                    else if (!CollectionUtils.TryGet(currentTransports, i, out var ct) || !m_PrefabRefData.HasComponent(ct.m_CurrentTransport) || m_Deleteds.HasComponent(ct.m_CurrentTransport))
                                    {
                                        origin.m_Methods |= PathMethod.Bicycle; origin.m_RoadTypes |= RoadTypes.Bicycle;
                                    }
                                    if (targetEntity == householdRenter.m_Property)
                                    {
                                        destination.m_Methods |= PathMethod.Bicycle; destination.m_RoadTypes |= RoadTypes.Bicycle;
                                    }
                                }
                            }

                            m_PathQueue.Enqueue(new SetupQueueItem(citizenEntity, parameters, origin, destination));
                            continue;
                        }

                        // 3. 路径已就绪，执行 Spawn 逻辑
                        DynamicBuffer<PathElement> pathBuffer = default(DynamicBuffer<PathElement>);
                        if (!isEmergency) pathBuffer = m_PathElements[citizenEntity];

                        if ((!isEmergency && pathBuffer.Length > 0) || m_PrefabRefData.HasComponent(currentTrip.m_TargetAgent))
                        {
                            Entity spawnFrom = currentBuildings[i].m_CurrentBuilding;
                            Entity workplaceProperty = Entity.Null;

                            // 统计逻辑
                            if (m_PropertyRenters.TryGetComponent(household, out var householdRenter) && !isEmergency && spawnFrom.Equals(householdRenter.m_Property))
                            {
                                if (currentTrip.m_Purpose == Purpose.GoingToWork && m_Workers.HasComponent(citizenEntity))
                                {
                                    Worker w = m_Workers[citizenEntity];
                                    if (pathInfo.m_Destination == Entity.Null)
                                    {
                                        m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, citizenEntity);
                                        m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenBecameUnemployed, Entity.Null, citizenEntity, w.m_Workplace));
                                    }
                                    else { w.m_LastCommuteTime = pathInfo.m_Duration; m_Workers[citizenEntity] = w; }
                                }
                                else if (currentTrip.m_Purpose == Purpose.GoingToSchool && m_Students.HasComponent(citizenEntity))
                                {
                                    if (pathInfo.m_Destination == Entity.Null)
                                    {
                                        m_CommandBuffer.AddComponent<StudentsRemoved>(unfilteredChunkIndex, m_Students[citizenEntity].m_School);
                                        m_CommandBuffer.RemoveComponent<Game.Citizens.Student>(unfilteredChunkIndex, citizenEntity);
                                    }
                                    else { Game.Citizens.Student s = m_Students[citizenEntity]; s.m_LastCommuteTime = pathInfo.m_Duration; m_Students[citizenEntity] = s; }
                                }
                            }

                            if (m_Workers.HasComponent(citizenEntity))
                            {
                                Worker w = m_Workers[citizenEntity];
                                workplaceProperty = !m_PropertyRenters.HasComponent(w.m_Workplace) ? w.m_Workplace : m_PropertyRenters[w.m_Workplace].m_Property;
                            }
                            if ((m_PropertyRenters.HasComponent(household) && spawnFrom.Equals(m_PropertyRenters[household].m_Property)) || spawnFrom.Equals(workplaceProperty))
                            {
                                m_LeaveQueue.Enqueue(citizenEntity);
                            }

                            Entity currentVehicleEntity = Entity.Null;
                            if (currentTransports.Length != 0) currentVehicleEntity = currentTransports[i].m_CurrentTransport;

                            uint timer = 512u;
                            Purpose divertPurpose = Purpose.None;
                            bool pathFailed = !isEmergency && pathBuffer.Length == 0;
                            bool hasDivertPath = false;

                            GetResidentFlags(citizenEntity, spawnFrom, isMailSender, pathFailed, ref target, ref currentTrip.m_Purpose, ref divertPurpose, ref timer, ref hasDivertPath);

                            if (m_UnderConstructionData.TryGetComponent(targetEntity, out var underConstruction) && underConstruction.m_NewPrefab == Entity.Null)
                            {
                                timer = math.max(timer, ObjectUtils.GetTripDelayFrames(underConstruction, pathInfo));
                            }

                            ResidentFlags residentFlags = ResidentFlags.None;
                            if (attendingMeetings.Length > 0)
                            {
                                Entity meeting2 = attendingMeetings[i].m_Meeting;
                                if (m_PrefabRefData.HasComponent(meeting2))
                                {
                                    CoordinatedMeeting cm = m_Meetings[meeting2];
                                    DynamicBuffer<HaveCoordinatedMeetingData> cmd = m_HaveCoordinatedMeetingDatas[m_PrefabRefData[meeting2].m_Prefab];
                                    if (cm.m_Status != MeetingStatus.Done)
                                    {
                                        HaveCoordinatedMeetingData hcmd = cmd[cm.m_Phase];
                                        if (currentTrip.m_Purpose == hcmd.m_TravelPurpose.m_Purpose && (hcmd.m_TravelPurpose.m_Resource == Resource.NoResource || hcmd.m_TravelPurpose.m_Resource == currentTrip.m_Resource) && cm.m_Target == Entity.Null)
                                        {
                                            DynamicBuffer<CoordinatedMeetingAttendee> cma = m_Attendees[meeting2];
                                            if (cma.Length > 0 && cma[0].m_Attendee == citizenEntity)
                                            {
                                                cm.m_Target = target.m_Target;
                                                m_Meetings[meeting2] = cm;
                                                residentFlags |= ResidentFlags.PreferredLeader;
                                            }
                                        }
                                    }
                                }
                            }

                            if (m_PrefabRefData.HasComponent(currentVehicleEntity) && !m_Deleteds.HasComponent(currentVehicleEntity))
                            {
                                ResetTrip(unfilteredChunkIndex, currentVehicleEntity, citizenEntity, currentBuilding, target, residentFlags, divertPurpose, timer, hasDivertPath);
                            }
                            else
                            {
                                currentVehicleEntity = SpawnResident(unfilteredChunkIndex, citizenEntity, currentBuilding, citizens[i], target, residentFlags, divertPurpose, timer, hasDivertPath, isHealthDead, isEmergency);
                                m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new CurrentTransport(currentVehicleEntity));
                            }

                            if ((currentTrip.m_Purpose != Purpose.GoingToWork && currentTrip.m_Purpose != Purpose.GoingToSchool) || currentBuilding != householdRenter.m_Property)
                            {
                                AddPetTargets(household, currentBuilding, target.m_Target);
                            }
                            m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new TravelPurpose { m_Data = currentTrip.m_Data, m_Purpose = currentTrip.m_Purpose, m_Resource = currentTrip.m_Resource });
                            m_CommandBuffer.RemoveComponent<CurrentBuilding>(unfilteredChunkIndex, citizenEntity);
                        }
                        else if ((m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0)
                        {
                            CitizenUtils.HouseholdMoveAway(m_CommandBuffer, unfilteredChunkIndex, household, MoveAwayReason.TripNeedNotMovedIn);
                        }

                        RemoveAllTrips(trips);
                        m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                        m_CommandBuffer.RemoveComponent<Target>(unfilteredChunkIndex, citizenEntity);
                    }
                    // --------------------------------------------------------------------------
                    // Case B: 处理泛目标 (Generic Target, e.g. Hospital)
                    // --------------------------------------------------------------------------
                    else
                    {
                        if (hasPathInfo || m_HumanChunks.Length == 0) continue;

                        if (!m_Transforms.HasComponent(currentBuilding))
                        {
                            RemoveAllTrips(trips);
                        }
                        else if (currentTrip.m_TargetAgent != Entity.Null)
                        {
                            m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new Target { m_Target = currentTrip.m_TargetAgent });
                        }
                        else if (PathUtils.IsPathfindingPurpose(currentTrip.m_Purpose))
                        {
                            Citizen c = citizens[i];
                            if (currentTrip.m_Purpose == Purpose.GoingHome)
                            {
                                if ((c.m_State & CitizenFlags.Commuter) == 0) { RemoveAllTrips(trips); continue; }
                                if (m_OutsideConnections.HasComponent(currentBuildings[i].m_CurrentBuilding)) { RemoveAllTrips(trips); continue; }
                            }

                            m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, in m_PathfindTypes);
                            m_CommandBuffer.SetComponent(unfilteredChunkIndex, citizenEntity, new PathInformation { m_State = PathFlags.Pending });

                            CreatureData cd;
                            ObjectEmergeSystem.SelectResidentPrefab(c, m_HumanChunks, m_EntityType, ref m_CreatureDataType, ref m_ResidentDataType, out cd, out randomSeed);

                            Household hh = m_Households[household];
                            DynamicBuffer<HouseholdCitizen> hhc = m_HouseholdCitizens[household];

                            PathfindParameters pp = default(PathfindParameters);
                            pp.m_MaxSpeed = 277.777771f;
                            pp.m_WalkSpeed = 1.66667f;
                            pp.m_Weights = CitizenUtils.GetPathfindWeights(c, hh, hhc.Length);
                            pp.m_Methods = PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(m_TimeOfDay);
                            pp.m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
                            pp.m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost;

                            SetupQueueTarget origin = new SetupQueueTarget { m_Type = SetupTargetType.CurrentLocation, m_Methods = PathMethod.Pedestrian, m_RandomCost = 30f };
                            SetupQueueTarget destination = new SetupQueueTarget { m_Methods = PathMethod.Pedestrian, m_RandomCost = 30f, m_ActivityMask = cd.m_SupportedActivities };

                            switch (currentTrip.m_Purpose)
                            {
                                case Purpose.GoingHome: destination.m_Type = SetupTargetType.OutsideConnection; break;
                                case Purpose.Hospital: destination.m_Entity = FindDistrict(currentBuilding); destination.m_Type = SetupTargetType.Hospital; break;
                                case Purpose.Safety: case Purpose.Escape: destination.m_Type = SetupTargetType.Safety; break;
                                case Purpose.EmergencyShelter: pp.m_Weights = new PathfindWeights(1f, 0f, 0f, 0f); destination.m_Entity = FindDistrict(currentBuilding); destination.m_Type = SetupTargetType.EmergencyShelter; break;
                                case Purpose.Crime: destination.m_Type = SetupTargetType.CrimeProducer; break;
                                case Purpose.Sightseeing: destination.m_Type = SetupTargetType.Sightseeing; break;
                                case Purpose.VisitAttractions: destination.m_Type = SetupTargetType.Attraction; break;
                            }

                            if (m_PropertyRenters.TryGetComponent(household, out var rData)) pp.m_Authorization1 = rData.m_Property;
                            if (m_Workers.HasComponent(citizenEntity))
                            {
                                Worker w = m_Workers[citizenEntity];
                                pp.m_Authorization2 = m_PropertyRenters.HasComponent(w.m_Workplace) ? m_PropertyRenters[w.m_Workplace].m_Property : w.m_Workplace;
                            }

                            // [Fix] 补全泛目标时的车辆/自行车检查逻辑
                            // 如果市民有车，必须允许其开车去医院/庇护所
                            if (m_CarKeepers.IsComponentEnabled(citizenEntity))
                            {
                                Entity car = m_CarKeepers[citizenEntity].m_Car;
                                if (m_ParkedCarData.HasComponent(car))
                                {
                                    PrefabRef carPrefab = m_PrefabRefData[car];
                                    CarData carData = m_PrefabCarData[carPrefab.m_Prefab];
                                    ParkedCar parkedCar = m_ParkedCarData[car];
                                    pp.m_MaxSpeed.x = carData.m_MaxSpeed;
                                    pp.m_ParkingTarget = parkedCar.m_Lane;
                                    pp.m_ParkingDelta = parkedCar.m_CurvePosition;
                                    pp.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref m_PrefabRefData, ref m_ObjectGeometryData);
                                    pp.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
                                    pp.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
                                    if (m_PersonalCarData.TryGetComponent(car, out var pCar) && (pCar.m_State & PersonalCarFlags.HomeTarget) == 0) pp.m_PathfindFlags |= PathfindFlags.ParkingReset;
                                }
                            }
                            else if (m_BicycleOwners.IsComponentEnabled(citizenEntity))
                            {
                                Entity bicycle = m_BicycleOwners[citizenEntity].m_Bicycle;
                                // 泛目标情况下无法像 Case A 那样动态生成自行车(需要确定的 Property)，因此仅处理已有自行车
                                if (m_PrefabRefData.HasComponent(bicycle))
                                {
                                    PrefabRef bPrefab = m_PrefabRefData[bicycle];
                                    if (m_PrefabCarData.TryGetComponent(bPrefab.m_Prefab, out var bCarData) && m_ObjectGeometryData.TryGetComponent(bPrefab.m_Prefab, out var bGeo))
                                    {
                                        pp.m_MaxSpeed.x = bCarData.m_MaxSpeed;
                                        pp.m_ParkingSize = VehicleUtils.GetParkingSize(bGeo, out offset);
                                        pp.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
                                        pp.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
                                        if (m_ParkedCarData.TryGetComponent(bicycle, out var bParked))
                                        {
                                            pp.m_ParkingTarget = bParked.m_Lane;
                                            pp.m_ParkingDelta = bParked.m_CurvePosition;
                                            if (m_PersonalCarData.TryGetComponent(bicycle, out var bpCar) && (bpCar.m_State & PersonalCarFlags.HomeTarget) == 0) pp.m_PathfindFlags |= PathfindFlags.ParkingReset;
                                        }
                                        else if (!CollectionUtils.TryGet(currentTransports, i, out var ct) || !m_PrefabRefData.HasComponent(ct.m_CurrentTransport) || m_Deleteds.HasComponent(ct.m_CurrentTransport))
                                        {
                                            origin.m_Methods |= PathMethod.Bicycle; origin.m_RoadTypes |= RoadTypes.Bicycle;
                                        }
                                    }
                                }
                            }

                            m_PathQueue.Enqueue(new SetupQueueItem(citizenEntity, pp, origin, destination));
                            m_CommandBuffer.AddComponent(unfilteredChunkIndex, citizenEntity, new Target { m_Target = Entity.Null });
                        }
                        else
                        {
                            RemoveAllTrips(trips);
                        }
                    }
                }
            }

            #endregion
        }

        #endregion

        #region HarmonyPatch (Prefix方式)
        // ==============================================================================
        // Harmony 补丁 (Prefix方式)
        // ==============================================================================
        [HarmonyPatch(typeof(TargetSystem))]
        public static class Patches
        {
            // 辅助判断：只拦截对应的原版系统实例
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool ShouldRedirect(object instance)
            {
                return Instance != null && instance.GetType() == typeof(TargetSystem);
            }

            // Patch: debugDisableSpawning (Getter)
            [HarmonyPatch(nameof(TargetSystem.debugDisableSpawning), MethodType.Getter)]
            [HarmonyPrefix]
            public static bool Prefix_get_debugDisableSpawning(TargetSystem __instance,
                                                ref bool __result)
            {
                if (ShouldRedirect(__instance))
                {
                    // 返回 Mod 系统的计算结果
                    __result = ModSystem.Instance.debugDisableSpawning;
                    return false; // 拦截原版执行
                }
                return true;
            }

        } // nested patch class

        #endregion
    }
}
