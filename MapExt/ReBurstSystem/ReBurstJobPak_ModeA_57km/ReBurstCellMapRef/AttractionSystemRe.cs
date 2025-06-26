using Game.Buildings;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct AttractivenessJob : IJobChunk
    {
        public ComponentTypeHandle<AttractivenessProvider> m_AttractivenessType;

        [ReadOnly]
        public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        [ReadOnly]
        public BufferTypeHandle<Efficiency> m_EfficiencyType;

        [ReadOnly]
        public ComponentTypeHandle<Signature> m_SignatureType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Buildings.Park> m_ParkType;

        [ReadOnly]
        public BufferTypeHandle<InstalledUpgrade> m_InstalledUpgradeType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Objects.Transform> m_TransformType;

        [ReadOnly]
        public ComponentLookup<AttractionData> m_AttractionDatas;

        [ReadOnly]
        public ComponentLookup<ParkData> m_ParkDatas;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefData;

        [ReadOnly]
        public CellMapData<TerrainAttractiveness> m_TerrainMap;

        [ReadOnly]
        public TerrainHeightData m_HeightData;

        public AttractivenessParameterData m_Parameters;

        public uint m_UpdateFrameIndex;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
            {
                return;
            }
            NativeArray<PrefabRef> nativeArray = chunk.GetNativeArray(ref m_PrefabType);
            NativeArray<AttractivenessProvider> nativeArray2 = chunk.GetNativeArray(ref m_AttractivenessType);
            BufferAccessor<Efficiency> bufferAccessor = chunk.GetBufferAccessor(ref m_EfficiencyType);
            NativeArray<Game.Buildings.Park> nativeArray3 = chunk.GetNativeArray(ref m_ParkType);
            NativeArray<Game.Objects.Transform> nativeArray4 = chunk.GetNativeArray(ref m_TransformType);
            BufferAccessor<InstalledUpgrade> bufferAccessor2 = chunk.GetBufferAccessor(ref m_InstalledUpgradeType);
            bool flag = chunk.Has(ref m_SignatureType);
            for (int i = 0; i < chunk.Count; i++)
            {
                Entity prefab = nativeArray[i].m_Prefab;
                AttractionData data = default;
                if (m_AttractionDatas.HasComponent(prefab))
                {
                    data = m_AttractionDatas[prefab];
                }
                if (bufferAccessor2.Length != 0)
                {
                    UpgradeUtils.CombineStats(ref data, bufferAccessor2[i], ref m_PrefabRefData, ref m_AttractionDatas);
                }
                float num = data.m_Attractiveness;
                if (!flag)
                {
                    num *= BuildingUtils.GetEfficiency(bufferAccessor, i);
                }
                if (chunk.Has(ref m_ParkType) && m_ParkDatas.HasComponent(prefab))
                {
                    Game.Buildings.Park park = nativeArray3[i];
                    ParkData parkData = m_ParkDatas[prefab];
                    float num2 = parkData.m_MaintenancePool > 0 ? park.m_Maintenance / (float)parkData.m_MaintenancePool : 1f;
                    num *= 0.8f + 0.2f * num2;
                }
                if (chunk.Has(ref m_TransformType))
                {
                    float3 position = nativeArray4[i].m_Position;
                    num *= 1f + 0.01f * TerrainAttractivenessSystemEvaluateAttractiveness(position, m_TerrainMap, m_HeightData, m_Parameters, default);
                }
                AttractivenessProvider value = new AttractivenessProvider
                {
                    m_Attractiveness = Mathf.RoundToInt(num)
                };
                nativeArray2[i] = value;
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

    }

}
