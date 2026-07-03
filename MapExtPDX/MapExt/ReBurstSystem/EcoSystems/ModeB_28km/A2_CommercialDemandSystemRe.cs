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

namespace MapExtPDX.ModeB
{
    /// <summary>
    /// 更新商業需求的作業。
    /// </summary>
    /// <remarks>
    /// 讀取城市狀態、稅率、資源與物業資料，計算每種商業資源的公司需求與建築需求。
    /// 基於 1.5.7f 版本適配：非住宿資源使用比率模型 (CurrentAvailables / TotalAvailables)。
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

        // --- 輸出/讀寫資料 ---
        public NativeValue<int> m_CompanyDemand; // 公司入駐需求（決定是否生成新公司）
        public NativeValue<int> m_BuildingDemand; // 建築建設需求（決定是否蓋新樓）
        public NativeArray<int> m_DemandFactors; // 需求因子（用於 UI 顯示，如稅收影響、工人不足等）
        public NativeArray<int> m_FreeProperties; // 空置的商業物業數量（按資源分類）
        public NativeArray<int> m_ResourceDemands; // 各具體資源的需求值
        public NativeArray<int> m_BuildingDemands; // 各具體資源的建築需求

        [ReadOnly] public NativeArray<int> m_ProduceCapacity; // (未在邏輯中使用，但存在於結構體中)
        [ReadOnly] public NativeArray<int> m_CurrentAvailables; // 當前市場上可用的商品/服務量（庫存量）
        [ReadOnly] public NativeArray<int> m_TotalAvailables; // 各資源總可用容量（1.5.7f 新增）
        [ReadOnly] public NativeArray<int> m_Propertyless; // 沒有物業的公司數量（正在尋找營業地點的公司）

        public float m_CommercialTaxEffectDemandOffset; // 商業稅收影響偏移量
        public bool m_UnlimitedDemand; // 作弊模式：無限需求

        public void Execute()
        {
            // ----------------------------------------------------------------
            // Phase 1: 檢查商業區是否已解鎖
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
            // Phase 2: 初始化/重置計數器
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
            // Phase 3: 統計空置商業物業 (Calculate Free Properties)
            // ----------------------------------------------------------------
            foreach (var archetypeChunk in m_CommercialPropertyChunks)
            {
                // 只有正在市場上待租的建築才算
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

                    // 檢查該建築是否有「商業公司」作為租戶
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

                    // 空置物業：根據該建築允許銷售的資源類型，增加對應的 FreeProperties 計數
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
            // Phase 4: 計算核心商業需求 (Main Demand Logic)
            // ----------------------------------------------------------------
            m_CompanyDemand.value = 0;
            m_BuildingDemand.value = 0;
            int currentPopulation = m_Populations[m_City].m_Population;

            // [Mod 改進點 1] 動態 Demand Buffer
            // 大地圖上交通死角多，需要容忍一定的空置率才能讓建築需求不被卡死。
            // 人口 <= 2000: buffer 0（等同原版嚴格判定）
            // 人口 > 2000: 按人口線性增長，上限 80
            // [MODIFIED] 斜率由 pop/2000 收斂為 pop/5000、上限由 200 收斂為 80：
            //   原上限 200 意味著大城市可容忍多達 200 間空置商鋪才停建，容易造成商業過剩、空鋪連片；
            //   收斂後在保留大地圖交通容錯的同時，抑制過度建設。
            int dynamicBuffer = 0;
            if (currentPopulation > 2000)
            {
                dynamicBuffer = math.min(currentPopulation / 5000, 80);
            }

            // 計算每種資源的需求
            resourceIter = ResourceIterator.GetIterator();
            int validCommercialResourceCount = 0;

            while (resourceIter.Next())
            {
                Resource resource = resourceIter.resource;
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);

                // 過濾掉非商業資源或無效資源
                if (!EconomyUtils.IsCommercialResource(resourceIter.resource) || !m_ResourceDatas.HasComponent(m_ResourcePrefabs[resourceIter.resource]))
                {
                    continue;
                }

                // --- A. 計算稅收影響 ---
                // 稅率基準是 10%，每高 1% 會降低需求
                float taxModifier = -0.05f * ((float)TaxSystem.GetCommercialTaxRate(resource, m_TaxRates) - 10f) * m_DemandParameters.m_TaxEffect.y;
                taxModifier += m_CommercialTaxEffectDemandOffset;

                // --- B. 計算資源的基礎需求 ---
                if (resourceIter.resource != Resource.Lodging) // 非住宿類（普通商品）
                {
                    // === 1.5.7f 新版比率模型 ===
                    // ratio = 當前庫存 / (1 + 總容量)，值域 [0, ~1]
                    // 當庫存占比低於 StorageMinimum 時產生需求
                    float storageRatio = (float)m_CurrentAvailables[resIndex] / (1f + (float)m_TotalAvailables[resIndex]);
                    int baseDemand = math.max(0, Mathf.RoundToInt(
                        m_DemandParameters.m_CommercialStorageEffect *
                        (m_DemandParameters.m_CommercialStorageMinimum - storageRatio * 100f)));

                    // [Mod 改進點 2] 抑制加油站 (Petrochemicals)
                    // 加油站在大地圖上容易過度建設，降低其需求靈敏度
                    if (resource == Resource.Petrochemicals)
                    {
                        baseDemand = (int)(baseDemand * 0.5f);
                    }

                    m_ResourceDemands[resIndex] = baseDemand;
                }
                else // 住宿類 (Lodging)
                {
                    // [Mod 改進點 3] 平滑旅館需求曲線
                    // 原版是二值判斷（缺就 100，不缺就 0），改用占用率平滑曲線
                    int requiredLodging = (int)((float)m_Tourisms[m_City].m_CurrentTourists * m_DemandParameters.m_HotelRoomPercentRequirement);
                    int currentLodging = m_Tourisms[m_City].m_Lodging.y;

                    if (currentLodging == 0 && requiredLodging > 0)
                    {
                        m_ResourceDemands[resIndex] = 100; // 沒旅館且有遊客，必須建
                    }
                    else if (currentLodging > 0)
                    {
                        float occupancy = requiredLodging / (float)currentLodging;
                        // 占用率 70% 開始產生需求，100% 時需求達到 100
                        float lodgingDemandFloat = (occupancy - 0.7f) * 333f;
                        m_ResourceDemands[resIndex] = math.clamp((int)lodgingDemandFloat, 0, 100);
                    }
                    else
                    {
                        // 既沒有遊客也沒有旅館，給予低值避免鎖死
                        m_ResourceDemands[resIndex] = 5;
                    }
                }

                // --- C. 應用稅收修正 ---
                // 注意：不額外 clamp，允許低稅率將需求推到 100 以上，歸一化階段自然壓回
                m_ResourceDemands[resIndex] = Mathf.RoundToInt((1f + taxModifier) * (float)m_ResourceDemands[resIndex]);

                // 記錄 UI 顯示的稅收因子
                int uiTaxImpact = Mathf.RoundToInt(100f * taxModifier);
                m_DemandFactors[11] += uiTaxImpact;

                // --- D. 匯總最終需求 ---
                if (m_ResourceDemands[resIndex] > 0)
                {
                    // 疊加整體公司入駐需求
                    m_CompanyDemand.value += m_ResourceDemands[resIndex];

                    // [Mod 改進點 1 應用] 使用 dynamicBuffer 代替原版的 <= 0 硬判斷
                    // 當空置物業 - 尋找物業的公司 <= 緩衝值 且 需求足夠強烈時，觸發建築需求
                    int freeVsWaiting = m_FreeProperties[resIndex] - m_Propertyless[resIndex];
                    m_BuildingDemands[resIndex] = ((freeVsWaiting <= dynamicBuffer && m_ResourceDemands[resIndex] > 10) ? m_ResourceDemands[resIndex] : 0);

                    if (m_BuildingDemands[resIndex] > 0)
                    {
                        m_BuildingDemand.value += m_BuildingDemands[resIndex];
                    }

                    // --- E. 更新 UI 因子 (Factors) ---
                    int buildingDemandContribution = ((m_BuildingDemands[resIndex] > 0) ? m_ResourceDemands[resIndex] : 0);
                    int baseDemandVal = m_ResourceDemands[resIndex];
                    int demandBeforeTax = baseDemandVal + uiTaxImpact;

                    if (resource == Resource.Lodging)
                        m_DemandFactors[9] += baseDemandVal;
                    else if (resource == Resource.Petrochemicals)
                        m_DemandFactors[16] += baseDemandVal;
                    else
                        m_DemandFactors[4] += baseDemandVal;

                    // Factors[13] 是 "Empty Buildings" 空置建築負面因子
                    m_DemandFactors[13] += math.min(0, buildingDemandContribution - demandBeforeTax);

                    validCommercialResourceCount++;
                }
            }

            // ----------------------------------------------------------------
            // Phase 5: 歸一化處理
            // ----------------------------------------------------------------
            m_DemandFactors[4] = ((m_DemandFactors[4] == 0) ? (-1) : m_DemandFactors[4]);

            // 極端情況：無人城市
            if (currentPopulation <= 0)
            {
                m_DemandFactors[4] = 0;
                m_DemandFactors[18] = m_BuildingDemand.value;
                m_DemandFactors[16] = 0;
            }

            // 沒有任何商業地塊時，清除空置建築因子
            if (m_CommercialPropertyChunks.Length == 0)
            {
                m_DemandFactors[13] = 0;
            }

            // 取平均值作為最終的全市商業需求條 (0-100)
            m_CompanyDemand.value = ((validCommercialResourceCount != 0) ? math.clamp(m_CompanyDemand.value / validCommercialResourceCount, 0, 100) : 0);
            m_BuildingDemand.value = ((validCommercialResourceCount != 0 && isCommercialZoneUnlocked) ? math.clamp(m_BuildingDemand.value / validCommercialResourceCount, 0, 100) : 0);

            // 開發者作弊模式
            if (m_UnlimitedDemand)
            {
                m_BuildingDemand.value = 100;
                m_CompanyDemand.value = 100;
            }
        }
    }

}
