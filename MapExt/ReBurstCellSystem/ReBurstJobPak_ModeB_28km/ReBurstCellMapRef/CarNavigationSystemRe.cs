using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Simulation.CarNavigationSystem;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct ApplyTrafficAmbienceJob : IJob
    {
        public NativeQueue<TrafficAmbienceEffect> m_EffectsQueue;

        public NativeArray<TrafficAmbienceCell> m_TrafficAmbienceMap;

        public void Execute()
        {
            // ÷ÿ∂®œÚ
            int kTextureSize = CellMapSystemRe.TrafficAmbienceSystemkTextureSize;

            TrafficAmbienceEffect item;
            while (m_EffectsQueue.TryDequeue(out item))
            {
                int2 cell = CellMapSystem<TrafficAmbienceCell>.GetCell(item.m_Position, CellMapSystemRe.kMapSize, kTextureSize);
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
