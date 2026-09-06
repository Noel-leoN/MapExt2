// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Reflection;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.PSI;
using Game.SceneFlow;
using Game.UI.Localization;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using MapExtPDX.SaveLoadSystem;

namespace MapExtPDX
{
    public class Mod : IMod
    {
        private const string Tag = "Mod";

        public const string ModName = "MapExtPDX"; // 保持与BepInEx版本一致

        // public const string ModFileName = "MapExtPDX2";
        public const string ModNameZH = "大地图mod";
        public static string ModVersion => Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        // 定义公共静态实例
        // (IMod标准接口仅包含OnLoad/Dispose，如自定义公共方法，需创建静态实例)
        public static Mod Instance { get; private set; }

        // 定义设置UI
        private ModSettings m_Setting;
        public ModSettings CurrentSettings => m_Setting;

        /// <summary>
        /// Settings 别名，使 EcoSystems 代码与 EconomyEX 保持一致的访问路径。
        /// </summary>
        public ModSettings Settings => m_Setting;

        // 日志初始化
        // 日志归结到Logs\ModName.log，不要放在Player.log
        public static ILog Logger = LogManager.GetLogger($"{ModName}").SetShowsErrorsInUI(false);
        public static void Info(string text) => Logger.Info(text);
        public static void Debug(string text) => Logger.Debug(text);
        public static void Warn(string text) => Logger.Warn(text);
        public static void Error(string text) => Logger.Error(text);
        public static void Error(Exception e, string text) => Logger.Error(e, text);

        // 定义Harmony
        public static readonly string HarmonyId = ModName; // 弃用

        /// <summary>
        /// 双Harmony实例模式
        /// </summary>
        //  双Harmony实例名称定义
        public static readonly string HarmonyIdGlobal = $"{ModName}_global";

        public static readonly string HarmonyIdModes = $"{ModName}_modes";

        // 用于全局并行补丁定义
        private Harmony _globalPatcher;

        // 用于MapSize模式选择补丁集定义
        private Harmony _modePatcher;

        // --- unpatch标志位，用于ReBurst安全卸载 ---
        public static bool IsUnloading { get; private set; } = false;

        /// <summary>
        /// <see cref="OnLoad"/> 是否中斷過。中斷後本 Mod 處於<b>半初始化</b>狀態——
        /// 補丁可能只套用了一部分，不等於「乾淨地沒生效」。
        /// </summary>
        public static bool InitializationFailed { get; private set; }

        /// <summary>
        /// Mod 載入入口（首次進入主菜單時執行一次）。本方法只負責兜底，
        /// 實際初始化全在 <see cref="OnLoadCore"/>。
        ///
        /// <para><b>為什麼需要自己兜底</b>：<c>ModManager.InitializeMods</c> 對逸出的異常
        /// 只寫一行 <c>[Modding] [ERROR]</c> 就繼續，<b>不彈窗</b>。於是「整個 Mod 沒生效」
        /// 會完全靜默地發生——2026-09-05 實測過一次（第一次 <c>AddSource</c> 撞上引擎無鎖的
        /// 本地化字典，見 <see cref="MapExt.Core.CompositeLocaleSource"/>），
        /// 20 個 patchset、SystemReplacer、71 個 UI binding 全部沒跑，而使用者只看到卡頓與
        /// 滑鼠縮放失效，只能反推。這裡捕獲後推一則主菜單通知，讓失效變成可見事件。</para>
        /// </summary>
        public void OnLoad(UpdateSystem updateSystem)
        {
            try
            {
                OnLoadCore(updateSystem);
            }
            catch (Exception ex)
            {
                InitializationFailed = true;
                ModLog.Error(Tag, $"OnLoad 中斷，本次遊戲 MapExt 未完整生效: {ex}");
                PushInitFailureNotification(ex);
            }
        }

        private void OnLoadCore(UpdateSystem updateSystem)
        {
            // === 0. 加载模组执行asset ===
            ModLog.Info(Tag, $"OnLoad, version:{ModVersion}");
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                ModLog.Info(Tag, $"{asset.name} v{asset.version} mod asset at {asset.path}");

            // === 初始化双Harmony实例 ===
            _globalPatcher = new Harmony(HarmonyIdGlobal);
            ModLog.Patch(Tag, "HarmonyGlobal 实例已创建");
            _modePatcher = new Harmony(HarmonyIdModes);
            ModLog.Patch(Tag, "HarmonyModes 实例已创建");

            // === B. 获取设置setting ===
            // 将当前实例赋值给静态属性(use for Setting)
            Instance = this;

            // Initialize settings
            m_Setting = new ModSettings(this);
            m_Setting.RegisterInOptionsUI();
            // 讀取本地化語言庫：設定 UI 文本與對話框文本合併為「每語言一次註冊」，
            // 且每次註冊獨立 try-catch。理由見 RegisterLocalization。
            RegisterLocalization();
            // 读取已保存设置
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(ModName, m_Setting, new ModSettings(this));
            ModLog.Ok(Tag, "Settings 已初始化");

            // 兩個 SaveConvert 開關在 UI 上互鎖，但外部工具（Simple Mod Checker Plus 的
            // 「啟動時恢復配置」等）可能把 .coc 寫成兩者皆 true 的非法組合，載入後歸一化一次
            m_Setting.NormalizeSaveConvertExclusion();

            // === 存檔轉換開關的持久化排查（Release 亦輸出） ===
            // 「OptionUI 勾了、重啟又跳回未選中」要先切開兩件事：值有沒有寫進 .coc，
            // 以及 .coc 的值有沒有被讀回運行時。這行印的是反序列化完成後的實際值，
            // 可直接與 ModsSettings/MapExtPDX/MapExtPDX.coc 的內容離線對照：
            //   兩邊都 true  → 持久化正常，問題在別處；
            //   檔案 true／此處 false → 反序列化在此鍵之前中止（看同批鍵是否也丟）；
            //   檔案 false → 值沒落盤、或落盤後被外部改回。實測後者更常見：先查
            //                _GameLogs/SimpleModCheckerPlus.log 有無 Restoring 'MapExtSettings:…'
            //                （它的還原跑在本 Mod OnLoad 之後，症狀是隔一次啟動才回退），
            //                再看 ModSettings 的撞名探測警告。
            // 設值時機另有 EnableVanillaConversion／DisableWorldBackdrop 兩個 setter 的
            // 🔄 變更日誌可交叉比對（本行之前出現＝反序列化，之後＝使用者操作）。
            ModLog.Info(Tag,
                $"SaveConvert 載入值: EnableVanillaConversion={m_Setting.EnableVanillaConversion}, " +
                $"DisableWorldBackdrop={m_Setting.DisableWorldBackdrop}");

            // 此後 setter 的呼叫一律來自使用者操作而非反序列化，撞名探測才有意義
            ModSettings.s_settingsLoaded = true;

#if DEBUG
            // === 设置值验证日志（仅 DEBUG 编译有效） ===
            // 用于诊断 .coc 文件加载异常或框架缓存不一致问题
            ModLog.Debug(Tag, "=== Settings Dump (DEBUG) ===");
            ModLog.Debug(Tag, $"  PatchMode={m_Setting.PatchModeChoice}");
            ModLog.Debug(Tag, $"  TerrainRes={m_Setting.TerrainResolution}, WaterRes={m_Setting.WaterResolution}");
            ModLog.Debug(Tag,
                $"  WaterSimQuality={m_Setting.WaterSimQuality}, WaterTexFmt={m_Setting.WaterTextureFormat}");
            ModLog.Debug(Tag, $"  TerrainBufferPrealloc={m_Setting.TerrainBufferPrealloc}");
            ModLog.Debug(Tag,
                $"  TerrainCascadeThrottle={m_Setting.TerrainCascadeThrottle}, TerrainCullThrottle={m_Setting.TerrainCullThrottle}");
            ModLog.Debug(Tag,
                $"  EnableVanillaConversion={m_Setting.EnableVanillaConversion}, DisableWorldBackdrop={m_Setting.DisableWorldBackdrop}");
            ModLog.Debug(Tag, $"  EconomyFix={m_Setting.isEnableEconomyFix}");
            ModLog.Debug(Tag,
                $"  EcoSystems: Demand={m_Setting.EnableDemandEcoSystem}, JobSearch={m_Setting.EnableJobSearchEcoSystem}");
            ModLog.Debug(Tag,
                $"  EcoSystems: HouseholdProp={m_Setting.EnableHouseholdPropertyEcoSystem}, ResBuyer={m_Setting.EnableResourceBuyerEcoSystem}");
            ModLog.Debug(Tag,
                $"  EcoSystems: ResidentAI={m_Setting.EnableResidentAIEcoSystem}, DownstreamAI={m_Setting.EnableDownstreamAIEcoSystem}");
            ModLog.Debug(Tag, $"  NoDogs: Street={m_Setting.NoDogsOnStreet}, Gen={m_Setting.NoDogsGeneration}");
            ModLog.Debug(Tag, $"  NoThroughTraffic={m_Setting.NoThroughTraffic}");
            ModLog.Debug(Tag, $"  DisableLoadGameValidation={m_Setting.DisableLoadGameValidation}");
            ModLog.Debug(Tag, "=== End Settings Dump ===");
#endif

            // === C. 初始化MapSize PatchManager ===
            // 应用启动时默认的补丁模式
            // m_Setting.PatchModeChoice 从配置文件中加载上次保存的模式
            // 从设置加载初始模式
            PatchModeSetting initialMode = m_Setting.PatchModeChoice;
            ModLog.Info(Tag, $"正在初始化 PatchManager 使用设置模式: {m_Setting.PatchModeChoice}");
            // 执行MapSize关联主要系统补丁
            PatchManager.Initialize(_modePatcher, initialMode);


            // === D. 存档验证系统 === 
            ModLog.Info(Tag, "全局并行方式补丁正在逐条执行...");
            // 4.1 加载SaveLoadSystem的2个class补丁
            if (m_Setting.DisableLoadGameValidation == false)
            {
                _globalPatcher.CreateClassProcessor(typeof(MetaDataExtenderPatch)).Patch();
                ModLog.Patch(Tag, $"{nameof(MetaDataExtenderPatch)} 已应用");
                _globalPatcher.CreateClassProcessor(typeof(LoadGameValidatorPatch)).Patch();
                ModLog.Patch(Tag, $"{nameof(LoadGameValidatorPatch)} 已应用");
            }

            // 其他并行的选项补丁，也在这里添加
            // _globalPatcher.CreateClassProcessor(typeof(ParallelOptionPatch)).Patch();
            // 對話框本地化已隨設定文本在 RegisterLocalization 一併註冊（合併為每語言一次）

            // 4.2 注册原版存档转换系统
            updateSystem.UpdateAt<VanillaSaveConversionSystem>(SystemUpdatePhase.LoadSimulation);
            ModLog.Patch(Tag, $"{nameof(VanillaSaveConversionSystem)} 已注册到 LoadSimulation");

            // === E. 性能工具 ===
            // 其他并行的选项补丁
            // 手动ECS调用Apply方式以应用已保存的设置值
            // --- 加载NoDogs ---
            m_Setting.UpdateNoDogsSystemStates();

            // --- 加载NoTroughTraffic(From CS2LiteBooster) ---
            m_Setting.UpdateNoThroughTrafficSystemStates();

            // --- 加载NoRandomTraffic(From CS2LiteBooster) ---
            // m_Setting.UpdateNoRandomTrafficSystemStates();

            // === F. 加载特色工具 ===
            // 加载LandValueRemake
            // m_Setting.UpdateLandValueRemakeSystemStates();
            //Info($"LandValue Remake补丁(全局并行) {nameof(ModLocalization)}已应用.");

            // 执行诊断系统
            // updateSystem.UpdateAt<SaveGameDiagnosticSystem>(SystemUpdatePhase.LateUpdate);

            // === G. 冲突 Mod 指纹检测 ===
            ModConflictDetector.ScanLoadedMods();
            m_Setting._detectedConflictMods = ModConflictDetector.GetDetectedModsSummary();
            // 根据检测结果生成基于当前设置的冲突报告
            var conflictReport = ModConflictDetector.GetConflictReport(m_Setting);
            if (conflictReport != "None")
            {
                m_Setting._conflictWarning = $"[Startup] {conflictReport}";
                ModLog.Warn(Tag, $"启动冲突报告: {conflictReport}");
            }

            // 自动禁用与冲突 Mod 重叠的系统组（在 SystemReplacer.Apply 之前执行！）
            var disabledGroups = ModConflictDetector.AutoDisableConflictGroups(m_Setting);
            if (disabledGroups.Count > 0)
            {
                m_Setting._conflictWarning = $"[Auto-Disabled] {string.Join(", ", disabledGroups)}";
                m_Setting._systemStatusReport = $"Auto-disabled {disabledGroups.Count} group(s) due to conflicts";
            }

            // === H. ECS替换系统补丁 ===
            // CellMapSystem<T> 和 经济系统 的ECS替换补丁
            SystemReplacer.Apply(updateSystem, _globalPatcher, m_Setting);

            // === I. 主菜单通知（冲突提醒） ===
            if (disabledGroups.Count > 0)
            {
                // pageId = AssemblyName.Namespace.TypeName (见 ModSetting 构造函数)
                const string pageId = "MapExt2.MapExtPDX.Mod";
                const string sectionId = "MapExt2.MapExtPDX.Mod.EconomyEX";

                NotificationSystem.Push(
                    identifier: "mapext.conflict",
                    title: LocalizedString.Value("RESTART REQUIRED!!!"),
                    text: LocalizedString.Value(
                        $"[MapExt2] Disabled {disabledGroups.Count} group(s) " +
                        $"due to mod conflicts: {string.Join(", ", disabledGroups)}"),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100,
                    onClicked: () =>
                    {
                        var optionsUI = Unity.Entities.World.DefaultGameObjectInjectionWorld?
                            .GetExistingSystemManaged<Game.UI.Menu.OptionsUISystem>();
                        optionsUI?.OpenPage(pageId, sectionId, false);
                    }
                );
            }

            // === J. RPF 硬冲突通知 ===
            // RPF 的 UpdateGroupSystem 跨阶段注册会导致 ECB 崩溃，两者不能共存
            if (ModConflictDetector.HasRealisticPathFinding)
            {
                NotificationSystem.Push(
                    identifier: "mapext.rpf_incompatible",
                    title: LocalizedString.Value("MapExt2: Incompatible Mod Detected"),
                    text: LocalizedString.Value(
                        "[MapExt2] RealisticPathFinding (RPF) is incompatible with MapExt2. " +
                        "RPF's cross-phase system registration causes EntityCommandBuffer crashes. " +
                        "Please disable one of them to avoid errors."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100
                );
                ModLog.Error(Tag, "RPF 与 MapExt2 存在不可调和的 ECS 管线冲突（UpdateGroupSystem 跨阶段注册），请禁用其中之一。");
            }
        }

        /// <summary>
        /// 註冊三語本地化：每個語言只呼叫一次 <c>AddSource</c>，且各自獨立 try-catch。
        ///
        /// <para><b>為什麼要合併</b>：原本設定文本（<c>LocaleEN</c>／HANS／HANT）與對話框文本
        /// （<c>ModLocalization</c>）各註冊三次、共 6 次，其中 <b>2 次落在 en-US</b>。
        /// en-US 是引擎的 fallback 語言，每次註冊都會多觸發一趟 <c>MergeFrom</c>
        /// 遍歷整個 fallback 字典，而那個字典無鎖——正是 2026-09-05 那次 OnLoad 中斷的
        /// 競爭窗口。合併後 en-US 只註冊一次，窗口減半。</para>
        ///
        /// <para><b>為什麼要 try-catch</b>：本地化註冊失敗的實際影響只是「該語言的文字顯示成
        /// key」，不該讓整個 Mod 失效。完整論證見
        /// <see cref="MapExt.Core.CompositeLocaleSource"/>。</para>
        /// </summary>
        private void RegisterLocalization()
        {
            ModLocalization.GetSources(out var dialogEn, out var dialogHans, out var dialogHant);
            var lm = GameManager.instance.localizationManager;

            int ok = 0;
            if (AddSourceSafe(lm, "en-US", new CompositeLocaleSource(new LocaleEN(m_Setting), dialogEn))) ok++;
            if (AddSourceSafe(lm, "zh-HANS", new CompositeLocaleSource(new LocaleHANS(m_Setting), dialogHans))) ok++;
            if (AddSourceSafe(lm, "zh-HANT", new CompositeLocaleSource(new LocaleHANT(m_Setting), dialogHant))) ok++;

            if (ok == 3)
            {
                ModLog.Ok(Tag, "本地化已註冊 (en-US, zh-HANS, zh-HANT；設定文本與對話框文本已合併為每語言一份)");
            }
            else
            {
                ModLog.Warn(Tag, $"本地化僅 {ok}/3 個語言註冊成功；未成功者其文字會顯示為 key，功能不受影響");
            }
        }

        /// <summary>
        /// 包一層 try-catch 的 <c>AddSource</c>，回傳是否成功。
        /// <para>引擎的 <c>LocalizationDictionary</c> 是無鎖的裸 <c>Dictionary</c>，
        /// <c>AddSource</c> 可能因別的執行流同時寫入 fallback 字典而拋
        /// <c>Collection was modified</c>。那是引擎缺陷、Mod 無從預防，
        /// 這裡只確保它不擴散成整個 OnLoad 中斷。</para>
        /// </summary>
        private static bool AddSourceSafe(
            Colossal.Localization.LocalizationManager lm, string localeId, Colossal.IDictionarySource source)
        {
            try
            {
                lm.AddSource(localeId, source);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"註冊 {localeId} 本地化失敗（引擎本地化字典無鎖，疑撞上並發寫入）: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 初始化中斷時推一則主菜單通知，讓「Mod 沒生效」變成可見事件。
        /// <para>文案<b>硬編碼英文</b>並用 <c>LocalizedString.Value</c>（字面值、不查表）：
        /// 中斷點有可能正是本地化註冊本身，那時查表必然拿不到譯文。</para>
        /// </summary>
        private static void PushInitFailureNotification(Exception ex)
        {
            try
            {
                NotificationSystem.Push(
                    identifier: "mapext.init_failed",
                    title: LocalizedString.Value("MapExt2: INITIALIZATION FAILED"),
                    text: LocalizedString.Value(
                        "MapExt2 did not finish loading, so map extension is NOT fully active this session. " +
                        "Do NOT load or save an extended-size city now - save corruption is possible. " +
                        $"Restart the game; if it repeats, report Logs/MapExtPDX.log ({ex.GetType().Name})."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100
                );
            }
            catch (Exception pushEx)
            {
                // 連通知都推不出去（UI 尚未就緒等），至少讓日誌留痕
                ModLog.Warn(Tag, $"初始化失敗通知推送失敗: {pushEx.Message}");
            }
        }

        // 被Settings中的Apply按钮调用
        public void OnPatchModeChanged(PatchModeSetting newModeFromSettings)
        {
            if (m_Setting == null) return;
            // 通知PatchManager使用设置中当前选定的新模式
            ModLog.Swap(Tag, $"MapSize Mode 在设置UI中改变为: {newModeFromSettings}");
            // 关键方法，切换应用补丁模式集
            PatchManager.SetPatchMode(newModeFromSettings);
            //m_Setting?.RefreshModSettingInfo(); // Update status display
        }

        public void ApplyPatchChangesFromSettings(PatchModeSetting modeToApply)
        {
            PatchManager.SetPatchMode(modeToApply);
            ModLog.Ok(Tag, $"已应用 MapSize Mode: {modeToApply}");
        }

        public void OnDispose()
        {
            ModLog.Info(Tag, nameof(OnDispose));

            IsUnloading = true;

            // 在Mod卸载时移除所有补丁
            // 必須傳入自己的 Harmony id：Harmony 的 UnpatchAll(null) 會對每個 patch 的
            // owner 檢查一律放行，且它遍歷的 GetAllPatchedMethods() 作用範圍是整個
            // appdomain——不傳 id 等於剝除進程內所有 Mod 的 patch，不只自己的。
            // 危害路徑：ModManager 在單一 Mod 初始化失敗時只 Dispose 該 Mod，此刻其他
            // Mod 已載入完成，其 patch 會被靜默清空。
            // （Harmony 2.2.2 無 UnpatchSelf，傳自身 id 是唯一手段。）
            _globalPatcher?.UnpatchAll(HarmonyIdGlobal);
            _modePatcher?.UnpatchAll(HarmonyIdModes);
            _globalPatcher = null;
            _modePatcher = null;
            ModLog.Ok(Tag, "所有 Harmony 补丁已移除");

            // 卸载Settings
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }

        /// <summary>
        /// Gets the current game mode.
        /// This method can be called by ModSettings.IsVisibleInMainMenu.
        /// </summary>
        public GameMode GetCurrentGameMode()
        {
            var gameMode = GameManager.instance.gameMode;
            ModLog.Info(Tag, $"当前游戏模式为 {gameMode}");
            return gameMode;
        }
    } // class Mod
} // namespace MapExtPDX
