using Game.Buildings;
using Game.Citizens;
using Game.Objects;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX
{
    [BurstCompile]
    public struct PopulationToGridJob : IJob
    {
        [ReadOnly]
        public NativeList<Entity> m_Entities;

        public NativeArray<PopulationCell> m_PopulationMap;

        [ReadOnly]
        public BufferLookup<Renter> m_Renters;

        [ReadOnly]
        public ComponentLookup<Transform> m_Transforms;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

        public void Execute()
        {
            for (int i = 0; i < PopulationToGridSystem.kTextureSize * PopulationToGridSystem.kTextureSize; i++)
            {
                m_PopulationMap[i] = default;
            }
            for (int j = 0; j < m_Entities.Length; j++)
            {
                Entity entity = m_Entities[j];
                int num = 0;
                DynamicBuffer<Renter> dynamicBuffer = m_Renters[entity];
                for (int k = 0; k < dynamicBuffer.Length; k++)
                {
                    Entity renter = dynamicBuffer[k].m_Renter;
                    if (m_HouseholdCitizens.HasBuffer(renter))
                    {
                        num += m_HouseholdCitizens[renter].Length;
                    }
                }
                //int2 cell = CellMapSystem<PopulationCell>.GetCell(m_Transforms[entity].m_Position, CellMapSystem<PopulationCell>.kMapSize, PopulationToGridSystem.kTextureSize);
                int2 cell = CellMapSystem<PopulationCell>.GetCell(m_Transforms[entity].m_Position, CellMapSystem<PopulationCell>.kMapSize, PopulationToGridSystem.kTextureSize);
                if (cell.x >= 0 && cell.y >= 0 && cell.x < PopulationToGridSystem.kTextureSize && cell.y < PopulationToGridSystem.kTextureSize)
                {
                    int index = cell.x + cell.y * PopulationToGridSystem.kTextureSize;
                    PopulationCell value = m_PopulationMap[index];
                    value.m_Population += num;
                    m_PopulationMap[index] = value;
                }
            }
        }
    }

}


/*
		[Preserve]
		protected override void OnUpdate()
		{
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup.Update(ref base.CheckedStateRef);
			PopulationToGridJob populationToGridJob = default(PopulationToGridJob);
			populationToGridJob.m_Entities = this.m_ResidentialPropertyQuery.ToEntityListAsync(base.World.UpdateAllocator.ToAllocator, out var outJobHandle);
			populationToGridJob.m_PopulationMap = base.m_Map;
			populationToGridJob.m_Renters = this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup;
			populationToGridJob.m_HouseholdCitizens = this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup;
			populationToGridJob.m_Transforms = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
			PopulationToGridJob jobData = populationToGridJob;
			base.Dependency = IJobExtensions.Schedule(jobData, JobUtils.CombineDependencies(outJobHandle, base.m_WriteDependencies, base.m_ReadDependencies, base.Dependency));
			base.AddWriter(base.Dependency);
		}
*/