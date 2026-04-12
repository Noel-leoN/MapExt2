// Game.Simulation.ResidentialDemandSystem
// 系统实例被多个外部系统调用，采用Job通用替换。

using Colossal.Collections;
using Game.Buildings;
using Game.City;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.Triggers;
using Game.Zones;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

// using UnityEngine; // 使用Unity.Mathematics代替以符合Burs


namespace EconomyEX.Systems
{
    /// <summary>
    /// ResidentialDemandSystem居民需求系统
    /// 经济/人口系统核心，源头生成居民建筑需求与家庭迁入需求。
    /// 原系统问题：
    /// 1.几乎全部采用硬编码绝对值判断，当人口规模扩大时，需求计算严重失衡；
    /// 2.部分逻辑存在严重缺陷，如空置率逻辑、学生效应等；
    /// 3.建筑需求与家庭需求混淆，比如空置率高时建筑需求砍至0，同时连带家庭需求砍至0；
    /// </summary>
    /// 【参考模型】
    /// 城市人口增长和房地产发展需求动力分析模型：
    /// (原系统属于相当简陋的模型并且数值不尽合理)
    /// 1. 核心驱动层 (Primary Drivers) - 占比约 60%
    /// 就业机会与产业结构(30%)： 有没有好工作是人来的根本原因。
    /// 游戏对应：空闲工作机会。
    /// 宏观经济与金融环境(20%)： 利率高低、信贷宽松程度直接决定房地产能否开发。
    /// 游戏对应：税收和独特银行建筑、政策。
    /// 基础设施与交通(10%)： 地铁、高铁、机场的通达性。
    /// 游戏对应：公共交通、道路密度。
    /// 2. 调节与限制层(Secondary Regulators) - 占比约 30%
    /// 住房成本与生活成本(15%)： 房价太高会挤出人口（负面），也会吸引投资（正面）。
    /// 游戏对应：地价、商业发展指数
    /// 政府政策与规划(10%)： 区域规划（Zoning）、学区划分。
    /// 游戏对应：教育水平、区域政策
    /// 人口结构(5%)： 老龄化程度、出生率、移民政策。
    /// 游戏对应：人口年龄构成、政策。
    /// 3. 摩擦与环境层(Tertiary Friction Factors) - 占比约 10%
    /// 治安与公共安全(4%)：
    /// 医疗卫生(3%)：
    /// 环境质量与气候(3%)： 空气污染、气候舒适度。
    [BurstCompile]
    public struct UpdateResidentialDemandJob : IJob
    {
        // ================= 输入数据 (只读) =================
        [ReadOnly] public NativeList<Entity> m_UnlockedZonePrefabs; // 已解锁的区域类型
        [ReadOnly] public ComponentLookup<Population> m_Populations; // 人口组件查找
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneDatas; // 区域数据查找
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas; // 区域属性查找
        [ReadOnly] public NativeList<DemandParameterData> m_DemandParameters; // 需求参数配置（全局参数）
        [ReadOnly] public NativeArray<int> m_StudyPositions; // 1-4级教育的空闲学位数量
        [ReadOnly] public NativeArray<int> m_TaxRates; // 0-4级学历的税率
        [ReadOnly] public float m_UnemploymentRate; // 失业率
        public Entity m_City; // 城市实体引用

        // ================= 输出与状态数据 =================
        public NativeValue<int> m_HouseholdDemand; // 输出：总家庭迁入需求 (基础值)
        public NativeValue<int3> m_BuildingDemand; // 输出：建筑需求向量 (x:低, y:中, z:高)

        // UI 显示用的因子 (决定需求面板上的提示信息，如"税收太高"、"空置房屋多")
        public NativeArray<int> m_LowDemandFactors;
        public NativeArray<int> m_MediumDemandFactors;
        public NativeArray<int> m_HighDemandFactors;

        public CountHouseholdDataSystem.HouseholdData m_HouseholdCountData; // 包含无家可归数据
        public CountResidentialPropertySystem.ResidentialPropertyData m_ResidentialPropertyData; // 空置房与总房产数据
        public Workplaces m_FreeWorkplaces; // 空闲岗位
        public Workplaces m_TotalWorkplaces; // 总岗位
        public NativeQueue<TriggerAction> m_TriggerQueue; // 触发器队列（如教程、音效）

        public float2 m_ResidentialDemandWeightsSelector; // 权重选择器 (x:负值权重, y:正值权重)
        public bool m_UnlimitedDemand; // 作弊模式：无限需求

        // ================= 核心逻辑 =================
        public void Execute()
        {
            // === 配置中心 ===
            // 家庭需求全局因子;原始值=1f
            float kHouseholdDemandFactor = 1f;

            // 建筑需求全局因子；原始值=1f
            float buildingLowFactor = 1f; // 低密度住宅
            float buildingMedFactor = 1f; // 中密度住宅
            float buildingHighFactor = 1f; // 高密度住宅

            // --- 基础权重 (决定各因子对总分的影响力) ---
            float kHappinessWeight = 1.0f; // 幸福度权重
            float kTaxWeight = 1.0f; // 税收权重
            float kHomelessPenaltyWeight = 1.0f; // 无家可归(负面)权重
            float kHomelessBonusWeight = 1.0f; // 无家可归(高密度正面)权重
            float ksimJobWeight = 1.0f; // 简单工作就业权重
            float kcomJobWeight = 1.0f; // 复杂工作就业权重
            float kStudentWeight = 1.0f; // 教育资源权重
            float kUnemploymentWeight = 1.0f; // 失业率权重

            // 空置率影响设定
            // 1. 目标健康空置率 (比如 5%，参考国际平均水平)
            float kTargetVacancyRate = 0.05f;
            // 2. 严重空置率 25% (超过此值强制停止建设)
            float kPanicVacancyRate = 0.25f;
            // 3. 空置率敏感度 
            // 差值：例如 目标0.05 - 实际0.10 = -0.05(空置太多，降低需求)
            // 放大倍数 2000f 意味着 1% 的偏差调整约 20点需求
            // 预设为1500f,增加将加强空置率奖惩，减少将削弱
            float kVacancySensitivity = 1500f;
            // 4. 虚拟缓冲
            // 在计算比率时，分母加上这个值。
            // 作用：在新城市(房产总数<100)时，大幅稀释空置率的波动。
            // 例如：只有10套房，空了10套。无缓冲=100%空置(崩盘)。有缓冲(60)=10/70=14%空置(可控)。
            float kVirtualHousingBuffer = 150f;
            // 5. 权重系数
            // 空置率低影响建筑权重
            // float kBuildWeight = 1.0f;
            // 空置率高吸引移民权重
            // float kMoveInWeight = 0.5f;
            // [MODIFIED] 低密度专属较温和的敏感度
            float kVacancySensitivityLow = 500f;

            // 无家可归中性率 (全球通行标准一般容忍度0.05% )
            float kNeutralHomelessRate = 0.0005f;
            // =================== 配置中心 ====================

            // A. 检查已解锁的密度类型

            bool3 unlockedDensities = default(bool3);
            foreach (Entity prefab in m_UnlockedZonePrefabs)
            {
                ZoneData zoneData = m_ZoneDatas[prefab];
                if (zoneData.m_AreaType == AreaType.Residential)
                {
                    ZonePropertiesData zoneProp = m_ZonePropertiesDatas[prefab];
                    switch (PropertyUtils.GetZoneDensity(zoneData, zoneProp))
                    {
                        case ZoneDensity.Low: unlockedDensities.x = true; break;
                        case ZoneDensity.Medium: unlockedDensities.y = true; break;
                        case ZoneDensity.High: unlockedDensities.z = true; break;
                    }
                }
            }

            // 获取房产基础数据
            int3 freeProperties = m_ResidentialPropertyData.m_FreeProperties;
            int3 totalProperties = m_ResidentialPropertyData.m_TotalProperties;
            DemandParameterData paramsData = m_DemandParameters[0];

            // 计算人口数及基础因子
            Population cityPopulation = m_Populations[m_City];
            int popCount = math.max(1, cityPopulation.m_Population); // 防止除零

            // B.计算各类基础因子

            // --- [新城市红利] ---
            // 人口越少，红利越高。超过20,000人口后该值为0。初始约20。
            float populationBonusFactor = 20f - math.smoothstep(0f, 20f, cityPopulation.m_Population / 20000f);

            // --- [教育因子] ---
            // 计算教育容量 (累加1-4级所有学位)
            // 原版逻辑在1000个学额时封顶毫无意义
            // 改为假设理想状态是覆盖 20% 的人口 (模拟学龄人口)            
            int totalStudentSlots = 0;
            for (int j = 1; j <= 4; j++)
            {
                totalStudentSlots += m_StudyPositions[j];
            }

            float studentCoverage = totalStudentSlots / (popCount * 0.2f); // 假设20%人口上学
            float studentFactor = paramsData.m_StudentEffect * math.clamp(studentCoverage * 5f, 0f, 5f);

            // --- [幸福度因子] ---
            // 平均幸福度 vs 最低幸福度阈值
            // 采用相对值，无需修改
            int effectiveHappiness = math.max(paramsData.m_MinimumHappiness, cityPopulation.m_AverageHappiness);
            float happinessFactor = paramsData.m_HappinessEffect *
                                    (effectiveHappiness - paramsData.m_NeutralHappiness);

            // --- [税收因子] ---
            // 计算所有学历等级的平均税率与 10% 的差值
            // 如果税率>10%，因子为负；税率<10%，因子为正。
            // 采用相对值，无需修改
            float avgTaxDeviation = 0f;
            for (int k = 0; k < 5; k++)
            {
                avgTaxDeviation += -(TaxSystem.GetResidentialTaxRate(k, m_TaxRates) - 10);
            }

            float taxFactor = paramsData.m_TaxEffect.x * (avgTaxDeviation / 5f);

            // --- [就业率因子] ---
            // 修复(改为比率) ---
            // 计算空缺职位比例。如果空缺率 > 中性值(比如10%)，则有加成。
            // 就业空缺率中位数
            float neutralJobRate = paramsData.m_NeutralAvailableWorkplacePercentage / 100f;

            float totalSimpJobs = math.max(1f, m_TotalWorkplaces.SimpleWorkplacesCount);
            float totalCompJobs = math.max(1f, m_TotalWorkplaces.ComplexWorkplacesCount);

            float simpJobRate = m_FreeWorkplaces.SimpleWorkplacesCount / totalSimpJobs;
            float compJobRate = m_FreeWorkplaces.ComplexWorkplacesCount / totalCompJobs;

            // 放大倍数设为 100f，意味着每 1% 的额外空缺提供一定点数的吸引力
            float simpleJobFactor = paramsData.m_AvailableWorkplaceEffect * (simpJobRate - neutralJobRate) * 100f;
            simpleJobFactor = math.clamp(simpleJobFactor, 0f, 40f);

            float complexJobFactor = paramsData.m_AvailableWorkplaceEffect * (compJobRate - neutralJobRate) * 100f;
            complexJobFactor = math.clamp(complexJobFactor, 0f, 20f);

            // --- [失业率因子] ---
            // [MODIFIED] 修正：自然失业率(NAIRU)强制为 4.5% (东亚+欧美紧凑型)，抛弃原版内置20%魔幻参数影响
            // (中性失业率 - 当前失业率)。如果当前失业率高，结果为负，降低需求。
            float unemploymentFactor = 4.5f - m_UnemploymentRate;
            if (unemploymentFactor < 0f)
            {
                // [MODIFIED] 重拳出击：突破NAIRU时，成倍扣减家庭需求，截断失业潮人口涌入
                unemploymentFactor *= 2.5f;
            }

            //--- [流浪人口因子] ---
            // 修复：改为比例
            // 避免大城市因绝对数量高而受到不合理的惩罚            
            float homelessRate = m_HouseholdCountData.m_HomelessHouseholdCount / (float)popCount;
            // 归一化：如果流浪率是中性率的2倍，则系数为2。
            float homelessRatioNormalized = homelessRate / kNeutralHomelessRate;
            // HouseholdDemand 负面惩罚 (无家可归太多降低城市吸引力) 
            float homelessPenalty = (0f - paramsData.m_HomelessEffect) * math.clamp(homelessRatioNormalized, 0f, 5f);
            // BuildingDemand正面需求 (无家可归的人急需住房，主要推高高密度/廉租房需求)
            float homelessBonus = paramsData.m_HomelessEffect * math.clamp(homelessRatioNormalized, 0f, 2f);

            // C. 应用权重 (加权处理)
            populationBonusFactor = GetFactorValue(populationBonusFactor, m_ResidentialDemandWeightsSelector);
            happinessFactor = GetFactorValue(happinessFactor * kHappinessWeight, m_ResidentialDemandWeightsSelector);
            homelessPenalty =
                GetFactorValue(homelessPenalty * kHomelessPenaltyWeight, m_ResidentialDemandWeightsSelector);
            homelessBonus = GetFactorValue(homelessBonus * kHomelessBonusWeight,
                m_ResidentialDemandWeightsSelector);
            taxFactor = GetFactorValue(taxFactor * kTaxWeight, m_ResidentialDemandWeightsSelector);
            simpleJobFactor = GetFactorValue(simpleJobFactor * ksimJobWeight, m_ResidentialDemandWeightsSelector);
            complexJobFactor = GetFactorValue(complexJobFactor * kcomJobWeight, m_ResidentialDemandWeightsSelector);
            studentFactor = GetFactorValue(studentFactor * kStudentWeight, m_ResidentialDemandWeightsSelector);
            unemploymentFactor =
                GetFactorValue(unemploymentFactor * kUnemploymentWeight, m_ResidentialDemandWeightsSelector);

            // D. 计算总家庭迁入需求 (Household Demand)
            // 基础池子，决定了有多少人想进城
            // 人口/幸福度/税收/失业率/工作机会/学生资源/无家可归惩罚等综合影响
            // 无家可归加成只影响建筑需求，不影响家庭需求
            float baseHouseholdScore = populationBonusFactor + happinessFactor + homelessPenalty + taxFactor +
                                       unemploymentFactor + studentFactor + math.max(simpleJobFactor, complexJobFactor);
            // 限制在 0-200 之间
            m_HouseholdDemand.value = (int)math.clamp(baseHouseholdScore * kHouseholdDemandFactor, 0f, 200f);

            // E. 计算空置率因子 (Vacancy Logic)            
            //============================================================================
            // --- 修复 6: 空置率惩罚修正：动态空置率逻辑(核心修改)
            // 原逻辑理想空闲量极低且为硬编码(5,10,10)，导致房屋过剩时需求被严重压制
            // 新逻辑使得需求计算基于总房产的百分比，而不是固定数值
            //============================================================================

            // 计算空置率影响
            // [MODIFIED] 低密度因为自身建筑容量小，使用专属的温和敏感度(kVacancySensitivityLow)，避免需求大起大落
            int offsetLow = GetSmoothedVacancyOffset(freeProperties.x, totalProperties.x, kVirtualHousingBuffer,
                kTargetVacancyRate, kVacancySensitivityLow);
            int offsetMed = GetSmoothedVacancyOffset(freeProperties.y, totalProperties.y, kVirtualHousingBuffer,
                kTargetVacancyRate, kVacancySensitivity);
            int offsetHigh = GetSmoothedVacancyOffset(freeProperties.z, totalProperties.z, kVirtualHousingBuffer,
                kTargetVacancyRate, kVacancySensitivity);

            // E+. 计算熔断系数 (Cutoff Multiplier)
            // 修复空城风险：如果空置率过高，直接乘0
            float cutOffLow = GetVacancyMultiplier(freeProperties.x, totalProperties.x, kVirtualHousingBuffer,
                kPanicVacancyRate);
            float cutOffMed = GetVacancyMultiplier(freeProperties.y, totalProperties.y, kVirtualHousingBuffer,
                kPanicVacancyRate);
            float cutOffHigh = GetVacancyMultiplier(freeProperties.z, totalProperties.z, kVirtualHousingBuffer,
                kPanicVacancyRate);

            // F. 组合最终需求
            // 公式：(家庭需求 + 空置率修正) * 熔断乘数
            // 注意：流浪汉Bonus(homelessBonus) 只加给高密度，且不应受空置率负面影响太大(因为他们急需住房)

            float finalLow = (m_HouseholdDemand.value - (simpleJobFactor / 2) + offsetLow) * cutOffLow;
            float finalMed = (m_HouseholdDemand.value + offsetMed) * cutOffMed;

            // 高密度特殊处理：流浪汉直接推高需求，但仍然受制于严重空置熔断
            float finalHigh = (m_HouseholdDemand.value + homelessBonus + offsetHigh) * cutOffHigh;

            m_BuildingDemand.value = new int3(
                (int)math.clamp(finalLow * buildingLowFactor, 0f, 100f),
                (int)math.clamp(finalMed * buildingMedFactor, 0f, 100f),
                (int)math.clamp(finalHigh * buildingHighFactor, 0f, 100f)
            );

            // F. 填充 UI 因子数组 (Low/Medium/High DemandFactors)
            // 索引含义推测：7=幸福, 6=工作, 5=失业, 11=税收, 13=空置率, 12=学生, 8=无家可归(高密度)

            // 低密度 UI 因子
            // 使用math代替MathF以符合Burst
            m_LowDemandFactors[7] = (int)math.round(happinessFactor);
            m_LowDemandFactors[6] = (int)math.round(simpleJobFactor) / 2; // 低密度对简单工作需求只有一半权重
            m_LowDemandFactors[5] = (int)math.round(unemploymentFactor);
            m_LowDemandFactors[11] = (int)math.round(taxFactor);
            // 低密度无学生加成(学生不会住别墅)
            m_LowDemandFactors[13] = offsetLow; // 显示空置率带来的加成或惩罚
            m_LowDemandFactors[18] = (totalProperties.x <= 0) ? 20 : 0; // 提示未建设

            // 中密度 UI 因子
            m_MediumDemandFactors[7] = (int)math.round(happinessFactor);
            m_MediumDemandFactors[6] = (int)math.round(simpleJobFactor);
            m_MediumDemandFactors[5] = (int)math.round(unemploymentFactor);
            m_MediumDemandFactors[11] = (int)math.round(taxFactor);
            m_MediumDemandFactors[12] = (int)math.round(studentFactor);
            m_MediumDemandFactors[13] = offsetMed;
            m_MediumDemandFactors[18] = (totalProperties.y <= 0) ? 20 : 0;

            // 高密度 UI 因子
            m_HighDemandFactors[7] = (int)math.round(happinessFactor);
            m_HighDemandFactors[8] = (int)math.round(homelessBonus); // 高密度独有：流浪汉提供正向需求
            m_HighDemandFactors[6] = (int)math.round(simpleJobFactor);
            m_HighDemandFactors[5] = (int)math.round(unemploymentFactor);
            m_HighDemandFactors[11] = (int)math.round(taxFactor);
            m_HighDemandFactors[12] = (int)math.round(studentFactor);
            m_HighDemandFactors[13] = offsetHigh;
            m_HighDemandFactors[18] = (totalProperties.z <= 0) ? 20 : 0;

            // 处理特殊情况 UI 清零
            if (totalSimpJobs + totalCompJobs <= 2) // 几乎无工作
            {
                if (m_LowDemandFactors[6] > 0) m_LowDemandFactors[6] = 0;
                if (m_MediumDemandFactors[6] > 0) m_MediumDemandFactors[6] = 0;
                if (m_HighDemandFactors[6] > 0) m_HighDemandFactors[6] = 0;
            }

            if (cityPopulation.m_Population == 0)
            {
                m_LowDemandFactors[5] = 0;
                m_MediumDemandFactors[5] = 0;
                m_HighDemandFactors[5] = 0;
            }

            //// G. 计算各密度总分 (Sum Factors)           
            ////============================================================================
            //// --- 修正 ---
            //// 应用空置率惩罚修正(建筑需求)移除空置率一票否决逻辑，改为使用动态计算的需求偏移值
            //// 移除重复计算的因子(幸福度、税收、学生、失业率、就业率)
            ////============================================================================
            //float baseDemand = m_HouseholdDemand.value * kHouseholdDemandFactor;

            //// 低密度
            //float sumLow = baseDemand - simpleJobFactor / 2 + /*happinessFactor + taxFactor + simpleJobFactor / 2f + unemploymentFactor + */ offsetLow;

            //// 中密度
            //float sumMed = baseDemand + /* happinessFactor + taxFactor + simpleJobFactor + unemploymentFactor + studentFactor + */ offsetMed;

            //// 高密度 (包含流浪汉正面加成)
            //float sumHigh = baseDemand + /*happinessFactor + taxFactor + simpleJobFactor + unemploymentFactor + studentFactor + */ homelessBonus + offsetHigh;

            //// I. 最终建筑需求 (Final Calculation)
            //int finalLow = (int)(math.clamp(sumLow, 0f, 100f) * /*cutOffLow **/ buildingLowFactor) ;
            //int finalMed = (int)(math.clamp(sumMed, 0f, 100f) * /*cutOffMed **/ buildingMedFactor);
            //int finalHigh = (int)(math.clamp(sumHigh, 0f, 100f) * /*cutOffHigh **/ buildingHighFactor);

            //// 建筑需求最终值
            //m_BuildingDemand.value = new int3(finalLow, finalMed, finalHigh);

            // 应用解锁限制
            m_BuildingDemand.value = math.select(default(int3), m_BuildingDemand.value, unlockedDensities);

            // 作弊模式
            if (m_UnlimitedDemand)
            {
                m_BuildingDemand.value = 100;
            }

            // J. 触发器 (Trigger)
            float totalPropCount = totalProperties.x + totalProperties.y + totalProperties.z;
            float totalDemandSum =
                m_BuildingDemand.value.x + m_BuildingDemand.value.y + m_BuildingDemand.value.z;

            m_TriggerQueue.Enqueue(new TriggerAction(TriggerType.ResidentialDemand, Entity.Null,
                (totalPropCount > 100) ? (totalDemandSum / 100f) : 0f));

            float freePropCount = freeProperties.x + freeProperties.y + freeProperties.z;
            m_TriggerQueue.Enqueue(new TriggerAction(TriggerType.EmptyBuilding, Entity.Null,
                (totalPropCount > 100) ? (freePropCount * 100f / totalPropCount) : 100f));
        }

        // 辅助方法：根据正负值应用不同的权重
        private int GetFactorValue(float factorValue, float2 weightSelector)
        {
            if (!(factorValue < 0f))
            {
                return (int)(factorValue * weightSelector.y); // 正值乘 y
            }

            return (int)(factorValue * weightSelector.x); // 负值乘 
        }

        // 辅助函数，用于根据空缺获取需求偏移
        // 修复后的空置率偏移计算
        private int GetSmoothedVacancyOffset(int free, int total, float buffer, float targetRate, float sensitivity)
        {
            // 使用缓冲分母：math.max(total, buffer) 不够平滑，建议直接 total + buffer
            // 这样当 total=0 时，分母为 buffer，空置率=0
            // 当 total 很大时，buffer 的影响忽略不计
            free = math.clamp(free, 0, total);
            float effectiveTotal = total + buffer;
            float vacancyRate = free / effectiveTotal;

            // 偏差 = 目标 - 实际
            // 实际 15% (0.15), 目标 8% (0.08) -> 差值 -0.07
            // -0.07 * 1500 = -105 分。这足以抵消大多数正面需求。
            float score = (targetRate - vacancyRate) * sensitivity;

            // 限制单项影响范围，防止溢出UI显示或逻辑崩坏
            return (int)math.clamp(score, -200f, 200f);
        }

        // 新增：空置率熔断乘数
        private float GetVacancyMultiplier(int free, int total, float buffer, float panicRate)
        {
            float effectiveTotal = total + buffer;
            float vacancyRate = free / effectiveTotal;

            // 如果空置率 >= 恐慌线 (20%)，乘数为 0
            // 如果空置率 <= 恐慌线 - 5% (15%)，乘数为 1
            // 中间平滑过渡
            float lowerThreshold = panicRate - 0.05f;

            // smoothstep 在 lower~panic 之间返回 0~1
            // 我们需要反过来，所以用 1 - smoothstep
            float penalty = math.smoothstep(lowerThreshold, panicRate, vacancyRate);
            return 1.0f - penalty;
        }
    }
}


