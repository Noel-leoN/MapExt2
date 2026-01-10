using Game.Simulation;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct WindCopyJob : IJobFor
    {
        public NativeArray<Wind> m_WindMap;

        [ReadOnly]
        public NativeArray<WindSimulationSystem.WindCell> m_Source;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        public void Execute(int index)
        {
            // 重定向
            int kTextureSize = CellMapSystemRe.WindSystemkTextureSize;
            // 重定向
            int3 kResolution = new(kTextureSize, kTextureSize, 16);

            float3 cellCenter = CellMapSystemRe.WindSimulationSystemGetCellCenter(index);
            cellCenter.y = TerrainUtils.SampleHeight(ref m_TerrainHeightData, cellCenter) + 25f;
            float num = math.max(0f, kResolution.z * (cellCenter.y - TerrainUtils.ToWorldSpace(ref m_TerrainHeightData, 0f)) / TerrainUtils.ToWorldSpace(ref m_TerrainHeightData, 65535f) - 0.5f);
            int3 cell = new int3(index % kTextureSize, index / kTextureSize, Math.Min(Mathf.FloorToInt(num), kResolution.z - 1));
            int3 cell2 = new int3(cell.x, cell.y, Math.Min(cell.z + 1, kResolution.z - 1));
            float2 xy = CellMapSystemRe.WindSimulationSystemGetCenterVelocity(cell, m_Source).xy;
            float2 xy2 = CellMapSystemRe.WindSimulationSystemGetCenterVelocity(cell2, m_Source).xy;
            float2 wind = math.lerp(xy, xy2, math.frac(num));
            m_WindMap[index] = new Wind
            {
                m_Wind = wind
            };
        }
        
    }

}
