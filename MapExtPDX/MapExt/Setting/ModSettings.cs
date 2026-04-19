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

    /// <summary>地形分辨率选项</summary>
    public enum TerrainResolutionSetting
    {
        Vanilla_4096,   // 原版 4096×4096
        High_8192,      // 高清 8192×8192 (默认)
    }

    /// <summary>水纹理分辨率选项</summary>
    public enum WaterResolutionSetting
    {
        Vanilla_2048,   // 原版 2048×2048
        Medium_1024,    // 中等 1024×1024
        Low_512,        // 低 512×512 (推荐)
        Ultra_256,      // 极低 256×256 (最高性能)
    }

    /// <summary>水模拟质量选项</summary>
    public enum WaterSimQualitySetting
    {
        Vanilla_EveryFrame,     // 原版：每帧模拟，开启背景水
        Reduced_NoBackdrop,     // 降低：每帧模拟，关闭背景水
        Minimal_Every4Frames,   // 极简：每四帧模拟，关闭背景水、模糊和后处理
        Paused_NoFlow,          // 暂停：停止水流模拟
    }

    /// <summary>水纹理格式精度</summary>
    public enum WaterTextureFormatSetting
    {
        High_RGBA32F,   // 原版高精度 32-bit
        Low_RGBA16F,    // 降级低精度 16-bit (省一半显存)
    }

    //[FileLocation(nameof(MapExtPDX))]
    [FileLocation("ModsSettings/" + Mod.ModName + "/" + Mod.ModName)]
    [SettingsUITabOrder(kMapSizeModeTab, kMiscTab, kPerformanceToolTab, kDebugTab)]
    [SettingsUIGroupOrder(kMainModeGroup, kTerrainWaterOptGroup, kResetGroup, kInfoGroup, kEcoGroup, kNoteGroup,
        kEcoSystemEnableGroup, kPathfindingGroup, kEcoBehaviorGroup,
        kNoDogsGroup, kNoTrafficGroup, kDebugGroup)]
    [SettingsUIShowGroupName(kMainModeGroup, kTerrainWaterOptGroup, kResetGroup, kEcoGroup,
        kEcoSystemEnableGroup, kPathfindingGroup, kEcoBehaviorGroup,
        kNoDogsGroup, kNoTrafficGroup, kDebugGroup)]
    public class ModSettings : ModSetting
    {
        private const string Tag = "Settings";

        // === Tab 常量 ===
        public const string kMapSizeModeTab = "MapSize Mode";
        public const string kPerformanceToolTab = "PerformanceTool";
        public const string kMiscTab = "EconomyEX";
        public const string kDebugTab = "Debug";

        // === Group 常量 ===
        // -- 首页 Tab --
        public const string kMainModeGroup = "MainMode";
        public const string kApplyModeGroup = "ApplyMode";
        public const string kTerrainWaterOptGroup = "TerrainWaterOpt";
        public const string kInfoGroup = "GameInfo";
        public const string kEcoGroup = "EconomyOverhaul";
        public const string kNoteGroup = "Warning";
        public const string kResetGroup = "Reset";

        // -- EconomyEX Tab --
        public const string kEcoSystemEnableGroup = "EcoSystemEnable";
        public const string kPathfindingGroup = "Pathfinding";
        public const string kEcoBehaviorGroup = "EcoBehavior";

        // -- Perf. Tools Tab --
        public const string kNoDogsGroup = "NoDogs";
        public const string kNoTrafficGroup = "NoTraffic";

        // -- Developer Tab --
        public const string kDebugGroup = "Debug";

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

        // === 地图尺寸模式 ===
        #region MapSize Mode

        [SettingsUISection(kMapSizeModeTab, kMainModeGroup)]
        [SettingsUIDropdown(typeof(ModSettings),
            nameof(GetPatchModeDropdownItems))] // Use this for enums if want custom display names
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public PatchModeSetting PatchModeChoice { get; set; } // = PatchModeSetting.ModeA; // Default to ModeA

        // 在游戏内显示状态信息 (已读取存档的地图大小值)
        [SettingsUISection(kMapSizeModeTab, kMainModeGroup)]
        public string ModSettingCoreValue => (PatchManager.CurrentCoreValue * 14336).ToString();

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
                ModLog.Info(Tag, $"ApplyPatchChanges 点击, 新选择的模式: {PatchModeChoice}");
                // 传递用户在UI中选择的 PatchModeChoice
                Mod.Instance?.OnPatchModeChanged(PatchModeChoice);
                ModLog.Ok(Tag, $"已通过按钮应用模式: {PatchModeChoice}");
            }
        }

        #endregion

        // === 地形-水体优化 (Beta) ===
        #region Terrain-Water Optimization

        /// <summary>
        /// 地形 StructuredBuffer 首帧预扩容。
        /// 根据地图倍率预分配更大的 GPU Buffer，避免运行时动态扩容卡顿。
        /// </summary>
        [SettingsUISection(kMapSizeModeTab, kTerrainWaterOptGroup)]
        public bool TerrainBufferPrealloc { get; set; } = true;

        /// <summary>
        /// 建筑裁剪降频。
        /// 相机平移时若无建筑/地形变化，跳过 CullBuildingLotsJob 全量裁剪，
        /// 复用上一帧缓存列表。
        /// </summary>
        [SettingsUISection(kMapSizeModeTab, kTerrainWaterOptGroup)]
        public bool TerrainCullThrottle { get; set; } = true;

        /// <summary>
        /// 远距级联降频更新。
        /// 远距地形级联每 4 帧更新一次，降低 GPU 开销。
        /// ⚠ 可能导致镜头移动时远景地形短暂错位。
        /// </summary>
        [SettingsUISection(kMapSizeModeTab, kTerrainWaterOptGroup)]
        public bool TerrainCascadeThrottle { get; set; } = false;

        // ==========================================
        // 分辨率设置 (部分已隐藏)
        // ==========================================
        // 地形分辨率: 8192 与水模拟级联不兼容，当前仅 4096 可用
        // 水纹理分辨率: Compute Shader 存在纹理尺寸隐式依赖，当前仅 2048 可用
        // 待自定义 Compute Shader (Phase 3) 实现后恢复 UI
        [SettingsUIHidden]
        public TerrainResolutionSetting TerrainResolution { get; set; }

        [SettingsUIHidden]
        public WaterResolutionSetting WaterResolution { get; set; }

        private WaterSimQualitySetting m_waterSimQuality = WaterSimQualitySetting.Vanilla_EveryFrame;

        [SettingsUISection(kMapSizeModeTab, kTerrainWaterOptGroup)]
        [SettingsUIDropdown(typeof(ModSettings), nameof(GetWaterSimQualityItems))]
        public WaterSimQualitySetting WaterSimQuality
        {
            get => m_waterSimQuality;
            set
            {
                if (m_waterSimQuality != value)
                {
                    m_waterSimQuality = value;
                    MapExt.Core.ResolutionManager.UpdateWaterSimQuality(value);
                }
            }
        }

        // 16-bit 格式已被禁用，因为损失精度会导致流水无法蔓延
        [SettingsUIHidden]
        public WaterTextureFormatSetting WaterTextureFormat { get; set; }

        // 分辨率选项隐藏后，VRAM 估算也无需显示
        [SettingsUIHidden]
        public string VRAMEstimate => $"Est. VRAM: {MapExt.Core.ResolutionManager.GetVRAMEstimate()}";

        #endregion

        // === 经济系统总开关 ===
        #region Economy Overhaul

        [SettingsUISection(kMapSizeModeTab, kEcoGroup)]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool isEnableEconomyFix { get; set; } = true;

        public bool IsEconomyFixDisabled => !isEnableEconomyFix;

        [SettingsUISection(kMapSizeModeTab, kNoteGroup)]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public string ModeChangeWarningMessage => "";

        #endregion

        /// <summary>
        /// 全局并行选项
        /// </summary>
        // 选项可用性
        public bool IsPatchUnAvailable => true;

        // === EconomyEX Tab - 经济子系统启用开关 ===
        #region Economy System Toggles

        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableDemandEcoSystem { get; set; } = true;

        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableJobSearchEcoSystem { get; set; } = true;

        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableHouseholdPropertyEcoSystem { get; set; } = true;

        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableResourceBuyerEcoSystem { get; set; } = false;

        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        [SettingsUIDisableByCondition(typeof(ModSettings), nameof(IsEconomyFixDisabled))]
        [SettingsUIHideByCondition(typeof(ModSettings), nameof(IsNotInMainMenu))]
        public bool EnableResidentAIEcoSystem { get; set; } = false;

        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
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

        #endregion

        // === EconomyEX Tab - 寻路优化参数 ===
        #region Pathfinding Parameters

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float ShoppingMaxCost { get; set; } = 8000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float CompanyShoppingMaxCost { get; set; } = 200000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float LeisureMaxCost { get; set; } = 12000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 17000f, step = 500f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float EmergencyMaxCost { get; set; } = 6000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindJobMaxCost { get; set; } = 200000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 17000f, max = 200000f, step = 1000f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindHomeMaxCost { get; set; } = 200000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolElementaryMaxCost { get; set; } = 10000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolHighSchoolMaxCost { get; set; } = 17000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolCollegeMaxCost { get; set; } = 50000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
        [SettingsUISlider(min = 1000f, max = 200000f, step = 1000f, scalarMultiplier = 1f, unit = Game.UI.Unit.kFloatSingleFraction)]
        public float FindSchoolUniversityMaxCost { get; set; } = 100000f;

        [SettingsUISection(kMiscTab, kPathfindingGroup)]
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

        #endregion

        // === EconomyEX Tab - 经济行为与吞吐量参数 ===
        #region Economy Behavior & Throughput

        /// <summary>
        /// 每次 B1 系统更新最多创建的求职者实体数量
        /// </summary>
        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
        [SettingsUISlider(min = 200, max = 5000, step = 100, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int JobSeekerCap { get; set; } = 1000;

        /// <summary>
        /// 每次 B2 系统更新最多处理的寻路请求数量
        /// </summary>
        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
        [SettingsUISlider(min = 500, max = 10000, step = 500, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int PathfindRequestCap { get; set; } = 4000;

        /// <summary>
        /// 购物概率人口压制系数。值越大，人口越多时购物概率衰减越快。
        /// 原版硬编码 0.0004f。
        /// </summary>
        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
        [SettingsUISlider(min = 1, max = 20, step = 1, scalarMultiplier = 10000, unit = Game.UI.Unit.kInteger)]
        public float ShoppingTrafficReduction { get; set; } = 0.0004f;

        /// <summary>
        /// 家庭购物资源需求倍率。频率降低后需提高单次购买量来补偿。
        /// 原版默认 1.0，Mod 默认 3.5。
        /// </summary>
        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
        [SettingsUISlider(min = 1.0f, max = 8.0f, step = 0.5f, scalarMultiplier = 1f,
            unit = Game.UI.Unit.kFloatSingleFraction)]
        public float HouseholdResourceDemandMultiplier { get; set; } = 3.5f;

        /// <summary>
        /// 每帧处理的常规搬家家庭数上限（已有住房但想搬家的家庭）。
        /// 原版硬编码 128。
        /// </summary>
        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
        [SettingsUISlider(min = 32, max = 512, step = 16, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int HomeSeekerCap { get; set; } = 128;

        /// <summary>
        /// 每帧处理的流浪家庭找房数上限（无家可归家庭）。
        /// 原版硬编码 1280。
        /// </summary>
        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
        [SettingsUISlider(min = 128, max = 5120, step = 128, scalarMultiplier = 1, unit = Game.UI.Unit.kInteger)]
        public int HomelessSeekerCap { get; set; } = 1280;

        [SettingsUISection(kMiscTab, kEcoBehaviorGroup)]
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

        #endregion

        // === NoDogs 2.0 ===
        #region NoDogs

        private bool m_NoDogsOnStreet = false;
        private bool m_NoDogsGeneration = false;
        private bool m_NoDogsPurge = false;

        [SettingsUISection(kPerformanceToolTab, kNoDogsGroup)]
        public bool NoDogsOnStreet
        {
            get => m_NoDogsOnStreet;
            set => m_NoDogsOnStreet = value;
        }

        [SettingsUISection(kPerformanceToolTab, kNoDogsGroup)]
        public bool NoDogsGeneration
        {
            get => m_NoDogsGeneration;
            set => m_NoDogsGeneration = value;
        }

        [SettingsUISection(kPerformanceToolTab, kNoDogsGroup)]
        public bool NoDogsPurge
        {
            get => m_NoDogsPurge;
            set => m_NoDogsPurge = value;
        }

        [SettingsUISection(kPerformanceToolTab, kNoDogsGroup)]
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
            ModLog.Info(Tag, $"NoDogs 2.0: OnStreet={m_NoDogsOnStreet}, Generation={m_NoDogsGeneration}, Purge={m_NoDogsPurge}");

            // 禁止外出：关闭 HouseholdPetSpawnSystem
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.HouseholdPetSpawnSystem>()
                .Enabled = !m_NoDogsOnStreet;

            // 阻止生成 / 清除存量：通知 ECS 系统
            var patchSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<MapExtPDX.EcoShared.P1_NoDogsPatchSystem>();
            if (patchSystem != null)
            {
                patchSystem.ApplySettings(m_NoDogsGeneration, m_NoDogsPurge);
            }

            // Purge 是一次性操作，执行后自动取消勾选
            if (m_NoDogsPurge)
            {
                m_NoDogsPurge = false;
            }

            ModLog.Patch(Tag, "NoDogs 2.0 补丁已应用 (全局并行)");
        }

        public int CurrentPetCount { get; set; } = 0;

        [SettingsUISection(kPerformanceToolTab, kNoDogsGroup)]
        public string DislayPetCount => $"Logical Pets Count: {CurrentPetCount}";

        [SettingsUISection(kPerformanceToolTab, kNoDogsGroup)]
        [SettingsUIButton]
        public bool RefreshPetCount
        {
            set
            {
                var patchSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<MapExtPDX.EcoShared.P1_NoDogsPatchSystem>();
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

        [SettingsUISection(kPerformanceToolTab, kNoTrafficGroup)]
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
            ModLog.Info(Tag,
                $"TrafficSpawnerAISystem Enabled={!m_NoThroughTrafficSystem}");
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.TrafficSpawnerAISystem>()
                .Enabled = !m_NoThroughTrafficSystem;

            ModLog.Patch(Tag, "NoThroughTraffic 补丁已应用 (全局并行)");
        }

        #endregion

        public void UpdateLandValueRemakeSystemStates()
        {
        }

        // === Conflict Monitoring ===
        #region Conflict Monitoring

        /// <summary>冲突警告信息，由 ConflictMonitoringSystem 更新</summary>
        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        public string ConflictWarning { get; set; } = "None";

        /// <summary>系统状态报告概要</summary>
        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        public string SystemStatusReport { get; set; } = "Waiting...";

        /// <summary>手动刷新状态按钮，触发 ConflictMonitoringSystem 即时检查</summary>
        [SettingsUISection(kMiscTab, kEcoSystemEnableGroup)]
        [SettingsUIButton]
        public bool RefreshStatus
        {
            set
            {
                // 触发 ConflictMonitoringSystem 的即时检查
                var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
                var monitor = world?.GetExistingSystemManaged<MapExtPDX.EcoShared.ConflictMonitoringSystem>();
                if (monitor != null)
                {
                    monitor.ForceCheck();
                }
            }
        }

        #endregion

        // === Debug ===
        // 开关LoadGame验证系统
        [SettingsUISection(kDebugTab, kDebugGroup)]
        public bool DisableLoadGameValidation { get; set; } = false;

        // === Defaults ===
        public override void SetDefaults()
        {
            // 设置默认的补丁模式
            PatchModeChoice = PatchModeSetting.ModeA;
            // 分辨率设置
            // 地形 8192 暂时禁用 — 水模拟与 8192 级联不兼容 (见 docs/TerrainSystem/Water_Terrain_Decoupling_Research.md)
            TerrainResolution = TerrainResolutionSetting.Vanilla_4096;
            WaterResolution = WaterResolutionSetting.Vanilla_2048;
            WaterSimQuality = WaterSimQualitySetting.Vanilla_EveryFrame;
            WaterTextureFormat = WaterTextureFormatSetting.High_RGBA32F;

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
            
            isEnableEconomyFix = true;
            EnableDemandEcoSystem = true;
            EnableJobSearchEcoSystem = true;
            EnableHouseholdPropertyEcoSystem = true;
            EnableResourceBuyerEcoSystem = false;
            EnableResidentAIEcoSystem = false;

            // 地形优化
            TerrainBufferPrealloc = true;
            TerrainCascadeThrottle = false;  // 默认关闭：会导致远景级联与视口不同步→地形错位
            TerrainCullThrottle = true;       // 默认开启：跳过无变化帧的建筑裁剪Job
        }

        // === Dropdown Helpers ===
        #region Dropdown Helpers

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

        private string GetPatchModeDisplayName(PatchModeSetting mode)
        {
            switch (mode)
            {
                case PatchModeSetting.ModeA: return "• ModeA 57km (4x4)";
                case PatchModeSetting.ModeB: return "• ModeB 28km (2x2)";
                case PatchModeSetting.ModeC: return "• ModeC 114km (8x8) (Not Recommended)";
                case PatchModeSetting.None: return "• None 14km (Vanilla)";
                default: return mode.ToString();
            }
        }

        public DropdownItem<int>[] GetTerrainResolutionItems()
        {
            return new DropdownItem<int>[]
            {
                new DropdownItem<int> { value = (int)TerrainResolutionSetting.Vanilla_4096, displayName = "4096×4096 (Vanilla)" },
            };
        }

        public DropdownItem<int>[] GetWaterResolutionItems()
        {
            return new DropdownItem<int>[]
            {
                new DropdownItem<int> { value = (int)WaterResolutionSetting.Vanilla_2048, displayName = "2048×2048 (Vanilla)" },
            };
        }

        public DropdownItem<int>[] GetWaterSimQualityItems()
        {
            return new DropdownItem<int>[]
            {
                new DropdownItem<int> { value = (int)WaterSimQualitySetting.Vanilla_EveryFrame, displayName = "Vanilla (Every Frame)" },
                new DropdownItem<int> { value = (int)WaterSimQualitySetting.Reduced_NoBackdrop, displayName = "Reduced (No Backdrop)" },
                new DropdownItem<int> { value = (int)WaterSimQualitySetting.Minimal_Every4Frames, displayName = "Minimal (Every 4 Frames)" },
                new DropdownItem<int> { value = (int)WaterSimQualitySetting.Paused_NoFlow, displayName = "Paused (No Flow)" },
            };
        }

        public DropdownItem<int>[] GetWaterTextureFormatItems()
        {
            return new DropdownItem<int>[]
            {
                new DropdownItem<int> { value = (int)WaterTextureFormatSetting.High_RGBA32F, displayName = "High - 32-bit HDR (Vanilla)" },
                new DropdownItem<int> { value = (int)WaterTextureFormatSetting.Low_RGBA16F, displayName = "Low - 16-bit Float (-43% VRAM)" },
            };
        }

        #endregion
    }
}