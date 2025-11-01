// Game.UI.InGame.PollutionInfoviewUISystem.cs
// PerformUpdate()

using Game.Buildings;
using Game.City;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    [BurstCompile]
    public struct CalculateAveragePollutionJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<PropertyRenter> m_PropertyRenterType;

        [ReadOnly]
        public ComponentLookup<WaterConsumer> m_WaterConsumerFromEntity;

        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> m_TransformFromEntity;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_GroundPollutionMap;

        [ReadOnly]
        public Entity m_City;

        public CitizenHappinessParameterData m_HappinessParameters;

        public NativeArray<int> m_Results;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<PropertyRenter> nativeArray = chunk.GetNativeArray(ref this.m_PropertyRenterType);
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            int num4 = 0;
            int num5 = 0;
            int num6 = 0;
            int num7 = 0;
            float num8 = 0f;
            for (int i = 0; i < chunk.Count; i++)
            {
                Entity property = nativeArray[i].m_Property;
                int2 airPollutionBonuses = CitizenHappinessJob.GetAirPollutionBonuses(property, ref this.m_TransformFromEntity, this.m_AirPollutionMap, cityModifiers, in this.m_HappinessParameters);
                num3 += airPollutionBonuses.x + airPollutionBonuses.y;
                num6++;
                int2 groundPollutionBonuses = CitizenHappinessJob.GetGroundPollutionBonuses(property, ref this.m_TransformFromEntity, this.m_GroundPollutionMap, cityModifiers, in this.m_HappinessParameters);
                num += groundPollutionBonuses.x + groundPollutionBonuses.y;
                num4++;
                int2 noiseBonuses = CitizenHappinessJob.GetNoiseBonuses(property, ref this.m_TransformFromEntity, this.m_NoisePollutionMap, in this.m_HappinessParameters);
                num2 += noiseBonuses.x + noiseBonuses.y;
                num5++;
                if (this.m_WaterConsumerFromEntity.TryGetComponent(property, out var componentData))
                {
                    num7 += componentData.m_FulfilledFresh;
                    num8 += componentData.m_Pollution * (float)componentData.m_FulfilledFresh;
                }
            }
            this.m_Results[0] += num;
            this.m_Results[1] += num4;
            this.m_Results[2] += num3;
            this.m_Results[3] += num6;
            this.m_Results[4] += num2;
            this.m_Results[5] += num5;
            this.m_Results[6] += num7;
            this.m_Results[7] += Mathf.RoundToInt(num8);
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }
}

