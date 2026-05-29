using Colossal;
using System.Collections.Generic;

namespace EconomyEX.Settings
{
    public class LocaleHANT : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANT(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "EconomyEX 經濟擴展" },
                
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionStatus), "執行狀態" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionStatus), "執行狀態" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.StatusInfo)), "• 模組狀態" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.StatusInfo)), "當前經濟模組的工作狀態。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ConflictWarning)), "• 衝突警告" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ConflictWarning)), "偵測到的可能導致錯誤的衝突資訊。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SystemStatusReport)), "• 系統狀態" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SystemStatusReport)), "經濟系統替換的即時狀態報告。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStatus)), "★ 重新整理狀態" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStatus)), "點擊手動重新整理衝突偵測和系統狀態顯示。" },

                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionGeneral), "通用設定" },
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionGeneral), "通用設定" },
                
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableEconomyFix)), "• 啟用經濟修復與性能優化 (總開關)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableEconomyFix)), "【此修正檔目前處於測試階段 (Beta)】\n優化並修復以下系統，以適配大城市經濟和性能問題。\n\n⚠️ 【重要】：變更此項設定後，【必須重啟遊戲】，否則不會生效並且會引發不可預知的 Bug！" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "  ├─ RCI需求調節系統組" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "優化居住、商業、工業需求計算模型，使之更平滑合理。\n\n⚠️ 修改後需重啟遊戲生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "  ├─ 找工作系統組" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "優化市民找工作行為與匹配演算法，提升就業尋找效率。\n\n⚠️ 與 Realistic JobSearch 等模組不相容！\n⚠️ 修改後需重啟遊戲生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "  ├─ 找房與租金系統組" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)), "優化家庭找房尋路計算；包含真實地價和租金計算（Land Value）重構，使之更加合理。\n\n⚠️ 修改後需重啟遊戲生效。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "  ├─ 消費採購與服務覆蓋尋路系統組" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)), "優化市民購物與企業採購的資源匹配，並優化服務覆蓋尋路，大幅降低超遠路程規劃產生的性能開銷。\n\n⚠️ 與 Realistic PathFinding 等尋路模組不相容！\n⚠️ 修改後需重啟遊戲生效。\n\n⚠️ 預設關閉，以避免與流行尋路模組衝突。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "  └─ 居民AI尋路優化補丁" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "修復市民尋路 AI 等待時間的邏輯缺陷，緩解尋路記憶體溢出的問題。\n\n⚠️ 與 Realistic PathFinding 等尋路模組不相容！\n⚠️ 修改後需重啟遊戲生效。\n\n⚠️ 預設關閉，以避免與流行尋路模組衝突。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "重設" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "將所有經濟子系統開關恢復為預設值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "確認要將所有經濟子系統開關重設為預設值嗎？" },

                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionPathfinding), "▍尋路優化參數(可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "購物最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "控制市民為了買東西（如雜貨、餐飲）願意承受的最大出行成本。數值越低，市民在找不到商店時放棄得越快，能大幅降低 CPU 負擔。\n" +
                    "★ 建議值：8000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)), "公司貨運最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "控制公司（工廠/商店）尋找材料並呼叫貨車送貨的最大搜尋範圍。公司補貨通常不局限於本地，較高數值可允許公司在全圖範圍內尋找資源。\n" +
                    "★ 建議值：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)), "休閒觀光最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "控制市民為了遊覽公園、地標或觀光願意承受的最大出行成本。數值越低，全圖無目的閒逛引發的尋路計算越少。\n" +
                    "★ 建議值：8000 ~ 12000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)), "醫院/犯罪最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "控制市民生病就醫或犯罪時的最大搜尋範圍。較低的值將這些行為限制在就近區域，鼓勵在地化的公共服務規劃。\n" +
                    "★ 提示：建議在此成本範圍內合理配置醫院與警局。若相關設施非常密集，可進一步降低此值。\n" +
                    "★ 建議值：4000 ~ 8000（預設：6000）"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "找工作最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "控制市民為了尋求工作崗位，最多願意跨越多大規模的地圖。該行為頻率極低，建議直接拉滿（對性能影響不明顯）。\n" +
                    "★ 建議值：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "找房搬家最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "控制市民搬家找房時的最大搜尋範圍上限。該行為頻率較低，建議直接拉滿。\n" +
                    "★ 建議值：200000"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)), "找小學最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "控制小學生尋找學校願意走的最遠路線開銷。較小的值能強迫小學生只能就近入學。\n" +
                    "★ 建議值：10000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)), "找高中最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "控制中學生尋找高中能夠跨越的最大路線開銷。\n" +
                    "★ 建議值：17000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)), "找學院最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "控制尋找學院(大專)級別的最大範圍。\n" +
                    "★ 建議值：50000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)), "找大學最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "控制尋找大學的最大範圍。如果是全圖唯一的大學城城邦，建議拉滿以覆蓋全圖每個角落。\n" +
                    "★ 建議值：100000 ~ 200000"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetPathfinding)), "重設" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetPathfinding)), "將所有尋路成本上限參數恢復為預設值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetPathfinding)), "確認要將所有尋路參數重設為預設值嗎？" },

                // --- 经济行为参数 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSectionBehavior), "經濟行為與吞吐量 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.JobSeekerCap)), "找工作系統：求職吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.JobSeekerCap)), "每次系統更新最多建立的求職者數量。城市人口越大可適當提高。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PathfindRequestCap)), "找工作系統：尋路吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PathfindRequestCap)), "每次尋路更新最多處理的求職尋路請求數量。通常為求職吞吐量的 2~4 倍。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "購物機率人口壓制係數 (x0.0001)" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "控制城市人口對家庭購物機率的衰減影響。數值越大，高人口時購物機率越低。預設：4（=0.0004）。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "家庭購物需求倍率" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "每次家庭購物時的資源購買量倍率。預設 3.5。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomeSeekerCap)), "找房系統：搬家吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomeSeekerCap)), "每幀最多處理的已有住房但想搬家的家庭數量。預設 128。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomelessSeekerCap)), "找房系統：流浪安置吞吐量" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomelessSeekerCap)), "每幀最多處理的無家可歸家庭找房數量。預設 1280。" },

                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoBehavior)), "重設" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoBehavior)), "將所有經濟行為與吞吐量參數恢復為預設值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoBehavior)), "確認要將所有經濟行為參數重設為預設值嗎？" },

                // ============================================================
                // Tab: 租金管控
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kRentControlTab), "租金管控" },

                // --- Group: 地价因子 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kLandValueFactorGroup), "地價影響因子 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueEnvironmentEffect)), "環境地價影響係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueEnvironmentEffect)),
                    "控制環境因子（地形吸引力、電信覆蓋、污染懲罰等）對道路 Edge 地價的傳遞比例。Edge 地價直接決定建築租金。\n\n" +
                    "原版中這些環境因子僅用於 UI 熱力圖顯示，不影響租金計算。本模組將其納入經濟模擬以提高真實感，但在服務設施完善的城區會導致工商業租金顯著偏高。\n\n" +
                    "★ 係數參考：\n" +
                    " - 0%：環境不影響地價（最接近原版行為）\n" +
                    " - 40%：平衡模式 — 保留地價差異化同時避免租金過高（預設推薦）\n" +
                    " - 70%：高真實感 — 黃金地段租金明顯上漲\n" +
                    " - 100%：完全傳遞 — Mod 原始行為，可能出現大面積高租金警告\n\n" +
                    "可於遊戲中即時調節。地價變化會隨時間平滑過渡。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ServiceBonusCapMultiplier)), "服務加成上限乘數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ServiceBonusCapMultiplier)),
                    "縮放服務覆蓋加成（醫療、警察、教育、公車等）對 Edge 地價的上限。\n\n" +
                    "★ 係數參考：\n" +
                    " - 100%：預設上限\n" +
                    " - 50%：減半 — 降低服務密集區的地價溢價\n" +
                    " - 200%：翻倍 — 放大服務區位優勢\n\n" +
                    "可於遊戲中即時調節。"
                },

                // --- Group: 租金公式 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kRentFormulaGroup), "租金公式調節 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RentMultiplierResidential)), "住宅租金乘數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RentMultiplierResidential)), "應用於所有住宅租金的總乘數。100% = 等效原版。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RentMultiplierCommercial)), "商業租金乘數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RentMultiplierCommercial)), "應用於所有商業租金的總乘數. 100% = 等效原版。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RentMultiplierIndustrial)), "工業租金乘數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RentMultiplierIndustrial)), "應用於所有工業租金的總乘數。100% = 等效原版。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueFactorResidential)), "住宅：地價貢獻係數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueFactorResidential)), "控制 Edge 地價對住宅租金的影響強度。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueFactorCommercial)), "商業：地價貢獻係數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueFactorCommercial)), "控制 Edge 地價對商業租金的影響強度。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueFactorIndustrial)), "工業：地價貢獻係數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueFactorIndustrial)), "控制 Edge 地價對工業租金的影響強度。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LevelFactorResidential)), "住宅：等級貢獻係數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LevelFactorResidential)), "控制建築等級對住宅租金的貢獻程度。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LevelFactorCommercial)), "商業：等級貢獻係數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LevelFactorCommercial)), "控制建築等級對商業租金的貢獻程度。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LevelFactorIndustrial)), "工業：等級貢獻係數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LevelFactorIndustrial)), "控制建築等級對工業租金的貢獻程度。\n\n可於遊戲中即時調節。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetRentControl)), "重設" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetRentControl)), "將所有租金管控參數恢復為預設值。" },
                { m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetRentControl)), "確認要將所有租金管控參數重設為預設值嗎？" },

                // ============================================================
                // Tab: 性能工具
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kSectionPerfTool), "性能工具" },

                // --- Group: NoDogs ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoDogsGroup), "寵物控制 (NoDogs)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsOnStreet)), "NoDogs: 禁止外出" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsOnStreet)),
                    "禁止寵物外出上街（關閉生成、渲染與尋路）。邏輯寵物實體仍存在於記憶體中。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsGeneration)), "NoDogs: 阻止新生成" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsGeneration)),
                    "將新家庭的寵物生成機率歸零，阻止新移民攜帶寵物。已有寵物保留不變。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoDogsPurge)), "⚠ NoDogs: 清除所有存量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoDogsPurge)),
                    "⚠ 警告：移除存檔中所有已有寵物實體，最大化性能提升。清除後已有家庭不會再獲得寵物，只有新搬入的家庭才會自帶（若未阻止生成）。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyNoDogs)), "► 套用 NoDogs 設定" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyNoDogs)),
                    "點擊後上方勾選才會生效。未點擊此按鈕前，勾選不會對遊戲產生任何影響。"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyNoDogs)),
                    "確認套用 NoDogs 設定？若勾選了「清除所有存量」，所有寵物將被永久移除！"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DislayPetCount)), "當前邏輯寵物數" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DislayPetCount)), "地圖上當前的邏輯寵物實體數量統計。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshPetCount)), "重新整理寵物統計" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPetCount)),
                    "點擊以重新計算地圖上的活動寵物實體數量。這只是一個統計，對遊戲狀態無任何影響。"
                },

                // --- Group: 过境交通控制 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoTrafficGroup), "過境交通控制" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.NoThroughTraffic)), "禁止過境交通" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.NoThroughTraffic)),
                    "禁止所有過境交通工具出現，降低尋路計算量和交通擁堵。 (可能需要執行一段時間生效)"
                },

                // --- Group: 编辑器工具 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEditorToolGroup), "編輯器工具" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EditorCollisionSkip)), "編輯器碰撞覆蓋" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EditorCollisionSkip)),
                    "在編輯器模式放置物件時跳過碰撞偵測錯誤。「僅樹木」移除樹木碰撞警告；「所有物件」移除所有放置碰撞警告。"
                },

                // --- Group: GPU 性能优化 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kGpuOptGroup), "GPU 性能優化" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableWorldBackdrop)), "⚠ 停用背景世界地圖" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableWorldBackdrop)),
                    "停用背景世界地圖（Backdrop），節省 GPU 和顯示記憶體資源。\n\n" +
                    "★ 性能收益：\n" +
                    " - 每幀節省 ~0.5-2ms GPU（級聯渲染、降採樣）\n" +
                    " - 減少 ~37MB 顯示記憶體佔用\n" +
                    " - 消除 AsyncGPUReadback 的 CPU 阻塞\n\n" +
                    "⚠ 注意：可遊玩區域以外的遠景地形將不再渲染。這僅影響視覺效果，不影響遊戲玩法。\n\n" +
                    "需要重啟遊戲生效。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterSimQuality)), "水體模擬質量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "控制水體模擬的 GPU 頻率。\n\n" +
                    "★ 選項說明：\n" +
                    " - 原版：每幀完整模擬（預設）\n" +
                    " - 降低：關閉流體模糊，輕微節省 GPU\n" +
                    " - 最低：每 4 幀模擬一次，顯著降低 GPU 負載\n" +
                    " - 暫停：完全停止水流模擬\n\n" +
                    "可於遊戲中即時調節，即時生效。"
                },

                // ============================================================
                // Tab: 调试
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "偵測" },

                // --- Group: 人口诊断 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPopDiagGroup), "人口診斷" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PopDiagReport)), "診斷報告" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PopDiagReport)), "顯示家庭人口健康度指標，包含搬離、流浪和找房統計。" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshPopDiag)), "重新整理診斷數據" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPopDiag)), "執行一次 ECS 查詢以收集人口診斷數據。" },

                // --- Group: 偵測 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "偵測參數" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableRescueDebugLog)), "• 啟用購車救援偵測日誌" },
                { m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableRescueDebugLog)), "開啟後，購車救援系統在每次執行救援、重試或清理放棄邏輯時會列印日誌。\n★ 建議在大城市或穩定運行後保持關閉以防止日誌檔案膨脹。" },
            };
        }

        public void Unload() { }
    }
}
