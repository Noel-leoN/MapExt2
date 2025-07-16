using Game.Objects;
using Game.Prefabs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
//using UnityEngine;
using static MapExtPDX.MapExt.ReBurstSystemModeC.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeC
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
            NativeArray<Game.Simulation.WaterSourceData> nativeArray = chunk.GetNativeArray(ref m_SourceType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabRefType);
            NativeArray<Transform> nativeArray3 = chunk.GetNativeArray(ref m_TransformType);
            for (int i = 0; i < chunk.Count; i++)
            {
                Game.Prefabs.WaterSourceData waterSourceData = m_PrefabSourceDatas[nativeArray2[i].m_Prefab];
                Game.Simulation.WaterSourceData waterSourceData2 = nativeArray[i];
                waterSourceData2.m_Amount = waterSourceData.m_Amount;
                waterSourceData2.m_Radius = waterSourceData.m_Radius;
                if (waterSourceData2.m_ConstantDepth != 2 && waterSourceData2.m_ConstantDepth != 3)
                {
                    waterSourceData2.m_Multiplier = CalculateSourceMultiplier(waterSourceData2, nativeArray3[i].m_Position);
                }
                waterSourceData2.m_Polluted = waterSourceData.m_InitialPolluted;
                nativeArray[i] = waterSourceData2;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

        public static float CalculateSourceMultiplier(Game.Simulation.WaterSourceData source, float3 pos)
        {
            float kCellSize = 56f; // 7f * MapSizeMultiplier;

            if (source.m_Radius < 0.01f)
            {
                return 0f;
            }

            pos.y = 0f;
            int num = UnityEngine.Mathf.CeilToInt(source.m_Radius / kCellSize);
            float num2 = 0f;
            float num3 = source.m_Radius * source.m_Radius;
            int num4 = UnityEngine.Mathf.FloorToInt(pos.x / kCellSize) - num;
            int num5 = UnityEngine.Mathf.FloorToInt(pos.z / kCellSize) - num;
            for (int i = num4; i <= num4 + 2 * num + 1; i++)
            {
                for (int j = num5; j <= num5 + 2 * num + 1; j++)
                {
                    float3 x = new float3(i * kCellSize, 0f, j * kCellSize);
                    num2 += 1f - math.smoothstep(0f, 1f, math.distancesq(x, pos) / num3);
                }
            }

            if (num2 < 0.001f)
            {
                // UnityEngine.Debug.LogWarning($"Warning: water source at {pos} has too small radius to work");
                return 1f;
            }

            return 1f / num2;
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
