using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static MapExtPDX.MapExt.ReBurstSystemModeC.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    [BurstCompile]
    public struct TerrainAttractivenessPrepareJob : IJobParallelForBatch
    {
        [ReadOnly]
        public TerrainHeightData m_TerrainData;

        [ReadOnly]
        public WaterSurfaceData m_WaterData;

        [ReadOnly]
        public CellMapData<ZoneAmbienceCell> m_ZoneAmbienceData;

        public NativeArray<float3> m_AttractFactorData;

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                float3 cellCenter = CellMapSystemRe.GetCellCenter(i, TerrainAttractivenessSystem.kTextureSize);
                m_AttractFactorData[i] = new float3(WaterUtils.SampleDepth(ref m_WaterData, cellCenter), TerrainUtils.SampleHeight(ref m_TerrainData, cellCenter), ZoneAmbienceSystemGetZoneAmbience(GroupAmbienceType.Forest, cellCenter, m_ZoneAmbienceData.m_Buffer, 1f));
            }
        }
    }

    [BurstCompile]
    public struct TerrainAttractivenessJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<float3> m_AttractFactorData;

        [ReadOnly]
        public float m_Scale;

        public NativeArray<TerrainAttractiveness> m_AttractivenessMap;

        public AttractivenessParameterData m_AttractivenessParameters;

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                float3 cellCenter = CellMapSystemRe.GetCellCenter(i, TerrainAttractivenessSystem.kTextureSize);
                float2 @float = 0;
                int num = Mathf.CeilToInt(math.max(m_AttractivenessParameters.m_ForestDistance, m_AttractivenessParameters.m_ShoreDistance) / m_Scale);
                for (int j = -num; j <= num; j++)
                {
                    for (int k = -num; k <= num; k++)
                    {
                        int num2 = math.min(TerrainAttractivenessSystem.kTextureSize - 1, math.max(0, i % TerrainAttractivenessSystem.kTextureSize + j));
                        int num3 = math.min(TerrainAttractivenessSystem.kTextureSize - 1, math.max(0, i / TerrainAttractivenessSystem.kTextureSize + k));
                        int index = num2 + num3 * TerrainAttractivenessSystem.kTextureSize;
                        float3 float2 = m_AttractFactorData[index];
                        float num4 = math.distance(CellMapSystemRe.GetCellCenter(index, TerrainAttractivenessSystem.kTextureSize), cellCenter);
                        @float.x = math.max(@float.x, math.saturate(1f - num4 / m_AttractivenessParameters.m_ForestDistance) * float2.z);
                        @float.y = math.max(@float.y, math.saturate(1f - num4 / m_AttractivenessParameters.m_ShoreDistance) * (float2.x > 2f ? 1f : 0f));
                    }
                }
                m_AttractivenessMap[i] = new TerrainAttractiveness
                {
                    m_ForestBonus = @float.x,
                    m_ShoreBonus = @float.y
                };
            }
        }
    }

}


