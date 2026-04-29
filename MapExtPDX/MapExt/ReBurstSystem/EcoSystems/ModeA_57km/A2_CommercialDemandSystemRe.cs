using Colossal.Collections;
using Game.Buildings;
using Game.City;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Game.Zones;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapExtPDX.ModeA
{
    /// <summary>
    /// 更新商业需求的作业。
    /// </summary>
    /// <remarks>
    /// 读取城市状态、税率、资源与物业数据，计算每种商业资源的公司需求与建筑需求。
    /// 基于 1.5.7f 版本适配：非住宿资源使用比率模型 (CurrentAvailables / TotalAvailables)。
    /// </remarks>
    [BurstCompile]
    public struct UpdateCommercialDemandJob : IJob
    {
        [DeallocateOnJobCompletion][ReadOnly] public NativeArray<ZoneData> m_UnlockedZoneDatas;
        [ReadOnly] public NativeList<ArchetypeChunk> m_CommercialPropertyChunks;
        [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly] public BufferTypeHandle<Renter> m_RenterType;
        [ReadOnly] public ComponentTypeHandle<PropertyOnMarket> m_PropertyOnMarketType;
        [ReadOnly] public ComponentLookup<Population> m_Populations;
        [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas;
        [ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
        [ReadOnly] public ComponentLookup<CommercialCompany> m_CommercialCompanies;
        [ReadOnly] public ComponentLookup<Tourism> m_Tourisms;
        [ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
        [ReadOnly] public DemandParameterData m_DemandParameters;
        [ReadOnly] public Entity m_City;
        [ReadOnly] public NativeArray<int> m_TaxRates;

        // --- 输出/读写数据 ---
        public NativeValue<int> m_CompanyDemand; // 公司入驻需求（决定是否生成新公司）
        public NativeValue<int> m_BuildingDemand; // 建筑建设需求（决定是否盖新楼）
        public NativeArray<int> m_DemandFactors; // 需求因子（用于UI显示，如税收影响、工人不足等）
        public NativeArray<int> m_FreeProperties; // 空置的商业物业数量（按资源分类）
        public NativeArray<int> m_ResourceDemands; // 各具体资源的需求值
        public NativeArray<int> m_BuildingDemands; // 各具体资源的建筑需求

        [ReadOnly] public NativeArray<int> m_ProduceCapacity; // (未在逻辑中使用，但存在于结构体中)
        [ReadOnly] public NativeArray<int> m_CurrentAvailables; // 当前市场上可用的商品/服务量（库存量）
        [ReadOnly] public NativeArray<int> m_TotalAvailables; // 各资源总可用容量（1.5.7f 新增）
        [ReadOnly] public NativeArray<int> m_Propertyless; // 没有物业的公司数量（正在寻找办公地点的公司）

        public float m_CommercialTaxEffectDemandOffset; // 商业税收影响偏移量
        public bool m_UnlimitedDemand; // 作弊模式：无限需求

        public void Execute()
        {
            // ----------------------------------------------------------------
            // Phase 1: 检查商业区是否已解锁
            // ----------------------------------------------------------------
            bool isCommercialZoneUnlocked = false;
            for (int i = 0; i < m_UnlockedZoneDatas.Length; i++)
            {
                if (m_UnlockedZoneDatas[i].m_AreaType == AreaType.Commercial)
                {
                    isCommercialZoneUnlocked = true;
                    break;
                }
            }

            // ----------------------------------------------------------------
            // Phase 2: 初始化/重置计数器
            // ----------------------------------------------------------------
            ResourceIterator resourceIter = ResourceIterator.GetIterator();
            while (resourceIter.Next())
            {
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);
                m_FreeProperties[resIndex] = 0;
                m_BuildingDemands[resIndex] = 0;
                m_ResourceDemands[resIndex] = 0;
            }
            for (int j = 0; j < m_DemandFactors.Length; j++)
            {
                m_DemandFactors[j] = 0;
            }

            // ----------------------------------------------------------------
            // Phase 3: 统计空置商业物业 (Calculate Free Properties)
            // ----------------------------------------------------------------
            foreach (var archetypeChunk in m_CommercialPropertyChunks)
            {
                // 只有正在市场上待租的建筑才算
                if (!archetypeChunk.Has(ref m_PropertyOnMarketType))
                {
                    continue;
                }

                NativeArray<PrefabRef> prefabs = archetypeChunk.GetNativeArray(ref m_PrefabType);
                BufferAccessor<Renter> renterAccessors = archetypeChunk.GetBufferAccessor(ref m_RenterType);

                for (int l = 0; l < prefabs.Length; l++)
                {
                    Entity prefabEntity = prefabs[l].m_Prefab;
                    if (!m_BuildingPropertyDatas.HasComponent(prefabEntity))
                    {
                        continue;
                    }

                    // 检查该建筑是否有"商业公司"作为租户
                    bool hasCommercialRenter = false;
                    DynamicBuffer<Renter> renters = renterAccessors[l];
                    for (int m = 0; m < renters.Length; m++)
                    {
                        if (m_CommercialCompanies.HasComponent(renters[m].m_Renter))
                        {
                            hasCommercialRenter = true;
                            break;
                        }
                    }

                    if (hasCommercialRenter)
                    {
                        continue;
                    }

                    // 空置物业：根据该建筑允许销售的资源类型，增加对应的 FreeProperties 计数
                    BuildingPropertyData buildingData = m_BuildingPropertyDatas[prefabEntity];
                    ResourceIterator validResourceIter = ResourceIterator.GetIterator();
                    while (validResourceIter.Next())
                    {
                        if ((buildingData.m_AllowedSold & validResourceIter.resource) != Resource.NoResource)
                        {
                            m_FreeProperties[EconomyUtils.GetResourceIndex(validResourceIter.resource)]++;
                        }
                    }
                }
            }

            // ----------------------------------------------------------------
            // Phase 4: 计算核心商业需求 (Main Demand Logic)
            // ----------------------------------------------------------------
            m_CompanyDemand.value = 0;
            m_BuildingDemand.value = 0;
            int currentPopulation = m_Populations[m_City].m_Population;

            // [Mod 改进点 1] 动态 Demand Buffer
            // 大地图上交通死角多，需要容忍一定的空置率才能让建筑需求不被卡死。
            // 人口 <= 2000: buffer 0（等同原版严格判定）
            // 人口 > 2000: 按人口线性增长，上限 200
            int dynamicBuffer = 0;
            if (currentPopulation > 2000)
            {
                dynamicBuffer = math.min(currentPopulation / 2000, 200);
            }

            // 计算每种资源的需求
            resourceIter = ResourceIterator.GetIterator();
            int validCommercialResourceCount = 0;

            while (resourceIter.Next())
            {
                Resource resource = resourceIter.resource;
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);

                // 过滤掉非商业资源或无效资源
                if (!EconomyUtils.IsCommercialResource(resourceIter.resource) || !m_ResourceDatas.HasComponent(m_ResourcePrefabs[resourceIter.resource]))
                {
                    continue;
                }

                // --- A. 计算税收影响 ---
                // 税率基准是10%，每高1%会降低需求
                float taxModifier = -0.05f * ((float)TaxSystem.GetCommercialTaxRate(resource, m_TaxRates) - 10f) * m_DemandParameters.m_TaxEffect.y;
                taxModifier += m_CommercialTaxEffectDemandOffset;

                // --- B. 计算资源的基础需求 ---
                if (resourceIter.resource != Resource.Lodging) // 非住宿类（普通商品）
                {
                    // === 1.5.7f 新版比率模型 ===
                    // ratio = 当前库存 / (1 + 总容量)，值域 [0, ~1]
                    // 当库存占比低于 StorageMinimum 时产生需求
                    float storageRatio = (float)m_CurrentAvailables[resIndex] / (1f + (float)m_TotalAvailables[resIndex]);
                    int baseDemand = math.max(0, Mathf.RoundToInt(
                        m_DemandParameters.m_CommercialStorageEffect *
                        (m_DemandParameters.m_CommercialStorageMinimum - storageRatio * 100f)));

                    // [Mod 改进点 2] 抑制加油站 (Petrochemicals)
                    // 加油站在大地图上容易过度建设，降低其需求灵敏度
                    if (resource == Resource.Petrochemicals)
                    {
                        baseDemand = (int)(baseDemand * 0.5f);
                    }

                    m_ResourceDemands[resIndex] = baseDemand;
                }
                else // 住宿类 (Lodging)
                {
                    // [Mod 改进点 3] 平滑旅馆需求曲线
                    // 原版是二值判断（缺就100，不缺就0），改用占用率平滑曲线
                    int requiredLodging = (int)((float)m_Tourisms[m_City].m_CurrentTourists * m_DemandParameters.m_HotelRoomPercentRequirement);
                    int currentLodging = m_Tourisms[m_City].m_Lodging.y;

                    if (currentLodging == 0 && requiredLodging > 0)
                    {
                        m_ResourceDemands[resIndex] = 100; // 没旅馆且有游客，必须建
                    }
                    else if (currentLodging > 0)
                    {
                        float occupancy = requiredLodging / (float)currentLodging;
                        // 占用率 70% 开始产生需求，100% 时需求达到 100
                        float lodgingDemandFloat = (occupancy - 0.7f) * 333f;
                        m_ResourceDemands[resIndex] = math.clamp((int)lodgingDemandFloat, 0, 100);
                    }
                    else
                    {
                        // 既没有游客也没有旅馆，给予低值避免锁死
                        m_ResourceDemands[resIndex] = 5;
                    }
                }

                // --- C. 应用税收修正 ---
                // 注意：不额外 clamp，允许低税率将需求推到 100 以上，归一化阶段自然压回
                m_ResourceDemands[resIndex] = Mathf.RoundToInt((1f + taxModifier) * (float)m_ResourceDemands[resIndex]);

                // 记录UI显示的税收因子
                int uiTaxImpact = Mathf.RoundToInt(100f * taxModifier);
                m_DemandFactors[11] += uiTaxImpact;

                // --- D. 汇总最终需求 ---
                if (m_ResourceDemands[resIndex] > 0)
                {
                    // 叠加整体公司入驻需求
                    m_CompanyDemand.value += m_ResourceDemands[resIndex];

                    // [Mod 改进点 1 应用] 使用 dynamicBuffer 代替原版的 <= 0 硬判断
                    // 当空置物业 - 寻找物业的公司 <= 缓冲值 且 需求足够强烈时，触发建筑需求
                    int freeVsWaiting = m_FreeProperties[resIndex] - m_Propertyless[resIndex];
                    m_BuildingDemands[resIndex] = ((freeVsWaiting <= dynamicBuffer && m_ResourceDemands[resIndex] > 10) ? m_ResourceDemands[resIndex] : 0);

                    if (m_BuildingDemands[resIndex] > 0)
                    {
                        m_BuildingDemand.value += m_BuildingDemands[resIndex];
                    }

                    // --- E. 更新UI因子 (Factors) ---
                    int buildingDemandContribution = ((m_BuildingDemands[resIndex] > 0) ? m_ResourceDemands[resIndex] : 0);
                    int baseDemandVal = m_ResourceDemands[resIndex];
                    int demandBeforeTax = baseDemandVal + uiTaxImpact;

                    if (resource == Resource.Lodging)
                        m_DemandFactors[9] += baseDemandVal;
                    else if (resource == Resource.Petrochemicals)
                        m_DemandFactors[16] += baseDemandVal;
                    else
                        m_DemandFactors[4] += baseDemandVal;

                    // Factors[13] 是 "Empty Buildings" 空置建筑负面因子
                    m_DemandFactors[13] += math.min(0, buildingDemandContribution - demandBeforeTax);

                    validCommercialResourceCount++;
                }
            }

            // ----------------------------------------------------------------
            // Phase 5: 归一化处理
            // ----------------------------------------------------------------
            m_DemandFactors[4] = ((m_DemandFactors[4] == 0) ? (-1) : m_DemandFactors[4]);

            // 极端情况：无人城市
            if (currentPopulation <= 0)
            {
                m_DemandFactors[4] = 0;
                m_DemandFactors[18] = m_BuildingDemand.value;
                m_DemandFactors[16] = 0;
            }

            // 没有任何商业地块时，清除空置建筑因子
            if (m_CommercialPropertyChunks.Length == 0)
            {
                m_DemandFactors[13] = 0;
            }

            // 取平均值作为最终的全市商业需求条 (0-100)
            m_CompanyDemand.value = ((validCommercialResourceCount != 0) ? math.clamp(m_CompanyDemand.value / validCommercialResourceCount, 0, 100) : 0);
            m_BuildingDemand.value = ((validCommercialResourceCount != 0 && isCommercialZoneUnlocked) ? math.clamp(m_BuildingDemand.value / validCommercialResourceCount, 0, 100) : 0);

            // 开发者作弊模式
            if (m_UnlimitedDemand)
            {
                m_BuildingDemand.value = 100;
                m_CompanyDemand.value = 100;
            }
        }
    }

}
