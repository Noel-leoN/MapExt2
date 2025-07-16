// Game.Buildings.PropertyUtils

#define UNITY_ASSERTIONS
using Game.Citizens;
using Game.City;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Buildings;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
	// ¾²Ì¬method¿â£¬ÎÞBCJob
	public static class PropertyUtilsRe
	{
		public static float GetPropertyScore(Entity property, Entity household, DynamicBuffer<HouseholdCitizen> citizenBuffer, ref ComponentLookup<PrefabRef> prefabRefs, ref ComponentLookup<BuildingPropertyData> buildingProperties, ref ComponentLookup<Building> buildings, ref ComponentLookup<BuildingData> buildingDatas, ref ComponentLookup<Household> households, ref ComponentLookup<Citizen> citizens, ref ComponentLookup<Game.Citizens.Student> students, ref ComponentLookup<Worker> workers, ref ComponentLookup<SpawnableBuildingData> spawnableDatas, ref ComponentLookup<CrimeProducer> crimes, ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages, ref ComponentLookup<Locked> locked, ref ComponentLookup<ElectricityConsumer> electricityConsumers, ref ComponentLookup<WaterConsumer> waterConsumers, ref ComponentLookup<GarbageProducer> garbageProducers, ref ComponentLookup<MailProducer> mailProducers, ref ComponentLookup<Game.Objects.Transform> transforms, ref ComponentLookup<Abandoned> abandoneds, ref ComponentLookup<Game.Buildings.Park> parks, ref BufferLookup<ResourceAvailability> availabilities, NativeArray<int> taxRates, NativeArray<GroundPollution> pollutionMap, NativeArray<AirPollution> airPollutionMap, NativeArray<NoisePollution> noiseMap, CellMapData<TelecomCoverage> telecomCoverages, DynamicBuffer<CityModifier> cityModifiers, Entity healthcareService, Entity entertainmentService, Entity educationService, Entity telecomService, Entity garbageService, Entity policeService, CitizenHappinessParameterData citizenHappinessParameterData, GarbageParameterData garbageParameterData)
		{
			if (!buildings.HasComponent(property))
			{
				return float.NegativeInfinity;
			}
			bool flag = (households[household].m_Flags & HouseholdFlags.MovedIn) != 0;
			bool flag2 = BuildingUtils.IsHomelessShelterBuilding(property, ref parks, ref abandoneds);
			if (flag2 && !flag)
			{
				return float.NegativeInfinity;
			}
			Building buildingData = buildings[property];
			Entity prefab = prefabRefs[property].m_Prefab;
            HouseholdFindPropertySystem.GenericApartmentQuality genericApartmentQuality = PropertyUtilsRe.GetGenericApartmentQuality(property, prefab, ref buildingData, ref buildingProperties, ref buildingDatas, ref spawnableDatas, ref crimes, ref serviceCoverages, ref locked, ref electricityConsumers, ref waterConsumers, ref garbageProducers, ref mailProducers, ref transforms, ref abandoneds, pollutionMap, airPollutionMap, noiseMap, telecomCoverages, cityModifiers, healthcareService, entertainmentService, educationService, telecomService, garbageService, policeService, citizenHappinessParameterData, garbageParameterData);
			int length = citizenBuffer.Length;
			float num = 0f;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			for (int i = 0; i < citizenBuffer.Length; i++)
			{
				Entity citizen = citizenBuffer[i].m_Citizen;
				Citizen citizen2 = citizens[citizen];
				num4 += citizen2.Happiness;
				if (citizen2.GetAge() == CitizenAge.Child)
				{
					num5++;
				}
				else
				{
					num3++;
					num6 += CitizenHappinessSystem.GetTaxBonuses(citizen2.GetEducationLevel(), taxRates, in citizenHappinessParameterData).y;
				}
				if (students.HasComponent(citizen))
				{
					num2++;
					Game.Citizens.Student student = students[citizen];
					if (student.m_School != property)
					{
						num += student.m_LastCommuteTime;
					}
				}
				else if (workers.HasComponent(citizen))
				{
					num2++;
					Worker worker = workers[citizen];
					if (worker.m_Workplace != property)
					{
						num += worker.m_LastCommuteTime;
					}
				}
			}
			if (num2 > 0)
			{
				num /= (float)num2;
			}
			if (citizenBuffer.Length > 0)
			{
				num4 /= citizenBuffer.Length;
				if (num3 > 0)
				{
					num6 /= num3;
				}
			}
			float serviceAvailability = PropertyUtils.GetServiceAvailability(buildingData.m_RoadEdge, buildingData.m_CurvePosition, availabilities);
			float cachedApartmentQuality = PropertyUtils.GetCachedApartmentQuality(length, num5, num4, genericApartmentQuality);
			float num7 = (flag2 ? (-1000) : 0);
			return serviceAvailability + cachedApartmentQuality + (float)(2 * num6) - num + num7;
		}
		

		public static HouseholdFindPropertySystem.GenericApartmentQuality GetGenericApartmentQuality(Entity building, Entity buildingPrefab, ref Building buildingData, ref ComponentLookup<BuildingPropertyData> buildingProperties, ref ComponentLookup<BuildingData> buildingDatas, ref ComponentLookup<SpawnableBuildingData> spawnableDatas, ref ComponentLookup<CrimeProducer> crimes, ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages, ref ComponentLookup<Locked> locked, ref ComponentLookup<ElectricityConsumer> electricityConsumers, ref ComponentLookup<WaterConsumer> waterConsumers, ref ComponentLookup<GarbageProducer> garbageProducers, ref ComponentLookup<MailProducer> mailProducers, ref ComponentLookup<Game.Objects.Transform> transforms, ref ComponentLookup<Abandoned> abandoneds, NativeArray<GroundPollution> pollutionMap, NativeArray<AirPollution> airPollutionMap, NativeArray<NoisePollution> noiseMap, CellMapData<TelecomCoverage> telecomCoverages, DynamicBuffer<CityModifier> cityModifiers, Entity healthcareService, Entity entertainmentService, Entity educationService, Entity telecomService, Entity garbageService, Entity policeService, CitizenHappinessParameterData happinessParameterData, GarbageParameterData garbageParameterData)
		{
            HouseholdFindPropertySystem.GenericApartmentQuality result = default(HouseholdFindPropertySystem.GenericApartmentQuality);
			bool flag = true;
			BuildingPropertyData buildingPropertyData = default(BuildingPropertyData);
			SpawnableBuildingData spawnableBuildingData = default(SpawnableBuildingData);
			if (buildingProperties.HasComponent(buildingPrefab))
			{
				buildingPropertyData = buildingProperties[buildingPrefab];
				flag = false;
			}
			BuildingData buildingData2 = buildingDatas[buildingPrefab];
			if (spawnableDatas.HasComponent(buildingPrefab) && !abandoneds.HasComponent(building))
			{
				spawnableBuildingData = spawnableDatas[buildingPrefab];
			}
			else
			{
				flag = true;
			}
			result.apartmentSize = (flag ? PropertyUtils.kHomelessApartmentSize : (buildingPropertyData.m_SpaceMultiplier * (float)buildingData2.m_LotSize.x * (float)buildingData2.m_LotSize.y / math.max(1f, buildingPropertyData.m_ResidentialProperties)));
			result.level = spawnableBuildingData.m_Level;
			int2 @int = default(int2);
			int2 healthcareBonuses;
			if (serviceCoverages.HasBuffer(buildingData.m_RoadEdge))
			{
				DynamicBuffer<Game.Net.ServiceCoverage> serviceCoverage = serviceCoverages[buildingData.m_RoadEdge];
				healthcareBonuses = CitizenHappinessSystem.GetHealthcareBonuses(buildingData.m_CurvePosition, serviceCoverage, ref locked, healthcareService, in happinessParameterData);
				@int += healthcareBonuses;
				healthcareBonuses = CitizenHappinessSystem.GetEntertainmentBonuses(buildingData.m_CurvePosition, serviceCoverage, cityModifiers, ref locked, entertainmentService, in happinessParameterData);
				@int += healthcareBonuses;
				result.welfareBonus = CitizenHappinessSystem.GetWelfareValue(buildingData.m_CurvePosition, serviceCoverage, in happinessParameterData);
				result.educationBonus = CitizenHappinessSystem.GetEducationBonuses(buildingData.m_CurvePosition, serviceCoverage, ref locked, educationService, in happinessParameterData, 1);
			}
			int2 crimeBonuses = CitizenHappinessSystem.GetCrimeBonuses(default(CrimeVictim), building, ref crimes, ref locked, policeService, in happinessParameterData);
			healthcareBonuses = (flag ? new int2(0, -happinessParameterData.m_MaxCrimePenalty - crimeBonuses.y) : crimeBonuses);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessJob.GetGroundPollutionBonuses(building, ref transforms, pollutionMap, cityModifiers, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessJob.GetAirPollutionBonuses(building, ref transforms, airPollutionMap, cityModifiers, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessJob.GetNoiseBonuses(building, ref transforms, noiseMap, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessSystem.GetTelecomBonuses(building, ref transforms, telecomCoverages, ref locked, telecomService, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = PropertyUtils.GetElectricityBonusForApartmentQuality(building, ref electricityConsumers, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = PropertyUtils.GetWaterBonusForApartmentQuality(building, ref waterConsumers, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = PropertyUtils.GetSewageBonusForApartmentQuality(building, ref waterConsumers, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessSystem.GetWaterPollutionBonuses(building, ref waterConsumers, cityModifiers, in happinessParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessSystem.GetGarbageBonuses(building, ref garbageProducers, ref locked, garbageService, in garbageParameterData);
			@int += healthcareBonuses;
			healthcareBonuses = CitizenHappinessSystem.GetMailBonuses(building, ref mailProducers, ref locked, telecomService, in happinessParameterData);
			@int += healthcareBonuses;
			if (flag)
			{
				healthcareBonuses = CitizenHappinessSystem.GetHomelessBonuses(in happinessParameterData);
				@int += healthcareBonuses;
			}
			result.score = @int.x + @int.y;
			return result;
		}

		public static float GetApartmentQuality(int familySize, int children, Entity building, ref Building buildingData, Entity buildingPrefab, ref ComponentLookup<BuildingPropertyData> buildingProperties, ref ComponentLookup<BuildingData> buildingDatas, ref ComponentLookup<SpawnableBuildingData> spawnableDatas, ref ComponentLookup<CrimeProducer> crimes, ref BufferLookup<Game.Net.ServiceCoverage> serviceCoverages, ref ComponentLookup<Locked> locked, ref ComponentLookup<ElectricityConsumer> electricityConsumers, ref ComponentLookup<WaterConsumer> waterConsumers, ref ComponentLookup<GarbageProducer> garbageProducers, ref ComponentLookup<MailProducer> mailProducers, ref ComponentLookup<PrefabRef> prefabs, ref ComponentLookup<Game.Objects.Transform> transforms, ref ComponentLookup<Abandoned> abandoneds, NativeArray<GroundPollution> pollutionMap, NativeArray<AirPollution> airPollutionMap, NativeArray<NoisePollution> noiseMap, CellMapData<TelecomCoverage> telecomCoverages, DynamicBuffer<CityModifier> cityModifiers, Entity healthcareService, Entity entertainmentService, Entity educationService, Entity telecomService, Entity garbageService, Entity policeService, CitizenHappinessParameterData happinessParameterData, GarbageParameterData garbageParameterData, int averageHappiness)
		{
            HouseholdFindPropertySystem.GenericApartmentQuality genericApartmentQuality = PropertyUtilsRe.GetGenericApartmentQuality(building, buildingPrefab, ref buildingData, ref buildingProperties, ref buildingDatas, ref spawnableDatas, ref crimes, ref serviceCoverages, ref locked, ref electricityConsumers, ref waterConsumers, ref garbageProducers, ref mailProducers, ref transforms, ref abandoneds, pollutionMap, airPollutionMap, noiseMap, telecomCoverages, cityModifiers, healthcareService, entertainmentService, educationService, telecomService, garbageService, policeService, happinessParameterData, garbageParameterData);
			int2 cachedWelfareBonuses = CitizenHappinessSystem.GetCachedWelfareBonuses(genericApartmentQuality.welfareBonus, averageHappiness);
			return CitizenHappinessSystem.GetApartmentWellbeing(genericApartmentQuality.apartmentSize / (float)familySize, spawnableDatas[buildingPrefab].m_Level) + math.sqrt(children) * (genericApartmentQuality.educationBonus.x + genericApartmentQuality.educationBonus.y) + (float)cachedWelfareBonuses.x + (float)cachedWelfareBonuses.y + genericApartmentQuality.score;
		}

	}
}
