// Game.Simulation.PollutionTriggerSystem.cs
// OnUpdate

using Colossal.Collections;
using Game.Buildings;
using Game.City;
using Game.Objects;
using Game.Prefabs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Simulation;

namespace MapExtPDX.MapExt.ReBurstSystemModeD
{
    [BurstCompile]
    public struct CalculateAverageAirPollutionJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;

        [ReadOnly]
        public ComponentLookup<Transform> m_Transforms;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        public CitizenHappinessParameterData m_HappinessParameters;

        public Entity m_City;

        public NativeAccumulator<AverageFloat>.ParallelWriter m_Result;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
            NativeArray<PropertyRenter> nativeArray = chunk.GetNativeArray(ref this.m_PropertyRenterType);
            for (int i = 0; i < chunk.Count; i++)
            {
                int2 airPollutionBonuses = CitizenHappinessJob.GetAirPollutionBonuses(nativeArray[i].m_Property, ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers, in this.m_HappinessParameters);
                this.m_Result.Accumulate(new AverageFloat
                {
                    m_Total = math.csum(airPollutionBonuses),
                    m_Count = 1
                });
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}

