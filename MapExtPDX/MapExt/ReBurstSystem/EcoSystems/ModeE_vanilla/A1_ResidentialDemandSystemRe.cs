// Game.Simulation.ResidentialDemandSystem
// 系統實例被多個外部系統呼叫，採用 Job 通用替換。

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

// using UnityEngine; // 使用 Unity.Mathematics 代替以符合 Burst


namespace MapExtPDX.ModeE
{
    /// <summary>
    /// ResidentialDemandSystem 居民需求系統
    /// 經濟／人口系統核心，源頭生成居民建築需求與家庭遷入需求。
    /// 原系統問題：
    /// 1. 幾乎全部採用硬編碼絕對值判斷，當人口規模擴大時，需求計算嚴重失衡；
    /// 2. 部分邏輯存在嚴重缺陷，如空置率邏輯、學生效應等；
    /// 3. 建築需求與家庭需求混淆，比如空置率高時建築需求砍至 0，同時連帶家庭需求砍至 0；
    /// </summary>
    /// 【參考模型】
    /// 城市人口增長和房地產發展需求動力分析模型：
    /// （原系統屬於相當簡陋的模型並且數值不盡合理）
    /// 1. 核心驅動層 (Primary Drivers) - 佔比約 60%
    /// 就業機會與產業結構 (30%)： 有沒有好工作是人來的根本原因。
    /// 遊戲對應：空閒工作機會。
    /// 宏觀經濟與金融環境 (20%)： 利率高低、信貸寬鬆程度直接決定房地產能否開發。
    /// 遊戲對應：稅收和獨特銀行建築、政策。
    /// 基礎設施與交通 (10%)： 地鐵、高鐵、機場的通達性。
    /// 遊戲對應：公共交通、道路密度。
    /// 2. 調節與限制層 (Secondary Regulators) - 佔比約 30%
    /// 住房成本與生活成本 (15%)： 房價太高會擠出人口（負面），也會吸引投資（正面）。
    /// 遊戲對應：地價、商業發展指數
    /// 政府政策與規劃 (10%)： 區域規劃（Zoning）、學區劃分。
    /// 遊戲對應：教育水平、區域政策
    /// 人口結構 (5%)： 老齡化程度、出生率、移民政策。
    /// 遊戲對應：人口年齡構成、政策。
    /// 3. 摩擦與環境層 (Tertiary Friction Factors) - 佔比約 10%
    /// 治安與公共安全 (4%)：
    /// 醫療衛生 (3%)：
    /// 環境品質與氣候 (3%)： 空氣污染、氣候舒適度。
    [BurstCompile]
    public struct UpdateResidentialDemandJob : IJob
    {
        // ⚠️⚠️⚠️ 欄位佈局約束（改動欄位前必讀）⚠️⚠️⚠️
        // 本 Job 透過 Transpiler 替換原版 ResidentialDemandSystem.UpdateResidentialDemandJob，
        // 由 JobPatchHelper 直接「複用原版 System 壓棧的 Job 實例記憶體佈局」——
        // 因此以下欄位的【型別 + 宣告順序】必須與原版逐一嚴格對齊（名稱不影響佈局，型別／順序錯位會讓 Burst 讀到錯誤記憶體，
        // 導致靜默資料損壞或崩潰，且【編譯不會報錯】）。
        // 新增／刪除／重排任何欄位前，務必先比對 _KnowledgeBase 原版欄位塊；遊戲版本升級時由 /check-upgrade 覆核。
        // ================= 輸入資料 (唯讀) =================
        [ReadOnly] public NativeList<Entity> m_UnlockedZonePrefabs; // 已解鎖的區域類型
        [ReadOnly] public ComponentLookup<Population> m_Populations; // 人口組件查找
        [ReadOnly] public ComponentLookup<ZoneData> m_ZoneDatas; // 區域資料查找
        [ReadOnly] public ComponentLookup<ZonePropertiesData> m_ZonePropertiesDatas; // 區域屬性查找
        [ReadOnly] public NativeList<DemandParameterData> m_DemandParameters; // 需求參數配置（全域參數）
        [ReadOnly] public NativeArray<int> m_StudyPositions; // 1-4 級教育的空閒學位數量
        [ReadOnly] public NativeArray<int> m_TaxRates; // 0-4 級學歷的稅率
        [ReadOnly] public float m_UnemploymentRate; // 失業率（百分比值，例如 5.0 表示 5%）
        public Entity m_City; // 城市實體引用

        // ================= 輸出與狀態資料 =================
        public NativeValue<int> m_HouseholdDemand; // 輸出：總家庭遷入需求 (基礎值)
        public NativeValue<int3> m_BuildingDemand; // 輸出：建築需求向量 (x:低, y:中, z:高)

        // UI 顯示用的因子 (決定需求面板上的提示資訊，如「稅收太高」、「空置房屋多」)
        public NativeArray<int> m_LowDemandFactors;
        public NativeArray<int> m_MediumDemandFactors;
        public NativeArray<int> m_HighDemandFactors;

        public CountHouseholdDataSystem.HouseholdData m_HouseholdCountData; // 包含無家可歸資料
        public CountResidentialPropertySystem.ResidentialPropertyData m_ResidentialPropertyData; // 空置房與總房產資料
        public Workplaces m_FreeWorkplaces; // 空閒崗位
        public Workplaces m_TotalWorkplaces; // 總崗位
        public NativeQueue<TriggerAction> m_TriggerQueue; // 觸發器佇列（如教程、音效）

        public float2 m_ResidentialDemandWeightsSelector; // 權重選擇器 (x:負值權重, y:正值權重)
        public bool m_UnlimitedDemand; // 作弊模式：無限需求

        // ================= 核心邏輯 =================
        public void Execute()
        {
            // === 配置中心 ===
            // 家庭需求全域因子；原始值=1f
            float kHouseholdDemandFactor = 1f;

            // 建築需求全域因子；原始值=1f
            float buildingLowFactor = 1f; // 低密度住宅
            float buildingMedFactor = 1f; // 中密度住宅
            float buildingHighFactor = 1f; // 高密度住宅

            // --- 基礎權重 (決定各因子對總分的影響力) ---
            float kHappinessWeight = 1.0f; // 幸福度權重
            float kTaxWeight = 1.0f; // 稅收權重
            float kHomelessPenaltyWeight = 1.0f; // 無家可歸(負面)權重
            float kHomelessBonusWeight = 1.0f; // 無家可歸(高密度正面)權重
            float ksimJobWeight = 1.0f; // 簡單工作就業權重
            float kcomJobWeight = 1.0f; // 複雜工作就業權重
            float kStudentWeight = 1.0f; // 教育資源權重
            float kUnemploymentWeight = 1.0f; // 失業率權重

            // 空置率影響設定
            // 1. 目標健康空置率 (比如 5%，參考國際平均水平)
            float kTargetVacancyRate = 0.05f;
            // 2. 嚴重空置率 25% (超過此值強制停止建設)
            float kPanicVacancyRate = 0.25f;
            // 3. 空置率敏感度
            // 差值：例如 目標 0.05 - 實際 0.10 = -0.05(空置太多，降低需求)
            // 放大倍數 1500f 意味著 1% 的偏差調整約 15 點需求
            // 預設為 1500f，增加將加強空置率獎懲，減少將削弱
            float kVacancySensitivity = 1500f;
            // 4. 虛擬緩衝
            // 在計算比率時，分母加上這個值。
            // 作用：在新城市(房產總數<100)時，大幅稀釋空置率的波動。
            // 例如：只有 10 套房，空了 10 套。無緩衝=100%空置(崩盤)。有緩衝(150)=10/160=6%空置(可控)。
            float kVirtualHousingBuffer = 150f;
            // 5. 權重係數
            // 空置率低影響建築權重
            // float kBuildWeight = 1.0f;
            // 空置率高吸引移民權重
            // float kMoveInWeight = 0.5f;
            // [MODIFIED] 低密度專屬較溫和的敏感度
            float kVacancySensitivityLow = 500f;

            // [MODIFIED 2026-07] 無家可歸中性率 (市民口徑)。
            // 量綱統一：homelessRate 分子分母皆採市民數（見下方 homelessRate 計算），
            // 對齊原版 CountHouseholdDataSystem.HomelessnessRate 的市民口徑，避免家庭/市民混用。
            // 由家庭口徑 0.0005 依「每戶約 2 人」重新標定為 0.001，維持觸發點大致不變。
            float kNeutralHomelessRate = 0.001f;
            // =================== 配置中心 ====================

            // A. 檢查已解鎖的密度類型

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

            // 取得房產基礎資料
            int3 freeProperties = m_ResidentialPropertyData.m_FreeProperties;
            int3 totalProperties = m_ResidentialPropertyData.m_TotalProperties;
            DemandParameterData paramsData = m_DemandParameters[0];

            // 計算人口數及基礎因子
            Population cityPopulation = m_Populations[m_City];
            int popCount = math.max(1, cityPopulation.m_Population); // 防止除零

            // B. 計算各類基礎因子

            // --- [新城市紅利] ---
            // 注意：此因子實際恆定在 19~20 之間，並非隨人口衰減至 0。
            // 因為 smoothstep(0,20,t) 的值域為 [0,1]，20f 減去它只能落在 [19,20]。
            // 即便人口達 40 萬(t=20)使 smoothstep=1，本值仍為 19。
            // 這是原版既有寫法：名為「紅利」，實質是一個約等於 20 的固定移民基礎分。保留原行為。
            float populationBonusFactor = 20f - math.smoothstep(0f, 20f, cityPopulation.m_Population / 20000f);

            // --- [教育因子] ---
            // 計算教育容量 (累加 1-4 級所有學位)
            // 原版邏輯在 1000 個學額時封頂毫無意義
            // 改為假設理想狀態是覆蓋 20% 的人口 (模擬學齡人口)
            int totalStudentSlots = 0;
            for (int j = 1; j <= 4; j++)
            {
                totalStudentSlots += m_StudyPositions[j];
            }

            float studentCoverage = totalStudentSlots / (popCount * 0.2f); // 假設 20% 人口上學
            float studentFactor = paramsData.m_StudentEffect * math.clamp(studentCoverage * 5f, 0f, 5f);

            // --- [幸福度因子] ---
            // 平均幸福度 vs 最低幸福度閾值
            // 採用相對值，無需修改
            int effectiveHappiness = math.max(paramsData.m_MinimumHappiness, cityPopulation.m_AverageHappiness);
            float happinessFactor = paramsData.m_HappinessEffect *
                                    (effectiveHappiness - paramsData.m_NeutralHappiness);

            // --- [稅收因子] ---
            // 計算所有學歷等級的平均稅率與 10% 的差值
            // 如果稅率>10%，因子為負；稅率<10%，因子為正。
            // 採用相對值，無需修改
            float avgTaxDeviation = 0f;
            for (int k = 0; k < 5; k++)
            {
                avgTaxDeviation += -(TaxSystem.GetResidentialTaxRate(k, m_TaxRates) - 10);
            }

            float taxFactor = paramsData.m_TaxEffect.x * (avgTaxDeviation / 5f);

            // --- [就業率因子] ---
            // 修復(改為比率) ---
            // 計算空缺職位比例。如果空缺率 > 中性值(比如 10%)，則有加成。
            // 就業空缺率中位數
            float neutralJobRate = paramsData.m_NeutralAvailableWorkplacePercentage / 100f;

            float totalSimpJobs = math.max(1f, m_TotalWorkplaces.SimpleWorkplacesCount);
            float totalCompJobs = math.max(1f, m_TotalWorkplaces.ComplexWorkplacesCount);

            float simpJobRate = m_FreeWorkplaces.SimpleWorkplacesCount / totalSimpJobs;
            float compJobRate = m_FreeWorkplaces.ComplexWorkplacesCount / totalCompJobs;

            // 放大倍數設為 100f，意味著每 1% 的額外空缺提供一定點數的吸引力
            float simpleJobFactor = paramsData.m_AvailableWorkplaceEffect * (simpJobRate - neutralJobRate) * 100f;
            simpleJobFactor = math.clamp(simpleJobFactor, 0f, 40f);

            float complexJobFactor = paramsData.m_AvailableWorkplaceEffect * (compJobRate - neutralJobRate) * 100f;
            complexJobFactor = math.clamp(complexJobFactor, 0f, 20f);

            // --- [失業率因子] ---
            // [MODIFIED] 修正：自然失業率(NAIRU)強制為 4.5% (東亞+歐美緊湊型)，拋棄原版內建 20% 魔幻參數影響
            // (中性失業率 - 當前失業率)。如果當前失業率高，結果為負，降低需求。
            float unemploymentFactor = 4.5f - m_UnemploymentRate;
            if (unemploymentFactor < 0f)
            {
                // [MODIFIED] 重拳出擊：突破 NAIRU 時，成倍扣減家庭需求，截斷失業潮人口湧入
                unemploymentFactor *= 2.5f;
            }

            //--- [流浪人口因子] ---
            // 修復：改為比例，避免大城市因絕對數量高而受到不合理的懲罰
            // [MODIFIED 2026-07] 量綱統一：分子改用市民數 m_HomelessCitizenCount，
            // 與分母 popCount(市民)同口徑；不再用家庭數 / 市民數的混用量綱。
            float homelessRate = m_HouseholdCountData.m_HomelessCitizenCount / (float)popCount;
            // 歸一化：如果流浪率是中性率的 2 倍，則係數為 2。
            float homelessRatioNormalized = homelessRate / kNeutralHomelessRate;
            // HouseholdDemand 負面懲罰 (無家可歸太多降低城市吸引力)
            // [MODIFIED 2026-07] 上限由 clamp(,0,5)→(,0,2.5)：單因子懲罰上限由 -100 收斂到 -50，
            // 與下方 homelessBonus 的 +40 大致對稱，避免單一因子瞬間把家庭需求壓到 0、造成移民面板忽開忽關。
            float homelessPenalty = (0f - paramsData.m_HomelessEffect) * math.clamp(homelessRatioNormalized, 0f, 2.5f);
            // BuildingDemand 正面需求 (無家可歸的人急需住房，主要推高高密度/廉租房需求)
            float homelessBonus = paramsData.m_HomelessEffect * math.clamp(homelessRatioNormalized, 0f, 2f);

            // C. 應用權重 (加權處理)
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

            // D. 計算總家庭遷入需求 (Household Demand)
            // 基礎池子，決定了有多少人想進城
            // 人口/幸福度/稅收/失業率/工作機會/學生資源/無家可歸懲罰等綜合影響
            // 無家可歸加成只影響建築需求，不影響家庭需求
            float baseHouseholdScore = populationBonusFactor + happinessFactor + homelessPenalty + taxFactor +
                                       unemploymentFactor + studentFactor + math.max(simpleJobFactor, complexJobFactor);
            // 限制在 0-200 之間
            m_HouseholdDemand.value = (int)math.clamp(baseHouseholdScore * kHouseholdDemandFactor, 0f, 200f);

            // E. 計算空置率因子 (Vacancy Logic)
            //============================================================================
            // --- 修復 6: 空置率懲罰修正：動態空置率邏輯(核心修改)
            // 原邏輯理想空閒量極低且為硬編碼(5,10,10)，導致房屋過剩時需求被嚴重壓制
            // 新邏輯使得需求計算基於總房產的百分比，而不是固定數值
            //============================================================================

            // 計算空置率影響
            // [MODIFIED] 低密度因為自身建築容量小，使用專屬的溫和敏感度(kVacancySensitivityLow)，避免需求大起大落
            int offsetLow = GetSmoothedVacancyOffset(freeProperties.x, totalProperties.x, kVirtualHousingBuffer,
                kTargetVacancyRate, kVacancySensitivityLow);
            int offsetMed = GetSmoothedVacancyOffset(freeProperties.y, totalProperties.y, kVirtualHousingBuffer,
                kTargetVacancyRate, kVacancySensitivity);
            int offsetHigh = GetSmoothedVacancyOffset(freeProperties.z, totalProperties.z, kVirtualHousingBuffer,
                kTargetVacancyRate, kVacancySensitivity);

            // E+. 計算熔斷係數 (Cutoff Multiplier)
            // 修復空城風險：如果空置率過高，直接乘 0
            float cutOffLow = GetVacancyMultiplier(freeProperties.x, totalProperties.x, kVirtualHousingBuffer,
                kPanicVacancyRate);
            float cutOffMed = GetVacancyMultiplier(freeProperties.y, totalProperties.y, kVirtualHousingBuffer,
                kPanicVacancyRate);
            float cutOffHigh = GetVacancyMultiplier(freeProperties.z, totalProperties.z, kVirtualHousingBuffer,
                kPanicVacancyRate);

            // F. 組合最終需求
            // 公式：(家庭需求 + 空置率修正) * 熔斷乘數
            // 注意：流浪漢 Bonus(homelessBonus) 只加給高密度，且不應受空置率負面影響太大(因為他們急需住房)

            float finalLow = (m_HouseholdDemand.value - (simpleJobFactor / 2) + offsetLow) * cutOffLow;
            float finalMed = (m_HouseholdDemand.value + offsetMed) * cutOffMed;

            // 高密度特殊處理：流浪漢直接推高需求，但仍然受制於嚴重空置熔斷
            float finalHigh = (m_HouseholdDemand.value + homelessBonus + offsetHigh) * cutOffHigh;

            m_BuildingDemand.value = new int3(
                (int)math.clamp(finalLow * buildingLowFactor, 0f, 100f),
                (int)math.clamp(finalMed * buildingMedFactor, 0f, 100f),
                (int)math.clamp(finalHigh * buildingHighFactor, 0f, 100f)
            );

            // F. 填充 UI 因子陣列 (Low/Medium/High DemandFactors)
            // 索引含義推測：7=幸福, 6=工作, 5=失業, 11=稅收, 13=空置率, 12=學生, 8=無家可歸(高密度)

            // 低密度 UI 因子
            // 使用 math 代替 MathF 以符合 Burst
            m_LowDemandFactors[7] = (int)math.round(happinessFactor);
            m_LowDemandFactors[6] = (int)math.round(simpleJobFactor) / 2; // 低密度對簡單工作需求只有一半權重
            m_LowDemandFactors[5] = (int)math.round(unemploymentFactor);
            m_LowDemandFactors[11] = (int)math.round(taxFactor);
            // 低密度無學生加成(學生不會住別墅)
            m_LowDemandFactors[13] = offsetLow; // 顯示空置率帶來的加成或懲罰
            m_LowDemandFactors[18] = (totalProperties.x <= 0) ? 20 : 0; // 提示未建設

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
            m_HighDemandFactors[8] = (int)math.round(homelessBonus); // 高密度獨有：流浪漢提供正向需求
            m_HighDemandFactors[6] = (int)math.round(simpleJobFactor);
            m_HighDemandFactors[5] = (int)math.round(unemploymentFactor);
            m_HighDemandFactors[11] = (int)math.round(taxFactor);
            m_HighDemandFactors[12] = (int)math.round(studentFactor);
            m_HighDemandFactors[13] = offsetHigh;
            m_HighDemandFactors[18] = (totalProperties.z <= 0) ? 20 : 0;

            // 處理特殊情況 UI 清零
            if (totalSimpJobs + totalCompJobs <= 2) // 幾乎無工作
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

            //// G. 計算各密度總分 (Sum Factors)
            ////============================================================================
            //// --- 修正 ---
            //// 應用空置率懲罰修正(建築需求)移除空置率一票否決邏輯，改為使用動態計算的需求偏移值
            //// 移除重複計算的因子(幸福度、稅收、學生、失業率、就業率)
            ////============================================================================
            //float baseDemand = m_HouseholdDemand.value * kHouseholdDemandFactor;

            //// 低密度
            //float sumLow = baseDemand - simpleJobFactor / 2 + /*happinessFactor + taxFactor + simpleJobFactor / 2f + unemploymentFactor + */ offsetLow;

            //// 中密度
            //float sumMed = baseDemand + /* happinessFactor + taxFactor + simpleJobFactor + unemploymentFactor + studentFactor + */ offsetMed;

            //// 高密度 (包含流浪漢正面加成)
            //float sumHigh = baseDemand + /*happinessFactor + taxFactor + simpleJobFactor + unemploymentFactor + studentFactor + */ homelessBonus + offsetHigh;

            //// I. 最終建築需求 (Final Calculation)
            //int finalLow = (int)(math.clamp(sumLow, 0f, 100f) * /*cutOffLow **/ buildingLowFactor) ;
            //int finalMed = (int)(math.clamp(sumMed, 0f, 100f) * /*cutOffMed **/ buildingMedFactor);
            //int finalHigh = (int)(math.clamp(sumHigh, 0f, 100f) * /*cutOffHigh **/ buildingHighFactor);

            //// 建築需求最終值
            //m_BuildingDemand.value = new int3(finalLow, finalMed, finalHigh);

            // 應用解鎖限制
            m_BuildingDemand.value = math.select(default(int3), m_BuildingDemand.value, unlockedDensities);

            // 作弊模式
            if (m_UnlimitedDemand)
            {
                m_BuildingDemand.value = 100;
            }

            // J. 觸發器 (Trigger)
            float totalPropCount = totalProperties.x + totalProperties.y + totalProperties.z;
            float totalDemandSum =
                m_BuildingDemand.value.x + m_BuildingDemand.value.y + m_BuildingDemand.value.z;

            m_TriggerQueue.Enqueue(new TriggerAction(TriggerType.ResidentialDemand, Entity.Null,
                (totalPropCount > 100) ? (totalDemandSum / 100f) : 0f));

            float freePropCount = freeProperties.x + freeProperties.y + freeProperties.z;
            m_TriggerQueue.Enqueue(new TriggerAction(TriggerType.EmptyBuilding, Entity.Null,
                (totalPropCount > 100) ? (freePropCount * 100f / totalPropCount) : 100f));
        }

        // 輔助方法：根據正負值應用不同的權重
        private int GetFactorValue(float factorValue, float2 weightSelector)
        {
            if (!(factorValue < 0f))
            {
                return (int)(factorValue * weightSelector.y); // 正值乘 y
            }

            return (int)(factorValue * weightSelector.x); // 負值乘 x
        }

        // 輔助函數，用於根據空缺取得需求偏移
        // 修復後的空置率偏移計算
        private int GetSmoothedVacancyOffset(int free, int total, float buffer, float targetRate, float sensitivity)
        {
            // 使用緩衝分母：math.max(total, buffer) 不夠平滑，改為直接 total + buffer
            // 這樣當 total=0 時，分母為 buffer，空置率=0
            // 當 total 很大時，buffer 的影響忽略不計
            free = math.clamp(free, 0, total);
            float effectiveTotal = total + buffer;
            float vacancyRate = free / effectiveTotal;

            // 偏差 = 目標 - 實際
            // 實際 15% (0.15), 目標 5% (0.05) -> 差值 -0.10
            // -0.10 * 1500 = -150 分。這足以抵消大多數正面需求。
            float score = (targetRate - vacancyRate) * sensitivity;

            // 限制單項影響範圍，防止溢出 UI 顯示或邏輯崩壞
            return (int)math.clamp(score, -200f, 200f);
        }

        // 新增：空置率熔斷乘數
        private float GetVacancyMultiplier(int free, int total, float buffer, float panicRate)
        {
            float effectiveTotal = total + buffer;
            float vacancyRate = free / effectiveTotal;

            // 如果空置率 >= 恐慌線 (panicRate，預設 25%)，乘數為 0
            // 如果空置率 <= 恐慌線 - 5% (預設 20%)，乘數為 1
            // 中間平滑過渡
            float lowerThreshold = panicRate - 0.05f;

            // smoothstep 在 lower~panic 之間返回 0~1
            // 我們需要反過來，所以用 1 - smoothstep
            float penalty = math.smoothstep(lowerThreshold, panicRate, vacancyRate);
            return 1.0f - penalty;
        }
    }
}
