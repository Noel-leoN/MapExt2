using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Widgets;
using Unity.Entities;
using EconomyEX.Helpers;

namespace EconomyEX
{
    /// <summary>编辑器碰撞检测跳过选项</summary>
    public enum EditorCollisionSkipMode
    {
        Off,         // 不干预
        TreesOnly,   // 仅跳过树木
        AllObjects   // 跳过所有对象
    }
}

namespace EconomyEX.Settings
{
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    [SettingsUITabOrder(kSectionStatus, kSectionGeneral, kSectionPerfTool, kDebugTab)]
    [SettingsUIGroupOrder(kSectionStatus, kSectionGeneral, kSectionPathfinding, kSectionBehavior,
        kNoDogsGroup, kNoTrafficGroup, kEditorToolGroup, kPopDiagGroup)]
    [SettingsUIShowGroupName(kSectionGeneral, kSectionPathfinding, kSectionBehavior,
        kNoDogsGroup, kNoTrafficGroup, kEditorToolGroup, kPopDiagGroup)]
    public class ModSettings : ModSetting
    {
        public const string kSectionGeneral = "General";
        public const string kSectionStatus = "Status";

        // -- Perf. Tools Tab --
        public const string kSectionPerfTool = "PerformanceTool";
        public const string kNoDogsGroup = "NoDogs";
        public const string kNoTrafficGroup = "NoTraffic";
        public const string kEditorToolGroup = "EditorTool";

        // -- Debug Tab --
        public const string kDebugTab = "Debug";
        public const string kPopDiagGroup = "PopDiag";

        public ModSettings(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(kSectionStatus, kSectionStatus)]
        public string StatusInfo { get; private set; } = "Initializing...";

        [SettingsUISection(kSectionStatus, kSectionStatus)]
        public string ConflictWarning { get; set; } = "None"; // Updated by ConflictMonitoringSystem

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        public bool EnableEconomyFix { get; set; } = true;

        public bool IsEconomyFixDisabled => !EnableEconomyFix;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableDemandEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableJobSearchEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableHouseholdPropertyEcoSystem { get; set; } = true;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableResourceBuyerEcoSystem { get; set; } = false;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        public bool EnableResidentAIEcoSystem { get; set; } = false;

        [SettingsUISection(kSectionGeneral, kSectionGeneral)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetEcoSystemToggles
        {
            set
            {
                EnableDemandEcoSystem = true;
                EnableJobSearchEcoSystem = true;
                EnableHouseholdPropertyEcoSystem = true;
                EnableResourceBuyerEcoSystem = false;
                EnableResidentAIEcoSystem = false;
            }
        }

        public const string kSectionPathfinding = "Pathfinding";

        // ==========================================
        // 寻路优化参数（与 MapExtPDX 属性名一致，便于移植）
        // ==========================================
        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float ShoppingMaxCost { get; set; } = 8000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float CompanyShoppingMaxCost { get; set; } = 200000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float LeisureMaxCost { get; set; } = 12000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 17000f, step = 500f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float EmergencyMaxCost { get; set; } = 6000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindJobMaxCost { get; set; } = 200000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindHomeMaxCost { get; set; } = 200000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolElementaryMaxCost { get; set; } = 10000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolHighSchoolMaxCost { get; set; } = 17000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolCollegeMaxCost { get; set; } = 50000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolUniversityMaxCost { get; set; } = 100000f;

        [SettingsUISection(kSectionGeneral, kSectionPathfinding)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetPathfinding
        {
            set
            {
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
            }
        }

        // ==========================================
        // 经济行为参数
        // ==========================================
        public const string kSectionBehavior = "Behavior";

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 200, max = 5000, step = 100, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int JobSeekerCap { get; set; } = 1000;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 500, max = 10000, step = 500, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int PathfindRequestCap { get; set; } = 4000;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 1, max = 20, step = 1, scalarMultiplier = 10000, unit = Game.UI.Unit.kInteger)]
        public float ShoppingTrafficReduction { get; set; } = 0.0004f;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 1.0f, max = 8.0f, step = 0.5f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float HouseholdResourceDemandMultiplier { get; set; } = 3.5f;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 32, max = 512, step = 16, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int HomeSeekerCap { get; set; } = 128;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUISlider(min = 128, max = 5120, step = 128, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int HomelessSeekerCap { get; set; } = 1280;

        [SettingsUISection(kSectionGeneral, kSectionBehavior)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ResetEcoBehavior
        {
            set
            {
                JobSeekerCap = 1000;
                PathfindRequestCap = 4000;
                ShoppingTrafficReduction = 0.0004f;
                HouseholdResourceDemandMultiplier = 3.5f;
                HomeSeekerCap = 128;
                HomelessSeekerCap = 1280;
            }
        }

        // === NoDogs 2.0 ===
        #region NoDogs

        private bool m_NoDogsOnStreet = false;
        private bool m_NoDogsGeneration = false;
        private bool m_NoDogsPurge = false;

        [SettingsUISection(kSectionPerfTool, kNoDogsGroup)]
        public bool NoDogsOnStreet
        {
            get => m_NoDogsOnStreet;
            set => m_NoDogsOnStreet = value;
        }

        [SettingsUISection(kSectionPerfTool, kNoDogsGroup)]
        public bool NoDogsGeneration
        {
            get => m_NoDogsGeneration;
            set => m_NoDogsGeneration = value;
        }

        [SettingsUISection(kSectionPerfTool, kNoDogsGroup)]
        public bool NoDogsPurge
        {
            get => m_NoDogsPurge;
            set => m_NoDogsPurge = value;
        }

        [SettingsUISection(kSectionPerfTool, kNoDogsGroup)]
        [SettingsUIButton]
        [SettingsUIConfirmation]
        public bool ApplyNoDogs
        {
            set
            {
                UpdateNoDogsSystemStates();
            }
        }

        public void UpdateNoDogsSystemStates()
        {
            ModLog.Info("Settings", $"NoDogs 2.0: OnStreet={m_NoDogsOnStreet}, Generation={m_NoDogsGeneration}, Purge={m_NoDogsPurge}");

            // 禁止外出：关闭 HouseholdPetSpawnSystem
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.HouseholdPetSpawnSystem>()
                .Enabled = !m_NoDogsOnStreet;

            // 阻止生成 / 清除存量：通知 ECS 系统
            var patchSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EconomyEX.Systems.P1_NoDogsPatchSystem>();
            if (patchSystem != null)
            {
                patchSystem.ApplySettings(m_NoDogsGeneration, m_NoDogsPurge);
            }

            // Purge 是一次性操作，执行后自动取消勾选
            if (m_NoDogsPurge)
            {
                m_NoDogsPurge = false;
            }

            ModLog.Patch("Settings", "NoDogs 2.0 补丁已应用");
        }

        public int CurrentPetCount { get; set; } = 0;

        [SettingsUISection(kSectionPerfTool, kNoDogsGroup)]
        public string DislayPetCount => $"Logical Pets Count: {CurrentPetCount}";

        [SettingsUISection(kSectionPerfTool, kNoDogsGroup)]
        [SettingsUIButton]
        public bool RefreshPetCount
        {
            set
            {
                var patchSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EconomyEX.Systems.P1_NoDogsPatchSystem>();
                if (patchSystem != null)
                {
                    CurrentPetCount = patchSystem.CountPets();
                }
            }
        }

        #endregion

        // === No Through-Traffic ===
        #region NoThroughTraffic

        private bool m_NoThroughTrafficSystem = false;

        [SettingsUISection(kSectionPerfTool, kNoTrafficGroup)]
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
            ModLog.Info("Settings",
                $"TrafficSpawnerAISystem Enabled={!m_NoThroughTrafficSystem}");
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.TrafficSpawnerAISystem>()
                .Enabled = !m_NoThroughTrafficSystem;

            ModLog.Patch("Settings", "NoThroughTraffic 补丁已应用");
        }

        #endregion

        // === Editor Collision ===
        #region EditorCollisionMode

        [SettingsUISection(kSectionPerfTool, kEditorToolGroup)]
        [SettingsUIDropdown(typeof(ModSettings), nameof(GetEditorCollisionSkipItems))]
        public EditorCollisionSkipMode EditorCollisionSkip { get; set; }

        public DropdownItem<int>[] GetEditorCollisionSkipItems()
        {
            return new DropdownItem<int>[]
            {
                new() { value = (int)EditorCollisionSkipMode.Off, displayName = "Off (Vanilla)" },
                new() { value = (int)EditorCollisionSkipMode.TreesOnly, displayName = "Trees Only" },
                new() { value = (int)EditorCollisionSkipMode.AllObjects, displayName = "All Objects" }
            };
        }

        #endregion

        // === 人口诊断 ===
        #region Population Diagnostics

        /// <summary>诊断数据缓存，由 RefreshPopDiag 按钮更新</summary>
        public string PopDiagData { get; set; } = "Click Refresh to run diagnostics.";

        /// <summary>诊断报告显示</summary>
        [SettingsUISection(kDebugTab, kPopDiagGroup)]
        public string PopDiagReport => PopDiagData;

        /// <summary>刷新人口诊断数据</summary>
        [SettingsUISection(kDebugTab, kPopDiagGroup)]
        [SettingsUIButton]
        public bool RefreshPopDiag
        {
            set
            {
                var system = Unity.Entities.World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<EconomyEX.Systems.PopulationDiagnosticSystem>();
                if (system != null)
                {
                    PopDiagData = system.RunDiagnostics();
                }
                else
                {
                    PopDiagData = "PopulationDiagnosticSystem not found.";
                }
            }
        }

        #endregion

        public void UpdateStatus()
        {
            if (Mod.IsMapExtPresent)
            {
                StatusInfo = "DISABLED: 'MapExt' mod detected. Please use MapExt's internal economy module.";
            }
            else if (Mod.IsActive)
            {
                StatusInfo = "ACTIVE: Economy Fixes Enabled (Vanilla Map Detected).";
            }
            else if (Helpers.MapSizeDetector.HasCheckedMapSize && !Mod.IsVanillaMap)
            {
                 StatusInfo = "DISABLED: Large Map Detected. This mod is for Vanilla maps only.";
            }
            else
            {
                StatusInfo = "IDLE: Waiting for map load...";
            }
        }

        /// <summary>系统状态报告概要，由 ConflictMonitoringSystem 更新</summary>
        [SettingsUISection(kSectionStatus, kSectionStatus)]
        public string SystemStatusReport { get; set; } = "Waiting...";

        /// <summary>手动刷新状态按钮，触发 ConflictMonitoringSystem 即时检查</summary>
        [SettingsUISection(kSectionStatus, kSectionStatus)]
        [SettingsUIButton]
        public bool RefreshStatus
        {
            set
            {
                UpdateStatus();
                // 触发 ConflictMonitoringSystem 的即时检查
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                var monitor = world?.GetExistingSystemManaged<EconomyEX.Helpers.ConflictMonitoringSystem>();
                if (monitor != null)
                {
                    monitor.ForceCheck();
                }
            }
        }

        public override void SetDefaults()
        {
            EnableEconomyFix = true;
            EnableDemandEcoSystem = true;
            EnableJobSearchEcoSystem = true;
            EnableHouseholdPropertyEcoSystem = true;
            EnableResourceBuyerEcoSystem = false;
            EnableResidentAIEcoSystem = false;
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
            JobSeekerCap = 1000;
            PathfindRequestCap = 4000;
            ShoppingTrafficReduction = 0.0004f;
            HouseholdResourceDemandMultiplier = 3.5f;
            HomeSeekerCap = 128;
            HomelessSeekerCap = 1280;
        }
    }
}
