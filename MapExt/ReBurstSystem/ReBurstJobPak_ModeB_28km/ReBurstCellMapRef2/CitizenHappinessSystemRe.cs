using Colossal.Collections;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static Game.Simulation.CitizenHappinessSystem;
using static MapExtPDX.MapExt.ReBurstSystemModeB.CellMapSystemRe;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{
    [BurstCompile]
    public struct CitizenHappinessJob : IJobChunk
    {
        [NativeDisableContainerSafetyRestriction]
        public NativeQueue<int>.ParallelWriter m_DebugQueue;

        public bool m_DebugOn;

        public ComponentTypeHandle<Citizen> m_CitizenType;

        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

        [ReadOnly]
        public ComponentTypeHandle<CrimeVictim> m_CrimeVictimType;

        [ReadOnly]
        public ComponentTypeHandle<Criminal> m_CriminalType;

        [ReadOnly]
        public ComponentTypeHandle<Game.Citizens.Student> m_StudentType;

        [ReadOnly]
        public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

        [ReadOnly]
        public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

        [ReadOnly]
        public BufferLookup<Game.Economy.Resources> m_Resources;

        [ReadOnly]
        public ComponentLookup<PropertyRenter> m_Properties;

        [ReadOnly]
        public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;

        [ReadOnly]
        public ComponentLookup<WaterConsumer> m_WaterConsumers;

        [ReadOnly]
        public ComponentLookup<Building> m_Buildings;

        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> m_Transforms;

        [ReadOnly]
        public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;

        [ReadOnly]
        public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverages;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<GarbageProducer> m_Garbages;

        [ReadOnly]
        public ComponentLookup<Household> m_Households;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

        [ReadOnly]
        public ComponentLookup<Locked> m_Locked;

        [ReadOnly]
        public ComponentLookup<CrimeProducer> m_CrimeProducers;

        [ReadOnly]
        public ComponentLookup<MailProducer> m_MailProducers;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;

        [ReadOnly]
        public BufferLookup<DistrictModifier> m_DistrictModifiers;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public BufferLookup<ServiceFee> m_ServiceFees;

        [ReadOnly]
        public ComponentLookup<HealthProblem> m_HealthProblems;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.Prison> m_Prisons;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.School> m_Schools;

        [ReadOnly]
        public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverage;

        [ReadOnly]
        public LocalEffectSystem.ReadData m_LocalEffectData;

        [ReadOnly]
        public NativeArray<int> m_TaxRates;

        public HealthcareParameterData m_HealthcareParameters;

        public ParkParameterData m_ParkParameters;

        public EducationParameterData m_EducationParameters;

        public TelecomParameterData m_TelecomParameters;

        public GarbageParameterData m_GarbageParameters;

        public PoliceConfigurationData m_PoliceParameters;

        public CitizenHappinessParameterData m_CitizenHappinessParameters;

        public TimeSettingsData m_TimeSettings;

        public ServiceFeeParameterData m_FeeParameters;

        public TimeData m_TimeData;

        public Entity m_City;

        [ReadOnly]
        public RandomSeed m_RandomSeed;

        public uint m_RawUpdateFrame;

        public NativeQueue<FactorItem>.ParallelWriter m_FactorQueue;

        public uint m_SimulationFrame;

        public NativeQueue<StatisticsEvent>.ParallelWriter m_StatisticsEventQueue;

        public struct FactorItem
        {
            public HappinessFactor m_Factor;

            public int4 m_Value;

            public uint m_UpdateFrame;
        }

        private void AddData(float value)
        {
            if (m_DebugOn)
            {
                m_DebugQueue.Enqueue(Mathf.RoundToInt(value));
            }
        }

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(m_EntityType);
            NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray(ref m_CitizenType);
            NativeArray<HouseholdMember> nativeArray3 = chunk.GetNativeArray(ref m_HouseholdMemberType);
            NativeArray<CrimeVictim> nativeArray4 = chunk.GetNativeArray(ref m_CrimeVictimType);
            NativeArray<Criminal> nativeArray5 = chunk.GetNativeArray(ref m_CriminalType);
            NativeArray<Game.Citizens.Student> nativeArray6 = chunk.GetNativeArray(ref m_StudentType);
            NativeArray<CurrentBuilding> nativeArray7 = chunk.GetNativeArray(ref m_CurrentBuildingType);
            NativeArray<HealthProblem> nativeArray8 = chunk.GetNativeArray(ref m_HealthProblemType);
            EnabledMask enabledMask = chunk.GetEnabledMask(ref m_CrimeVictimType);
            DynamicBuffer<CityModifier> cityModifiers = m_CityModifiers[m_City];
            DynamicBuffer<ServiceFee> fees = m_ServiceFees[m_City];
            float relativeFee = ServiceFeeSystem.GetFee(PlayerResource.Electricity, fees) / m_FeeParameters.m_ElectricityFee.m_Default;
            float relativeFee2 = ServiceFeeSystem.GetFee(PlayerResource.Water, fees) / m_FeeParameters.m_WaterFee.m_Default;
            int4 value = default;
            int4 value2 = default;
            int4 value3 = default;
            int4 value4 = default;
            int4 value5 = default;
            int4 value6 = default;
            int4 value7 = default;
            int4 value8 = default;
            int4 value9 = default;
            int4 value10 = default;
            int4 value11 = default;
            int4 value12 = default;
            int4 value13 = default;
            int4 value14 = default;
            int4 value15 = default;
            int4 value16 = default;
            int4 value17 = default;
            int4 value18 = default;
            int4 value19 = default;
            int4 value20 = default;
            int4 value21 = default;
            int4 value22 = default;
            int4 value23 = default;
            int4 value24 = default;
            int4 value25 = default;
            Unity.Mathematics.Random random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < chunk.Count; i++)
            {
                _ = nativeArray[i];
                Entity household = nativeArray3[i].m_Household;
                if (!m_Resources.HasBuffer(household))
                {
                    return;
                }
                Citizen value26 = nativeArray2[i];
                if (CollectionUtils.TryGet(nativeArray8, i, out var value27) && CitizenUtils.IsDead(value27) || (m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0 && (value26.m_State & CitizenFlags.Tourist) == 0)
                {
                    continue;
                }
                Entity entity = Entity.Null;
                Entity entity2 = Entity.Null;
                if (m_Properties.HasComponent(household))
                {
                    entity = m_Properties[household].m_Property;
                    if (m_CurrentDistrictData.HasComponent(entity))
                    {
                        entity2 = m_CurrentDistrictData[entity].m_District;
                    }
                }
                DynamicBuffer<HouseholdCitizen> householdCitizens = m_HouseholdCitizens[household];
                int num3 = 0;
                for (int j = 0; j < householdCitizens.Length; j++)
                {
                    if (value26.GetAge() == CitizenAge.Child)
                    {
                        num3++;
                    }
                }
                int householdTotalWealth = EconomyUtils.GetHouseholdTotalWealth(m_Households[household], m_Resources[household]);
                int2 @int = householdTotalWealth > 0 ? new int2(0, math.min(15, householdTotalWealth / 1000)) : default;
                value22.x += @int.x + @int.y;
                value22.y++;
                value22.z += @int.x;
                value22.w += @int.y;
                int2 int2 = 0;
                if (CollectionUtils.TryGet(nativeArray5, i, out var value28) && (value28.m_Flags & CriminalFlags.Prisoner) != 0 && CollectionUtils.TryGet(nativeArray7, i, out var value29) && m_Prisons.TryGetComponent(value29.m_CurrentBuilding, out var componentData))
                {
                    int2 += new int2(componentData.m_PrisonerHealth, componentData.m_PrisonerWellbeing);
                }
                if (CollectionUtils.TryGet(nativeArray6, i, out var value30) && m_Schools.TryGetComponent(value30.m_School, out var componentData2))
                {
                    int2 += new int2(componentData2.m_StudentHealth, componentData2.m_StudentWellbeing);
                }
                value21 += new int4(int2.x + int2.y, 1, int2.x, int2.y);
                int2 int3 = new int2(0, 0);
                int2 int4 = default;
                int2 int5 = default;
                int2 int6 = default;
                int2 int7 = default;
                int2 int8 = default;
                int2 int9 = default;
                int2 int10 = default;
                int2 int11 = default;
                int2 int12 = default;
                int2 int13 = default;
                int2 int14 = default;
                int2 int15 = default;
                int2 int16 = default;
                int2 int17 = default;
                int2 int18 = default;
                int2 int19 = default;
                int2 int20 = default;
                int2 int21 = default;
                int2 int22 = default;
                CrimeVictim crimeVictim = default;
                if (enabledMask[i])
                {
                    crimeVictim = nativeArray4[i];
                }
                if (m_Properties.TryGetComponent(household, out var componentData3) && m_Prefabs.TryGetComponent(componentData3.m_Property, out var componentData4))
                {
                    Entity prefab = componentData4.m_Prefab;
                    Entity property = componentData3.m_Property;
                    Entity healthcareServicePrefab = m_HealthcareParameters.m_HealthcareServicePrefab;
                    Entity parkServicePrefab = m_ParkParameters.m_ParkServicePrefab;
                    Entity educationServicePrefab = m_EducationParameters.m_EducationServicePrefab;
                    Entity telecomServicePrefab = m_TelecomParameters.m_TelecomServicePrefab;
                    Entity garbageServicePrefab = m_GarbageParameters.m_GarbageServicePrefab;
                    Entity policeServicePrefab = m_PoliceParameters.m_PoliceServicePrefab;
                    Entity entity3 = Entity.Null;
                    float curvePosition = 0f;
                    if (m_Buildings.HasComponent(property))
                    {
                        Building building = m_Buildings[property];
                        entity3 = building.m_RoadEdge;
                        curvePosition = building.m_CurvePosition;
                    }
                    int4 = GetElectricitySupplyBonuses(property, ref m_ElectricityConsumers, in m_CitizenHappinessParameters);
                    value5.x += int4.x + int4.y;
                    value5.z += int4.x;
                    value5.w += int4.y;
                    value5.y++;
                    int5 = GetElectricityFeeBonuses(property, ref m_ElectricityConsumers, relativeFee, in m_CitizenHappinessParameters);
                    value6.x += int5.x + int5.y;
                    value6.z += int5.x;
                    value6.w += int5.y;
                    value6.y++;
                    int10 = GetWaterSupplyBonuses(property, ref m_WaterConsumers, in m_CitizenHappinessParameters);
                    value10.x += int10.x + int10.y;
                    value10.z += int10.x;
                    value10.w += int10.y;
                    value10.y++;
                    int11 = GetWaterFeeBonuses(property, ref m_WaterConsumers, relativeFee2, in m_CitizenHappinessParameters);
                    value11.x += int11.x + int11.y;
                    value11.z += int11.x;
                    value11.w += int11.y;
                    value11.y++;
                    int12 = GetWaterPollutionBonuses(property, ref m_WaterConsumers, cityModifiers, in m_CitizenHappinessParameters);
                    value12.x += int12.x + int12.y;
                    value12.z += int12.x;
                    value12.w += int12.y;
                    value12.y++;
                    int13 = GetSewageBonuses(property, ref m_WaterConsumers, in m_CitizenHappinessParameters);
                    value13.x += int13.x + int13.y;
                    value13.z += int13.x;
                    value13.w += int13.y;
                    value13.y++;
                    if (m_ServiceCoverages.HasBuffer(entity3))
                    {
                        DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = m_ServiceCoverages[entity3];
                        int6 = GetHealthcareBonuses(curvePosition, serviceCoverage, ref m_Locked, healthcareServicePrefab, in m_CitizenHappinessParameters);
                        value7.x += int6.x + int6.y;
                        value7.z += int6.x;
                        value7.w += int6.y;
                        value7.y++;
                        int16 = GetEntertainmentBonuses(curvePosition, serviceCoverage, cityModifiers, ref m_Locked, parkServicePrefab, in m_CitizenHappinessParameters);
                        value15.x += int16.x + int16.y;
                        value15.z += int16.x;
                        value15.w += int16.y;
                        value15.y++;
                        int17 = GetEducationBonuses(curvePosition, serviceCoverage, ref m_Locked, educationServicePrefab, in m_CitizenHappinessParameters, num3);
                        value16.x += int17.x + int17.y;
                        value16.z += int17.x;
                        value16.w += int17.y;
                        value16.y++;
                        int20 = GetWellfareBonuses(curvePosition, serviceCoverage, in m_CitizenHappinessParameters, value26.Happiness);
                        value18.x += int20.x + int20.y;
                        value18.z += int20.x;
                        value18.w += int20.y;
                        value18.y++;
                    }
                    int7 = GetGroundPollutionBonuses(property, ref m_Transforms, m_PollutionMap, cityModifiers, in m_CitizenHappinessParameters);
                    value8.x += int7.x + int7.y;
                    value8.z += int7.x;
                    value8.w += int7.y;
                    value8.y++;
                    int8 = GetAirPollutionBonuses(property, ref m_Transforms, m_AirPollutionMap, cityModifiers, in m_CitizenHappinessParameters);
                    value3.x += int8.x + int8.y;
                    value3.z += int8.x;
                    value3.w += int8.y;
                    value3.y++;
                    int9 = GetNoiseBonuses(property, ref m_Transforms, m_NoisePollutionMap, in m_CitizenHappinessParameters);
                    value9.x += int9.x + int9.y;
                    value9.z += int9.x;
                    value9.w += int9.y;
                    value9.y++;
                    int14 = GetGarbageBonuses(property, ref m_Garbages, ref m_Locked, garbageServicePrefab, in m_GarbageParameters);
                    value14.x += int14.x + int14.y;
                    value14.z += int14.x;
                    value14.w += int14.y;
                    value14.y++;
                    int15 = GetCrimeBonuses(crimeVictim, property, ref m_CrimeProducers, ref m_Locked, policeServicePrefab, in m_CitizenHappinessParameters);
                    value.x += int15.x + int15.y;
                    value.z += int15.x;
                    value.w += int15.y;
                    value.y++;
                    int18 = GetMailBonuses(property, ref m_MailProducers, ref m_Locked, telecomServicePrefab, in m_CitizenHappinessParameters);
                    value17.x += int18.x + int18.y;
                    value17.z += int18.x;
                    value17.w += int18.y;
                    value17.y++;
                    int19 = GetTelecomBonuses(property, ref m_Transforms, m_TelecomCoverage, ref m_Locked, telecomServicePrefab, in m_CitizenHappinessParameters);
                    value2.x += int19.x + int19.y;
                    value2.z += int19.x;
                    value2.w += int19.y;
                    value2.y++;
                    value25.y++;
                    if (m_SpawnableBuildings.HasComponent(prefab) && m_BuildingDatas.HasComponent(prefab) && m_BuildingPropertyDatas.HasComponent(prefab) && !m_HomelessHouseholds.HasComponent(household))
                    {
                        SpawnableBuildingData spawnableBuildingData = m_SpawnableBuildings[prefab];
                        BuildingData buildingData = m_BuildingDatas[prefab];
                        BuildingPropertyData buildingPropertyData = m_BuildingPropertyDatas[prefab];
                        float num4 = buildingPropertyData.m_SpaceMultiplier * buildingData.m_LotSize.x * buildingData.m_LotSize.y / (householdCitizens.Length * buildingPropertyData.m_ResidentialProperties);
                        int3.y = Mathf.RoundToInt(GetApartmentWellbeing(num4, spawnableBuildingData.m_Level));
                        value4.x += int3.x + int3.y;
                        value4.z += int3.x;
                        value4.w += int3.y;
                        value4.y++;
                        AddData(math.min(100f, 100f * num4));
                    }
                    else
                    {
                        int3.y = Mathf.RoundToInt(GetApartmentWellbeing(0.01f, 1));
                        value4.x += int3.y;
                        value4.w += int3.y;
                        value4.y++;
                        int22 = GetHomelessBonuses(in m_CitizenHappinessParameters);
                        value25.x += int22.x + int22.y;
                        value25.z += int22.x;
                        value25.w += int22.y;
                    }
                }
                bool flag = (value26.m_State & CitizenFlags.Tourist) != 0;
                if (random.NextFloat() < 0.02f * (flag ? 10f : 1f))
                {
                    value26.m_LeisureCounter = (byte)math.min(255, math.max(0, value26.m_LeisureCounter - 1));
                }
                value26.m_PenaltyCounter = (byte)math.max(0, value26.m_PenaltyCounter - 1);
                int2 leisureBonuses = GetLeisureBonuses(value26.m_LeisureCounter);
                value19.x += leisureBonuses.x + leisureBonuses.y;
                value19.z += leisureBonuses.x;
                value19.w += leisureBonuses.y;
                value19.y++;
                if (!flag)
                {
                    int21 = GetTaxBonuses(value26.GetEducationLevel(), m_TaxRates, in m_CitizenHappinessParameters);
                }
                value20.x += int21.x + int21.y;
                value20.z += int21.x;
                value20.w += int21.y;
                value20.y++;
                int2 sicknessBonuses = GetSicknessBonuses(nativeArray8.Length != 0, in m_CitizenHappinessParameters);
                value7.x += sicknessBonuses.x + sicknessBonuses.y;
                value7.z += sicknessBonuses.x;
                value7.w += sicknessBonuses.y;
                value7.y++;
                int2 deathPenalty = GetDeathPenalty(householdCitizens, ref m_HealthProblems, in m_CitizenHappinessParameters);
                value24.x += deathPenalty.x + deathPenalty.y;
                value24.z += deathPenalty.x;
                value24.w += deathPenalty.y;
                value24.y++;
                int num5 = value26.m_PenaltyCounter > 0 ? m_CitizenHappinessParameters.m_PenaltyEffect : 0;
                value23.x += num5;
                value23.w += num5;
                value23.y++;
                int num6 = math.max(0, 50 + num5 + deathPenalty.y + @int.y + int4.y + int5.y + int10.y + int11.y + int13.y + int6.y + leisureBonuses.y + int2.y + int12.y + int9.y + int14.y + int15.y + int16.y + int18.y + int17.y + int19.y + int3.y + int20.y + int21.y + int22.y);
                int num7 = 50 + int6.x + sicknessBonuses.x + deathPenalty.x + int2.x + int7.x + int8.x + int4.x + int10.x + int13.x + int12.x + int14.x + int3.x + int20.x + int22.x;
                float value31 = num6;
                float value32 = num7;
                if (m_Transforms.HasComponent(entity))
                {
                    Game.Objects.Transform transform = m_Transforms[entity];
                    m_LocalEffectData.ApplyModifier(ref value31, transform.m_Position, LocalModifierType.Wellbeing);
                    m_LocalEffectData.ApplyModifier(ref value32, transform.m_Position, LocalModifierType.Health);
                }
                if (m_DistrictModifiers.HasBuffer(entity2))
                {
                    DynamicBuffer<DistrictModifier> modifiers = m_DistrictModifiers[entity2];
                    AreaUtils.ApplyModifier(ref value31, modifiers, DistrictModifierType.Wellbeing);
                }
                num6 = Mathf.RoundToInt(value31);
                num7 = Mathf.RoundToInt(value32);
                int num8 = random.NextInt(100) > 50 + value26.m_WellBeing - num6 ? 1 : -1;
                value26.m_WellBeing = (byte)math.max(0, math.min(100, value26.m_WellBeing + num8));
                num8 = random.NextInt(100) > 50 + value26.m_Health - num7 ? 1 : -1;
                int maxHealth = GetMaxHealth(value26.GetAgeInDays(m_SimulationFrame, m_TimeData) / m_TimeSettings.m_DaysPerYear);
                value26.m_Health = (byte)math.max(0, math.min(maxHealth, value26.m_Health + num8));
                if (value26.m_WellBeing < m_CitizenHappinessParameters.m_LowWellbeing)
                {
                    num++;
                }
                if (value26.m_Health < m_CitizenHappinessParameters.m_LowHealth)
                {
                    num2++;
                }
                nativeArray2[i] = value26;
            }
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Telecom,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value2
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Crime,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.AirPollution,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value3
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Apartment,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value4
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Electricity,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value5
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.ElectricityFee,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value6
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Healthcare,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value7
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.GroundPollution,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value8
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.NoisePollution,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value9
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Water,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value10
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.WaterFee,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value11
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.WaterPollution,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value12
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Sewage,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value13
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Garbage,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value14
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Entertainment,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value15
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Education,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value16
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Mail,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value17
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Welfare,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value18
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Leisure,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value19
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Tax,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value20
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Buildings,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value21
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Consumption,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value22
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.TrafficPenalty,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value23
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.DeathPenalty,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value24
            });
            m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Homelessness,
                m_UpdateFrame = m_RawUpdateFrame,
                m_Value = value25
            });
            m_StatisticsEventQueue.Enqueue(new StatisticsEvent
            {
                m_Statistic = StatisticType.WellbeingLevel,
                m_Change = num
            });
            m_StatisticsEventQueue.Enqueue(new StatisticsEvent
            {
                m_Statistic = StatisticType.HealthLevel,
                m_Change = num2
            });
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

        public static int2 GetGroundPollutionBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<GroundPollution> pollutionMap, DynamicBuffer<CityModifier> cityModifiers, in CitizenHappinessParameterData data)
        {
            int2 result = default;
            if (transforms.HasComponent(building))
            {
                short y = (short)(GroundPollutionSystemGetPollution(transforms[building].m_Position, pollutionMap).m_Pollution / data.m_PollutionBonusDivisor);
                float value = 1f;
                CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.PollutionHealthAffect);
                result.x = (int)(-math.min(data.m_MaxAirAndGroundPollutionBonus, y) * value);
            }

            return result;
        }

        public static int2 GetAirPollutionBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<AirPollution> airPollutionMap, DynamicBuffer<CityModifier> cityModifiers, in CitizenHappinessParameterData data)
        {
            int2 result = default;
            if (transforms.HasComponent(building))
            {
                short y = (short)(AirPollutionSystemGetPollution(transforms[building].m_Position, airPollutionMap).m_Pollution / data.m_PollutionBonusDivisor);
                float value = 1f;
                CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.PollutionHealthAffect);
                result.x = (int)(-math.min(data.m_MaxAirAndGroundPollutionBonus, y) * value);
            }

            return result;
        }

        public static int2 GetNoiseBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<NoisePollution> noiseMap, in CitizenHappinessParameterData data)
        {
            int2 result = default;
            if (transforms.HasComponent(building))
            {
                short y = (short)(NoisePollutionSystemGetPollution(transforms[building].m_Position, noiseMap).m_Pollution / data.m_PollutionBonusDivisor);
                result.y = -math.min(data.m_MaxNoisePollutionBonus, y);
            }

            return result;
        }
    }
}
