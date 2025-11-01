using Game.Effects;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
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
            if (WeatherAudioJob.NearWater(m_CameraPosition, m_WaterTextureSize, m_WaterAudioNearDistance, ref m_WaterDepths))
            {
                EffectInstance value = m_EffectInstances[m_WaterAudioEntity];
                float y = TerrainUtils.SampleHeight(ref m_TerrainData, m_CameraPosition);
                float x = math.lerp(value.m_Intensity, m_WeatherAudioData.m_WaterAudioIntensity, m_WeatherAudioData.m_WaterFadeSpeed);
                value.m_Position = new float3(m_CameraPosition.x, y, m_CameraPosition.z);
                value.m_Rotation = quaternion.identity;
                value.m_Intensity = math.saturate(x);
                m_EffectInstances[m_WaterAudioEntity] = value;
                m_SourceUpdateData.Add(m_WaterAudioEntity, new Transform
                {
                    m_Position = m_CameraPosition,
                    m_Rotation = quaternion.identity
                });
            }
            else if (m_EffectInstances.HasComponent(m_WaterAudioEntity))
            {
                EffectInstance value2 = m_EffectInstances[m_WaterAudioEntity];
                if (value2.m_Intensity <= 0.01f)
                {
                    m_SourceUpdateData.Remove(m_WaterAudioEntity);
                    return;
                }
                float x2 = math.lerp(value2.m_Intensity, 0f, m_WeatherAudioData.m_WaterFadeSpeed);
                value2.m_Intensity = math.saturate(x2);
                m_EffectInstances[m_WaterAudioEntity] = value2;
                m_SourceUpdateData.Add(m_WaterAudioEntity, new Transform
                {
                    m_Position = m_CameraPosition,
                    m_Rotation = quaternion.identity
                });
            }
        }

        private static bool NearWater(float3 position, int2 texSize, int distance, ref NativeArray<SurfaceWater> depthsCPU)
        {
            float2 @float = CellMapSystemRe.kMapSize / (float2)texSize; //
            int2 cell = WaterSystem.GetCell(position - new float3(@float.x / 2f, 0f, @float.y / 2f), CellMapSystemRe.kMapSize, texSize); //
            int2 @int = default;
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