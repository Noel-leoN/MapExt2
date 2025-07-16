// Game.Simulation.RentAdjustSystem
// OnUpdate

using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Notifications;
using Game.Objects;
using Game.Prefabs;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Game.Simulation;

namespace MapExtPDX.MapExt.ReBurstSystemModeB
{

    [BurstCompile]
    public struct AdjustRentJob : IJobChunk
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        public BufferTypeHandle<Renter> m_RenterType;

        [ReadOnly]
        public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<PropertyRenter> m_PropertyRenters;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<Building> m_Buildings;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_Prefabs;

        [ReadOnly]
        public ComponentLookup<BuildingPropertyData> m_BuildingProperties;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingDatas;

        [ReadOnly]
        public ComponentLookup<WorkProvider> m_WorkProviders;

        [ReadOnly]
        public ComponentLookup<WorkplaceData> m_WorkplaceDatas;

        [ReadOnly]
        public ComponentLookup<CompanyNotifications> m_CompanyNotifications;

        [ReadOnly]
        public ComponentLookup<Attached> m_Attached;

        [ReadOnly]
        public ComponentLookup<Game.Areas.Lot> m_Lots;

        [ReadOnly]
        public ComponentLookup<Geometry> m_Geometries;

        [ReadOnly]
        public ComponentLookup<LandValue> m_LandValues;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<PropertyOnMarket> m_OnMarkets;

        [ReadOnly]
        public BufferLookup<HouseholdCitizen> m_HouseholdCitizenBufs;

        [ReadOnly]
        public BufferLookup<Game.Areas.SubArea> m_SubAreas;

        [ReadOnly]
        public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;

        [ReadOnly]
        public ComponentLookup<Abandoned> m_Abandoned;

        [ReadOnly]
        public ComponentLookup<Destroyed> m_Destroyed;

        [ReadOnly]
        public ComponentLookup<Game.Objects.Transform> m_Transforms;

        [ReadOnly]
        public BufferLookup<CityModifier> m_CityModifiers;

        [ReadOnly]
        public ComponentLookup<HealthProblem> m_HealthProblems;

        [ReadOnly]
        public ComponentLookup<Worker> m_Workers;

        [ReadOnly]
        public ComponentLookup<Citizen> m_Citizens;

        [ReadOnly]
        public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingData;

        [ReadOnly]
        public ComponentLookup<ZoneData> m_ZoneData;

        [ReadOnly]
        public BufferLookup<Game.Economy.Resources> m_ResourcesBuf;

        [ReadOnly]
        public ComponentLookup<ExtractorProperty> m_ExtractorProperties;

        [ReadOnly]
        public ComponentLookup<IndustrialProcessData> m_ProcessDatas;

        [ReadOnly]
        public BufferLookup<OwnedVehicle> m_OwnedVehicles;

        [ReadOnly]
        public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;

        [ReadOnly]
        public BufferLookup<LayoutElement> m_LayoutElements;

        [ReadOnly]
        public ResourcePrefabs m_ResourcePrefabs;

        [ReadOnly]
        public ComponentLookup<ResourceData> m_ResourceDatas;

        [ReadOnly]
        public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<BuildingNotifications> m_BuildingNotifications;

        [ReadOnly]
        public NativeArray<int> m_TaxRates;

        [ReadOnly]
        public NativeArray<AirPollution> m_AirPollutionMap;

        [ReadOnly]
        public NativeArray<GroundPollution> m_PollutionMap;

        [ReadOnly]
        public NativeArray<NoisePollution> m_NoiseMap;

        public CitizenHappinessParameterData m_CitizenHappinessParameterData;

        public BuildingConfigurationData m_BuildingConfigurationData;

        public PollutionParameterData m_PollutionParameters;

        public IconCommandBuffer m_IconCommandBuffer;

        public uint m_UpdateFrameIndex;

        [ReadOnly]
        public Entity m_City;

        public EconomyParameterData m_EconomyParameterData;

        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        private bool CanDisplayHighRentWarnIcon(DynamicBuffer<Renter> renters)
        {
            bool result = true;
            for (int i = 0; i < renters.Length; i++)
            {
                Entity renter = renters[i].m_Renter;
                if (this.m_CompanyNotifications.HasComponent(renter))
                {
                    CompanyNotifications companyNotifications = this.m_CompanyNotifications[renter];
                    if (companyNotifications.m_NoCustomersEntity != Entity.Null || companyNotifications.m_NoInputEntity != Entity.Null)
                    {
                        result = false;
                        break;
                    }
                }
                if (this.m_WorkProviders.HasComponent(renter))
                {
                    WorkProvider workProvider = this.m_WorkProviders[renter];
                    if (workProvider.m_EducatedNotificationEntity != Entity.Null || workProvider.m_UneducatedNotificationEntity != Entity.Null)
                    {
                        result = false;
                        break;
                    }
                }
                if (!this.m_HouseholdCitizenBufs.HasBuffer(renter))
                {
                    continue;
                }
                DynamicBuffer<HouseholdCitizen> dynamicBuffer = this.m_HouseholdCitizenBufs[renter];
                result = false;
                for (int j = 0; j < dynamicBuffer.Length; j++)
                {
                    if (!CitizenUtils.IsDead(dynamicBuffer[j].m_Citizen, ref this.m_HealthProblems))
                    {
                        result = true;
                        break;
                    }
                }
            }
            return result;
        }

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (chunk.GetSharedComponent(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
            {
                return;
            }
            NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
            BufferAccessor<Renter> bufferAccessor = chunk.GetBufferAccessor(ref this.m_RenterType);
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];
            for (int i = 0; i < nativeArray.Length; i++)
            {
                Entity entity = nativeArray[i];
                Entity prefab = this.m_Prefabs[entity].m_Prefab;
                if (!this.m_BuildingProperties.HasComponent(prefab))
                {
                    continue;
                }
                BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[prefab];
                Building value = this.m_Buildings[entity];
                DynamicBuffer<Renter> renters = bufferAccessor[i];
                BuildingData buildingData = this.m_BuildingDatas[prefab];
                int lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;
                float landValueBase = 0f;
                if (this.m_LandValues.HasComponent(value.m_RoadEdge))
                {
                    landValueBase = this.m_LandValues[value.m_RoadEdge].m_LandValue;
                }
                Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
                int buildingLevel = PropertyUtils.GetBuildingLevel(prefab, this.m_SpawnableBuildingData);
                bool ignoreLandValue = false;
                if (this.m_SpawnableBuildingData.HasComponent(prefab))
                {
                    SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingData[prefab];
                    areaType = this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_AreaType;
                    if (this.m_ZonePropertiesDatas.TryGetComponent(spawnableBuildingData.m_ZonePrefab, out var componentData))
                    {
                        ignoreLandValue = componentData.m_IgnoreLandValue;
                    }
                }
                this.ProcessPollutionNotification(areaType, entity, cityModifiers);
                int rentPricePerRenter = PropertyUtils.GetRentPricePerRenter(buildingPropertyData, buildingLevel, lotSize, landValueBase, areaType, ref this.m_EconomyParameterData, ignoreLandValue);
                if (this.m_OnMarkets.HasComponent(entity))
                {
                    PropertyOnMarket value2 = this.m_OnMarkets[entity];
                    value2.m_AskingRent = rentPricePerRenter;
                    this.m_OnMarkets[entity] = value2;
                }
                int num = buildingPropertyData.CountProperties();
                bool flag = false;
                int2 @int = default(int2);
                bool flag2 = this.m_ExtractorProperties.HasComponent(entity);
                for (int num2 = renters.Length - 1; num2 >= 0; num2--)
                {
                    Entity renter = renters[num2].m_Renter;
                    if (this.m_PropertyRenters.HasComponent(renter))
                    {
                        PropertyRenter value3 = this.m_PropertyRenters[renter];
                        if (!this.m_ResourcesBuf.HasBuffer(renter))
                        {
                            UnityEngine.Debug.Log($"no resources:{renter.Index}");
                            continue;
                        }
                        int num3 = 0;
                        if (this.m_HouseholdCitizenBufs.HasBuffer(renter))
                        {
                            num3 = EconomyUtils.GetHouseholdIncome(this.m_HouseholdCitizenBufs[renter], ref this.m_Workers, ref this.m_Citizens, ref this.m_HealthProblems, ref this.m_EconomyParameterData, this.m_TaxRates) + math.max(0, EconomyUtils.GetResources(Resource.Money, this.m_ResourcesBuf[renter]));
                        }
                        else
                        {
                            Entity prefab2 = this.m_Prefabs[renter].m_Prefab;
                            if (!this.m_ProcessDatas.HasComponent(prefab2) || !this.m_WorkProviders.HasComponent(renter) || !this.m_WorkplaceDatas.HasComponent(prefab2))
                            {
                                continue;
                            }
                            int companyMaxProfitPerDay = EconomyUtils.GetCompanyMaxProfitPerDay(this.m_WorkProviders[renter], areaType == Game.Zones.AreaType.Industrial, buildingLevel, this.m_ProcessDatas[prefab2], this.m_ResourcePrefabs, this.m_WorkplaceDatas[prefab2], ref this.m_ResourceDatas, ref this.m_EconomyParameterData);
                            num3 = ((companyMaxProfitPerDay >= num3) ? companyMaxProfitPerDay : ((!this.m_OwnedVehicles.HasBuffer(renter)) ? EconomyUtils.GetCompanyTotalWorth(this.m_ResourcesBuf[renter], this.m_ResourcePrefabs, ref this.m_ResourceDatas) : EconomyUtils.GetCompanyTotalWorth(this.m_ResourcesBuf[renter], this.m_OwnedVehicles[renter], ref this.m_LayoutElements, ref this.m_DeliveryTrucks, this.m_ResourcePrefabs, ref this.m_ResourceDatas)));
                        }
                        value3.m_Rent = rentPricePerRenter;
                        this.m_PropertyRenters[renter] = value3;
                        if (rentPricePerRenter > num3)
                        {
                            this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, renter, value: true);
                        }
                        @int.y++;
                        if (rentPricePerRenter > num3)
                        {
                            @int.x++;
                        }
                    }
                    else
                    {
                        renters.RemoveAt(num2);
                        flag = true;
                    }
                }
                if (!((float)@int.x / math.max(1f, @int.y) > 0.7f) || !this.CanDisplayHighRentWarnIcon(renters))
                {
                    this.m_IconCommandBuffer.Remove(entity, this.m_BuildingConfigurationData.m_HighRentNotification);
                    value.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[entity] = value;
                }
                else if (renters.Length > 0 && !flag2 && num > renters.Length && (value.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) == 0)
                {
                    this.m_IconCommandBuffer.Add(entity, this.m_BuildingConfigurationData.m_HighRentNotification, IconPriority.Problem);
                    value.m_Flags |= Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[entity] = value;
                }
                if (renters.Length > num && this.m_PropertyRenters.HasComponent(renters[renters.Length - 1].m_Renter))
                {
                    this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex, renters[renters.Length - 1].m_Renter);
                    renters.RemoveAt(renters.Length - 1);
                    UnityEngine.Debug.LogWarning($"Removed extra renter from building:{entity.Index}");
                }
                if (renters.Length == 0 && (value.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) != Game.Buildings.BuildingFlags.None)
                {
                    this.m_IconCommandBuffer.Remove(entity, this.m_BuildingConfigurationData.m_HighRentNotification);
                    value.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[entity] = value;
                }
                if (this.m_Prefabs.HasComponent(entity) && !this.m_Abandoned.HasComponent(entity) && !this.m_Destroyed.HasComponent(entity) && flag && num > renters.Length)
                {
                    this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, entity, new PropertyOnMarket
                    {
                        m_AskingRent = rentPricePerRenter
                    });
                }
            }
        }

        private void ProcessPollutionNotification(Game.Zones.AreaType areaType, Entity buildingEntity, DynamicBuffer<CityModifier> cityModifiers)
        {
            if (areaType == Game.Zones.AreaType.Residential)
            {
                int2 groundPollutionBonuses = CitizenHappinessJob.GetGroundPollutionBonuses(buildingEntity, ref this.m_Transforms, this.m_PollutionMap, cityModifiers, in this.m_CitizenHappinessParameterData);
                int2 noiseBonuses = CitizenHappinessJob.GetNoiseBonuses(buildingEntity, ref this.m_Transforms, this.m_NoiseMap, in this.m_CitizenHappinessParameterData);
                int2 airPollutionBonuses = CitizenHappinessJob.GetAirPollutionBonuses(buildingEntity, ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers, in this.m_CitizenHappinessParameterData);
                bool flag = groundPollutionBonuses.x + groundPollutionBonuses.y < 2 * this.m_PollutionParameters.m_GroundPollutionNotificationLimit;
                bool flag2 = airPollutionBonuses.x + airPollutionBonuses.y < 2 * this.m_PollutionParameters.m_AirPollutionNotificationLimit;
                bool flag3 = noiseBonuses.x + noiseBonuses.y < 2 * this.m_PollutionParameters.m_NoisePollutionNotificationLimit;
                BuildingNotifications value = this.m_BuildingNotifications[buildingEntity];
                if (flag && !value.HasNotification(BuildingNotification.GroundPollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_GroundPollutionNotification, IconPriority.Problem);
                    value.m_Notifications |= BuildingNotification.GroundPollution;
                    this.m_BuildingNotifications[buildingEntity] = value;
                }
                else if (!flag && value.HasNotification(BuildingNotification.GroundPollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_PollutionParameters.m_GroundPollutionNotification);
                    value.m_Notifications &= ~BuildingNotification.GroundPollution;
                    this.m_BuildingNotifications[buildingEntity] = value;
                }
                if (flag2 && !value.HasNotification(BuildingNotification.AirPollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_AirPollutionNotification, IconPriority.Problem);
                    value.m_Notifications |= BuildingNotification.AirPollution;
                    this.m_BuildingNotifications[buildingEntity] = value;
                }
                else if (!flag2 && value.HasNotification(BuildingNotification.AirPollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_PollutionParameters.m_AirPollutionNotification);
                    value.m_Notifications &= ~BuildingNotification.AirPollution;
                    this.m_BuildingNotifications[buildingEntity] = value;
                }
                if (flag3 && !value.HasNotification(BuildingNotification.NoisePollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_NoisePollutionNotification, IconPriority.Problem);
                    value.m_Notifications |= BuildingNotification.NoisePollution;
                    this.m_BuildingNotifications[buildingEntity] = value;
                }
                else if (!flag3 && value.HasNotification(BuildingNotification.NoisePollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_PollutionParameters.m_NoisePollutionNotification);
                    value.m_Notifications &= ~BuildingNotification.NoisePollution;
                    this.m_BuildingNotifications[buildingEntity] = value;
                }
            }
        }

        void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }

}
