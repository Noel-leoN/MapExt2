using Game.Effects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeC.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
{
    [BurstCompile]
    public struct AudioGroupingJob : IJob
    {
        public ComponentLookup<EffectInstance> m_EffectInstances;

        [ReadOnly]
        public ComponentLookup<EffectData> m_EffectDatas;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefs;

        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> m_TransformData;

        [ReadOnly]
        public NativeArray<TrafficAmbienceCell> m_TrafficAmbienceMap;

        [ReadOnly]
        public NativeArray<ZoneAmbienceCell> m_AmbienceMap;

        [ReadOnly]
        public NativeArray<AudioGroupingSettingsData> m_Settings;

        public SourceUpdateData m_SourceUpdateData;

        public EffectFlagSystem.EffectFlagData m_EffectFlagData;

        public float3 m_CameraPosition;

        public NativeArray<Entity> m_AmbienceEntities;

        public NativeArray<Entity> m_NearAmbienceEntities;

        public NativeArray<float> m_CurrentValues;

        [DeallocateOnJobCompletion]
        public NativeArray<Entity> m_OnFireTrees;

        [ReadOnly]
        public TerrainHeightData m_TerrainData;

        [ReadOnly]
        public float m_ForestFireDistance;

        [ReadOnly]
        public float m_Precipitation;

        [ReadOnly]
        public bool m_IsRaining;

        public void Execute()
        {
            float3 @float = m_CameraPosition;
            float num = TerrainUtils.SampleHeight(ref m_TerrainData, m_CameraPosition);
            m_CameraPosition.y -= num;
            for (int i = 0; i < m_AmbienceEntities.Length; i++)
            {
                Entity entity = m_AmbienceEntities[i];
                Entity entity2 = m_NearAmbienceEntities[i];
                AudioGroupingSettingsData audioGroupingSettingsData = m_Settings[i];
                if (!m_EffectInstances.HasComponent(entity))
                {
                    continue;
                }
                float num2 = 0f;
                float num3 = 0f;
                switch (audioGroupingSettingsData.m_Type)
                {
                    case GroupAmbienceType.Traffic:
                        num2 = TrafficAmbienceSystemGetTrafficAmbience2(m_CameraPosition, m_TrafficAmbienceMap, 1f / audioGroupingSettingsData.m_Scale).m_Traffic;
                        break;
                    case GroupAmbienceType.Forest:
                    case GroupAmbienceType.NightForest:
                        {
                            GroupAmbienceType groupAmbienceType = m_EffectFlagData.m_IsNightTime ? GroupAmbienceType.NightForest : GroupAmbienceType.Forest;
                            if (audioGroupingSettingsData.m_Type == groupAmbienceType && !IsNearForestOnFire(@float))
                            {
                                num2 = ZoneAmbienceSystemGetZoneAmbience(GroupAmbienceType.Forest, m_CameraPosition, m_AmbienceMap, 1f / m_Settings[i].m_Scale);
                                if (entity2 != Entity.Null)
                                {
                                    num3 = ZoneAmbienceSystemGetZoneAmbienceNear(GroupAmbienceType.Forest, m_CameraPosition, m_AmbienceMap, m_Settings[i].m_NearWeight, 1f / m_Settings[i].m_Scale);
                                }
                            }
                            break;
                        }
                    case GroupAmbienceType.Rain:
                        if (m_IsRaining)
                        {
                            num2 = math.min(1f / audioGroupingSettingsData.m_Scale, math.max(0f, m_Precipitation) * 2f);
                            num3 = num2;
                        }
                        break;
                    default:
                        num2 = ZoneAmbienceSystemGetZoneAmbience(audioGroupingSettingsData.m_Type, m_CameraPosition, m_AmbienceMap, 1f / audioGroupingSettingsData.m_Scale);
                        if (entity2 != Entity.Null)
                        {
                            num3 = ZoneAmbienceSystemGetZoneAmbienceNear(audioGroupingSettingsData.m_Type, m_CameraPosition, m_AmbienceMap, m_Settings[i].m_NearWeight, 1f / audioGroupingSettingsData.m_Scale);
                        }
                        break;
                }
                m_CurrentValues[(int)audioGroupingSettingsData.m_Type] = num2;
                bool flag = true;
                Entity prefab = m_PrefabRefs[entity].m_Prefab;
                bool flag2 = (m_EffectDatas[prefab].m_Flags.m_RequiredFlags & EffectConditionFlags.Cold) != 0;
                bool flag3 = (m_EffectDatas[prefab].m_Flags.m_ForbiddenFlags & EffectConditionFlags.Cold) != 0;
                if (flag2 || flag3)
                {
                    bool isColdSeason = m_EffectFlagData.m_IsColdSeason;
                    flag = flag2 && isColdSeason || flag3 && !isColdSeason;
                }
                if (num2 > 0.001f && flag)
                {
                    EffectInstance value = m_EffectInstances[entity];
                    float num4 = math.saturate(audioGroupingSettingsData.m_Scale * num2);
                    num4 *= math.saturate((audioGroupingSettingsData.m_Height.y - m_CameraPosition.y) / (audioGroupingSettingsData.m_Height.y - audioGroupingSettingsData.m_Height.x));
                    num4 = math.lerp(value.m_Intensity, num4, audioGroupingSettingsData.m_FadeSpeed);
                    value.m_Position = @float;
                    value.m_Rotation = quaternion.identity;
                    value.m_Intensity = math.saturate(num4);
                    m_EffectInstances[entity] = value;
                    m_SourceUpdateData.Add(entity, new Game.Objects.Transform
                    {
                        m_Position = @float,
                        m_Rotation = quaternion.identity
                    });
                }
                else
                {
                    if (m_EffectInstances.HasComponent(entity))
                    {
                        EffectInstance value2 = m_EffectInstances[entity];
                        value2.m_Intensity = 0f;
                        m_EffectInstances[entity] = value2;
                    }
                    m_SourceUpdateData.Remove(entity);
                }
                flag = true;
                if (entity2 != Entity.Null)
                {
                    prefab = m_PrefabRefs[entity2].m_Prefab;
                    flag2 = (m_EffectDatas[prefab].m_Flags.m_RequiredFlags & EffectConditionFlags.Cold) != 0;
                    flag3 = (m_EffectDatas[prefab].m_Flags.m_ForbiddenFlags & EffectConditionFlags.Cold) != 0;
                    if (flag2 || flag3)
                    {
                        bool isColdSeason2 = m_EffectFlagData.m_IsColdSeason;
                        flag = flag2 && isColdSeason2 || flag3 && !isColdSeason2;
                    }
                }
                if (num3 > 0.001f && flag)
                {
                    EffectInstance value3 = m_EffectInstances[entity2];
                    float num5 = math.saturate(audioGroupingSettingsData.m_Scale * num3);
                    num5 *= math.saturate((audioGroupingSettingsData.m_NearHeight.y - m_CameraPosition.y) / (audioGroupingSettingsData.m_NearHeight.y - audioGroupingSettingsData.m_NearHeight.x));
                    num5 = math.lerp(value3.m_Intensity, num5, audioGroupingSettingsData.m_FadeSpeed);
                    value3.m_Position = @float;
                    value3.m_Rotation = quaternion.identity;
                    value3.m_Intensity = math.saturate(num5);
                    m_EffectInstances[entity2] = value3;
                    m_SourceUpdateData.Add(entity2, new Game.Objects.Transform
                    {
                        m_Position = @float,
                        m_Rotation = quaternion.identity
                    });
                }
                else
                {
                    if (m_EffectInstances.HasComponent(entity2))
                    {
                        EffectInstance value4 = m_EffectInstances[entity2];
                        value4.m_Intensity = 0f;
                        m_EffectInstances[entity2] = value4;
                    }
                    m_SourceUpdateData.Remove(entity2);
                }
            }
        }

        private bool IsNearForestOnFire(float3 cameraPosition)
        {
            for (int i = 0; i < m_OnFireTrees.Length; i++)
            {
                Entity entity = m_OnFireTrees[i];
                if (m_TransformData.HasComponent(entity) && math.distancesq(m_TransformData[entity].m_Position, cameraPosition) < m_ForestFireDistance * m_ForestFireDistance)
                {
                    return true;
                }
            }
            return false;
        }
    }


}
