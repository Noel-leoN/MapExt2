using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    [BurstCompile]
    public struct NoisePollutionSwapJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<NoisePollution> m_PollutionMap;

        public void Execute(int index)
        {
            NoisePollution value = m_PollutionMap[index];
            int num = index % NoisePollutionSystem.kTextureSize;
            int num2 = index / NoisePollutionSystem.kTextureSize;
            short num3 = (short)(num > 0 ? m_PollutionMap[index - 1].m_PollutionTemp : 0);
            short num4 = (short)(num < NoisePollutionSystem.kTextureSize - 1 ? m_PollutionMap[index + 1].m_PollutionTemp : 0);
            short num5 = (short)(num2 > 0 ? m_PollutionMap[index - NoisePollutionSystem.kTextureSize].m_PollutionTemp : 0);
            short num6 = (short)(num2 < NoisePollutionSystem.kTextureSize - 1 ? m_PollutionMap[index + NoisePollutionSystem.kTextureSize].m_PollutionTemp : 0);
            short num7 = (short)(num > 0 && num2 > 0 ? m_PollutionMap[index - 1 - NoisePollutionSystem.kTextureSize].m_PollutionTemp : 0);
            short num8 = (short)(num < NoisePollutionSystem.kTextureSize - 1 && num2 > 0 ? m_PollutionMap[index + 1 - NoisePollutionSystem.kTextureSize].m_PollutionTemp : 0);
            short num9 = (short)(num > 0 && num2 < NoisePollutionSystem.kTextureSize - 1 ? m_PollutionMap[index - 1 + NoisePollutionSystem.kTextureSize].m_PollutionTemp : 0);
            short num10 = (short)(num < NoisePollutionSystem.kTextureSize - 1 && num2 < NoisePollutionSystem.kTextureSize - 1 ? m_PollutionMap[index + 1 + NoisePollutionSystem.kTextureSize].m_PollutionTemp : 0);
            value.m_Pollution = (short)(value.m_PollutionTemp / 4 + (num3 + num4 + num5 + num6) / 8 + (num7 + num8 + num9 + num10) / 16);
            /// 原版计算过大，改为1/4较正常
            value.m_Pollution /= 4;
            ///
            m_PollutionMap[index] = value;
        }
    }

}