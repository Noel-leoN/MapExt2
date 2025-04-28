
using Colossal.Mathematics;
using Game.Areas;
using Game.Prefabs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// 地图大小变化请在 Execute()内设置！
/// </summary>


namespace MapExtPDX
{
    [BurstCompile]
    public struct GenerateMapTilesJob : IJobParallelFor
    {
        [DeallocateOnJobCompletion]
        [ReadOnly]
        public NativeArray<Entity> m_Entities;

        [ReadOnly]
        public Entity m_Prefab;

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
            const int Value = 4; // 4 -> 57344; 16 -> 229376

            // ... 其余代码与原Job保持一致 
            Entity entity = this.m_Entities[index];
            this.m_PrefabRefData[entity] = new PrefabRef(this.m_Prefab);
            this.m_AreaData[entity] = new Area(AreaFlags.Complete);
            DynamicBuffer<Node> dynamicBuffer = this.m_NodeData[entity];
            int2 @int = new int2(index % 23, index / 23);
            float2 @float = new float2(23f, 23f) * 311.65216f * Value; // 区块数不变，边长扩展4倍；311.65216f
            Bounds2 bounds = default(Bounds2);
            bounds.min = (float2)@int * 623.3043f * Value - @float; // 边长扩展4倍；623.3043f
            bounds.max = (float2)(@int + 1) * 623.3043f * Value - @float;
            dynamicBuffer.ResizeUninitialized(4);
            dynamicBuffer[0] = new Node(new float3(bounds.min.x, 0f, bounds.min.y), float.MinValue);
            dynamicBuffer[1] = new Node(new float3(bounds.min.x, 0f, bounds.max.y), float.MinValue);
            dynamicBuffer[2] = new Node(new float3(bounds.max.x, 0f, bounds.max.y), float.MinValue);
            dynamicBuffer[3] = new Node(new float3(bounds.max.x, 0f, bounds.min.y), float.MinValue);
        }
    }

}



