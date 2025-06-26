using System.Drawing.Text;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Tools.AreaToolSystem;

/// 地图大小变化时请修改Execute()内设置！

using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct CreateDefinitionsJob : IJob
    {
        [ReadOnly]
        public bool m_AllowCreateArea;

        [ReadOnly]
        public bool m_EditorMode;

        [ReadOnly]
        public Mode m_Mode;

        [ReadOnly]
        public State m_State;

        [ReadOnly]
        public Entity m_Prefab;

        [ReadOnly]
        public Entity m_Recreate;

        [NativeDisableContainerSafetyRestriction]
        [ReadOnly]
        public NativeArray<Entity> m_ApplyTempAreas;

        [NativeDisableContainerSafetyRestriction]
        [ReadOnly]
        public NativeArray<Entity> m_ApplyTempBuildings;

        [ReadOnly]
        public NativeList<ControlPoint> m_MoveStartPositions;

        [ReadOnly]
        public ComponentLookup<Temp> m_TempData;

        [ReadOnly]
        public ComponentLookup<Owner> m_OwnerData;

        [ReadOnly]
        public ComponentLookup<Clear> m_ClearData;

        [ReadOnly]
        public ComponentLookup<Space> m_SpaceData;

        [ReadOnly]
        public ComponentLookup<Area> m_AreaData;

        [ReadOnly]
        public ComponentLookup<Game.Net.Node> m_NodeData;

        [ReadOnly]
        public ComponentLookup<Edge> m_EdgeData;

        [ReadOnly]
        public ComponentLookup<Curve> m_CurveData;

        [ReadOnly]
        public ComponentLookup<Game.Net.Elevation> m_NetElevationData;

        [ReadOnly]
        public ComponentLookup<Game.Tools.EditorContainer> m_EditorContainerData;

        [ReadOnly]
        public ComponentLookup<LocalTransformCache> m_LocalTransformCacheData;

        [ReadOnly]
        public ComponentLookup<Transform> m_TransformData;

        [ReadOnly]
        public ComponentLookup<Building> m_BuildingData;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefData;

        [ReadOnly]
        public ComponentLookup<AreaGeometryData> m_PrefabAreaData;

        [ReadOnly]
        public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;

        [ReadOnly]
        public BufferLookup<Game.Areas.Node> m_Nodes;

        [ReadOnly]
        public BufferLookup<Triangle> m_Triangles;

        [ReadOnly]
        public BufferLookup<Game.Areas.SubArea> m_SubAreas;

        [ReadOnly]
        public BufferLookup<LocalNodeCache> m_CachedNodes;

        [ReadOnly]
        public BufferLookup<Game.Net.SubNet> m_SubNets;

        [ReadOnly]
        public BufferLookup<ConnectedEdge> m_ConnectedEdges;

        [ReadOnly]
        public BufferLookup<Game.Objects.SubObject> m_SubObjects;

        [ReadOnly]
        public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;

        [ReadOnly]
        public NativeList<ControlPoint> m_ControlPoints;

        public NativeValue<Tooltip> m_Tooltip;

        public EntityCommandBuffer m_CommandBuffer;

        public void Execute()
        {
            if (m_ControlPoints.Length != 1 || !m_ControlPoints[0].Equals(default))
            {
                switch (m_Mode)
                {
                    case Mode.Edit:
                        Edit();
                        break;
                    case Mode.Generate:
                        Generate();
                        break;
                }
            }
        }

        private void Generate()
        {
            // 核心设置
            int Value = MapSizeMultiplier;

            int2 @int = default;
            @int.y = 0;
            Bounds2 bounds = default;
            while (@int.y < 23)
            {
                @int.x = 0;
                while (@int.x < 23)
                {
                    Entity e = m_CommandBuffer.CreateEntity();
                    CreationDefinition component = new CreationDefinition
                    {
                        m_Prefab = m_Prefab
                    };
                    component.m_Prefab = m_Prefab;
                    float2 @float = new float2(23f, 23f) * 311.652161f * Value;
                    bounds.min = (float2)@int * 623.3043f * Value - @float;
                    bounds.max = (float2)(@int + 1) * 623.3043f * Value - @float;
                    DynamicBuffer<Game.Areas.Node> dynamicBuffer = m_CommandBuffer.AddBuffer<Game.Areas.Node>(e);
                    dynamicBuffer.ResizeUninitialized(5);
                    dynamicBuffer[0] = new Game.Areas.Node(new float3(bounds.min.x, 0f, bounds.min.y), float.MinValue);
                    dynamicBuffer[1] = new Game.Areas.Node(new float3(bounds.min.x, 0f, bounds.max.y), float.MinValue);
                    dynamicBuffer[2] = new Game.Areas.Node(new float3(bounds.max.x, 0f, bounds.max.y), float.MinValue);
                    dynamicBuffer[3] = new Game.Areas.Node(new float3(bounds.max.x, 0f, bounds.min.y), float.MinValue);
                    dynamicBuffer[4] = dynamicBuffer[0];
                    m_CommandBuffer.AddComponent(e, component);
                    m_CommandBuffer.AddComponent(e, default(Updated));
                    @int.x++;
                }
                @int.y++;
            }
            m_Tooltip.value = Tooltip.GenerateAreas;
        }

        private void GetControlPoints(int index, out ControlPoint firstPoint, out ControlPoint lastPoint)
        {
            switch (m_State)
            {
                case State.Default:
                    firstPoint = m_ControlPoints[index];
                    lastPoint = m_ControlPoints[index];
                    break;
                case State.Create:
                    firstPoint = default;
                    lastPoint = m_ControlPoints[m_ControlPoints.Length - 1];
                    break;
                case State.Modify:
                    firstPoint = m_MoveStartPositions[index];
                    lastPoint = m_ControlPoints[0];
                    break;
                case State.Remove:
                    firstPoint = m_MoveStartPositions[index];
                    lastPoint = m_ControlPoints[0];
                    break;
                default:
                    firstPoint = default;
                    lastPoint = default;
                    break;
            }
        }

        private void Edit()
        {
            AreaGeometryData areaData = m_PrefabAreaData[m_Prefab];
            int num = m_State switch
            {
                State.Default => m_ControlPoints.Length,
                State.Create => 1,
                State.Modify => m_MoveStartPositions.Length,
                State.Remove => m_MoveStartPositions.Length,
                _ => 0,
            };
            m_Tooltip.value = Tooltip.None;
            bool flag = false;
            NativeParallelHashSet<Entity> createdEntities = new NativeParallelHashSet<Entity>(num * 2, Allocator.Temp);
            for (int i = 0; i < num; i++)
            {
                GetControlPoints(i, out var firstPoint, out var _);
                if (m_Nodes.HasBuffer(firstPoint.m_OriginalEntity) && math.any(firstPoint.m_ElementIndex >= 0))
                {
                    createdEntities.Add(firstPoint.m_OriginalEntity);
                }
            }
            NativeList<ClearAreaData> clearAreas = default;
            for (int j = 0; j < num; j++)
            {
                GetControlPoints(j, out var firstPoint2, out var lastPoint2);
                if (j == 0 && m_State == State.Modify)
                {
                    flag = !firstPoint2.Equals(lastPoint2);
                }
                Entity e = m_CommandBuffer.CreateEntity();
                CreationDefinition component = new CreationDefinition
                {
                    m_Prefab = m_Prefab
                };
                if (m_Nodes.HasBuffer(firstPoint2.m_OriginalEntity) && math.any(firstPoint2.m_ElementIndex >= 0))
                {
                    component.m_Original = firstPoint2.m_OriginalEntity;
                }
                else if (m_Recreate != Entity.Null)
                {
                    component.m_Original = m_Recreate;
                }
                float minNodeDistance = AreaUtils.GetMinNodeDistance(areaData);
                int2 @int = default;
                DynamicBuffer<Game.Areas.Node> nodes = m_CommandBuffer.AddBuffer<Game.Areas.Node>(e);
                DynamicBuffer<LocalNodeCache> dynamicBuffer = default;
                bool isComplete = false;
                if (m_Nodes.HasBuffer(firstPoint2.m_OriginalEntity) && math.any(firstPoint2.m_ElementIndex >= 0))
                {
                    component.m_Flags |= CreationFlags.Relocate;
                    isComplete = true;
                    Entity sourceArea = GetSourceArea(firstPoint2.m_OriginalEntity);
                    DynamicBuffer<Game.Areas.Node> dynamicBuffer2 = m_Nodes[sourceArea];
                    DynamicBuffer<LocalNodeCache> dynamicBuffer3 = default;
                    if (m_CachedNodes.HasBuffer(sourceArea))
                    {
                        dynamicBuffer3 = m_CachedNodes[sourceArea];
                    }
                    float num2 = float.MinValue;
                    int num3 = -1;
                    if (lastPoint2.m_ElementIndex.x >= 0)
                    {
                        num3 = lastPoint2.m_ElementIndex.x;
                        if (m_OwnerData.TryGetComponent(firstPoint2.m_OriginalEntity, out var componentData))
                        {
                            Entity owner = componentData.m_Owner;
                            while (m_OwnerData.HasComponent(owner) && !m_BuildingData.HasComponent(owner))
                            {
                                if (m_LocalTransformCacheData.HasComponent(owner))
                                {
                                    num3 = m_LocalTransformCacheData[owner].m_ParentMesh;
                                }
                                owner = m_OwnerData[owner].m_Owner;
                            }
                            if (m_TransformData.TryGetComponent(owner, out var componentData2))
                            {
                                num2 = lastPoint2.m_Position.y - componentData2.m_Position.y;
                            }
                        }
                        if (num3 != -1)
                        {
                            if (num2 == float.MinValue)
                            {
                                num2 = 0f;
                            }
                        }
                        else
                        {
                            num2 = float.MinValue;
                        }
                    }
                    if (firstPoint2.m_ElementIndex.y >= 0)
                    {
                        int y = firstPoint2.m_ElementIndex.y;
                        int index = math.select(firstPoint2.m_ElementIndex.y + 1, 0, firstPoint2.m_ElementIndex.y == dynamicBuffer2.Length - 1);
                        float2 @float = new float2(math.distance(lastPoint2.m_Position, dynamicBuffer2[y].m_Position), math.distance(lastPoint2.m_Position, dynamicBuffer2[index].m_Position));
                        bool flag2 = flag && math.any(@float < minNodeDistance);
                        int num4 = math.select(1, 0, flag2 || !flag);
                        int length = dynamicBuffer2.Length + num4;
                        nodes.ResizeUninitialized(length);
                        int num5 = 0;
                        if (dynamicBuffer3.IsCreated)
                        {
                            dynamicBuffer = m_CommandBuffer.AddBuffer<LocalNodeCache>(e);
                            dynamicBuffer.ResizeUninitialized(length);
                            for (int k = 0; k <= firstPoint2.m_ElementIndex.y; k++)
                            {
                                nodes[num5] = dynamicBuffer2[k];
                                dynamicBuffer[num5] = dynamicBuffer3[k];
                                num5++;
                            }
                            @int.x = num5;
                            for (int l = 0; l < num4; l++)
                            {
                                nodes[num5] = new Game.Areas.Node(lastPoint2.m_Position, num2);
                                dynamicBuffer[num5] = new LocalNodeCache
                                {
                                    m_Position = lastPoint2.m_Position,
                                    m_ParentMesh = num3
                                };
                                num5++;
                            }
                            @int.y = num5;
                            for (int m = firstPoint2.m_ElementIndex.y + 1; m < dynamicBuffer2.Length; m++)
                            {
                                nodes[num5] = dynamicBuffer2[m];
                                dynamicBuffer[num5] = dynamicBuffer3[m];
                                num5++;
                            }
                        }
                        else
                        {
                            for (int n = 0; n <= firstPoint2.m_ElementIndex.y; n++)
                            {
                                nodes[num5++] = dynamicBuffer2[n];
                            }
                            for (int num6 = 0; num6 < num4; num6++)
                            {
                                nodes[num5++] = new Game.Areas.Node(lastPoint2.m_Position, num2);
                            }
                            for (int num7 = firstPoint2.m_ElementIndex.y + 1; num7 < dynamicBuffer2.Length; num7++)
                            {
                                nodes[num5++] = dynamicBuffer2[num7];
                            }
                        }
                        switch (m_State)
                        {
                            case State.Default:
                                if (m_AllowCreateArea)
                                {
                                    m_Tooltip.value = Tooltip.CreateAreaOrModifyEdge;
                                }
                                else
                                {
                                    m_Tooltip.value = Tooltip.ModifyEdge;
                                }
                                break;
                            case State.Modify:
                                if (!flag2 && flag)
                                {
                                    m_Tooltip.value = Tooltip.InsertNode;
                                }
                                break;
                        }
                    }
                    else
                    {
                        bool flag3 = false;
                        if (!m_OwnerData.HasComponent(component.m_Original) || dynamicBuffer2.Length >= 4)
                        {
                            if (m_State == State.Remove)
                            {
                                flag3 = true;
                            }
                            else
                            {
                                int index2 = math.select(firstPoint2.m_ElementIndex.x - 1, dynamicBuffer2.Length - 1, firstPoint2.m_ElementIndex.x == 0);
                                int index3 = math.select(firstPoint2.m_ElementIndex.x + 1, 0, firstPoint2.m_ElementIndex.x == dynamicBuffer2.Length - 1);
                                float2 float2 = new float2(math.distance(lastPoint2.m_Position, dynamicBuffer2[index2].m_Position), math.distance(lastPoint2.m_Position, dynamicBuffer2[index3].m_Position));
                                flag3 = flag && math.any(float2 < minNodeDistance);
                            }
                        }
                        int num8 = math.select(0, 1, flag || flag3);
                        int num9 = math.select(1, 0, flag3 || !flag);
                        int num10 = dynamicBuffer2.Length + num9 - num8;
                        nodes.ResizeUninitialized(num10);
                        int num11 = 0;
                        if (dynamicBuffer3.IsCreated)
                        {
                            dynamicBuffer = m_CommandBuffer.AddBuffer<LocalNodeCache>(e);
                            dynamicBuffer.ResizeUninitialized(num10);
                            for (int num12 = 0; num12 <= firstPoint2.m_ElementIndex.x - num8; num12++)
                            {
                                nodes[num11] = dynamicBuffer2[num12];
                                dynamicBuffer[num11] = dynamicBuffer3[num12];
                                num11++;
                            }
                            @int.x = num11;
                            for (int num13 = 0; num13 < num9; num13++)
                            {
                                nodes[num11] = new Game.Areas.Node(lastPoint2.m_Position, num2);
                                dynamicBuffer[num11] = new LocalNodeCache
                                {
                                    m_Position = lastPoint2.m_Position,
                                    m_ParentMesh = num3
                                };
                                num11++;
                            }
                            @int.y = num11;
                            for (int num14 = firstPoint2.m_ElementIndex.x + 1; num14 < dynamicBuffer2.Length; num14++)
                            {
                                nodes[num11] = dynamicBuffer2[num14];
                                dynamicBuffer[num11] = dynamicBuffer3[num14];
                                num11++;
                            }
                        }
                        else
                        {
                            for (int num15 = 0; num15 <= firstPoint2.m_ElementIndex.x - num8; num15++)
                            {
                                nodes[num11++] = dynamicBuffer2[num15];
                            }
                            for (int num16 = 0; num16 < num9; num16++)
                            {
                                nodes[num11++] = new Game.Areas.Node(lastPoint2.m_Position, num2);
                            }
                            for (int num17 = firstPoint2.m_ElementIndex.x + 1; num17 < dynamicBuffer2.Length; num17++)
                            {
                                nodes[num11++] = dynamicBuffer2[num17];
                            }
                        }
                        if (num10 < 3)
                        {
                            component.m_Flags |= CreationFlags.Delete;
                        }
                        switch (m_State)
                        {
                            case State.Default:
                                if (m_AllowCreateArea)
                                {
                                    m_Tooltip.value = Tooltip.CreateAreaOrModifyNode;
                                }
                                else
                                {
                                    m_Tooltip.value = Tooltip.ModifyNode;
                                }
                                break;
                            case State.Modify:
                                if (num10 < 3)
                                {
                                    m_Tooltip.value = Tooltip.DeleteArea;
                                }
                                else if (flag3)
                                {
                                    m_Tooltip.value = Tooltip.MergeNodes;
                                }
                                else if (flag)
                                {
                                    m_Tooltip.value = Tooltip.MoveNode;
                                }
                                break;
                            case State.Remove:
                                if (num10 < 3)
                                {
                                    m_Tooltip.value = Tooltip.DeleteArea;
                                }
                                else if (flag3)
                                {
                                    m_Tooltip.value = Tooltip.RemoveNode;
                                }
                                break;
                        }
                    }
                }
                else
                {
                    if (m_Recreate != Entity.Null)
                    {
                        component.m_Flags |= CreationFlags.Recreate;
                    }
                    bool flag4 = false;
                    if (m_ControlPoints.Length >= 2)
                    {
                        flag4 = math.distance(m_ControlPoints[m_ControlPoints.Length - 2].m_Position, m_ControlPoints[m_ControlPoints.Length - 1].m_Position) < minNodeDistance;
                    }
                    int num18 = math.select(m_ControlPoints.Length, m_ControlPoints.Length - 1, flag4);
                    nodes.ResizeUninitialized(num18);
                    if (m_EditorMode)
                    {
                        dynamicBuffer = m_CommandBuffer.AddBuffer<LocalNodeCache>(e);
                        dynamicBuffer.ResizeUninitialized(num18);
                        @int = new int2(0, num18);
                        float num19 = float.MinValue;
                        int num20 = lastPoint2.m_ElementIndex.x;
                        if (m_TransformData.HasComponent(lastPoint2.m_OriginalEntity))
                        {
                            Entity entity = lastPoint2.m_OriginalEntity;
                            while (m_OwnerData.HasComponent(entity) && !m_BuildingData.HasComponent(entity))
                            {
                                if (m_LocalTransformCacheData.HasComponent(entity))
                                {
                                    num20 = m_LocalTransformCacheData[entity].m_ParentMesh;
                                }
                                entity = m_OwnerData[entity].m_Owner;
                            }
                            if (m_TransformData.TryGetComponent(entity, out var componentData3))
                            {
                                num19 = componentData3.m_Position.y;
                            }
                        }
                        for (int num21 = 0; num21 < num18; num21++)
                        {
                            int num22 = -1;
                            float num23 = float.MinValue;
                            if (m_ControlPoints[num21].m_ElementIndex.x >= 0)
                            {
                                num22 = math.select(m_ControlPoints[num21].m_ElementIndex.x, num20, num20 != -1);
                                num23 = math.select(num23, m_ControlPoints[num21].m_Position.y - num19, num19 != float.MinValue);
                            }
                            if (num22 != -1)
                            {
                                if (num23 == float.MinValue)
                                {
                                    num23 = 0f;
                                }
                            }
                            else
                            {
                                num23 = float.MinValue;
                            }
                            nodes[num21] = new Game.Areas.Node(m_ControlPoints[num21].m_Position, num23);
                            dynamicBuffer[num21] = new LocalNodeCache
                            {
                                m_Position = m_ControlPoints[num21].m_Position,
                                m_ParentMesh = num22
                            };
                        }
                    }
                    else
                    {
                        for (int num24 = 0; num24 < num18; num24++)
                        {
                            nodes[num24] = new Game.Areas.Node(m_ControlPoints[num24].m_Position, float.MinValue);
                        }
                    }
                    switch (m_State)
                    {
                        case State.Default:
                            if (m_ControlPoints.Length == 1 && m_AllowCreateArea)
                            {
                                m_Tooltip.value = Tooltip.CreateArea;
                            }
                            break;
                        case State.Create:
                            if (!flag4)
                            {
                                if (m_ControlPoints.Length >= 4 && m_ControlPoints[0].m_Position.Equals(m_ControlPoints[m_ControlPoints.Length - 1].m_Position))
                                {
                                    m_Tooltip.value = Tooltip.CompleteArea;
                                }
                                else
                                {
                                    m_Tooltip.value = Tooltip.AddNode;
                                }
                            }
                            break;
                    }
                }
                bool flag5 = false;
                Transform inverseParentTransform = default;
                if (m_TransformData.HasComponent(lastPoint2.m_OriginalEntity))
                {
                    if ((areaData.m_Flags & Game.Areas.GeometryFlags.ClearArea) != 0)
                    {
                        ClearAreaHelpers.FillClearAreas(m_PrefabRefData[lastPoint2.m_OriginalEntity].m_Prefab, m_TransformData[lastPoint2.m_OriginalEntity], nodes, isComplete, m_PrefabObjectGeometryData, ref clearAreas);
                    }
                    OwnerDefinition ownerDefinition = GetOwnerDefinition(lastPoint2.m_OriginalEntity, component.m_Original, createdEntities, upgrade: true, (areaData.m_Flags & Game.Areas.GeometryFlags.ClearArea) != 0, clearAreas);
                    if (ownerDefinition.m_Prefab != Entity.Null)
                    {
                        inverseParentTransform.m_Position = -ownerDefinition.m_Position;
                        inverseParentTransform.m_Rotation = math.inverse(ownerDefinition.m_Rotation);
                        flag5 = true;
                        m_CommandBuffer.AddComponent(e, ownerDefinition);
                    }
                }
                else if (m_OwnerData.HasComponent(component.m_Original))
                {
                    Entity owner2 = m_OwnerData[component.m_Original].m_Owner;
                    if (m_TransformData.HasComponent(owner2))
                    {
                        if ((areaData.m_Flags & Game.Areas.GeometryFlags.ClearArea) != 0)
                        {
                            ClearAreaHelpers.FillClearAreas(m_PrefabRefData[owner2].m_Prefab, m_TransformData[owner2], nodes, isComplete, m_PrefabObjectGeometryData, ref clearAreas);
                        }
                        OwnerDefinition ownerDefinition2 = GetOwnerDefinition(owner2, component.m_Original, createdEntities, upgrade: true, (areaData.m_Flags & Game.Areas.GeometryFlags.ClearArea) != 0, clearAreas);
                        if (ownerDefinition2.m_Prefab != Entity.Null)
                        {
                            inverseParentTransform.m_Position = -ownerDefinition2.m_Position;
                            inverseParentTransform.m_Rotation = math.inverse(ownerDefinition2.m_Rotation);
                            flag5 = true;
                            m_CommandBuffer.AddComponent(e, ownerDefinition2);
                        }
                        else
                        {
                            Transform transform = m_TransformData[owner2];
                            inverseParentTransform.m_Position = -transform.m_Position;
                            inverseParentTransform.m_Rotation = math.inverse(transform.m_Rotation);
                            flag5 = true;
                            component.m_Owner = owner2;
                        }
                    }
                    else
                    {
                        component.m_Owner = owner2;
                    }
                }
                if (flag5)
                {
                    for (int num25 = @int.x; num25 < @int.y; num25++)
                    {
                        LocalNodeCache localNodeCache = dynamicBuffer[num25];
                        localNodeCache.m_Position = ObjectUtils.WorldToLocal(inverseParentTransform, localNodeCache.m_Position);
                    }
                }
                m_CommandBuffer.AddComponent(e, component);
                m_CommandBuffer.AddComponent(e, default(Updated));
                if (m_AreaData.TryGetComponent(component.m_Original, out var componentData4) && m_SubObjects.TryGetBuffer(component.m_Original, out var bufferData) && (componentData4.m_Flags & AreaFlags.Complete) != 0)
                {
                    CheckSubObjects(bufferData, nodes, createdEntities, minNodeDistance, (componentData4.m_Flags & AreaFlags.CounterClockwise) != 0);
                }
                if (clearAreas.IsCreated)
                {
                    clearAreas.Clear();
                }
            }
            if (clearAreas.IsCreated)
            {
                clearAreas.Dispose();
            }
            createdEntities.Dispose();
        }

        private Entity GetSourceArea(Entity originalArea)
        {
            if (m_ApplyTempAreas.IsCreated)
            {
                for (int i = 0; i < m_ApplyTempAreas.Length; i++)
                {
                    Entity entity = m_ApplyTempAreas[i];
                    if (originalArea == m_TempData[entity].m_Original)
                    {
                        return entity;
                    }
                }
            }
            return originalArea;
        }

        private void CheckSubObjects(DynamicBuffer<Game.Objects.SubObject> subObjects, DynamicBuffer<Game.Areas.Node> nodes, NativeParallelHashSet<Entity> createdEntities, float minNodeDistance, bool isCounterClockwise)
        {
            Line2.Segment line = default;
            for (int i = 0; i < subObjects.Length; i++)
            {
                Game.Objects.SubObject subObject = subObjects[i];
                if (!m_BuildingData.HasComponent(subObject.m_SubObject))
                {
                    continue;
                }
                if (m_ApplyTempBuildings.IsCreated)
                {
                    bool flag = false;
                    for (int j = 0; j < m_ApplyTempBuildings.Length; j++)
                    {
                        if (m_ApplyTempBuildings[j] == subObject.m_SubObject)
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (flag)
                    {
                        continue;
                    }
                }
                Transform transform = m_TransformData[subObject.m_SubObject];
                PrefabRef prefabRef = m_PrefabRefData[subObject.m_SubObject];
                if (!m_PrefabObjectGeometryData.TryGetComponent(prefabRef.m_Prefab, out var componentData))
                {
                    continue;
                }
                float num;
                if ((componentData.m_Flags & Game.Objects.GeometryFlags.Circular) != Game.Objects.GeometryFlags.None)
                {
                    num = componentData.m_Size.x * 0.5f;
                }
                else
                {
                    num = math.length(MathUtils.Size(componentData.m_Bounds.xz)) * 0.5f;
                    transform.m_Position.xz -= math.rotate(transform.m_Rotation, MathUtils.Center(componentData.m_Bounds)).xz;
                }
                float num2 = 0f;
                int num3 = -1;
                bool flag2 = nodes.Length <= 2;
                if (!flag2)
                {
                    float num4 = float.MaxValue;
                    float num5 = num + minNodeDistance;
                    num5 *= num5;
                    line.a = nodes[nodes.Length - 1].m_Position.xz;
                    for (int k = 0; k < nodes.Length; k++)
                    {
                        line.b = nodes[k].m_Position.xz;
                        float t;
                        float num6 = MathUtils.DistanceSquared(line, transform.m_Position.xz, out t);
                        if (num6 < num5)
                        {
                            flag2 = true;
                            break;
                        }
                        if (num6 < num4)
                        {
                            num4 = num6;
                            num2 = t;
                            num3 = k;
                        }
                        line.a = line.b;
                    }
                }
                if (!flag2 && num3 >= 0)
                {
                    int2 @int = math.select(new int2(num3 - 1, num3), new int2(num3 - 2, num3 + 1), new bool2(num2 == 0f, num2 == 1f));
                    @int = math.select(@int, @int + new int2(nodes.Length, -nodes.Length), new bool2(@int.x < 0, @int.y >= nodes.Length));
                    @int = math.select(@int, @int.yx, isCounterClockwise);
                    float2 xz = nodes[@int.x].m_Position.xz;
                    float2 xz2 = nodes[@int.y].m_Position.xz;
                    flag2 = math.dot(transform.m_Position.xz - xz, MathUtils.Right(xz2 - xz)) <= 0f;
                }
                if (flag2)
                {
                    Entity e = m_CommandBuffer.CreateEntity();
                    CreationDefinition component = new CreationDefinition
                    {
                        m_Original = subObject.m_SubObject
                    };
                    component.m_Flags |= CreationFlags.Delete;
                    ObjectDefinition component2 = new ObjectDefinition
                    {
                        m_ParentMesh = -1,
                        m_Position = transform.m_Position,
                        m_Rotation = transform.m_Rotation,
                        m_LocalPosition = transform.m_Position,
                        m_LocalRotation = transform.m_Rotation
                    };
                    m_CommandBuffer.AddComponent(e, component);
                    m_CommandBuffer.AddComponent(e, component2);
                    m_CommandBuffer.AddComponent(e, default(Updated));
                    UpdateSubNets(transform, prefabRef.m_Prefab, subObject.m_SubObject, default, removeAll: true);
                    UpdateSubAreas(transform, prefabRef.m_Prefab, subObject.m_SubObject, createdEntities, default, removeAll: true);
                }
            }
        }

        private OwnerDefinition GetOwnerDefinition(Entity parent, Entity area, NativeParallelHashSet<Entity> createdEntities, bool upgrade, bool fullUpdate, NativeList<ClearAreaData> clearAreas)
        {
            OwnerDefinition result = default;
            if (!m_EditorMode)
            {
                return result;
            }
            Entity entity = parent;
            while (m_OwnerData.HasComponent(entity) && !m_BuildingData.HasComponent(entity))
            {
                entity = m_OwnerData[entity].m_Owner;
            }
            OwnerDefinition ownerDefinition = default;
            if (m_InstalledUpgrades.TryGetBuffer(entity, out var bufferData) && bufferData.Length != 0)
            {
                if (fullUpdate && m_TransformData.HasComponent(entity))
                {
                    Transform transform = m_TransformData[entity];
                    ClearAreaHelpers.FillClearAreas(bufferData, area, m_TransformData, m_ClearData, m_PrefabRefData, m_PrefabObjectGeometryData, m_SubAreas, m_Nodes, m_Triangles, ref clearAreas);
                    ClearAreaHelpers.InitClearAreas(clearAreas, transform);
                    if (createdEntities.Add(entity))
                    {
                        Entity owner = Entity.Null;
                        if (m_OwnerData.HasComponent(entity))
                        {
                            owner = m_OwnerData[entity].m_Owner;
                        }
                        UpdateOwnerObject(owner, entity, createdEntities, transform, default, upgrade: false, clearAreas);
                    }
                    ownerDefinition.m_Prefab = m_PrefabRefData[entity].m_Prefab;
                    ownerDefinition.m_Position = transform.m_Position;
                    ownerDefinition.m_Rotation = transform.m_Rotation;
                }
                entity = bufferData[0].m_Upgrade;
            }
            if (m_TransformData.HasComponent(entity))
            {
                Transform transform2 = m_TransformData[entity];
                if (createdEntities.Add(entity))
                {
                    Entity owner2 = Entity.Null;
                    if (ownerDefinition.m_Prefab == Entity.Null && m_OwnerData.HasComponent(entity))
                    {
                        owner2 = m_OwnerData[entity].m_Owner;
                    }
                    UpdateOwnerObject(owner2, entity, createdEntities, transform2, ownerDefinition, upgrade, default);
                }
                result.m_Prefab = m_PrefabRefData[entity].m_Prefab;
                result.m_Position = transform2.m_Position;
                result.m_Rotation = transform2.m_Rotation;
            }
            return result;
        }

        private void UpdateOwnerObject(Entity owner, Entity original, NativeParallelHashSet<Entity> createdEntities, Transform transform, OwnerDefinition ownerDefinition, bool upgrade, NativeList<ClearAreaData> clearAreas)
        {
            Entity e = m_CommandBuffer.CreateEntity();
            Entity prefab = m_PrefabRefData[original].m_Prefab;
            CreationDefinition component = new CreationDefinition
            {
                m_Owner = owner,
                m_Original = original
            };
            if (upgrade)
            {
                component.m_Flags |= CreationFlags.Upgrade | CreationFlags.Parent;
            }
            ObjectDefinition component2 = new ObjectDefinition
            {
                m_ParentMesh = -1,
                m_Position = transform.m_Position,
                m_Rotation = transform.m_Rotation
            };
            if (m_TransformData.HasComponent(owner))
            {
                Transform transform2 = ObjectUtils.WorldToLocal(ObjectUtils.InverseTransform(m_TransformData[owner]), transform);
                component2.m_LocalPosition = transform2.m_Position;
                component2.m_LocalRotation = transform2.m_Rotation;
            }
            else
            {
                component2.m_LocalPosition = transform.m_Position;
                component2.m_LocalRotation = transform.m_Rotation;
            }
            m_CommandBuffer.AddComponent(e, component);
            m_CommandBuffer.AddComponent(e, component2);
            m_CommandBuffer.AddComponent(e, default(Updated));
            if (ownerDefinition.m_Prefab != Entity.Null)
            {
                m_CommandBuffer.AddComponent(e, ownerDefinition);
            }
            UpdateSubNets(transform, prefab, original, clearAreas, removeAll: false);
            UpdateSubAreas(transform, prefab, original, createdEntities, clearAreas, removeAll: false);
        }

        private void UpdateSubNets(Transform transform, Entity prefab, Entity original, NativeList<ClearAreaData> clearAreas, bool removeAll)
        {
            if (!m_SubNets.HasBuffer(original))
            {
                return;
            }
            DynamicBuffer<Game.Net.SubNet> dynamicBuffer = m_SubNets[original];
            for (int i = 0; i < dynamicBuffer.Length; i++)
            {
                Entity subNet = dynamicBuffer[i].m_SubNet;
                if (m_NodeData.HasComponent(subNet))
                {
                    if (!HasEdgeStartOrEnd(subNet, original))
                    {
                        Game.Net.Node node = m_NodeData[subNet];
                        Entity e = m_CommandBuffer.CreateEntity();
                        CreationDefinition component = new CreationDefinition
                        {
                            m_Original = subNet
                        };
                        if (m_EditorContainerData.HasComponent(subNet))
                        {
                            component.m_SubPrefab = m_EditorContainerData[subNet].m_Prefab;
                        }
                        Game.Net.Elevation componentData;
                        bool onGround = !m_NetElevationData.TryGetComponent(subNet, out componentData) || math.cmin(math.abs(componentData.m_Elevation)) < 2f;
                        if (removeAll)
                        {
                            component.m_Flags |= CreationFlags.Delete;
                        }
                        else if (ClearAreaHelpers.ShouldClear(clearAreas, node.m_Position, onGround))
                        {
                            component.m_Flags |= CreationFlags.Delete | CreationFlags.Hidden;
                        }
                        OwnerDefinition component2 = new OwnerDefinition
                        {
                            m_Prefab = prefab,
                            m_Position = transform.m_Position,
                            m_Rotation = transform.m_Rotation
                        };
                        m_CommandBuffer.AddComponent(e, component2);
                        m_CommandBuffer.AddComponent(e, component);
                        m_CommandBuffer.AddComponent(e, default(Updated));
                        NetCourse component3 = new NetCourse
                        {
                            m_Curve = new Bezier4x3(node.m_Position, node.m_Position, node.m_Position, node.m_Position),
                            m_Length = 0f,
                            m_FixedIndex = -1,
                            m_StartPosition =
                                {
                                    m_Entity = subNet,
                                    m_Position = node.m_Position,
                                    m_Rotation = node.m_Rotation,
                                    m_CourseDelta = 0f
                                },
                            m_EndPosition =
                                {
                                    m_Entity = subNet,
                                    m_Position = node.m_Position,
                                    m_Rotation = node.m_Rotation,
                                    m_CourseDelta = 1f
                                }
                        };
                        m_CommandBuffer.AddComponent(e, component3);
                    }
                }
                else if (m_EdgeData.HasComponent(subNet))
                {
                    Edge edge = m_EdgeData[subNet];
                    Entity e2 = m_CommandBuffer.CreateEntity();
                    CreationDefinition component4 = new CreationDefinition
                    {
                        m_Original = subNet
                    };
                    if (m_EditorContainerData.HasComponent(subNet))
                    {
                        component4.m_SubPrefab = m_EditorContainerData[subNet].m_Prefab;
                    }
                    Curve curve = m_CurveData[subNet];
                    Game.Net.Elevation componentData2;
                    bool onGround2 = !m_NetElevationData.TryGetComponent(subNet, out componentData2) || math.cmin(math.abs(componentData2.m_Elevation)) < 2f;
                    if (removeAll)
                    {
                        component4.m_Flags |= CreationFlags.Delete;
                    }
                    else if (ClearAreaHelpers.ShouldClear(clearAreas, curve.m_Bezier, onGround2))
                    {
                        component4.m_Flags |= CreationFlags.Delete | CreationFlags.Hidden;
                    }
                    OwnerDefinition component5 = new OwnerDefinition
                    {
                        m_Prefab = prefab,
                        m_Position = transform.m_Position,
                        m_Rotation = transform.m_Rotation
                    };
                    m_CommandBuffer.AddComponent(e2, component5);
                    m_CommandBuffer.AddComponent(e2, component4);
                    m_CommandBuffer.AddComponent(e2, default(Updated));
                    NetCourse component6 = default;
                    component6.m_Curve = curve.m_Bezier;
                    component6.m_Length = MathUtils.Length(component6.m_Curve);
                    component6.m_FixedIndex = -1;
                    component6.m_StartPosition.m_Entity = edge.m_Start;
                    component6.m_StartPosition.m_Position = component6.m_Curve.a;
                    component6.m_StartPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(component6.m_Curve));
                    component6.m_StartPosition.m_CourseDelta = 0f;
                    component6.m_EndPosition.m_Entity = edge.m_End;
                    component6.m_EndPosition.m_Position = component6.m_Curve.d;
                    component6.m_EndPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(component6.m_Curve));
                    component6.m_EndPosition.m_CourseDelta = 1f;
                    m_CommandBuffer.AddComponent(e2, component6);
                }
            }
        }

        private bool HasEdgeStartOrEnd(Entity node, Entity owner)
        {
            DynamicBuffer<ConnectedEdge> dynamicBuffer = m_ConnectedEdges[node];
            for (int i = 0; i < dynamicBuffer.Length; i++)
            {
                Entity edge = dynamicBuffer[i].m_Edge;
                Edge edge2 = m_EdgeData[edge];
                if ((edge2.m_Start == node || edge2.m_End == node) && m_OwnerData.HasComponent(edge) && m_OwnerData[edge].m_Owner == owner)
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateSubAreas(Transform transform, Entity prefab, Entity original, NativeParallelHashSet<Entity> createdEntities, NativeList<ClearAreaData> clearAreas, bool removeAll)
        {
            if (!m_SubAreas.HasBuffer(original))
            {
                return;
            }
            DynamicBuffer<Game.Areas.SubArea> dynamicBuffer = m_SubAreas[original];
            for (int i = 0; i < dynamicBuffer.Length; i++)
            {
                Entity area = dynamicBuffer[i].m_Area;
                if (!createdEntities.Add(area))
                {
                    continue;
                }
                Entity e = m_CommandBuffer.CreateEntity();
                CreationDefinition component = new CreationDefinition
                {
                    m_Original = area
                };
                OwnerDefinition component2 = new OwnerDefinition
                {
                    m_Prefab = prefab,
                    m_Position = transform.m_Position,
                    m_Rotation = transform.m_Rotation
                };
                m_CommandBuffer.AddComponent(e, component2);
                DynamicBuffer<Game.Areas.Node> nodes = m_Nodes[area];
                if (removeAll)
                {
                    component.m_Flags |= CreationFlags.Delete;
                }
                else if (m_SpaceData.HasComponent(area))
                {
                    DynamicBuffer<Triangle> triangles = m_Triangles[area];
                    if (ClearAreaHelpers.ShouldClear(clearAreas, nodes, triangles, transform))
                    {
                        component.m_Flags |= CreationFlags.Delete | CreationFlags.Hidden;
                    }
                }
                m_CommandBuffer.AddComponent(e, component);
                m_CommandBuffer.AddComponent(e, default(Updated));
                m_CommandBuffer.AddBuffer<Game.Areas.Node>(e).CopyFrom(nodes.AsNativeArray());
                if (m_CachedNodes.HasBuffer(area))
                {
                    DynamicBuffer<LocalNodeCache> dynamicBuffer2 = m_CachedNodes[area];
                    m_CommandBuffer.AddBuffer<LocalNodeCache>(e).CopyFrom(dynamicBuffer2.AsNativeArray());
                }
            }
        }
    }

}




