#define UNITY_ASSERTIONS
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct LandValueMapUpdateJob : IJobParallelFor
    {
        public NativeArray<LandValueCell> m_LandValueMap;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

        [ReadOnly]
        public NativeArray<TerrainAttractiveness> m_AttractiveMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_GroundPollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public NativeArray<AvailabilityInfoCell> m_AvailabilityInfoMap;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverageMap;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public ComponentLookup<LandValue> m_LandValueData;

        [ReadOnly]
        public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

        [ReadOnly]
        public AttractivenessParameterData m_AttractivenessParameterData;

        [ReadOnly]
        public LandValueParameterData m_LandValueParameterData;

        public float m_CellSize;

        public void Execute(int index)
        {
            // OnUpdate中引入参数
            // m_CellSize = CellMapSystem<LandValueCell>.kMapSize / LandValueSystem.kTextureSize
            // 直接修改job内部逻辑，避免transpiler双重修补
            m_CellSize = CellMapSystemRe.kMapSize / LandValueSystem.kTextureSize; // 57344 / 128; 

            // 后续修补
            int kTextureSize = 128;
            // int kUpdatesPerDay = 32;

            float3 cellCenter = GetCellCenter(index, kTextureSize);
            if (WaterUtils.SampleDepth(ref m_WaterSurfaceData, cellCenter) > 1f)
            {
                m_LandValueMap[index] = new LandValueCell
                {
                    m_LandValue = m_LandValueParameterData.m_LandValueBaseline
                };
                return;
            }
            NetIterator netIterator = default;
            netIterator.m_TotalCount = 0;
            netIterator.m_TotalLandValueBonus = 0f;
            netIterator.m_Bounds = new Bounds3(cellCenter - new float3(1.5f * m_CellSize, 10000f, 1.5f * m_CellSize), cellCenter + new float3(1.5f * m_CellSize, 10000f, 1.5f * m_CellSize));
            netIterator.m_EdgeGeometryData = m_EdgeGeometryData;
            netIterator.m_LandValueData = m_LandValueData;
            NetIterator iterator = netIterator;
            m_NetSearchTree.Iterate(ref iterator);
            float num = GroundPollutionSystemGetPollution(cellCenter, m_GroundPollutionMap).m_Pollution;
            float num2 = AirPollutionSystemGetPollution(cellCenter, m_AirPollutionMap).m_Pollution;
            float num3 = NoisePollutionSystemGetPollution(cellCenter, m_NoisePollutionMap).m_Pollution;
            float x = AvailabilityInfoToGridSystemGetAvailabilityInfo(cellCenter, m_AvailabilityInfoMap).m_AvailabilityInfo.x;
            float num4 = TelecomCoverage.SampleNetworkQuality(m_TelecomCoverageMap, cellCenter);
            LandValueCell value = m_LandValueMap[index];
            float num5 = iterator.m_TotalCount > 0f ? iterator.m_TotalLandValueBonus / iterator.m_TotalCount : 0f;
            float num6 = math.min((x - 5f) * m_LandValueParameterData.m_AttractivenessBonusMultiplier, m_LandValueParameterData.m_CommonFactorMaxBonus);
            float num7 = math.min(num4 * m_LandValueParameterData.m_TelecomCoverageBonusMultiplier, m_LandValueParameterData.m_CommonFactorMaxBonus);
            num5 += num6 + num7;
            float num8 = WaterUtils.SamplePolluted(ref m_WaterSurfaceData, cellCenter);
            float num9 = 0f;
            if (num8 <= 0f && num <= 0f)
            {
                num9 = TerrainAttractivenessSystem.EvaluateAttractiveness(TerrainUtils.SampleHeight(ref m_TerrainHeightData, cellCenter), m_AttractiveMap[index], m_AttractivenessParameterData);
                num5 += math.min(math.max(num9 - 5f, 0f) * m_LandValueParameterData.m_AttractivenessBonusMultiplier, m_LandValueParameterData.m_CommonFactorMaxBonus);
            }
            float num10 = num * m_LandValueParameterData.m_GroundPollutionPenaltyMultiplier + num2 * m_LandValueParameterData.m_AirPollutionPenaltyMultiplier + num3 * m_LandValueParameterData.m_NoisePollutionPenaltyMultiplier;
            float num11 = math.max(m_LandValueParameterData.m_LandValueBaseline, m_LandValueParameterData.m_LandValueBaseline + num5 - num10);
            if (math.abs(value.m_LandValue - num11) >= 0.1f)
            {
                value.m_LandValue = math.lerp(value.m_LandValue, num11, 0.4f);
            }
            m_LandValueMap[index] = value;
        }
        private struct NetIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public int m_TotalCount;

            public float m_TotalLandValueBonus;

            public Bounds3 m_Bounds;

            public ComponentLookup<LandValue> m_LandValueData;

            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds, m_Bounds);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                if (MathUtils.Intersect(bounds.m_Bounds, m_Bounds) && m_LandValueData.HasComponent(entity) && m_EdgeGeometryData.HasComponent(entity))
                {
                    LandValue landValue = m_LandValueData[entity];
                    if (landValue.m_LandValue > 0f)
                    {
                        m_TotalLandValueBonus += landValue.m_LandValue;
                        m_TotalCount++;
                    }
                }
            }
        }
    }

}