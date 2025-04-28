using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

[BurstCompile]
public struct ZoneAmbienceUpdateJob : IJobParallelFor
{
    public NativeArray<ZoneAmbienceCell> m_ZoneMap;

    public void Execute(int index)
    {
        ZoneAmbienceCell zoneAmbienceCell = this.m_ZoneMap[index];
        this.m_ZoneMap[index] = new ZoneAmbienceCell
        {
            m_Value = zoneAmbienceCell.m_Accumulator,
            m_Accumulator = default(ZoneAmbiences)
        };
    }
}

/*
		[Preserve]
		protected override void OnUpdate()
		{
			ZoneAmbienceUpdateJob zoneAmbienceUpdateJob = default(ZoneAmbienceUpdateJob);
			zoneAmbienceUpdateJob.m_ZoneMap = base.m_Map;
			ZoneAmbienceUpdateJob jobData = zoneAmbienceUpdateJob;
			base.Dependency = IJobParallelForExtensions.Schedule(jobData, ZoneAmbienceSystem.kTextureSize * ZoneAmbienceSystem.kTextureSize, ZoneAmbienceSystem.kTextureSize, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency));
			base.AddWriter(base.Dependency);
			base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
		}
*/