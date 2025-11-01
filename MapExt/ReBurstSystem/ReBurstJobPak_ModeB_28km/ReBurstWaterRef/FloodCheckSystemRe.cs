using Colossal.Mathematics;
using Game.Events;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct FloodCheckJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

        [ReadOnly]
        public ComponentTypeHandle<WaterLevelChange> m_WaterLevelChangeType;

        [ReadOnly]
        public ComponentTypeHandle<Duration> m_DurationType;

        [ReadOnly]
        public ComponentTypeHandle<Transform> m_TransformType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        [ReadOnly]
        public ComponentLookup<InDanger> m_InDangerData;

        [ReadOnly]
        public ComponentLookup<WaterLevelChange> m_WaterLevelChangeData;

        [ReadOnly]
        public ComponentLookup<WaterLevelChangeData> m_PrefabWaterLevelChangeData;

        [ReadOnly]
        public ComponentLookup<ObjectGeometryData> m_PrefaObjectGeometryData;

        [ReadOnly]
        public ComponentLookup<PlaceableObjectData> m_PrefaPlaceableObjectData;

        [ReadOnly]
        public uint m_UpdateFrameIndex;

        [ReadOnly]
        public uint m_SimulationFrame;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public NativeList<ArchetypeChunk> m_WaterLevelChangeChunks;

        [ReadOnly]
        public EntityArchetype m_SubmergeArchetype;

        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (chunk.GetSharedComponent(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
            {
                return;
            }
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<Transform> nativeArray2 = chunk.GetNativeArray(ref this.m_TransformType);
            NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            for (int i = 0; i < nativeArray2.Length; i++)
            {
                Transform transform = nativeArray2[i];
                if (this.IsFlooded(transform.m_Position, out var depth))
                {
                    Entity entity = nativeArray[i];
                    PrefabRef prefabRef = nativeArray3[i];
                    if ((!this.m_PrefaObjectGeometryData.TryGetComponent(prefabRef.m_Prefab, out var componentData) || (componentData.m_Flags & GeometryFlags.CanSubmerge) == 0) && (!this.m_PrefaPlaceableObjectData.TryGetComponent(prefabRef.m_Prefab, out var componentData2) || (componentData2.m_Flags & (PlacementFlags.Floating | PlacementFlags.Swaying)) != (PlacementFlags.Floating | PlacementFlags.Swaying)))
                    {
                        Entity @event = this.FindFloodEvent(entity, transform.m_Position);
                        Entity e = this.m_CommandBuffer.CreateEntity(unfilteredChunkIndex, this.m_SubmergeArchetype);
                        this.m_CommandBuffer.SetComponent(unfilteredChunkIndex, e, new Submerge
                        {
                            m_Event = @event,
                            m_Target = entity,
                            m_Depth = depth
                        });
                    }
                }
            }
        }

        private Entity FindFloodEvent(Entity entity, float3 position)
        {
            if (this.m_InDangerData.HasComponent(entity))
            {
                InDanger inDanger = this.m_InDangerData[entity];
                if (this.m_WaterLevelChangeData.HasComponent(inDanger.m_Event))
                {
                    return inDanger.m_Event;
                }
            }
            Entity result = Entity.Null;
            float num = 0.001f;
            for (int i = 0; i < this.m_WaterLevelChangeChunks.Length; i++)
            {
                ArchetypeChunk archetypeChunk = this.m_WaterLevelChangeChunks[i];
                NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(this.m_EntityType);
                NativeArray<WaterLevelChange> nativeArray2 = archetypeChunk.GetNativeArray(ref this.m_WaterLevelChangeType);
                NativeArray<Duration> nativeArray3 = archetypeChunk.GetNativeArray(ref this.m_DurationType);
                NativeArray<PrefabRef> nativeArray4 = archetypeChunk.GetNativeArray(ref this.m_PrefabRefType);
                for (int j = 0; j < nativeArray2.Length; j++)
                {
                    WaterLevelChange waterLevelChange = nativeArray2[j];
                    Duration duration = nativeArray3[j];
                    PrefabRef prefabRef = nativeArray4[j];
                    WaterLevelChangeData waterLevelChangeData = this.m_PrefabWaterLevelChangeData[prefabRef.m_Prefab];
                    if (duration.m_StartFrame <= this.m_SimulationFrame && waterLevelChangeData.m_ChangeType == WaterLevelChangeType.Sine)
                    {
                        float num2 = (float)(this.m_SimulationFrame - duration.m_StartFrame) / 60f;
                        float num3 = (float)(duration.m_EndFrame - CellMapSystemRe.TsunamiEndDelay - duration.m_StartFrame) / 60f; //
                        float num4 = CellMapSystemRe.WaveSpeed * 60f; //
                        float num5 = num2 * num4;
                        float num6 = num5 - num3 * num4;
                        float2 @float = CellMapSystemRe.kMapSize / 2 * -waterLevelChange.m_Direction; //
                        float t;
                        float num7 = MathUtils.Distance(new Line2(@float, @float + MathUtils.Right(waterLevelChange.m_Direction)), position.xz, out t);
                        float num8 = math.lerp(num5, num6, 0.5f);
                        float num9 = math.smoothstep((num5 - num6) * 0.75f, 0f, num7 - num8);
                        if (num9 > num)
                        {
                            result = nativeArray[j];
                            num = num9;
                        }
                    }
                }
            }
            return result;
        }

        private bool IsFlooded(float3 position, out float depth)
        {
            float num = WaterUtils.SampleDepth(ref this.m_WaterSurfaceData, position);
            if (num > 0.5f)
            {
                num += TerrainUtils.SampleHeight(ref this.m_TerrainHeightData, position) - position.y;
                if (num > 0.5f)
                {
                    depth = num;
                    return true;
                }
            }
            depth = 0f;
            return false;
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}