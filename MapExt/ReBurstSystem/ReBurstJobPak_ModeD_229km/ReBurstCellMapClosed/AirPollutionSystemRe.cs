using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeD.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeD
{
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
            int kSpread = 3;
            NativeArray<AirPollution> nativeArray = new NativeArray<AirPollution>(m_PollutionMap.Length, Allocator.Temp);
            Random random = m_Random.GetRandom((int)m_Frame);
            for (int i = 0; i < m_PollutionMap.Length; i++)
            {
                float3 cellCenter = GetCellCenter(i, AirPollutionSystem.kTextureSize);
                Wind wind = WindSystemGetWind(cellCenter, m_WindMap);
                short pollution = AirPollutionSystemGetPollution(cellCenter - m_PollutionParameters.m_WindAdvectionSpeed * new float3(wind.m_Wind.x, 0f, wind.m_Wind.y), m_PollutionMap).m_Pollution;
                nativeArray[i] = new AirPollution
                {
                    m_Pollution = pollution
                };
            }
            float value = m_PollutionParameters.m_AirFade / (float)AirPollutionSystem.kUpdatesPerDay;
            for (int j = 0; j < AirPollutionSystem.kTextureSize; j++)
            {
                for (int k = 0; k < AirPollutionSystem.kTextureSize; k++)
                {
                    int num = j * AirPollutionSystem.kTextureSize + k;
                    int pollution2 = nativeArray[num].m_Pollution;
                    pollution2 += k > 0 ? nativeArray[num - 1].m_Pollution >> kSpread : 0;
                    pollution2 += k < AirPollutionSystem.kTextureSize - 1 ? nativeArray[num + 1].m_Pollution >> kSpread : 0;
                    pollution2 += j > 0 ? nativeArray[num - AirPollutionSystem.kTextureSize].m_Pollution >> kSpread : 0;
                    pollution2 += j < AirPollutionSystem.kTextureSize - 1 ? nativeArray[num + AirPollutionSystem.kTextureSize].m_Pollution >> kSpread : 0;
                    pollution2 -= (nativeArray[num].m_Pollution >> kSpread - 2) + MathUtils.RoundToIntRandom(ref random, value);
                    pollution2 = math.clamp(pollution2, 0, 32767);
                    m_PollutionMap[num] = new AirPollution
                    {
                        m_Pollution = (short)pollution2
                    };
                }
            }
            nativeArray.Dispose();
        }


    }

}

/*
    [Preserve]
    protected override void OnUpdate()
    {
        AirPollutionMoveJob airPollutionMoveJob = default(AirPollutionMoveJob);
        airPollutionMoveJob.m_PollutionMap = base.m_Map;
        airPollutionMoveJob.m_WindMap = this.m_WindSystem.GetMap(readOnly: true, out var dependencies);
        airPollutionMoveJob.m_PollutionParameters = this.m_PollutionParameterQuery.GetSingleton<PollutionParameterData>();
        airPollutionMoveJob.m_Random = RandomSeed.Next();
        airPollutionMoveJob.m_Frame = this.m_SimulationSystem.frameIndex;
        AirPollutionMoveJob jobData = airPollutionMoveJob;
        base.Dependency = IJobExtensions.Schedule(jobData, JobUtils.CombineDependencies(dependencies, base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency));
        this.m_WindSystem.AddReader(base.Dependency);
        base.AddWriter(base.Dependency);
        base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
    }
*/
