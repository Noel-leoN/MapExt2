using Game.Effects;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace MapExtPDX
{
    [BurstCompile]
    public struct WeatherAudioJob : IJob
    {
        public ComponentLookup<EffectInstance> m_EffectInstances;

        public SourceUpdateData m_SourceUpdateData;

        [ReadOnly]
        public int2 m_WaterTextureSize;

        [ReadOnly]
        public float3 m_CameraPosition;

        [ReadOnly]
        public int m_WaterAudioNearDistance;

        [ReadOnly]
        public Entity m_WaterAudioEntity;

        [ReadOnly]
        public WeatherAudioData m_WeatherAudioData;

        [ReadOnly]
        public NativeArray<SurfaceWater> m_WaterDepths;

        [ReadOnly]
        public TerrainHeightData m_TerrainData;

        public void Execute()
        {
            if (WeatherAudioJob.NearWater(this.m_CameraPosition, this.m_WaterTextureSize, this.m_WaterAudioNearDistance, ref this.m_WaterDepths))
            {
                EffectInstance value = this.m_EffectInstances[this.m_WaterAudioEntity];
                float y = TerrainUtils.SampleHeight(ref this.m_TerrainData, this.m_CameraPosition);
                float x = math.lerp(value.m_Intensity, this.m_WeatherAudioData.m_WaterAudioIntensity, this.m_WeatherAudioData.m_WaterFadeSpeed);
                value.m_Position = new float3(this.m_CameraPosition.x, y, this.m_CameraPosition.z);
                value.m_Rotation = quaternion.identity;
                value.m_Intensity = math.saturate(x);
                this.m_EffectInstances[this.m_WaterAudioEntity] = value;
                this.m_SourceUpdateData.Add(this.m_WaterAudioEntity, new Transform
                {
                    m_Position = this.m_CameraPosition,
                    m_Rotation = quaternion.identity
                });
            }
            else if (this.m_EffectInstances.HasComponent(this.m_WaterAudioEntity))
            {
                EffectInstance value2 = this.m_EffectInstances[this.m_WaterAudioEntity];
                if (value2.m_Intensity <= 0.01f)
                {
                    this.m_SourceUpdateData.Remove(this.m_WaterAudioEntity);
                    return;
                }
                float x2 = math.lerp(value2.m_Intensity, 0f, this.m_WeatherAudioData.m_WaterFadeSpeed);
                value2.m_Intensity = math.saturate(x2);
                this.m_EffectInstances[this.m_WaterAudioEntity] = value2;
                this.m_SourceUpdateData.Add(this.m_WaterAudioEntity, new Transform
                {
                    m_Position = this.m_CameraPosition,
                    m_Rotation = quaternion.identity
                });
            }
        }

        private static bool NearWater(float3 position, int2 texSize, int distance, ref NativeArray<SurfaceWater> depthsCPU)
        {
            float2 @float = (float)WaterSystem.kMapSize / (float2)texSize;
            int2 cell = WaterSystem.GetCell(position - new float3(@float.x / 2f, 0f, @float.y / 2f), WaterSystem.kMapSize, texSize);
            int2 @int = default(int2);
            for (int i = -distance; i <= distance; i++)
            {
                for (int j = -distance; j <= distance; j++)
                {
                    @int.x = math.clamp(cell.x + i, 0, texSize.x - 2);
                    @int.y = math.clamp(cell.y + j, 0, texSize.y - 2);
                    if (depthsCPU[@int.x + 1 + texSize.x * @int.y].m_Depth > 0f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }

}

/*
		[Preserve]
		protected override void OnUpdate()
		{
			if (this.m_WaterSystem.Loaded && this.m_CameraUpdateSystem.activeViewer != null && this.m_CameraUpdateSystem.activeCameraController != null)
			{
				if (this.m_SmallWaterAudioEntity == Entity.Null)
				{
					this.Initialize();
				}
				IGameCameraController activeCameraController = this.m_CameraUpdateSystem.activeCameraController;
				float3 position = this.m_CameraUpdateSystem.activeViewer.position;
				if (base.EntityManager.HasComponent<EffectInstance>(this.m_SmallWaterAudioEntity) && activeCameraController.zoom < (float)this.m_WaterAudioEnabledZoom)
				{
					this.__TypeHandle.__Game_Effects_EffectInstance_RW_ComponentLookup.Update(ref base.CheckedStateRef);
					WeatherAudioJob weatherAudioJob = default(WeatherAudioJob);
					weatherAudioJob.m_WaterTextureSize = this.m_WaterSystem.TextureSize;
					weatherAudioJob.m_WaterAudioNearDistance = this.m_WaterAudioNearDistance;
					weatherAudioJob.m_CameraPosition = position;
					weatherAudioJob.m_WaterAudioEntity = this.m_SmallWaterAudioEntity;
					weatherAudioJob.m_WeatherAudioData = base.EntityManager.GetComponentData<WeatherAudioData>(this.m_WeatherAudioEntityQuery.GetSingletonEntity());
					weatherAudioJob.m_SourceUpdateData = this.m_AudioManager.GetSourceUpdateData(out var deps);
					weatherAudioJob.m_TerrainData = this.m_TerrainSystem.GetHeightData();
					weatherAudioJob.m_EffectInstances = this.__TypeHandle.__Game_Effects_EffectInstance_RW_ComponentLookup;
					weatherAudioJob.m_WaterDepths = this.m_WaterSystem.GetDepths(out var deps2);
					WeatherAudioJob jobData = weatherAudioJob;
					base.Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(deps, deps2, base.Dependency));
					this.m_TerrainSystem.AddCPUHeightReader(base.Dependency);
					this.m_AudioManager.AddSourceUpdateWriter(base.Dependency);
				}
			}
		}
*/