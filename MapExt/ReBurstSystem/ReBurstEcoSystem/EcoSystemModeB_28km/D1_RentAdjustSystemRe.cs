// Game.Simulation.RentAdjustSystem
// 独立系统，可考虑ECS替换

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
using Game.Simulation;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.MapExt.ReBurstEcoSystemModeB
{
    // 修正1：原计算将收入+全部储蓄作为支付能力，收入为零但储蓄较多的家庭会显得富有并且能够负担得起高昂的租金。但实际上0收入的家庭不会将所有储蓄全部用于支付高额租金，原计算将向他们收取高昂的租金，将导致迅速耗尽积蓄后被迫搬出去。参照现实通行规律，国际标准建议住房支出占收入 ≤30%,住房支出中，储蓄贡献不超过 10%–15% 的总储蓄; 现改为收入的30%+储蓄的1/10;
    // 修正2 ：原逻辑为(v1.3.6f版本改动)如果一个家庭目前的租金低于其经济能力的一半 ，他们就会寻求新房产。其目的是模拟中产阶级化——富裕的公民搬到更昂贵的住房。然而，由于 renterUpkeepCapacity 包括他们所有的储蓄（参见修正 #1），一个刚刚获得大笔遗产或勤奋储蓄的家庭将被标记为“太富有”，即使他们的收入没有改变，也将被迫寻找新的住房。这可能导致不必要且可能不受欢迎的居民洗牌。此外，原逻辑为一旦付不起租金或能够改善时立即开始找房，与现实中大多数人会尝试通过其他方式（如增加工作时间、削减开支等）来维持现有住房的行为不符。现改为：仅当租金过高（付不起）时，家庭才会寻求新房；当租金过低（低于30%）时，家庭有30%的概率寻求升级住房。并且引入检查周期，避免每帧都检查导致的性能问题和频繁搬家。
    // 修正3：原逻辑为仅当建筑物空置时才显示高租金警告，一旦满员就不会显示，这与现实情况不符。可能有意设计为高租金阻止新租户入住的提醒，但这种信息并不完整。现改为当建筑物无论是否有空置房源，只要大部分租户负担不起租金时就显示高租金警告。
    // 修正4 ：调试代码删除

    [BurstCompile]
    public struct AdjustRentJob : IJobChunk
    {
        // ====== 数据访问字段 (Data Accessor Fields) ======
        [ReadOnly] public EntityTypeHandle m_EntityType;
        public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly] public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

        // 可写组件查找 (Writable Lookups)
        [NativeDisableParallelForRestriction] public ComponentLookup<PropertyRenter> m_PropertyRenters;
        [NativeDisableParallelForRestriction] public ComponentLookup<Building> m_Buildings;
        [NativeDisableParallelForRestriction] public ComponentLookup<PropertyOnMarket> m_OnMarkets;
        [NativeDisableParallelForRestriction] public ComponentLookup<BuildingNotifications> m_BuildingNotifications;

        // 只读组件查找 (ReadOnly Lookups)
        [ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingProperties;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas; // Data from prefab: lot size, etc.
        [ReadOnly] public ComponentLookup<WorkProvider> m_WorkProviders; // Data for commercial/industrial buildings that provide jobs
        [ReadOnly] public ComponentLookup<WorkplaceData> m_WorkplaceDatas; // Data from prefab: workplace complexity, etc.
        [ReadOnly] public ComponentLookup<CompanyNotifications> m_CompanyNotifications; // Notifications specific to companies
        [ReadOnly] public ComponentLookup<Attached> m_Attached; // Component indicating an entity is attached to another (e.g., building to a lot)
        [ReadOnly] public ComponentLookup<Game.Areas.Lot> m_Lots; // Lot data
        [ReadOnly] public ComponentLookup<Geometry> m_Geometries; // Geometric data
        [ReadOnly] public ComponentLookup<LandValue> m_LandValues; // Land value data, often on road segments
        [ReadOnly] public BufferLookup<HouseholdCitizen> m_HouseholdCitizenBufs; // Buffer of citizens belonging to a household
        [ReadOnly] public BufferLookup<Game.Areas.SubArea> m_SubAreas; // Sub-area data
        [ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgrades; // Upgrades installed in a building
        [ReadOnly] public ComponentLookup<Abandoned> m_Abandoned; // Tag component for abandoned buildings
        [ReadOnly] public ComponentLookup<Destroyed> m_Destroyed; // Tag component for destroyed buildings
        [ReadOnly] public ComponentLookup<Transform> m_Transforms; // Position and rotation data
        [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers; // City-wide economic modifiers
        [ReadOnly] public ComponentLookup<HealthProblem> m_HealthProblems; // Citizen health data
        [ReadOnly] public ComponentLookup<Worker> m_Workers; // Worker component on citizens
        [ReadOnly] public ComponentLookup<Citizen> m_Citizens; // Citizen data (age, education, etc.)
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingData; // Data for zoned (growable) buildings
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneData; // Data about the zone type
        [ReadOnly] public BufferLookup<Resources> m_ResourcesBuf; // Buffer of resources (money, materials) for a household or company
        [ReadOnly] public ComponentLookup<ExtractorProperty> m_ExtractorProperties; // Component for resource extractors (e.g., oil pumps)
        [ReadOnly] public ComponentLookup<IndustrialProcessData> m_ProcessDatas; // Data from prefab: industrial production recipes
        [ReadOnly] public BufferLookup<OwnedVehicle> m_OwnedVehicles; // Vehicles owned by a company/household
        [ReadOnly] public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks; // Data specific to delivery trucks
        [ReadOnly] public BufferLookup<LayoutElement> m_LayoutElements; // Data for vehicle layouts
        [ReadOnly] public ResourcePrefabs m_ResourcePrefabs; // A collection of resource prefabs
        [ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas; // Data from resource prefabs
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas; // Data from zone prefabs
        // v1.4.2新增：服务可用性组件查询，用于检查建筑是否缺乏必要的服务（如水、电、垃圾处理等）。
        [ReadOnly] public ComponentLookup<ServiceAvailable> m_ServiceAvailables;
        // v1.4.2新增： 查询正在建设中的建筑组件
        [ReadOnly] public ComponentLookup<UnderConstruction> m_UnderConstructions;

        // 配置数据
        [ReadOnly] public NativeArray<int> m_TaxRates; // City-wide tax rates
        [ReadOnly] public NativeArray<AirPollution> m_AirPollutionMap; // Pollution map data
        [ReadOnly] public NativeArray<GroundPollution> m_PollutionMap;
        [ReadOnly] public NativeArray<NoisePollution> m_NoiseMap;

        public CitizenHappinessParameterData m_CitizenHappinessParameterData;
        public BuildingConfigurationData m_BuildingConfigurationData;
        public PollutionParameterData m_PollutionParameters;
        public ServiceFeeParameterData m_FeeParameters;
        public EconomyParameterData m_EconomyParameterData;

        // 命令缓冲区
        public IconCommandBuffer m_IconCommandBuffer; // Used to add/remove notification icons
        public EntityCommandBuffer.ParallelWriter m_CommandBuffer; // Used for adding/removing components

        // 运行时参数
        public uint m_UpdateFrameIndex; // The current frame index for staggered updates
        [ReadOnly] public Entity m_City; // The entity representing the city settings

        /// <summary>
        /// 判断是否应该显示“高租金”警告图标。
        /// 避免在有更严重问题（如无客户、无员工、全员死亡）时显示租金图标，减少视觉干扰。
        /// </summary>
        private bool CanDisplayHighRentWarnIcon(DynamicBuffer<Renter> renters)
        {
            // 默认显示，除非发现更高优先级的严重问题
            bool canDisplay = true;

            for (int i = 0; i < renters.Length; i++)
            {
                Entity renter = renters[i].m_Renter;

                // 1. 检查公司是否有更严重的问题（如：没有客户、没有原材料）
                if (this.m_CompanyNotifications.HasComponent(renter))
                {
                    CompanyNotifications companyNotifications = this.m_CompanyNotifications[renter];
                    // 如果存在"无客户"或"无输入"的问题，优先显示那些图标，不显示高租金图标
                    if (companyNotifications.m_NoCustomersEntity != Entity.Null || companyNotifications.m_NoInputEntity != Entity.Null)
                    {
                        canDisplay = false;
                        break;
                    }
                }

                // 2. 检查是否有员工教育水平不匹配的问题
                if (this.m_WorkProviders.HasComponent(renter))
                {
                    WorkProvider workProvider = this.m_WorkProviders[renter];
                    if (workProvider.m_EducatedNotificationEntity != Entity.Null || workProvider.m_UneducatedNotificationEntity != Entity.Null)
                    {
                        canDisplay = false; // A more critical issue exists.
                        break;
                    }
                }

                // 3. 家庭检查：确保至少有一个家庭成员是活着的
                if (!this.m_HouseholdCitizenBufs.HasBuffer(renter))
                {
                    continue;
                }
                DynamicBuffer<HouseholdCitizen> householdCitizens = this.m_HouseholdCitizenBufs[renter];
                canDisplay = false; // 先假设全员ga，不能显示租金警告
                for (int j = 0; j < householdCitizens.Length; j++)
                {
                    if (!CitizenUtils.IsDead(householdCitizens[j].m_Citizen, ref this.m_HealthProblems))
                    {
                        canDisplay = true;  // 只要有一个家庭成员生存，就可能需要显示租金警告
                        break;
                    }
                }
            }
            return canDisplay;
        }

        // =========================================================
        // 核心执行逻辑
        // =========================================================
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // 性能优化：只在特定的模拟帧处理该Chunk，分散负载
            if (chunk.GetSharedComponent(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
            {
                return;
            }

            NativeArray<Entity> buildingEntities = chunk.GetNativeArray(this.m_EntityType);
            BufferAccessor<Renter> renterBuffers = chunk.GetBufferAccessor(ref this.m_RenterType);
            DynamicBuffer<CityModifier> cityModifiers = this.m_CityModifiers[this.m_City];

            // 遍历Chunk中的所有建筑实体
            for (int i = 0; i < buildingEntities.Length; i++)
            {
                Entity buildingEntity = buildingEntities[i];
                Entity buildingPrefab = this.m_Prefabs[buildingEntity].m_Prefab;

                // 跳过非租赁类建筑
                if (!this.m_BuildingProperties.HasComponent(buildingPrefab))
                {
                    continue;
                }

                // ====== 1. 获取建筑基础数据 ======
                BuildingPropertyData buildingPropertyData = this.m_BuildingProperties[buildingPrefab];
                Building building = this.m_Buildings[buildingEntity];
                DynamicBuffer<Renter> renters = renterBuffers[i];
                BuildingData buildingData = this.m_BuildingDatas[buildingPrefab];

                // 计算地块大小
                int lotSize = buildingData.m_LotSize.x * buildingData.m_LotSize.y;

                // 获取基础地价 (RoadEdge)
                float landValueBase = 0f;
                if (this.m_LandValues.HasComponent(building.m_RoadEdge))
                {
                    landValueBase = this.m_LandValues[building.m_RoadEdge].m_LandValue;
                }

                // ====== 2. 确定区域类型 (住宅/商业/工业/办公) ======
                Game.Zones.AreaType areaType = Game.Zones.AreaType.None;
                int buildingLevel = PropertyUtils.GetBuildingLevel(buildingPrefab, this.m_SpawnableBuildingData);
                bool ignoreLandValue = false;
                bool isOffice = false;

                if (this.m_SpawnableBuildingData.HasComponent(buildingPrefab))
                {
                    SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingData[buildingPrefab];
                    areaType = this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_AreaType;

                    // 检查区域属性（例如某些低密度区可能忽略地价影响）
                    if (this.m_ZonePropertiesDatas.TryGetComponent(spawnableBuildingData.m_ZonePrefab, out var componentData))
                    {
                        ignoreLandValue = componentData.m_IgnoreLandValue;
                    }
                    isOffice = (this.m_ZoneData[spawnableBuildingData.m_ZonePrefab].m_ZoneFlags & ZoneFlags.Office) != 0;
                }

                // --- 3. 处理污染通知 (仅住宅) ---
                this.ProcessPollutionNotification(areaType, buildingEntity, cityModifiers);

                // --- 4. 计算每户租金 ---
                int buildingGarbageFeePerDay = this.m_FeeParameters.GetBuildingGarbageFeePerDay(areaType, isOffice);
                int rentPerRenter = PropertyUtils.GetRentPricePerRenter(buildingPropertyData, buildingLevel, lotSize, landValueBase, areaType, ref this.m_EconomyParameterData, ignoreLandValue);

                // 更新市场挂牌价格
                if (this.m_OnMarkets.HasComponent(buildingEntity))
                {
                    PropertyOnMarket onMarketData = this.m_OnMarkets[buildingEntity];
                    onMarketData.m_AskingRent = rentPerRenter;
                    this.m_OnMarkets[buildingEntity] = onMarketData;
                }

                int propertyCount = buildingPropertyData.CountProperties(); // 建筑总户数
                bool rentersWereRemoved = false; // 标记是否有租户被移除
                // x = 付不起的人数, y = 检查的总人数
                int2 affordabilityStats = default(int2);
                bool isExtractorBuilding = this.m_ExtractorProperties.HasComponent(buildingEntity); // 是否为采集设施（如油井）

                // --- 5. 遍历该建筑内的所有租户 (倒序遍历以便移除) ---
                for (int renterIndex = renters.Length - 1; renterIndex >= 0; renterIndex--)
                {
                    Entity renter = renters[renterIndex].m_Renter;
                    if (this.m_PropertyRenters.HasComponent(renter))
                    {
                        PropertyRenter propertyRenterData = this.m_PropertyRenters[renter];

                        if (!this.m_ResourcesBuf.HasBuffer(renter))
                        {
                            // [Mod修改逻辑] 调试代码，绝不应出现在生产版本！应该包装在 #if UNITY_EDITOR 预处理器指令中或完全删除！
                            // UnityEngine.Debug.Log($"no resources:{renter.Index}");
                            // 新增修正：当Resources buffer缺失时，移除该租户。
                            this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex, renter);
                            continue;
                        }

                        // --- 5a. 计算租户支付能力 (Income / Budget) ---
                        int renterUpkeepCapacity = 0;
                        bool isHousehold = this.m_HouseholdCitizenBufs.HasBuffer(renter);

                        if (isHousehold) // 家庭实体
                        {
                            // [Mod修改逻辑]
                            // 原计算将收入+全部储蓄作为支付能力，收入为零但储蓄较多的家庭会显得富有并且能够负担得起高昂的租金。但实际上0收入的家庭不会将所有储蓄全部用于支付高额租金，原计算将向他们收取高昂的租金，将导致迅速耗尽积蓄后被迫搬出去。参照现实通行规律，国际标准建议住房支出占收入 ≤30%,住房支出中，储蓄贡献不超过 10%–15% 的总储蓄; 现改为收入的30%+储蓄的1/10;
                            int householdIncome = EconomyUtils.GetHouseholdIncome(this.m_HouseholdCitizenBufs[renter], ref this.m_Workers, ref this.m_Citizens, ref this.m_HealthProblems, ref this.m_EconomyParameterData, this.m_TaxRates);
                            int householdSavings = math.max(0, EconomyUtils.GetResources(Resource.Money, this.m_ResourcesBuf[renter]));
                            // 核心修改：支付能力 = 收入的30% + 存款的10%。
                            renterUpkeepCapacity = (int)(householdIncome * 0.3f + householdSavings * 0.1f);
                        }
                        else // 公司实体
                        {
                            Entity renterPrefab = this.m_Prefabs[renter].m_Prefab;
                            // 检查公司数据完整性
                            if (!this.m_ProcessDatas.HasComponent(renterPrefab) || !this.m_WorkProviders.HasComponent(renter) || !this.m_WorkplaceDatas.HasComponent(renterPrefab))
                            {
                                continue; // Skip if company data is missing.
                            }

                            IndustrialProcessData industrialProcessData = this.m_ProcessDatas[renterPrefab];
                            bool isIndustrial = !this.m_ServiceAvailables.HasComponent(renter);

                            // 估算公司最大日利润
                            int companyMaxProfitPerDay = EconomyUtils.GetCompanyMaxProfitPerDay(this.m_WorkProviders[renter], areaType == Game.Zones.AreaType.Industrial, buildingLevel, this.m_ProcessDatas[renterPrefab], this.m_ResourcePrefabs, this.m_WorkplaceDatas[renterPrefab], ref this.m_ResourceDatas, ref this.m_EconomyParameterData);

                            // 公司预算取 预期利润 和 公司总资产 中的较大值/特定逻辑
                            renterUpkeepCapacity = ((companyMaxProfitPerDay >= renterUpkeepCapacity) ? companyMaxProfitPerDay : ((!this.m_OwnedVehicles.HasBuffer(renter)) ? EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData, this.m_ResourcesBuf[renter], this.m_ResourcePrefabs, ref this.m_ResourceDatas) : EconomyUtils.GetCompanyTotalWorth(isIndustrial, industrialProcessData, this.m_ResourcesBuf[renter], this.m_OwnedVehicles[renter], ref this.m_LayoutElements, ref this.m_DeliveryTrucks, this.m_ResourcePrefabs, ref this.m_ResourceDatas)));

                        }

                        // --- 5b. 更新租金并检查是否需要搬家 ---
                        propertyRenterData.m_Rent = rentPerRenter;
                        this.m_PropertyRenters[renter] = propertyRenterData;

                        // [Mod修改逻辑]
                        // 原逻辑为(v1.3.6f版本改动)如果一个家庭目前的租金低于其经济能力的一半 ，他们就会寻求新房产。其目的是模拟中产阶级化——富裕的公民搬到更昂贵的住房。然而，由于 renterUpkeepCapacity 包括他们所有的储蓄（参见修正 #1），一个刚刚获得大笔遗产或勤奋储蓄的家庭将被标记为“太富有”，即使他们的收入没有改变，也将被迫寻找新的住房。这可能导致不必要且可能不受欢迎的居民洗牌。此外，原逻辑为一旦付不起租金或能够改善时立即开始找房，与现实中大多数人会尝试通过其他方式（如增加工作时间、削减开支等）来维持现有住房的行为不符。现改为：当租金过高（付不起）时，家庭会寻求新房；当租金过低（低于30%）时，家庭有30%的概率寻求升级住房。并且引入检查周期，避免每帧都检查导致的性能问题和频繁搬家。

                        int totalCostPerDay = rentPerRenter + buildingGarbageFeePerDay;

                        if (isHousehold)
                        {
                            // 定义检查周期（以 RentAdjustSystem 的更新次数为单位）
                            // 游戏内1天 = RentAdjustSystem.kUpdatesPerDay (值为16)，即此Job每天运行16次。
                            // 【待测试】根据需要可调整检查频率以平衡性能和响应性。
                            int kUpdatesPerDay = RentAdjustSystem.kUpdatesPerDay;
                            int checkPeriodForHighRent = 2 * kUpdatesPerDay; // 租金过高的困难家庭：2天(月)检查16次
                            int checkPeriodForLowRent = 4 * kUpdatesPerDay;  // 租金过低的富裕家庭：4天检查16次 (但有概率)

                            // 利用实体索引值，将家庭分散到不同的检查“批次”中。
                            // (uint)renter.Index % checkPeriodForHighRent 会给每个家庭一个 0 到 (period-1) 的固定偏移量。
                            // 这确保了在任何一个时刻，只有约 1/period 的家庭会进行检查。

                            // 利用 Entity.Index 对检查时间进行分散
                            // `m_UpdateFrameIndex` 在此Job中是单调递增的，可以看作是时间。
                            bool isCheckFrameHigh = (m_UpdateFrameIndex % checkPeriodForHighRent == (uint)renter.Index % checkPeriodForHighRent);
                            bool isCheckFrameLow = (m_UpdateFrameIndex % checkPeriodForLowRent == (uint)renter.Index % checkPeriodForLowRent);

                            // 【条件1】: 租金过高 (付不起)
                            // 只有当“今天”轮到这个家庭检查时，才执行逻辑。

                            if (totalCostPerDay > renterUpkeepCapacity && isCheckFrameHigh)
                            {
                                // 如果满足条件，且该家庭当前不在找房，则启用PropertySeeker
                                if (!this.m_OnMarkets.IsComponentEnabled(renter))
                                {
                                    this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, renter, value: true);
                                }
                            }
                            // 【条件2】: 租金过低 (改善住房)
                            // 只有当“今天”轮到这个家庭检查时...
                            else if (totalCostPerDay < renterUpkeepCapacity && isCheckFrameLow)
                            {
                                // 并且，满足30%的概率条件时，才触发。
                                // 我们使用一个简单的确定性随机算法，确保结果可重复且分布均匀。
                                if (((uint)renter.Index + (uint)(m_UpdateFrameIndex / checkPeriodForLowRent)) % 10 < 3)
                                {
                                    if (!this.m_OnMarkets.IsComponentEnabled(renter))
                                    {
                                        this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, renter, value: true);
                                    }
                                }
                            }
                        }

                        else // 对公司的逻辑暂时保持不变或简化
                        {
                            if (totalCostPerDay > renterUpkeepCapacity)
                            {
                                this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, renter, value: true);
                            }
                        }
                        // 修正结束；

                        // --- 5c. 统计支付能力数据 ---
                        affordabilityStats.y++; // 检查的总人数 +1
                        if (rentPerRenter > renterUpkeepCapacity)
                        {
                            affordabilityStats.x++; // 付不起的人数 +1
                        }
                    }
                    else
                    {
                        // 数据异常：Buffer里有实体但没有PropertyRenter组件，移除之
                        renters.RemoveAt(renterIndex);
                        rentersWereRemoved = true;
                    }
                }

                // ====== 6. 处理“高租金”图标逻辑 ======
                // 计算付不起租金的比例
                float highRentRatio = (float)affordabilityStats.x / math.max(1f, affordabilityStats.y);

                // 如果比例低于70% 或 有更重要的问题，移除图标
                if (highRentRatio <= 0.7f || !this.CanDisplayHighRentWarnIcon(renters))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_BuildingConfigurationData.m_HighRentNotification);
                    building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[buildingEntity] = building;
                }

                // [Mod修改]
                // 原逻辑为仅当建筑物空置时才显示高租金警告，一旦满员就不会显示，这与现实情况不符。可能有意设计为高租金阻止新租户入住的提醒，但这种信息并不完整。现改为当建筑物无论是否有空置房源，只要大部分租户负担不起租金时就显示高租金警告。
                // mod：将>改为>=;即使满员也会警告，提示玩家经济压力
                else if (renters.Length > 0 && !isExtractorBuilding && propertyCount >= renters.Length && (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) == 0)
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_BuildingConfigurationData.m_HighRentNotification, IconPriority.Problem);
                    building.m_Flags |= Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[buildingEntity] = building;
                }

                // ====== 7. 清理与收尾 ======

                // 超员处理：如果租户数量超过建筑容量，驱逐最后一个
                if (renters.Length > 0 && renters.Length > propertyCount)
                {
                    Entity lastRenter = renters[renters.Length - 1].m_Renter;
                    if (this.m_PropertyRenters.HasComponent(lastRenter))
                    {
                        this.m_CommandBuffer.RemoveComponent<PropertyRenter>(unfilteredChunkIndex, renters[renters.Length - 1].m_Renter);
                        renters.RemoveAt(renters.Length - 1);
                    }
                    // 修正5： 调试代码，绝不应出现在生产版本！
                    // UnityEngine.Debug.LogWarning($"Removed extra renter from building:{buildingEntity.Index}");
                }

                // 如果建筑变空了，确保移除警告
                if (renters.Length == 0 && (building.m_Flags & Game.Buildings.BuildingFlags.HighRentWarning) != Game.Buildings.BuildingFlags.None)
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_BuildingConfigurationData.m_HighRentNotification);
                    building.m_Flags &= ~Game.Buildings.BuildingFlags.HighRentWarning;
                    this.m_Buildings[buildingEntity] = building;
                }

                // 如果有租户搬走/被移除，且建筑未废弃/损毁，且还有空位，将其重新挂牌上市
                if (this.m_Prefabs.HasComponent(buildingEntity) && !this.m_Abandoned.HasComponent(buildingEntity) && !this.m_Destroyed.HasComponent(buildingEntity) && rentersWereRemoved && propertyCount > renters.Length)
                {
                    this.m_CommandBuffer.AddComponent(unfilteredChunkIndex, buildingEntity, new PropertyOnMarket
                    {
                        m_AskingRent = rentPerRenter
                    });
                }
            }
        }

        private void ProcessPollutionNotification(Game.Zones.AreaType areaType, Entity buildingEntity, DynamicBuffer<CityModifier> cityModifiers)
        {
            if (areaType == Game.Zones.AreaType.Residential)
            {
                // mod v2.* 修改：GetGroundPollutionBonuses 等静态方法重定向；
                int2 groundPollutionBonuses = CellMapSystemRe.GetGroundPollutionBonuses(buildingEntity, ref this.m_Transforms, this.m_PollutionMap, cityModifiers, in this.m_CitizenHappinessParameterData);
                int2 noiseBonuses = CellMapSystemRe.GetNoiseBonuses(buildingEntity, ref this.m_Transforms, this.m_NoiseMap, in this.m_CitizenHappinessParameterData);
                int2 airPollutionBonuses = CellMapSystemRe.GetAirPollutionBonuses(buildingEntity, ref this.m_Transforms, this.m_AirPollutionMap, cityModifiers, in this.m_CitizenHappinessParameterData);

                // v1.4.2新增：如果建筑正在建设中，则不显示污染相关通知
                bool isUnderConstruction = this.m_UnderConstructions.HasComponent(buildingEntity);

                bool isGroundPollutionBad = !isUnderConstruction && groundPollutionBonuses.x + groundPollutionBonuses.y < 2 * this.m_PollutionParameters.m_GroundPollutionNotificationLimit;
                bool isAirPollutionBad = !isUnderConstruction && airPollutionBonuses.x + airPollutionBonuses.y < 2 * this.m_PollutionParameters.m_AirPollutionNotificationLimit;
                bool isNoisePollutionBad = !isUnderConstruction && noiseBonuses.x + noiseBonuses.y < 2 * this.m_PollutionParameters.m_NoisePollutionNotificationLimit;

                BuildingNotifications notifications = this.m_BuildingNotifications[buildingEntity];

                // 处理地面污染图标
                if (isGroundPollutionBad && !notifications.HasNotification(BuildingNotification.GroundPollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_GroundPollutionNotification, IconPriority.Problem);
                    notifications.m_Notifications |= BuildingNotification.GroundPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
                else if (!isGroundPollutionBad && notifications.HasNotification(BuildingNotification.GroundPollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_PollutionParameters.m_GroundPollutionNotification);
                    notifications.m_Notifications &= ~BuildingNotification.GroundPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }

                // 处理空气污染图标
                if (isAirPollutionBad && !notifications.HasNotification(BuildingNotification.AirPollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_AirPollutionNotification, IconPriority.Problem);
                    notifications.m_Notifications |= BuildingNotification.AirPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
                else if (!isAirPollutionBad && notifications.HasNotification(BuildingNotification.AirPollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_PollutionParameters.m_AirPollutionNotification);
                    notifications.m_Notifications &= ~BuildingNotification.AirPollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }

                // 处理噪音污染图标
                if (isNoisePollutionBad && !notifications.HasNotification(BuildingNotification.NoisePollution))
                {
                    this.m_IconCommandBuffer.Add(buildingEntity, this.m_PollutionParameters.m_NoisePollutionNotification, IconPriority.Problem);
                    notifications.m_Notifications |= BuildingNotification.NoisePollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
                else if (!isNoisePollutionBad && notifications.HasNotification(BuildingNotification.NoisePollution))
                {
                    this.m_IconCommandBuffer.Remove(buildingEntity, this.m_PollutionParameters.m_NoisePollutionNotification);
                    notifications.m_Notifications &= ~BuildingNotification.NoisePollution;
                    this.m_BuildingNotifications[buildingEntity] = notifications;
                }
            }
        }
    }

}
