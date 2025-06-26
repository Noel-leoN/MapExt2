using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
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
/*
    [Preserve]
    protected override void OnUpdate()
    {
        PolluteGroundWaterJob polluteGroundWaterJob = default(PolluteGroundWaterJob);
        polluteGroundWaterJob.m_GroundWaterMap = this.m_GroundWaterSystem.GetMap(readOnly: false, out var dependencies);
        polluteGroundWaterJob.m_PollutionMap = this.m_GroundPollutionSystem.GetMap(readOnly: true, out var dependencies2);
        PolluteGroundWaterJob jobData = polluteGroundWaterJob;
        base.Dependency = jobData.Schedule(JobHandle.CombineDependencies(base.Dependency, dependencies, dependencies2));
        this.m_GroundWaterSystem.AddWriter(base.Dependency);
        this.m_GroundPollutionSystem.AddReader(base.Dependency);
    }
*/