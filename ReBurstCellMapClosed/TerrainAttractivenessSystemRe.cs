using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX
{
    [BurstCompile]
    public struct TerrainAttractivenessPrepareJob : IJobParallelForBatch
    {
        [ReadOnly]
        public TerrainHeightData m_TerrainData;

        [ReadOnly]
        public WaterSurfaceData m_WaterData;

        [ReadOnly]
        public CellMapData<ZoneAmbienceCell> m_ZoneAmbienceData;

        public NativeArray<float3> m_AttractFactorData;

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                float3 cellCenter = TerrainAttractivenessSystem.GetCellCenter(i);
                this.m_AttractFactorData[i] = new float3(WaterUtils.SampleDepth(ref this.m_WaterData, cellCenter), TerrainUtils.SampleHeight(ref this.m_TerrainData, cellCenter), ZoneAmbienceSystem.GetZoneAmbience(GroupAmbienceType.Forest, cellCenter, this.m_ZoneAmbienceData.m_Buffer, 1f));
            }
        }
    }

    [BurstCompile]
    public struct TerrainAttractivenessJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<float3> m_AttractFactorData;

        [ReadOnly]
        public float m_Scale;

        public NativeArray<TerrainAttractiveness> m_AttractivenessMap;

        public AttractivenessParameterData m_AttractivenessParameters;

        public void Execute(int startIndex, int count)
        {
            for (int i = startIndex; i < startIndex + count; i++)
            {
                float3 cellCenter = TerrainAttractivenessSystem.GetCellCenter(i);
                float2 @float = 0;
                int num = Mathf.CeilToInt(math.max(this.m_AttractivenessParameters.m_ForestDistance, this.m_AttractivenessParameters.m_ShoreDistance) / this.m_Scale);
                for (int j = -num; j <= num; j++)
                {
                    for (int k = -num; k <= num; k++)
                    {
                        int num2 = math.min(TerrainAttractivenessSystem.kTextureSize - 1, math.max(0, i % TerrainAttractivenessSystem.kTextureSize + j));
                        int num3 = math.min(TerrainAttractivenessSystem.kTextureSize - 1, math.max(0, i / TerrainAttractivenessSystem.kTextureSize + k));
                        int index = num2 + num3 * TerrainAttractivenessSystem.kTextureSize;
                        float3 float2 = this.m_AttractFactorData[index];
                        float num4 = math.distance(TerrainAttractivenessSystem.GetCellCenter(index), cellCenter);
                        @float.x = math.max(@float.x, math.saturate(1f - num4 / this.m_AttractivenessParameters.m_ForestDistance) * float2.z);
                        @float.y = math.max(@float.y, math.saturate(1f - num4 / this.m_AttractivenessParameters.m_ShoreDistance) * ((float2.x > 2f) ? 1f : 0f));
                    }
                }
                this.m_AttractivenessMap[i] = new TerrainAttractiveness
                {
                    m_ForestBonus = @float.x,
                    m_ShoreBonus = @float.y
                };
            }
        }
    }

}


/*
[Preserve]
protected override void OnUpdate()
{
    TerrainHeightData heightData = this.m_TerrainSystem.GetHeightData();
    TerrainAttractivenessPrepareJob terrainAttractivenessPrepareJob = default(TerrainAttractivenessPrepareJob);
    terrainAttractivenessPrepareJob.m_AttractFactorData = this.m_AttractFactorData;
    terrainAttractivenessPrepareJob.m_TerrainData = heightData;
    terrainAttractivenessPrepareJob.m_WaterData = this.m_WaterSystem.GetSurfaceData(out var deps);
    terrainAttractivenessPrepareJob.m_ZoneAmbienceData = this.m_ZoneAmbienceSystem.GetData(readOnly: true, out var dependencies);
    TerrainAttractivenessPrepareJob jobData = terrainAttractivenessPrepareJob;
    TerrainAttractivenessJob jobData2 = new TerrainAttractivenessJob
    {
        m_Scale = heightData.scale.x * (float)TerrainAttractivenessSystem.kTextureSize,
        m_AttractFactorData = this.m_AttractFactorData,
        m_AttractivenessMap = base.m_Map,
        m_AttractivenessParameters = this.m_AttractivenessParameterGroup.GetSingleton<AttractivenessParameterData>()
    };
    JobHandle jobHandle = jobData.ScheduleBatch(base.m_Map.Length, 4, JobHandle.CombineDependencies(deps, dependencies, base.Dependency));
    this.m_TerrainSystem.AddCPUHeightReader(jobHandle);
    this.m_ZoneAmbienceSystem.AddReader(jobHandle);
    this.m_WaterSystem.AddSurfaceReader(jobHandle);
    base.Dependency = jobData2.ScheduleBatch(base.m_Map.Length, 4, JobHandle.CombineDependencies(base.m_WriteDependencies, base.m_ReadDependencies, jobHandle));
    base.AddWriter(base.Dependency);
    base.Dependency = JobHandle.CombineDependencies(base.m_ReadDependencies, base.m_WriteDependencies, base.Dependency);
}
*/
