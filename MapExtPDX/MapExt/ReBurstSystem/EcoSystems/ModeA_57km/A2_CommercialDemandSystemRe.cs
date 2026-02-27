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

namespace MapExtPDX.ModeA
{
    /// <summary>
    /// 更新商业需求的作业。
    /// </summary>
    /// <remarks>
    /// 读取城市状态、税率、资源与物业数据，计算每种商业资源的公司需求与建筑需求。 
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
        [ReadOnly] public NativeArray<int> m_Propertyless; // 没有物业的公司数量（正在寻找办公地点的公司）

        public float m_CommercialTaxEffectDemandOffset; // 商业税收影响偏移量
        public bool m_UnlimitedDemand; // 作弊模式：无限需求

        // 添加一个常量来控制总物业数量的近似统计，用于计算空置率
        // 注意：原代码没有统计TotalProperties，我们只能通过Free和Propertyless估算，或者修改逻辑仅基于Free绝对值
        // 这里为了保持Struct结构不变，我们只优化逻辑算法

        public void Execute()
        {
            // ============= 配置中心 =============
            // 每个市民对商业服务的需求量（单位：商业服务容量）
            float kPerCitizenShoppingDemand = 0.3f;
            // ========= End of 配置中心 ==========

            // ----------------------------------------------------------------
            // Phase 1: 检查商业区是否已解锁
            // ----------------------------------------------------------------
            // 检查城市是否已经解锁了商业区（如果没有解锁，需求应锁死为0）
            bool isCommercialZoneUnlocked = false;
            for (int i = 0; i < this.m_UnlockedZoneDatas.Length; i++)
            {
                if (this.m_UnlockedZoneDatas[i].m_AreaType == AreaType.Commercial)
                {
                    isCommercialZoneUnlocked = true;
                    break;
                }
            }

            // ----------------------------------------------------------------
            // Phase 2: 初始化/重置计数器
            // ----------------------------------------------------------------
            // 遍历所有资源，将“空闲物业数”、“建筑需求”、“资源需求”归零
            ResourceIterator resourceIter = ResourceIterator.GetIterator();
            while (resourceIter.Next())
            {
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);
                this.m_FreeProperties[resIndex] = 0;
                this.m_BuildingDemands[resIndex] = 0;
                this.m_ResourceDemands[resIndex] = 0;
            }
            // 重置UI显示用的需求因子（如“税收过高”、“没有顾客”等提示）
            for (int j = 0; j < this.m_DemandFactors.Length; j++)
            {
                this.m_DemandFactors[j] = 0;
            }

            // ----------------------------------------------------------------
            // Phase 3: 统计空置商业物业 (Calculate Free Properties)
            // 遍历所有商业建筑，检查是否有租户（Renter），如果没有则计为空置
            // ----------------------------------------------------------------
            // 遍历所有商业建筑的数据块（Chunks）
            for (int k = 0; k < this.m_CommercialPropertyChunks.Length; k++)
            {
                ArchetypeChunk archetypeChunk = this.m_CommercialPropertyChunks[k];
                // 只有正在市场上待租的建筑才算
                if (!archetypeChunk.Has(ref this.m_PropertyOnMarketType))
                {
                    continue;
                }

                NativeArray<PrefabRef> prefabs = archetypeChunk.GetNativeArray(ref this.m_PrefabType);
                BufferAccessor<Renter> renterAccessors = archetypeChunk.GetBufferAccessor(ref this.m_RenterType);

                for (int l = 0; l < prefabs.Length; l++)
                {
                    // 获取商业建筑预制体实体
                    Entity prefabEntity = prefabs[l].m_Prefab;
                    // 如果该建筑没有该预制体，跳过
                    if (!this.m_BuildingPropertyDatas.HasComponent(prefabEntity))
                    {
                        continue;
                    }

                    // 检查该建筑是否有“商业公司”作为租户
                    bool hasCommercialRenter = false;
                    DynamicBuffer<Renter> renters = renterAccessors[l];
                    for (int m = 0; m < renters.Length; m++)
                    {
                        if (this.m_CommercialCompanies.HasComponent(renters[m].m_Renter))
                        {
                            hasCommercialRenter = true;
                            break;
                        }
                    }

                    // 如果已经有公司租了，就不算空闲物业，跳过
                    if (hasCommercialRenter)
                    {
                        continue;
                    }

                    // 如果是空置的，根据该建筑允许销售的资源类型，增加对应的FreeProperties计数
                    BuildingPropertyData buildingData = this.m_BuildingPropertyDatas[prefabEntity];
                    ResourceIterator validResourceIter = ResourceIterator.GetIterator();
                    while (validResourceIter.Next())
                    {
                        if ((buildingData.m_AllowedSold & validResourceIter.resource) != Resource.NoResource)
                        {
                            this.m_FreeProperties[EconomyUtils.GetResourceIndex(validResourceIter.resource)]++;
                        }
                    }
                }
            }

            // ----------------------------------------------------------------
            // Phase 4: 计算核心商业需求 (Main Demand Logic)
            // 基于税率、人口规模、现有供应量来计算
            // ----------------------------------------------------------------
            this.m_CompanyDemand.value = 0; // 公司入驻需求（租房子）
            this.m_BuildingDemand.value = 0; // 建设新楼需求（建房子）
            int currentPopulation = this.m_Populations[this.m_City].m_Population;

            // [改进点 1] 动态 Demand Buffer 计算
            // 人口 < 2000: buffer 0 (严格)
            // 人口 100,000: buffer 10 (宽松)
            // 甚至可以更大，取决于地图大小，上限设为 15 左右防止无脑铺
            // 大型城市存在交通死角和寻路延迟，必须容忍较高的空置数，否则需求起不来。
            int dynamicBuffer = 0;
            if (currentPopulation > 2000)
            {
                dynamicBuffer = math.clamp(currentPopulation / 2000, 10, 200);
            }

            // 计算每种资源的需求
            resourceIter = ResourceIterator.GetIterator();
            int validCommercialResourceCount = 0; // 用于计算平均值的分母

            while (resourceIter.Next())
            {
                Resource resource = resourceIter.resource;
                int resIndex = EconomyUtils.GetResourceIndex(resourceIter.resource);

                // 过滤掉非商业资源或无效资源
                if (!EconomyUtils.IsCommercialResource(resourceIter.resource) || !this.m_ResourceDatas.HasComponent(this.m_ResourcePrefabs[resourceIter.resource]))
                {
                    continue;
                }

                // --- A. 计算税收影响 ---
                //// 税率基准是10%，每高1%会降低需求
                float taxRate = (float)TaxSystem.GetCommercialTaxRate(resource, this.m_TaxRates);
                // 保持原逻辑，但确保数值稳定
                float taxModifier = -0.05f * (taxRate - 10f) * this.m_DemandParameters.m_TaxEffect.y;
                taxModifier += this.m_CommercialTaxEffectDemandOffset;

                // --- B. 计算资源的基础需求 (BUG 核心区域) ---
                if (resourceIter.resource != Resource.Lodging) // 非旅馆类（普通商品）
                {
                    //// ======== 问题代码：使用Log10计算目标容量 ========
                    //// [分析] 这里的逻辑是计算一个“期望的存量上限”(targetCap)。
                    //// 现在的公式是：如果人口>1000，则 2500 * Log10(0.01 * 人口)。
                    //// 然后需求 = 100 - (当前存量 - 期望存量) / 25。
                    //int targetAvailableCap = ((currentPopulation <= 1000) ? 2500 : (2500 * (int)Mathf.Log10(0.01f * (float)currentPopulation)));

                    // ====== [修复] 需求计算逻辑优化 ========
                    // [FIX] 不再使用 Log10，而是使用线性比例。
                    // 假设每个市民平均需要一定量的商业服务缓冲。
                    // 这里的除数因子 (e.g. 10f) 代表每10个市民产生1单位的服务需求/库存期望。
                    // 这个数值需要根据游戏的资源产出平衡进行调整，通常 1-10 之间。
                    // 原代码 Log10 导致百万人口时 Threshold 只有 10000。
                    // 我们改为：Threshold = Population * 0.5 (假设每人需要0.5单位的商品周转量)

                    // 为了安全起见，我们设定一个基础值加上人口系数
                    float perCapitaDemandFactor = kPerCitizenShoppingDemand; // 可调整系数，代表理想的人均商品库存量
                    int baseThreshold = 2500;
                    // 人口小于1000时按原逻辑固定2500供应量，超过则按人口比例线性增长
                    int dynamicThreshold = (currentPopulation <= 1000) ? baseThreshold
                    : baseThreshold + (int)(currentPopulation * perCapitaDemandFactor);

                    // === [改进点 2] 抑制加油站 (Petrochemicals) ===
                    // 如果是石油制品，人为提高其“虚拟库存量”或者降低其需求敏感度
                    int currentAvailable = this.m_CurrentAvailables[resIndex];
                    // Divisor 调整：为了避免需求过于“粘滞”，建议设个上限
                    // 例如：divisor最大不超过 500，这样即使在大城市，需求反应也足够灵敏
                    int rawDivisor = math.max(100, dynamicThreshold / 100);
                    int divisor = math.min(rawDivisor, 500); // 增加一个上限钳制

                    if (resource == Resource.Petrochemicals)
                    {
                        // 策略：让加油站更容易满足。
                        // 比如：只要有 30% 的期望库存，就算满足了。
                        dynamicThreshold = (int)(dynamicThreshold * 0.3f);

                        // 或者：增加除数，使其对短缺敏感（曲线更平缓）
                        // divisor /= 2;
                    }
                    else
                    {
                        divisor = math.min(rawDivisor, 2000); // 普通商业放宽，平滑需求
                    }

                    // 计算盈余（可能是负数，即短缺）
                    int availableSurplus = currentAvailable - dynamicThreshold;

                    // 计算需求：100 - (盈余 / 敏感度)
                    // 如果盈余很大，除数很小 -> 扣分巨大 -> 需求为0
                    int rawDemand = 100 - (availableSurplus / divisor);
                    // =================================================

                    // 应用动态缓冲，钳制需求为0-100
                    this.m_ResourceDemands[resIndex] = math.clamp(rawDemand, 0, 100);
                }
                else // 旅馆类 (Lodging) - 逻辑相对正常
                {
                    // 检查游客需求是否满足
                    int requiredLodging = (int)((float)this.m_Tourisms[this.m_City].m_CurrentTourists * this.m_DemandParameters.m_HotelRoomPercentRequirement);
                    int currentLodging = this.m_Tourisms[this.m_City].m_Lodging.y;

                    // === [改进点 3] 拯救旅馆 (平滑需求曲线) ===
                    // 不要用硬性的 > 0 判断
                    // 计算占用率： Occupancy = Required / Capacity
                    // 如果占用率超过 70%，就开始产生需求
                    // 如果占用率达到 100%，需求达到 100
                    if (currentLodging == 0 && requiredLodging > 0)
                    {
                        this.m_ResourceDemands[resIndex] = 100; // 没旅馆且有游客，必须建
                    }
                    else if (currentLodging > 0)
                    {
                        float occupancy = (float)requiredLodging / (float)currentLodging;
                        // 阈值 0.7 (70%)，斜率因子 333 (让 0.7->1.0 映射到 0->100)
                        float lodgingDemandFloat = (occupancy - 0.7f) * 333f;
                        this.m_ResourceDemands[resIndex] = math.clamp((int)lodgingDemandFloat, 0, 100);
                    }
                    // 既没有游客也没有旅馆，需求给予一个低值，避免潜在锁定为0
                    else
                    {
                        this.m_ResourceDemands[resIndex] = 5;
                    }
                    // === End of 改进点 3 ===
                }

                // --- C. 应用税收修正 ---
                this.m_ResourceDemands[resIndex] = (int)math.round((1f + taxModifier) * (float)this.m_ResourceDemands[resIndex]); // 改为math

                // 再次 Clamp 保证安全
                this.m_ResourceDemands[resIndex] = math.clamp(this.m_ResourceDemands[resIndex], 0, 100);

                // 记录UI显示的税收因子
                int uiTaxImpact = (int)math.round(100f * taxModifier);
                this.m_DemandFactors[11] += uiTaxImpact; // 改为math

                // --- D. 汇总最终需求 ---
                // --- 修复点 3：建筑需求逻辑优化 ---
                if (this.m_ResourceDemands[resIndex] > 0)
                {
                    // 叠加整体公司入驻需求
                    this.m_CompanyDemand.value += this.m_ResourceDemands[resIndex];

                    //// 如果没有空置物业了，才产生“建设新建筑”的需求
                    //// Propertyless 代表无物业的公司，FreeProperties 代表空物业。
                    //// 逻辑：如果 (空置物业 - 正在找房的公司) <= 0，说明房子不够了，需要新建。
                    //bool isBuildingShortage = (this.m_FreeProperties[resIndex] - this.m_Propertyless[resIndex] <= 0);
                    //this.m_BuildingDemands[resIndex] = (isBuildingShortage ? this.m_ResourceDemands[resIndex] : 0);

                    // === [应用改进点 1] 使用 dynamicBuffer ===
                    // 逻辑：空置物业 - 正在寻找物业的公司 <= 允许的缓冲值
                    int freeVsWaiting = this.m_FreeProperties[resIndex] - this.m_Propertyless[resIndex];

                    // 只有当商品需求比较强烈（例如 > 10）时，才考虑建设新楼
                    // 否则只增加公司入驻需求（填满现有建筑）
                    this.m_BuildingDemands[resIndex] = ((freeVsWaiting <= dynamicBuffer && this.m_ResourceDemands[resIndex] > 10) ? this.m_ResourceDemands[resIndex] : 0);
                    // === End of 改进点 1 ===

                    // 叠加整体建筑建设需求
                    if (this.m_BuildingDemands[resIndex] > 0)
                    {
                        this.m_BuildingDemand.value += this.m_BuildingDemands[resIndex];
                    }

                    // --- E. 更新UI因子 (Factors) ---
                    int buildingDemandContribution = ((this.m_BuildingDemands[resIndex] > 0) ? this.m_ResourceDemands[resIndex] : 0);
                    int baseDemandVal = this.m_ResourceDemands[resIndex];
                    int demandBeforeTax = baseDemandVal + uiTaxImpact; // 反推回税前需求

                    if (resource == Resource.Lodging)
                        this.m_DemandFactors[9] += baseDemandVal; // 旅馆需求
                    else if (resource == Resource.Petrochemicals)
                        this.m_DemandFactors[16] += baseDemandVal; // 石油需求
                    else
                        this.m_DemandFactors[4] += baseDemandVal; // 居民购物需求

                    // 这里的逻辑有点绕：Factors[13] 是 "Empty Buildings" (空置建筑负面因子)
                    // 如果需要新建(num5) 但 实际需求高(num7)，这里用来计算空置建筑对需求的压制
                    this.m_DemandFactors[13] += math.min(0, buildingDemandContribution - demandBeforeTax);

                    validCommercialResourceCount++;
                }
            }

            // 5. 归一化处理
            // 防止除以0，处理UI因子显示方向
            this.m_DemandFactors[4] = ((this.m_DemandFactors[4] == 0) ? (-1) : this.m_DemandFactors[4]);

            // 极端情况处理：无人城市
            if (currentPopulation <= 0)
            {
                this.m_DemandFactors[4] = 0;
                this.m_DemandFactors[18] = this.m_BuildingDemand.value; // 显示为基础需求
                this.m_DemandFactors[16] = 0;
            }

            // 如果地图上甚至没有任何商业地块，清除空置建筑因子
            if (this.m_CommercialPropertyChunks.Length == 0)
            {
                this.m_DemandFactors[13] = 0;
            }

            // 取平均值作为最终的全市商业需求条 (0-100)
            this.m_CompanyDemand.value = ((validCommercialResourceCount != 0) ? math.clamp(this.m_CompanyDemand.value / validCommercialResourceCount, 0, 100) : 0);
            this.m_BuildingDemand.value = ((validCommercialResourceCount != 0 && isCommercialZoneUnlocked) ? math.clamp(this.m_BuildingDemand.value / validCommercialResourceCount, 0, 100) : 0);

            // 开发者作弊模式
            if (this.m_UnlimitedDemand)
            {
                this.m_BuildingDemand.value = 100;
                this.m_CompanyDemand.value = 100;
            }
        }
    }

}

