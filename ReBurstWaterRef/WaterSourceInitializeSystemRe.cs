using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace MapExtPDX
{
    [BurstCompile]
    public struct InitializeWaterSourcesJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        public ComponentTypeHandle<Game.Simulation.WaterSourceData> m_SourceType;

        [ReadOnly]
        public ComponentTypeHandle<Transform> m_TransformType;

        [ReadOnly]
        public ComponentLookup<Game.Prefabs.WaterSourceData> m_PrefabSourceDatas;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Game.Simulation.WaterSourceData> nativeArray = chunk.GetNativeArray(ref this.m_SourceType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            NativeArray<Transform> nativeArray3 = chunk.GetNativeArray(ref this.m_TransformType);
            for (int i = 0; i < chunk.Count; i++)
            {
                Game.Prefabs.WaterSourceData waterSourceData = this.m_PrefabSourceDatas[nativeArray2[i].m_Prefab];
                Game.Simulation.WaterSourceData waterSourceData2 = nativeArray[i];
                waterSourceData2.m_Amount = waterSourceData.m_Amount;
                waterSourceData2.m_Radius = waterSourceData.m_Radius;
                if (waterSourceData2.m_ConstantDepth != 2 && waterSourceData2.m_ConstantDepth != 3)
                {
                    waterSourceData2.m_Multiplier = WaterSystem.CalculateSourceMultiplier(waterSourceData2, nativeArray3[i].m_Position);
                }
                waterSourceData2.m_Polluted = waterSourceData.m_InitialPolluted;
                nativeArray[i] = waterSourceData2;
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
			this.__TypeHandle.__Game_Prefabs_WaterSourceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Simulation_WaterSourceData_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			InitializeWaterSourcesJob initializeWaterSourcesJob = default(InitializeWaterSourcesJob);
			initializeWaterSourcesJob.m_SourceType = this.__TypeHandle.__Game_Simulation_WaterSourceData_RW_ComponentTypeHandle;
			initializeWaterSourcesJob.m_PrefabRefType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
			initializeWaterSourcesJob.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
			initializeWaterSourcesJob.m_PrefabSourceDatas = this.__TypeHandle.__Game_Prefabs_WaterSourceData_RO_ComponentLookup;
			InitializeWaterSourcesJob jobData = initializeWaterSourcesJob;
			base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, this.m_WaterSourceQuery, base.Dependency);
		}
*/
