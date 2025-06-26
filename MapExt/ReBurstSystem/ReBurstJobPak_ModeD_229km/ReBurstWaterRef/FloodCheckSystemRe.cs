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
using static MapExtPDX.MapExt.ReBurstSystemModeD.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeD
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
            if (chunk.GetSharedComponent(m_UpdateFrameType).m_Index != m_UpdateFrameIndex)
            {
                return;
            }
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
            NativeArray<Transform> nativeArray2 = chunk.GetNativeArray(ref m_TransformType);
            NativeArray<PrefabRef> nativeArray3 = chunk.GetNativeArray(ref m_PrefabRefType);
            for (int i = 0; i < nativeArray2.Length; i++)
            {
                Transform transform = nativeArray2[i];
                if (IsFlooded(transform.m_Position, out var depth))
                {
                    Entity entity = nativeArray[i];
                    PrefabRef prefabRef = nativeArray3[i];
                    if ((!m_PrefaObjectGeometryData.TryGetComponent(prefabRef.m_Prefab, out var componentData) || (componentData.m_Flags & GeometryFlags.CanSubmerge) == 0) && (!m_PrefaPlaceableObjectData.TryGetComponent(prefabRef.m_Prefab, out var componentData2) || (componentData2.m_Flags & (PlacementFlags.Floating | PlacementFlags.Swaying)) != (PlacementFlags.Floating | PlacementFlags.Swaying)))
                    {
                        Entity entity2 = FindFloodEvent(entity, transform.m_Position);
                        Entity e = m_CommandBuffer.CreateEntity(unfilteredChunkIndex, m_SubmergeArchetype);
                        m_CommandBuffer.SetComponent(unfilteredChunkIndex, e, new Submerge
                        {
                            m_Event = entity2,
                            m_Target = entity,
                            m_Depth = depth
                        });
                    }
                }
            }
        }


        private Entity FindFloodEvent(Entity entity, float3 position)
        {
            if (m_InDangerData.HasComponent(entity))
            {
                InDanger inDanger = m_InDangerData[entity];
                if (m_WaterLevelChangeData.HasComponent(inDanger.m_Event))
                {
                    return inDanger.m_Event;
                }
            }
            Entity result = Entity.Null;
            float num = 0.001f;
            for (int i = 0; i < m_WaterLevelChangeChunks.Length; i++)
            {
                ArchetypeChunk archetypeChunk = m_WaterLevelChangeChunks[i];
                NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
                NativeArray<WaterLevelChange> nativeArray2 = archetypeChunk.GetNativeArray(ref m_WaterLevelChangeType);
                NativeArray<Duration> nativeArray3 = archetypeChunk.GetNativeArray(ref m_DurationType);
                NativeArray<PrefabRef> nativeArray4 = archetypeChunk.GetNativeArray(ref m_PrefabRefType);
                for (int j = 0; j < nativeArray2.Length; j++)
                {
                    WaterLevelChange waterLevelChange = nativeArray2[j];
                    Duration duration = nativeArray3[j];
                    PrefabRef prefabRef = nativeArray4[j];
                    WaterLevelChangeData waterLevelChangeData = m_PrefabWaterLevelChangeData[prefabRef.m_Prefab];
                    if (duration.m_StartFrame <= m_SimulationFrame && waterLevelChangeData.m_ChangeType == WaterLevelChangeType.Sine)
                    {
                        float num2 = (m_SimulationFrame - duration.m_StartFrame) / 60f;
                        float num3 = (duration.m_EndFrame - TsunamiEndDelay - duration.m_StartFrame) / 60f;
                        float num4 = WaveSpeed * 60f;
                        float num5 = num2 * num4;
                        float num6 = num5 - num3 * num4;
                        float2 @float = kMapSize / 2 * -waterLevelChange.m_Direction;
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
            float num = WaterUtils.SampleDepth(ref m_WaterSurfaceData, position);
            if (num > 0.5f)
            {
                num += TerrainUtils.SampleHeight(ref m_TerrainHeightData, position) - position.y;
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
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}
/*
    [Preserve]
    protected override void OnUpdate()
    {
        uint updateFrameIndex = (this.m_SimulationSystem.frameIndex >> 4) & 0xFu;
        this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Prefabs_WaterLevelChangeData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_WaterLevelChange_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_InDanger_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_Duration_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_WaterLevelChange_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
        FloodCheckJob floodCheckJob = default(FloodCheckJob);
        floodCheckJob.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
        floodCheckJob.m_UpdateFrameType = this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle;
        floodCheckJob.m_WaterLevelChangeType = this.__TypeHandle.__Game_Events_WaterLevelChange_RO_ComponentTypeHandle;
        floodCheckJob.m_DurationType = this.__TypeHandle.__Game_Events_Duration_RO_ComponentTypeHandle;
        floodCheckJob.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
        floodCheckJob.m_PrefabRefType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
        floodCheckJob.m_InDangerData = this.__TypeHandle.__Game_Events_InDanger_RO_ComponentLookup;
        floodCheckJob.m_WaterLevelChangeData = this.__TypeHandle.__Game_Events_WaterLevelChange_RO_ComponentLookup;
        floodCheckJob.m_PrefabWaterLevelChangeData = this.__TypeHandle.__Game_Prefabs_WaterLevelChangeData_RO_ComponentLookup;
        floodCheckJob.m_PrefaObjectGeometryData = this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;
        floodCheckJob.m_UpdateFrameIndex = updateFrameIndex;
        floodCheckJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
        floodCheckJob.m_TerrainHeightData = this.m_TerrainSystem.GetHeightData();
        floodCheckJob.m_WaterSurfaceData = this.m_WaterSystem.GetSurfaceData(out var deps);
        floodCheckJob.m_WaterLevelChangeChunks = this.m_WaterLevelChangeQuery.ToArchetypeChunkListAsync(base.World.UpdateAllocator.ToAllocator, out var outJobHandle);
        floodCheckJob.m_SubmergeArchetype = this.m_SubmergeArchetype;
        floodCheckJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
        FloodCheckJob jobData = floodCheckJob;
        base.Dependency = JobChunkExtensions.ScheduleParallel(jobData, this.m_TargetQuery, JobHandle.CombineDependencies(base.Dependency, deps, outJobHandle));
        this.m_TerrainSystem.AddCPUHeightReader(base.Dependency);
        this.m_WaterSystem.AddSurfaceReader(base.Dependency);
        this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
    }
*/
