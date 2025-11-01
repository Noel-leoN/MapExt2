using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Simulation.CarNavigationSystem;
// using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct ApplyTrafficAmbienceJob : IJob
    {
        public NativeQueue<TrafficAmbienceEffect> m_EffectsQueue;

        public NativeArray<TrafficAmbienceCell> m_TrafficAmbienceMap;

        public void Execute()
        {
            TrafficAmbienceEffect item;
            while (m_EffectsQueue.TryDequeue(out item))
            {
                int2 cell = CellMapSystem<TrafficAmbienceCell>.GetCell(item.m_Position, CellMapSystemRe.kMapSize, TrafficAmbienceSystem.kTextureSize);
                if (cell.x >= 0 && cell.y >= 0 && cell.x < TrafficAmbienceSystem.kTextureSize && cell.y < TrafficAmbienceSystem.kTextureSize)
                {
                    int index = cell.x + cell.y * TrafficAmbienceSystem.kTextureSize;
                    TrafficAmbienceCell value = m_TrafficAmbienceMap[index];
                    value.m_Accumulator += item.m_Amount;
                    m_TrafficAmbienceMap[index] = value;
                }
            }
        }
    }

}
