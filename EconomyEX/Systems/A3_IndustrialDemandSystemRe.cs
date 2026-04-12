// Game.Simulation.IndustrialDemandSystem
// 系统实例被多个外部系统调用，采用Job通用替换。

using Colossal.Collections;
using Game.Buildings;
using Game.City;
using Game.Companies;
using Game.Economy;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Zones;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace EconomyEX.Systems
{
    /// <summary>
    /// 
    /// </summary>
    [BurstCompile]
    public struct UpdateIndustrialDemandJob : IJob
    {
        // ----------------- 输入/输出数据定义 --------------------
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ZoneData> m_UnlockedZoneDatas; // 已解锁的分区数据

        // 区块数据 (ArchetypeChunks)存放实体
        [ReadOnly] public NativeList<ArchetypeChunk> m_IndustrialPropertyChunks; // 实体工业地块 (制造业/仓储)
        [ReadOnly] public NativeList<ArchetypeChunk> m_OfficePropertyChunks; // 办公地块 (无形产业)
        [ReadOnly] public NativeList<ArchetypeChunk> m_StorageCompanyChunks; // 仓储公司实体
        [ReadOnly] public NativeList<ArchetypeChunk> m_CityServiceChunks; // 城市服务实体

        // 组件句柄 (TypeHandles)
        [ReadOnly] public EntityTypeHandle m_EntityType;
        [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly] public ComponentTypeHandle<CityServiceUpkeep> m_ServiceUpkeepType;
        [ReadOnly] public ComponentTypeHandle<PropertyOnMarket> m_PropertyOnMarketType; // 正在招租的物业

        // 组件查找 (ComponentLookups)
        [ReadOnly] public ComponentLookup<Population> m_Populations;
        [ReadOnly] public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;
        [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
        [ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas; // 建筑大小、标志
        [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas; // 建筑类别属性
        [ReadOnly] public ComponentLookup<Attached> m_Attached;
        [ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
        [ReadOnly] public ComponentLookup<StorageLimitData> m_StorageLimitDatas;
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingDatas;

        // 缓冲区查找 (BufferLookups)
        [ReadOnly] public BufferLookup<ServiceUpkeepData> m_ServiceUpkeeps;
        [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
        [ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;
        [ReadOnly] public BufferLookup<ServiceUpkeepData> m_Upkeeps;

        // 全局参数
        public EconomyParameterData m_EconomyParameters;
        public DemandParameterData m_DemandParameters;
        [ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
        [ReadOnly] public NativeArray<int> m_EmployableByEducation; // 按教育水平划分的可用劳动力
        [ReadOnly] public NativeArray<int> m_TaxRates; // 税率
        [ReadOnly] public Workplaces m_FreeWorkplaces; // 各学历空缺岗位

        public Entity m_City;

        // ================================================
        // 输出：全局需求 (UI显示用，0-100)
        // ================================================
        public NativeValue<int> m_IndustrialCompanyDemand; // 实体工业-企业经营意愿总和
        public NativeValue<int> m_IndustrialBuildingDemand; // 实体工业-建筑规划需求 (黄色条)
        public NativeValue<int> m_StorageCompanyDemand; // 仓储-企业经营意愿总和
        public NativeValue<int> m_StorageBuildingDemand; // 仓储-建筑规划需求
        public NativeValue<int> m_OfficeCompanyDemand; // 办公-企业经营意愿总和
        public NativeValue<int> m_OfficeBuildingDemand; // 办公-建筑规划需求 (紫色条)

        // 输出：UI提示因子 (如: "受高税收影响", "受劳动力不足影响")
        public NativeArray<int> m_IndustrialDemandFactors;
        public NativeArray<int> m_OfficeDemandFactors;

        // 输出：每种具体资源的需求详情 (供下游系统引用)
        public NativeArray<int> m_IndustrialCompanyDemands; // 具体资源的企业入驻意愿
        public NativeArray<int> m_IndustrialBuildingDemands; // 具体资源的建筑需求
        public NativeArray<int> m_StorageBuildingDemands;
        public NativeArray<int> m_StorageCompanyDemands;

        // 经济统计数据
        [ReadOnly] public NativeArray<int> m_Productions; // 当前生产量
        [ReadOnly] public NativeArray<int> m_CompanyResourceDemands; // 企业资源需求/消耗量
        [ReadOnly] public NativeArray<int> m_HouseholdResourceDemands; // 家庭资源需求/消耗量

        // 统计计算用临时数组
        public NativeArray<int> m_FreeProperties; // 可用空置物业
        [ReadOnly] public NativeArray<int> m_Propertyless; // 有公司但无物业的流浪公司数量

        public NativeArray<int> m_FreeStorages; // 空闲仓库数量
        public NativeArray<int> m_Storages; // 现有仓库数量
        public NativeArray<int> m_StorageCapacities; // 仓库总容量(单位)
        public NativeArray<int> m_ResourceDemands; // 资源总缺口 (计算中间值)

        public float m_IndustrialOfficeTaxEffectDemandOffset; // 税收影响偏移量
        public bool m_UnlimitedDemand; // 作弊模式：无限需求

        public void Execute()
        {
            // ===============================================
            // 初始化
            // ===============================================
            // 原版仓储需求参数：(改为局部变量以便修改)
            int kStorageProductionDemand = 2000; // 保持原版方式，保证生成仓储公司需求
            int kStorageCompanyEstimateLimit = 864000;

            // [MODIFIED 1] 获取当前城市总人口，用于解决百万人口的数值溢出问题
            int currentPopulation = m_Populations[m_City].m_Population;
            // 计算缩放因子：基准为10,000人。如果是1M人口，缩放因子为100。
            // 这样1000人的劳动力缺口在1M人口城市会被视为等同于1w人口城市的10人缺口。
            float populationScaler = math.max(1f, currentPopulation / 10000f);

            // -----------------------------------------------------------------------
            // 1. 区域解锁检查
            // 检查是否有已解锁的工业区(办公包含在工业区,仅资源种类不同)。
            // 如果没有解锁工业区，最终的工业建筑需求将被强制为0。
            // -----------------------------------------------------------------------
            bool hasIndustrialZoneUnlocked = false;
            for (int i = 0; i < m_UnlockedZoneDatas.Length; i++)
            {
                if (m_UnlockedZoneDatas[i].m_AreaType == AreaType.Industrial)
                {
                    hasIndustrialZoneUnlocked = true;
                    break;
                }
            }

            DynamicBuffer<CityModifier> cityModifiers = m_CityModifiers[m_City];

            // -----------------------------------------------------------------------
            // 2. 初始化资源需求与清零计数器
            // 遍历所有资源，基于家庭和公司当前的消耗来初始化基础需求。
            // -----------------------------------------------------------------------
            ResourceIterator resourceIter = ResourceIterator.GetIterator();
            while (resourceIter.Next())
            {
                // 资源索引和数据
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);
                ResourceData resData = m_ResourceDatas[m_ResourcePrefabs[resourceIter.resource]];

                // 无形产品(办公资源)
                if (EconomyUtils.IsOfficeResource(resourceIter.resource))
                {
                    // 办公资源需求权重较高 (IceFlake Studios 官方补丁跟进：由2倍提升至3倍)
                    m_ResourceDemands[resIndex] =
                        (m_HouseholdResourceDemands[resIndex] + m_CompanyResourceDemands[resIndex]) * 3;
                }
                // 有形产品(非办公资源,实体工业资源)
                else
                {
                    // 制造业资源：如果是非办公需求且是工业原料/产品(即产业链未起步)，给予100的基础引导需求，否则使用实际公司需求
                    bool isBaseIndustrial =
                        EconomyUtils.IsIndustrialResource(resData, includeMaterial: false, includeOffice: false);
                    m_ResourceDemands[resIndex] = ((m_CompanyResourceDemands[resIndex] == 0 && isBaseIndustrial)
                        ? 100
                        : m_CompanyResourceDemands[resIndex]);
                }

                // 重置计数器
                m_FreeProperties[resIndex] = 0; // 市场上适合该资源的空房产
                m_Storages[resIndex] = 0; // 该资源的仓库数量
                m_FreeStorages[resIndex] = 0; // 该资源的全局可用空闲仓库位
                m_StorageCapacities[resIndex] = 0; // 该资源的全局最大存储容量
            }

            // 重置需求因子 UI 数据
            for (int j = 0; j < m_IndustrialDemandFactors.Length; j++) m_IndustrialDemandFactors[j] = 0;
            for (int k = 0; k < m_OfficeDemandFactors.Length; k++) m_OfficeDemandFactors[k] = 0;

            // -----------------------------------------------------------------------
            // 3. 统计城市服务的资源消耗
            // 城市服务（如发电厂、警局）的维护也会产生资源需求。包括有形和无形资源。
            // -----------------------------------------------------------------------
            foreach (var serviceChunk in m_CityServiceChunks)
            {
                if (!serviceChunk.Has(ref m_ServiceUpkeepType)) continue;

                NativeArray<Entity> entities = serviceChunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> prefabs = serviceChunk.GetNativeArray(ref m_PrefabType);

                for (int entIdx = 0; entIdx < prefabs.Length; entIdx++)
                {
                    Entity prefabEntity = prefabs[entIdx].m_Prefab;
                    Entity serviceEntity = entities[entIdx];

                    // 3a. 基础维护消耗
                    if (m_ServiceUpkeeps.HasBuffer(prefabEntity))
                    {
                        DynamicBuffer<ServiceUpkeepData> upkeepBuffer = m_ServiceUpkeeps[prefabEntity];
                        foreach (var upkeep in upkeepBuffer)

                        {

                            if (upkeep.m_Upkeep.m_Resource != Resource.Money)

                            {

                                m_ResourceDemands[EconomyUtils.GetResourceIndex(upkeep.m_Upkeep.m_Resource)] +=

                                    upkeep.m_Upkeep.m_Amount;

                            }

                        }
                    }

                    // 3b. 服务升级组件带来的额外消耗
                    if (m_InstalledUpgrades.HasBuffer(serviceEntity))
                    {
                        DynamicBuffer<InstalledUpgrade> upgradeBuffer = m_InstalledUpgrades[serviceEntity];
                        for (int u = 0; u < upgradeBuffer.Length; u++)
                        {
                            // 如果升级是关闭状态，则跳过
                            if (BuildingUtils.CheckOption(upgradeBuffer[u], BuildingOption.Inactive) ||
                                !m_Prefabs.HasComponent(upgradeBuffer[u].m_Upgrade))
                                continue;

                            Entity upgradePrefab = m_Prefabs[upgradeBuffer[u].m_Upgrade].m_Prefab;
                            if (m_Upkeeps.HasBuffer(upgradePrefab))
                            {
                                DynamicBuffer<ServiceUpkeepData> upgradeUpkeeps = m_Upkeeps[upgradePrefab];
                                for (int uu = 0; uu < upgradeUpkeeps.Length; uu++)
                                {
                                    m_ResourceDemands[
                                            EconomyUtils.GetResourceIndex(upgradeUpkeeps[uu].m_Upkeep.m_Resource)] +=
                                        upgradeUpkeeps[uu].m_Upkeep.m_Amount;
                                }
                            }
                        }
                    }
                }

            }

            // -----------------------------------------------------------------------
            // 4. 统计现有仓储容量 (Storage Companies)
            // 遍历现有的仓储公司，统计各资源的存储能力。
            // 仓库只处理有形资源。
            // -----------------------------------------------------------------------
            foreach (var storageChunk in m_StorageCompanyChunks)
            {
                NativeArray<Entity> entities = storageChunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> prefabs = storageChunk.GetNativeArray(ref m_PrefabType);

                // 遍历每个仓储公司实体
                for (int entIdx = 0; entIdx < entities.Length; entIdx++)
                {
                    Entity entity = entities[entIdx];
                    Entity prefab = prefabs[entIdx].m_Prefab;

                    // 仅处理有工业制造(这里包含有形和无形产品，但此处应指有形产品)数据的仓库
                    if (m_IndustrialProcessDatas.HasComponent(prefab))
                    {
                        // 获取该仓库存储的资源类型
                        int resIndex =
                            EconomyUtils.GetResourceIndex(m_IndustrialProcessDatas[prefab].m_Output.m_Resource);
                        m_Storages[resIndex]++; // 增加该资源的仓库计数

                        StorageLimitData limitData = m_StorageLimitDatas[prefab];

                        // 检查是否已经有租户（PropertyRenter）
                        if (!m_PropertyRenters.HasComponent(entity) ||
                            !m_Prefabs.HasComponent(m_PropertyRenters[entity].m_Property))
                        {
                            // 无租户/空闲仓库
                            // 这是一个“空壳”仓储公司（尚未完全激活或没有物业关联）
                            // 逻辑注记：这里 FreeStorages-- 是反直觉的。可能是为了后续计算“净缺口”。
                            // 意为：虽然存在这个公司实体，但它还没准备好，所以在计算“可用空间”时扣除计数？
                            // m_FreeStorages代表有效空闲的仓库数量；m_StorageCapacities代表总体存储容量
                            // 或者意为：该仓库未入驻公司，不能算入可用仓库；而仓库已建好，可算入全局总存储容量
                            m_FreeStorages[resIndex]--;
                            m_StorageCapacities[resIndex] += kStorageCompanyEstimateLimit;
                        }
                        else
                        {
                            // 有租户，累加其实际容量
                            Entity property = m_PropertyRenters[entity].m_Property;
                            Entity propertyPrefab = m_Prefabs[property].m_Prefab;
                            m_StorageCapacities[resIndex] +=
                                limitData.GetAdjustedLimitForWarehouse(m_SpawnableBuildingDatas[propertyPrefab],
                                    m_BuildingDatas[propertyPrefab]);
                        }
                    }
                }

            }

            // -----------------------------------------------------------------------
            // 5. 统计空闲工业/办公地产(办公地产包含在工业地产内)
            // 查看市场上有哪些空房子（PropertyOnMarket），并记录它们适合生产/存储什么资源。
            // -----------------------------------------------------------------------
            foreach (var chunk in m_IndustrialPropertyChunks)
            {
                // 必须是“市场上待租”的物业
                if (!chunk.Has(ref m_PropertyOnMarketType)) continue;

                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> prefabs = chunk.GetNativeArray(ref m_PrefabType);

                for (int i = 0; i < prefabs.Length; i++)
                {
                    Entity prefab = prefabs[i].m_Prefab;
                    if (!m_BuildingPropertyDatas.HasComponent(prefab)) continue;

                    BuildingPropertyData propData = m_BuildingPropertyDatas[prefab];

                    // 如果是附属建筑，检查父建筑的生产限制
                    if (m_Attached.TryGetComponent(entities[i], out Attached attached) &&
                        m_Prefabs.TryGetComponent(attached.m_Parent, out PrefabRef parentPrefabRef) &&
                        m_BuildingPropertyDatas.TryGetComponent(parentPrefabRef.m_Prefab,
                            out BuildingPropertyData parentPropData))
                    {
                        propData.m_AllowedManufactured = (Resource)((long)propData.m_AllowedManufactured & (long)parentPropData.m_AllowedManufactured);
                    }

                    // 遍历资源，标记该建筑允许生产或存储哪些资源
                    ResourceIterator allowIterator = ResourceIterator.GetIterator();
                    while (allowIterator.Next())
                    {
                        int resIndex = EconomyUtils.GetResourceIndex(allowIterator.resource);
                        // 可生产资源的建筑(含有少量存储空间但不算仓库)
                        if (((long)propData.m_AllowedManufactured & (long)allowIterator.resource) != 0L)
                        {
                            m_FreeProperties[resIndex]++;
                        }

                        // 允许仓储的建筑/仓库(算仓库且只能存储)
                        if (((long)propData.m_AllowedStored & (long)allowIterator.resource) != 0L)
                        {
                            m_FreeStorages[resIndex]++;
                        }
                    }
                }

            }

            // ---------------- 核心需求计算逻辑 -----------------

            // 记录上一帧是否有办公需求，用于平滑或迟滞逻辑
            bool wasOfficeBuildingDemandPositive = m_OfficeBuildingDemand.value > 0;

            // 重置本帧需求值
            m_IndustrialCompanyDemand.value = 0;
            m_IndustrialBuildingDemand.value = 0;
            m_StorageCompanyDemand.value = 0;
            m_StorageBuildingDemand.value = 0;
            m_OfficeCompanyDemand.value = 0;
            m_OfficeBuildingDemand.value = 0;

            int officeResourceCount = 0; // 计数：有多少种无形资源产生了需求
            int industrialResourceCount = 0; // 计数：有多少种有形资源产生了需求

            resourceIter = ResourceIterator.GetIterator();
            while (resourceIter.Next())
            {
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);
                if (!m_ResourceDatas.HasComponent(m_ResourcePrefabs[resourceIter.resource])) continue;

                ResourceData resData = m_ResourceDatas[m_ResourcePrefabs[resourceIter.resource]];
                bool isProduceable = resData.m_IsProduceable; // 可由原料生产
                bool isMaterial = resData.m_IsMaterial; // 采集业原料资源，不可生产只能采集
                bool isTradable = resData.m_IsTradable; // 可交易资源
                bool isOfficeResource = resData.m_Weight == 0f; // 无形商品(办公资源)

                // === A. 仓储需求计算 (Storage Demand) ===
                // 可交易并且非无形资源
                if (isTradable && !isOfficeResource)
                {
                    int currentDemand = m_ResourceDemands[resIndex];
                    m_StorageCompanyDemands[resIndex] = 0;
                    m_StorageBuildingDemands[resIndex] = 0;

                    // 如果需求超过阈值且当前容量不足，产生仓储公司需求
                    if (currentDemand > kStorageProductionDemand && m_StorageCapacities[resIndex] < currentDemand)
                    {
                        m_StorageCompanyDemands[resIndex] = 1;
                    }

                    // 如果没有空闲仓库位置，产生仓储建筑需求
                    if (m_FreeStorages[resIndex] < 0)
                    {
                        m_StorageBuildingDemands[resIndex] = 1;
                    }

                    m_StorageCompanyDemand.value += m_StorageCompanyDemands[resIndex];
                    m_StorageBuildingDemand.value += m_StorageBuildingDemands[resIndex];

                    // 更新 UI 因子：仓储需求 (索引17)
                    m_IndustrialDemandFactors[17] += math.max(0, m_StorageBuildingDemands[resIndex]);
                }

                if (!isProduceable) continue; // 无法生产的资源跳过后续计算

                // === B. 有形/无形产品基本生产需求计算 ===
                // 等同于企业经营意愿计算 (Company Profitability/Demand)

                // 1. 基础需求分
                float baseDemand = (isMaterial
                    ? m_DemandParameters.m_ExtractorBaseDemand
                    : m_DemandParameters.m_IndustrialBaseDemand);

                // 2. 市场供需比率 (Supply/Demand Ratio)
                // 需求越高，生产越少，比率越高，刺激需求
                float supplyDemandRatio = (1f + m_ResourceDemands[resIndex] - m_Productions[resIndex]) /
                                          (m_ResourceDemands[resIndex] + 1f);

                // 3. 应用特定城市修正 (Modifiers) : 电子产品(有形)/软件(无形)
                if (resourceIter.resource == Resource.Electronics)
                    CityUtils.ApplyModifier(ref baseDemand, cityModifiers,
                        CityModifierType.IndustrialElectronicsDemand);
                else if (resourceIter.resource == Resource.Software)
                    CityUtils.ApplyModifier(ref baseDemand, cityModifiers, CityModifierType.OfficeSoftwareDemand);


                // 4. 税收影响 (Tax Effect)
                int taxRate = (isOfficeResource
                    ? TaxSystem.GetOfficeTaxRate(resourceIter.resource, m_TaxRates)
                    : TaxSystem.GetIndustrialTaxRate(resourceIter.resource, m_TaxRates));

                // 税率低于 10% 产生正向刺激，高于 10% 产生负向抑制
                // 税率偏移：(税率 - 10%) * -0.05 * 敏感度。税率高于10%降低需求，反之提升需求。
                float taxFactor = m_DemandParameters.m_TaxEffect.z * -0.05f * (taxRate - 10f);
                taxFactor += m_IndustrialOfficeTaxEffectDemandOffset;
                float taxEffectVal = 100f * taxFactor; // 放大用于计算

                // 5. 劳动力可用性影响 (Workforce Effect)
                // [MODIFIED 2] 
                int highEduWorkerSurplus = 0; // 高学历劳动力得分 (Office偏好)
                int lowEduWorkerSurplus = 0; // 低学历劳动力得分 (Industrial偏好)
                float neutralUnemploymentRatio = m_DemandParameters.m_NeutralUnemployment / 100f;

                //// 遍历5个教育等级 (0-4)
                for (int eduLevel = 0; eduLevel < 5; eduLevel++)
                {
                    // 计算：可用劳动力 - 自然失业 - 现有空缺
                    int laborDelta = (int)(m_EmployableByEducation[eduLevel] * (1f - neutralUnemploymentRatio)) -
                                     m_FreeWorkplaces[eduLevel];

                    // 原逻辑为使用绝对数量计算
                    // 修正：按人口比例归一化差值。防止百万人口时数值过大。
                    // 将大城市的巨大数值缩放到原始设计预期的小数值范围内。
                    int scaledLaborDelta = (int)(laborDelta / populationScaler);

                    if (eduLevel < 2) lowEduWorkerSurplus += scaledLaborDelta;
                    else highEduWorkerSurplus += scaledLaborDelta;
                }


                // 注意：这里传入 scaledLaborDelta (缩放后的值)，因此 MapAndClaimWorkforceEffect 的 -2000~[Max] 范围现在依然有效
                if (taxEffectVal > 0f)
                {
                    // [MODIFIED 3] 放开廉价劳力红利上限，激活出口加工业玩法
                    highEduWorkerSurplus = (int)MapAndClaimWorkforceEffect(highEduWorkerSurplus,
                        0f - math.max(10f + taxEffectVal, 10f), 25f);
                    lowEduWorkerSurplus = (int)MapAndClaimWorkforceEffect(lowEduWorkerSurplus,
                        0f - math.max(10f + taxEffectVal, 10f), 40f);
                }
                else
                {
                    // [MODIFIED 3] 同上
                    highEduWorkerSurplus = math.clamp(highEduWorkerSurplus, -10, 25);
                    lowEduWorkerSurplus = math.clamp(lowEduWorkerSurplus, -10, 40);
                }

                // 6. 综合计算总市场需求(本地需求)
                float marketDemand = 50f * math.max(0f, baseDemand * supplyDemandRatio); // 市场拉动

                // 7. 计算最终公司需求分数(Company Demand)
                int calculatedCompanyDemand;

                // 无形资源需求(办公) 
                if (isOfficeResource)
                {
                    // 办公：市场 + 税收 + 高学历
                    calculatedCompanyDemand = (marketDemand > 0f)
                        ? Mathf.RoundToInt(marketDemand + taxEffectVal + highEduWorkerSurplus)
                        : 0;
                    calculatedCompanyDemand = math.clamp(calculatedCompanyDemand, 0, 100);

                    m_IndustrialCompanyDemands[resIndex] = calculatedCompanyDemand; // 更新对应资源的公司总需求
                    // [MODIFIED] 防止累加溢出
                    m_OfficeCompanyDemand.value += calculatedCompanyDemand; // 累加到办公公司总需求

                    officeResourceCount++;
                }
                else
                {
                    // 工业：市场 + 税收 + 全学历
                    calculatedCompanyDemand =
                        Mathf.RoundToInt(marketDemand + taxEffectVal + highEduWorkerSurplus + lowEduWorkerSurplus);
                    calculatedCompanyDemand = math.clamp(calculatedCompanyDemand, 0, 100);

                    m_IndustrialCompanyDemands[resIndex] = calculatedCompanyDemand; // 更新对应资源的公司总需求
                    m_IndustrialCompanyDemand.value +=
                        math.min(int.MaxValue - 1000, calculatedCompanyDemand); // 累加到工业公司总需求

                    if (!isMaterial) industrialResourceCount++; // 仅限非原料类资源
                }

                // === C. 建筑需求计算 (Building Demand/ Zoning Demand) ===
                // 将新公司入驻需求转换为建筑需求(扣除空置)
                // 游戏中1公司占1建筑；

                // 遍历每种资源，如果有资源需求才考虑建筑
                if (m_ResourceDemands[resIndex] > 0)
                {
                    int demand = m_IndustrialCompanyDemands[resIndex];
                    // 短缺数量 = 当前资源空闲资产 - 流浪公司
                    // (流浪公司全部入驻所需要空闲资产)
                    int propShortage = m_Propertyless[resIndex] - m_FreeProperties[resIndex];
                    // 设置当前资源对应建筑需求
                    m_IndustrialBuildingDemands[resIndex] = (demand > 0) switch
                    {
                        true when !isMaterial => propShortage >= 0 ? 50 : 0, // 短缺赋予需求
                        true => 1,
                        _ => 0,
                    };

                    // 累加总建筑需求
                    if (m_IndustrialBuildingDemands[resIndex] > 0)
                    {
                        int finalBuildingDemand = m_IndustrialCompanyDemands[resIndex];

                        if (isOfficeResource)
                            m_OfficeBuildingDemand.value += finalBuildingDemand; // 使用公司意愿强度作为权重
                        else if (!isMaterial)
                            m_IndustrialBuildingDemand.value += finalBuildingDemand;
                    }
                }
                // 资源需求为0时
                else
                {
                    m_IndustrialBuildingDemands[resIndex] = 0;
                }

                // === D. 填充 UI 需求因子 (Demand Factors) ===
                // 这些索引对应 UI 上的提示条 (例如：2=Educated Workers, 11=Tax, 13=Demand)

                // 如果是原料采集业（Extractor），不由一般工业/办公UI需求条显示，跳过
                if (isMaterial) continue;

                // 办公资源需求
                if (isOfficeResource)
                {
                    // 仅当有上一帧需求 或 当前有强劲新需求时才更新 UI，避免 UI 闪烁
                    if (!wasOfficeBuildingDemandPositive || (m_IndustrialBuildingDemands[resIndex] > 0 &&
                                                             m_IndustrialCompanyDemands[resIndex] > 0))
                    {
                        m_OfficeDemandFactors[2] += highEduWorkerSurplus; // High Skill Labor
                        m_OfficeDemandFactors[4] += (int)marketDemand; // Local Demand/Market
                        m_OfficeDemandFactors[11] += (int)taxEffectVal; // Taxes
                        m_OfficeDemandFactors[13] += m_IndustrialBuildingDemands[resIndex]; // Building Demand
                    }
                }

                // 有形产品(工业)资源需求
                else
                {
                    m_IndustrialDemandFactors[2] += highEduWorkerSurplus;
                    m_IndustrialDemandFactors[1] += lowEduWorkerSurplus; // Low Skill Labor
                    m_IndustrialDemandFactors[4] += (int)marketDemand;
                    m_IndustrialDemandFactors[11] += (int)taxEffectVal;
                    m_IndustrialDemandFactors[13] += m_IndustrialBuildingDemands[resIndex];
                }
            }

            // ---------------- 后处理与归一化 ----------------
            // -----------------------------------------------------------------------
            // 7. 后处理与修正
            // -----------------------------------------------------------------------

            // a. 处理因子显示逻辑：如果为0则设为-1 (可能用于隐藏UI条)，否则保持原值
            m_OfficeDemandFactors[4] = ((m_OfficeDemandFactors[4] == 0) ? (-1) : m_OfficeDemandFactors[4]);
            m_IndustrialDemandFactors[4] = ((m_IndustrialDemandFactors[4] == 0) ? (-1) : m_IndustrialDemandFactors[4]);
            m_IndustrialDemandFactors[13] =
                ((m_IndustrialDemandFactors[13] == 0) ? (-1) : m_IndustrialDemandFactors[13]);
            m_OfficeDemandFactors[13] = ((m_OfficeDemandFactors[13] == 0) ? (-1) : m_OfficeDemandFactors[13]);

            // b. 如果城市没有人口，强制将市场需求因子置为 0
            if (m_Populations[m_City].m_Population <= 0)
            {
                m_OfficeDemandFactors[4] = 0;
                m_IndustrialDemandFactors[4] = 0;
            }

            // c. 如果地图上完全没有对应的特定产业分区，将“建筑需求”因子从13转移到索引 18 (可能是"无分区"提示)，并清空原因子
            // Index 18 可能是 "Zone Availability"
            if (m_IndustrialPropertyChunks.Length == 0)
            {
                m_IndustrialDemandFactors[18] = m_IndustrialDemandFactors[13];
                m_IndustrialDemandFactors[13] = 0;
            }

            if (m_OfficePropertyChunks.Length == 0)
            {
                m_OfficeDemandFactors[18] = m_OfficeDemandFactors[13];
                m_OfficeDemandFactors[13] = 0;
            }

            // d. 仓储建筑需求指数平滑增强 (Power 0.75)
            m_StorageBuildingDemand.value = Mathf.CeilToInt(math.pow(20f * m_StorageBuildingDemand.value, 0.75f));

            // e. 归一化需求值：乘以2并除以有效资源种类数，求平均强度
            // 工业建筑总需求
            //===========修正：增加0除判断=============
            if (industrialResourceCount > 0)
                m_IndustrialBuildingDemand.value = (hasIndustrialZoneUnlocked
                    ? (2 * m_IndustrialBuildingDemand.value / industrialResourceCount)
                    : 0);

            // 办公公司总需求
            // this.m_OfficeCompanyDemand.value *= 2 * this.m_OfficeCompanyDemand.value / officeResourceCount;
            // 此处可能是书写错误，变成了自乘；
            //============重要修正：改为办公建筑需求计算并去掉自乘============
            if (officeResourceCount > 0)
            {
                // 原版逻辑：value = value * 2 * value / count (平方级增长，非常危险)
                // [MODIFIED ] 修正为线性或更加平滑的逻辑： value = 2 * value / count
                m_OfficeCompanyDemand.value = 2 * m_OfficeCompanyDemand.value / officeResourceCount;

                // 办公建筑需求通常跟随公司需求，但也需要归一化
                m_OfficeBuildingDemand.value = (m_OfficeBuildingDemand.value > 0)
                    ? (2 * m_OfficeBuildingDemand.value / officeResourceCount)
                    : 0;
            }

            // f. 建筑需求最终钳位到 0-100
            m_IndustrialBuildingDemand.value = math.clamp(m_IndustrialBuildingDemand.value, 0, 100);
            m_OfficeBuildingDemand.value = math.clamp(m_OfficeBuildingDemand.value, 0, 100);

            // g. 开发者作弊模式
            if (m_UnlimitedDemand)
            {
                m_OfficeBuildingDemand.value = 100;
                m_IndustrialBuildingDemand.value = 100;
            }
        }

        // ------------------ 辅助方法 ------------------

        /// <summary>
        /// 映射劳动力效应值。
        /// 将原始的劳动力盈余/赤字映射到一个合理的分数区间。
        /// </summary>
        /// <param name="value">原始劳动力差值</param>
        /// <param name="min">映射的最小分数</param>
        /// <param name="max">映射的最大分数</param>
        /// <returns>映射后的需求影响分</returns>
        // 保持原有的Map函数不变，通过Scaler适配它的输入范围
        private float MapAndClaimWorkforceEffect(float value, float min, float max)
        {
            if (value < 0f)
            {
                // 注意这里期望输入是 -2000
                float valueToClamp = math.unlerp(-2000f, 0f, value);
                valueToClamp = math.clamp(valueToClamp, 0f, 1f);
                return math.lerp(min, 0f, valueToClamp);
            }

            // 注意这里期望输入是 20
            float valueToClamp2 = math.unlerp(0f, 20f, value);
            valueToClamp2 = math.clamp(valueToClamp2, 0f, 1f);
            return math.lerp(0f, max, valueToClamp2);
        }
    }
}


