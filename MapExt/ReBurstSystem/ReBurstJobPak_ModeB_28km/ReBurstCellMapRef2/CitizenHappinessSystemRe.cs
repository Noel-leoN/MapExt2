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
    // v1.3.6f变动
    [BurstCompile]
    public struct CitizenHappinessJob : IJobChunk
    {
        // 重定向私有结构体
        public struct FactorItem
        {
            public HappinessFactor m_Factor;

            public int4 m_Value;

            public uint m_UpdateFrame;
        }

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

        public LeisureParametersData m_LeisureParameters;

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
            int4 value26 = default(int4);
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
                Citizen citizen = nativeArray2[i];
                if ((CollectionUtils.TryGet(nativeArray8, i, out var value27) && CitizenUtils.IsDead(value27)) || ((this.m_Households[household].m_Flags & HouseholdFlags.MovedIn) == 0 && (citizen.m_State & CitizenFlags.Tourist) == 0))
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
                    if (citizen.GetAge() == CitizenAge.Child)
                    {
                        num3++;
                    }
                }
                int shoppedValueLastDay = (int)this.m_Households[household].m_ShoppedValueLastDay;
                int2 @int = ((shoppedValueLastDay > 0) ? new int2(0, math.min(15, shoppedValueLastDay / 50)) : default(int2));
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
                int2 int23 = default(int2);
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
                        int20 = CitizenHappinessSystem.GetWellfareBonuses(curvePosition, serviceCoverage, in this.m_CitizenHappinessParameters, citizen.Happiness);
                        value18.x += int20.x + int20.y;
                        value18.z += int20.x;
                        value18.w += int20.y;
                        value18.y++;
                    }

                    // redir
                    int7 = CitizenHappinessJob.GetGroundPollutionBonuses(property, ref this.m_Transforms, this.m_PollutionMap, cityModifiers, in this.m_CitizenHappinessParameters);
                    value8.x += int7.x + int7.y;
                    value8.z += int7.x;
                    value8.w += int7.y;
                    value8.y++;
                    
                    // redir
                    int8 = CitizenHappinessJob.GetAirPollutionBonuses(property, ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers, in this.m_CitizenHappinessParameters);
                    value3.x += int8.x + int8.y;
                    value3.z += int8.x;
                    value3.w += int8.y;
                    value3.y++;

                    // redir
                    int9 = CitizenHappinessJob.GetNoiseBonuses(property, ref this.m_Transforms, this.m_NoisePollutionMap, in this.m_CitizenHappinessParameters);
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
                    int23 = CitizenHappinessSystem.GetUnemploymentBonuses(ref citizen, in this.m_CitizenHappinessParameters);
                    value26.x += int23.x + int23.y;
                    value26.z += int23.x;
                    value26.w += int23.y;
                    value26.y++;
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
                bool flag = (citizen.m_State & CitizenFlags.Tourist) != 0;
                if (random.NextInt(0, 100) < (flag ? this.m_LeisureParameters.m_ChanceTouristDecreaseLeisureCounter : this.m_LeisureParameters.m_ChanceCitizenDecreaseLeisureCounter))
                {
                    citizen.m_LeisureCounter = (byte)math.min(255, math.max(0, citizen.m_LeisureCounter - this.m_LeisureParameters.m_AmountLeisureCounterDecrease));
                }
                citizen.m_PenaltyCounter = (byte)math.max(0, citizen.m_PenaltyCounter - 1);
                int2 leisureBonuses = CitizenHappinessSystem.GetLeisureBonuses(citizen.m_LeisureCounter);
                value19.x += leisureBonuses.x + leisureBonuses.y;
                value19.z += leisureBonuses.x;
                value19.w += leisureBonuses.y;
                value19.y++;
                if (!flag)
                {
                    int21 = CitizenHappinessSystem.GetTaxBonuses(citizen.GetEducationLevel(), this.m_TaxRates, cityModifiers, in this.m_CitizenHappinessParameters);
                }
                value20.x += int21.x + int21.y;
                value20.z += int21.x;
                value20.w += int21.y;
                value20.y++;
                int2 sicknessBonuses = CitizenHappinessSystem.GetSicknessBonuses(nativeArray8.Length != 0, ref citizen, in this.m_CitizenHappinessParameters);
                value7.x += sicknessBonuses.x + sicknessBonuses.y;
                value7.z += sicknessBonuses.x;
                value7.w += sicknessBonuses.y;
                value7.y++;
                int2 deathPenalty = CitizenHappinessSystem.GetDeathPenalty(householdCitizens, ref this.m_HealthProblems, in this.m_CitizenHappinessParameters);
                value24.x += deathPenalty.x + deathPenalty.y;
                value24.z += deathPenalty.x;
                value24.w += deathPenalty.y;
                value24.y++;
                int num5 = ((citizen.m_PenaltyCounter > 0) ? this.m_CitizenHappinessParameters.m_PenaltyEffect : 0);
                value23.x += num5;
                value23.w += num5;
                value23.y++;
                int num6 = math.max(0, 50 + num5 + deathPenalty.y + @int.y + int4.y + int5.y + int10.y + int11.y + int13.y + int6.y + leisureBonuses.y + int2.y + int12.y + int9.y + int14.y + int15.y + int16.y + int18.y + int17.y + int19.y + int3.y + int20.y + int21.y + int22.y + int23.y);
                int num7 = 50 + int6.x + sicknessBonuses.x + deathPenalty.x + int2.x + int7.x + int8.x + int4.x + int10.x + int13.x + int12.x + int14.x + int3.x + int20.x + int22.x + int23.x;
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
                int num8 = ((random.NextInt(100) > 50 + citizen.m_WellBeing - num6) ? 1 : (-1));
                citizen.m_WellBeing = (byte)math.max(0, math.min(100, citizen.m_WellBeing + num8));
                num8 = ((random.NextInt(100) > 50 + citizen.m_Health - num7) ? 1 : (-1));
                int maxHealth = CitizenHappinessSystem.GetMaxHealth(citizen.GetAgeInDays(this.m_SimulationFrame, this.m_TimeData) / (float)this.m_TimeSettings.m_DaysPerYear);
                citizen.m_Health = (byte)math.max(0, math.min(maxHealth, citizen.m_Health + num8));
                if (citizen.m_WellBeing < this.m_CitizenHappinessParameters.m_LowWellbeing)
                {
                    num++;
                }
                if (citizen.m_Health < this.m_CitizenHappinessParameters.m_LowHealth)
                {
                    num2++;
                }
                nativeArray2[i] = citizen;
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
            this.m_FactorQueue.Enqueue(new FactorItem
            {
                m_Factor = HappinessFactor.Unemployment,
                m_UpdateFrame = this.m_RawUpdateFrame,
                m_Value = value26
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

        // 重写3个静态方法
        public static int2 GetGroundPollutionBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<GroundPollution> pollutionMap, DynamicBuffer<CityModifier> cityModifiers, in CitizenHappinessParameterData data)
        {
            int2 result = default(int2);
            if (transforms.HasComponent(building))
            {
                short y = (short)(GroundPollutionSystemGetPollution(transforms[building].m_Position, pollutionMap).m_Pollution / data.m_PollutionBonusDivisor);
                float value = 1f;
                CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.PollutionHealthAffect);
                result.x = (int)((float)(-math.min(data.m_MaxAirAndGroundPollutionBonus, y)) * value);
            }
            return result;
        }

        public static int2 GetAirPollutionBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<AirPollution> airPollutionMap, DynamicBuffer<CityModifier> cityModifiers, in CitizenHappinessParameterData data)
        {
            int2 result = default(int2);
            if (transforms.HasComponent(building))
            {
                short y = (short)(AirPollutionSystemGetPollution(transforms[building].m_Position, airPollutionMap).m_Pollution / data.m_PollutionBonusDivisor);
                float value = 1f;
                CityUtils.ApplyModifier(ref value, cityModifiers, CityModifierType.PollutionHealthAffect);
                result.x = (int)((float)(-math.min(data.m_MaxAirAndGroundPollutionBonus, y)) * value);
            }
            return result;
        }

        public static int2 GetNoiseBonuses(Entity building, ref ComponentLookup<Game.Objects.Transform> transforms, NativeArray<NoisePollution> noiseMap, in CitizenHappinessParameterData data)
        {
            int2 result = default(int2);
            if (transforms.HasComponent(building))
            {
                short y = (short)(NoisePollutionSystemGetPollution(transforms[building].m_Position, noiseMap).m_Pollution / data.m_PollutionBonusDivisor);
                result.y = -math.min(data.m_MaxNoisePollutionBonus, y);
            }
            return result;
        }
    }
}
