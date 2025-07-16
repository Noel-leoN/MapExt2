using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    /// <summary>
    /// 需修改m_CellSize输入值；
    /// OnUpdate中引入m_CellSize=CellMapSystem.kMapSize/kTextureSize
    /// </summary>
    [BurstCompile]
    public struct AvailabilityInfoToGridJob : IJobParallelFor
    {
        public NativeArray<AvailabilityInfoCell> m_AvailabilityInfoMap;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

        [ReadOnly]
        public BufferLookup<ResourceAvailability> m_AvailabilityData;

        [ReadOnly]
        public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

        public float m_CellSize;

        public void Execute(int index)
        {
            // 修补输入值；
            m_CellSize = 28672f;
            // 原始代码
            float3 cellCenter = GetCellCenter(index, AvailabilityInfoToGridSystem.kTextureSize);
            NetIterator netIterator = default;
            netIterator.m_TotalWeight = default;
            netIterator.m_Result = default;
            netIterator.m_Bounds = new Bounds3(cellCenter - new float3(1.5f * m_CellSize, 10000f, 1.5f * m_CellSize), cellCenter + new float3(1.5f * m_CellSize, 10000f, 1.5f * m_CellSize));
            netIterator.m_CellSize = m_CellSize;
            netIterator.m_EdgeGeometryData = m_EdgeGeometryData;
            netIterator.m_Availabilities = m_AvailabilityData;
            NetIterator iterator = netIterator;
            m_NetSearchTree.Iterate(ref iterator);
            AvailabilityInfoCell value = m_AvailabilityInfoMap[index];
            value.m_AvailabilityInfo = math.select(iterator.m_Result.m_AvailabilityInfo / iterator.m_TotalWeight.m_AvailabilityInfo, 0f, iterator.m_TotalWeight.m_AvailabilityInfo == 0f);
            m_AvailabilityInfoMap[index] = value;
        }
        private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public AvailabilityInfoCell m_TotalWeight;

            public AvailabilityInfoCell m_Result;

            public float m_CellSize;

            public Bounds3 m_Bounds;

            public BufferLookup<ResourceAvailability> m_Availabilities;

            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds, m_Bounds);
            }

            private void AddData(float2 attractiveness2, float2 uneducated2, float2 educated2, float2 services2, float2 workplaces2, float2 t, float3 curvePos, float weight)
            {
                float num = math.lerp(attractiveness2.x, attractiveness2.y, t.y);
                float num2 = 0.5f * math.lerp(uneducated2.x + educated2.x, uneducated2.y + educated2.y, t.y);
                float num3 = math.lerp(services2.x, services2.y, t.y);
                float num4 = math.lerp(workplaces2.x, workplaces2.y, t.y);
                m_Result.AddAttractiveness(weight * num);
                m_TotalWeight.AddAttractiveness(weight);
                m_Result.AddConsumers(weight * num2);
                m_TotalWeight.AddConsumers(weight);
                m_Result.AddServices(weight * num3);
                m_TotalWeight.AddServices(weight);
                m_Result.AddWorkplaces(weight * num4);
                m_TotalWeight.AddWorkplaces(weight);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                if (MathUtils.Intersect(bounds.m_Bounds, m_Bounds) && m_Availabilities.HasBuffer(entity) && m_EdgeGeometryData.HasComponent(entity))
                {
                    DynamicBuffer<ResourceAvailability> dynamicBuffer = m_Availabilities[entity];
                    float2 availability = dynamicBuffer[18].m_Availability;
                    float2 availability2 = dynamicBuffer[2].m_Availability;
                    float2 availability3 = dynamicBuffer[3].m_Availability;
                    float2 availability4 = dynamicBuffer[1].m_Availability;
                    float2 availability5 = dynamicBuffer[0].m_Availability;
                    EdgeGeometry edgeGeometry = m_EdgeGeometryData[entity];
                    int num = (int)math.ceil(edgeGeometry.m_Start.middleLength * 0.05f);
                    int num2 = (int)math.ceil(edgeGeometry.m_End.middleLength * 0.05f);
                    float3 @float = 0.5f * (m_Bounds.min + m_Bounds.max);
                    for (int i = 1; i <= num; i++)
                    {
                        float2 t = i / new float2(num, num + num2);
                        float3 curvePos = math.lerp(MathUtils.Position(edgeGeometry.m_Start.m_Left, t.x), MathUtils.Position(edgeGeometry.m_Start.m_Right, t.x), 0.5f);
                        float weight = math.max(0f, 1f - math.distance(@float.xz, curvePos.xz) / (1.5f * 28672f));// m_CellSize
                        AddData(availability, availability2, availability3, availability4, availability5, t, curvePos, weight);
                    }
                    for (int j = 1; j <= num2; j++)
                    {
                        float2 t2 = new float2(j, num + j) / new float2(num2, num + num2);
                        float3 curvePos2 = math.lerp(MathUtils.Position(edgeGeometry.m_End.m_Left, t2.x), MathUtils.Position(edgeGeometry.m_End.m_Right, t2.x), 0.5f);
                        float weight2 = math.max(0f, 1f - math.distance(@float.xz, curvePos2.xz) / (1.5f * 28672f));// m_CellSize
                        AddData(availability, availability2, availability3, availability4, availability5, t2, curvePos2, weight2);
                    }
                }
            }
        }

    }

}

/*

*/


/*
[Preserve]
protected override void OnUpdate()
{
    this.__TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentLookup.Update(ref base.CheckedStateRef);
    this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup.Update(ref base.CheckedStateRef);
    AvailabilityInfoToGridJob availabilityInfoToGridJob = default(AvailabilityInfoToGridJob);
    availabilityInfoToGridJob.m_NetSearchTree = this.m_NetSearchSystem.GetNetSearchTree(readOnly: true, out var dependencies);
    availabilityInfoToGridJob.m_AvailabilityInfoMap = base.m_Map;
    availabilityInfoToGridJob.m_AvailabilityData = this.__TypeHandle.__Game_Net_ResourceAvailability_RO_BufferLookup;
    availabilityInfoToGridJob.m_EdgeGeometryData = this.__TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentLookup;
    availabilityInfoToGridJob.m_CellSize = (float)CellMapSystem<AvailabilityInfoCell>.kMapSize / (float)AvailabilityInfoToGridSystem.kTextureSize;
    AvailabilityInfoToGridJob jobData = availabilityInfoToGridJob;
    base.Dependency = IJobParallelForExtensions.Schedule(jobData, AvailabilityInfoToGridSystem.kTextureSize * AvailabilityInfoToGridSystem.kTextureSize, AvailabilityInfoToGridSystem.kTextureSize, JobHandle.CombineDependencies(dependencies, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency)));
    base.AddWriter(base.Dependency);
    this.m_NetSearchSystem.AddNetSearchTreeReader(base.Dependency);
    base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
}
*/
