using Game.Effects;
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
            float3 cameraPosition = this.m_CameraPosition;
            float num = TerrainUtils.SampleHeight(ref this.m_TerrainData, this.m_CameraPosition);
            this.m_CameraPosition.y -= num;
            for (int i = 0; i < this.m_AmbienceEntities.Length; i++)
            {
                Entity entity = this.m_AmbienceEntities[i];
                Entity entity2 = this.m_NearAmbienceEntities[i];
                AudioGroupingSettingsData audioGroupingSettingsData = this.m_Settings[i];
                if (!this.m_EffectInstances.HasComponent(entity))
                {
                    continue;
                }
                float num2 = 0f;
                float num3 = 0f;
                switch (audioGroupingSettingsData.m_Type)
                {
                    case GroupAmbienceType.Traffic:
                        num2 = TrafficAmbienceSystem.GetTrafficAmbience2(this.m_CameraPosition, this.m_TrafficAmbienceMap, 1f / audioGroupingSettingsData.m_Scale).m_Traffic;
                        break;
                    case GroupAmbienceType.Forest:
                    case GroupAmbienceType.NightForest:
                        {
                            GroupAmbienceType groupAmbienceType = (this.m_EffectFlagData.m_IsNightTime ? GroupAmbienceType.NightForest : GroupAmbienceType.Forest);
                            if (audioGroupingSettingsData.m_Type == groupAmbienceType && !this.IsNearForestOnFire(cameraPosition))
                            {
                                num2 = ZoneAmbienceSystem.GetZoneAmbience(GroupAmbienceType.Forest, this.m_CameraPosition, this.m_AmbienceMap, 1f / this.m_Settings[i].m_Scale);
                                if (entity2 != Entity.Null)
                                {
                                    num3 = ZoneAmbienceSystem.GetZoneAmbienceNear(GroupAmbienceType.Forest, this.m_CameraPosition, this.m_AmbienceMap, this.m_Settings[i].m_NearWeight, 1f / this.m_Settings[i].m_Scale);
                                }
                            }
                            break;
                        }
                    case GroupAmbienceType.Rain:
                        if (this.m_IsRaining)
                        {
                            num2 = math.min(1f / audioGroupingSettingsData.m_Scale, math.max(0f, this.m_Precipitation) * 2f);
                            num3 = num2;
                        }
                        break;
                    default:
                        num2 = ZoneAmbienceSystem.GetZoneAmbience(audioGroupingSettingsData.m_Type, this.m_CameraPosition, this.m_AmbienceMap, 1f / audioGroupingSettingsData.m_Scale);
                        if (entity2 != Entity.Null)
                        {
                            num3 = ZoneAmbienceSystem.GetZoneAmbienceNear(audioGroupingSettingsData.m_Type, this.m_CameraPosition, this.m_AmbienceMap, this.m_Settings[i].m_NearWeight, 1f / audioGroupingSettingsData.m_Scale);
                        }
                        break;
                }
                bool flag = true;
                Entity prefab = this.m_PrefabRefs[entity].m_Prefab;
                bool flag2 = (this.m_EffectDatas[prefab].m_Flags.m_RequiredFlags & EffectConditionFlags.Cold) != 0;
                bool flag3 = (this.m_EffectDatas[prefab].m_Flags.m_ForbiddenFlags & EffectConditionFlags.Cold) != 0;
                if (flag2 || flag3)
                {
                    bool isColdSeason = this.m_EffectFlagData.m_IsColdSeason;
                    flag = (flag2 && isColdSeason) || (flag3 && !isColdSeason);
                }
                if (num2 > 0.001f && flag)
                {
                    EffectInstance value = this.m_EffectInstances[entity];
                    float num4 = math.saturate(audioGroupingSettingsData.m_Scale * num2);
                    num4 *= math.saturate((audioGroupingSettingsData.m_Height.y - this.m_CameraPosition.y) / (audioGroupingSettingsData.m_Height.y - audioGroupingSettingsData.m_Height.x));
                    num4 = math.lerp(value.m_Intensity, num4, audioGroupingSettingsData.m_FadeSpeed);
                    value.m_Position = cameraPosition;
                    value.m_Rotation = quaternion.identity;
                    value.m_Intensity = math.saturate(num4);
                    this.m_EffectInstances[entity] = value;
                    this.m_SourceUpdateData.Add(entity, new Game.Objects.Transform
                    {
                        m_Position = cameraPosition,
                        m_Rotation = quaternion.identity
                    });
                }
                else
                {
                    if (this.m_EffectInstances.HasComponent(entity))
                    {
                        EffectInstance value2 = this.m_EffectInstances[entity];
                        value2.m_Intensity = 0f;
                        this.m_EffectInstances[entity] = value2;
                    }
                    this.m_SourceUpdateData.Remove(entity);
                }
                flag = true;
                if (entity2 != Entity.Null)
                {
                    prefab = this.m_PrefabRefs[entity2].m_Prefab;
                    flag2 = (this.m_EffectDatas[prefab].m_Flags.m_RequiredFlags & EffectConditionFlags.Cold) != 0;
                    flag3 = (this.m_EffectDatas[prefab].m_Flags.m_ForbiddenFlags & EffectConditionFlags.Cold) != 0;
                    if (flag2 || flag3)
                    {
                        bool isColdSeason2 = this.m_EffectFlagData.m_IsColdSeason;
                        flag = (flag2 && isColdSeason2) || (flag3 && !isColdSeason2);
                    }
                }
                if (num3 > 0.001f && flag)
                {
                    EffectInstance value3 = this.m_EffectInstances[entity2];
                    float num5 = math.saturate(audioGroupingSettingsData.m_Scale * num3);
                    num5 *= math.saturate((audioGroupingSettingsData.m_NearHeight.y - this.m_CameraPosition.y) / (audioGroupingSettingsData.m_NearHeight.y - audioGroupingSettingsData.m_NearHeight.x));
                    num5 = math.lerp(value3.m_Intensity, num5, audioGroupingSettingsData.m_FadeSpeed);
                    value3.m_Position = cameraPosition;
                    value3.m_Rotation = quaternion.identity;
                    value3.m_Intensity = math.saturate(num5);
                    this.m_EffectInstances[entity2] = value3;
                    this.m_SourceUpdateData.Add(entity2, new Game.Objects.Transform
                    {
                        m_Position = cameraPosition,
                        m_Rotation = quaternion.identity
                    });
                }
                else
                {
                    if (this.m_EffectInstances.HasComponent(entity2))
                    {
                        EffectInstance value4 = this.m_EffectInstances[entity2];
                        value4.m_Intensity = 0f;
                        this.m_EffectInstances[entity2] = value4;
                    }
                    this.m_SourceUpdateData.Remove(entity2);
                }
            }
        }

        private bool IsNearForestOnFire(float3 cameraPosition)
        {
            for (int i = 0; i < this.m_OnFireTrees.Length; i++)
            {
                Entity entity = this.m_OnFireTrees[i];
                if (this.m_TransformData.HasComponent(entity) && math.distancesq(this.m_TransformData[entity].m_Position, cameraPosition) < this.m_ForestFireDistance * this.m_ForestFireDistance)
                {
                    return true;
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
    if (GameManager.instance.gameMode == GameMode.Game && !GameManager.instance.isGameLoading)
    {
        if (this.m_AmbienceEntities.Length == 0 || !base.EntityManager.HasComponent<EffectInstance>(this.m_AmbienceEntities[0]))
        {
            this.Initialize();
        }
        Camera main = Camera.main;
        if (!(main == null))
        {
            float3 cameraPosition = main.transform.position;
            AudioGroupingMiscSetting singleton = this.m_AudioGroupingMiscSettingQuery.GetSingleton<AudioGroupingMiscSetting>();
            this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Prefabs_EffectData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            this.__TypeHandle.__Game_Effects_EffectInstance_RW_ComponentLookup.Update(ref base.CheckedStateRef);
            AudioGroupingJob audioGroupingJob = default(AudioGroupingJob);
            audioGroupingJob.m_CameraPosition = cameraPosition;
            audioGroupingJob.m_SourceUpdateData = this.m_AudioManager.GetSourceUpdateData(out var deps);
            audioGroupingJob.m_TrafficAmbienceMap = this.m_TrafficAmbienceSystem.GetMap(readOnly: true, out var dependencies);
            audioGroupingJob.m_AmbienceMap = this.m_ZoneAmbienceSystem.GetMap(readOnly: true, out var dependencies2);
            audioGroupingJob.m_Settings = this.m_Settings;
            audioGroupingJob.m_EffectFlagData = this.m_EffectFlagSystem.GetData();
            audioGroupingJob.m_AmbienceEntities = this.m_AmbienceEntities;
            audioGroupingJob.m_NearAmbienceEntities = this.m_NearAmbienceEntities;
            audioGroupingJob.m_OnFireTrees = this.m_OnFireTreeQuery.ToEntityArray(Allocator.TempJob);
            audioGroupingJob.m_EffectInstances = this.__TypeHandle.__Game_Effects_EffectInstance_RW_ComponentLookup;
            audioGroupingJob.m_EffectDatas = this.__TypeHandle.__Game_Prefabs_EffectData_RO_ComponentLookup;
            audioGroupingJob.m_PrefabRefs = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
            audioGroupingJob.m_TransformData = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
            audioGroupingJob.m_TerrainData = this.m_TerrainSystem.GetHeightData();
            audioGroupingJob.m_ForestFireDistance = singleton.m_ForestFireDistance;
            audioGroupingJob.m_Precipitation = this.m_ClimateSystem.precipitation;
            audioGroupingJob.m_IsRaining = this.m_ClimateSystem.isRaining;
            AudioGroupingJob jobData = audioGroupingJob;
            base.Dependency = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(JobHandle.CombineDependencies(dependencies2, deps), dependencies, base.Dependency));
            this.m_TerrainSystem.AddCPUHeightReader(base.Dependency);
            this.m_AudioManager.AddSourceUpdateWriter(base.Dependency);
            this.m_TrafficAmbienceSystem.AddReader(base.Dependency);
        }
    }
}
*/