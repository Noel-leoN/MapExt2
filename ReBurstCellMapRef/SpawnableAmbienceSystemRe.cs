#define UNITY_ASSERTIONS
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

/*
		
*/

/*
		[BurstCompile]
		public struct ApplyAmbienceJob : IJobParallelFor
		{
			public NativeParallelQueue<GroupAmbienceEffect>.Reader m_SpawnableQueue;

			public NativeArray<ZoneAmbienceCell> m_ZoneAmbienceMap;

			public void Execute(int index)
			{
				NativeParallelQueue<GroupAmbienceEffect>.Enumerator enumerator = this.m_SpawnableQueue.GetEnumerator(index);
				while (enumerator.MoveNext())
				{
					GroupAmbienceEffect current = enumerator.Current;
					this.m_ZoneAmbienceMap.ElementAt(current.m_CellIndex).m_Accumulator.AddAmbience(current.m_Type, current.m_Amount);
				}
				enumerator.Dispose();
			}
		}
*/

namespace MapExtPDX
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
            NativeArray<Transform> nativeArray = chunk.GetNativeArray(ref this.m_TransformType);
            BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor(ref this.m_RenterType);
            if (bufferAccessor.Length != 0)
            {
                NativeArray<PrefabRef> nativeArray2 = chunk.GetNativeArray(ref this.m_PrefabType);
                BufferAccessor<Efficiency> bufferAccessor2 = chunk.GetBufferAccessor(ref this.m_EfficiencyType);
                for (int i = 0; i < chunk.Count; i++)
                {
                    Entity prefab = nativeArray2[i].m_Prefab;
                    if (this.m_SpawnableAmbienceDatas.TryGetComponent(prefab, out var componentData) && this.m_BuildingDatas.TryGetComponent(prefab, out var componentData2))
                    {
                        float3 position = nativeArray[i].m_Position;
                        int num = componentData2.m_LotSize.x * componentData2.m_LotSize.y;
                        float amount = (float)(bufferAccessor[i].Length * num) * BuildingUtils.GetEfficiency(bufferAccessor2, i);
                        int2 cell = CellMapSystem<ZoneAmbienceCell>.GetCell(position, CellMapSystem<ZoneAmbienceCell>.kMapSize, ZoneAmbienceSystem.kTextureSize);
                        int num2 = cell.x + cell.y * ZoneAmbienceSystem.kTextureSize;
                        int hashCode = num2 * this.m_Queue.HashRange / (ZoneAmbienceSystem.kTextureSize * ZoneAmbienceSystem.kTextureSize);
                        if (cell.x >= 0 && cell.y >= 0 && cell.x < ZoneAmbienceSystem.kTextureSize && cell.y < ZoneAmbienceSystem.kTextureSize)
                        {
                            this.m_Queue.Enqueue(hashCode, new GroupAmbienceEffect
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
                int2 cell2 = CellMapSystem<ZoneAmbienceCell>.GetCell(nativeArray[j].m_Position, CellMapSystem<ZoneAmbienceCell>.kMapSize, ZoneAmbienceSystem.kTextureSize);
                int num3 = cell2.x + cell2.y * ZoneAmbienceSystem.kTextureSize;
                int hashCode2 = num3 * this.m_Queue.HashRange / (ZoneAmbienceSystem.kTextureSize * ZoneAmbienceSystem.kTextureSize);
                if (cell2.x >= 0 && cell2.y >= 0 && cell2.x < ZoneAmbienceSystem.kTextureSize && cell2.y < ZoneAmbienceSystem.kTextureSize)
                {
                    this.m_Queue.Enqueue(hashCode2, new GroupAmbienceEffect
                    {
                        m_Amount = 1f,
                        m_Type = GroupAmbienceType.Forest,
                        m_CellIndex = num3
                    });
                }
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

        public struct GroupAmbienceEffect
        {
            public GroupAmbienceType m_Type;

            public float m_Amount;

            public int m_CellIndex;
        }
    }

}

/*
    [Preserve]
    protected override void OnUpdate()
    {
        uint updateFrame = SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex, SpawnableAmbienceSystem.kUpdatesPerDay, 16);
        this.m_SpawnableQuery.ResetFilter();
        this.m_SpawnableQuery.SetSharedComponentFilter(new UpdateFrame(updateFrame));
        NativeParallelQueue<GroupAmbienceEffect> nativeParallelQueue = new NativeParallelQueue<GroupAmbienceEffect>(math.max(1, JobsUtility.JobWorkerCount / 2), Allocator.TempJob);
        this.__TypeHandle.__Game_Prefabs_GroupAmbienceData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Buildings_Renter_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
        this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
        SpawnableAmbienceJob jobData = default(SpawnableAmbienceJob);
        jobData.m_PrefabType = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
        jobData.m_RenterType = this.__TypeHandle.__Game_Buildings_Renter_RO_BufferTypeHandle;
        jobData.m_EfficiencyType = this.__TypeHandle.__Game_Buildings_Efficiency_RO_BufferTypeHandle;
        jobData.m_TransformType = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentTypeHandle;
        jobData.m_BuildingDatas = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup;
        jobData.m_SpawnableAmbienceDatas = this.__TypeHandle.__Game_Prefabs_GroupAmbienceData_RO_ComponentLookup;
        jobData.m_Queue = nativeParallelQueue.AsWriter();
        JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, this.m_SpawnableQuery, base.Dependency);
        ApplyAmbienceJob jobData2 = default(ApplyAmbienceJob);
        jobData2.m_SpawnableQueue = nativeParallelQueue.AsReader();
        jobData2.m_ZoneAmbienceMap = this.m_ZoneAmbienceSystem.GetMap(readOnly: false, out var dependencies);
        JobHandle jobHandle2 = IJobParallelForExtensions.Schedule(jobData2, nativeParallelQueue.HashRange, 1, JobHandle.CombineDependencies(jobHandle, dependencies));
        this.m_ZoneAmbienceSystem.AddWriter(jobHandle2);
        nativeParallelQueue.Dispose(jobHandle2);
        base.Dependency = jobHandle;
    }
*/
