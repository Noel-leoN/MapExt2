// Game.UI.InGame.AverageHappinessSection
// OnUpdate

using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.UI.InGame;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    // v1.3.6f变动
    // 关联CitizenHappinessSystemRe/BuildingHappinessRe
    [BurstCompile]
    public struct CountHappinessJob : IJob
    {
        [ReadOnly]
        public Entity m_SelectedEntity;

        [ReadOnly]
        public ComponentLookup<Building> m_BuildingFromEntity;

        [ReadOnly]
        public ComponentLookup<ResidentialProperty> m_ResidentialPropertyFromEntity;

        [ReadOnly]
        public ComponentLookup<Household> m_HouseholdFromEntity;

        [ReadOnly]
        public ComponentLookup<Citizen> m_CitizenFromEntity;

        [ReadOnly]
        public ComponentLookup<HealthProblem> m_HealthProblemFromEntity;

        [ReadOnly]
        public ComponentLookup<PropertyRenter> m_PropertyRenterFromEntity;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_HouseholdCitizenFromEntity;

        [ReadOnly]
        public BufferLookup<Renter> m_RenterFromEntity;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefFromEntity;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingDataFromEntity;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDataFromEntity;

        [ReadOnly]
        public ComponentLookup<ElectricityConsumer> m_ElectricityConsumerFromEntity;

        [ReadOnly]
        public ComponentLookup<WaterConsumer> m_WaterConsumerFromEntity;

        [ReadOnly]
        public ComponentLookup<Locked> m_LockedFromEntity;

        [ReadOnly]
        public ComponentLookup<Transform> m_TransformFromEntity;

        [ReadOnly]
        public ComponentLookup<GarbageProducer> m_GarbageProducersFromEntity;

        [ReadOnly]
        public ComponentLookup<CrimeProducer> m_CrimeProducersFromEntity;

        [ReadOnly]
        public ComponentLookup<MailProducer> m_MailProducerFromEntity;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDataFromEntity;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifierFromEntity;

        [ReadOnly]
        public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverageFromEntity;

        public CitizenHappinessParameterData m_CitizenHappinessParameters;

        public GarbageParameterData m_GarbageParameters;

        public HealthcareParameterData m_HealthcareParameters;

        public ParkParameterData m_ParkParameters;

        public EducationParameterData m_EducationParameters;

        public TelecomParameterData m_TelecomParameters;

        [ReadOnly]
        public DynamicBuffer<HappinessFactorParameterData> m_HappinessFactorParameters;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverage;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<int> m_TaxRates;

        public NativeArray<int2> m_Factors;

        public NativeArray<int> m_Results;

        public Entity m_City;

        public float m_RelativeElectricityFee;

        public float m_RelativeWaterFee;

        public void Execute()
        {
            int happiness = 0;
            int citizenCount = 0;
            if (this.m_BuildingFromEntity.HasComponent(this.m_SelectedEntity) && this.m_ResidentialPropertyFromEntity.HasComponent(this.m_SelectedEntity))
            {
                // 重定向
                BuildingHappinessRe.GetResidentialBuildingHappinessFactors(this.m_City, this.m_TaxRates, this.m_SelectedEntity, this.m_Factors, ref this.m_PrefabRefFromEntity, ref this.m_SpawnableBuildingDataFromEntity, ref this.m_BuildingPropertyDataFromEntity, ref this.m_CityModifierFromEntity, ref this.m_BuildingFromEntity, ref this.m_ElectricityConsumerFromEntity, ref this.m_WaterConsumerFromEntity, ref this.m_ServiceCoverageFromEntity, ref this.m_LockedFromEntity, ref this.m_TransformFromEntity, ref this.m_GarbageProducersFromEntity, ref this.m_CrimeProducersFromEntity, ref this.m_MailProducerFromEntity, ref this.m_RenterFromEntity, ref this.m_CitizenFromEntity, ref this.m_HouseholdCitizenFromEntity, ref this.m_BuildingDataFromEntity, this.m_CitizenHappinessParameters, this.m_GarbageParameters, this.m_HealthcareParameters, this.m_ParkParameters, this.m_EducationParameters, this.m_TelecomParameters, this.m_HappinessFactorParameters, this.m_PollutionMap, this.m_NoisePollutionMap, this.m_AirPollutionMap, this.m_TelecomCoverage, this.m_RelativeElectricityFee, this.m_RelativeWaterFee);
                // mod
                if (TryAddPropertyHappiness(ref happiness, ref citizenCount, this.m_SelectedEntity, this.m_HouseholdFromEntity, this.m_CitizenFromEntity, this.m_HealthProblemFromEntity, this.m_RenterFromEntity, this.m_HouseholdCitizenFromEntity))
                {
                    this.m_Results[1] = citizenCount;
                    this.m_Results[2] = happiness;
                }
                this.m_Results[0] = ((citizenCount > 0) ? 1 : 0);
            }
            else
            {
                if (!this.m_HouseholdCitizenFromEntity.TryGetBuffer(this.m_SelectedEntity, out var bufferData))
                {
                    return;
                }
                for (int i = 0; i < bufferData.Length; i++)
                {
                    Entity citizen = bufferData[i].m_Citizen;
                    if (this.m_CitizenFromEntity.HasComponent(citizen) && !CitizenUtils.IsDead(citizen, ref this.m_HealthProblemFromEntity))
                    {
                        happiness += this.m_CitizenFromEntity[citizen].Happiness;
                        citizenCount++;
                    }
                }
                this.m_Results[0] = 1;
                this.m_Results[1] = citizenCount;
                this.m_Results[2] = happiness;
                if (this.m_PropertyRenterFromEntity.TryGetComponent(this.m_SelectedEntity, out var componentData))
                { 
                    //重定向
                    BuildingHappinessRe.GetResidentialBuildingHappinessFactors(this.m_City, this.m_TaxRates, componentData.m_Property, this.m_Factors, ref this.m_PrefabRefFromEntity, ref this.m_SpawnableBuildingDataFromEntity, ref this.m_BuildingPropertyDataFromEntity, ref this.m_CityModifierFromEntity, ref this.m_BuildingFromEntity, ref this.m_ElectricityConsumerFromEntity, ref this.m_WaterConsumerFromEntity, ref this.m_ServiceCoverageFromEntity, ref this.m_LockedFromEntity, ref this.m_TransformFromEntity, ref this.m_GarbageProducersFromEntity, ref this.m_CrimeProducersFromEntity, ref this.m_MailProducerFromEntity, ref this.m_RenterFromEntity, ref this.m_CitizenFromEntity, ref this.m_HouseholdCitizenFromEntity, ref this.m_BuildingDataFromEntity, this.m_CitizenHappinessParameters, this.m_GarbageParameters, this.m_HealthcareParameters, this.m_ParkParameters, this.m_EducationParameters, this.m_TelecomParameters, this.m_HappinessFactorParameters, this.m_PollutionMap, this.m_NoisePollutionMap, this.m_AirPollutionMap, this.m_TelecomCoverage, this.m_RelativeElectricityFee, this.m_RelativeWaterFee);
                }
            }
        }

        // 重定向
        private static bool TryAddPropertyHappiness(ref int happiness, ref int citizenCount, Entity entity, ComponentLookup<Household> householdFromEntity, ComponentLookup<Citizen> citizenFromEntity, ComponentLookup<HealthProblem> healthProblemFromEntity, BufferLookup<Renter> renterFromEntity, BufferLookup<HouseholdCitizen> householdCitizenFromEntity)
        {
            bool result = false;
            if (renterFromEntity.TryGetBuffer(entity, out var bufferData))
            {
                for (int i = 0; i < bufferData.Length; i++)
                {
                    Entity renter = bufferData[i].m_Renter;
                    if (!householdFromEntity.HasComponent(renter) || !householdCitizenFromEntity.TryGetBuffer(renter, out var bufferData2))
                    {
                        continue;
                    }
                    result = true;
                    for (int j = 0; j < bufferData2.Length; j++)
                    {
                        Entity citizen = bufferData2[j].m_Citizen;
                        if (citizenFromEntity.HasComponent(citizen) && !CitizenUtils.IsDead(citizen, ref healthProblemFromEntity))
                        {
                            happiness += citizenFromEntity[citizen].Happiness;
                            citizenCount++;
                        }
                    }
                }
            }
            return result;
        }
    }

    [BurstCompile]
    public struct CountDistrictHappinessJob : IJobChunk
    {
        [ReadOnly]
        public Entity m_SelectedEntity;

        [ReadOnly]
        public EntityTypeHandle m_EntityHandle;

        [ReadOnly]
        public ComponentTypeHandle<CurrentDistrict> m_CurrentDistrictHandle;

        [ReadOnly]
        public ComponentLookup<Household> m_HouseholdFromEntity;

        [ReadOnly]
        public ComponentLookup<Citizen> m_CitizenFromEntity;

        [ReadOnly]
        public ComponentLookup<HealthProblem> m_HealthProblemFromEntity;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_HouseholdCitizenFromEntity;

        [ReadOnly]
        public BufferLookup<Renter> m_RenterFromEntity;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefFromEntity;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingDataFromEntity;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDataFromEntity;

        [ReadOnly]
        public ComponentLookup<Building> m_BuildingFromEntity;

        [ReadOnly]
        public ComponentLookup<ElectricityConsumer> m_ElectricityConsumerFromEntity;

        [ReadOnly]
        public ComponentLookup<WaterConsumer> m_WaterConsumerFromEntity;

        [ReadOnly]
        public ComponentLookup<Locked> m_LockedFromEntity;

        [ReadOnly]
        public ComponentLookup<Transform> m_TransformFromEntity;

        [ReadOnly]
        public ComponentLookup<GarbageProducer> m_GarbageProducersFromEntity;

        [ReadOnly]
        public ComponentLookup<CrimeProducer> m_CrimeProducersFromEntity;

        [ReadOnly]
        public ComponentLookup<MailProducer> m_MailProducerFromEntity;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDataFromEntity;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifierFromEntity;

        [ReadOnly]
        public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverageFromEntity;

        public CitizenHappinessParameterData m_CitizenHappinessParameters;

        public GarbageParameterData m_GarbageParameters;

        public HealthcareParameterData m_HealthcareParameters;

        public ParkParameterData m_ParkParameters;

        public EducationParameterData m_EducationParameters;

        public TelecomParameterData m_TelecomParameters;

        [ReadOnly]
        public DynamicBuffer<HappinessFactorParameterData> m_HappinessFactorParameters;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverage;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoisePollutionMap;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<int> m_TaxRates;

        public NativeArray<int2> m_Factors;

        public NativeArray<int> m_Results;

        public Entity m_City;

        public float m_RelativeElectricityFee;

        public float m_RelativeWaterFee;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityHandle);
            NativeArray<CurrentDistrict> nativeArray2 = chunk.GetNativeArray(ref this.m_CurrentDistrictHandle);
            int num = 0;
            int happiness = 0;
            int citizenCount = 0;
            for (int i = 0; i < nativeArray.Length; i++)
            {
                Entity entity = nativeArray[i];
                // 重定向TryAddPropertyHappiness
                if (!(nativeArray2[i].m_District != this.m_SelectedEntity) && this.m_SpawnableBuildingDataFromEntity.HasComponent(this.m_PrefabRefFromEntity[entity].m_Prefab) && TryAddPropertyHappiness(ref happiness, ref citizenCount, entity, this.m_HouseholdFromEntity, this.m_CitizenFromEntity, this.m_HealthProblemFromEntity, this.m_RenterFromEntity, this.m_HouseholdCitizenFromEntity))
                {
                    num = 1;
                    // 重定向
                    BuildingHappinessRe.GetResidentialBuildingHappinessFactors(this.m_City, this.m_TaxRates, entity, this.m_Factors, ref this.m_PrefabRefFromEntity, ref this.m_SpawnableBuildingDataFromEntity, ref this.m_BuildingPropertyDataFromEntity, ref this.m_CityModifierFromEntity, ref this.m_BuildingFromEntity, ref this.m_ElectricityConsumerFromEntity, ref this.m_WaterConsumerFromEntity, ref this.m_ServiceCoverageFromEntity, ref this.m_LockedFromEntity, ref this.m_TransformFromEntity, ref this.m_GarbageProducersFromEntity, ref this.m_CrimeProducersFromEntity, ref this.m_MailProducerFromEntity, ref this.m_RenterFromEntity, ref this.m_CitizenFromEntity, ref this.m_HouseholdCitizenFromEntity, ref this.m_BuildingDataFromEntity, this.m_CitizenHappinessParameters, this.m_GarbageParameters, this.m_HealthcareParameters, this.m_ParkParameters, this.m_EducationParameters, this.m_TelecomParameters, this.m_HappinessFactorParameters, this.m_PollutionMap, this.m_NoisePollutionMap, this.m_AirPollutionMap, this.m_TelecomCoverage, this.m_RelativeElectricityFee, this.m_RelativeWaterFee);
                }
            }
            this.m_Results[0] += num;
            this.m_Results[1] += citizenCount;
            this.m_Results[2] += happiness;
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }

        // 重定向
        private static bool TryAddPropertyHappiness(ref int happiness, ref int citizenCount, Entity entity, ComponentLookup<Household> householdFromEntity, ComponentLookup<Citizen> citizenFromEntity, ComponentLookup<HealthProblem> healthProblemFromEntity, BufferLookup<Renter> renterFromEntity, BufferLookup<HouseholdCitizen> householdCitizenFromEntity)
        {
            bool result = false;
            if (renterFromEntity.TryGetBuffer(entity, out var bufferData))
            {
                for (int i = 0; i < bufferData.Length; i++)
                {
                    Entity renter = bufferData[i].m_Renter;
                    if (!householdFromEntity.HasComponent(renter) || !householdCitizenFromEntity.TryGetBuffer(renter, out var bufferData2))
                    {
                        continue;
                    }
                    result = true;
                    for (int j = 0; j < bufferData2.Length; j++)
                    {
                        Entity citizen = bufferData2[j].m_Citizen;
                        if (citizenFromEntity.HasComponent(citizen) && !CitizenUtils.IsDead(citizen, ref healthProblemFromEntity))
                        {
                            happiness += citizenFromEntity[citizen].Happiness;
                            citizenCount++;
                        }
                    }
                }
            }
            return result;
        }
    }











}
