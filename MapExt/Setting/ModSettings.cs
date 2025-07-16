// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using Game.UI.Widgets;
using MapExtPDX.MapExt.MapSizePatchSet;
using System.Collections.Generic;
using Unity.Entities;

// 保持与Mod.cs同一命名空间
namespace MapExtPDX
{
    // Enum for our patch modes
    public enum PatchModeSetting
    {
        ModeA, // CoreValue = 4  // 57km (default)
        ModeB, // CoreValue = 2  // 28km
        ModeC, // CoreValue = 8  // 114km
        ModeD, // CoreValue = 16 // 229km
        None   // Vanilla = 1
    }

    [FileLocation(nameof(MapExtPDX))]
    [SettingsUITabOrder(kMapSizeModeTab, kPerformanceToolTab, kMiscTab, kDebugTab)]
    [SettingsUIGroupOrder(kMainModeGroup, kResetGroup, kInfoGroup, kPerformanceToolGroup, kMiscGroup, kAirwayGroup, kDebugGroup)]
    [SettingsUIShowGroupName(kMainModeGroup, kResetGroup, kInfoGroup, kPerformanceToolGroup, kMiscGroup, kAirwayGroup, kDebugGroup)]

    public class ModSettings : Game.Modding.ModSetting
    {
        public const string kMapSizeModeTab = "MapSize Mode";
        public const string kPerformanceToolTab = "PerformanceTool";
        public const string kMiscTab = "Misc";
        public const string kDebugTab = "Debug";
        public const string kMainModeGroup = "MainMode";
        public const string kApplyModeGroup = "ApplyMode";
        public const string kInfoGroup = "GameInfo";
        public const string kResetGroup = "Reset";
        public const string kPerformanceToolGroup = "PerformanceTool";
        public const string kMiscGroup = "Misc";
        public const string kDebugGroup = "Debug";
        public const string kAirwayGroup = "AirwayRegenerate";
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
        [SettingsUIDropdown(typeof(ModSettings), nameof(GetPatchModeDropdownItems))] // Use this for enums if want custom display names
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

                Mod.Info($"ApplyPatchChanges button clicked. New selected mode from UI: {this.PatchModeChoice}");
                // 传递用户在UI中选择的 PatchModeChoice
                Mod.Instance?.OnPatchModeChanged(this.PatchModeChoice);
                Mod.Info($"Settings changes applied via button for mode: {this.PatchModeChoice}");

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
                case PatchModeSetting.ModeA: return "ModeA 57km (4x4)";
                case PatchModeSetting.ModeB: return "ModeB 28km (2x2)";
                case PatchModeSetting.ModeC: return "ModeC 114km (8x8) (not Recommand)";
                case PatchModeSetting.ModeD: return "ModeD 229km (16x16) (not Recommand)";
                case PatchModeSetting.None: return "None 14km (Vanilla)";
                default: return mode.ToString();
            }
        }


        /// <summary>
        /// 全局并行选项
        /// </summary>
        /// 
        // 选项可用性
        public bool IsPatchUnAvailable => true;

        // 实时应用并保存
        public override void Apply()
        {
            base.Apply(); // 这会保存设置到文件
        }

        // 设置字段初始化器默认值
        private bool m_NoDogsSystem = false;
        private bool m_NoThroughTrafficSystem = false;
        private bool m_LandValueRemakeSystem = false;

        [SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        //[SettingsUIDisableByConditionAttribute(typeof(ModSettings), nameof(IsPatchUnAvailable))]
        public bool NoDogs
        {
            get => m_NoDogsSystem;
            set
            {
                // 检查值是否真的改变了，避免不必要的重复调用
                if (m_NoDogsSystem != value)
                {
                    m_NoDogsSystem = value;

                    // 关键：在值被改变的瞬间，立即调用我们的更新逻辑！
                    UpdateNoDogsSystemStates();
                }
            }
        }
        public void UpdateNoDogsSystemStates()
        {
            Mod.Info($"Setting 'HouseholdPetSpawnSystem' is now: {nameof(Game.Simulation.HouseholdPetSpawnSystem)}. Updating system enabled state.");

            // 获取系统实例并根据设置更新其 .Enabled 属性
            // 当 DisableMyOptionalSystem 为 true 时, .Enabled 应为 false
            // 当 DisableMyOptionalSystem 为 false 时, .Enabled 应为 true
            // 所以逻辑是 .Enabled = !DisableMyOptionalSystem
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.HouseholdPetSpawnSystem>().Enabled = !m_NoDogsSystem;


            // 如果多个系统需要控制，可以继续在这里添加
            // World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<AnotherSystem>().Enabled = !this.SomeOtherSetting;
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
            Mod.Info($"Setting 'TrafficSpawnerAISystem' is now: {nameof(Game.Simulation.TrafficSpawnerAISystem)}. Updating system enabled state.");
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.TrafficSpawnerAISystem>().Enabled = !m_NoThroughTrafficSystem;
        }



        //[SettingsUISection(kPerformanceToolTab, kPerformanceToolGroup)]
        //[SettingsUIDisableByConditionAttribute(typeof(ModSettings), nameof(IsPatchUnAvailable))]
        //public bool NoRandomTraffic { get; set; }

        [SettingsUISection(kMiscTab, kAirwayGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsInMainMenu))]
        public bool ApplyAirwayRegenerate
        {
            set
            {
                AirwaySystem_OnUpdate_Patch.RequestManualRegeneration();
                Mod.Info($"ApplyAirwayRegenerate button clicked. SessionLock change to {AirwaySystem_OnUpdate_Patch.s_HasRunThisSession}");
            }
        }

        [SettingsUISection(kMiscTab, kMiscGroup)]
        [SettingsUIDisableByConditionAttribute(typeof(ModSettings), nameof(IsPatchUnAvailable))]  // 暂时禁用
        public bool LandValueRemake
        {
            get => m_LandValueRemakeSystem;
            set
            {
                if (m_LandValueRemakeSystem != value)
                {
                    m_LandValueRemakeSystem = value;

                    UpdateLandValueRemakeSystemStates();
                }
            }
        }

        public void UpdateLandValueRemakeSystemStates()
        {
            //Mod.Info($"Setting 'LandValueRemakeSystem' is now: {nameof(Game.Simulation.LandValueSystem)}. Updating system enabled state.");
            //World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = !m_LandValueRemakeSystem;
        }

        // private bool m_LoadGameValidatorPatch;
        // 开关LoadGame验证系统
        [SettingsUISection(kDebugTab, kDebugGroup)]
        public bool DisableLoadGameValidation { get; set; } = false;

    }
}



