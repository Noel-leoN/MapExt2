// Game.Simulation.HouseholdFindPropertySystem
// OnUpdate

using Colossal.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Debug;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Game.Zones;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;
using static Game.Simulation.HouseholdFindPropertySystem;

namespace MapExtPDX.MapExt.ReBurstSystemModeA
{
    // v1.3.6f变动
    // 关联PropertyUtilsRe
    // StartHomeFinding寻找住所数量待测试修补

    [BurstCompile]
    public struct PreparePropertyJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingProperties;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public BufferLookup<Renter> m_Renters;

        [ReadOnly]
        public ComponentLookup<Abandoned> m_Abandoneds;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.Park> m_Parks;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        [ReadOnly]
        public ComponentLookup<ParkData> m_ParkDatas;

        [ReadOnly]
        public ComponentLookup<Household> m_Households;

        [ReadOnly]
        public ComponentLookup<Building> m_Buildings;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingPropertyData;

        [ReadOnly]
        public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverages;

        [ReadOnly]
        public ComponentLookup<CrimeProducer> m_Crimes;

        [ReadOnly]
        public ComponentLookup<Transform> m_Transforms;

        [ReadOnly]
        public ComponentLookup<Locked> m_Locked;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;

        [ReadOnly]
        public ComponentLookup<WaterConsumer> m_WaterConsumers;

        [ReadOnly]
        public ComponentLookup<GarbageProducer> m_GarbageProducers;

        [ReadOnly]
        public ComponentLookup<MailProducer> m_MailProducers;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoiseMap;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverages;

        public HealthcareParameterData m_HealthcareParameters;

        public ParkParameterData m_ParkParameters;

        public EducationParameterData m_EducationParameters;

        public TelecomParameterData m_TelecomParameters;

        public GarbageParameterData m_GarbageParameters;

        public PoliceConfigurationData m_PoliceParameters;

        public CitizenHappinessParameterData m_CitizenHappinessParameterData;

        public Entity m_City;

        public NativeParallelHashMap<Entity, HouseholdFindPropertySystem.CachedPropertyInformation>.ParallelWriter m_PropertyData;

        private int CalculateFree(Entity property)
        {
            Entity prefab = this.m_Prefabs[property].m_Prefab;
            int num = 0;
            if (this.m_BuildingDatas.HasComponent(prefab) && (this.m_Abandoneds.HasComponent(property) || (this.m_Parks.HasComponent(property) && this.m_ParkDatas[prefab].m_AllowHomeless)))
            {
                num = BuildingUtils.GetShelterHomelessCapacity(prefab, ref this.m_BuildingDatas, ref this.m_BuildingPropertyData) - this.m_Renters[property].Length;
            }
            else if (this.m_BuildingProperties.HasComponent(prefab))
            {
                BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[prefab];
                DynamicBuffer<Renter> dynamicBuffer = this.m_Renters[property];
                num = buildingPropertyData.CountProperties(AreaType.Residential);
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    Entity renter = dynamicBuffer[i].m_Renter;
                    if (this.m_Households.HasComponent(renter))
                    {
                        num--;
                    }
                }
            }
            return num;
        }

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            for (int i = 0; i < nativeArray.Length; i++)
            {
                Entity entity = nativeArray[i];
                int num = this.CalculateFree(entity);
                if (num > 0)
                {
                    Entity prefab = this.m_Prefabs[entity].m_Prefab;
                    Building buildingData = this.m_Buildings[entity];
                    Entity healthcareServicePrefab = this.m_HealthcareParameters.m_HealthcareServicePrefab;
                    Entity parkServicePrefab = this.m_ParkParameters.m_ParkServicePrefab;
                    Entity educationServicePrefab = this.m_EducationParameters.m_EducationServicePrefab;
                    Entity telecomServicePrefab = this.m_TelecomParameters.m_TelecomServicePrefab;
                    Entity garbageServicePrefab = this.m_GarbageParameters.m_GarbageServicePrefab;
                    Entity policeServicePrefab = this.m_PoliceParameters.m_PoliceServicePrefab;
                    DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
                    HouseholdFindPropertySystem.GenericApartmentQuality genericApartmentQuality = PropertyUtilsRe.GetGenericApartmentQuality(entity, prefab, ref buildingData, ref this.m_BuildingProperties, ref this.m_BuildingDatas, ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages, ref this.m_Locked, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers, ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms, ref this.m_Abandoneds, this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap, this.m_TelecomCoverages, cityModifiers, healthcareServicePrefab, parkServicePrefab, educationServicePrefab, telecomServicePrefab, garbageServicePrefab, policeServicePrefab, this.m_CitizenHappinessParameterData, this.m_GarbageParameters);
                    this.m_PropertyData.TryAdd(entity, new HouseholdFindPropertySystem.CachedPropertyInformation
                    {
                        free = num,
                        quality = genericApartmentQuality
                    });
                }
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

    [BurstCompile]
    public struct FindPropertyJob : IJob
    {
        public NativeList<Entity> m_HomelessHouseholdEntities;

        public NativeList<Entity> m_MovedInHouseholdEntities;

        public NativeParallelHashMap<Entity, CachedPropertyInformation> m_CachedPropertyInfo;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        [ReadOnly]
        public ComponentLookup<PropertyOnMarket> m_PropertiesOnMarket;

        [ReadOnly]
        public BufferLookup<PathInformations> m_PathInformationBuffers;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefs;

        [ReadOnly]
        public ComponentLookup<Building> m_Buildings;

        [ReadOnly]
        public ComponentLookup<Worker> m_Workers;

        [ReadOnly]
        public ComponentLookup<Game.Citizens.Student> m_Students;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingProperties;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableDatas;

        [ReadOnly]
        public ComponentLookup<PropertyRenter> m_PropertyRenters;

        [ReadOnly]
        public BufferLookup<ResourceAvailability> m_Availabilities;

        [ReadOnly]
        public BufferLookup<Game.Net.ServiceCoverage> m_ServiceCoverages;

        [ReadOnly]
        public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

        [ReadOnly]
        public ComponentLookup<Citizen> m_Citizens;

        [ReadOnly]
        public ComponentLookup<CrimeProducer> m_Crimes;

        [ReadOnly]
        public ComponentLookup<Transform> m_Transforms;

        [ReadOnly]
        public ComponentLookup<Locked> m_Lockeds;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public ComponentLookup<HealthProblem> m_HealthProblems;

        [ReadOnly]
        public ComponentLookup<Game.Buildings.Park> m_Parks;

        [ReadOnly]
        public ComponentLookup<Abandoned> m_Abandoneds;

        [ReadOnly]
        public BufferLookup<OwnedVehicle> m_OwnedVehicles;

        [ReadOnly]
        public ComponentLookup<ElectricityConsumer> m_ElectricityConsumers;

        [ReadOnly]
        public ComponentLookup<WaterConsumer> m_WaterConsumers;

        [ReadOnly]
        public ComponentLookup<GarbageProducer> m_GarbageProducers;

        [ReadOnly]
        public ComponentLookup<MailProducer> m_MailProducers;

        [ReadOnly]
        public ComponentLookup<Household> m_Households;

        [ReadOnly]
        public ComponentLookup<CurrentBuilding> m_CurrentBuildings;

        [ReadOnly]
        public ComponentLookup<CurrentTransport> m_CurrentTransports;

        [ReadOnly]
        public ComponentLookup<PathInformation> m_PathInformations;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_CitizenBuffers;

        public ComponentLookup<PropertySeeker> m_PropertySeekers;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoiseMap;

        [ReadOnly]
        public CellMapData<TelecomCoverage> m_TelecomCoverages;

        [ReadOnly]
        public NativeArray<int> m_TaxRates;

        [ReadOnly]
        public CountResidentialPropertySystem.ResidentialPropertyData m_ResidentialPropertyData;

        [ReadOnly]
        public HealthcareParameterData m_HealthcareParameters;

        [ReadOnly]
        public ParkParameterData m_ParkParameters;

        [ReadOnly]
        public EducationParameterData m_EducationParameters;

        [ReadOnly]
        public TelecomParameterData m_TelecomParameters;

        [ReadOnly]
        public GarbageParameterData m_GarbageParameters;

        [ReadOnly]
        public PoliceConfigurationData m_PoliceParameters;

        [ReadOnly]
        public CitizenHappinessParameterData m_CitizenHappinessParameterData;

        [ReadOnly]
        public EconomyParameterData m_EconomyParameters;

        [ReadOnly]
        public uint m_SimulationFrame;

        public EntityCommandBuffer m_CommandBuffer;

        [ReadOnly]
        public Entity m_City;

        public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

        public NativeQueue<RentAction>.ParallelWriter m_RentActionQueue;

        private void StartHomeFinding(Entity household, Entity commuteCitizen, Entity targetLocation, Entity oldHome, float minimumScore, bool targetIsOrigin, DynamicBuffer<HouseholdCitizen> citizens)
        {
            this.m_CommandBuffer.AddComponent(household, new PathInformation
            {
                m_State = PathFlags.Pending
            });
            Household household2 = this.m_Households[household];
            PathfindWeights weights = default(PathfindWeights);
            if (this.m_Citizens.TryGetComponent(commuteCitizen, out var componentData))
            {
                weights = CitizenUtils.GetPathfindWeights(componentData, household2, citizens.Length);
            }
            else
            {
                for (int i = 0; i < citizens.Length; i++)
                {
                    weights.m_Value += CitizenUtils.GetPathfindWeights(componentData, household2, citizens.Length).m_Value;
                }
                weights.m_Value *= 1f / (float)citizens.Length;
            }
            PathfindParameters parameters = new PathfindParameters
            {
                m_MaxSpeed = 111.111115f,
                m_WalkSpeed = 1.6666667f,
                m_Weights = weights,
                m_Methods = (PathMethod.Pedestrian | PathMethod.PublicTransportDay),
                m_MaxCost = CitizenBehaviorSystem.kMaxPathfindCost,
                m_PathfindFlags = (PathfindFlags.Simplified | PathfindFlags.IgnorePath)
            };
            SetupQueueTarget a = new SetupQueueTarget
            {
                m_Type = SetupTargetType.CurrentLocation,
                m_Methods = PathMethod.Pedestrian,
                m_Entity = targetLocation
            };
            SetupQueueTarget b = new SetupQueueTarget
            {
                m_Type = SetupTargetType.FindHome,
                m_Methods = PathMethod.Pedestrian,
                m_Entity = household,
                m_Entity2 = oldHome,
                m_Value2 = minimumScore
            };
            if (this.m_OwnedVehicles.TryGetBuffer(household, out var bufferData) && bufferData.Length != 0)
            {
                parameters.m_Methods |= (PathMethod)(targetIsOrigin ? 8194 : 8198);
                parameters.m_ParkingSize = float.MinValue;
                parameters.m_IgnoredRules |= RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic | RuleFlags.ForbidSlowTraffic;
                a.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                a.m_RoadTypes |= RoadTypes.Car;
                b.m_Methods |= PathMethod.Road | PathMethod.MediumRoad;
                b.m_RoadTypes |= RoadTypes.Car;
            }
            if (targetIsOrigin)
            {
                parameters.m_MaxSpeed.y = 277.77777f;
                parameters.m_Methods |= PathMethod.Taxi | PathMethod.PublicTransportNight;
                parameters.m_SecondaryIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults();
            }
            else
            {
                CommonUtils.Swap(ref a, ref b);
            }

            // 待测试mod
            // parameters.m_MaxResultCount = 10;
            parameters.m_MaxResultCount = 10;
            // 设置正在寻找住处的实体所能搜寻工作/上学地点周边的空闲住宅数量，原始设定过低导致大量排队时卡住;

            parameters.m_PathfindFlags |= (PathfindFlags)(targetIsOrigin ? 256 : 128);
            this.m_CommandBuffer.AddBuffer<PathInformations>(household).Add(new PathInformations
            {
                m_State = PathFlags.Pending
            });
            SetupQueueItem value = new SetupQueueItem(household, parameters, a, b);
            this.m_PathfindQueue.Enqueue(value);
        }

        private Entity GetFirstWorkplaceOrSchool(DynamicBuffer<HouseholdCitizen> citizens, ref Entity citizen)
        {
            for (int i = 0; i < citizens.Length; i++)
            {
                citizen = citizens[i].m_Citizen;
                if (this.m_Workers.HasComponent(citizen))
                {
                    return this.m_Workers[citizen].m_Workplace;
                }
                if (this.m_Students.HasComponent(citizen))
                {
                    return this.m_Students[citizen].m_School;
                }
            }
            return Entity.Null;
        }

        private Entity GetCurrentLocation(DynamicBuffer<HouseholdCitizen> citizens)
        {
            for (int i = 0; i < citizens.Length; i++)
            {
                if (this.m_CurrentBuildings.TryGetComponent(citizens[i].m_Citizen, out var componentData))
                {
                    return componentData.m_CurrentBuilding;
                }
                if (this.m_CurrentTransports.TryGetComponent(citizens[i].m_Citizen, out var componentData2))
                {
                    return componentData2.m_CurrentTransport;
                }
            }
            return Entity.Null;
        }

        public void Execute()
        {
            int num = 0;
            for (int i = 0; i < this.m_HomelessHouseholdEntities.Length; i++)
            {
                Entity householdEntity = this.m_HomelessHouseholdEntities[i];
                if (this.ProcessFindHome(householdEntity))
                {
                    num++;
                }
                if (num >= HouseholdFindPropertySystem.kMaxProcessEntitiesPerUpdate / 2)
                {
                    break;
                }
            }
            for (int j = 0; j < this.m_MovedInHouseholdEntities.Length; j++)
            {
                Entity householdEntity2 = this.m_MovedInHouseholdEntities[j];
                if (this.ProcessFindHome(householdEntity2))
                {
                    num++;
                }
                if (num >= HouseholdFindPropertySystem.kMaxProcessEntitiesPerUpdate)
                {
                    break;
                }
            }
        }

        private bool ProcessFindHome(Entity householdEntity)
        {
            DynamicBuffer<HouseholdCitizen> dynamicBuffer = this.m_CitizenBuffers[householdEntity];
            if (dynamicBuffer.Length == 0)
            {
                return false;
            }
            PropertySeeker propertySeeker = this.m_PropertySeekers[householdEntity];
            int householdIncome = EconomyUtils.GetHouseholdIncome(dynamicBuffer, ref this.m_Workers, ref this.m_Citizens, ref this.m_HealthProblems, ref this.m_EconomyParameters, this.m_TaxRates);
            if (this.m_PathInformationBuffers.TryGetBuffer(householdEntity, out var bufferData))
            {
                this.ProcessPathInformations(householdEntity, bufferData, propertySeeker, dynamicBuffer, householdIncome);
                return false;
            }
            Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity, ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);

            // mod
            float bestPropertyScore = ((householdHomeBuilding != Entity.Null) ? PropertyUtilsRe.GetPropertyScore(householdHomeBuilding, householdEntity, dynamicBuffer, ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings, ref this.m_BuildingDatas, ref this.m_Households, ref this.m_Citizens, ref this.m_Students, ref this.m_Workers, ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages, ref this.m_Lockeds, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers, ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms, ref this.m_Abandoneds, ref this.m_Parks, ref this.m_Availabilities, this.m_TaxRates, this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap, this.m_TelecomCoverages, this.m_CityModifiers[this.m_City], this.m_HealthcareParameters.m_HealthcareServicePrefab, this.m_ParkParameters.m_ParkServicePrefab, this.m_EducationParameters.m_EducationServicePrefab, this.m_TelecomParameters.m_TelecomServicePrefab, this.m_GarbageParameters.m_GarbageServicePrefab, this.m_PoliceParameters.m_PoliceServicePrefab, this.m_CitizenHappinessParameterData, this.m_GarbageParameters) : float.NegativeInfinity);
            if (householdHomeBuilding == Entity.Null && propertySeeker.m_LastPropertySeekFrame + HouseholdFindPropertySystem.kFindPropertyCoolDown > this.m_SimulationFrame)
            {
                if (this.m_PathInformations[householdEntity].m_State != PathFlags.Pending && math.csum(this.m_ResidentialPropertyData.m_FreeProperties) < 10)
                {
                    CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                }
                return false;
            }
            Entity citizen = Entity.Null;
            Entity firstWorkplaceOrSchool = this.GetFirstWorkplaceOrSchool(dynamicBuffer, ref citizen);
            bool flag = firstWorkplaceOrSchool == Entity.Null;
            Entity entity = (flag ? this.GetCurrentLocation(dynamicBuffer) : firstWorkplaceOrSchool);
            if (householdHomeBuilding == Entity.Null && entity == Entity.Null)
            {
                CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
                return false;
            }
            propertySeeker.m_TargetProperty = firstWorkplaceOrSchool;
            propertySeeker.m_BestProperty = householdHomeBuilding;
            propertySeeker.m_BestPropertyScore = bestPropertyScore;
            propertySeeker.m_LastPropertySeekFrame = this.m_SimulationFrame;
            this.m_PropertySeekers[householdEntity] = propertySeeker;
            this.StartHomeFinding(householdEntity, citizen, entity, householdHomeBuilding, propertySeeker.m_BestPropertyScore, flag, dynamicBuffer);
            return true;
        }

        private void ProcessPathInformations(Entity householdEntity, DynamicBuffer<PathInformations> pathInformations, PropertySeeker propertySeeker, DynamicBuffer<HouseholdCitizen> citizens, int income)
        {
            int num = 0;
            PathInformations pathInformations2 = pathInformations[num];
            if ((pathInformations2.m_State & PathFlags.Pending) != 0)
            {
                return;
            }
            this.m_CommandBuffer.RemoveComponent<PathInformations>(householdEntity);
            bool flag = propertySeeker.m_TargetProperty != Entity.Null;
            Entity entity = (flag ? pathInformations2.m_Origin : pathInformations2.m_Destination);
            bool flag2 = false;
            while (!this.m_CachedPropertyInfo.ContainsKey(entity) || this.m_CachedPropertyInfo[entity].free <= 0)
            {
                num++;
                if (pathInformations.Length > num)
                {
                    pathInformations2 = pathInformations[num];
                    entity = (flag ? pathInformations2.m_Origin : pathInformations2.m_Destination);
                    continue;
                }
                entity = Entity.Null;
                flag2 = true;
                break;
            }
            if (flag2 && pathInformations.Length != 0 && pathInformations[0].m_Destination != Entity.Null)
            {
                return;
            }
            float num2 = float.NegativeInfinity;
            if (entity != Entity.Null && this.m_CachedPropertyInfo.ContainsKey(entity) && this.m_CachedPropertyInfo[entity].free > 0)
            {
                num2 = PropertyUtilsRe.GetPropertyScore(entity, householdEntity, citizens, ref this.m_PrefabRefs, ref this.m_BuildingProperties, ref this.m_Buildings, ref this.m_BuildingDatas, ref this.m_Households, ref this.m_Citizens, ref this.m_Students, ref this.m_Workers, ref this.m_SpawnableDatas, ref this.m_Crimes, ref this.m_ServiceCoverages, ref this.m_Lockeds, ref this.m_ElectricityConsumers, ref this.m_WaterConsumers, ref this.m_GarbageProducers, ref this.m_MailProducers, ref this.m_Transforms, ref this.m_Abandoneds, ref this.m_Parks, ref this.m_Availabilities, this.m_TaxRates, this.m_PollutionMap, this.m_AirPollutionMap, this.m_NoiseMap, this.m_TelecomCoverages, this.m_CityModifiers[this.m_City], this.m_HealthcareParameters.m_HealthcareServicePrefab, this.m_ParkParameters.m_ParkServicePrefab, this.m_EducationParameters.m_EducationServicePrefab, this.m_TelecomParameters.m_TelecomServicePrefab, this.m_GarbageParameters.m_GarbageServicePrefab, this.m_PoliceParameters.m_PoliceServicePrefab, this.m_CitizenHappinessParameterData, this.m_GarbageParameters);
            }
            if (num2 < propertySeeker.m_BestPropertyScore)
            {
                entity = propertySeeker.m_BestProperty;
            }
            bool flag3 = (this.m_Households[householdEntity].m_Flags & HouseholdFlags.MovedIn) != 0;
            bool flag4 = entity != Entity.Null && BuildingUtils.IsHomelessShelterBuilding(entity, ref this.m_Parks, ref this.m_Abandoneds);
            bool flag5 = CitizenUtils.IsHouseholdNeedSupport(citizens, ref this.m_Citizens, ref this.m_Students);
            bool flag6 = this.m_PropertiesOnMarket.HasComponent(entity) && (flag5 || this.m_PropertiesOnMarket[entity].m_AskingRent < income);
            bool flag7 = !this.m_PropertyRenters.HasComponent(householdEntity) || !this.m_PropertyRenters[householdEntity].m_Property.Equals(entity);
            Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(householdEntity, ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);
            if (householdHomeBuilding != Entity.Null && householdHomeBuilding == entity)
            {
                if (!this.m_HomelessHouseholds.HasComponent(householdEntity) && !flag5 && income < this.m_PropertyRenters[householdEntity].m_Rent)
                {
                    CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity, MoveAwayReason.NoMoney);
                }
                else
                {
                    this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
                }
            }
            else if ((flag6 && flag7) || (flag3 && flag4))
            {
                this.m_RentActionQueue.Enqueue(new RentAction
                {
                    m_Property = entity,
                    m_Renter = householdEntity
                });
                if (this.m_CachedPropertyInfo.ContainsKey(entity))
                {
                    CachedPropertyInformation value = this.m_CachedPropertyInfo[entity];
                    value.free--;
                    this.m_CachedPropertyInfo[entity] = value;
                }
                this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(householdEntity, value: false);
            }
            else if (entity == Entity.Null && (!this.m_HomelessHouseholds.HasComponent(householdEntity) || this.m_HomelessHouseholds[householdEntity].m_TempHome == Entity.Null))
            {
                CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, householdEntity);
            }
            else
            {
                propertySeeker.m_BestProperty = default(Entity);
                propertySeeker.m_BestPropertyScore = float.NegativeInfinity;
                this.m_PropertySeekers[householdEntity] = propertySeeker;
            }
        }
    }

}
