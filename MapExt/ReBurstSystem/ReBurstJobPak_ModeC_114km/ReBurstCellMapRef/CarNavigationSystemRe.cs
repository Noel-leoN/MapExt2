using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Simulation.CarNavigationSystem;
using static MapExtPDX.MapExt.ReBurstSystemModeC.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    [BurstCompile]
    public struct ApplyTrafficAmbienceJob : IJob
    {
        public NativeQueue<TrafficAmbienceEffect> m_EffectsQueue;

        public NativeArray<TrafficAmbienceCell> m_TrafficAmbienceMap;

        public void Execute()
        {
            TrafficAmbienceEffect item;
            while (m_EffectsQueue.TryDequeue(out item))
            {
                int2 cell = CellMapSystem<TrafficAmbienceCell>.GetCell(item.m_Position, kMapSize, TrafficAmbienceSystem.kTextureSize);
                if (cell.x >= 0 && cell.y >= 0 && cell.x < TrafficAmbienceSystem.kTextureSize && cell.y < TrafficAmbienceSystem.kTextureSize)
                {
                    int index = cell.x + cell.y * TrafficAmbienceSystem.kTextureSize;
                    TrafficAmbienceCell value = m_TrafficAmbienceMap[index];
                    value.m_Accumulator += item.m_Amount;
                    m_TrafficAmbienceMap[index] = value;
                }
            }
        }
    }

}
/*
		[Preserve]
		protected override void OnUpdate()
		{
			uint index = this.m_SimulationSystem.frameIndex % 16u;
			this.m_VehicleQuery.ResetFilter();
			this.m_VehicleQuery.SetSharedComponentFilter(new UpdateFrame(index));
			this.m_Actions.m_LaneReservationQueue = new NativeQueue<CarNavigationHelpers.LaneReservation>(Allocator.TempJob);
			this.m_Actions.m_LaneEffectsQueue = new NativeQueue<CarNavigationHelpers.LaneEffects>(Allocator.TempJob);
			this.m_Actions.m_LaneSignalQueue = new NativeQueue<CarNavigationHelpers.LaneSignal>(Allocator.TempJob);
			this.m_Actions.m_TrafficAmbienceQueue = new NativeQueue<TrafficAmbienceEffect>(Allocator.TempJob);
			this.__TypeHandle.__Game_Objects_BlockedLane_RW_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_CarTrailerLane_RW_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Areas_Triangle_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Areas_Node_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_LaneOverlap_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_LaneObject_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_SubLane_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_ParkingLaneData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_CarLaneData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_VehicleSideEffectData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_TrainData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_CarData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Creatures_Creature_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Vehicle_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Controller_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Train_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Car_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Moving_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Routes_Position_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_Road_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_LaneSignal_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_LaneCondition_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_LaneReservation_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_Curve_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_AreaLane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_SlaveLane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_MasterLane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_ConnectionLane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_ParkingLane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_CarLane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_Lane_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Unspawned_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Common_Owner_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__EntityStorageInfoLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Pathfind_PathElement_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_CarNavigationLane_RW_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Odometer_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Blocker_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Pathfind_PathOwner_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_CarCurrentLane_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_CarNavigation_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_LayoutElement_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Common_PseudoRandomSeed_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_OutOfControl_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Vehicles_Car_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Common_Target_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Moving_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
			UpdateNavigationJob jobData = default(UpdateNavigationJob);
			jobData.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
			jobData.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
			jobData.m_MovingType = this.__TypeHandle.__Game_Objects_Moving_RO_ComponentTypeHandle;
			jobData.m_TargetType = this.__TypeHandle.__Game_Common_Target_RO_ComponentTypeHandle;
			jobData.m_CarType = this.__TypeHandle.__Game_Vehicles_Car_RO_ComponentTypeHandle;
			jobData.m_OutOfControlType = this.__TypeHandle.__Game_Vehicles_OutOfControl_RO_ComponentTypeHandle;
			jobData.m_PseudoRandomSeedType = this.__TypeHandle.__Game_Common_PseudoRandomSeed_RO_ComponentTypeHandle;
			jobData.m_PrefabRefType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
			jobData.m_LayoutElementType = this.__TypeHandle.__Game_Vehicles_LayoutElement_RO_BufferTypeHandle;
			jobData.m_NavigationType = this.__TypeHandle.__Game_Vehicles_CarNavigation_RW_ComponentTypeHandle;
			jobData.m_CurrentLaneType = this.__TypeHandle.__Game_Vehicles_CarCurrentLane_RW_ComponentTypeHandle;
			jobData.m_PathOwnerType = this.__TypeHandle.__Game_Pathfind_PathOwner_RW_ComponentTypeHandle;
			jobData.m_BlockerType = this.__TypeHandle.__Game_Vehicles_Blocker_RW_ComponentTypeHandle;
			jobData.m_OdometerType = this.__TypeHandle.__Game_Vehicles_Odometer_RW_ComponentTypeHandle;
			jobData.m_NavigationLaneType = this.__TypeHandle.__Game_Vehicles_CarNavigationLane_RW_BufferTypeHandle;
			jobData.m_PathElementType = this.__TypeHandle.__Game_Pathfind_PathElement_RW_BufferTypeHandle;
			jobData.m_EntityStorageInfoLookup = this.__TypeHandle.__EntityStorageInfoLookup;
			jobData.m_OwnerData = this.__TypeHandle.__Game_Common_Owner_RO_ComponentLookup;
			jobData.m_UnspawnedData = this.__TypeHandle.__Game_Objects_Unspawned_RO_ComponentLookup;
			jobData.m_LaneData = this.__TypeHandle.__Game_Net_Lane_RO_ComponentLookup;
			jobData.m_CarLaneData = this.__TypeHandle.__Game_Net_CarLane_RO_ComponentLookup;
			jobData.m_ParkingLaneData = this.__TypeHandle.__Game_Net_ParkingLane_RO_ComponentLookup;
			jobData.m_ConnectionLaneData = this.__TypeHandle.__Game_Net_ConnectionLane_RO_ComponentLookup;
			jobData.m_MasterLaneData = this.__TypeHandle.__Game_Net_MasterLane_RO_ComponentLookup;
			jobData.m_SlaveLaneData = this.__TypeHandle.__Game_Net_SlaveLane_RO_ComponentLookup;
			jobData.m_AreaLaneData = this.__TypeHandle.__Game_Net_AreaLane_RO_ComponentLookup;
			jobData.m_CurveData = this.__TypeHandle.__Game_Net_Curve_RO_ComponentLookup;
			jobData.m_LaneReservationData = this.__TypeHandle.__Game_Net_LaneReservation_RO_ComponentLookup;
			jobData.m_LaneConditionData = this.__TypeHandle.__Game_Net_LaneCondition_RO_ComponentLookup;
			jobData.m_LaneSignalData = this.__TypeHandle.__Game_Net_LaneSignal_RO_ComponentLookup;
			jobData.m_RoadData = this.__TypeHandle.__Game_Net_Road_RO_ComponentLookup;
			jobData.m_PropertyRenterData = this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup;
			jobData.m_TransformData = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
			jobData.m_PositionData = this.__TypeHandle.__Game_Routes_Position_RO_ComponentLookup;
			jobData.m_MovingData = this.__TypeHandle.__Game_Objects_Moving_RO_ComponentLookup;
			jobData.m_CarData = this.__TypeHandle.__Game_Vehicles_Car_RO_ComponentLookup;
			jobData.m_TrainData = this.__TypeHandle.__Game_Vehicles_Train_RO_ComponentLookup;
			jobData.m_ControllerData = this.__TypeHandle.__Game_Vehicles_Controller_RO_ComponentLookup;
			jobData.m_VehicleData = this.__TypeHandle.__Game_Vehicles_Vehicle_RO_ComponentLookup;
			jobData.m_CreatureData = this.__TypeHandle.__Game_Creatures_Creature_RO_ComponentLookup;
			jobData.m_PrefabRefData = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
			jobData.m_PrefabCarData = this.__TypeHandle.__Game_Prefabs_CarData_RO_ComponentLookup;
			jobData.m_PrefabTrainData = this.__TypeHandle.__Game_Prefabs_TrainData_RO_ComponentLookup;
			jobData.m_PrefabBuildingData = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup;
			jobData.m_PrefabObjectGeometryData = this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;
			jobData.m_PrefabSideEffectData = this.__TypeHandle.__Game_Prefabs_VehicleSideEffectData_RO_ComponentLookup;
			jobData.m_PrefabLaneData = this.__TypeHandle.__Game_Prefabs_NetLaneData_RO_ComponentLookup;
			jobData.m_PrefabCarLaneData = this.__TypeHandle.__Game_Prefabs_CarLaneData_RO_ComponentLookup;
			jobData.m_PrefabParkingLaneData = this.__TypeHandle.__Game_Prefabs_ParkingLaneData_RO_ComponentLookup;
			jobData.m_Lanes = this.__TypeHandle.__Game_Net_SubLane_RO_BufferLookup;
			jobData.m_LaneObjects = this.__TypeHandle.__Game_Net_LaneObject_RO_BufferLookup;
			jobData.m_LaneOverlaps = this.__TypeHandle.__Game_Net_LaneOverlap_RO_BufferLookup;
			jobData.m_AreaNodes = this.__TypeHandle.__Game_Areas_Node_RO_BufferLookup;
			jobData.m_AreaTriangles = this.__TypeHandle.__Game_Areas_Triangle_RO_BufferLookup;
			jobData.m_TrailerLaneData = this.__TypeHandle.__Game_Vehicles_CarTrailerLane_RW_ComponentLookup;
			jobData.m_BlockedLanes = this.__TypeHandle.__Game_Objects_BlockedLane_RW_BufferLookup;
			jobData.m_RandomSeed = RandomSeed.Next();
			jobData.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			jobData.m_LeftHandTraffic = this.m_CityConfigurationSystem.leftHandTraffic;
			jobData.m_NetSearchTree = this.m_NetSearchSystem.GetNetSearchTree(readOnly: true, out var dependencies);
			jobData.m_AreaSearchTree = this.m_AreaSearchSystem.GetSearchTree(readOnly: true, out var dependencies2);
			jobData.m_StaticObjectSearchTree = this.m_ObjectSearchSystem.GetStaticSearchTree(readOnly: true, out var dependencies3);
			jobData.m_MovingObjectSearchTree = this.m_ObjectSearchSystem.GetMovingSearchTree(readOnly: true, out var dependencies4);
			jobData.m_TerrainHeightData = this.m_TerrainSystem.GetHeightData();
			jobData.m_LaneObjectBuffer = this.m_Actions.m_LaneObjectUpdater.Begin(Allocator.TempJob);
			jobData.m_LaneReservations = this.m_Actions.m_LaneReservationQueue.AsParallelWriter();
			jobData.m_LaneEffects = this.m_Actions.m_LaneEffectsQueue.AsParallelWriter();
			jobData.m_LaneSignals = this.m_Actions.m_LaneSignalQueue.AsParallelWriter();
			jobData.m_TrafficAmbienceEffects = this.m_Actions.m_TrafficAmbienceQueue.AsParallelWriter();
			JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, this.m_VehicleQuery, JobUtils.CombineDependencies(base.Dependency, dependencies, dependencies2, dependencies3, dependencies4));
			this.m_NetSearchSystem.AddNetSearchTreeReader(jobHandle);
			this.m_AreaSearchSystem.AddSearchTreeReader(jobHandle);
			this.m_ObjectSearchSystem.AddStaticSearchTreeReader(jobHandle);
			this.m_ObjectSearchSystem.AddMovingSearchTreeReader(jobHandle);
			this.m_TerrainSystem.AddCPUHeightReader(jobHandle);
			this.m_Actions.m_Dependency = jobHandle;
			base.Dependency = jobHandle;
		}
*/