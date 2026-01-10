// Game.Tools.ValidationSystem;

using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Tools.ValidationSystem;

// ValidationJob -> Game.Buildings.ValidationHelpers.ValidateBuilding -> GroundWaterSystem.GetGroundWater -> kMapSize/kTextureSize
// v2.1.1 mod新增遗漏

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    /// <summary>
    /// 并行作业，用于在一组原型块（archetype chunks）中对各种实体、对象和组件执行验证检查。
    /// </summary>
    /// <remarks>ValidationJob 检查多种条件和约束，例如对象有效性、升级资格、区域与边缘的正确性，以及水源需求。 验证结果和错误通过线程安全的错误队列进行报告。</remarks>
    [BurstCompile]
    public struct ValidationJob : IJobParallelForDefer
    {
        [ReadOnly]
        public bool m_EditorMode;

        [ReadOnly]
        public bool m_AllowEditBuiltinPrefabs;

        [ReadOnly]
        public NativeArray<ArchetypeChunk> m_Chunks;

        [ReadOnly]
        public ChunkType m_ChunkType;

        [ReadOnly]
        public EntityData m_EntityData;

        [ReadOnly]
        public NativeList<BoundsData> m_EdgeList;

        [ReadOnly]
        public NativeList<BoundsData> m_ObjectList;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ObjectSearchTree;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

        [ReadOnly]
        public NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> m_AreaSearchTree;

        [ReadOnly]
        public NativeParallelHashMap<Entity, int> m_InstanceCounts;

        [ReadOnly]
        public WaterSurfaceData<SurfaceWater> m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public NativeArray<GroundWater> m_GroundWaterMap;

        [ReadOnly]
        public Bounds3 m_worldBounds;

        public NativeQueue<ErrorData>.ParallelWriter m_ErrorQueue;

        [NativeDisableContainerSafetyRestriction]
        private NativeList<ConnectedNode> m_TempNodes;

        public void Execute(int index)
        {
            ArchetypeChunk archetypeChunk = this.m_Chunks[index];
            TempFlags tempFlags = (archetypeChunk.Has(ref this.m_ChunkType.m_Native) ? (TempFlags.Select | TempFlags.Duplicate) : (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate));
            if (archetypeChunk.Has(ref this.m_ChunkType.m_Object))
            {
                NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<Temp> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Temp);
                NativeArray<Owner> nativeArray3 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Owner);
                NativeArray<Transform> nativeArray4 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Transform);
                NativeArray<Attached> nativeArray5 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Attached);
                NativeArray<Game.Objects.NetObject> nativeArray6 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_NetObject);
                NativeArray<PrefabRef> nativeArray7 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_PrefabRef);
                NativeArray<Building> nativeArray8 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Building);
                bool flag = archetypeChunk.Has(ref this.m_ChunkType.m_OutsideConnection);
                for (int i = 0; i < nativeArray.Length; i++)
                {
                    Entity entity = nativeArray[i];

                    Temp temp = nativeArray2[i];
                    if ((temp.m_Flags & tempFlags) == 0)
                    {
                        Transform transform = nativeArray4[i];
                        PrefabRef prefabRef = nativeArray7[i];
                        Owner owner = default(Owner);
                        if (nativeArray3.Length != 0)
                        {
                            owner = nativeArray3[i];
                        }
                        Attached attached = default(Attached);
                        if (nativeArray5.Length != 0)
                        {
                            attached = nativeArray5[i];
                        }
                        Game.Objects.ValidationHelpers.ValidateObject(entity, temp, owner, transform, prefabRef, attached, flag, this.m_EditorMode, this.m_EntityData, this.m_EdgeList, this.m_ObjectList, this.m_ObjectSearchTree, this.m_NetSearchTree, this.m_AreaSearchTree, this.m_InstanceCounts, this.m_WaterSurfaceData, this.m_TerrainHeightData, this.m_ErrorQueue);
                    }
                    if ((temp.m_Flags & (TempFlags.Delete | TempFlags.Modify | TempFlags.Replace | TempFlags.Upgrade)) != 0 && temp.m_Original != Entity.Null && this.m_EntityData.m_OnFire.HasComponent(temp.m_Original))
                    {
                        ErrorData value = default(ErrorData);
                        value.m_ErrorType = ErrorType.OnFire;
                        value.m_ErrorSeverity = ErrorSeverity.Error;
                        value.m_TempEntity = entity;
                        value.m_Position = float.NaN;
                        this.m_ErrorQueue.Enqueue(value);
                    }
                    if (this.m_EditorMode && (temp.m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Upgrade | TempFlags.Cancel | TempFlags.Duplicate)) == TempFlags.Upgrade && this.m_EntityData.m_PrefabRef.TryGetComponent(temp.m_Original, out var componentData) && this.m_EntityData.m_PrefabObjectGeometry.TryGetComponent(componentData.m_Prefab, out var componentData2) && (componentData2.m_Flags & Game.Objects.GeometryFlags.ReadOnly) != 0 && ((componentData2.m_Flags & Game.Objects.GeometryFlags.Builtin) == 0 || !this.m_AllowEditBuiltinPrefabs) && ApplyObjectsSystem.ShouldSaveInstance(entity, temp.m_Original, ref this.m_EntityData.m_Owner, ref this.m_EntityData.m_ServiceUpgrade, ref this.m_EntityData.m_Upgrades))
                    {
                        ErrorData value2 = default(ErrorData);
                        value2.m_ErrorType = ErrorType.NotEditable;
                        value2.m_ErrorSeverity = ErrorSeverity.Error;
                        value2.m_TempEntity = entity;
                        value2.m_Position = float.NaN;
                        this.m_ErrorQueue.Enqueue(value2);
                    }

                }
                for (int j = 0; j < nativeArray8.Length; j++)
                {
                    if ((nativeArray2[j].m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0)
                    {
                        Entity entity2 = nativeArray[j];
                        Building building = nativeArray8[j];
                        Transform transform2 = nativeArray4[j];
                        PrefabRef prefabRef2 = nativeArray7[j];
                        
                        // mod 重定向
                        ValidateBuilding(entity2, building, transform2, prefabRef2, this.m_EntityData, this.m_GroundWaterMap, this.m_ErrorQueue);
                        // mod end;
                    }
                }
                for (int k = 0; k < nativeArray6.Length; k++)
                {
                    if ((nativeArray2[k].m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0)
                    {
                        Entity entity3 = nativeArray[k];
                        Game.Objects.NetObject netObject = nativeArray6[k];
                        Transform transform3 = nativeArray4[k];
                        PrefabRef prefabRef3 = nativeArray7[k];
                        Owner owner2 = default(Owner);
                        if (nativeArray3.Length != 0)
                        {
                            owner2 = nativeArray3[k];
                        }
                        Attached attached2 = default(Attached);
                        if (nativeArray5.Length != 0)
                        {
                            attached2 = nativeArray5[k];
                        }
                        Game.Objects.ValidationHelpers.ValidateNetObject(entity3, owner2, netObject, transform3, prefabRef3, attached2, this.m_EntityData, this.m_ErrorQueue);
                    }
                }
                if (archetypeChunk.Has(ref this.m_ChunkType.m_TransportStop))
                {
                    for (int l = 0; l < nativeArray.Length; l++)
                    {
                        Temp temp2 = nativeArray2[l];
                        if ((temp2.m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0)
                        {
                            Entity entity4 = nativeArray[l];
                            Transform transform4 = nativeArray4[l];
                            PrefabRef prefabRef4 = nativeArray7[l];
                            Owner owner3 = default(Owner);
                            if (nativeArray3.Length != 0)
                            {
                                owner3 = nativeArray3[l];
                            }
                            Attached attached3 = default(Attached);
                            if (nativeArray5.Length != 0)
                            {
                                attached3 = nativeArray5[l];
                            }
                            Game.Routes.ValidationHelpers.ValidateStop(this.m_EditorMode, entity4, temp2, owner3, transform4, prefabRef4, attached3, this.m_EntityData, this.m_ErrorQueue);
                        }
                    }
                }
                if (flag)
                {
                    for (int m = 0; m < nativeArray.Length; m++)
                    {
                        if ((nativeArray2[m].m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0)
                        {
                            Entity entity5 = nativeArray[m];
                            Transform transform5 = nativeArray4[m];
                            Game.Objects.ValidationHelpers.ValidateOutsideConnection(entity5, transform5, this.m_TerrainHeightData, this.m_ErrorQueue);
                        }
                    }
                }
            }
            if (archetypeChunk.Has(ref this.m_ChunkType.m_ServiceUpgrade))
            {
                NativeArray<Entity> nativeArray9 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<PrefabRef> nativeArray10 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_PrefabRef);
                NativeArray<Owner> nativeArray11 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Owner);
                for (int n = 0; n < nativeArray9.Length; n++)
                {
                    Entity entity6 = nativeArray9[n];
                    PrefabRef prefabRef5 = nativeArray10[n];
                    Owner owner4 = default(Owner);
                    if (nativeArray11.Length != 0)
                    {
                        owner4 = nativeArray11[n];
                    }
                    Game.Buildings.ValidationHelpers.ValidateUpgrade(entity6, owner4, prefabRef5, this.m_EntityData, this.m_ErrorQueue);
                }
            }
            if (archetypeChunk.Has(ref this.m_ChunkType.m_Edge))
            {
                NativeArray<Entity> nativeArray12 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<Temp> nativeArray13 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Temp);
                NativeArray<Owner> nativeArray14 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Owner);
                NativeArray<Edge> nativeArray15 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Edge);
                NativeArray<EdgeGeometry> nativeArray16 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_EdgeGeometry);
                NativeArray<StartNodeGeometry> nativeArray17 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_StartNodeGeometry);
                NativeArray<EndNodeGeometry> nativeArray18 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_EndNodeGeometry);
                NativeArray<Composition> nativeArray19 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Composition);
                NativeArray<Fixed> nativeArray20 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Fixed);
                NativeArray<PrefabRef> nativeArray21 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_PrefabRef);
                if (!this.m_TempNodes.IsCreated)
                {
                    this.m_TempNodes = new NativeList<ConnectedNode>(16, Allocator.Temp);
                }
                bool flag2 = nativeArray20.Length != 0;
                for (int num = 0; num < nativeArray16.Length; num++)
                {
                    Temp temp3 = nativeArray13[num];
                    if ((temp3.m_Flags & tempFlags) == 0)
                    {
                        Entity entity7 = nativeArray12[num];
                        Edge edge = nativeArray15[num];
                        EdgeGeometry edgeGeometry = nativeArray16[num];
                        StartNodeGeometry startNodeGeometry = nativeArray17[num];
                        EndNodeGeometry endNodeGeometry = nativeArray18[num];
                        Composition composition = nativeArray19[num];
                        PrefabRef prefabRef6 = nativeArray21[num];
                        Owner owner5 = default(Owner);
                        if (nativeArray14.Length != 0)
                        {
                            owner5 = nativeArray14[num];
                        }
                        Fixed obj = new Fixed
                        {
                            m_Index = -1
                        };
                        if (flag2)
                        {
                            obj = nativeArray20[num];
                        }
                        Game.Net.ValidationHelpers.ValidateEdge(entity7, temp3, owner5, obj, edge, edgeGeometry, startNodeGeometry, endNodeGeometry, composition, prefabRef6, this.m_EditorMode, this.m_EntityData, this.m_EdgeList, this.m_ObjectSearchTree, this.m_NetSearchTree, this.m_AreaSearchTree, this.m_WaterSurfaceData, this.m_TerrainHeightData, this.m_ErrorQueue, this.m_TempNodes);
                    }
                }
            }
            if (archetypeChunk.Has(ref this.m_ChunkType.m_Lane))
            {
                NativeArray<Entity> nativeArray22 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<Temp> nativeArray23 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Temp);
                NativeArray<Owner> nativeArray24 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Owner);
                NativeArray<Lane> nativeArray25 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Lane);
                NativeArray<Game.Net.TrackLane> nativeArray26 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_TrackLane);
                NativeArray<Curve> nativeArray27 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Curve);
                NativeArray<EdgeLane> nativeArray28 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_EdgeLane);
                NativeArray<PrefabRef> nativeArray29 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_PrefabRef);
                for (int num2 = 0; num2 < nativeArray26.Length; num2++)
                {
                    if ((nativeArray23[num2].m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0)
                    {
                        Entity entity8 = nativeArray22[num2];
                        Lane lane = nativeArray25[num2];
                        Game.Net.TrackLane trackLane = nativeArray26[num2];
                        Curve curve = nativeArray27[num2];
                        PrefabRef prefabRef7 = nativeArray29[num2];
                        Owner owner6 = default(Owner);
                        if (nativeArray24.Length != 0)
                        {
                            owner6 = nativeArray24[num2];
                        }
                        EdgeLane edgeLane = default(EdgeLane);
                        if (nativeArray28.Length != 0)
                        {
                            edgeLane = nativeArray28[num2];
                        }
                        Game.Net.ValidationHelpers.ValidateLane(entity8, owner6, lane, trackLane, curve, edgeLane, prefabRef7, this.m_EntityData, this.m_ErrorQueue);
                    }
                }
            }
            if (archetypeChunk.Has(ref this.m_ChunkType.m_Area))
            {
                NativeArray<Entity> nativeArray30 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<Temp> nativeArray31 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Temp);
                NativeArray<Owner> nativeArray32 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Owner);
                NativeArray<Area> nativeArray33 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Area);
                NativeArray<Geometry> nativeArray34 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_AreaGeometry);
                NativeArray<Storage> nativeArray35 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_AreaStorage);
                BufferAccessor<Game.Areas.Node> bufferAccessor = archetypeChunk.GetBufferAccessor(ref this.m_ChunkType.m_AreaNode);
                NativeArray<PrefabRef> nativeArray36 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_PrefabRef);
                for (int num3 = 0; num3 < nativeArray30.Length; num3++)
                {
                    Temp temp4 = nativeArray31[num3];
                    if ((temp4.m_Flags & tempFlags) == 0)
                    {
                        Entity entity9 = nativeArray30[num3];
                        Area area = nativeArray33[num3];
                        DynamicBuffer<Game.Areas.Node> nodes = bufferAccessor[num3];
                        PrefabRef prefabRef8 = nativeArray36[num3];
                        Geometry geometry = default(Geometry);
                        if (nativeArray34.Length != 0)
                        {
                            geometry = nativeArray34[num3];
                        }
                        Storage storage = default(Storage);
                        if (nativeArray35.Length != 0)
                        {
                            storage = nativeArray35[num3];
                        }
                        Owner owner7 = default(Owner);
                        if (nativeArray32.Length != 0)
                        {
                            owner7 = nativeArray32[num3];
                        }
                        Game.Areas.ValidationHelpers.ValidateArea(this.m_EditorMode, entity9, temp4, owner7, area, geometry, storage, nodes, prefabRef8, this.m_EntityData, this.m_ObjectSearchTree, this.m_NetSearchTree, this.m_AreaSearchTree, this.m_WaterSurfaceData, this.m_TerrainHeightData, this.m_ErrorQueue);
                    }
                }
            }
            if (archetypeChunk.Has(ref this.m_ChunkType.m_RouteSegment))
            {
                NativeArray<Entity> nativeArray37 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<Temp> nativeArray38 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Temp);
                NativeArray<PrefabRef> nativeArray39 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_PrefabRef);
                BufferAccessor<RouteWaypoint> bufferAccessor2 = archetypeChunk.GetBufferAccessor(ref this.m_ChunkType.m_RouteWaypoint);
                BufferAccessor<RouteSegment> bufferAccessor3 = archetypeChunk.GetBufferAccessor(ref this.m_ChunkType.m_RouteSegment);
                for (int num4 = 0; num4 < nativeArray37.Length; num4++)
                {
                    Temp temp5 = nativeArray38[num4];
                    if ((temp5.m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0)
                    {
                        Game.Routes.ValidationHelpers.ValidateRoute(nativeArray37[num4], prefabRef: nativeArray39[num4], waypoints: bufferAccessor2[num4], segments: bufferAccessor3[num4], temp: temp5, data: this.m_EntityData, errorQueue: this.m_ErrorQueue);
                    }
                }
            }
            if (!this.m_EditorMode && archetypeChunk.Has(ref this.m_ChunkType.m_Brush))
            {
                NativeArray<Entity> nativeArray40 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
                NativeArray<Brush> nativeArray41 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Brush);
                for (int num5 = 0; num5 < nativeArray40.Length; num5++)
                {
                    Brush brush = nativeArray41[num5];
                    if (this.m_EntityData.m_TerraformingData.HasComponent(brush.m_Tool))
                    {
                        Entity entity10 = nativeArray40[num5];
                        Bounds3 bounds = new Bounds3(brush.m_Position - brush.m_Size * 0.4f, brush.m_Position + brush.m_Size * 0.4f);
                        Game.Areas.ValidationHelpers.BrushAreaIterator iterator = new Game.Areas.ValidationHelpers.BrushAreaIterator
                        {
                            m_BrushEntity = entity10,
                            m_Brush = brush,
                            m_BrushBounds = bounds,
                            m_Data = this.m_EntityData,
                            m_ErrorQueue = this.m_ErrorQueue
                        };
                        this.m_AreaSearchTree.Iterate(ref iterator);
                        Game.Objects.ValidationHelpers.ValidateWorldBounds(entity10, default(Owner), bounds, this.m_EntityData, this.m_TerrainHeightData, this.m_ErrorQueue);
                    }
                }
            }
            if (!archetypeChunk.Has(ref this.m_ChunkType.m_WaterSourceData))
            {
                return;
            }
            NativeArray<Entity> nativeArray42 = archetypeChunk.GetNativeArray(this.m_ChunkType.m_Entity);
            NativeArray<Temp> nativeArray43 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Temp);
            NativeArray<Transform> nativeArray44 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_Transform);
            NativeArray<Game.Simulation.WaterSourceData> nativeArray45 = archetypeChunk.GetNativeArray(ref this.m_ChunkType.m_WaterSourceData);
            for (int num6 = 0; num6 < nativeArray42.Length; num6++)
            {
                Temp temp6 = nativeArray43[num6];
                if ((temp6.m_Flags & (TempFlags.Delete | TempFlags.Select | TempFlags.Duplicate)) == 0 || (temp6.m_Flags & TempFlags.Dragging) != 0)
                {
                    Entity entity11 = nativeArray42[num6];
                    Transform transform6 = nativeArray44[num6];
                    Game.Simulation.WaterSourceData waterSourceData = nativeArray45[num6];
                    Game.Objects.ValidationHelpers.ValidateWaterSource(entity11, transform6, waterSourceData, this.m_TerrainHeightData, this.m_worldBounds, this.m_ErrorQueue);
                }
            }
        }

        public static void ValidateBuilding(Entity entity, Building building, Transform transform, PrefabRef prefabRef, EntityData data, NativeArray<GroundWater> groundWaterMap, NativeQueue<ErrorData>.ParallelWriter errorQueue)
        {
            if (building.m_RoadEdge == Entity.Null)
            {
                BuildingData buildingData = data.m_PrefabBuilding[prefabRef.m_Prefab];
                if ((buildingData.m_Flags & Game.Prefabs.BuildingFlags.RequireRoad) != 0)
                {
                    float3 position = BuildingUtils.CalculateFrontPosition(transform, buildingData.m_LotSize.y);
                    bool num = (buildingData.m_Flags & (Game.Prefabs.BuildingFlags.CanBeOnRoad | Game.Prefabs.BuildingFlags.CanBeOnRoadArea)) != 0;
                    bool flag = (buildingData.m_Flags & Game.Prefabs.BuildingFlags.CanBeRoadSide) != 0;
                    if (num && !flag)
                    {
                        position = transform.m_Position;
                    }
                    errorQueue.Enqueue(new ErrorData
                    {
                        m_ErrorSeverity = ErrorSeverity.Warning,
                        m_ErrorType = ErrorType.NoRoadAccess,
                        m_TempEntity = entity,
                        m_Position = position
                    });
                }
            }

            // mod GroundWaterSystem.GetGroundWater
            if (((data.m_WaterPumpingStationData.TryGetComponent(prefabRef.m_Prefab, out var componentData) && (componentData.m_Types & AllowedWaterTypes.Groundwater) != AllowedWaterTypes.None) || data.m_GroundWaterPoweredData.HasComponent(prefabRef.m_Prefab)) && CellMapSystemRe.GroundWaterSystemGetGroundWater(transform.m_Position, groundWaterMap).m_Max <= 500)
            {
                errorQueue.Enqueue(new ErrorData
                {
                    m_ErrorSeverity = ErrorSeverity.Error,
                    m_ErrorType = ErrorType.NoGroundWater,
                    m_TempEntity = entity,
                    m_Position = transform.m_Position
                });
            }
        }

    }

}
