using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct PolluteGroundWaterJob : IJob
    {
        public NativeArray<GroundWater> m_GroundWaterMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        public void Execute()
        {
            for (int i = 0; i < m_GroundWaterMap.Length; i++)
            {
                GroundWater value = m_GroundWaterMap[i];
                GroundPollution pollution = GroundPollutionSystemGetPollution(GetCellCenter(i, GroundWaterSystem.kTextureSize), m_PollutionMap);
                if (pollution.m_Pollution > 0)
                {
                    value.m_Polluted = (short)math.min(value.m_Amount, value.m_Polluted + pollution.m_Pollution / 200);
                    m_GroundWaterMap[i] = value;
                }
            }
        }
    }

}
