using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
// using static MapExtPDX.MapExt.ReBurst.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct TrafficAmbienceUpdateJob : IJobParallelFor
    {
        public NativeArray<TrafficAmbienceCell> m_TrafficMap;

        public void Execute(int index)
        {
            TrafficAmbienceCell trafficAmbienceCell = m_TrafficMap[index];
            m_TrafficMap[index] = new TrafficAmbienceCell
            {
                m_Traffic = trafficAmbienceCell.m_Accumulator,
                m_Accumulator = 0f
            };
        }
    }

}
