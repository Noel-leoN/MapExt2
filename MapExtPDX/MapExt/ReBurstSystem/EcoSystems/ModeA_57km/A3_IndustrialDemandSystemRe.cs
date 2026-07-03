// Game.Simulation.IndustrialDemandSystem
// 系統實例被多個外部系統呼叫，採用 Job 通用替換。

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

namespace MapExtPDX.ModeA
{
    /// <summary>
    /// 更新工業／辦公／倉儲需求的作業。
    /// </summary>
    /// <remarks>
    /// 讀取城市狀態、稅率、勞動力與物業資料，計算實體工業、辦公、倉儲三類的公司需求與建築需求。
    /// 基於 1.5.7f 版本適配：租戶判定改用 Renter buffer + CompanyData；市民消費改用 CityResourceUsages。
    /// </remarks>
    [BurstCompile]
    public struct UpdateIndustrialDemandJob : IJob
    {
        // ----------------- 輸入／輸出資料定義 --------------------
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ZoneData> m_UnlockedZoneDatas; // 已解鎖的分區資料

        // 區塊資料 (ArchetypeChunks) 存放實體
        [ReadOnly] public NativeList<ArchetypeChunk> m_IndustrialPropertyChunks; // 實體工業地塊 (製造業／倉儲)
        [ReadOnly] public NativeList<ArchetypeChunk> m_OfficePropertyChunks; // 辦公地塊 (無形產業)
        [ReadOnly] public NativeList<ArchetypeChunk> m_StorageCompanyChunks; // 倉儲公司實體
        [ReadOnly] public NativeList<ArchetypeChunk> m_CityServiceChunks; // 城市服務實體

        // 組件句柄 (TypeHandles)
        [ReadOnly] public EntityTypeHandle m_EntityType;
        [ReadOnly] public ComponentTypeHandle<PrefabRef> m_PrefabType;
        [ReadOnly] public ComponentTypeHandle<CityServiceUpkeep> m_ServiceUpkeepType;
        [ReadOnly] public ComponentTypeHandle<PropertyOnMarket> m_PropertyOnMarketType; // 正在招租的物業
        [ReadOnly] public BufferTypeHandle<Renter> m_RenterType; // [1.5.7f] 租戶緩衝區句柄

        // 組件查找 (ComponentLookups)
        [ReadOnly] public ComponentLookup<Population> m_Populations;
        [ReadOnly] public ComponentLookup<IndustrialProcessData> m_IndustrialProcessDatas;
        [ReadOnly] public ComponentLookup<PropertyRenter> m_PropertyRenters;
        [ReadOnly] public ComponentLookup<PrefabRef> m_Prefabs;
        [ReadOnly] public ComponentLookup<BuildingData> m_BuildingDatas; // 建築大小、旗標
        [ReadOnly] public ComponentLookup<BuildingPropertyData> m_BuildingPropertyDatas; // 建築類別屬性
        [ReadOnly] public ComponentLookup<Attached> m_Attached;
        [ReadOnly] public ComponentLookup<ResourceData> m_ResourceDatas;
        [ReadOnly] public ComponentLookup<StorageLimitData> m_StorageLimitDatas;
        [ReadOnly] public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingDatas;
        [ReadOnly] public ComponentLookup<CompanyData> m_CompanyDatas; // [1.5.7f] 判斷 Renter 是否為公司實體

        // 緩衝區查找 (BufferLookups)
        [ReadOnly] public BufferLookup<ServiceUpkeepData> m_ServiceUpkeeps;
        [ReadOnly] public BufferLookup<CityModifier> m_CityModifiers;
        [ReadOnly] public BufferLookup<InstalledUpgrade> m_InstalledUpgrades;
        [ReadOnly] public BufferLookup<ServiceUpkeepData> m_Upkeeps;

        // 全域參數
        public EconomyParameterData m_EconomyParameters;
        public DemandParameterData m_DemandParameters;
        [ReadOnly] public ResourcePrefabs m_ResourcePrefabs;
        [ReadOnly] public NativeArray<int> m_EmployableByEducation; // 按教育水平劃分的可用勞動力
        [ReadOnly] public NativeArray<int> m_TaxRates; // 稅率
        [ReadOnly] public Workplaces m_FreeWorkplaces; // 各學歷空缺崗位

        public Entity m_City;

        // ================================================
        // 輸出：全域需求 (UI 顯示用，0-100)
        // ================================================
        public NativeValue<int> m_IndustrialCompanyDemand; // 實體工業-企業經營意願總和
        public NativeValue<int> m_IndustrialBuildingDemand; // 實體工業-建築規劃需求 (黃色條)
        public NativeValue<int> m_StorageCompanyDemand; // 倉儲-企業經營意願總和
        public NativeValue<int> m_StorageBuildingDemand; // 倉儲-建築規劃需求
        public NativeValue<int> m_OfficeCompanyDemand; // 辦公-企業經營意願總和
        public NativeValue<int> m_OfficeBuildingDemand; // 辦公-建築規劃需求 (紫色條)

        // 輸出：UI 提示因子 (如：「受高稅收影響」、「受勞動力不足影響」)
        public NativeArray<int> m_IndustrialDemandFactors;
        public NativeArray<int> m_OfficeDemandFactors;

        // 輸出：每種具體資源的需求詳情 (供下游系統引用)
        public NativeArray<int> m_IndustrialCompanyDemands; // 具體資源的企業入駐意願
        public NativeArray<int> m_IndustrialBuildingDemands; // 具體資源的建築需求
        public NativeArray<int> m_StorageBuildingDemands;
        public NativeArray<int> m_StorageCompanyDemands;

        // 經濟統計資料
        [ReadOnly] public NativeArray<int> m_Productions; // 當前生產量
        [ReadOnly] public NativeArray<int> m_CompanyResourceDemands; // 企業資源需求／消耗量
        [ReadOnly] public NativeArray<CityProductionStatisticSystem.CityResourceUsage> m_CityResourceUsages; // [1.5.7f] 城市資源使用統計

        // 統計計算用臨時陣列
        public NativeArray<int> m_FreeProperties; // 可用空置物業
        [ReadOnly] public NativeArray<int> m_Propertyless; // 有公司但無物業的流浪公司數量

        public NativeArray<int> m_FreeStorages; // 空閒倉庫數量
        public NativeArray<int> m_Storages; // 現有倉庫數量
        public NativeArray<int> m_StorageCapacities; // 倉庫總容量 (單位)
        public NativeArray<int> m_ResourceDemands; // 資源總缺口 (計算中間值)

        public float m_IndustrialOfficeTaxEffectDemandOffset; // 稅收影響偏移量
        public bool m_UnlimitedDemand; // 作弊模式：無限需求

        public void Execute()
        {
            // ===============================================
            // 初始化
            // ===============================================
            // 原版倉儲需求參數：(改為區域變數以便修改)
            int kStorageProductionDemand = 2000; // 保持原版方式，保證生成倉儲公司需求
            int kStorageCompanyEstimateLimit = 864000;

            // [MODIFIED 1] 取得當前城市總人口，用於解決百萬人口的數值溢出問題
            int currentPopulation = m_Populations[m_City].m_Population;
            // 計算縮放因子：基準為 10,000 人。若是 1M 人口，縮放因子為 100。
            // 這樣 1000 人的勞動力缺口在 1M 人口城市會被視為等同於 1w 人口城市的 10 人缺口。
            float populationScaler = math.max(1f, currentPopulation / 10000f);

            // -----------------------------------------------------------------------
            // 1. 區域解鎖檢查
            // 檢查是否有已解鎖的工業區 (辦公包含在工業區，僅資源種類不同)。
            // 若沒有解鎖工業區，最終的工業建築需求將被強制為 0。
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
            // 2. 初始化資源需求與清零計數器
            // 遍歷所有資源，基於家庭和公司當前的消耗來初始化基礎需求。
            // -----------------------------------------------------------------------
            ResourceIterator resourceIter = ResourceIterator.GetIterator();
            while (resourceIter.Next())
            {
                // 資源索引和資料
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);
                ResourceData resData = m_ResourceDatas[m_ResourcePrefabs[resourceIter.resource]];

                // 無形產品 (辦公資源)
                if (EconomyUtils.IsOfficeResource(resourceIter.resource))
                {
                    // [1.5.7f] 使用 CityResourceUsages 中的市民消費資料替代舊的 HouseholdResourceDemands
                    int citizenUsage = m_CityResourceUsages[resIndex][CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens];
                    m_ResourceDemands[resIndex] = 1 + citizenUsage + m_CompanyResourceDemands[resIndex];
                }
                // 有形產品 (非辦公資源，實體工業資源)
                else
                {
                    // 製造業資源：若是非辦公需求且是工業原料／產品 (即產業鏈未起步)，給予 100 的基礎引導需求，否則使用實際公司需求
                    bool isBaseIndustrial =
                        EconomyUtils.IsIndustrialResource(resData, includeMaterial: false, includeOffice: false);
                    m_ResourceDemands[resIndex] = ((m_CompanyResourceDemands[resIndex] == 0 && isBaseIndustrial)
                        ? 100
                        : m_CompanyResourceDemands[resIndex]);
                }

                // 重置計數器
                m_FreeProperties[resIndex] = 0; // 市場上適合該資源的空房產
                m_Storages[resIndex] = 0; // 該資源的倉庫數量
                m_FreeStorages[resIndex] = 0; // 該資源的全域可用空閒倉庫位
                m_StorageCapacities[resIndex] = 0; // 該資源的全域最大儲存容量
            }

            // 重置需求因子 UI 資料
            for (int j = 0; j < m_IndustrialDemandFactors.Length; j++) m_IndustrialDemandFactors[j] = 0;
            for (int k = 0; k < m_OfficeDemandFactors.Length; k++) m_OfficeDemandFactors[k] = 0;

            // -----------------------------------------------------------------------
            // 3. 統計城市服務的資源消耗
            // 城市服務 (如發電廠、警局) 的維護也會產生資源需求。包括有形和無形資源。
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

                    // 3a. 基礎維護消耗
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

                    // 3b. 服務升級組件帶來的額外消耗
                    if (m_InstalledUpgrades.HasBuffer(serviceEntity))
                    {
                        DynamicBuffer<InstalledUpgrade> upgradeBuffer = m_InstalledUpgrades[serviceEntity];
                        for (int u = 0; u < upgradeBuffer.Length; u++)
                        {
                            // 若升級是關閉狀態，則跳過
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
            // 4. 統計現有倉儲容量 (Storage Companies)
            // 遍歷現有的倉儲公司，統計各資源的儲存能力。
            // 倉庫只處理有形資源。
            // -----------------------------------------------------------------------
            foreach (var storageChunk in m_StorageCompanyChunks)
            {
                NativeArray<Entity> entities = storageChunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> prefabs = storageChunk.GetNativeArray(ref m_PrefabType);

                // 遍歷每個倉儲公司實體
                for (int entIdx = 0; entIdx < entities.Length; entIdx++)
                {
                    Entity entity = entities[entIdx];
                    Entity prefab = prefabs[entIdx].m_Prefab;

                    // 僅處理有工業製造 (這裡包含有形和無形產品，但此處應指有形產品) 資料的倉庫
                    if (m_IndustrialProcessDatas.HasComponent(prefab))
                    {
                        // 取得該倉庫儲存的資源類型
                        int resIndex =
                            EconomyUtils.GetResourceIndex(m_IndustrialProcessDatas[prefab].m_Output.m_Resource);
                        m_Storages[resIndex]++; // 增加該資源的倉庫計數

                        StorageLimitData limitData = m_StorageLimitDatas[prefab];

                        // 檢查是否已經有租戶 (PropertyRenter)
                        if (!m_PropertyRenters.HasComponent(entity) ||
                            !m_Prefabs.HasComponent(m_PropertyRenters[entity].m_Property))
                        {
                            // 無租戶／空閒倉庫
                            // 這是一個「空殼」倉儲公司 (尚未完全啟用或沒有物業關聯)
                            // 邏輯註記：這裡 FreeStorages-- 是反直覺的。可能是為了後續計算「淨缺口」。
                            // 意為：雖然存在這個公司實體，但它還沒準備好，所以在計算「可用空間」時扣除計數？
                            // m_FreeStorages 代表有效空閒的倉庫數量；m_StorageCapacities 代表整體儲存容量
                            // 或者意為：該倉庫未入駐公司，不能算入可用倉庫；而倉庫已建好，可算入全域總儲存容量
                            m_FreeStorages[resIndex]--;
                            m_StorageCapacities[resIndex] += kStorageCompanyEstimateLimit;
                        }
                        else
                        {
                            // 有租戶，累加其實際容量
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
            // 5. 統計空閒工業／辦公地產 (辦公地產包含在工業地產內)
            // 查看市場上有哪些空房子 (PropertyOnMarket)，並記錄它們適合生產／儲存什麼資源。
            // -----------------------------------------------------------------------
            foreach (var chunk in m_IndustrialPropertyChunks)
            {
                // [1.5.7f] 必須是「市場上待租」的物業，且必須有 Renter buffer
                if (!chunk.Has(ref m_PropertyOnMarketType) || !chunk.Has(ref m_RenterType)) continue;

                NativeArray<Entity> entities = chunk.GetNativeArray(m_EntityType);
                NativeArray<PrefabRef> prefabs = chunk.GetNativeArray(ref m_PrefabType);
                BufferAccessor<Renter> renterAccessor = chunk.GetBufferAccessor(ref m_RenterType);

                for (int i = 0; i < prefabs.Length; i++)
                {
                    Entity prefab = prefabs[i].m_Prefab;
                    if (!m_BuildingPropertyDatas.HasComponent(prefab)) continue;

                    // [1.5.7f] 檢查是否已有公司租戶，有則不算空置
                    bool hasCompanyRenter = false;
                    DynamicBuffer<Renter> renters = renterAccessor[i];
                    for (int r = 0; r < renters.Length; r++)
                    {
                        if (m_CompanyDatas.HasComponent(renters[r].m_Renter))
                        {
                            hasCompanyRenter = true;
                            break;
                        }
                    }
                    if (hasCompanyRenter) continue;

                    BuildingPropertyData propData = m_BuildingPropertyDatas[prefab];

                    // 若是附屬建築，檢查父建築的生產限制
                    if (m_Attached.TryGetComponent(entities[i], out Attached attached) &&
                        m_Prefabs.TryGetComponent(attached.m_Parent, out PrefabRef parentPrefabRef) &&
                        m_BuildingPropertyDatas.TryGetComponent(parentPrefabRef.m_Prefab,
                            out BuildingPropertyData parentPropData))
                    {
                        propData.m_AllowedManufactured = (Resource)((long)propData.m_AllowedManufactured & (long)parentPropData.m_AllowedManufactured);
                    }

                    // 遍歷資源，標記該建築允許生產或儲存哪些資源
                    ResourceIterator allowIterator = ResourceIterator.GetIterator();
                    while (allowIterator.Next())
                    {
                        int resIndex = EconomyUtils.GetResourceIndex(allowIterator.resource);
                        // 可生產資源的建築 (含有少量儲存空間但不算倉庫)
                        if (((long)propData.m_AllowedManufactured & (long)allowIterator.resource) != 0L)
                        {
                            m_FreeProperties[resIndex]++;
                        }

                        // 允許倉儲的建築／倉庫 (算倉庫且只能儲存)
                        if (((long)propData.m_AllowedStored & (long)allowIterator.resource) != 0L)
                        {
                            m_FreeStorages[resIndex]++;
                        }
                    }
                }
            }

            // ---------------- 核心需求計算邏輯 -----------------

            // 記錄上一幀是否有辦公需求，用於平滑或遲滯邏輯
            bool wasOfficeBuildingDemandPositive = m_OfficeBuildingDemand.value > 0;

            // 重置本幀需求值
            m_IndustrialCompanyDemand.value = 0;
            m_IndustrialBuildingDemand.value = 0;
            m_StorageCompanyDemand.value = 0;
            m_StorageBuildingDemand.value = 0;
            m_OfficeCompanyDemand.value = 0;
            m_OfficeBuildingDemand.value = 0;

            int officeResourceCount = 0; // 計數：有多少種無形資源產生了需求
            int industrialResourceCount = 0; // 計數：有多少種有形資源產生了需求

            // -----------------------------------------------------------------------
            // [PERF] 勞動力盈餘原始值外提 (效能優化)
            // 五個教育等級的勞動力盈餘累加「不依賴當前資源」，原版卻放在資源迴圈 (約 40 種)
            // 內部重複計算約 40 次 (共 200 次迭代)，屬純冗餘。此處外提到迴圈外一次算完，
            // 迴圈內僅對這份快取的原始值套用 MapAndClaimWorkforceEffect 映射 (映射依賴逐資源的 taxEffectVal，
            // 故映射本身仍留在迴圈內)。行為完全等價，僅省去重複的整數除法與陣列存取。
            // -----------------------------------------------------------------------
            int rawLowEduSurplus = 0; // 低學歷 (等級 0-1) 勞動力盈餘原始累加值 (Industrial 偏好)
            int rawHighEduSurplus = 0; // 高學歷 (等級 2-4) 勞動力盈餘原始累加值 (Office 偏好)
            {
                float neutralUnemploymentRatio = m_DemandParameters.m_NeutralUnemployment / 100f;
                // 遍歷 5 個教育等級 (0-4)
                for (int eduLevel = 0; eduLevel < 5; eduLevel++)
                {
                    // 計算：可用勞動力 - 自然失業 - 現有空缺
                    int laborDelta = (int)(m_EmployableByEducation[eduLevel] * (1f - neutralUnemploymentRatio)) -
                                     m_FreeWorkplaces[eduLevel];

                    // 原邏輯為使用絕對數量計算
                    // 修正：按人口比例歸一化差值。防止百萬人口時數值過大。
                    // 將大城市的巨大數值縮放到原始設計預期的小數值範圍內。
                    int scaledLaborDelta = (int)(laborDelta / populationScaler);

                    if (eduLevel < 2) rawLowEduSurplus += scaledLaborDelta;
                    else rawHighEduSurplus += scaledLaborDelta;
                }
            }

            resourceIter = ResourceIterator.GetIterator();
            while (resourceIter.Next())
            {
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);
                if (!m_ResourceDatas.HasComponent(m_ResourcePrefabs[resourceIter.resource])) continue;

                ResourceData resData = m_ResourceDatas[m_ResourcePrefabs[resourceIter.resource]];
                bool isProduceable = resData.m_IsProduceable; // 可由原料生產
                bool isMaterial = resData.m_IsMaterial; // 採集業原料資源，不可生產只能採集
                bool isTradable = resData.m_IsTradable; // 可交易資源
                bool isOfficeResource = resData.m_Weight == 0f; // 無形商品 (辦公資源)

                // === A. 倉儲需求計算 (Storage Demand) ===
                // 可交易並且非無形資源
                if (isTradable && !isOfficeResource)
                {
                    int currentDemand = m_ResourceDemands[resIndex];
                    m_StorageCompanyDemands[resIndex] = 0;
                    m_StorageBuildingDemands[resIndex] = 0;

                    // 若需求超過閾值且當前容量不足，產生倉儲公司需求
                    if (currentDemand > kStorageProductionDemand && m_StorageCapacities[resIndex] < currentDemand)
                    {
                        m_StorageCompanyDemands[resIndex] = 1;
                    }

                    // 若沒有空閒倉庫位置，產生倉儲建築需求
                    if (m_FreeStorages[resIndex] < 0)
                    {
                        m_StorageBuildingDemands[resIndex] = 1;
                    }

                    m_StorageCompanyDemand.value += m_StorageCompanyDemands[resIndex];
                    m_StorageBuildingDemand.value += m_StorageBuildingDemands[resIndex];

                    // 更新 UI 因子：倉儲需求 (索引 17)
                    m_IndustrialDemandFactors[17] += math.max(0, m_StorageBuildingDemands[resIndex]);
                }

                if (!isProduceable) continue; // 無法生產的資源跳過後續計算

                // === B. 有形／無形產品基本生產需求計算 ===
                // 等同於企業經營意願計算 (Company Profitability/Demand)

                // 1. 基礎需求分
                float baseDemand = (isMaterial
                    ? m_DemandParameters.m_ExtractorBaseDemand
                    : m_DemandParameters.m_IndustrialBaseDemand);

                // 2. 市場供需比率 (Supply/Demand Ratio)
                // 需求越高，生產越少，比率越高，刺激需求
                float supplyDemandRatio = (1f + m_ResourceDemands[resIndex] - m_Productions[resIndex]) /
                                          (m_ResourceDemands[resIndex] + 1f);

                // 3. 套用特定城市修正 (Modifiers)：電子產品(有形)／軟體(無形)
                if (resourceIter.resource == Resource.Electronics)
                    CityUtils.ApplyModifier(ref baseDemand, cityModifiers,
                        CityModifierType.IndustrialElectronicsDemand);
                else if (resourceIter.resource == Resource.Software)
                    CityUtils.ApplyModifier(ref baseDemand, cityModifiers, CityModifierType.OfficeSoftwareDemand);


                // 4. 稅收影響 (Tax Effect)
                int taxRate = (isOfficeResource
                    ? TaxSystem.GetOfficeTaxRate(resourceIter.resource, m_TaxRates)
                    : TaxSystem.GetIndustrialTaxRate(resourceIter.resource, m_TaxRates));

                // 稅率低於 10% 產生正向刺激，高於 10% 產生負向抑制
                // 稅率偏移：(稅率 - 10%) * -0.05 * 敏感度。稅率高於 10% 降低需求，反之提升需求。
                float taxFactor = m_DemandParameters.m_TaxEffect.z * -0.05f * (taxRate - 10f);
                taxFactor += m_IndustrialOfficeTaxEffectDemandOffset;
                float taxEffectVal = 100f * taxFactor; // 放大用於計算

                // 5. 勞動力可用性影響 (Workforce Effect)
                // [MODIFIED 2] 原始盈餘值已於迴圈外算好 (rawLowEduSurplus / rawHighEduSurplus)，
                // 此處僅將其映射為需求分。映射區間依 taxEffectVal 正負而不同，故必須留在迴圈內。
                int highEduWorkerSurplus; // 高學歷勞動力得分 (Office 偏好)
                int lowEduWorkerSurplus; // 低學歷勞動力得分 (Industrial 偏好)

                // 注意：這裡傳入 scaled 後的原始值，因此 MapAndClaimWorkforceEffect 的 -2000~[Max] 範圍現在依然有效
                if (taxEffectVal > 0f)
                {
                    // [MODIFIED 3] 放開廉價勞力紅利上限，激活出口加工業玩法
                    highEduWorkerSurplus = (int)MapAndClaimWorkforceEffect(rawHighEduSurplus,
                        0f - math.max(10f + taxEffectVal, 10f), 25f);
                    lowEduWorkerSurplus = (int)MapAndClaimWorkforceEffect(rawLowEduSurplus,
                        0f - math.max(10f + taxEffectVal, 10f), 40f);
                }
                else
                {
                    // [MODIFIED 3] 同上
                    highEduWorkerSurplus = math.clamp(rawHighEduSurplus, -10, 25);
                    lowEduWorkerSurplus = math.clamp(rawLowEduSurplus, -10, 40);
                }

                // 6. 綜合計算總市場需求 (本地需求)
                float marketDemand = 50f * math.max(0f, baseDemand * supplyDemandRatio); // 市場拉動

                // 7. 計算最終公司需求分數 (Company Demand)
                int calculatedCompanyDemand;

                // 無形資源需求 (辦公)
                if (isOfficeResource)
                {
                    // 辦公：市場 + 稅收 + 高學歷
                    calculatedCompanyDemand = (marketDemand > 0f)
                        ? Mathf.RoundToInt(marketDemand + taxEffectVal + highEduWorkerSurplus)
                        : 0;
                    calculatedCompanyDemand = math.clamp(calculatedCompanyDemand, 0, 100);

                    m_IndustrialCompanyDemands[resIndex] = calculatedCompanyDemand; // 更新對應資源的公司總需求
                    m_OfficeCompanyDemand.value += calculatedCompanyDemand; // 累加到辦公公司總需求

                    officeResourceCount++;
                }
                else
                {
                    // 工業：市場 + 稅收 + 全學歷
                    calculatedCompanyDemand =
                        Mathf.RoundToInt(marketDemand + taxEffectVal + highEduWorkerSurplus + lowEduWorkerSurplus);
                    calculatedCompanyDemand = math.clamp(calculatedCompanyDemand, 0, 100);

                    m_IndustrialCompanyDemands[resIndex] = calculatedCompanyDemand; // 更新對應資源的公司總需求
                    // calculatedCompanyDemand 已 clamp 至 0..100，累加不可能溢出，直接相加即可
                    m_IndustrialCompanyDemand.value += calculatedCompanyDemand; // 累加到工業公司總需求

                    if (!isMaterial) industrialResourceCount++; // 僅限非原料類資源
                }

                // === C. 建築需求計算 (Building Demand / Zoning Demand) ===
                // 將新公司入駐需求轉換為建築需求 (扣除空置)
                // 遊戲中 1 公司佔 1 建築；

                // 遍歷每種資源，若有資源需求才考慮建築
                if (m_ResourceDemands[resIndex] > 0)
                {
                    int demand = m_IndustrialCompanyDemands[resIndex];
                    // 短缺數量 = 流浪公司 - 當前資源空閒資產
                    // (流浪公司全部入駐所需要空閒資產)
                    int propShortage = m_Propertyless[resIndex] - m_FreeProperties[resIndex];
                    // 設置當前資源對應建築需求
                    m_IndustrialBuildingDemands[resIndex] = (demand > 0) switch
                    {
                        true when !isMaterial => propShortage >= 0 ? 50 : 0, // 短缺賦予需求
                        true => 1,
                        _ => 0,
                    };

                    // 累加總建築需求
                    if (m_IndustrialBuildingDemands[resIndex] > 0)
                    {
                        int finalBuildingDemand = m_IndustrialCompanyDemands[resIndex];

                        if (isOfficeResource)
                            m_OfficeBuildingDemand.value += finalBuildingDemand; // 使用公司意願強度作為權重
                        else if (!isMaterial)
                            m_IndustrialBuildingDemand.value += finalBuildingDemand;
                    }
                }
                // 資源需求為 0 時
                else
                {
                    m_IndustrialBuildingDemands[resIndex] = 0;
                }

                // === D. 填充 UI 需求因子 (Demand Factors) ===
                // 這些索引對應 UI 上的提示條 (例如：2=Educated Workers, 11=Tax, 13=Demand)

                // 若是原料採集業 (Extractor)，不由一般工業／辦公 UI 需求條顯示，跳過
                if (isMaterial) continue;

                // 辦公資源需求
                if (isOfficeResource)
                {
                    // 僅當有上一幀需求 或 當前有強勁新需求時才更新 UI，避免 UI 閃爍
                    if (!wasOfficeBuildingDemandPositive || (m_IndustrialBuildingDemands[resIndex] > 0 &&
                                                             m_IndustrialCompanyDemands[resIndex] > 0))
                    {
                        m_OfficeDemandFactors[2] += highEduWorkerSurplus; // High Skill Labor
                        m_OfficeDemandFactors[4] += (int)marketDemand; // Local Demand/Market
                        m_OfficeDemandFactors[11] += (int)taxEffectVal; // Taxes
                        m_OfficeDemandFactors[13] += m_IndustrialBuildingDemands[resIndex]; // Building Demand
                    }
                }

                // 有形產品 (工業) 資源需求
                else
                {
                    m_IndustrialDemandFactors[2] += highEduWorkerSurplus;
                    m_IndustrialDemandFactors[1] += lowEduWorkerSurplus; // Low Skill Labor
                    m_IndustrialDemandFactors[4] += (int)marketDemand;
                    m_IndustrialDemandFactors[11] += (int)taxEffectVal;
                    m_IndustrialDemandFactors[13] += m_IndustrialBuildingDemands[resIndex];
                }
            }

            // ---------------- 後處理與歸一化 ----------------
            // -----------------------------------------------------------------------
            // 7. 後處理與修正
            // -----------------------------------------------------------------------

            // a. 處理因子顯示邏輯：若為 0 則設為 -1 (可能用於隱藏 UI 條)，否則保持原值
            m_OfficeDemandFactors[4] = ((m_OfficeDemandFactors[4] == 0) ? (-1) : m_OfficeDemandFactors[4]);
            m_IndustrialDemandFactors[4] = ((m_IndustrialDemandFactors[4] == 0) ? (-1) : m_IndustrialDemandFactors[4]);
            m_IndustrialDemandFactors[13] =
                ((m_IndustrialDemandFactors[13] == 0) ? (-1) : m_IndustrialDemandFactors[13]);
            m_OfficeDemandFactors[13] = ((m_OfficeDemandFactors[13] == 0) ? (-1) : m_OfficeDemandFactors[13]);

            // b. 若城市沒有人口，強制將市場需求因子置為 0
            if (m_Populations[m_City].m_Population <= 0)
            {
                m_OfficeDemandFactors[4] = 0;
                m_IndustrialDemandFactors[4] = 0;
            }

            // c. 若地圖上完全沒有對應的特定產業分區，將「建築需求」因子從 13 轉移到索引 18 (可能是「無分區」提示)，並清空原因子
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

            // d. 倉儲建築需求指數平滑增強 (Power 0.75)
            m_StorageBuildingDemand.value = Mathf.CeilToInt(math.pow(20f * m_StorageBuildingDemand.value, 0.75f));

            // e. 歸一化需求值：乘以 2 並除以有效資源種類數，求平均強度
            // 工業建築總需求
            //=========== 修正：增加除零判斷 =============
            if (industrialResourceCount > 0)
                m_IndustrialBuildingDemand.value = (hasIndustrialZoneUnlocked
                    ? (2 * m_IndustrialBuildingDemand.value / industrialResourceCount)
                    : 0);

            // === 辦公需求歸一化（兩處改動性質不同，勿混為一談）===
            //
            // 【改動 A：m_OfficeCompanyDemand 去平方 —— 屬「潔癖式正確」，行為增益趨近於零】
            //   原版寫法：m_OfficeCompanyDemand.value *= 2 * m_OfficeCompanyDemand.value / count
            //   對照同段落 m_IndustrialBuildingDemand / m_StorageBuildingDemand 皆為乾淨的「= 」賦值，
            //   唯獨此行誤用「*=」，使結果變成 value = value × (2×value/count)，即平方級增長。
            //   幾乎可確定是複製貼上後漏改運算子的「無意手誤」，非設計、非掩飾：
            //   officeCompanyDemand 在全 Game.dll 僅被 IndustrialSpawnSystem 當作
            //   「(industrial + storage + office) > 0」的布林開關使用（決定該幀是否生成新公司），
            //   對正整數平方後仍為正，布林結果不變 → 無任何可見症狀（UI／建物數／存檔皆不受影響），
            //   故原廠 QA 復現不出、長期未修。此處改為線性只是消除隱患，實際手感幾乎無變化。
            //
            // 【改動 B：m_OfficeBuildingDemand 補歸一化 —— 這才是真正改變辦公生長速率的實質改動】
            //   玩家看到的辦公需求條與實際蓋樓走的是 officeBuildingDemand（見 CityInfoUISystem）。
            //   原版此值為所有辦公資源 buildingDemand 的「裸累加」後直接 clamp(0,100)，
            //   極易長期頂滿 100 → 辦公無腦瘋長。此處補一層「2×平均」歸一化，
            //   將其由「求和」改為「求平均強度」，辦公建築需求方才理性、不再恆定觸頂。
            //   注意：此行偏離原版行為，會影響辦公密度實際生長速率，調參時以此為準。
            if (officeResourceCount > 0)
            {
                // 改動 A：線性化，僅消除平方隱患（下游只作布林用，行為近乎不變）
                m_OfficeCompanyDemand.value = 2 * m_OfficeCompanyDemand.value / officeResourceCount;

                // 改動 B：新增歸一化，實質抑制辦公需求觸頂（真正影響手感的一行）
                m_OfficeBuildingDemand.value = (m_OfficeBuildingDemand.value > 0)
                    ? (2 * m_OfficeBuildingDemand.value / officeResourceCount)
                    : 0;
            }

            // f. 建築需求最終鉗位到 0-100
            m_IndustrialBuildingDemand.value = math.clamp(m_IndustrialBuildingDemand.value, 0, 100);
            m_OfficeBuildingDemand.value = math.clamp(m_OfficeBuildingDemand.value, 0, 100);

            // g. 開發者作弊模式
            if (m_UnlimitedDemand)
            {
                m_OfficeBuildingDemand.value = 100;
                m_IndustrialBuildingDemand.value = 100;
            }
        }

        // ------------------ 輔助方法 ------------------

        /// <summary>
        /// 映射勞動力效應值。
        /// 將原始的勞動力盈餘／赤字映射到一個合理的分數區間。
        /// </summary>
        /// <param name="value">原始勞動力差值</param>
        /// <param name="min">映射的最小分數</param>
        /// <param name="max">映射的最大分數</param>
        /// <returns>映射後的需求影響分</returns>
        // 保持原有的 Map 函數不變，透過 Scaler 適配它的輸入範圍
        private float MapAndClaimWorkforceEffect(float value, float min, float max)
        {
            if (value < 0f)
            {
                // 注意這裡期望輸入是 -2000
                float valueToClamp = math.unlerp(-2000f, 0f, value);
                valueToClamp = math.clamp(valueToClamp, 0f, 1f);
                return math.lerp(min, 0f, valueToClamp);
            }

            // 注意這裡期望輸入是 20
            float valueToClamp2 = math.unlerp(0f, 20f, value);
            valueToClamp2 = math.clamp(valueToClamp2, 0f, 1f);
            return math.lerp(0f, max, valueToClamp2);
        }
    }
}
