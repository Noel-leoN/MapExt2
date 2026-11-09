// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// 本文件同时编进开了可空检查的 MapExTools.Core，Mod 端默认没开：文件内自行打开，两边结果一致
#nullable enable

namespace MapExtPDX
{
    /// <summary>.coc 键的取值类型。</summary>
    public enum StartupKeyKind
    {
        /// <summary>地图尺寸模式，按枚举名存取（取值见 <see cref="StartupKeys.ModeNames"/>）。</summary>
        Mode,

        /// <summary>布尔开关，存为 <c>true</c>／<c>false</c>。</summary>
        Bool,
    }

    /// <summary>一个启动期设定键：名称即 <c>ModSettings</c> 的属性名，也是 .coc 里的键名。</summary>
    public sealed class StartupKeyInfo
    {
        public readonly string Name;
        public readonly StartupKeyKind Kind;

        /// <summary>默认值：<see cref="StartupKeyKind.Mode"/> 为模式名字符串，<see cref="StartupKeyKind.Bool"/> 为 bool。</summary>
        public readonly object DefaultValue;

        /// <summary>是否受经济修复总开关 <c>isEnableEconomyFix</c> 约束。</summary>
        public readonly bool EcoGroup;

        public StartupKeyInfo(string name, StartupKeyKind kind, object defaultValue, bool ecoGroup)
        {
            Name = name;
            Kind = kind;
            DefaultValue = defaultValue;
            EcoGroup = ecoGroup;
        }
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
    /// MapExt 启动期设定与存档标签的共享定义：.coc 里的 9 个键、地图尺寸模式与 CoreValue、存档标签、
    /// 地图面积推断基准。
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

        /// <summary>各模式的 CoreValue，顺序同 <see cref="ModeNames"/>。</summary>
        public static readonly StartupModeInfo[] Modes =
        {
            new StartupModeInfo("ModeA", 4), // 57 km
            new StartupModeInfo("ModeB", 2), // 28 km
            new StartupModeInfo("ModeC", 8), // 114 km
            new StartupModeInfo("ModeD", 3), // 43 km
            new StartupModeInfo("None", 1),  // 14 km 原版
        };

        /// <summary>受管的 9 个键与默认值，须与 <c>ModSettings</c> 的属性初始值和 <c>SetDefaults</c> 一致。</summary>
        public static readonly StartupKeyInfo[] Keys =
        {
            new StartupKeyInfo(ModeKey, StartupKeyKind.Mode, "ModeA", false),
            new StartupKeyInfo("isEnableEconomyFix", StartupKeyKind.Bool, true, false),
            new StartupKeyInfo("EnableDemandEcoSystem", StartupKeyKind.Bool, true, true),
            new StartupKeyInfo("EnableJobSearchEcoSystem", StartupKeyKind.Bool, true, true),
            new StartupKeyInfo("EnableHouseholdPropertyEcoSystem", StartupKeyKind.Bool, true, true),
            new StartupKeyInfo("EnableResourceBuyerEcoSystem", StartupKeyKind.Bool, false, true),
            new StartupKeyInfo("EnableResidentAIEcoSystem", StartupKeyKind.Bool, false, true),
            new StartupKeyInfo("EnableDownstreamAIEcoSystem", StartupKeyKind.Bool, false, true),
            new StartupKeyInfo("DisableLoadGameValidation", StartupKeyKind.Bool, false, false),
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

        /// <summary>按键名取键定义；不认识时返回 null。</summary>
        public static StartupKeyInfo? FindKey(string name)
        {
            foreach (StartupKeyInfo key in Keys)
            {
                if (key.Name == name) return key;
            }

            return null;
        }

        #endregion
    }
}
