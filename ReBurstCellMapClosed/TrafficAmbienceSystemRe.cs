using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace MapExtPDX
{
	[BurstCompile]
	public struct TrafficAmbienceUpdateJob : IJobParallelFor
	{
		public NativeArray<TrafficAmbienceCell> m_TrafficMap;

		public void Execute(int index)
		{
			TrafficAmbienceCell trafficAmbienceCell = this.m_TrafficMap[index];
			this.m_TrafficMap[index] = new TrafficAmbienceCell
			{
				m_Traffic = trafficAmbienceCell.m_Accumulator,
				m_Accumulator = 0f
			};
		}
	}

}
/*
		[Preserve]
		protected override void OnUpdate()
		{
			TrafficAmbienceUpdateJob trafficAmbienceUpdateJob = default(TrafficAmbienceUpdateJob);
			trafficAmbienceUpdateJob.m_TrafficMap = base.m_Map;
			TrafficAmbienceUpdateJob jobData = trafficAmbienceUpdateJob;
			base.Dependency = IJobParallelForExtensions.Schedule(jobData, TrafficAmbienceSystem.kTextureSize * TrafficAmbienceSystem.kTextureSize, TrafficAmbienceSystem.kTextureSize, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency));
			base.AddWriter(base.Dependency);
			base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
		}
*/