using System.Runtime.CompilerServices;
using Colossal.Collections;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.UI.Localization;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game.UI.Tooltip;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    public struct TempResult
    {
        public AllowedWaterTypes m_Types;

        public int m_Production;

        public int m_MaxCapacity;
    }

    [BurstCompile]
    public struct TempJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        [ReadOnly]
        public ComponentTypeHandle<Temp> m_TempType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

        [ReadOnly]
        public BufferTypeHandle<Game.Objects.SubObject> m_SubObjectType;

        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<WaterPumpingStationData> m_PumpDatas;

        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> m_Transforms;

        [ReadOnly]
        public ComponentLookup<Game.Simulation.WaterSourceData> m_WaterSources;

        [ReadOnly]
        public NativeArray<GroundWater> m_GroundWaterMap;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        public NativeReference<TempResult> m_Result;

        public WaterPipeParameterData m_Parameters;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            ref TempResult reference = ref this.m_Result.ValueAsRef();
            NativeArray<PrefabRef> nativeArray = chunk.GetNativeArray(ref this.m_PrefabType);
            NativeArray<Temp> nativeArray2 = chunk.GetNativeArray(ref this.m_TempType);
            NativeArray<Game.Objects.Transform> nativeArray3 = chunk.GetNativeArray(ref this.m_TransformType);
            BufferAccessor<Game.Objects.SubObject> bufferAccessor = chunk.GetBufferAccessor(ref this.m_SubObjectType);
            BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
            for (int i = 0; i < chunk.Count; i++)
            {
                if ((nativeArray2[i].m_Flags & (TempFlags.Create | TempFlags.Modify | TempFlags.Upgrade)) == 0)
                {
                    continue;
                }
                this.m_PumpDatas.TryGetComponent(nativeArray[i].m_Prefab, out var componentData);
                if (bufferAccessor2.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref componentData, bufferAccessor2[i], ref this.m_Prefabs, ref this.m_PumpDatas);
                }
                int num = 0;
                if (componentData.m_Types != AllowedWaterTypes.None)
                {
                    if ((componentData.m_Types & AllowedWaterTypes.Groundwater) != AllowedWaterTypes.None)
                    {
                        int num2 = Mathf.RoundToInt(math.clamp((float)GroundWaterSystemGetGroundWater(nativeArray3[i].m_Position, this.m_GroundWaterMap).m_Max / this.m_Parameters.m_GroundwaterPumpEffectiveAmount, 0f, 1f) * (float)componentData.m_Capacity);
                        num += num2;
                    }
                    if ((componentData.m_Types & AllowedWaterTypes.SurfaceWater) != AllowedWaterTypes.None && bufferAccessor.Length != 0)
                    {
                        DynamicBuffer<Game.Objects.SubObject> dynamicBuffer = bufferAccessor[i];
                        for (int j = 0; j < dynamicBuffer.Length; j++)
                        {
                            Entity subObject = dynamicBuffer[j].m_SubObject;
                            if (this.m_WaterSources.HasComponent(subObject) && this.m_Transforms.TryGetComponent(subObject, out var componentData2))
                            {
                                float surfaceWaterAvailability = WaterPumpingStationAISystem.GetSurfaceWaterAvailability(componentData2.m_Position, componentData.m_Types, this.m_WaterSurfaceData, this.m_Parameters.m_SurfaceWaterPumpEffectiveDepth);
                                num += Mathf.RoundToInt(surfaceWaterAvailability * (float)componentData.m_Capacity);
                            }
                        }
                    }
                }
                else
                {
                    num = componentData.m_Capacity;
                }
                reference.m_Types |= componentData.m_Types;
                reference.m_Production += math.min(num, componentData.m_Capacity);
                reference.m_MaxCapacity += componentData.m_Capacity;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

    [BurstCompile]
    public struct GroundWaterPumpJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        [ReadOnly]
        public ComponentTypeHandle<Temp> m_TempType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<WaterPumpingStationData> m_PumpDatas;

        [ReadOnly]
        public NativeArray<GroundWater> m_GroundWaterMap;

        public NativeParallelHashMap<int2, int> m_PumpCapacityMap;

        public NativeList<int2> m_TempGroundWaterPumpCells;

        public WaterPipeParameterData m_Parameters;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<PrefabRef> nativeArray = chunk.GetNativeArray(ref this.m_PrefabType);
            NativeArray<Temp> nativeArray2 = chunk.GetNativeArray(ref this.m_TempType);
            NativeArray<Game.Objects.Transform> nativeArray3 = chunk.GetNativeArray(ref this.m_TransformType);
            BufferAccessor<InstalledUpgrade> bufferAccessor = chunk.GetBufferAccessor(ref this.m_InstalledUpgradeType);
            bool flag = nativeArray2.Length != 0;
            for (int i = 0; i < chunk.Count; i++)
            {
                if (flag && (nativeArray2[i].m_Flags & (TempFlags.Create | TempFlags.Modify | TempFlags.Upgrade)) == 0)
                {
                    continue;
                }
                this.m_PumpDatas.TryGetComponent(nativeArray[i].m_Prefab, out var componentData);
                if (bufferAccessor.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref componentData, bufferAccessor[i], ref this.m_Prefabs, ref this.m_PumpDatas);
                }
                if ((componentData.m_Types & AllowedWaterTypes.Groundwater) != AllowedWaterTypes.None && GroundWaterSystemTryGetCell(nativeArray3[i].m_Position, out var cell))
                {
                    int num = Mathf.CeilToInt(math.clamp((float)GroundWaterSystemGetGroundWater(nativeArray3[i].m_Position, this.m_GroundWaterMap).m_Max / this.m_Parameters.m_GroundwaterPumpEffectiveAmount, 0f, 1f) * (float)componentData.m_Capacity);
                    if (!this.m_PumpCapacityMap.ContainsKey(cell))
                    {
                        this.m_PumpCapacityMap.Add(cell, num);
                    }
                    else
                    {
                        this.m_PumpCapacityMap[cell] += num;
                    }
                    if (flag)
                    {
                        this.m_TempGroundWaterPumpCells.Add(in cell);
                    }
                }
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }
}
