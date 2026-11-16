// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// 本文件同时编进开了可空检查的 MapExTools.Core，Mod 端默认没开：文件内自行打开，两边结果一致
#nullable enable

using System;

namespace MapExtPDX
{
    /// <summary>.coc 键的取值类型。</summary>
    public enum StartupKeyKind
    {
        /// <summary>地图尺寸模式，按枚举名存取（取值见 <see cref="StartupKeys.ModeNames"/>）。</summary>
        Mode,

        /// <summary>布尔开关，存为 <c>true</c>／<c>false</c>。</summary>
        Bool,

        /// <summary>其他枚举下拉项，按枚举名存取（取值见 <see cref="StartupKeyInfo.EnumNames"/>）。</summary>
        Enum,

        /// <summary>整数滑块项，存为十进制整数。</summary>
        Int,

        /// <summary>浮点滑块项，存为十进制数值。</summary>
        Float,
    }

    /// <summary>MapExTools 模组设置页的一级分类。</summary>
    public enum OptionCategory
    {
        /// <summary>核心模式与存档转换。</summary>
        Core,

        /// <summary>性能、画面与编辑器工具。</summary>
        Performance,

        /// <summary>寻路成本上限。</summary>
        Pathfinding,

        /// <summary>经济行为与吞吐量。</summary>
        EcoBehavior,

        /// <summary>地价与租金管控。</summary>
        RentControl,

        /// <summary>游戏内面板外观与开发预设。</summary>
        Interface,
    }

    /// <summary>MapExTools 模组设置页分类内的分组卡片。</summary>
    public enum OptionGroup
    {
        ModeAndConvert,
        EcoToggles,
        TerrainWater,
        EntitiesTraffic,
        EditorBrush,
        VehicleRescue,
        PathfindDaily,
        PathfindSchool,
        EcoThroughput,
        EcoConsumption,
        EcoCandidates,
        LandValue,
        RentMatrix,
        PanelSize,
        DashboardSections,
        DebugExport,
    }

    /// <summary>一个受管的 .coc 设定键：名称即 <c>ModSettings</c> 的属性名，也是 .coc 里的键名。</summary>
    public sealed class StartupKeyInfo
    {
        public readonly string Name;
        public readonly StartupKeyKind Kind;

        /// <summary>默认值：Mode/Enum 为枚举名字符串，Bool 为 bool，Int 为 int，Float 为 float。</summary>
        public readonly object DefaultValue;

        /// <summary>是否受经济修复总开关 <c>isEnableEconomyFix</c> 约束。</summary>
        public readonly bool EcoGroup;

        public readonly OptionCategory Category;
        public readonly OptionGroup Group;

        /// <summary>枚举声明顺序的全部名称（供数字序号解码与 Mod 启动反射自检）。非枚举项为 null。</summary>
        public readonly string[]? EnumNames;

        /// <summary>界面可选的合法枚举名称（排除了已挂起项如 <c>Adaptive_EventDriven</c>）。非枚举项为 null。</summary>
        public readonly string[]? SelectableEnumNames;

        public readonly double Min;
        public readonly double Max;
        public readonly double Step;

        /// <summary>界面显示相对存储值的倍率（如 <c>ShoppingTrafficReduction</c> 存 0.0004、界面显示 4，倍率为 10000）。</summary>
        public readonly double DisplayScale;

        /// <summary>单位后缀（如 <c>%</c>、<c>m</c>、<c>rem</c>、<c>×0.0001</c>），无单位时为空字符串。</summary>
        public readonly string Unit;

        private StartupKeyInfo(
            string name,
            StartupKeyKind kind,
            object defaultValue,
            bool ecoGroup,
            OptionCategory category,
            OptionGroup group,
            string[]? enumNames = null,
            string[]? selectableEnumNames = null,
            double min = 0d,
            double max = 0d,
            double step = 0d,
            double displayScale = 1d,
            string unit = "")
        {
            Name = name;
            Kind = kind;
            DefaultValue = defaultValue;
            EcoGroup = ecoGroup;
            Category = category;
            Group = group;
            EnumNames = enumNames;
            SelectableEnumNames = selectableEnumNames ?? enumNames;
            Min = min;
            Max = max;
            Step = step;
            DisplayScale = displayScale;
            Unit = unit;
        }

        public static StartupKeyInfo Mode(string name, string defaultMode, OptionCategory category, OptionGroup group) =>
            new StartupKeyInfo(name, StartupKeyKind.Mode, defaultMode, false, category, group, StartupKeys.ModeNames, StartupKeys.ModeNames);

        public static StartupKeyInfo Bool(string name, bool defaultValue, OptionCategory category, OptionGroup group, bool ecoGroup = false) =>
            new StartupKeyInfo(name, StartupKeyKind.Bool, defaultValue, ecoGroup, category, group);

        public static StartupKeyInfo Enum(
            string name,
            string defaultValue,
            string[] enumNames,
            OptionCategory category,
            OptionGroup group,
            string[]? selectableEnumNames = null) =>
            new StartupKeyInfo(name, StartupKeyKind.Enum, defaultValue, false, category, group, enumNames, selectableEnumNames);

        public static StartupKeyInfo Int(
            string name,
            int defaultValue,
            int min,
            int max,
            int step,
            OptionCategory category,
            OptionGroup group,
            string unit = "") =>
            new StartupKeyInfo(name, StartupKeyKind.Int, defaultValue, false, category, group, null, null, min, max, step, 1d, unit);

        public static StartupKeyInfo Float(
            string name,
            float defaultValue,
            double min,
            double max,
            double step,
            OptionCategory category,
            OptionGroup group,
            double displayScale = 1d,
            string unit = "") =>
            new StartupKeyInfo(name, StartupKeyKind.Float, defaultValue, false, category, group, null, null, min, max, step, displayScale, unit);
    }

    /// <summary>一个地图尺寸模式与它的 CoreValue（边长倍率）。</summary>
    public sealed class StartupModeInfo
    {
        public readonly string Name;
        public readonly int CoreValue;

        public StartupModeInfo(string name, int coreValue)
        {
            Name = name;
            CoreValue = coreValue;
        }

        /// <summary>地图边长（米）= <see cref="StartupKeys.OriginalMapSize"/> × CoreValue。</summary>
        public int MapSize => StartupKeys.OriginalMapSize * CoreValue;
    }

    /// <summary>
    /// MapExt 持久化设定与存档标签的共享定义：9 个启动期设定键（<see cref="Keys"/>）、全量 65 个 OptionUI
    /// 设定键（<see cref="AllKeys"/>）、地图尺寸模式与 CoreValue、存档标签、地图面积推断基准。
    ///
    /// <para><b>同一份文件编进两个工程</b>：Mod（<c>MapExt2.csproj</c> 默认通配）与外部工具
    /// <c>MapExTools.Core</c>（<c>Compile Include … Link</c>）。所以这里只能写纯 C#：C# 9 语法、
    /// 不引用 Unity／Colossal 程序集；可空上下文由文件顶部的 <c>#nullable enable</c> 统一打开。</para>
    ///
    /// <para>改默认值、增删模式或改标签时，要同步 <c>ModSettings</c> 的属性初始值与
    /// <c>SetDefaults</c>、<c>PatchManager.GetCoreValueForMode</c>。Mod 启动时逐项自检，
    /// 不一致记 Warn（<c>Mod.VerifyStartupKeys</c>）。</para>
    /// </summary>
    public static class StartupKeys
    {
        #region Constants

        /// <summary>设定文件相对用户数据目录的路径（对应 <c>ModSettings</c> 的 <c>FileLocation</c>）。</summary>
        public const string CocRelativePath = "ModsSettings/MapExtPDX/MapExtPDX.coc";

        /// <summary>.coc 里本 Mod 设定块的块名。</summary>
        public const string CocBlockName = "MapExtPDX";

        /// <summary>地图尺寸模式的键名。</summary>
        public const string ModeKey = "PatchModeChoice";

        /// <summary>原版地图边长（米）。</summary>
        public const int OriginalMapSize = 14336;

        /// <summary>存档 <c>modsEnabled</c> 里模式值标签的前缀，后接 CoreValue 整数。</summary>
        public const string CoreValueTagPrefix = "MapEXT_CoreValue=";

        /// <summary>存档 <c>modsEnabled</c> 里本 Mod 条目的标识前缀（比较时不分大小写）。</summary>
        public const string ModIdentifierPrefix = "MapExt";

        /// <summary>用过本 Mod 但没有模式值标签的旧版存档，按 57 km（CV=4）处理。</summary>
        public const int LegacySaveCoreValue = 4;

        /// <summary>
        /// 原版存档（<c>modsEnabled</c> 里没有本 Mod）的判定值。它不是某个模式的 CoreValue，
        /// 只能在 None 模式（CV=1）下直接读。
        /// </summary>
        public const int UntaggedVanillaCoreValue = -1;

        /// <summary>标签存在但解析不出整数时的判定值，一律按不符处理。</summary>
        public const int UnparsableCoreValue = 0;

        /// <summary>地图面积比值与 CV² 的相对容差：超过就判为面积不可信。</summary>
        public const double MapAreaRatioTolerance = 0.05;

        #endregion

        #region Fields

        /// <summary>
        /// 模式枚举名，顺序与 <c>PatchModeSetting</c> 的声明顺序一致。
        /// 游戏解析不出枚举名时取 <c>default(T)</c>，即第一项 ModeA。
        /// </summary>
        public static readonly string[] ModeNames = { "ModeA", "ModeB", "ModeC", "ModeD", "None" };

        /// <summary><c>WaterSimQualitySetting</c> 的声明顺序（末尾 <c>Adaptive_EventDriven</c> 已硬挂起）。</summary>
        public static readonly string[] WaterSimQualityNames =
        {
            "Vanilla_EveryFrame", "Reduced_NoBackdrop", "Minimal_Every4Frames", "Paused_NoFlow", "Adaptive_EventDriven"
        };

        /// <summary><c>WaterSimQualitySetting</c> 在界面中允许选择的 4 个有效档位。</summary>
        public static readonly string[] WaterSimQualitySelectableNames =
        {
            "Vanilla_EveryFrame", "Reduced_NoBackdrop", "Minimal_Every4Frames", "Paused_NoFlow"
        };

        /// <summary><c>SnowSimFreezeSetting</c> 的声明顺序。</summary>
        public static readonly string[] SnowSimFreezeNames = { "Off", "Auto", "Always" };

        /// <summary><c>EditorCollisionSkipMode</c> 的声明顺序。</summary>
        public static readonly string[] EditorCollisionSkipNames = { "Off", "TreesOnly", "AllObjects" };

        /// <summary><c>HeightmapExportOrientation</c> 的声明顺序。</summary>
        public static readonly string[] HeightmapOrientationNames =
        {
            "Native", "FlipVertical", "FlipHorizontal", "Rotate180", "All"
        };

        /// <summary>各模式的 CoreValue，顺序同 <see cref="ModeNames"/>。</summary>
        public static readonly StartupModeInfo[] Modes =
        {
            new StartupModeInfo("ModeA", 4), // 57 km
            new StartupModeInfo("ModeB", 2), // 28 km
            new StartupModeInfo("ModeC", 8), // 114 km
            new StartupModeInfo("ModeD", 3), // 43 km
            new StartupModeInfo("None", 1),  // 14 km 原版
        };

        // --- 9 个核心启动键实例（供 Keys 与 AllKeys 共享同一引用） ---
        private static readonly StartupKeyInfo s_patchModeChoice =
            StartupKeyInfo.Mode(ModeKey, "ModeA", OptionCategory.Core, OptionGroup.ModeAndConvert);

        private static readonly StartupKeyInfo s_enableEconomyFix =
            StartupKeyInfo.Bool("isEnableEconomyFix", true, OptionCategory.Core, OptionGroup.EcoToggles);

        private static readonly StartupKeyInfo s_enableDemand =
            StartupKeyInfo.Bool("EnableDemandEcoSystem", true, OptionCategory.Core, OptionGroup.EcoToggles, ecoGroup: true);

        private static readonly StartupKeyInfo s_enableJobSearch =
            StartupKeyInfo.Bool("EnableJobSearchEcoSystem", true, OptionCategory.Core, OptionGroup.EcoToggles, ecoGroup: true);

        private static readonly StartupKeyInfo s_enableHouseholdProperty =
            StartupKeyInfo.Bool("EnableHouseholdPropertyEcoSystem", true, OptionCategory.Core, OptionGroup.EcoToggles, ecoGroup: true);

        private static readonly StartupKeyInfo s_enableResourceBuyer =
            StartupKeyInfo.Bool("EnableResourceBuyerEcoSystem", false, OptionCategory.Core, OptionGroup.EcoToggles, ecoGroup: true);

        private static readonly StartupKeyInfo s_enableResidentAI =
            StartupKeyInfo.Bool("EnableResidentAIEcoSystem", false, OptionCategory.Core, OptionGroup.EcoToggles, ecoGroup: true);

        private static readonly StartupKeyInfo s_enableDownstreamAI =
            StartupKeyInfo.Bool("EnableDownstreamAIEcoSystem", false, OptionCategory.Core, OptionGroup.EcoToggles, ecoGroup: true);

        private static readonly StartupKeyInfo s_disableLoadGameValidation =
            StartupKeyInfo.Bool("DisableLoadGameValidation", false, OptionCategory.Core, OptionGroup.ModeAndConvert);

        /// <summary>启动期受管的 9 个核心键与默认值（供命令行接口与固定样本测试保持兼容）。</summary>
        public static readonly StartupKeyInfo[] Keys =
        {
            s_patchModeChoice,
            s_enableEconomyFix,
            s_enableDemand,
            s_enableJobSearch,
            s_enableHouseholdProperty,
            s_enableResourceBuyer,
            s_enableResidentAI,
            s_enableDownstreamAI,
            s_disableLoadGameValidation,
        };

        /// <summary>
        /// MapExTools 图形界面受管的全部 65 个持久化键（须与 <c>ModSettings</c> 的属性名、类型及默认值严格对齐）。
        /// 前 9 项与 <see cref="Keys"/> 顺序及实例一致，保证追加缺失键时的写入次序向后兼容。
        /// </summary>
        public static readonly StartupKeyInfo[] AllKeys =
        {
            // === 0. 原有 9 个启动期核心键（保持前导顺序与引用不变） ===
            s_patchModeChoice,
            s_enableEconomyFix,
            s_enableDemand,
            s_enableJobSearch,
            s_enableHouseholdProperty,
            s_enableResourceBuyer,
            s_enableResidentAI,
            s_enableDownstreamAI,
            s_disableLoadGameValidation,

            // === 1. 核心模式与转换补充（2 项） ===
            StartupKeyInfo.Bool("EnableVanillaConversion", false, OptionCategory.Core, OptionGroup.ModeAndConvert),
            StartupKeyInfo.Bool("DisableWorldBackdrop", false, OptionCategory.Core, OptionGroup.ModeAndConvert),

            // === 2. 性能、画面与工具（14 项） ===
            StartupKeyInfo.Bool("TerrainBufferPrealloc", true, OptionCategory.Performance, OptionGroup.TerrainWater),
            StartupKeyInfo.Bool("TerrainCascadeThrottle", false, OptionCategory.Performance, OptionGroup.TerrainWater),
            StartupKeyInfo.Bool("TerrainDownsampleThrottle", false, OptionCategory.Performance, OptionGroup.TerrainWater),
            StartupKeyInfo.Bool("WaterPauseFreeze", true, OptionCategory.Performance, OptionGroup.TerrainWater),
            StartupKeyInfo.Enum("WaterSimQuality", "Vanilla_EveryFrame", WaterSimQualityNames,
                OptionCategory.Performance, OptionGroup.TerrainWater, WaterSimQualitySelectableNames),
            StartupKeyInfo.Enum("SnowSimFreeze", "Off", SnowSimFreezeNames,
                OptionCategory.Performance, OptionGroup.TerrainWater),

            StartupKeyInfo.Bool("NoDogsOnStreet", false, OptionCategory.Performance, OptionGroup.EntitiesTraffic),
            StartupKeyInfo.Bool("NoDogsGeneration", false, OptionCategory.Performance, OptionGroup.EntitiesTraffic),
            StartupKeyInfo.Bool("NoThroughTraffic", false, OptionCategory.Performance, OptionGroup.EntitiesTraffic),

            StartupKeyInfo.Enum("EditorCollisionSkip", "Off", EditorCollisionSkipNames,
                OptionCategory.Performance, OptionGroup.EditorBrush),
            StartupKeyInfo.Bool("TerrainBrushRoadBlock", false, OptionCategory.Performance, OptionGroup.EditorBrush),
            StartupKeyInfo.Float("TerrainBrushRoadMargin", 4f, 0d, 30d, 1d,
                OptionCategory.Performance, OptionGroup.EditorBrush, 1d, "m"),

            StartupKeyInfo.Bool("EnableVehicleRescue", false, OptionCategory.Performance, OptionGroup.VehicleRescue),
            StartupKeyInfo.Bool("EnableRescueDebugLog", false, OptionCategory.Performance, OptionGroup.VehicleRescue),

            // === 3. 寻路成本上限（10 项：严守 min >= 1000 / 17000，禁止为 0） ===
            StartupKeyInfo.Float("ShoppingMaxCost", 8000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindDaily),
            StartupKeyInfo.Float("CompanyShoppingMaxCost", 200000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindDaily),
            StartupKeyInfo.Float("LeisureMaxCost", 12000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindDaily),
            StartupKeyInfo.Float("EmergencyMaxCost", 6000f, 1000d, 17000d, 500d, OptionCategory.Pathfinding, OptionGroup.PathfindDaily),
            StartupKeyInfo.Float("FindJobMaxCost", 200000f, 17000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindDaily),
            StartupKeyInfo.Float("FindHomeMaxCost", 200000f, 17000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindDaily),

            StartupKeyInfo.Float("FindSchoolElementaryMaxCost", 10000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindSchool),
            StartupKeyInfo.Float("FindSchoolHighSchoolMaxCost", 17000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindSchool),
            StartupKeyInfo.Float("FindSchoolCollegeMaxCost", 50000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindSchool),
            StartupKeyInfo.Float("FindSchoolUniversityMaxCost", 100000f, 1000d, 200000d, 1000d, OptionCategory.Pathfinding, OptionGroup.PathfindSchool),

            // === 4. 经济行为与吞吐量（9 项） ===
            StartupKeyInfo.Int("JobSeekerCap", 1000, 200, 5000, 100, OptionCategory.EcoBehavior, OptionGroup.EcoThroughput),
            StartupKeyInfo.Int("PathfindRequestCap", 4000, 500, 10000, 500, OptionCategory.EcoBehavior, OptionGroup.EcoThroughput),
            StartupKeyInfo.Int("HomeSeekerCap", 128, 32, 512, 16, OptionCategory.EcoBehavior, OptionGroup.EcoThroughput),
            StartupKeyInfo.Int("HomelessSeekerCap", 1280, 128, 5120, 128, OptionCategory.EcoBehavior, OptionGroup.EcoThroughput),

            StartupKeyInfo.Float("ShoppingTrafficReduction", 0.0004f, 0.0001d, 0.0020d, 0.0001d,
                OptionCategory.EcoBehavior, OptionGroup.EcoConsumption, 10000d, "×0.0001"),
            StartupKeyInfo.Float("HouseholdResourceDemandMultiplier", 3.5f, 1.0d, 8.0d, 0.5d,
                OptionCategory.EcoBehavior, OptionGroup.EcoConsumption, 1d, "×"),

            StartupKeyInfo.Int("ResourceSellerCandidateCap", 8, 3, 20, 1, OptionCategory.EcoBehavior, OptionGroup.EcoCandidates),
            StartupKeyInfo.Int("LeisureCandidateCap", 6, 3, 15, 1, OptionCategory.EcoBehavior, OptionGroup.EcoCandidates),
            StartupKeyInfo.Int("FindHomeCandidateCap", 5, 3, 15, 1, OptionCategory.EcoBehavior, OptionGroup.EcoCandidates),

            // === 5. 地价与租金管控（11 项） ===
            StartupKeyInfo.Int("LandValueEnvironmentEffect", 40, 0, 100, 5, OptionCategory.RentControl, OptionGroup.LandValue, "%"),
            StartupKeyInfo.Int("ServiceBonusCapMultiplier", 100, 0, 200, 10, OptionCategory.RentControl, OptionGroup.LandValue, "%"),

            StartupKeyInfo.Int("RentMultiplierResidential", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("RentMultiplierCommercial", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("RentMultiplierIndustrial", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("LandValueFactorResidential", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("LandValueFactorCommercial", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("LandValueFactorIndustrial", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("LevelFactorResidential", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("LevelFactorCommercial", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),
            StartupKeyInfo.Int("LevelFactorIndustrial", 100, 0, 200, 5, OptionCategory.RentControl, OptionGroup.RentMatrix, "%"),

            // === 6. 游戏内面板外观与开发预设（10 项） ===
            StartupKeyInfo.Int("UIMenuPanelWidth", 200, 160, 320, 10, OptionCategory.Interface, OptionGroup.PanelSize, "rem"),
            StartupKeyInfo.Int("UIDetailPanelWidth", 220, 200, 450, 10, OptionCategory.Interface, OptionGroup.PanelSize, "rem"),
            StartupKeyInfo.Int("UIPanelHeight", 1000, 300, 1000, 20, OptionCategory.Interface, OptionGroup.PanelSize, "rem"),

            StartupKeyInfo.Bool("DashboardDefaultCityStats", true, OptionCategory.Interface, OptionGroup.DashboardSections),
            StartupKeyInfo.Bool("DashboardDefaultResidential", true, OptionCategory.Interface, OptionGroup.DashboardSections),
            StartupKeyInfo.Bool("DashboardDefaultCommercial", true, OptionCategory.Interface, OptionGroup.DashboardSections),
            StartupKeyInfo.Bool("DashboardDefaultActivity", true, OptionCategory.Interface, OptionGroup.DashboardSections),
            StartupKeyInfo.Bool("DashboardDefaultMisc", true, OptionCategory.Interface, OptionGroup.DashboardSections),

            StartupKeyInfo.Enum("HeightmapExportDirection", "Native", HeightmapOrientationNames,
                OptionCategory.Interface, OptionGroup.DebugExport),
            StartupKeyInfo.Bool("HeightmapExportRaw", false, OptionCategory.Interface, OptionGroup.DebugExport),
        };

        #endregion

        #region Helpers

        /// <summary>原版地图的分块总面积（14336²，约 2.0552E+08 m²），地图面积推断的基准。</summary>
        public static double VanillaTileArea => (double)OriginalMapSize * OriginalMapSize;

        /// <summary>按模式名取 CoreValue；名称不认识时返回 0。比较区分大小写，与游戏解析枚举名一致。</summary>
        public static int GetCoreValue(string modeName)
        {
            foreach (StartupModeInfo mode in Modes)
            {
                if (mode.Name == modeName) return mode.CoreValue;
            }

            return 0;
        }

        /// <summary>按 CoreValue 取模式；不是现行模式时返回 null（已废弃的旧 ModeD CV=16 也返回 null）。</summary>
        public static StartupModeInfo? FindModeByCoreValue(int coreValue)
        {
            foreach (StartupModeInfo mode in Modes)
            {
                if (mode.CoreValue == coreValue) return mode;
            }

            return null;
        }

        /// <summary>是否为现行模式之一的 CoreValue。</summary>
        public static bool IsKnownCoreValue(int coreValue) => FindModeByCoreValue(coreValue) != null;

        /// <summary>在全部 65 个受管键中按键名取定义；不认识时返回 null（区分大小写）。</summary>
        public static StartupKeyInfo? FindKey(string name)
        {
            foreach (StartupKeyInfo key in AllKeys)
            {
                if (key.Name == name) return key;
            }

            return null;
        }

        /// <summary>判断两个同类型受管键的取值是否相等（浮点采用 <c>1e-6f</c> 容差防精度毛刺）。</summary>
        public static bool ValueEquals(StartupKeyInfo key, object a, object b)
        {
            if (key.Kind == StartupKeyKind.Float && a is float fa && b is float fb)
            {
                return Math.Abs(fa - fb) < 1e-6f;
            }

            return Equals(a, b);
        }

        #endregion
    }
}
