// Game.Areas.MapTileSystem

using Colossal.Mathematics;
using Game.Areas;
using Game.Prefabs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct GenerateMapTilesJob : IJobParallelFor
    {
        [DeallocateOnJobCompletion]
        [ReadOnly] public NativeArray<Entity> m_Entities;

        [ReadOnly] public Entity m_Prefab;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<PrefabRef> m_PrefabRefData;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<Area> m_AreaData;

        [NativeDisableParallelForRestriction]
        public BufferLookup<Node> m_NodeData;

        // 新增配置参数 (暂不实施动态配置)

        public void Execute(int index)
        {
            // 核心参数设置
            float BASE_MAP_SIZE = 14336f; // 基础地图边长
            int CoreValue = CellMapSystemRe.kMapSize / 14336; // 2-> 28672 4 -> 57344; 16 -> 229376

            // 瓦片基础数值
            int GRID_COUNT = 23; // 瓦片行/列数

            Entity entity = m_Entities[index];
            m_PrefabRefData[entity] = new PrefabRef(m_Prefab);
            m_AreaData[entity] = new Area(AreaFlags.Complete);
            DynamicBuffer<Node> dynamicBuffer = m_NodeData[entity];

            // 1. 计算当前倍率下的地图总尺寸和单个瓦片尺寸
            // 原始逻辑：tileSize = 623.3043f (即 14336 / 23)
            float currentTotalMapSize = BASE_MAP_SIZE * (float)CoreValue;
            float tileSize = currentTotalMapSize / (float)GRID_COUNT;

            // 2. 计算地图偏移量 (用于将地图中心对齐到 0,0)
            float2 mapHalfExtents = new float2(currentTotalMapSize * 0.5f);

            // 3. 计算当前瓦片的网格坐标 (x, y)
            int2 gridIndex = new int2(index % GRID_COUNT, index / GRID_COUNT);

            // 4. 计算瓦片的包围盒 (Bounds)
            // 公式：(网格坐标 * 瓦片大小) - 地图半宽
            Bounds2 bounds = default(Bounds2);
            bounds.min = (float2)gridIndex * tileSize - mapHalfExtents;
            bounds.max = (float2)(gridIndex + 1) * tileSize - mapHalfExtents;

            dynamicBuffer.ResizeUninitialized(4);
            // 注意：这里Y轴保持为0，构建XZ平面的四边形
            dynamicBuffer[0] = new Node(new float3(bounds.min.x, 0f, bounds.min.y), float.MinValue);
            dynamicBuffer[1] = new Node(new float3(bounds.min.x, 0f, bounds.max.y), float.MinValue);
            dynamicBuffer[2] = new Node(new float3(bounds.max.x, 0f, bounds.max.y), float.MinValue);
            dynamicBuffer[3] = new Node(new float3(bounds.max.x, 0f, bounds.min.y), float.MinValue);
        }
    }

}



