using Game.Buildings;
using Game.Citizens;
using Game.Objects;
using Game.Simulation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static MapExtPDX.MapExt.ReBurstSystemModeA.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
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
                int2 cell = CellMapSystem<PopulationCell>.GetCell(m_Transforms[entity].m_Position, kMapSize, PopulationToGridSystem.kTextureSize);
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