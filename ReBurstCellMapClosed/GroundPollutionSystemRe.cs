using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX
{

    [BurstCompile]
    public struct PollutionFadeJob : IJob
    {
        public NativeArray<GroundPollution> m_PollutionMap;

        public PollutionParameterData m_PollutionParameters;

        public RandomSeed m_Random;

        public uint m_Frame;

        public void Execute()
        {
            Unity.Mathematics.Random random = this.m_Random.GetRandom((int)this.m_Frame);
            for (int i = 0; i < this.m_PollutionMap.Length; i++)
            {
                GroundPollution value = this.m_PollutionMap[i];
                if (value.m_Pollution > 0)
                {
                    value.m_Pollution = (short)math.max(0, this.m_PollutionMap[i].m_Pollution - MathUtils.RoundToIntRandom(ref random, (float)this.m_PollutionParameters.m_GroundFade / (float)GroundPollutionSystem.kUpdatesPerDay));
                }
                this.m_PollutionMap[i] = value;
            }
        }
    } 
}

/*

[Preserve]
protected override void OnUpdate()
{
    PollutionFadeJob pollutionFadeJob = default(PollutionFadeJob);
    pollutionFadeJob.m_PollutionMap = base.m_Map;
    pollutionFadeJob.m_PollutionParameters = this.m_PollutionParameterGroup.GetSingleton<PollutionParameterData>();
    pollutionFadeJob.m_Random = RandomSeed.Next();
    pollutionFadeJob.m_Frame = this.m_SimulationSystem.frameIndex;
    PollutionFadeJob jobData = pollutionFadeJob;
    base.Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency));
    base.AddWriter(base.Dependency);
    base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
}
*/