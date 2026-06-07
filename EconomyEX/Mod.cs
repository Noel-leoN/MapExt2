using System;
using System.Linq;
using System.Reflection;
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

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            Info($"Loading {ModName}...");

            // 1. Check for MapExt
            if (CheckMapExtPresence())
            {
                Warn("MapExt detected! EconomyEX will remain DORMANT to avoid conflicts.");
                IsMapExtPresent = true;
                // We still load settings to explain why it's disabled, but we don't patch anything.
            }

            // 2. Initialize Settings
            Settings = new ModSettings(this);
            Settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(Settings));
            GameManager.instance.localizationManager.AddSource("zh-HANS", new LocaleHANS(Settings));
            GameManager.instance.localizationManager.AddSource("zh-HANT", new LocaleHANT(Settings));
            Colossal.IO.AssetDatabase.AssetDatabase.global.LoadSettings(ModName, Settings, new ModSettings(this));
            
            // Update UI status immediately
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

            if (IsMapExtPresent) return; // Stop here if MapExt is found.

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
            return AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == "MapExtPDX");
        }

        /// <summary>
        /// Called by MapSizeDetector when a Vanilla map (<= 14km) is detected.
        /// </summary>
        public void ActivateEconomyFix()
        {
            if (IsMapExtPresent) return;
            if (IsActive) return; // Already active

            Info("Activating Economy Fixes...");
            IsActive = true;
            IsVanillaMap = true; // Confirmed
            
            // Enable our custom systems and disable vanilla ones
            SystemRegistrar.EnableEconomySystems();
            
            // Apply Job Patches (if any)
            JobPatchHelper.Apply(_harmony, JobPatchDefinitions.GetEcoSystemTargets());

            // Apply manual Harmony Patches for System Replacements
            _harmony.CreateClassProcessor(typeof(PathfindSetupSystem_FindTargets_Patch)).Patch();
            _harmony.CreateClassProcessor(typeof(LandValueSystemMod.Patches)).Patch();
            _harmony.CreateClassProcessor(typeof(ServiceCoverageSystem_SetupPathfindMethods_Patch)).Patch();

            // Apply NoDogs and NoThroughTraffic saved settings
            Settings.UpdateNoDogsSystemStates();
            Settings.UpdateNoThroughTrafficSystemStates();

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
             if (!IsActive) return;

             Info("Deactivating Economy Fixes (Large Map Detected)...");
             IsActive = false;
             IsVanillaMap = false;

             // Revert or Disable our systems? 
             // Ideally we should Unpatch, but Harmony Unpatching at runtime can be risky or complex.
             // For now, we will just Disable our systems and Re-enable Vanilla ones.
             SystemRegistrar.DisableEconomySystems();
             
             // Note: Transpilers (JobPatches) are hard to revert at runtime without a restart usually,
             // but since we only patch on Load, we might be stuck with them if we switch maps without restarting.
             // However, our Job Patches are designed to be replacements. 
             // If we are on a Large Map, we SHOULD NOT run this mod at all.
             // If the user switches from Vanilla -> Large Map in one session:
             // The MapSizeDetector run at 'FinalizeTerrainData' which happens during map load.
             
             Settings.UpdateStatus();
        }
    }
}
