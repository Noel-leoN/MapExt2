using Colossal.Collections;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct LandValueTooltipJob : IJob
    {
        [ReadOnly]
        public NativeArray<LandValueCell> m_LandValueMap;

        [ReadOnly]
        public NativeArray<TerrainAttractiveness> m_AttractiveMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_GroundPollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public AttractivenessParameterData m_AttractivenessParameterData;

        [ReadOnly]
        public float m_TerrainHeight;

        [ReadOnly]
        public float3 m_RaycastPosition;

        public NativeValue<float> m_LandValueResult;

        public NativeValue<float> m_TerrainAttractiveResult;

        public NativeValue<float> m_AirPollutionResult;

        public NativeValue<float> m_NoisePollutionResult;

        public NativeValue<float> m_GroundPollutionResult;

        public void Execute()
        {
            int cellIndex = GetCellIndex(m_RaycastPosition);
            m_LandValueResult.value = m_LandValueMap[cellIndex].m_LandValue;
            TerrainAttractiveness attractiveness = TerrainAttractivenessSystemGetAttractiveness(m_RaycastPosition, m_AttractiveMap);
            m_TerrainAttractiveResult.value = TerrainAttractivenessSystem.EvaluateAttractiveness(m_TerrainHeight, attractiveness, m_AttractivenessParameterData);
            m_GroundPollutionResult.value = GroundPollutionSystemGetPollution(m_RaycastPosition, m_GroundPollutionMap).m_Pollution;
            m_AirPollutionResult.value = AirPollutionSystemGetPollution(m_RaycastPosition, m_AirPollutionMap).m_Pollution;
            m_NoisePollutionResult.value = NoisePollutionSystemGetPollution(m_RaycastPosition, m_NoisePollutionMap).m_Pollution;
        }

        public static int GetCellIndex(float3 pos)
        {
            int num = kMapSize / LandValueSystem.kTextureSize;
            return Mathf.FloorToInt((kMapSize / 2 + pos.x) / num) + Mathf.FloorToInt((kMapSize / 2 + pos.z) / num) * LandValueSystem.kTextureSize;
        }
    }

}

