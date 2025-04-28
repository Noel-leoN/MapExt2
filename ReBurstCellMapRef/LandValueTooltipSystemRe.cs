using Colossal.Collections;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX
{
    [BurstCompile]
    public struct LandValueTooltipJob : IJob
    {
        [ReadOnly]
        public NativeArray<LandValueCell> m_LandValueMap;

        [ReadOnly]
        public NativeArray<TerrainAttractiveness> m_AttractiveMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_GroundPollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public AttractivenessParameterData m_AttractivenessParameterData;

        [ReadOnly]
        public float m_TerrainHeight;

        [ReadOnly]
        public float3 m_RaycastPosition;

        public NativeValue<float> m_LandValueResult;

        public NativeValue<float> m_TerrainAttractiveResult;

        public NativeValue<float> m_AirPollutionResult;

        public NativeValue<float> m_NoisePollutionResult;

        public NativeValue<float> m_GroundPollutionResult;

        public void Execute()
        {
            int cellIndex = LandValueSystem.GetCellIndex(this.m_RaycastPosition);
            this.m_LandValueResult.value = this.m_LandValueMap[cellIndex].m_LandValue;
            TerrainAttractiveness attractiveness = TerrainAttractivenessSystem.GetAttractiveness(this.m_RaycastPosition, this.m_AttractiveMap);
            this.m_TerrainAttractiveResult.value = TerrainAttractivenessSystem.EvaluateAttractiveness(this.m_TerrainHeight, attractiveness, this.m_AttractivenessParameterData);
            this.m_GroundPollutionResult.value = GroundPollutionSystem.GetPollution(this.m_RaycastPosition, this.m_GroundPollutionMap).m_Pollution;
            this.m_AirPollutionResult.value = AirPollutionSystem.GetPollution(this.m_RaycastPosition, this.m_AirPollutionMap).m_Pollution;
            this.m_NoisePollutionResult.value = NoisePollutionSystem.GetPollution(this.m_RaycastPosition, this.m_NoisePollutionMap).m_Pollution;
        }
    }

}

/*
    [Preserve]
    protected override void OnDestroy()
    {
        this.m_LandValueResult.Dispose();
        this.m_TerrainAttractiveResult.Dispose();
        this.m_NoisePollutionResult.Dispose();
        this.m_AirPollutionResult.Dispose();
        this.m_GroundPollutionResult.Dispose();
        base.OnDestroy();
    }

    private bool IsInfomodeActivated()
    {
        if (this.m_PrefabSystem.TryGetPrefab<InfoviewPrefab>(this.m_LandValueParameterQuery.GetSingleton<LandValueParameterData>().m_LandValueInfoViewPrefab, out var prefab))
        {
            return this.m_ToolSystem.activeInfoview == prefab;
        }
        return false;
    }

    [Preserve]
    protected override void OnUpdate()
    {
        if (this.IsInfomodeActivated() || this.m_LandValueDebugSystem.Enabled)
        {
            base.CompleteDependency();
            this.m_LandValueTooltip.value = this.m_LandValueResult.value;
            base.AddMouseTooltip(this.m_LandValueTooltip);
            if (this.m_LandValueDebugSystem.Enabled)
            {
                if (this.m_TerrainAttractiveResult.value > 0f)
                {
                    this.m_TerrainAttractiveTooltip.value = this.m_TerrainAttractiveResult.value;
                    base.AddMouseTooltip(this.m_TerrainAttractiveTooltip);
                }
                if (this.m_AirPollutionResult.value > 0f)
                {
                    this.m_AirPollutionTooltip.value = this.m_AirPollutionResult.value;
                    base.AddMouseTooltip(this.m_AirPollutionTooltip);
                }
                if (this.m_GroundPollutionResult.value > 0f)
                {
                    this.m_GroundPollutionTooltip.value = this.m_GroundPollutionResult.value;
                    base.AddMouseTooltip(this.m_GroundPollutionTooltip);
                }
                if (this.m_NoisePollutionResult.value > 0f)
                {
                    this.m_NoisePollutionTooltip.value = this.m_NoisePollutionResult.value;
                    base.AddMouseTooltip(this.m_NoisePollutionTooltip);
                }
            }
            this.m_LandValueResult.value = 0f;
            this.m_TerrainAttractiveResult.value = 0f;
            this.m_AirPollutionResult.value = 0f;
            this.m_GroundPollutionResult.value = 0f;
            this.m_NoisePollutionResult.value = 0f;
            this.m_ToolRaycastSystem.typeMask = TypeMask.Terrain | TypeMask.Water;
            this.m_ToolRaycastSystem.GetRaycastResult(out var result);
            TerrainHeightData data = this.m_TerrainSystem.GetHeightData();
            LandValueTooltipJob landValueTooltipJob = default(LandValueTooltipJob);
            landValueTooltipJob.m_LandValueMap = this.m_LandValueSystem.GetMap(readOnly: true, out var dependencies);
            landValueTooltipJob.m_AttractiveMap = this.m_TerrainAttractivenessSystem.GetMap(readOnly: true, out var dependencies2);
            landValueTooltipJob.m_GroundPollutionMap = this.m_GroundPollutionSystem.GetMap(readOnly: true, out var dependencies3);
            landValueTooltipJob.m_AirPollutionMap = this.m_AirPollutionSystem.GetMap(readOnly: true, out var dependencies4);
            landValueTooltipJob.m_NoisePollutionMap = this.m_NoisePollutionSystem.GetMap(readOnly: true, out var dependencies5);
            landValueTooltipJob.m_TerrainHeight = TerrainUtils.SampleHeight(ref data, result.m_Hit.m_HitPosition);
            landValueTooltipJob.m_AttractivenessParameterData = this.m_AttractivenessParameterQuery.GetSingleton<AttractivenessParameterData>();
            landValueTooltipJob.m_LandValueResult = this.m_LandValueResult;
            landValueTooltipJob.m_NoisePollutionResult = this.m_NoisePollutionResult;
            landValueTooltipJob.m_AirPollutionResult = this.m_AirPollutionResult;
            landValueTooltipJob.m_GroundPollutionResult = this.m_GroundPollutionResult;
            landValueTooltipJob.m_TerrainAttractiveResult = this.m_TerrainAttractiveResult;
            landValueTooltipJob.m_RaycastPosition = result.m_Hit.m_HitPosition;
            LandValueTooltipJob jobData = landValueTooltipJob;
            base.Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(base.Dependency, JobHandle.CombineDependencies(dependencies2, dependencies, JobHandle.CombineDependencies(dependencies3, dependencies4, dependencies5))));
            this.m_LandValueSystem.AddReader(base.Dependency);
            this.m_TerrainAttractivenessSystem.AddReader(base.Dependency);
            this.m_GroundPollutionSystem.AddReader(base.Dependency);
            this.m_AirPollutionSystem.AddReader(base.Dependency);
            this.m_NoisePollutionSystem.AddReader(base.Dependency);
        }
        else
        {
            this.m_LandValueResult.value = 0f;
            this.m_TerrainAttractiveResult.value = 0f;
            this.m_AirPollutionResult.value = 0f;
            this.m_GroundPollutionResult.value = 0f;
            this.m_NoisePollutionResult.value = 0f;
        }
    }
*/