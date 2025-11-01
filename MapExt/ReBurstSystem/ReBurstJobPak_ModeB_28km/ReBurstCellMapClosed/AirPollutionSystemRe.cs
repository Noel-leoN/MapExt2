using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    /// <summary>
    /// 重定向引用的外部静态方法/kTextureSize=256
    /// ModeBCD无需变更内部代码
    /// </summary>
    [BurstCompile]
    public struct AirPollutionMoveJob : IJob
    {
        public NativeArray<AirPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<Wind> m_WindMap;

        public PollutionParameterData m_PollutionParameters;

        public RandomSeed m_Random;

        public uint m_Frame;

        public void Execute()
        {
            // 直接引入原系统私有字段
            int kSpread = 3;
            // int kTextureSize = 256;
            // int kUpdatesPerDay = 128;

            // 原始Job代码，重定向静态方法
            NativeArray<AirPollution> nativeArray = new NativeArray<AirPollution>(this.m_PollutionMap.Length, Allocator.Temp);
            Random random = this.m_Random.GetRandom((int)this.m_Frame);
            for (int i = 0; i < this.m_PollutionMap.Length; i++)
            {
                float3 cellCenter = AirPollutionSystemGetCellCenter(i); // 重定向
                Wind wind = WindSystemGetWind(cellCenter, this.m_WindMap); // 重定向
                short pollution = AirPollutionSystemGetPollution(cellCenter - this.m_PollutionParameters.m_WindAdvectionSpeed * new float3(wind.m_Wind.x, 0f, wind.m_Wind.y), this.m_PollutionMap).m_Pollution; // 重定向
                nativeArray[i] = new AirPollution
                {
                    m_Pollution = pollution
                };
            }
            float value = (float)this.m_PollutionParameters.m_AirFade / (float)AirPollutionSystem.kUpdatesPerDay; // 公开字段，直接引用

            // 重定向AirPollutionSystemkTextureSize
            for (int j = 0; j < AirPollutionSystem.kTextureSize; j++)
            {
                for (int k = 0; k < AirPollutionSystem.kTextureSize; k++)
                {
                    int num = j * AirPollutionSystem.kTextureSize + k;
                    int pollution2 = nativeArray[num].m_Pollution;
                    pollution2 += ((k > 0) ? (nativeArray[num - 1].m_Pollution >> kSpread) : 0);
                    pollution2 += ((k < AirPollutionSystem.kTextureSize - 1) ? (nativeArray[num + 1].m_Pollution >> kSpread) : 0);
                    pollution2 += ((j > 0) ? (nativeArray[num - AirPollutionSystem.kTextureSize].m_Pollution >> kSpread) : 0);
                    pollution2 += ((j < AirPollutionSystem.kTextureSize - 1) ? (nativeArray[num + AirPollutionSystem.kTextureSize].m_Pollution >> kSpread) : 0);
                    pollution2 -= (nativeArray[num].m_Pollution >> kSpread - 2) + MathUtils.RoundToIntRandom(ref random, value);
                    pollution2 = math.clamp(pollution2, 0, 32767);
                    this.m_PollutionMap[num] = new AirPollution
                    {
                        m_Pollution = (short)pollution2
                    };
                }
            }
            nativeArray.Dispose();
        }
    } // BusrtJob Struct
} // namespace

