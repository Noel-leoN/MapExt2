// Game.UI.InGame.BuildingHappiness.cs

using Colossal.Entities;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.MapExt.ReBurstSystemModeD
{
    // 静态方法库，无BCJob
    public static class BuildingHappinessRe
    {
        public static void GetResidentialBuildingHappinessFactors(Entity city, NativeArray<int> taxRates, Entity property, NativeArray<int2> factors, ref ComponentLookup<PrefabRef> prefabs, ref ComponentLookup<SpawnableBuildingData> spawnableBuildings, ref ComponentLookup<BuildingPropertyData> buildingPropertyDatas, ref BufferLookup<CityModifier> cityModifiers, ref ComponentLookup<Building> buildings, ref ComponentLookup<ElectricityConsumer> electricityConsumers, ref ComponentLookup<WaterConsumer> waterConsumers, ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages, ref ComponentLookup<Locked> locked, ref ComponentLookup<Game.Objects.Transform> transforms, ref ComponentLookup<GarbageProducer> garbageProducers, ref ComponentLookup<CrimeProducer> crimeProducers, ref ComponentLookup<MailProducer> mailProducers, ref BufferLookup<Renter> renters, ref ComponentLookup<Citizen> citizenDatas, ref BufferLookup<HouseholdCitizen> householdCitizens, ref ComponentLookup<BuildingData> buildingDatas, CitizenHappinessParameterData citizenHappinessParameters, GarbageParameterData garbageParameters, HealthcareParameterData healthcareParameters, ParkParameterData parkParameters, EducationParameterData educationParameters, TelecomParameterData telecomParameters, DynamicBuffer<HappinessFactorParameterData> happinessFactorParameters, NativeArray<GroundPollution> pollutionMap, NativeArray<NoisePollution> noisePollutionMap, NativeArray<AirPollution> airPollutionMap, CellMapData<TelecomCoverage> telecomCoverage, float relativeElectricityFee, float relativeWaterFee)
        {
            if (!prefabs.HasComponent(property))
            {
                return;
            }
            Entity prefab = prefabs[property].m_Prefab;
            if (!spawnableBuildings.HasComponent(prefab) || !buildingDatas.HasComponent(prefab))
            {
                return;
            }
            BuildingPropertyData buildingPropertyData = buildingPropertyDatas[prefab];
            DynamicBuffer<CityModifier> cityModifiers2 = cityModifiers[city];
            BuildingData buildingData = buildingDatas[prefab];
            float num = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
            Entity entity = Entity.Null;
            float curvePosition = 0f;
            int level = spawnableBuildings[prefab].m_Level;
            if (buildings.HasComponent(property))
            {
                Building building = buildings[property];
                entity = building.m_RoadEdge;
                curvePosition = building.m_CurvePosition;
            }
            if (buildingPropertyData.m_ResidentialProperties <= 0)
            {
                return;
            }
            num /= (float)buildingPropertyData.m_ResidentialProperties;
            float num2 = 1f;
            int currentHappiness = 50;
            int num3 = 128;
            float num4 = 0.3f;
            float num5 = 0.25f;
            float num6 = 0.25f;
            float num7 = 0.15f;
            float num8 = 0.05f;
            float num9 = 2f;
            if (renters.HasBuffer(property))
            {
                num4 = 0f;
                num5 = 0f;
                num6 = 0f;
                num7 = 0f;
                num8 = 0f;
                int2 @int = default(int2);
                int2 int2 = default(int2);
                int num10 = 0;
                int num11 = 0;
                DynamicBuffer<Renter> dynamicBuffer = renters[property];
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    Entity renter = dynamicBuffer[i].m_Renter;
                    if (!householdCitizens.HasBuffer(renter))
                    {
                        continue;
                    }
                    num11++;
                    DynamicBuffer<HouseholdCitizen> dynamicBuffer2 = householdCitizens[renter];
                    for (int j = 0; j < dynamicBuffer2.Length; j++)
                    {
                        Entity citizen = dynamicBuffer2[j].m_Citizen;
                        if (citizenDatas.HasComponent(citizen))
                        {
                            Citizen citizen2 = citizenDatas[citizen];
                            int2.x += citizen2.Happiness;
                            int2.y++;
                            num10 += citizen2.m_LeisureCounter;
                            switch (citizen2.GetEducationLevel())
                            {
                                case 0:
                                    num4 += 1f;
                                    break;
                                case 1:
                                    num5 += 1f;
                                    break;
                                case 2:
                                    num6 += 1f;
                                    break;
                                case 3:
                                    num7 += 1f;
                                    break;
                                case 4:
                                    num8 += 1f;
                                    break;
                            }
                            if (citizen2.GetAge() == CitizenAge.Child)
                            {
                                @int.x++;
                            }
                        }
                    }
                    @int.y++;
                }
                if (@int.y > 0)
                {
                    num2 = (float)@int.x / (float)@int.y;
                }
                if (int2.y > 0)
                {
                    currentHappiness = Mathf.RoundToInt((float)int2.x / (float)int2.y);
                    num3 = Mathf.RoundToInt((float)num10 / (float)int2.y);
                    num4 /= (float)int2.y;
                    num5 /= (float)int2.y;
                    num6 /= (float)int2.y;
                    num7 /= (float)int2.y;
                    num8 /= (float)int2.y;
                    num9 = (float)int2.y / (float)num11;
                }
            }
            Entity healthcareServicePrefab = healthcareParameters.m_HealthcareServicePrefab;
            Entity parkServicePrefab = parkParameters.m_ParkServicePrefab;
            Entity educationServicePrefab = educationParameters.m_EducationServicePrefab;
            Entity telecomServicePrefab = telecomParameters.m_TelecomServicePrefab;
            if (!locked.HasEnabledComponent(happinessFactorParameters[4].m_LockedEntity))
            {
                int2 electricitySupplyBonuses = CitizenHappinessSystem.GetElectricitySupplyBonuses(property, ref electricityConsumers, in citizenHappinessParameters);
                int2 value = factors[3];
                value.x++;
                value.y += (electricitySupplyBonuses.x + electricitySupplyBonuses.y) / 2 - happinessFactorParameters[4].m_BaseLevel;
                factors[3] = value;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[23].m_LockedEntity))
            {
                int2 electricityFeeBonuses = CitizenHappinessSystem.GetElectricityFeeBonuses(property, ref electricityConsumers, relativeElectricityFee, in citizenHappinessParameters);
                int2 value2 = factors[26];
                value2.x++;
                value2.y += (electricityFeeBonuses.x + electricityFeeBonuses.y) / 2 - happinessFactorParameters[23].m_BaseLevel;
                factors[26] = value2;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[8].m_LockedEntity))
            {
                int2 waterSupplyBonuses = CitizenHappinessSystem.GetWaterSupplyBonuses(property, ref waterConsumers, in citizenHappinessParameters);
                int2 value3 = factors[7];
                value3.x++;
                value3.y += (waterSupplyBonuses.x + waterSupplyBonuses.y) / 2 - happinessFactorParameters[8].m_BaseLevel;
                factors[7] = value3;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[24].m_LockedEntity))
            {
                int2 waterFeeBonuses = CitizenHappinessSystem.GetWaterFeeBonuses(property, ref waterConsumers, relativeWaterFee, in citizenHappinessParameters);
                int2 value4 = factors[27];
                value4.x++;
                value4.y += (waterFeeBonuses.x + waterFeeBonuses.y) / 2 - happinessFactorParameters[24].m_BaseLevel;
                factors[27] = value4;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[9].m_LockedEntity))
            {
                int2 waterPollutionBonuses = CitizenHappinessSystem.GetWaterPollutionBonuses(property, ref waterConsumers, cityModifiers2, in citizenHappinessParameters);
                int2 value5 = factors[8];
                value5.x++;
                value5.y += (waterPollutionBonuses.x + waterPollutionBonuses.y) / 2 - happinessFactorParameters[9].m_BaseLevel;
                factors[8] = value5;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[10].m_LockedEntity))
            {
                int2 sewageBonuses = CitizenHappinessSystem.GetSewageBonuses(property, ref waterConsumers, in citizenHappinessParameters);
                int2 value6 = factors[9];
                value6.x++;
                value6.y += (sewageBonuses.x + sewageBonuses.y) / 2 - happinessFactorParameters[10].m_BaseLevel;
                factors[9] = value6;
            }
            if (serviceCoverages.HasBuffer(entity))
            {
                DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = serviceCoverages[entity];
                if (!locked.HasEnabledComponent(happinessFactorParameters[5].m_LockedEntity))
                {
                    int2 healthcareBonuses = CitizenHappinessSystem.GetHealthcareBonuses(curvePosition, serviceCoverage, ref locked, healthcareServicePrefab, in citizenHappinessParameters);
                    int2 value7 = factors[4];
                    value7.x++;
                    value7.y += (healthcareBonuses.x + healthcareBonuses.y) / 2 - happinessFactorParameters[5].m_BaseLevel;
                    factors[4] = value7;
                }
                if (!locked.HasEnabledComponent(happinessFactorParameters[12].m_LockedEntity))
                {
                    int2 entertainmentBonuses = CitizenHappinessSystem.GetEntertainmentBonuses(curvePosition, serviceCoverage, cityModifiers2, ref locked, parkServicePrefab, in citizenHappinessParameters);
                    int2 value8 = factors[11];
                    value8.x++;
                    value8.y += (entertainmentBonuses.x + entertainmentBonuses.y) / 2 - happinessFactorParameters[12].m_BaseLevel;
                    factors[11] = value8;
                }
                if (!locked.HasEnabledComponent(happinessFactorParameters[13].m_LockedEntity))
                {
                    int2 educationBonuses = CitizenHappinessSystem.GetEducationBonuses(curvePosition, serviceCoverage, ref locked, educationServicePrefab, in citizenHappinessParameters, 1);
                    int2 value9 = factors[12];
                    value9.x++;
                    value9.y += Mathf.RoundToInt(num2 * (float)(educationBonuses.x + educationBonuses.y) / 2f) - happinessFactorParameters[13].m_BaseLevel;
                    factors[12] = value9;
                }
                if (!locked.HasEnabledComponent(happinessFactorParameters[15].m_LockedEntity))
                {
                    int2 wellfareBonuses = CitizenHappinessSystem.GetWellfareBonuses(curvePosition, serviceCoverage, in citizenHappinessParameters, currentHappiness);
                    int2 value10 = factors[14];
                    value10.x++;
                    value10.y += (wellfareBonuses.x + wellfareBonuses.y) / 2 - happinessFactorParameters[15].m_BaseLevel;
                    factors[14] = value10;
                }
            }
            // method替换
            if (!locked.HasEnabledComponent(happinessFactorParameters[6].m_LockedEntity))
            {
                int2 groundPollutionBonuses = CitizenHappinessJob.GetGroundPollutionBonuses(property, ref transforms, pollutionMap, cityModifiers2, in citizenHappinessParameters);
                int2 value11 = factors[5];
                value11.x++;
                value11.y += (groundPollutionBonuses.x + groundPollutionBonuses.y) / 2 - happinessFactorParameters[6].m_BaseLevel;
                factors[5] = value11;
            }
            // method替换
            if (!locked.HasEnabledComponent(happinessFactorParameters[2].m_LockedEntity))
            {
                int2 airPollutionBonuses = CitizenHappinessJob.GetAirPollutionBonuses(property, ref transforms, airPollutionMap, cityModifiers2, in citizenHappinessParameters);
                int2 value12 = factors[2];
                value12.x++;
                value12.y += (airPollutionBonuses.x + airPollutionBonuses.y) / 2 - happinessFactorParameters[2].m_BaseLevel;
                factors[2] = value12;
            }
            // method替换
            if (!locked.HasEnabledComponent(happinessFactorParameters[7].m_LockedEntity))
            {
                int2 noiseBonuses = CitizenHappinessJob.GetNoiseBonuses(property, ref transforms, noisePollutionMap, in citizenHappinessParameters);
                int2 value13 = factors[6];
                value13.x++;
                value13.y += (noiseBonuses.x + noiseBonuses.y) / 2 - happinessFactorParameters[7].m_BaseLevel;
                factors[6] = value13;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[11].m_LockedEntity))
            {
                int2 garbageBonuses = CitizenHappinessSystem.GetGarbageBonuses(property, ref garbageProducers, ref locked, happinessFactorParameters[11].m_LockedEntity, in garbageParameters);
                int2 value14 = factors[10];
                value14.x++;
                value14.y += (garbageBonuses.x + garbageBonuses.y) / 2 - happinessFactorParameters[11].m_BaseLevel;
                factors[10] = value14;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[1].m_LockedEntity))
            {
                int2 crimeBonuses = CitizenHappinessSystem.GetCrimeBonuses(default(CrimeVictim), property, ref crimeProducers, ref locked, happinessFactorParameters[1].m_LockedEntity, in citizenHappinessParameters);
                int2 value15 = factors[1];
                value15.x++;
                value15.y += (crimeBonuses.x + crimeBonuses.y) / 2 - happinessFactorParameters[1].m_BaseLevel;
                factors[1] = value15;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[14].m_LockedEntity))
            {
                int2 mailBonuses = CitizenHappinessSystem.GetMailBonuses(property, ref mailProducers, ref locked, telecomServicePrefab, in citizenHappinessParameters);
                int2 value16 = factors[13];
                value16.x++;
                value16.y += (mailBonuses.x + mailBonuses.y) / 2 - happinessFactorParameters[14].m_BaseLevel;
                factors[13] = value16;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[0].m_LockedEntity))
            {
                int2 telecomBonuses = CitizenHappinessSystem.GetTelecomBonuses(property, ref transforms, telecomCoverage, ref locked, telecomServicePrefab, in citizenHappinessParameters);
                int2 value17 = factors[0];
                value17.x++;
                value17.y += (telecomBonuses.x + telecomBonuses.y) / 2 - happinessFactorParameters[0].m_BaseLevel;
                factors[0] = value17;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[16].m_LockedEntity))
            {
                int2 leisureBonuses = CitizenHappinessSystem.GetLeisureBonuses((byte)num3);
                int2 value18 = factors[15];
                value18.x++;
                value18.y += (leisureBonuses.x + leisureBonuses.y) / 2 - happinessFactorParameters[16].m_BaseLevel;
                factors[15] = value18;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[17].m_LockedEntity))
            {
                float2 @float = new float2(num4, num4) * CitizenHappinessSystem.GetTaxBonuses(0, taxRates, in citizenHappinessParameters) + new float2(num5, num5) * CitizenHappinessSystem.GetTaxBonuses(1, taxRates, in citizenHappinessParameters) + new float2(num6, num6) * CitizenHappinessSystem.GetTaxBonuses(2, taxRates, in citizenHappinessParameters) + new float2(num7, num7) * CitizenHappinessSystem.GetTaxBonuses(3, taxRates, in citizenHappinessParameters) + new float2(num8, num8) * CitizenHappinessSystem.GetTaxBonuses(4, taxRates, in citizenHappinessParameters);
                int2 value19 = factors[16];
                value19.x++;
                value19.y += Mathf.RoundToInt(@float.x + @float.y) / 2 - happinessFactorParameters[17].m_BaseLevel;
                factors[16] = value19;
            }
            if (!locked.HasEnabledComponent(happinessFactorParameters[3].m_LockedEntity))
            {
                float2 float2 = CitizenHappinessSystem.GetApartmentWellbeing(buildingPropertyData.m_SpaceMultiplier * num / num9, level);
                int2 value20 = factors[21];
                value20.x++;
                value20.y += Mathf.RoundToInt(float2.x + float2.y) / 2 - happinessFactorParameters[3].m_BaseLevel;
                factors[21] = value20;
            }
        }

        public static void GetCompanyHappinessFactors(Entity property, NativeArray<int2> factors, ref ComponentLookup<PrefabRef> prefabs, ref ComponentLookup<SpawnableBuildingData> spawnableBuildings, ref ComponentLookup<BuildingPropertyData> buildingPropertyDatas, ref ComponentLookup<Building> buildings, ref ComponentLookup<OfficeBuilding> officeBuildings, ref BufferLookup<Renter> renters, ref ComponentLookup<BuildingData> buildingDatas, ref ComponentLookup<CompanyData> companies, ref ComponentLookup<IndustrialProcessData> industrialProcessDatas, ref ComponentLookup<WorkProvider> workProviders, ref BufferLookup<Employee> employees, ref ComponentLookup<WorkplaceData> workplaceDatas, ref ComponentLookup<Citizen> citizens, ref ComponentLookup<HealthProblem> healthProblems, ref ComponentLookup<ServiceAvailable> serviceAvailables, ref ComponentLookup<ResourceData> resourceDatas, ref ComponentLookup<ZonePropertiesData> zonePropertiesDatas, ref BufferLookup<Efficiency> efficiencies, ref ComponentLookup<ServiceCompanyData> serviceCompanyDatas, ref BufferLookup<ResourceAvailability> availabilities, ref BufferLookup<TradeCost> tradeCosts, EconomyParameterData economyParameters, NativeArray<int> taxRates, NativeArray<Entity> processes, ResourcePrefabs resourcePrefabs)
        {
            if (!prefabs.HasComponent(property))
            {
                return;
            }
            Entity prefab = prefabs[property].m_Prefab;
            if (!spawnableBuildings.HasComponent(prefab) || !buildingDatas.HasComponent(prefab))
            {
                return;
            }
            BuildingPropertyData buildingPropertyData = buildingPropertyDatas[prefab];
            BuildingData buildingData = buildingDatas[prefab];
            SpawnableBuildingData spawnableData = spawnableBuildings[prefab];
            int level = spawnableData.m_Level;
            Building building = default(Building);
            if (buildings.HasComponent(property))
            {
                building = buildings[property];
            }
            bool flag = false;
            Entity entity = default(Entity);
            Entity entity2 = default(Entity);
            IndustrialProcessData processData = default(IndustrialProcessData);
            ServiceCompanyData serviceCompanyData = default(ServiceCompanyData);
            Resource resource = buildingPropertyData.m_AllowedManufactured | buildingPropertyData.m_AllowedSold;
            if (resource == Resource.NoResource)
            {
                return;
            }
            if (renters.HasBuffer(property))
            {
                DynamicBuffer<Renter> dynamicBuffer = renters[property];
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    entity = dynamicBuffer[i].m_Renter;
                    if (!companies.HasComponent(entity) || !prefabs.HasComponent(entity))
                    {
                        continue;
                    }
                    entity2 = prefabs[entity].m_Prefab;
                    if (industrialProcessDatas.HasComponent(entity2))
                    {
                        if (serviceCompanyDatas.HasComponent(entity2))
                        {
                            serviceCompanyData = serviceCompanyDatas[entity2];
                        }
                        processData = industrialProcessDatas[entity2];
                        flag = true;
                        break;
                    }
                }
            }
            if (flag)
            {
                BuildingHappinessRe.AddCompanyHappinessFactors(factors, property, prefab, entity, entity2, processData, serviceCompanyData, buildingPropertyData.m_AllowedSold != Resource.NoResource, level, ref officeBuildings, ref workProviders, ref employees, ref workplaceDatas, ref citizens, ref healthProblems, ref serviceAvailables, ref buildingPropertyDatas, ref resourceDatas, ref serviceCompanyDatas, ref efficiencies, ref availabilities, ref tradeCosts, taxRates, building, spawnableData, buildingData, resourcePrefabs, ref economyParameters);
                return;
            }
            for (int j = 0; j < processes.Length; j++)
            {
                processData = industrialProcessDatas[processes[j]];
                if (serviceCompanyDatas.HasComponent(processes[j]))
                {
                    serviceCompanyData = serviceCompanyDatas[processes[j]];
                }
                if ((resource & processData.m_Output.m_Resource) != Resource.NoResource)
                {
                    BuildingHappinessRe.AddCompanyHappinessFactors(factors, property, prefab, entity, entity2, processData, serviceCompanyData, buildingPropertyData.m_AllowedSold != Resource.NoResource, level, ref officeBuildings, ref workProviders, ref employees, ref workplaceDatas, ref citizens, ref healthProblems, ref serviceAvailables, ref buildingPropertyDatas, ref resourceDatas, ref serviceCompanyDatas, ref efficiencies, ref availabilities, ref tradeCosts, taxRates, building, spawnableData, buildingData, resourcePrefabs, ref economyParameters);
                }
            }
        }

        private static void AddCompanyHappinessFactors(NativeArray<int2> factors, Entity property, Entity prefab, Entity renter, Entity renterPrefab, IndustrialProcessData processData, ServiceCompanyData serviceCompanyData, bool commercial, int level, ref ComponentLookup<OfficeBuilding> officeBuildings, ref ComponentLookup<WorkProvider> workProviders, ref BufferLookup<Employee> employees, ref ComponentLookup<WorkplaceData> workplaceDatas, ref ComponentLookup<Citizen> citizens, ref ComponentLookup<HealthProblem> healthProblems, ref ComponentLookup<ServiceAvailable> serviceAvailables, ref ComponentLookup<BuildingPropertyData> buildingPropertyDatas, ref ComponentLookup<ResourceData> resourceDatas, ref ComponentLookup<ServiceCompanyData> serviceCompanyDatas, ref BufferLookup<Efficiency> efficiencies, ref BufferLookup<ResourceAvailability> availabilities, ref BufferLookup<TradeCost> tradeCosts, NativeArray<int> taxRates, Building building, SpawnableBuildingData spawnableData, BuildingData buildingData, ResourcePrefabs resourcePrefabs, ref EconomyParameterData economyParameters)
        {
        }
    }
}
