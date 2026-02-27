
using Colossal.Collections;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.ModeB
{
    [BurstCompile]
    public struct SpawnableAmbienceJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabType;

        [ReadOnly]
        public BufferTypeHandle<Renter> m_RenterType;

        [ReadOnly]
        public ComponentTypeHandle<Transform> m_TransformType;

        [ReadOnly]
        public BufferTypeHandle<Efficiency> m_EfficiencyType;

        [ReadOnly]
        public ComponentLookup<GroupAmbienceData> m_SpawnableAmbienceDatas;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        public NativeParallelQueue<GroupAmbienceEffect>.Writer m_Queue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            int kMapSize = XCellMapSystemRe.kMapSize;
            int kTextureSize = XCellMapSystemRe.ZoneAmbienceSystemkTextureSize;

            NativeArray<Transform> nativeArray = chunk.GetNativeArray(ref m_TransformType);
            BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor(ref m_RenterType);
            if (bufferAccessor.Length != 0)
            {
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref m_PrefabType);
                BufferAccessor<Efficiency> bufferAccessor2 = chunk.GetBufferAccessor(ref m_EfficiencyType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity prefab = nativeArray2[i].m_Prefab;
                    if (m_SpawnableAmbienceDatas.TryGetComponent(prefab, out var componentData) && m_BuildingDatas.TryGetComponent(prefab, out var componentData2))
                    {
                        float3 position = nativeArray[i].m_Position;
                        int num = componentData2.m_LotSize.x * componentData2.m_LotSize.y;
                        float amount = bufferAccessor[i].Length * num * BuildingUtils.GetEfficiency(bufferAccessor2, i);
                        int2 cell = CellMapSystem<ZoneAmbienceCell>.GetCell(position, kMapSize, kTextureSize);
                        int num2 = cell.x + cell.y * kTextureSize;
                        int hashCode = num2 * m_Queue.HashRange / (kTextureSize * kTextureSize);
                        if (cell.x >= 0 && cell.y >= 0 && cell.x < kTextureSize && cell.y < kTextureSize)
                        {
                            m_Queue.Enqueue(hashCode, new GroupAmbienceEffect
                            {
                                m_Amount = amount,
                                m_Type = componentData.m_AmbienceType,
                                m_CellIndex = num2
                            });
                        }
                    }
                }
                return;
            }
            for (int j = 0; j < chunk.Count; j++)
            {
                int2 cell2 = CellMapSystem<ZoneAmbienceCell>.GetCell(nativeArray[j].m_Position, kMapSize, kTextureSize);
                int num3 = cell2.x + cell2.y * kTextureSize;
                int hashCode2 = num3 * m_Queue.HashRange / (kTextureSize * kTextureSize);
                if (cell2.x >= 0 && cell2.y >= 0 && cell2.x < kTextureSize && cell2.y < kTextureSize)
                {
                    m_Queue.Enqueue(hashCode2, new GroupAmbienceEffect
                    {
                        m_Amount = 1f,
                        m_Type = GroupAmbienceType.Forest,
                        m_CellIndex = num3
                    });
                }
            }
        }
    }

    // v1.2.5f
    // mod v2.2.0
    [BurstCompile]
    public struct EmitterAmbienceJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<Transform> m_TransformType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        [ReadOnly]
        public ComponentLookup<AmbienceEmitterData> m_AmbienceEmitterDatas;

        public NativeParallelQueue<GroupAmbienceEffect>.Writer m_Queue;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            int kMapSize = XCellMapSystemRe.kMapSize;
            int kTextureSize = XCellMapSystemRe.ZoneAmbienceSystemkTextureSize;

            NativeArray<Transform> nativeArray = chunk.GetNativeArray(ref this.m_TransformType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            for (int i = 0; i < chunk.Count; i++)
            {
                float3 position = nativeArray[i].m_Position;
                PrefabRef prefabRef = nativeArray2[i];
                if (!this.m_AmbienceEmitterDatas.HasComponent(prefabRef.m_Prefab))
                {
                    continue;
                }
                AmbienceEmitterData ambienceEmitterData = this.m_AmbienceEmitterDatas[prefabRef.m_Prefab];
                if (ambienceEmitterData.m_Intensity != 0f)
                {
                    int2 cell = CellMapSystem<ZoneAmbienceCell>.GetCell(position, kMapSize, kTextureSize); // 
                    int num = cell.x + cell.y * kTextureSize;
                    int hashCode = num * this.m_Queue.HashRange / (kTextureSize * kTextureSize);
                    if (cell.x >= 0 && cell.y >= 0 && cell.x < kTextureSize && cell.y < kTextureSize)
                    {
                        this.m_Queue.Enqueue(hashCode, new GroupAmbienceEffect
                        {
                            m_Amount = ambienceEmitterData.m_Intensity,
                            m_Type = ambienceEmitterData.m_AmbienceType,
                            m_CellIndex = num
                        });
                    }
                }
            }
        }
    }

    public struct GroupAmbienceEffect
    {
        public GroupAmbienceType m_Type;

        public float m_Amount;

        public int m_CellIndex;
    }


}


