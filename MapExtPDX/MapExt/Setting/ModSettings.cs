// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Game.UI.Widgets;
using Unity.Entities;
using MapExtPDX.MapExt.Core;

// 保持与Mod.cs同一命名空间
namespace MapExtPDX
{
    // Enum for our patch modes
    public enum PatchModeSetting
    {
        ModeA, // CoreValue = 4  // 57km (default)
        ModeB, // CoreValue = 2  // 28km
        ModeC, // CoreValue = 8  // 114km

        // ModeD, // CoreValue = 16 // 229km
        None // Vanilla = 1
    }

    public enum NoDogsMode
    {
        Vanilla,
        DisableOnStreet,
        PreventGeneration,
        PurgeExisting
    }

    //[FileLocation(nameof(MapExtPDX))]
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    [SettingsUITabOrder(kMapSizeModeTab, kMiscTab, kPerformanceToolTab, kDebugTab)]
    [SettingsUIGroupOrder(kMainModeGroup, kResetGroup, kInfoGroup, kEcoGroup, kNoteGroup, 
        kMiscGroup, kEconomyTweakGroup, kPerformanceToolGroup, kDebugGroup)]
    [SettingsUIShowGroupName(kMainModeGroup, kResetGroup, kEcoGroup, 
        kMiscGroup, kEconomyTweakGroup, kPerformanceToolGroup, kDebugGroup)]
    public class ModSettings : ModSetting
    {
        public const string kMapSizeModeTab = "▍MapSize Mode";
        public const string kPerformanceToolTab = "▍PerformanceTool";
        public const string kMiscTab = "▍Misc";
        public const string kDebugTab = "▍Debug";
        public const string kMainModeGroup = "▍MainMode";
        public const string kApplyModeGroup = "▍ApplyMode";
        public const string kInfoGroup = "▍GameInfo";
        public const string kEcoGroup = "▍Economy Logic & Perf.";
        public const string kNoteGroup = "▍Warning!";
        public const string kResetGroup = "▍Reset";
        public const string kPerformanceToolGroup = "▍PerformanceTool";
        public const string kMiscGroup = "▍Misc";
        public const string kEconomyTweakGroup = "▍EconomyTweak";

        public const string kDebugGroup = "▍Debug";
        //public const string kAirwayGroup = "AirwayRegenerate";
        //public const string kPatchSettingsGroup = "PatchSettings"; // New group for our patch controls

        // Original group constants (can be removed if not used)
        // public const string kButtonGroup = "Button";
        // public const string kToggleGroup = "Toggle";
        // public const string kSliderGroup = "Slider";
        // public const string kDropdownGroup = "Dropdown";
        // public const string kKeybindingGroup = "KeyBinding";

        public string DisplayedMapSize { get; set; } = "N/A"; // 用于显示 playableArea
        public string DisplayedTerrainSystemValue { get; set; } = "N/A"; // 用于显示 TargetSystemA 的值
        public string DisplayedWaterSystemValue { get; set; } = "N/A"; // 用于显示 TargetSystemB 的值
        public string DisplayedCellMapSystemValue { get; set; } = "N/A"; // 用于显示泛型系统的值
        public string DetectedSaveCoreValue { get; set; } = "N/A"; // 用于显示从存档检测到的CoreValue

        // 构造函数
        public ModSettings(IMod mod) : base(mod)
        {
            // Set default values for any new settings if they are not already persisted
            SetDefaults();
        }

        // --- 控制设置页面的可见性 ---
        //  Returns true (hide) when NOT in the main menu.
        public bool IsNotInMainMenu => GameManager.instance.gameMode != GameMode.MainMenu;

        public bool IsInMainMenu => GameManager.instance.gameMode == GameMode.MainMenu;

        // --- PATCH SETTINGS ---
        [SettingsUISection(kMapSizeModeTab, kMainModeGroup)]
        [SettingsUIDropdown(typeof(ModSettings),
            nameof(GetPatchModeDropdownItems))] // Use this for enums if want custom display names
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public PatchModeSetting PatchModeChoice { get; set; } // = PatchModeSetting.ModeA; // Default to ModeA

        /// <summary>
        /// 核心关键功能，ModSetting实例中调用执行
        /// </summary>
        [SettingsUISection(kMapSizeModeTab, kApplyModeGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool ApplyPatchChanges
        {
            set
            {
                //Mod.Info($"ApplyPatchChanges button clicked. Selected mode: {PatchModeChoice}");
                // Tell the Mod to re-apply patches based on the current PatchModeChoice
                // The PatchModeChoice property is already updated by the UI at this point.
                // 使用静态实例来调用方法
                // 这样是类型安全的，编译器知道 Mod.Instance 的类型是 Mod
                // ?. 是空值条件运算符，确保如果Instance为null时不会出错
                //Mod.Instance?.OnPatchModeChanged(PatchManager.PatchModeChoice);
                //Mod.Info($"Settings changes applied via button.");

                Mod.Info($"ApplyPatchChanges button clicked. New selected mode from UI: {PatchModeChoice}");
                // 传递用户在UI中选择的 PatchModeChoice
                Mod.Instance?.OnPatchModeChanged(PatchModeChoice);
                Mod.Info($"Settings changes applied via button for mode: {PatchModeChoice}");
            }
        }

        // 在游戏内显示状态信息
        // 已读取存档的地图大小值
        [SettingsUISection(kMapSizeModeTab, kInfoGroup)]
        public string ModSettingCoreValue => (PatchManager.CurrentCoreValue * 14336).ToString();

        // 显示备份警告
        //[SettingsUISection(kMapSizeModeTab, kInfoGroup)]
        //public string WarningInfo => "Caution";

        // [SettingsUISection(kMapSizeModeTab, kInfoGroup)]
        // public string LoadedSaveCoreValue =>
        // PatchManager.LoadedSaveCoreValue.HasValue
        // ? $"Loaded Save MapSize: {PatchManager.LoadedSaveCoreValue.Value * 14336}m"
        // : "No game loaded.";


        // [SettingsUISection(kMapSizeModeTab, kInfoGroup)]
        // [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsInMainMenu))]
        // public string IsModSettingCoreValueMatch => PatchManager.CurrentCoreValue == PatchManager.LoadedSaveCoreValue
        //  ? $"Loaded Save and MapSize Mode Setting match up" : "Loaded Save and Mode Setting is conflicting!";

        public override void SetDefaults()
        {
            // 设置默认的补丁模式
            PatchModeChoice = PatchModeSetting.ModeA;
            ShoppingMaxCost = 8000f;
            CompanyShoppingMaxCost = 200000f;
            LeisureMaxCost = 12000f;
            EmergencyMaxCost = 6000f;
            FindJobMaxCost = 200000f;
            FindHomeMaxCost = 200000f;
            FindSchoolElementaryMaxCost = 10000f;
            FindSchoolHighSchoolMaxCost = 17000f;
            FindSchoolCollegeMaxCost = 50000f;
            FindSchoolUniversityMaxCost = 100000f;
            
            isEnableEconomyFix = true;
            EnableDemandEcoSystem = true;
            EnableJobSearchEcoSystem = true;
            EnableHouseholdPropertyEcoSystem = true;
            EnableResourceBuyerEcoSystem = true;
            EnableResidentAIEcoSystem = true;
        }

        // Helper for the dropdown (optional, direct enum use is fine too but this gives more control)
        public DropdownItem<int>[] GetPatchModeDropdownItems()
        {
            var items = new List<DropdownItem<int>>();
            foreach (PatchModeSetting mode in System.Enum.GetValues(typeof(PatchModeSetting)))
            {
                items.Add(new DropdownItem<int>
                {
                    value = (int)mode,
                    displayName = GetPatchModeDisplayName(mode)
                });
            }

            return items.ToArray();
        }

        // Example for custom display names, can be replaced by localization
        private string GetPatchModeDisplayName(PatchModeSetting mode)
        {
            switch (mode)
            {
                case PatchModeSetting.ModeA: return "• ModeA 57km (4x4)";
                case PatchModeSetting.ModeB: return "• ModeB 28km (2x2)";
                case PatchModeSetting.ModeC: return "• ModeC 114km (8x8) (Not Recommended)";
                // case PatchModeSetting.ModeD: return "🟣 ModeD 229km (16x16) (Test Only!)";
                case PatchModeSetting.None: return "• None 14km (Vanilla)";
                default: return mode.ToString();
            }
        }

        // === 经济系统补丁 ===
        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool isEnableEconomyFix { get; set; } = true;

        public bool IsEconomyFixDisabled => !isEnableEconomyFix;

        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableDemandEcoSystem { get; set; } = true;

        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableJobSearchEcoSystem { get; set; } = true;

        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableHouseholdPropertyEcoSystem { get; set; } = true;

        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableResourceBuyerEcoSystem { get; set; } = true;

        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableResidentAIEcoSystem { get; set; } = true;

        //[SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        //[SettingsUIButton]
        //[SettingsUIConfirmation]
        //[SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        //public bool ApplyEcoPatchChanges
        //{
        //    set
        //    {
        //        //Mod.Info($"ApplyPatchChanges button clicked. Selected mode: {PatchModeChoice}");
        //        // Tell the Mod to re-apply patches based on the current PatchModeChoice
        //        // The PatchModeChoice property is already updated by the UI at this point.
        //        // 使用静态实例来调用方法
        //        // 这样是类型安全的，编译器知道 Mod.Instance 的类型是 Mod
        //        // ?. 是空值条件运算符，确保如果Instance为null时不会出错
        //        //Mod.Instance?.OnPatchModeChanged(PatchManager.PatchModeChoice);
        //        //Mod.Info($"Settings changes applied via button.");

        //        Mod.Info($"ApplyEcoPatchChanges button clicked from UI: {isEnableEconomyFix}");
        //        // 传递用户在UI中选择的 EcoPatchMode
        //        Mod.Instance?.OnEcoPatchChanged(isEnableEconomyFix);
        //        Mod.Info($"Eco Settings changes applied via button for mode: {isEnableEconomyFix}");

        //    }
        //}

        [SettingsUISection(kMapSizeModeTab, kNoteGroup)]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public string ModeChangeWarningMessage => "";

        /// <summary>
        /// 全局并行选项
        /// </summary>
        /// 
        // 选项可用性
        public bool IsPatchUnAvailable => true;

        // 设置字段初始化器默认值
        private NoDogsMode m_NoDogsSystem = NoDogsMode.Vanilla;
        private bool m_NoThroughTrafficSystem = false;
        // private bool m_LandValueRemakeSystem = false;

        [SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        [SettingsUIDropdown(typeof(ModSettings), nameof(GetNoDogsModeDropdownItems))]

        public NoDogsMode NoDogs
        {
            get => m_NoDogsSystem;
            set
            {
                if (m_NoDogsSystem != value)
                {
                    m_NoDogsSystem = value;
                    UpdateNoDogsSystemStates();
                }
            }
        }

        public void UpdateNoDogsSystemStates()
        {
            Mod.Info($"Setting NoDogs is now: {m_NoDogsSystem}. Updating system enabled state.");

            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.HouseholdPetSpawnSystem>()
                .Enabled = (m_NoDogsSystem == NoDogsMode.Vanilla);

            // Trigger ECS system to perform necessary generation blocking or purging
            var patchSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<MapExtPDX.ModeA.P1_NoDogsPatchSystem>();
            if (patchSystem != null)
            {
                patchSystem.ModeChanged(m_NoDogsSystem);
            }

            Mod.Info($"NoDogs补丁已应用.(全局并行) Mode: {m_NoDogsSystem}");
        }

        public DropdownItem<int>[] GetNoDogsModeDropdownItems()
        {
            var items = new List<DropdownItem<int>>();
            foreach (NoDogsMode mode in System.Enum.GetValues(typeof(NoDogsMode)))
            {
                items.Add(new DropdownItem<int>
                {
                    value = (int)mode,
                    displayName = GetNoDogsModeDisplayName(mode)
                });
            }
            return items.ToArray();
        }

        private string GetNoDogsModeDisplayName(NoDogsMode mode)
        {
            switch (mode)
            {
                case NoDogsMode.Vanilla: return "1. Vanilla (原版生生不息)";
                case NoDogsMode.DisableOnStreet: return "2. Disable OnStreet (禁止外出)";
                case NoDogsMode.PreventGeneration: return "3. Prevent Gen (阻止新生成)";
                case NoDogsMode.PurgeExisting: return "4. Purge All (阻止生成并清除存量 !!! 警告由于游戏机制：存量清除后，家庭不会再买狗。若想有新狗只有新搬入的家庭自带。)";
                default: return mode.ToString();
            }
        }

        public int CurrentPetCount { get; set; } = 0;

        [SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        public string DislayPetCount => $"Logical Pets Count: {CurrentPetCount}";

        [SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        [SettingsUIButton]
        public bool RefreshPetCount
        {
            set
            {
                var patchSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<MapExtPDX.ModeA.P1_NoDogsPatchSystem>();
                if (patchSystem != null)
                {
                    CurrentPetCount = patchSystem.CountPets();
                }
            }
        }


        [SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        //[SettingsUIDisableByConditionAttribute(typeof(ModSettings), nameof(IsPatchUnAvailable))]
        public bool NoThroughTraffic
        {
            get => m_NoThroughTrafficSystem;
            set
            {
                if (m_NoThroughTrafficSystem != value)
                {
                    m_NoThroughTrafficSystem = value;

                    UpdateNoThroughTrafficSystemStates();
                }
            }
        }

        public void UpdateNoThroughTrafficSystemStates()
        {
            Mod.Info(
                $"Setting 'TrafficSpawnerAISystem' is now: {nameof(Game.Simulation.TrafficSpawnerAISystem)}. Updating system enabled state.");
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.TrafficSpawnerAISystem>()
                .Enabled = !m_NoThroughTrafficSystem;

            Mod.Info($"NoThroughTraffic补丁已应用.(全局并行)");
        }

        //[SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        //[SettingsUIDisableByConditionAttribute(typeof(ModSettings), nameof(IsPatchUnAvailable))]
        //public bool NoRandomTraffic { get; set; }

        //[SettingsUISection(kMiscTab, kMiscGroup)]
        //[SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsPatchUnAvailable))] // 暂时禁用
        //public bool LandValueRemake
        //{
        //    get => m_LandValueRemakeSystem;
        //    set
        //    {
        //        if (m_LandValueRemakeSystem != value)
        //        {
        //            m_LandValueRemakeSystem = value;

        //            UpdateLandValueRemakeSystemStates();
        //        }
        //    }
        //}

        public void UpdateLandValueRemakeSystemStates()
        {
            //Mod.Info($"Setting 'LandValueRemakeSystem' is now: {nameof(Game.Simulation.LandValueSystem)}. Updating system enabled state.");
            //World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = !m_LandValueRemakeSystem;
        }

        // ==========================================
        // 寻路优化参数
        // ==========================================
        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float ShoppingMaxCost { get; set; } = 8000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float CompanyShoppingMaxCost { get; set; } = 200000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float LeisureMaxCost { get; set; } = 12000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 17000f, step = 500f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float EmergencyMaxCost { get; set; } = 6000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindJobMaxCost { get; set; } = 200000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindHomeMaxCost { get; set; } = 200000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolElementaryMaxCost { get; set; } = 10000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolHighSchoolMaxCost { get; set; } = 17000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolCollegeMaxCost { get; set; } = 50000f;

        [SettingsUISection(kMiscTab, kEconomyTweakGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolUniversityMaxCost { get; set; } = 100000f;

        // private bool m_LoadGameValidatorPatch;
        // 开关LoadGame验证系统
        [SettingsUISection(kDebugTab, kDebugGroup)]
        public bool DisableLoadGameValidation { get; set; } = false;
    }
}