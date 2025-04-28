using Colossal.Collections;
using Colossal.Mathematics;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Events;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace MapExtPDX
{
    [BurstCompile]
    public struct WaterDangerJob : IJobChunk
    {
        private struct EndangeredStaticObjectIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public int m_JobIndex;

            public uint m_SimulationFrame;

            public float m_DangerSpeed;

            public float m_DangerHeight;

            public Bounds1 m_PredictionDistance;

            public Entity m_Event;

            public Line2 m_StartLine;

            public WaterLevelChangeData m_WaterLevelChangeData;

            public ComponentLookup<Building> m_BuildingData;

            public ComponentLookup<Game.Buildings.EmergencyShelter> m_EmergencyShelterData;

            public ComponentLookup<InDanger> m_InDangerData;

            public EntityArchetype m_EndangerArchetype;

            public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                if (bounds.m_Bounds.min.y < this.m_DangerHeight)
                {
                    return MathUtils.Intersect(this.m_PredictionDistance, WaterDangerJob.GetDistanceBounds(bounds.m_Bounds.xz, this.m_StartLine));
                }
                return false;
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (bounds.m_Bounds.min.y >= this.m_DangerHeight)
                {
                    return;
                }
                Bounds1 distanceBounds = WaterDangerJob.GetDistanceBounds(bounds.m_Bounds.xz, this.m_StartLine);
                if (!MathUtils.Intersect(this.m_PredictionDistance, distanceBounds) || !this.m_BuildingData.HasComponent(item))
                {
                    return;
                }
                DangerFlags dangerFlags = this.m_WaterLevelChangeData.m_DangerFlags;
                if ((dangerFlags & DangerFlags.Evacuate) != 0 && this.m_EmergencyShelterData.HasComponent(item))
                {
                    dangerFlags &= ~DangerFlags.Evacuate;
                    dangerFlags |= DangerFlags.StayIndoors;
                }
                if (this.m_InDangerData.HasComponent(item))
                {
                    InDanger inDanger = this.m_InDangerData[item];
                    if (inDanger.m_EndFrame >= this.m_SimulationFrame + 64 && (inDanger.m_Event == this.m_Event || !EventUtils.IsWorse(dangerFlags, inDanger.m_Flags)))
                    {
                        return;
                    }
                }
                float num = 30f + (distanceBounds.max - this.m_PredictionDistance.min) / this.m_DangerSpeed;
                Entity e = this.m_CommandBuffer.CreateEntity(this.m_JobIndex, this.m_EndangerArchetype);
                this.m_CommandBuffer.SetComponent(this.m_JobIndex, e, new Endanger
                {
                    m_Event = this.m_Event,
                    m_Target = item,
                    m_Flags = dangerFlags,
                    m_EndFrame = this.m_SimulationFrame + 64 + (uint)(num * 60f)
                });
            }
        }

        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        [ReadOnly]
        public ComponentTypeHandle<WaterLevelChange> m_WaterLevelChangeType;

        [ReadOnly]
        public ComponentTypeHandle<Duration> m_DurationType;

        public ComponentTypeHandle<Game.Events.DangerLevel> m_DangerLevelType;

        [ReadOnly]
        public ComponentLookup<InDanger> m_InDangerData;

        [ReadOnly]
        public ComponentLookup<Building> m_BuildingData;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.EmergencyShelter> m_EmergencyShelterData;

        [ReadOnly]
        public ComponentLookup<WaterLevelChangeData> m_PrefabWaterLevelChangeData;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public uint m_SimulationFrame;

        [ReadOnly]
        public Entity m_City;

        [ReadOnly]
        public EntityArchetype m_EndangerArchetype;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_StaticObjectSearchTree;

        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabRefType);
            NativeArray<WaterLevelChange> nativeArray3 = chunk.GetNativeArray(ref this.m_WaterLevelChangeType);
            NativeArray<Duration> nativeArray4 = chunk.GetNativeArray(ref this.m_DurationType);
            NativeArray<Game.Events.DangerLevel> nativeArray5 = chunk.GetNativeArray(ref this.m_DangerLevelType);
            for (int i = 0; i < nativeArray3.Length; i++)
            {
                Entity eventEntity = nativeArray[i];
                PrefabRef prefabRef = nativeArray2[i];
                WaterLevelChange waterLevelChange = nativeArray3[i];
                Duration duration = nativeArray4[i];
                WaterLevelChangeData waterLevelChangeData = this.m_PrefabWaterLevelChangeData[prefabRef.m_Prefab];
                if (this.m_SimulationFrame < duration.m_EndFrame && waterLevelChangeData.m_DangerFlags != 0)
                {
                    this.FindEndangeredObjects(unfilteredChunkIndex, eventEntity, duration, waterLevelChange, waterLevelChangeData);
                }
                bool flag = this.m_SimulationFrame > duration.m_StartFrame && this.m_SimulationFrame < duration.m_EndFrame;
                nativeArray5[i] = new Game.Events.DangerLevel(flag ? waterLevelChangeData.m_DangerLevel : 0f);
            }
        }

        private void FindEndangeredObjects(int jobIndex, Entity eventEntity, Duration duration, WaterLevelChange waterLevelChange, WaterLevelChangeData waterLevelChangeData)
        {
            float value = 10f;
            float num = 0f;
            DynamicBuffer<CityModifier> modifiers = this.m_CityModifiers[this.m_City];
            CityUtils.ApplyModifier(ref value, modifiers, CityModifierType.DisasterWarningTime);
            if (duration.m_StartFrame > this.m_SimulationFrame)
            {
                value -= (float)(duration.m_StartFrame - this.m_SimulationFrame) / 60f;
            }
            else
            {
                num = (float)(this.m_SimulationFrame - duration.m_StartFrame) / 60f;
            }
            value = math.max(0f, value);
            float num2 = (float)(duration.m_EndFrame - WaterLevelChangeSystem.TsunamiEndDelay - duration.m_StartFrame) / 60f;
            float num3 = WaterSystem.WaveSpeed * 60f;
            float num4 = num * num3;
            float min = num4 - num2 * num3;
            float2 @float = WaterSystem.kMapSize / 2 * -waterLevelChange.m_Direction;
            Line2 startLine = new Line2(@float, @float + MathUtils.Right(waterLevelChange.m_Direction));
            Bounds1 predictionDistance = new Bounds1(min, num4);
            predictionDistance.max += value * num3;
            EndangeredStaticObjectIterator endangeredStaticObjectIterator = default(EndangeredStaticObjectIterator);
            endangeredStaticObjectIterator.m_JobIndex = jobIndex;
            endangeredStaticObjectIterator.m_SimulationFrame = this.m_SimulationFrame;
            endangeredStaticObjectIterator.m_DangerSpeed = num3;
            endangeredStaticObjectIterator.m_DangerHeight = waterLevelChange.m_DangerHeight;
            endangeredStaticObjectIterator.m_PredictionDistance = predictionDistance;
            endangeredStaticObjectIterator.m_Event = eventEntity;
            endangeredStaticObjectIterator.m_StartLine = startLine;
            endangeredStaticObjectIterator.m_WaterLevelChangeData = waterLevelChangeData;
            endangeredStaticObjectIterator.m_BuildingData = this.m_BuildingData;
            endangeredStaticObjectIterator.m_EmergencyShelterData = this.m_EmergencyShelterData;
            endangeredStaticObjectIterator.m_InDangerData = this.m_InDangerData;
            endangeredStaticObjectIterator.m_EndangerArchetype = this.m_EndangerArchetype;
            endangeredStaticObjectIterator.m_CommandBuffer = this.m_CommandBuffer;
            EndangeredStaticObjectIterator iterator = endangeredStaticObjectIterator;
            this.m_StaticObjectSearchTree.Iterate(ref iterator);
        }

        private static Bounds1 GetDistanceBounds(Bounds2 bounds, Line2 line)
        {
            float t;
            float4 x = new float4(MathUtils.Distance(line, bounds.min, out t), MathUtils.Distance(line, new float2(bounds.min.x, bounds.max.y), out t), MathUtils.Distance(line, bounds.max, out t), MathUtils.Distance(line, new float2(bounds.max.x, bounds.min.y), out t));
            Bounds1 bounds2 = new Bounds1(math.cmin(x), math.cmax(x));
            if (MathUtils.Intersect(bounds, line, out var _))
            {
                return bounds2 | 0f;
            }
            return bounds2;
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
        this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Prefabs_WaterLevelChangeData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Buildings_EmergencyShelter_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_InDanger_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_DangerLevel_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_Duration_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Events_WaterLevelChange_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
        WaterDangerJob jobData = default(WaterDangerJob);
        jobData.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
        jobData.m_PrefabRefType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
        jobData.m_WaterLevelChangeType = this.__TypeHandle.__Game_Events_WaterLevelChange_RW_ComponentTypeHandle;
        jobData.m_DurationType = this.__TypeHandle.__Game_Events_Duration_RO_ComponentTypeHandle;
        jobData.m_DangerLevelType = this.__TypeHandle.__Game_Events_DangerLevel_RW_ComponentTypeHandle;
        jobData.m_InDangerData = this.__TypeHandle.__Game_Events_InDanger_RO_ComponentLookup;
        jobData.m_BuildingData = this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup;
        jobData.m_EmergencyShelterData = this.__TypeHandle.__Game_Buildings_EmergencyShelter_RO_ComponentLookup;
        jobData.m_PrefabWaterLevelChangeData = this.__TypeHandle.__Game_Prefabs_WaterLevelChangeData_RO_ComponentLookup;
        jobData.m_CityModifiers = this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup;
        jobData.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
        jobData.m_City = this.m_CitySystem.City;
        jobData.m_EndangerArchetype = this.m_EndangerArchetype;
        jobData.m_StaticObjectSearchTree = this.m_ObjectSearchSystem.GetStaticSearchTree(readOnly: true, out var dependencies);
        jobData.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
        JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, this.m_WaterLevelChangeQuery, JobHandle.CombineDependencies(base.Dependency, dependencies));
        this.m_ObjectSearchSystem.AddStaticSearchTreeReader(jobHandle);
        this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle);
        base.Dependency = jobHandle;
    }
*/