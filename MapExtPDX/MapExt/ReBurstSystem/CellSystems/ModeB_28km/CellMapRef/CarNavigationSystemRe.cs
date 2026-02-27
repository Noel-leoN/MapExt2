using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Simulation.CarNavigationSystem;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.ModeB
{
    [BurstCompile]
    public struct ApplyTrafficAmbienceJob : IJob
    {
        public NativeQueue<TrafficAmbienceEffect> m_EffectsQueue;

        public NativeArray<TrafficAmbienceCell> m_TrafficAmbienceMap;

        public void Execute()
        {
            int kTextureSize = XCellMapSystemRe.TrafficAmbienceSystemkTextureSize;

            TrafficAmbienceEffect item;
            while (m_EffectsQueue.TryDequeue(out item))
            {
                int2 cell = CellMapSystem<TrafficAmbienceCell>.GetCell(item.m_Position, XCellMapSystemRe.kMapSize, kTextureSize);
                if (cell.x >= 0 && cell.y >= 0 && cell.x < kTextureSize && cell.y < kTextureSize)
                {
                    int index = cell.x + cell.y * kTextureSize;
                    TrafficAmbienceCell value = m_TrafficAmbienceMap[index];
                    value.m_Accumulator += item.m_Amount;
                    m_TrafficAmbienceMap[index] = value;
                }
            }
        }
    }

}


