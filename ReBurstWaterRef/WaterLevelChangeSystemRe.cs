using Game.Events;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


namespace MapExtPDX
{
    [BurstCompile]
    public struct WaterLevelChangeJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        public ComponentTypeHandle<WaterLevelChange> m_WaterLevelChangeType;

        [ReadOnly]
        public ComponentTypeHandle<Duration> m_DurationType;

        [ReadOnly]
        public ComponentLookup<WaterLevelChangeData> m_PrefabWaterLevelChangeData;

        [ReadOnly]
        public uint m_SimulationFrame;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            NativeArray<WaterLevelChange> nativeArray3 = chunk.GetNativeArray(ref this.m_WaterLevelChangeType);
            NativeArray<Duration> nativeArray4 = chunk.GetNativeArray(ref this.m_DurationType);
            for (int i = 0; i < nativeArray3.Length; i++)
            {
                _ = nativeArray[i];
                PrefabRef prefabRef = nativeArray2[i];
                WaterLevelChange value = nativeArray3[i];
                Duration duration = nativeArray4[i];
                WaterLevelChangeData waterLevelChangeData = this.m_PrefabWaterLevelChangeData[prefabRef.m_Prefab];
                float num = (float)(this.m_SimulationFrame - duration.m_StartFrame) / 60f - waterLevelChangeData.m_EscalationDelay;
                if (num < 0f)
                {
                    continue;
                }
                if (waterLevelChangeData.m_ChangeType == WaterLevelChangeType.Sine)
                {
                    float num2 = (float)(duration.m_EndFrame - WaterLevelChangeSystem.TsunamiEndDelay - duration.m_StartFrame) / 60f;
                    if (num < 0.05f * num2)
                    {
                        value.m_Intensity = -0.2f * value.m_MaxIntensity * math.sin(20f * num / num2 * math.PI);
                    }
                    else if (num < num2)
                    {
                        value.m_Intensity = value.m_MaxIntensity * (0.5f * math.sin(5f * (num - 0.05f * num2) / (0.95f * num2) * 2f * Mathf.PI) + 0.5f * math.saturate((num - 0.05f * num2) / (0.2f * num2)));
                    }
                    else
                    {
                        value.m_Intensity = 0f;
                    }
                    value.m_Intensity *= 4f;
                }
                else
                {
                    _ = waterLevelChangeData.m_ChangeType;
                    _ = 2;
                }
                nativeArray3[i] = value;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}

/*
		[Preserve]
		protected override void OnUpdate()
		{
			this.__TypeHandle.__Game_Prefabs_WaterLevelChangeData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Events_Duration_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Events_WaterLevelChange_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
			WaterLevelChangeJob waterLevelChangeJob = default(WaterLevelChangeJob);
			waterLevelChangeJob.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
			waterLevelChangeJob.m_PrefabRefType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
			waterLevelChangeJob.m_WaterLevelChangeType = this.__TypeHandle.__Game_Events_WaterLevelChange_RW_ComponentTypeHandle;
			waterLevelChangeJob.m_DurationType = this.__TypeHandle.__Game_Events_Duration_RO_ComponentTypeHandle;
			waterLevelChangeJob.m_PrefabWaterLevelChangeData = this.__TypeHandle.__Game_Prefabs_WaterLevelChangeData_RO_ComponentLookup;
			waterLevelChangeJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			WaterLevelChangeJob jobData = waterLevelChangeJob;
			base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, this.m_WaterLevelChangeQuery, base.Dependency);
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
		}
*/