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

/*
		public enum HappinessFactor
		{
			Telecom = 0,
			Crime = 1,
			AirPollution = 2,
			Apartment = 3,
			Electricity = 4,
			Healthcare = 5,
			GroundPollution = 6,
			NoisePollution = 7,
			Water = 8,
			WaterPollution = 9,
			Sewage = 10,
			Garbage = 11,
			Entertainment = 12,
			Education = 13,
			Mail = 14,
			Welfare = 15,
			Leisure = 16,
			Tax = 17,
			Buildings = 18,
			Consumption = 19,
			TrafficPenalty = 20,
			DeathPenalty = 21,
			Homelessness = 22,
			ElectricityFee = 23,
			WaterFee = 24,
			Count = 25
		}

		
*/

namespace MapExtPDX
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
            if (this.m_DebugOn)
            {
                this.m_DebugQueue.Enqueue(Mathf.RoundToInt(value));
            }
        }

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray(ref this.m_CitizenType);
            NativeArray<HouseholdMember> nativeArray3 = chunk.GetNativeArray(ref this.m_HouseholdMemberType);
            NativeArray<CrimeVictim> nativeArray4 = chunk.GetNativeArray(ref this.m_CrimeVictimType);
            NativeArray<Criminal> nativeArray5 = chunk.GetNativeArray(ref this.m_CriminalType);
            NativeArray<Game.Citizens.Student> nativeArray6 = chunk.GetNativeArray(ref this.m_StudentType);
            NativeArray<CurrentBuilding> nativeArray7 = chunk.GetNativeArray(ref this.m_CurrentBuildingType);
            NativeArray<HealthProblem> nativeArray8 = chunk.GetNativeArray(ref this.m_HealthProblemType);
            EnabledMask enabledMask = chunk.GetEnabledMask(ref this.m_CrimeVictimType);
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
            DynamicBuffer<ServiceFee> fees = this.m_ServiceFees[this.m_City];
            float relativeFee = ServiceFeeSystem.GetFee(PlayerResource.Electricity, fees) / this.m_FeeParameters.m_ElectricityFee.m_Default;
            float relativeFee2 = ServiceFeeSystem.GetFee(PlayerResource.Water, fees) / this.m_FeeParameters.m_WaterFee.m_Default;
            int4 value = default(int4);
            int4 value2 = default(int4);
            int4 value3 = default(int4);
            int4 value4 = default(int4);
            int4 value5 = default(int4);
            int4 value6 = default(int4);
            int4 value7 = default(int4);
            int4 value8 = default(int4);
            int4 value9 = default(int4);
            int4 value10 = default(int4);
            int4 value11 = default(int4);
            int4 value12 = default(int4);
            int4 value13 = default(int4);
            int4 value14 = default(int4);
            int4 value15 = default(int4);
            int4 value16 = default(int4);
            int4 value17 = default(int4);
            int4 value18 = default(int4);
            int4 value19 = default(int4);
            int4 value20 = default(int4);
            int4 value21 = default(int4);
            int4 value22 = default(int4);
            int4 value23 = default(int4);
            int4 value24 = default(int4);
            int4 value25 = default(int4);
            Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < chunk.Count; i++)
            {
                _ = nativeArray[i];
                Entity household = nativeArray3[i].m_Household;
                if (!this.m_Resources.HasBuffer(household))
                {
                    return;
                }
                Citizen value26 = nativeArray2[i];
                if ((CollectionUtils.TryGet(nativeArray8, i, out var value27) && CitizenUtils.IsDead(value27)) || ((this.m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0 && (value26.m_State & CitizenFlags.Tourist) == 0))
                {
                    continue;
                }
                Entity entity = Entity.Null;
                Entity entity2 = Entity.Null;
                if (this.m_Properties.HasComponent(household))
                {
                    entity = this.m_Properties[household].m_Property;
                    if (this.m_CurrentDistrictData.HasComponent(entity))
                    {
                        entity2 = this.m_CurrentDistrictData[entity].m_District;
                    }
                }
                DynamicBuffer<HouseholdCitizen> householdCitizens = this.m_HouseholdCitizens[household];
                int num3 = 0;
                for (int j = 0; j < householdCitizens.Length; j++)
                {
                    if (value26.GetAge() == CitizenAge.Child)
                    {
                        num3++;
                    }
                }
                int householdTotalWealth = EconomyUtils.GetHouseholdTotalWealth(this.m_Households[household], this.m_Resources[household]);
                int2 @int = ((householdTotalWealth > 0) ? new int2(0, math.min(15, householdTotalWealth / 1000)) : default(int2));
                value22.x += @int.x + @int.y;
                value22.y++;
                value22.z += @int.x;
                value22.w += @int.y;
                int2 int2 = 0;
                if (CollectionUtils.TryGet(nativeArray5, i, out var value28) && (value28.m_Flags & CriminalFlags.Prisoner) != 0 && CollectionUtils.TryGet(nativeArray7, i, out var value29) && this.m_Prisons.TryGetComponent(value29.m_CurrentBuilding, out var componentData))
                {
                    int2 += new int2(componentData.m_PrisonerHealth, componentData.m_PrisonerWellbeing);
                }
                if (CollectionUtils.TryGet(nativeArray6, i, out var value30) && this.m_Schools.TryGetComponent(value30.m_School, out var componentData2))
                {
                    int2 += new int2(componentData2.m_StudentHealth, componentData2.m_StudentWellbeing);
                }
                value21 += new int4(int2.x + int2.y, 1, int2.x, int2.y);
                int2 int3 = new int2(0, 0);
                int2 int4 = default(int2);
                int2 int5 = default(int2);
                int2 int6 = default(int2);
                int2 int7 = default(int2);
                int2 int8 = default(int2);
                int2 int9 = default(int2);
                int2 int10 = default(int2);
                int2 int11 = default(int2);
                int2 int12 = default(int2);
                int2 int13 = default(int2);
                int2 int14 = default(int2);
                int2 int15 = default(int2);
                int2 int16 = default(int2);
                int2 int17 = default(int2);
                int2 int18 = default(int2);
                int2 int19 = default(int2);
                int2 int20 = default(int2);
                int2 int21 = default(int2);
                int2 int22 = default(int2);
                CrimeVictim crimeVictim = default(CrimeVictim);
                if (enabledMask[i])
                {
                    crimeVictim = nativeArray4[i];
                }
                if (this.m_Properties.TryGetComponent(household, out var componentData3) && this.m_Prefabs.TryGetComponent(componentData3.m_Property, out var componentData4))
                {
                    Entity prefab = componentData4.m_Prefab;
                    Entity property = componentData3.m_Property;
                    Entity healthcareServicePrefab = this.m_HealthcareParameters.m_HealthcareServicePrefab;
                    Entity parkServicePrefab = this.m_ParkParameters.m_ParkServicePrefab;
                    Entity educationServicePrefab = this.m_EducationParameters.m_EducationServicePrefab;
                    Entity telecomServicePrefab = this.m_TelecomParameters.m_TelecomServicePrefab;
                    Entity garbageServicePrefab = this.m_GarbageParameters.m_GarbageServicePrefab;
                    Entity policeServicePrefab = this.m_PoliceParameters.m_PoliceServicePrefab;
                    Entity entity3 = Entity.Null;
                    float curvePosition = 0f;
                    if (this.m_Buildings.HasComponent(property))
                    {
                        Building building = this.m_Buildings[property];
                        entity3 = building.m_RoadEdge;
                        curvePosition = building.m_CurvePosition;
                    }
                    int4 = CitizenHappinessSystem.GetElectricitySupplyBonuses(property, ref this.m_ElectricityConsumers, in this.m_CitizenHappinessParameters);
                    value5.x += int4.x + int4.y;
                    value5.z += int4.x;
                    value5.w += int4.y;
                    value5.y++;
                    int5 = CitizenHappinessSystem.GetElectricityFeeBonuses(property, ref this.m_ElectricityConsumers, relativeFee, in this.m_CitizenHappinessParameters);
                    value6.x += int5.x + int5.y;
                    value6.z += int5.x;
                    value6.w += int5.y;
                    value6.y++;
                    int10 = CitizenHappinessSystem.GetWaterSupplyBonuses(property, ref this.m_WaterConsumers, in this.m_CitizenHappinessParameters);
                    value10.x += int10.x + int10.y;
                    value10.z += int10.x;
                    value10.w += int10.y;
                    value10.y++;
                    int11 = CitizenHappinessSystem.GetWaterFeeBonuses(property, ref this.m_WaterConsumers, relativeFee2, in this.m_CitizenHappinessParameters);
                    value11.x += int11.x + int11.y;
                    value11.z += int11.x;
                    value11.w += int11.y;
                    value11.y++;
                    int12 = CitizenHappinessSystem.GetWaterPollutionBonuses(property, ref this.m_WaterConsumers, cityModifiers, in this.m_CitizenHappinessParameters);
                    value12.x += int12.x + int12.y;
                    value12.z += int12.x;
                    value12.w += int12.y;
                    value12.y++;
                    int13 = CitizenHappinessSystem.GetSewageBonuses(property, ref this.m_WaterConsumers, in this.m_CitizenHappinessParameters);
                    value13.x += int13.x + int13.y;
                    value13.z += int13.x;
                    value13.w += int13.y;
                    value13.y++;
                    if (this.m_ServiceCoverages.HasBuffer(entity3))
                    {
                        DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = this.m_ServiceCoverages[entity3];
                        int6 = CitizenHappinessSystem.GetHealthcareBonuses(curvePosition, serviceCoverage, ref this.m_Locked, healthcareServicePrefab, in this.m_CitizenHappinessParameters);
                        value7.x += int6.x + int6.y;
                        value7.z += int6.x;
                        value7.w += int6.y;
                        value7.y++;
                        int16 = CitizenHappinessSystem.GetEntertainmentBonuses(curvePosition, serviceCoverage, cityModifiers, ref this.m_Locked, parkServicePrefab, in this.m_CitizenHappinessParameters);
                        value15.x += int16.x + int16.y;
                        value15.z += int16.x;
                        value15.w += int16.y;
                        value15.y++;
                        int17 = CitizenHappinessSystem.GetEducationBonuses(curvePosition, serviceCoverage, ref this.m_Locked, educationServicePrefab, in this.m_CitizenHappinessParameters, num3);
                        value16.x += int17.x + int17.y;
                        value16.z += int17.x;
                        value16.w += int17.y;
                        value16.y++;
                        int20 = CitizenHappinessSystem.GetWellfareBonuses(curvePosition, serviceCoverage, in this.m_CitizenHappinessParameters, value26.Happiness);
                        value18.x += int20.x + int20.y;
                        value18.z += int20.x;
                        value18.w += int20.y;
                        value18.y++;
                    }
                    int7 = CitizenHappinessSystem.GetGroundPollutionBonuses(property, ref this.m_Transforms, this.m_PollutionMap, cityModifiers, in this.m_CitizenHappinessParameters);
                    value8.x += int7.x + int7.y;
                    value8.z += int7.x;
                    value8.w += int7.y;
                    value8.y++;
                    int8 = CitizenHappinessSystem.GetAirPollutionBonuses(property, ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers, in this.m_CitizenHappinessParameters);
                    value3.x += int8.x + int8.y;
                    value3.z += int8.x;
                    value3.w += int8.y;
                    value3.y++;
                    int9 = CitizenHappinessSystem.GetNoiseBonuses(property, ref this.m_Transforms, this.m_NoisePollutionMap, in this.m_CitizenHappinessParameters);
                    value9.x += int9.x + int9.y;
                    value9.z += int9.x;
                    value9.w += int9.y;
                    value9.y++;
                    int14 = CitizenHappinessSystem.GetGarbageBonuses(property, ref this.m_Garbages, ref this.m_Locked, garbageServicePrefab, in this.m_GarbageParameters);
                    value14.x += int14.x + int14.y;
                    value14.z += int14.x;
                    value14.w += int14.y;
                    value14.y++;
                    int15 = CitizenHappinessSystem.GetCrimeBonuses(crimeVictim, property, ref this.m_CrimeProducers, ref this.m_Locked, policeServicePrefab, in this.m_CitizenHappinessParameters);
                    value.x += int15.x + int15.y;
                    value.z += int15.x;
                    value.w += int15.y;
                    value.y++;
                    int18 = CitizenHappinessSystem.GetMailBonuses(property, ref this.m_MailProducers, ref this.m_Locked, telecomServicePrefab, in this.m_CitizenHappinessParameters);
                    value17.x += int18.x + int18.y;
                    value17.z += int18.x;
                    value17.w += int18.y;
                    value17.y++;
                    int19 = CitizenHappinessSystem.GetTelecomBonuses(property, ref this.m_Transforms, this.m_TelecomCoverage, ref this.m_Locked, telecomServicePrefab, in this.m_CitizenHappinessParameters);
                    value2.x += int19.x + int19.y;
                    value2.z += int19.x;
                    value2.w += int19.y;
                    value2.y++;
                    value25.y++;
                    if (this.m_SpawnableBuildings.HasComponent(prefab) && this.m_BuildingDatas.HasComponent(prefab) && this.m_BuildingPropertyDatas.HasComponent(prefab) && !this.m_HomelessHouseholds.HasComponent(household))
                    {
                        SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildings[prefab];
                        BuildingData buildingData = this.m_BuildingDatas[prefab];
                        BuildingPropertyData buildingPropertyData = this.m_BuildingPropertyDatas[prefab];
                        float num4 = buildingPropertyData.m_SpaceMultiplier * (float)buildingData.m_LotSize.x * (float)buildingData.m_LotSize.y / (float)(householdCitizens.Length * buildingPropertyData.m_ResidentialProperties);
                        int3.y = Mathf.RoundToInt(CitizenHappinessSystem.GetApartmentWellbeing(num4, spawnableBuildingData.m_Level));
                        value4.x += int3.x + int3.y;
                        value4.z += int3.x;
                        value4.w += int3.y;
                        value4.y++;
                        this.AddData(math.min(100f, 100f * num4));
                    }
                    else
                    {
                        int3.y = Mathf.RoundToInt(CitizenHappinessSystem.GetApartmentWellbeing(0.01f, 1));
                        value4.x += int3.y;
                        value4.w += int3.y;
                        value4.y++;
                        int22 = CitizenHappinessSystem.GetHomelessBonuses(in this.m_CitizenHappinessParameters);
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
                int2 leisureBonuses = CitizenHappinessSystem.GetLeisureBonuses(value26.m_LeisureCounter);
                value19.x += leisureBonuses.x + leisureBonuses.y;
                value19.z += leisureBonuses.x;
                value19.w += leisureBonuses.y;
                value19.y++;
                if (!flag)
                {
                    int21 = CitizenHappinessSystem.GetTaxBonuses(value26.GetEducationLevel(), this.m_TaxRates, in this.m_CitizenHappinessParameters);
                }
                value20.x += int21.x + int21.y;
                value20.z += int21.x;
                value20.w += int21.y;
                value20.y++;
                int2 sicknessBonuses = CitizenHappinessSystem.GetSicknessBonuses(nativeArray8.Length != 0, in this.m_CitizenHappinessParameters);
                value7.x += sicknessBonuses.x + sicknessBonuses.y;
                value7.z += sicknessBonuses.x;
                value7.w += sicknessBonuses.y;
                value7.y++;
                int2 deathPenalty = CitizenHappinessSystem.GetDeathPenalty(householdCitizens, ref this.m_HealthProblems, in this.m_CitizenHappinessParameters);
                value24.x += deathPenalty.x + deathPenalty.y;
                value24.z += deathPenalty.x;
                value24.w += deathPenalty.y;
                value24.y++;
                int num5 = ((value26.m_PenaltyCounter > 0) ? this.m_CitizenHappinessParameters.m_PenaltyEffect : 0);
                value23.x += num5;
                value23.w += num5;
                value23.y++;
                int num6 = math.max(0, 50 + num5 + deathPenalty.y + @int.y + int4.y + int5.y + int10.y + int11.y + int13.y + int6.y + leisureBonuses.y + int2.y + int12.y + int9.y + int14.y + int15.y + int16.y + int18.y + int17.y + int19.y + int3.y + int20.y + int21.y + int22.y);
                int num7 = 50 + int6.x + sicknessBonuses.x + deathPenalty.x + int2.x + int7.x + int8.x + int4.x + int10.x + int13.x + int12.x + int14.x + int3.x + int20.x + int22.x;
                float value31 = num6;
                float value32 = num7;
                if (this.m_Transforms.HasComponent(entity))
                {
                    Game.Objects.Transform transform = this.m_Transforms[entity];
                    this.m_LocalEffectData.ApplyModifier(ref value31, transform.m_Position, LocalModifierType.Wellbeing);
                    this.m_LocalEffectData.ApplyModifier(ref value32, transform.m_Position, LocalModifierType.Health);
                }
                if (this.m_DistrictModifiers.HasBuffer(entity2))
                {
                    DynamicBuffer<DistrictModifier> modifiers = this.m_DistrictModifiers[entity2];
                    AreaUtils.ApplyModifier(ref value31, modifiers, DistrictModifierType.Wellbeing);
                }
                num6 = Mathf.RoundToInt(value31);
                num7 = Mathf.RoundToInt(value32);
                int num8 = ((random.NextInt(100) > 50 + value26.m_WellBeing - num6) ? 1 : (-1));
                value26.m_WellBeing = (byte)math.max(0, math.min(100, value26.m_WellBeing + num8));
                num8 = ((random.NextInt(100) > 50 + value26.m_Health - num7) ? 1 : (-1));
                int maxHealth = CitizenHappinessSystem.GetMaxHealth(value26.GetAgeInDays(this.m_SimulationFrame, this.m_TimeData) / (float)this.m_TimeSettings.m_DaysPerYear);
                value26.m_Health = (byte)math.max(0, math.min(maxHealth, value26.m_Health + num8));
                if (value26.m_WellBeing < this.m_CitizenHappinessParameters.m_LowWellbeing)
                {
                    num++;
                }
                if (value26.m_Health < this.m_CitizenHappinessParameters.m_LowHealth)
                {
                    num2++;
                }
                nativeArray2[i] = value26;
            }
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Telecom,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value2
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Crime,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.AirPollution,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value3
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Apartment,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value4
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Electricity,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value5
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.ElectricityFee,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value6
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Healthcare,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value7
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.GroundPollution,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value8
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.NoisePollution,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value9
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Water,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value10
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.WaterFee,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value11
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.WaterPollution,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value12
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Sewage,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value13
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Garbage,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value14
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Entertainment,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value15
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Education,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value16
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Mail,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value17
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Welfare,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value18
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Leisure,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value19
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Tax,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value20
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Buildings,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value21
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Consumption,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value22
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.TrafficPenalty,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value23
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.DeathPenalty,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value24
            });
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Homelessness,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value25
            });
            this.m_StatisticsEventQueue.Enqueue(new StatisticsEvent
            {
                m_Statistic = StatisticType.WellbeingLevel,
                m_Change = num
            });
            this.m_StatisticsEventQueue.Enqueue(new StatisticsEvent
            {
                m_Statistic = StatisticType.HealthLevel,
                m_Change = num2
            });
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }
}


/*
[BurstCompile]
    public struct HappinessFactorJob : IJob
    {
        public NativeArray<int4> m_HappinessFactors;

        public NativeQueue<FactorItem> m_FactorQueue;

        public NativeQueue<TriggerAction> m_TriggerActionQueue;

        public uint m_RawUpdateFrame;

        public Entity m_ParameterEntity;

        [ReadOnly]
        public BufferLookup<HappinessFactorParameterData> m_Parameters;

        [ReadOnly]
        public ComponentLookup<Locked> m_Locked;

        public void Execute()
        {
            for (int i = 0; i < 25; i++)
            {
                this.m_HappinessFactors[CitizenHappinessSystem.GetFactorIndex((HappinessFactor)i, this.m_RawUpdateFrame)] = default(int4);
            }
            FactorItem item;
            while (this.m_FactorQueue.TryDequeue(out item))
            {
                if (item.m_UpdateFrame != this.m_RawUpdateFrame)
                {
                    UnityEngine.Debug.LogWarning("Different updateframe in HappinessFactorJob than in its queue");
                }
                this.m_HappinessFactors[CitizenHappinessSystem.GetFactorIndex(item.m_Factor, item.m_UpdateFrame)] += item.m_Value;
            }
            DynamicBuffer<HappinessFactorParameterData> parameters = this.m_Parameters[this.m_ParameterEntity];
            for (int j = 0; j < 25; j++)
            {
                this.m_TriggerActionQueue.Enqueue(new TriggerAction(CitizenHappinessSystem.GetTriggerTypeForHappinessFactor((HappinessFactor)j), Entity.Null, CitizenHappinessSystem.GetHappinessFactor((HappinessFactor)j, this.m_HappinessFactors, parameters, ref this.m_Locked).x));
            }
        }
    }
*/




/*
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			this.m_CitizenQuery.ResetFilter();
			this.m_CitizenQuery.AddSharedComponentFilter(new UpdateFrame(updateFrameWithInterval));
			NativeQueue<int>.ParallelWriter debugQueue = default(NativeQueue<int>.ParallelWriter);
			if (this.m_DebugData.IsEnabled)
			{
				debugQueue = this.m_DebugData.GetQueue(clear: false, out var _).AsParallelWriter();
			}
			this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_School_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_Prison_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_City_ServiceFee_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Areas_DistrictModifier_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_MailProducer_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_CrimeProducer_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_Locked_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_GarbageProducer_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_WaterConsumer_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Areas_CurrentDistrict_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Net_ServiceCoverage_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_ElectricityConsumer_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_Student_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_Criminal_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_CrimeVictim_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle.Update(ref base.CheckedStateRef);
			CitizenHappinessJob jobData = default(CitizenHappinessJob);
			jobData.m_DebugQueue = debugQueue;
			jobData.m_DebugOn = this.m_DebugData.IsEnabled;
			jobData.m_CitizenType = this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle;
			jobData.m_EntityType = this.__TypeHandle.__Unity_Entities_Entity_TypeHandle;
			jobData.m_HouseholdMemberType = this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;
			jobData.m_CrimeVictimType = this.__TypeHandle.__Game_Citizens_CrimeVictim_RO_ComponentTypeHandle;
			jobData.m_CriminalType = this.__TypeHandle.__Game_Citizens_Criminal_RO_ComponentTypeHandle;
			jobData.m_StudentType = this.__TypeHandle.__Game_Citizens_Student_RO_ComponentTypeHandle;
			jobData.m_CurrentBuildingType = this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;
			jobData.m_HealthProblemType = this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle;
			jobData.m_Households = this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup;
			jobData.m_HealthProblems = this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentLookup;
			jobData.m_Buildings = this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup;
			jobData.m_ElectricityConsumers = this.__TypeHandle.__Game_Buildings_ElectricityConsumer_RO_ComponentLookup;
			jobData.m_Properties = this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup;
			jobData.m_Resources = this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup;
			jobData.m_ServiceCoverages = this.__TypeHandle.__Game_Net_ServiceCoverage_RO_BufferLookup;
			jobData.m_Transforms = this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup;
			jobData.m_CurrentDistrictData = this.__TypeHandle.__Game_Areas_CurrentDistrict_RO_ComponentLookup;
			jobData.m_Prefabs = this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
			jobData.m_WaterConsumers = this.__TypeHandle.__Game_Buildings_WaterConsumer_RO_ComponentLookup;
			jobData.m_Garbages = this.__TypeHandle.__Game_Buildings_GarbageProducer_RO_ComponentLookup;
			jobData.m_HouseholdCitizens = this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup;
			jobData.m_Locked = this.__TypeHandle.__Game_Prefabs_Locked_RO_ComponentLookup;
			jobData.m_CrimeProducers = this.__TypeHandle.__Game_Buildings_CrimeProducer_RO_ComponentLookup;
			jobData.m_MailProducers = this.__TypeHandle.__Game_Buildings_MailProducer_RO_ComponentLookup;
			jobData.m_BuildingDatas = this.__TypeHandle.__Game_Prefabs_BuildingData_RO_ComponentLookup;
			jobData.m_BuildingPropertyDatas = this.__TypeHandle.__Game_Prefabs_BuildingPropertyData_RO_ComponentLookup;
			jobData.m_SpawnableBuildings = this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;
			jobData.m_DistrictModifiers = this.__TypeHandle.__Game_Areas_DistrictModifier_RO_BufferLookup;
			jobData.m_CityModifiers = this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup;
			jobData.m_ServiceFees = this.__TypeHandle.__Game_City_ServiceFee_RO_BufferLookup;
			jobData.m_Prisons = this.__TypeHandle.__Game_Buildings_Prison_RO_ComponentLookup;
			jobData.m_Schools = this.__TypeHandle.__Game_Buildings_School_RO_ComponentLookup;
			jobData.m_HomelessHouseholds = this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup;
			jobData.m_PollutionMap = this.m_GroundPollutionSystem.GetMap(readOnly: true, out var dependencies);
			jobData.m_AirPollutionMap = this.m_AirPollutionSystem.GetMap(readOnly: true, out var dependencies2);
			jobData.m_NoisePollutionMap = this.m_NoisePollutionSystem.GetMap(readOnly: true, out var dependencies3);
			jobData.m_TelecomCoverage = this.m_TelecomCoverageSystem.GetData(readOnly: true, out var dependencies4);
			jobData.m_LocalEffectData = this.m_LocalEffectSystem.GetReadData(out var dependencies5);
			jobData.m_HealthcareParameters = this.m_HealthcareParameterQuery.GetSingleton<HealthcareParameterData>();
			jobData.m_ParkParameters = this.m_ParkParameterQuery.GetSingleton<ParkParameterData>();
			jobData.m_EducationParameters = this.m_EducationParameterQuery.GetSingleton<EducationParameterData>();
			jobData.m_TelecomParameters = this.m_TelecomParameterQuery.GetSingleton<TelecomParameterData>();
			jobData.m_GarbageParameters = this.m_GarbageParameterQuery.GetSingleton<GarbageParameterData>();
			jobData.m_PoliceParameters = this.m_PoliceParameterQuery.GetSingleton<PoliceConfigurationData>();
			jobData.m_CitizenHappinessParameters = this.m_CitizenHappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>();
			jobData.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			jobData.m_TimeSettings = this.m_TimeSettingQuery.GetSingleton<TimeSettingsData>();
			jobData.m_FeeParameters = this.__query_429327288_0.GetSingleton<ServiceFeeParameterData>();
			jobData.m_TimeData = this.m_TimeDataQuery.GetSingleton<TimeData>();
			jobData.m_TaxRates = this.m_TaxSystem.GetTaxRates();
			jobData.m_RawUpdateFrame = updateFrameWithInterval;
			jobData.m_City = this.m_CitySystem.City;
			jobData.m_RandomSeed = RandomSeed.Next();
			jobData.m_FactorQueue = this.m_FactorQueue.AsParallelWriter();
			jobData.m_StatisticsEventQueue = this.m_CityStatisticsSystem.GetStatisticsEventQueue(out var deps2).AsParallelWriter();
			JobHandle jobHandle = JobChunkExtensions.ScheduleParallel(jobData, this.m_CitizenQuery, JobHandle.CombineDependencies(dependencies5, dependencies4, JobHandle.CombineDependencies(dependencies2, dependencies3, JobHandle.CombineDependencies(base.Dependency, dependencies, deps2))));
			if (this.m_DebugData.IsEnabled)
			{
				this.m_DebugData.AddWriter(jobHandle);
			}
			this.m_GroundPollutionSystem.AddReader(jobHandle);
			this.m_AirPollutionSystem.AddReader(jobHandle);
			this.m_NoisePollutionSystem.AddReader(jobHandle);
			this.m_TelecomCoverageSystem.AddReader(jobHandle);
			this.m_LocalEffectSystem.AddLocalEffectReader(jobHandle);
			this.m_TaxSystem.AddReader(jobHandle);
			this.m_CityStatisticsSystem.AddWriter(jobHandle);
			this.__TypeHandle.__Game_Prefabs_Locked_RO_ComponentLookup.Update(ref base.CheckedStateRef);
			this.__TypeHandle.__Game_Prefabs_HappinessFactorParameterData_RO_BufferLookup.Update(ref base.CheckedStateRef);
			HappinessFactorJob happinessFactorJob = default(HappinessFactorJob);
			happinessFactorJob.m_FactorQueue = this.m_FactorQueue;
			happinessFactorJob.m_HappinessFactors = this.m_HappinessFactors;
			happinessFactorJob.m_RawUpdateFrame = updateFrameWithInterval;
			happinessFactorJob.m_TriggerActionQueue = this.m_TriggerSystem.CreateActionBuffer();
			happinessFactorJob.m_ParameterEntity = this.m_HappinessFactorParameterQuery.GetSingletonEntity();
			happinessFactorJob.m_Parameters = this.__TypeHandle.__Game_Prefabs_HappinessFactorParameterData_RO_BufferLookup;
			happinessFactorJob.m_Locked = this.__TypeHandle.__Game_Prefabs_Locked_RO_ComponentLookup;
			HappinessFactorJob jobData2 = happinessFactorJob;
			base.Dependency = IJobExtensions.Schedule(jobData2, jobHandle);
			this.m_LastDeps = base.Dependency;
			this.m_TriggerSystem.AddActionBufferWriter(base.Dependency);
		}
*/
