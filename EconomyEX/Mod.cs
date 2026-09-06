using System;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.PSI;
using Game.SceneFlow;
using Game.UI.Localization;
using HarmonyLib;
using EconomyEX.Systems;
using EconomyEX.Settings;
using EconomyEX.Helpers;

namespace EconomyEX
{
    public class Mod : IMod
    {
        public const string ModName = "EconomyEX";
        public static ILog Logger = LogManager.GetLogger($"{ModName}").SetShowsErrorsInUI(false);
        public static void Info(string text) => Logger.Info(text);
        public static void Warn(string text) => Logger.Warn(text);
        public static void Error(string text) => Logger.Error(text);
        public static void Error(Exception e, string text) => Logger.Error(e, text);
        public static void Debug(string text) => Logger.Info(text);

        public static Mod Instance { get; private set; }
        public ModSettings Settings { get; private set; }
        
        private Harmony _harmony;
        public const string HarmonyId = "EconomyEX.Patch";

        // State Flags
        public static bool IsMapExtPresent { get; private set; } = false;
        public static bool IsVanillaMap { get; private set; } = false; // Set by MapSizeDetector
        public static bool IsActive { get; private set; } = false; // Effective Active State

        /// <summary>
        /// <see cref="OnLoad"/> 是否中斷過。中斷後本 Mod 處於半初始化狀態。
        /// </summary>
        public static bool InitializationFailed { get; private set; }

        /// <summary>
        /// Mod 載入入口。本方法只負責兜底，實際初始化在 <see cref="OnLoadCore"/>。
        ///
        /// <para><b>為什麼需要兜底</b>：<c>ModManager.InitializeMods</c> 對逸出的異常只寫一行
        /// <c>[Modding] [ERROR]</c> 就繼續，<b>不彈窗</b>，於是「整個 Mod 沒生效」會靜默發生。
        /// MapExtPDX 於 2026-09-05 實測到一次（本地化註冊撞上引擎無鎖的字典），
        /// 本 Mod 的 <c>AddSource</c> 走同一條引擎路徑，暴露面相同，故一併加固。</para>
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
                Error(ex, "OnLoad 中斷，本次遊戲 EconomyEX 未完整生效");
                PushInitFailureNotification(ex);
            }
        }

        private void OnLoadCore(UpdateSystem updateSystem)
        {
            Instance = this;

            // 1. Check for MapExt — 必须在所有初始化之前
            if (CheckMapExtPresence())
            {
                IsMapExtPresent = true;
                Logger.Warn("MapExtPDX detected. EconomyEX will NOT initialize (fully dormant). No conflicts.");
                return; // 完全跳过：不注册 Settings/UI/Harmony/Systems
            }

            Info($"Loading {ModName}...");

            // 2. Initialize Settings
            Settings = new ModSettings(this);
            Settings.RegisterInOptionsUI();
            var lm = GameManager.instance.localizationManager;
            AddSourceSafe(lm, "en-US", new LocaleEN(Settings));
            AddSourceSafe(lm, "zh-HANS", new LocaleHANS(Settings));
            AddSourceSafe(lm, "zh-HANT", new LocaleHANT(Settings));
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(ModName, Settings, new ModSettings(this));
            Settings.UpdateStatus();

            // Scan for known conflicting mods
            ModConflictDetector.ScanLoadedMods();
            Settings._detectedConflictMods = ModConflictDetector.GetDetectedModsSummary();
            var conflictReport = ModConflictDetector.GetConflictReport(Settings);
            if (conflictReport != "None")
            {
                Settings._conflictWarning = $"[Startup] {conflictReport}";
                Warn($"启动冲突报告: {conflictReport}");
            }

            // 3. Initialize Harmony
            _harmony = new Harmony(HarmonyId);

            // 4. Install Map Size Detector
            // This patch checks the map size when a map is loaded.
            MapSizeDetector.Install(_harmony);
            Info("MapSizeDetector installed.");

            // 5. Auto-disable conflicting groups BEFORE system registration
            var disabledGroups = ModConflictDetector.AutoDisableConflictGroups(Settings);
            if (disabledGroups.Count > 0)
            {
                Settings._conflictWarning = $"[Auto-Disabled] {string.Join(", ", disabledGroups)}";
                Settings._systemStatusReport = $"Auto-disabled {disabledGroups.Count} group(s) due to conflicts";
            }

            // 6. Register Systems (but keep them disabled/unpatched until MapSize is verified)
            // We Register them to the World so they exist, but we don't enable them yet.
            // Actual enabling happens in MapSizeDetector.OnVanillaMapDetected()
            SystemRegistrar.RegisterSystems(updateSystem);
            
            // 7. Install Conflict Monitoring (passive diagnostic)
            updateSystem.UpdateAt<ConflictMonitoringSystem>(SystemUpdatePhase.MainLoop);

            // 8. Main menu notification
            if (disabledGroups.Count > 0)
            {
                // pageId = AssemblyName.Namespace.TypeName (见 ModSetting 构造函数)
                const string pageId = "EconomyEX.EconomyEX.Mod";
                const string sectionId = "EconomyEX.EconomyEX.Mod.Status";

                NotificationSystem.Push(
                    identifier: "economyex.conflict",
                    title: LocalizedString.Value("RESTART REQUIRED!!!"),
                    text: LocalizedString.Value(
                        $"[EconomyEX] Disabled {disabledGroups.Count} group(s) " +
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
        }

        /// <summary>
        /// 包一層 try-catch 的 <c>AddSource</c>。
        /// <para>引擎的 <c>LocalizationDictionary</c> 是無鎖的裸 <c>Dictionary</c>：註冊
        /// fallback 語言（en-US）時會觸發一趟遍歷整個字典的 <c>MergeFrom</c>，
        /// 期間若有別的執行流寫入就拋 <c>Collection was modified</c>。那是引擎缺陷、
        /// Mod 無從預防；本地化失敗的實際影響只是「該語言文字顯示為 key」，
        /// 不該擴散成整個 OnLoad 中斷。</para>
        /// </summary>
        private static void AddSourceSafe(
            Colossal.Localization.LocalizationManager lm, string localeId, Colossal.IDictionarySource source)
        {
            try
            {
                lm.AddSource(localeId, source);
            }
            catch (Exception ex)
            {
                Warn($"註冊 {localeId} 本地化失敗（引擎本地化字典無鎖，疑撞上並發寫入），該語言文字將顯示為 key: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化中斷時推一則主菜單通知。文案硬編碼英文並用 <c>LocalizedString.Value</c>
        /// （字面值、不查表）——中斷點有可能正是本地化註冊本身。
        /// </summary>
        private static void PushInitFailureNotification(Exception ex)
        {
            try
            {
                NotificationSystem.Push(
                    identifier: "economyex.init_failed",
                    title: LocalizedString.Value("EconomyEX: INITIALIZATION FAILED"),
                    text: LocalizedString.Value(
                        "EconomyEX did not finish loading, so the economy system replacements are NOT active " +
                        $"this session. Restart the game; if it repeats, report Logs/EconomyEX.log ({ex.GetType().Name})."),
                    progressState: Colossal.PSI.Common.ProgressState.Failed,
                    progress: 100
                );
            }
            catch (Exception pushEx)
            {
                Warn($"初始化失敗通知推送失敗: {pushEx.Message}");
            }
        }

        public void OnDispose()
        {
            Info("Disposing...");
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
            if (_harmony != null)
            {
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
            }
            IsActive = false;
        }

        private bool CheckMapExtPresence()
        {
            // 使用 PDX modManager 而非 AppDomain.GetAssemblies()
            // modManager 在所有 mod 的 OnLoad() 前已填充完毕，不受加载顺序影响
            foreach (var modInfo in GameManager.instance.modManager)
            {
                var name = modInfo.asset.name;
                if (string.IsNullOrEmpty(name)) continue;
                if (name.Contains("MapExt2") || name.Contains("MapExtPDX"))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Called by MapSizeDetector when a Vanilla map (<= 14km) is detected.
        /// </summary>
        public void ActivateEconomyFix()
        {
            if (IsMapExtPresent) return;

            // [BUGFIX] 系統 Enabled 狀態必須每次載入都重新套用——引擎在退回主菜單時會把世界內
            // 所有系統復活成 Enabled = true。MapSizeDetector 每次 FinalizeTerrainData 都會呼叫
            // 本方法，但舊版把 IsActive 守衛放在最前面，於是二次載入時整段被跳過，導致原版與
            // 替換系統同時運行，且不寫任何錯誤日誌。SetSystemEnabled 為 idempotent，重複套用
            // 安全；Harmony patch 不可重複掛載，故仍留在守衛之後。
            // 做法對照 MapExtPDX 的 SystemReplacer.ReDisableVanillaSystems。
            SystemRegistrar.EnableEconomySystems();

            // 這兩項同樣是直接寫原版系統的 Enabled（HouseholdPetSpawnSystem /
            // TrafficSpawnerAISystem），一併每次載入重新套用——否則二次載入後
            // NoDogs 與 NoThroughTraffic 兩個設定會靜默回退成原版行為。
            Settings?.UpdateNoDogsSystemStates();
            Settings?.UpdateNoThroughTrafficSystemStates();

            if (IsActive) return; // 以下僅首次啟用時執行

            Info("Activating Economy Fixes...");
            IsActive = true;
            IsVanillaMap = true; // Confirmed
            
            // Apply Job Patches (if any)
            JobPatchHelper.Apply(_harmony, JobPatchDefinitions.GetEcoSystemTargets());

            // Apply manual Harmony Patches for System Replacements
            _harmony.CreateClassProcessor(typeof(PathfindSetupSystem_FindTargets_Patch)).Patch();
            _harmony.CreateClassProcessor(typeof(LandValueSystemMod.Patches)).Patch();
            _harmony.CreateClassProcessor(typeof(ServiceCoverageSystem_SetupPathfindMethods_Patch)).Patch();

            // Apply GPU optimization patches (Backdrop Disable + Water Sim Quality)
            _harmony.CreateClassProcessor(typeof(TerrainBackdropDisablePatch)).Patch();
            _harmony.CreateClassProcessor(typeof(WaterSystemOptRuntimePatch)).Patch();

            Settings.UpdateStatus();
        }

        /// <summary>
        /// Called by MapSizeDetector when a Large map is detected.
        /// </summary>
        public void DeactivateEconomyFix()
        {
             if (IsMapExtPresent) return;

             // [BUGFIX] 同 ActivateEconomyFix：Mod 系統同樣會被引擎復活，大地圖上必須每次載入
             // 都重新禁用，否則二次載入後 EconomyEX 的替換系統會在它不支援的大地圖上運行。
             // Revert or Disable our systems?
             // Ideally we should Unpatch, but Harmony Unpatching at runtime can be risky or complex.
             // For now, we will just Disable our systems and Re-enable Vanilla ones.
             SystemRegistrar.DisableEconomySystems();

             // IsVanillaMap 描述「當前地圖」而非 patch 狀態，每次載入都必須更新。
             IsVanillaMap = false;

             if (IsActive)
             {
                 Info("Deactivating Economy Fixes (Large Map Detected)...");
                 IsActive = false;
             }

             // Note: Transpilers (JobPatches) are hard to revert at runtime without a restart usually,
             // but since we only patch on Load, we might be stuck with them if we switch maps without restarting.
             // However, our Job Patches are designed to be replacements.
             // If we are on a Large Map, we SHOULD NOT run this mod at all.
             // If the user switches from Vanilla -> Large Map in one session:
             // The MapSizeDetector run at 'FinalizeTerrainData' which happens during map load.

             // [BUGFIX] UpdateStatus 必須無條件執行：舊版排在 if (!IsActive) return 之後，
             // 而「首次就載入大地圖」時 IsActive 本來就是 false，於是設定頁永遠停在
             // "IDLE: Waiting for map load..."，玩家看不到「已因大地圖停用」。
             // 它讀 IsActive／IsVanillaMap，故排在兩者定案之後。
             Settings?.UpdateStatus();
        }
    }
}
