// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// --- LOCALE ---

using Colossal;
using System.Collections.Generic;

namespace MapExtPDX
{
    public class LocaleHANT : IDictionarySource
    {
        private readonly ModSettings m_Setting;

        public LocaleHANT(ModSettings setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                // ============================================================
                // Mod 标题
                // ============================================================
                { m_Setting.GetSettingsLocaleID(), "#大地圖" },

                // ============================================================
                // Tab 1: 地图尺寸
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMapSizeModeTab), "地圖尺寸" },

                // --- Group: 地图尺寸模式 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kMainModeGroup), "地圖尺寸模式" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PatchModeChoice)), "► 選擇地圖尺寸模式" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PatchModeChoice)),
                    "⚠️ 變更模式後必須點擊「套用設定」按鈕生效！\n\n模式詳情:\n - ModeA: 57km (4x4) DEM:14m\n - ModeB: 28km (2x2) DEM:7m\n - ModeC: 114km (8x8) DEM:28m\n - 純淨模式: 14km 原版(1x1) DEM:3.5m\n\n注意:\n1. 隨著地圖尺寸的增加，DEM地形解析度會相應降低，導致部分山地、水岸與坡道顯得粗糙。如果對地形平滑度要求較高，建議使用較為平坦的地圖或使用模組工具進行修飾。\n2. 由於遊戲底層的浮點精度限制，在地圖邊緣區域可能會出現模擬數據計算偏差（產生虛假的視覺效果），使用 114km 模式時尤為明顯。建議將城市活動中心（住/商/工）儘量建設在地圖中心區域。\n\n⚠️ 【重要警告】：在變更地圖尺寸模式後，【必須重啟遊戲】才能安全載入存檔，否則可能導致損壞存檔！"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "• 當前已套用地圖尺寸"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ModSettingCoreValue)),
                    "當前已選擇並成功套用的地圖尺寸。該尺寸指地圖邊長。單位為公尺。\n⚠️ 注意: 雖然本模組具有存檔驗證以防錯誤載入不同尺寸地圖存檔，但仍然強烈建議在使用本模組載入遊戲存檔前，請備份好您的所有遊戲存檔(推薦Skyve)，以防遊戲崩潰或各種奇特問題損壞存檔！大地圖製作不易，且行且珍惜。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ApplyPatchChanges)), "► 套用設定" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "點擊以套用所選的地圖尺寸模式。\n\n⚠️ 【重要】：本模組核心邏輯不支援熱切換。在套用新設定後，【必須重啟遊戲】才能安全讀取存檔，否則系統邏輯將會錯亂並導致損壞存檔風險！"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ApplyPatchChanges)),
                    "正在套用地圖尺寸模式，請耐心等待完成。\n\n⚠️ 完成後，請務必【重啟遊戲】以確保設定完全生效，切勿直接讀取存檔！"
                },

                // --- Group: 地形-水体优化 (Beta) ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kTerrainWaterOptGroup), "地形-水體性能優化 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainBufferPrealloc)), "地形緩衝預分配" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainBufferPrealloc)),
                    "根據地圖倍率在首幀預分配更大的 GPU StructuredBuffer，" +
                    "避免大量建築/道路可見時執行階段動態擴容卡頓。\n\n" +
                    "★ 建議：大地圖全部開啟，無視覺副作用。\n" +
                    "★ 提示：該選項即時生效無須重啟。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainCullThrottle)), "建築裁剪降頻" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainCullThrottle)),
                    "相機平移時，若建築和地形均無實際變化，跳過 CullBuildingLotsJob 全量裁剪，" +
                    "複用上一幀的快取列表。\n\n" +
                    "大地圖下該 Job 遍歷所有建築實體，開銷隨建築數量線性增長。該優化可顯著降低平移相機時的 CPU 佔用。\n\n" +
                    "★ 建議：大地圖全部開啟，無視覺副作用。\n" +
                    "★ 提示：該選項即时生效無須重啟。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainCascadeThrottle)), "⚠ 地形級聯降頻 (實驗性)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainCascadeThrottle)),
                    "透過將遠距地形級聯層每 4 幀更新一次（而非每幀）來降低 GPU 負載。\n\n" +
                    "⚠ 警告：移動相機時可能出現地形偏移/錯位，" +
                    "因為級聯視埠範圍每幀更新但渲染被降頻。\n\n" +
                    "★ 建議：除非在超大地圖上遇到嚴重 GPU 瓶頸，否則保持關閉。\n" +
                    "★ 提示：該選項即時生效無須重啟。"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "► 水體模擬質量"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterSimQuality)),
                    "控制水系統 CPU 和 GPU 計算的更新頻率，以提升大地圖的更新率和遊戲速度。\n\n" +
                    " - Vanilla: 原版高精度：每幀排程計算，效果最好，消耗最大。\n" +
                    " - Reduced: 降低消耗：每幀計算並傳播水體，但關閉背景大地圖邊緣的水流計算。\n" +
                    " - Minimal: 極簡性能：每四幀跳過計算並顯式關閉水面模糊和後處理效果，將大幅降低 GPU 請求頻率，可能會有些微的水流卡頓。\n" +
                    " - Paused: 暫停流體：完全凍結水體流動計算（水面將靜止但水位不會出現大變化）。\n\n" +
                    "★ 提示：該選項即時生效無須重啟。"
                },

                // (隐藏项 - 保留用于序列化)
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.TerrainResolution)), "地形解析度" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.TerrainResolution)),
                    "新建地圖的地形高度圖解析度。8192 提供更精細的地形編輯和渲染（使用地形筆刷時尤為明顯）。" +
                    "已有存檔將保持其原始解析度。\n" +
                    "⚠️ 修改後需重啟遊戲。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterResolution)), "水體模擬解析度" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterResolution)),
                    "水體模擬紋理大小。較低的值可大幅降低 GPU/顯示記憶體佔用，視覺影響極小。" +
                    "大地圖建議使用 512 或 256。\n" +
                    "⚠️ 修改後載入舊存檔時水面將重設（河流和湖泊會從水源重新注水）。\n" +
                    "⚠️ 修改後需重啟遊戲。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.VRAMEstimate)), "地形/水體預估顯示記憶體佔用" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.VRAMEstimate)),
                    "當前解析度設定下地形級聯紋理和水體模擬紋理的大致 GPU 顯示記憶體佔用量。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.WaterTextureFormat)), "水模擬貼圖精度 (顯示記憶體優化)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.WaterTextureFormat)),
                    "將原本 32 浮點的模擬通道數據強制壓縮到 16 浮點，省去高達 43% 的顯示記憶體佔用並將理論上的頻寬開銷減半，極大提升 GPU 模擬性能限制。\n\n" +
                    " - 原版 HDR (32-bit)：精度高無損，消耗約 180MB 顯示記憶體。 \n" +
                    " - 性能模式 (16-bit)：精度有損，消耗約 105MB 顯示記憶體。在水深大於 100 公尺時邊緣可能會因截斷出現計算波紋（通常很少見）。\n\n" +
                    "⚠️ 修改後需【重啟遊戲】或重新讀取存檔生效。"
                },

                // --- Group: 经济系统修复 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoGroup), "經濟系統修復" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "• 經濟系統修復与性能優化 (Beta 總開關)"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.isEnableEconomyFix)),
                    "【此修正檔目前處於測試階段 (Beta)】\n優化並修復以下系統，以適配百萬人口規模的巨型城市：\n - 住宅/商業/工業需求系統\n - 家庭找房系統\n - 家庭行為系統 (消費行為修正)\n - 市民尋找工作系統\n - 租金計算系統\n - 資源採購與服務覆蓋尋路系統\n - 居民 AI 尋路優化補丁\n\n⚠️ 【重要】：變更此項設定後，【必須重啟遊戲】，否則不會生效並且會引發不可預知的 Bug！"
                },

                // --- Group: 警告 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kNoteGroup), "警告" },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ModeChangeWarningMessage)),
                    "⚠️ 變更上述【任一】選項後，請務必【重啟遊戲】再讀取存檔！"
                },

                // ============================================================
                // Tab 2: EconomyEX
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kMiscTab), "EconomyEX" },

                // --- Group: 经济子系统开关 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoSystemEnableGroup), "經濟子系統開關" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDemandEcoSystem)), "├─ RCI需求調節系統組" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDemandEcoSystem)),
                    "優化居住、商業、工業需求計算模型，使之更平滑合理，並匹配百萬人口規模的巨型城市。\n\n⚠️ 修改後需重啟遊戲生效。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)), "├─ 找工作系統組" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableJobSearchEcoSystem)),
                    "優化市民找工作行為與匹配演算法，提升就業尋找效率。\n\n⚠️ 與 Realistic JobSearch 等模組不相容！\n⚠️ 修改後需重啟遊戲生效。"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "├─ 找房與租金系統組"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableHouseholdPropertyEcoSystem)),
                    "優化家庭找房尋路計算；包含真實地價和租金計算（Land Value）重構，使之更加合理。\n\n⚠️ 修改後需重啟遊戲生效。"
                },
                {
                    m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "├─ 消費採購與服務覆蓋尋路系統組"
                },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResourceBuyerEcoSystem)),
                    "優化市民購物與企業採購的資源匹配，並優化服務覆蓋尋路，大幅降低超遠路程規劃產生的性能開銷。\n\n⚠️ 與 Realistic PathFinding 等尋路模組不相容！\n⚠️ 修改後需重啟遊戲生效。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)), "└─ 居民AI尋路優化補丁" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableResidentAIEcoSystem)),
                    "修復市民尋路 AI 等待時間的邏輯缺陷，緩解大地圖底層尋路記憶體溢出的問題。\n\n⚠️ 與 Realistic PathFinding 等尋路模組不相容！\n⚠️ 修改後需重啟遊戲生效。\n\n⚠️ 預設關閉，以避免與流行尋路模組衝突。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableDownstreamAIEcoSystem)), "└─ 載具AI與尋路系統組 (Beta)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableDownstreamAIEcoSystem)),
                    "替換私家車 AI、出租車 AI、休閒系統和找學校系統為可配置版本，按出行目的分級設定尋路成本上限。防止車輛在大地圖中因尋路成本超過原版硬編碼上限而在途中消失。\n\n⚠️ 與修改相同載具 AI 系統的模組不相容！\n⚠️ 修改後需重啟遊戲生效。\n\n⚠️ 預設關閉 (Beta)。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoSystemToggles)), "重設" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoSystemToggles)),
                    "將所有經濟子系統開關恢復為預設值。"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoSystemToggles)),
                    "確認要將所有經濟子系統開關重設為預設值嗎？"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ConflictWarning)), "衝突警告" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ConflictWarning)),
                    "偵測到的與其他模組修改相同原版系統的衝突資訊。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.SystemStatusReport)), "系統狀態" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.SystemStatusReport)),
                    "經濟系統替換的即時狀態報告。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshStatus)), "↻ 重新整理狀態" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshStatus)),
                    "點擊手動重新整理衝突偵測和系統狀態顯示。"
                },

                // --- Group: 寻路成本上限 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPathfindingGroup), "尋路成本上限 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingMaxCost)), "購物最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingMaxCost)),
                    "控制市民為了買東西（如雜貨、餐飲）願意承受的最大出行成本。數值越低，市民在找不到商店時放棄得越快，能大幅降低大地圖下的 CPU 負擔。\n" +
                    "★ 建議值：\n" +
                    " - 14km / 28km： 8000\n" +
                    " - 57km / 114km：8000 ~ 12000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)), "公司貨運最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.CompanyShoppingMaxCost)),
                    "控制公司（工廠/商店）尋找材料並呼叫貨車送貨的最大搜尋範圍。由於公司補貨通常不局限於本地，極高數值（最大 20 萬）可允許公司在全圖範圍內尋找資源，防止在大地圖中出現材料荒。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LeisureMaxCost)), "休閒觀光最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LeisureMaxCost)),
                    "控制市民為了遊覽公園、地標或觀光願意承受的最大出行成本。數值越低，全圖無目的閒逛引發的尋路計算越少。\n" +
                    "★ 建議值：\n" +
                    " - 14km / 28km： 8000 ~ 12000\n" +
                    " - 57km / 114km：12000 ~ 20000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EmergencyMaxCost)), "醫院/犯罪最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EmergencyMaxCost)),
                    "控制市民生病就醫或犯罪時的最大搜尋範圍。較低的值將這些行為限制在就近區域，鼓勵在地化的公共服務規劃。\n" +
                    "★ 提示：建議在此成本範圍內合理配置醫院與警局。若相關設施非常密集，可進一步降低此值。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：4000 ~ 8000（預設：6000）"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindJobMaxCost)), "找工作最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindJobMaxCost)),
                    "控制市民為了尋求工作崗位，最多願意跨越多大規模的地圖。該行為頻率極低，建議直接拉滿（對性能影響不明顯）。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindHomeMaxCost)), "找房搬家最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindHomeMaxCost)),
                    "控制市民搬家找房時的最大搜尋範圍上限。提升此數值可讓市民跨越整張大地圖尋找新住宅，避免偏遠新城無人入住。該行為頻率較低，建議直接拉滿。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)), "找小學最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolElementaryMaxCost)),
                    "控制小學生尋找學校願意走的最遠路線開銷。較小的值能強迫小學生只能就近入學。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：10000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)), "找高中最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolHighSchoolMaxCost)),
                    "控制中學生尋找高中能夠跨越的最大路線開銷。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：17000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)), "找學院最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolCollegeMaxCost)),
                    "控制尋找學院(大專)級別的最大範圍。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：50000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)), "找大學最高尋路成本" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.FindSchoolUniversityMaxCost)),
                    "控制尋找大學的最大範圍。如果是全圖唯一的大學城城邦，建議拉滿以覆蓋全圖每個角落。\n" +
                    "★ 建議值：\n" +
                    " - 全地圖通用：100000 ~ 200000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetPathfinding)), "重設" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetPathfinding)),
                    "將所有尋路成本上限參數恢復為預設值。"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetPathfinding)),
                    "確認要將所有尋路參數重設為預設值嗎？"
                },

                // --- Group: 经济行为与吞吐量 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kEcoBehaviorGroup), "經濟行為與吞吐量 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.JobSeekerCap)), "找工作系統：求職吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.JobSeekerCap)),
                    "每次系統更新最多建立的求職者數量。城市人口越大可適當提高。\n" +
                    "較高的值加快就業尋找速度，但增加 CPU 負擔。可於遊戲中即時調節。\n" +
                    "★ 建議值：\n" +
                    " - 50萬以下人口：200 ~ 500\n" +
                    " - 200万人口：500 ~ 1000\n" +
                    " - 500万以上人口：1000 ~ 3000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PathfindRequestCap)), "找工作系統：尋路吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PathfindRequestCap)),
                    "每次尋路更新最多處理的求職尋路請求數量。通常為求職吞吐量的 2~4 倍。\n" +
                    "較高的值加快職缺匹配，但增加尋路系統的 CPU 負擔。可於遊戲中即時調節。\n" +
                    "★ 建議值：\n" +
                    " - 50万以下人口：1000 ~ 2000\n" +
                    " - 200万人口：2000 ~ 4000\n" +
                    " - 500万以上人口：4000 ~ 8000"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ShoppingTrafficReduction)), "購物機率人口壓制係數 (x0.0001)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ShoppingTrafficReduction)),
                    "控制城市人口對家庭購物機率的衰減影響。公式：shopChance = 200 / sqrt(係數 × 人口)。" +
                    "數值越大，高人口時購物機率越低，商業區交易量越少。\n\n" +
                    "★ 不同人口規模的效果對照：（以預設值 0.0004 為例）\n" +
                    " - 1万人口：shopChance ≈ 100% → 幾乎每戶都購物\n" +
                    " - 10万人口：shopChance ≈ 32% → 三分之一家庭購物\n" +
                    " - 100万人口：shopChance ≈ 10% → 十分之一家庭購物\n" +
                    " - 500万人口：shopChance ≈ 4% → 極少數家庭購物\n\n" +
                    "★ 按人口規模調節建議：\n" +
                    " - 10万以下小城市：0.0004（預設，與原版一致）\n" +
                    " - 10万~50万中型城市：0.0003 ~ 0.0004\n" +
                    " - 50万~200万大型城市：0.0002 ~ 0.0003\n" +
                    " - 200万以上超大城市：0.0001 ~ 0.0002\n\n" +
                    "可於遊戲中即時調節。降低此值可鼓勵消費、提升商業區收入。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)), "家庭購物需求倍率" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HouseholdResourceDemandMultiplier)),
                    "每次家庭購物時的資源購買量倍率。由於大地圖優化降低了購物頻率（tick頻率為原版1/2），" +
                    "需要增大單次購買量來補償，保持經濟系統總消費量平衡。\n\n" +
                    "★ 倍率與實際效果：\n" +
                    " - 1.0：與原版購買量一致（但頻率降低，總消費量不足）\n" +
                    " - 3.5：補償後約為原版總消費量的 70%~88%（預設值）\n" +
                    " - 5.0：接近完全補償原版消費水平\n" +
                    " - 8.0：超額補償，適合極大地圖 + 極低購物機率\n\n" +
                    "★ 按地圖/人口規模調節建議：\n" +
                    " - 14km原版地圖：1.0 ~ 2.0\n" +
                    " - 28km (ModeB)：2.0 ~ 3.5\n" +
                    " - 57km (ModeA)：3.5 ~ 5.0（預設 3.5）\n" +
                    " - 114km (ModeC)：5.0 ~ 8.0\n\n" +
                    "★ 觀察指標：如果商業區大量空置/倒閉，請提高此值。" +
                    "如果商品供不应求（工業產品被秒殺），請降低此值。\n" +
                    "可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomeSeekerCap)), "找房系統：搬家吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomeSeekerCap)),
                    "每幀最多處理的【已有住房但想搬家】的家庭數量。此參數控制系統每次更新(16幀/次)中評估搬家請求的速率。" +
                    "數值越大，搬家匹配越快，但單幀 CPU 開銷越高（FindPropertyJob 為單線程）。\n\n" +
                    "★ 調節建議：\n" +
                    " - 50万以下人口：64 ~ 128（預設）\n" +
                    " - 200万人口：128 ~ 256\n" +
                    " - 500万以上人口：256 ~ 512\n\n" +
                    "★ 如何判斷：若城市中大量家庭不主動搬遷（明明有更好住房），說明吞吐量不足，請提高此值。" +
                    "若遊戲出現卡頓，請降低此值。可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.HomelessSeekerCap)), "找房系統：流浪安置吞吐量" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.HomelessSeekerCap)),
                    "每幀最多處理的【無家可歸】家庭找房數量。流浪家庭的找房優先級高於搬家家庭。" +
                    "此參數決定無家可歸者被安置的速度。\n\n" +
                    "★ 調節建議：\n" +
                    " - 50万以下人口：640 ~ 1280（預設）\n" +
                    " - 200万人口：1280 ~ 2560\n" +
                    " - 500万以上人口：2560 ~ 5120\n\n" +
                    "★ 如何判斷：若城市中大量流浪漢長期無法入住空置住宅，請提高此值。" +
                    "若大量流浪同時湧入導致更新率驟降，請降低此值。可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetEcoBehavior)), "重設" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetEcoBehavior)),
                    "將所有經濟行為與吞吐量參數恢復為預設值。"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetEcoBehavior)),
                    "確認要將所有經濟行為參數重設為預設值嗎？"
                },

                // ============================================================
                // Tab 3: 租金管控
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kRentControlTab), "租金管控" },

                // --- Group: 地价因子 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kLandValueFactorGroup), "地價因子 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueEnvironmentEffect)), "環境影響係數" },
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
                    "縮放服務覆蓋（醫療、警務、教育、交通）對 Edge 地價加成的上限。\n\n" +
                    "★ 係數參考：\n" +
                    " - 100%：Mod 預設上限\n" +
                    " - 50%：上限減半 — 降低服務密集區地價的溢價\n" +
                    " - 200%：上限翻倍 — 放大服務設施的區位優勢\n\n" +
                    "可於遊戲中即時調節。"
                },

                // --- Group: 租金公式 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kRentFormulaGroup), "租金公式調節 (可於遊戲中即時調節)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RentMultiplierResidential)), "住宅租金總乘數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RentMultiplierResidential)),
                    "應用於所有住宅租金的總體乘數。100% = 等效原版公式。降低此值可全面降低住宅租金。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RentMultiplierCommercial)), "商業租金總乘數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RentMultiplierCommercial)),
                    "應用於所有商業租金的總體乘數。100% = 等效原版公式。降低此值有助於商業在高地價區域生存。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RentMultiplierIndustrial)), "工業租金總乘数" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RentMultiplierIndustrial)),
                    "應用於所有工業租金的總體乘數。100% = 等效原版公式。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueFactorResidential)), "住宅：地價貢獻係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueFactorResidential)),
                    "控制 Edge 地價對住宅租金的影響強度。0% = 租金完全忽略地價，100% = 原版公式權重。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueFactorCommercial)), "商業：地價貢獻係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueFactorCommercial)),
                    "控制 Edge 地價對商業租金的影響強度。降低此值有助於商鋪在黃金地段生存。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LandValueFactorIndustrial)), "工業：地價貢獻係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LandValueFactorIndustrial)),
                    "控制 Edge 地價對工業租金的影響強度。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LevelFactorResidential)), "住宅：等級貢獻係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LevelFactorResidential)),
                    "控制建築等級（升級層級）對住宅租金的貢獻程度。等級越高通常租金越貴。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LevelFactorCommercial)), "商業：等級貢獻係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LevelFactorCommercial)),
                    "控制建築等級對商業租金的貢獻程度。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.LevelFactorIndustrial)), "工業：等級貢獻係數" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.LevelFactorIndustrial)),
                    "控制建築等級對工業租金的貢獻程度。\n\n可於遊戲中即時調節。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.ResetRentControl)), "重設" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.ResetRentControl)),
                    "將所有租金管控參數恢復為預設值。"
                },
                {
                    m_Setting.GetOptionWarningLocaleID(nameof(ModSettings.ResetRentControl)),
                    "確認要將所有租金管控參數重設為預設值嗎？"
                },

                // ============================================================
                // Tab 4: 性能工具
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kPerformanceToolTab), "性能工具" },

                // --- Group: 存档转换 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kSaveConvertGroup), "存檔轉換 (實驗性)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EnableVanillaConversion)), "⚠ 原版地圖擴展 (高度實驗性)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EnableVanillaConversion)),
                    "⚠ 高度實驗性功能。啟用後可將原版 14km 存檔轉換到當前 MapExt 模式。\n\n" +
                    "★ 轉換時執行的操作：\n" +
                    " - 合成並擴展地形高度圖（無世界背景時自動生成平面地形）\n" +
                    " - 完美保留原版自然資源與地下水\n" +
                    " - 刪除所有外部連接節點（公路/鐵路/航空/航運）\n" +
                    " - 解鎖全部 529 格地圖分塊\n\n" +
                    "★ 已知問題：\n" +
                    " - 水體模擬可能需要轉換後透過 MapExt 遊戲內面板手動調整\n" +
                    " - 擴展區域可能出現模擬不穩定\n\n" +
                    "★ 使用風險自負。與「停用背景世界地圖」互斥。\n" +
                    "⚠ 啟用後需重啟遊戲。"
                },

                // --- Group: 地形性能 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kTerrainPerfGroup), "地形性能" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableWorldBackdrop)), "⚠ 停用背景世界地圖 (Backdrop)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableWorldBackdrop)),
                    "載入已有存檔時，阻止背景世界地圖（World Heightmap）生效。\n\n" +
                    "★ 性能收益：\n" +
                    " - 每幀節省約 0.5-2ms GPU 時間\n" +
                    " - 減少 ~37MB 顯示記憶體佔用\n" +
                    " - 消除 CPU 主執行緒同步阻塞等待 GPU 回讀\n\n" +
                    "★ 副作用：可玩區域外的遠景變為平坦地形。\n\n" +
                    "⚠ 開啟後儲存存檔，背景世界地圖數據將永久丟失！\n" +
                    "★ 與「原版存檔轉換」互斥。\n" +
                    "★ 提示：僅對包含背景世界地圖的存檔有效。無需重啟，重新載入存檔即可生效。"
                },

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
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.EditorCollisionSkip)), "跳過碰撞偵測 (編輯器)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.EditorCollisionSkip)),
                    "允許在地圖編輯器放置物件時跳過碰撞驗證檢查。'僅跳過樹木'可極大提升種植大量樹木時的性能，'跳過所有物件'則對所有物件生效。"
                },

                // --- Group: 游戏内 UI 外观 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kInGameUIGroup), "遊戲內 UI 外觀" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.UIMenuPanelWidth)), "左側選單面板寬度" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.UIMenuPanelWidth)),
                    "控制左側選單面板的寬度（rem 單位）。也可以在遊戲內透過拖曳面板邊緣調整。預設值：220，範圍：160~320。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.UIDetailPanelWidth)), "右側詳情面板寬度" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.UIDetailPanelWidth)),
                    "控制右側詳情面板的寬度（rem 單位）。也可以在遊戲內透過拖曳面板邊緣調整。預設值：260，範圍：200~450。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.UIPanelHeight)), "面板高度" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.UIPanelHeight)),
                    "控制左側面板的高度（rem 單位）。也可以在遊戲內透過拖曳面板底部調整。預設值：1000，範圍：300~1000。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DashboardDefaultCityStats)), "預設展開：城市統計" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DashboardDefaultCityStats)),
                    "勾選後，開啟儀表板時「城市統計」區塊將自動展開。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DashboardDefaultResidential)), "預設展開：住宅市場" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DashboardDefaultResidential)),
                    "勾選後，開啟儀表板時「住宅市場」區塊將自動展開。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DashboardDefaultCommercial)), "預設展開：商業市場" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DashboardDefaultCommercial)),
                    "勾選後，開啟儀表板時「商業市場」區塊將自動展開。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DashboardDefaultActivity)), "預設展開：人口活動" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DashboardDefaultActivity)),
                    "勾選後，開啟儀表板時「人口活動」區塊將自動展開。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DashboardDefaultMisc)), "預設展開：其他" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DashboardDefaultMisc)),
                    "勾選後，開啟儀表板時「其他」區塊將自動展開。"
                },

                // ============================================================
                // Tab 5: 界面
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kUITab), "介面" },

                // ============================================================
                // Tab 6: 开发者选项
                // ============================================================
                { m_Setting.GetOptionTabLocaleID(ModSettings.kDebugTab), "開發者選項" },

                // --- Group: 人口诊断 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kPopDiagGroup), "人口診斷" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.PopDiagReport)), "診斷報告" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.PopDiagReport)),
                    "人口健康度即時快照：\n" +
                    " - Households：常駐家庭總數與住房率\n" +
                    " - MovingAway：正在離開城市的家庭（靜默刪除候選）\n" +
                    " - Homeless：當前無家可歸的家庭\n" +
                    " - Seekers：正在找房的家庭（housed = 升級/被驅逐，homeless = 新到達）\n" +
                    " - HighRent Buildings：高租金警告建築數\n\n" +
                    "點擊「重新整理」更新。此診斷僅讀取數據，不影響遊戲狀態。"
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.RefreshPopDiag)), "↻ 重新整理診斷" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.RefreshPopDiag)),
                    "點擊以查詢當前 ECS 世界中的人口健康度指標。結果顯示在上方。"
                },

                // --- Group: 开发者选项 ---
                { m_Setting.GetOptionGroupLocaleID(ModSettings.kDebugGroup), "開發者選項" },
                { m_Setting.GetOptionLabelLocaleID(nameof(ModSettings.DisableLoadGameValidation)), "× 禁止遊戲讀取存檔驗證" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(ModSettings.DisableLoadGameValidation)),
                    "⚠️ 警告！預設(不勾選)為啟用遊戲讀取存檔驗證，以防止錯誤設定地圖尺寸模式而讀取不同尺寸的存檔造成損壞存檔！\n該選項勾選後將取消驗證，僅用於使用舊版 MapExt mod 特殊尺寸模式而無法正確識別的情況。使用舊版存檔請務必確認 '地圖尺寸模式' 是否設定正確，否則可能損壞存檔！\n務必在使用該功能前備份您的存檔"
                },
            };
            return entries;
        }

        public void Unload()
        {
        }
    }
}
