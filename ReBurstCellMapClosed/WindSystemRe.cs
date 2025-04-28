using System;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX
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
            float3 cellCenter = WindSimulationSystem.GetCellCenter(index);
            cellCenter.y = TerrainUtils.SampleHeight(ref this.m_TerrainHeightData, cellCenter) + 25f;
            float num = math.max(0f, (float)WindSimulationSystem.kResolution.z * (cellCenter.y - TerrainUtils.ToWorldSpace(ref this.m_TerrainHeightData, 0f)) / TerrainUtils.ToWorldSpace(ref this.m_TerrainHeightData, 65535f) - 0.5f);
            int3 cell = new int3(index % WindSystem.kTextureSize, index / WindSystem.kTextureSize, Math.Min(Mathf.FloorToInt(num), WindSimulationSystem.kResolution.z - 1));
            int3 cell2 = new int3(cell.x, cell.y, Math.Min(cell.z + 1, WindSimulationSystem.kResolution.z - 1));
            float2 xy = WindSimulationSystem.GetCenterVelocity(cell, this.m_Source).xy;
            float2 xy2 = WindSimulationSystem.GetCenterVelocity(cell2, this.m_Source).xy;
            float2 wind = math.lerp(xy, xy2, math.frac(num));
            this.m_WindMap[index] = new Wind
            {
                m_Wind = wind
            };
        }
    }

}


/*
[Preserve]
protected override void OnUpdate()
{
    TerrainHeightData heightData = this.m_TerrainSystem.GetHeightData();
    if (heightData.isCreated)
    {
        WindCopyJob windCopyJob = default(WindCopyJob);
        windCopyJob.m_WindMap = base.m_Map;
        windCopyJob.m_Source = this.m_WindSimulationSystem.GetCells(out var deps);
        windCopyJob.m_TerrainHeightData = heightData;
        WindCopyJob jobData = windCopyJob;
        base.Dependency = jobData.Schedule(base.m_Map.Length, JobHandle.CombineDependencies(deps, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency)));
        base.AddWriter(base.Dependency);
        this.m_TerrainSystem.AddCPUHeightReader(base.Dependency);
        this.m_WindSimulationSystem.AddReader(base.Dependency);
        this.m_WindTextureSystem.RequireUpdate();
    }
}
*/