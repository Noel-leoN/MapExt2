// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit MapExt2 and the author.

// 本文件同时编进开了可空检查的 MapExTools.Core 与默认未开的 MapExt2.csproj：
// 必须保持纯 C# 9 语法、零 Unity/Colossal/HarmonyLib 程序集引用，文件内自行开启 #nullable enable。
#nullable enable

using System;
using System.Collections.Generic;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>受保护原版系统与宿主在 MapExt 架构中的介入类别。</summary>
    public enum ProtectedSystemRole
    {
        /// <summary>全模式（含 <c>None</c> 原版尺寸）必替的 CellMap 系统（<c>LandValueSystem</c>）。</summary>
        AlwaysReplacedCellMap,

        /// <summary>仅扩展模式（<c>ModeA–D</c>）替换的其余 11 个 <c>CellMapSystem&lt;T&gt;</c> 系统。</summary>
        ExpandedOnlyCellMap,

        /// <summary>
        /// 仅扩展模式（<c>ModeA–D</c>）挂载 ReBurst Transpiler 且运行期须保持 <c>Enabled == true</c> 的 18 个模拟核心宿主。
        /// 同时参与静态 <c>set_Enabled</c> 检测、Harmony 目标方法重叠检测与运行期低频状态轮询。
        /// </summary>
        ExpandedOnlyCellMapCoreHost,

        /// <summary>
        /// 仅扩展模式（<c>ModeA–D</c>）挂载 ReBurst Transpiler 的 9 个工具／界面／音频宿主。
        /// 常态即处于 <c>Enabled == false</c>，仅参与 Harmony 目标方法重叠检测，不参与静态 <c>Enabled</c> 检测与运行期轮询。
        /// </summary>
        ExpandedOnlyCellMapAuxHost,

        /// <summary>
        /// <c>CellOnly</c> 双重归属系统（C1 <c>HouseholdFindPropertySystem</c>、D1 <c>RentAdjustSystem</c>）：
        /// <c>ModeA–D</c> 下即使关闭经济组仍强制注册 <c>*_CellOnly</c> 并禁用原版（硬冲突，不可靠关组规避）；
        /// <c>None</c> 原版尺寸模式下仅在 <c>isEnableEconomyFix &amp;&amp; EnableHouseholdPropertyEcoSystem</c> 开启时替换。
        /// </summary>
        CellOnlyDualRole,

        /// <summary>全模式共用的纯 Shared 经济系统替换（含 <c>NetUpkeepSystem</c> 纯禁用），受经济修复开关控制。</summary>
        SharedEconomyReplaced,

        /// <summary>全模式共用的 3 个 RCI 需求 Job Transpiler 宿主，受 <c>isEnableEconomyFix &amp;&amp; EnableDemandEcoSystem</c> 控制。</summary>
        SharedDemandHost,
    }

    /// <summary>结合当前 <c>MapExtPDX.coc</c> 模式与经济分组开关计算出的冲突定级。</summary>
    public enum ConflictSeverity
    {
        /// <summary>当前模式下本系统／补丁未介入，无冲突。</summary>
        None = 0,

        /// <summary>信息备注（如云端待下载未扫字节码、仅 <c>OnDispose</c> 含无参 <c>UnpatchAll</c>、或 <c>EconomyEX</c> 自动休眠）。</summary>
        Info = 1,

        /// <summary>已被当前 <c>.coc</c> 设定（如关闭对应纯经济分组或处于 <c>None</c> 模式）规避。</summary>
        Mitigated = 2,

        /// <summary>条件冲突或需关注项（部分可通过关闭对应纯经济分组规避，见二维判定结果）。</summary>
        Warning = 3,

        /// <summary>硬冲突：在当前模式下无法通过关闭经济分组消除，或涉及写存档组件／主线程阻塞／全局清补丁。</summary>
        Hard = 4,
    }

    /// <summary>Harmony 目标方法的生效条件分类。</summary>
    public enum PatchTargetScope
    {
        /// <summary>全模式（<c>None</c> 与 <c>ModeA–D</c>）均会挂载。</summary>
        AllModes,

        /// <summary>仅扩展模式（<c>ModeA–D</c>）挂载（含地形分辨率 &gt; 4096 或仅 <c>ModeA</c> 的条件项）。</summary>
        ExpandedModesOnly,

        /// <summary>受 <c>isEnableEconomyFix &amp;&amp; EnableDemandEcoSystem</c> 控制。</summary>
        EconomyDemand,

        /// <summary>受 <c>isEnableEconomyFix &amp;&amp; EnableHouseholdPropertyEcoSystem</c> 控制。</summary>
        EconomyHouseholdProperty,

        /// <summary>受 <c>isEnableEconomyFix &amp;&amp; EnableResourceBuyerEcoSystem</c> 控制。</summary>
        EconomyResourceBuyer,

        /// <summary>
        /// <c>PathfindSetupSystem.FindTargets</c>：扩展模式下必挂（C1 补丁或 <c>CellOnly</c> dispatcher 二选一），
        /// <c>None</c> 模式下当 <c>HouseholdProperty</c>／<c>ResourceBuyer</c>／<c>DownstreamAI</c> 任一生效时挂载。
        /// </summary>
        FindTargets,
    }

    /// <summary>单个受保护原版系统或宿主的元数据定义。</summary>
    public sealed class ProtectedSystemTarget
    {
        /// <summary>完整 CLR 类型名（嵌套类使用 <c>+</c>，如 <c>Game.Simulation.CarNavigationSystem+Actions</c>）。</summary>
        public readonly string FullTypeName;

        public readonly ProtectedSystemRole Role;

        /// <summary>所属逻辑分组名（如 <c>CellMap</c>、<c>HouseholdProperty</c>），与 <c>AppliedConfig</c> 一致。</summary>
        public readonly string GroupName;

        /// <summary>可通过关闭该 <c>.coc</c> 布尔键消解冲突时的键名；无法单靠子开关消解时为 null。</summary>
        public readonly string? MitigatingToggleKey;

        public ProtectedSystemTarget(
            string fullTypeName,
            ProtectedSystemRole role,
            string groupName,
            string? mitigatingToggleKey = null)
        {
            FullTypeName = fullTypeName;
            Role = role;
            GroupName = groupName;
            MitigatingToggleKey = mitigatingToggleKey;
        }

        /// <summary>是否参与静态 <c>ComponentSystemBase.set_Enabled</c> 篡改检测（排除 9 个常态禁用的工具/UI/音频宿主）。</summary>
        public bool CheckStaticEnabledWrite => Role != ProtectedSystemRole.ExpandedOnlyCellMapAuxHost;
    }

    /// <summary>MapExt 两个 Harmony 实例挂载的目标方法声明。</summary>
    public sealed class PatchedMethodTarget
    {
        /// <summary>声明该方法的完整类型名（泛型定义保留反引号算术后缀，如 <c>Game.Simulation.CellMapSystem`1</c>）。</summary>
        public readonly string TargetTypeName;

        /// <summary>泛型闭合参数完整类型名（仅用于区分 <c>CellMapSystem&lt;LandValueCell&gt;</c> 全模式与其余 11 个扩展模式闭合类）。</summary>
        public readonly string? GenericArgumentTypeName;

        public readonly string MethodName;

        /// <summary><c>MapExtPDX_modes</c> 或 <c>MapExtPDX_global</c>。</summary>
        public readonly string HarmonyOwner;

        public readonly PatchTargetScope Scope;

        public PatchedMethodTarget(
            string targetTypeName,
            string methodName,
            string harmonyOwner,
            PatchTargetScope scope,
            string? genericArgumentTypeName = null)
        {
            TargetTypeName = targetTypeName;
            MethodName = methodName;
            HarmonyOwner = harmonyOwner;
            Scope = scope;
            GenericArgumentTypeName = genericArgumentTypeName;
        }
    }

    /// <summary>
    /// 已知第三方 Mod 审计与存量指纹规则条目（仅收录静态系统开关特征无法单独完整表达的机制项；
    /// 不收录未完成早期原型，所有描述均采用中性可复核事实，文案由三语表按 <see cref="RuleId"/> 渲染）。
    /// </summary>
    public sealed class KnownModConflictRule
    {
        /// <summary>稳定规则标识（如 <c>RWH</c>、<c>RoadBuilderDualInstall</c>），对应三语文案键后缀。</summary>
        public readonly string RuleId;

        /// <summary>Paradox Mods 平台数字 ID（字符串形式，如 <c>"77171"</c>）；有测试分支时见 <see cref="AlternatePdxModIds"/>。</summary>
        public readonly string PdxModId;

        public readonly string[] AlternatePdxModIds;

        /// <summary>匹配的托管程序集名（不含 <c>.dll</c>，大小写不敏感精确匹配）。</summary>
        public readonly string[] AssemblyNames;

        /// <summary>完成代码审计时的基准版本号（供界面与当前安装版本并列展示；硬冲突不因版本升高自动放行）。</summary>
        public readonly string AuditedVersion;

        /// <summary>在扩展模式（<c>ModeA–D</c>）下的基础定级。</summary>
        public readonly ConflictSeverity ExpandedModeSeverity;

        /// <summary>在原版尺寸模式（<c>None</c>）且关联经济分组开启时的基础定级。</summary>
        public readonly ConflictSeverity VanillaModeSeverity;

        /// <summary>
        /// 若冲突可通过关闭特定纯经济分组开关消解，列出需关闭的 <c>.coc</c> 键名；
        /// <see cref="CanMitigateInExpandedModes"/> 为 false 时仅在 <c>None</c> 模式下允许一键关组规避。
        /// </summary>
        public readonly string[] MitigatingToggleKeys;

        public readonly bool CanMitigateInExpandedModes;

        /// <summary>是否仅当本地 <c>Mods/</c> 与激活 Playset 订阅同时存在启用副本时才触发提示（如 <c>RoadBuilder</c> 双装前端模块冲突）。</summary>
        public readonly bool RequireDualLocalAndPdxInstall;

        public KnownModConflictRule(
            string ruleId,
            string pdxModId,
            string[] assemblyNames,
            string auditedVersion,
            ConflictSeverity expandedModeSeverity,
            ConflictSeverity vanillaModeSeverity,
            string[]? mitigatingToggleKeys = null,
            bool canMitigateInExpandedModes = false,
            string[]? alternatePdxModIds = null,
            bool requireDualLocalAndPdxInstall = false)
        {
            RuleId = ruleId;
            PdxModId = pdxModId;
            AssemblyNames = assemblyNames;
            AuditedVersion = auditedVersion;
            ExpandedModeSeverity = expandedModeSeverity;
            VanillaModeSeverity = vanillaModeSeverity;
            MitigatingToggleKeys = mitigatingToggleKeys ?? Array.Empty<string>();
            CanMitigateInExpandedModes = canMitigateInExpandedModes;
            AlternatePdxModIds = alternatePdxModIds ?? Array.Empty<string>();
            RequireDualLocalAndPdxInstall = requireDualLocalAndPdxInstall;
        }
    }

    /// <summary>基于当前 <c>.coc</c> 设定对单个系统或目标方法的二维判定结果。</summary>
    public readonly struct SystemConflictEvaluation
    {
        public readonly ConflictSeverity Severity;

        /// <summary>若可通过一键关闭某个纯经济分组消解，返回对应的 <c>.coc</c> 布尔键名；否则为 null。</summary>
        public readonly string? MitigatingToggleKey;

        public SystemConflictEvaluation(ConflictSeverity severity, string? mitigatingToggleKey = null)
        {
            Severity = severity;
            MitigatingToggleKey = mitigatingToggleKey;
        }
    }

    /// <summary>用于二维冲突评估的精简 <c>.coc</c> 生效设定快照（Mod 端与外部工具共用）。</summary>
    public sealed class ConflictEvalSettings
    {
        public readonly string ModeName;
        public readonly bool EconomyFix;
        public readonly bool Demand;
        public readonly bool JobSearch;
        public readonly bool HouseholdProperty;
        public readonly bool ResourceBuyer;
        public readonly bool ResidentAI;
        public readonly bool DownstreamAI;

        public ConflictEvalSettings(
            string modeName,
            bool economyFix,
            bool demand,
            bool jobSearch,
            bool householdProperty,
            bool resourceBuyer,
            bool residentAI,
            bool downstreamAI)
        {
            ModeName = string.IsNullOrEmpty(modeName) ? "ModeA" : modeName;
            EconomyFix = economyFix;
            Demand = demand;
            JobSearch = jobSearch;
            HouseholdProperty = householdProperty;
            ResourceBuyer = resourceBuyer;
            ResidentAI = residentAI;
            DownstreamAI = downstreamAI;
        }

        public bool IsExpandedMode => ModeName != "None";

        public bool IsToggleEffective(string toggleKey)
        {
            if (toggleKey == KeyEconomyFix)
            {
                return EconomyFix;
            }

            if (!EconomyFix)
            {
                return false;
            }

            switch (toggleKey)
            {
                case KeyDemand: return Demand;
                case KeyJobSearch: return JobSearch;
                case KeyHouseholdProperty: return HouseholdProperty;
                case KeyResourceBuyer: return ResourceBuyer;
                case KeyResidentAI: return ResidentAI;
                case KeyDownstreamAI: return DownstreamAI;
                default: return false;
            }
        }

        public static ConflictEvalSettings FromDictionary(IReadOnlyDictionary<string, object> values)
        {
            string mode = values.TryGetValue(StartupKeys.ModeKey, out object? m) && m is string ms && ms.Length > 0
                ? ms
                : "ModeA";
            bool BoolOf(string key, bool fallback) =>
                values.TryGetValue(key, out object? v) && v is bool b ? b : fallback;

            return new ConflictEvalSettings(
                mode,
                BoolOf(KeyEconomyFix, true),
                BoolOf(KeyDemand, true),
                BoolOf(KeyJobSearch, true),
                BoolOf(KeyHouseholdProperty, true),
                BoolOf(KeyResourceBuyer, false),
                BoolOf(KeyResidentAI, false),
                BoolOf(KeyDownstreamAI, false));
        }

        private const string KeyEconomyFix = "isEnableEconomyFix";
        private const string KeyDemand = "EnableDemandEcoSystem";
        private const string KeyJobSearch = "EnableJobSearchEcoSystem";
        private const string KeyHouseholdProperty = "EnableHouseholdPropertyEcoSystem";
        private const string KeyResourceBuyer = "EnableResourceBuyerEcoSystem";
        private const string KeyResidentAI = "EnableResidentAIEcoSystem";
        private const string KeyDownstreamAI = "EnableDownstreamAIEcoSystem";
    }

    /// <summary>
    /// 游戏启动期（<c>Mod.OnLoad</c>）检测到即自动停用重叠经济分组的已知模组。
    /// 本类的 <see cref="AutoDisableGroups"/> 是自动停用分组的唯一来源：<c>ModConflictDetector.AutoDisableConflictGroups</c>
    /// 经 <see cref="ConflictTargets.PlanRuntimeAutoDisable"/> 读取，不另写分组。
    /// </summary>
    public sealed class RuntimeAutoDisableTarget
    {
        /// <summary>稳定标识，与 <c>ModConflictDetector</c> 记录的命中标识一致。</summary>
        public readonly string TargetId;

        public readonly string PdxModId;
        public readonly string[] AssemblyNames;

        /// <summary>检测到该模组时要停用的经济分组（分组名与 <c>AppliedConfig</c> 一致）；设定里本就关着的分组不受影响。</summary>
        public readonly string[] AutoDisableGroups;

        /// <summary>自动停用日志中箭头后的交集说明（如 <c>LeisureSystem</c>）；为 null 时只写模组标识。</summary>
        public readonly string? OverlapNote;

        public RuntimeAutoDisableTarget(
            string targetId,
            string pdxModId,
            string[] assemblyNames,
            string[] autoDisableGroups,
            string? overlapNote = null)
        {
            TargetId = targetId;
            PdxModId = pdxModId;
            AssemblyNames = assemblyNames;
            AutoDisableGroups = autoDisableGroups;
            OverlapNote = overlapNote;
        }
    }

    /// <summary><see cref="ConflictTargets.PlanRuntimeAutoDisable"/> 算出的一个自动停用分组，以及触发它的已知模组。</summary>
    public sealed class RuntimeAutoDisableDecision
    {
        public readonly string Group;
        public readonly RuntimeAutoDisableTarget Target;

        public RuntimeAutoDisableDecision(string group, RuntimeAutoDisableTarget target)
        {
            Group = group;
            Target = target;
        }
    }

    /// <summary>
    /// MapExtPDX 与 MapExTools 共用的冲突检测目标与已知规则单源定义（SSOT）。
    ///
    /// <para>涵盖：受保护的原版替换系统与 ReBurst 宿主分层（18 个模拟核心宿主 vs 9 个工具/界面/音频宿主）、
    /// <c>(PatchMode, System)</c> 二维可规避性判定、Dual Harmony（<c>MapExtPDX_modes</c> 与 <c>MapExtPDX_global</c>）
    /// 目标方法全集，以及附带审计基准版本号的已知冲突规则表。</para>
    /// </summary>
    public static class ConflictTargets
    {
        #region Constants

        public const string HarmonyOwnerModes = "MapExtPDX_modes";
        public const string HarmonyOwnerGlobal = "MapExtPDX_global";

        public const string MapExtPdxModId = "107939";
        public const string EconomyExPdxModId = "137149";

        public const string KeyEconomyFix = "isEnableEconomyFix";
        public const string KeyDemand = "EnableDemandEcoSystem";
        public const string KeyJobSearch = "EnableJobSearchEcoSystem";
        public const string KeyHouseholdProperty = "EnableHouseholdPropertyEcoSystem";
        public const string KeyResourceBuyer = "EnableResourceBuyerEcoSystem";
        public const string KeyResidentAI = "EnableResidentAIEcoSystem";
        public const string KeyDownstreamAI = "EnableDownstreamAIEcoSystem";

        public const string GroupCellMap = "CellMap";
        public const string GroupEconomy = "Economy";
        public const string GroupDemand = "Demand";
        public const string GroupJobSearch = "JobSearch";
        public const string GroupHouseholdProperty = "HouseholdProperty";
        public const string GroupResourceBuyer = "ResourceBuyer";
        public const string GroupResidentAI = "ResidentAI";
        public const string GroupDownstreamAI = "DownstreamAI";

        public const string CellMapGenericTypeName = "Game.Simulation.CellMapSystem`1";
        public const string BaseDataReaderGenericTypeName = "Game.Simulation.BaseDataReader`2";
        public const string LandValueCellTypeName = "Game.Simulation.LandValueCell";

        #endregion

        #region Self & Companion Identifiers

        /// <summary>本工程及配套子 Mod 程序集名（静态扫描时排除自身，EconomyEX 检测到 MapExtPDX 时会自动休眠）。</summary>
        public static readonly string[] SelfAndCompanionAssemblies =
        {
            "MapExt2",
            "MapExtPDX",
            "EconomyEX",
        };

        /// <summary><c>CellMapSystemPatchManager</c> 自动扫描修补 <c>kMapSize</c>/<c>kTextureSize</c> 读取指令的允许命名空间前缀。</summary>
        public static readonly string[] CellMapFieldPatchNamespaces =
        {
            "Game.Simulation",
            "Game.Debug",
            "Game.UI.Tooltip",
            "Game.Tools",
        };

        /// <summary>12 个被替换的 <c>CellMapSystem&lt;TCell&gt;</c> 对应的网格单元结构完整类型名（首项为全模式必替的 <c>LandValueCell</c>）。</summary>
        public static readonly string[] ReplacedCellMapCellTypes =
        {
            LandValueCellTypeName,
            "Game.Simulation.AirPollution",
            "Game.Simulation.AvailabilityInfoCell",
            "Game.Simulation.GroundPollution",
            "Game.Simulation.GroundWater",
            "Game.Simulation.NaturalResourceCell",
            "Game.Simulation.NoisePollution",
            "Game.Simulation.PopulationCell",
            "Game.Simulation.SoilWater",
            "Game.Simulation.TerrainAttractiveness",
            "Game.Simulation.TrafficAmbienceCell",
            "Game.Simulation.ZoneAmbienceCell",
        };

        #endregion

        #region Protected Systems & Hosts

        /// <summary>受保护的原版系统与 ReBurst 宿主完整清单。</summary>
        public static readonly ProtectedSystemTarget[] ProtectedSystems =
        {
            // === 1. 全模式必替 CellMap 系统（1 个） ===
            new ProtectedSystemTarget("Game.Simulation.LandValueSystem", ProtectedSystemRole.AlwaysReplacedCellMap, GroupCellMap),

            // === 2. 仅扩展模式（ModeA–D）替换的 CellMap 系统（11 个） ===
            new ProtectedSystemTarget("Game.Simulation.AirPollutionSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.AvailabilityInfoToGridSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.GroundPollutionSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.GroundWaterSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.NaturalResourceSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.NoisePollutionSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.PopulationToGridSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.SoilWaterSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.TerrainAttractivenessSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.TrafficAmbienceSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.ZoneAmbienceSystem", ProtectedSystemRole.ExpandedOnlyCellMap, GroupCellMap),

            // === 3. 仅扩展模式（ModeA–D）模拟核心 CellMap ReBurst 宿主（18 个，含嵌套类 CarNavigationSystem+Actions） ===
            new ProtectedSystemTarget("Game.Simulation.TelecomCoverageSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.WindSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.WindSimulationSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.AttractionSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.GroundWaterPollutionSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.ObjectPolluteSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.ZoneSpawnSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.CitizenHappinessSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.PollutionTriggerSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.PowerPlantAISystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.WaterPumpingStationAISystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.FloodCheckSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.WaterDangerSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.WaterLevelChangeSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Rendering.NetColorSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Areas.MapTileSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.SpawnableAmbienceSystem", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Simulation.CarNavigationSystem+Actions", ProtectedSystemRole.ExpandedOnlyCellMapCoreHost, GroupCellMap),

            // === 4. 仅扩展模式（ModeA–D）工具／界面／音频宿主（9 个：常态 Enabled=false，不参与静态 Enabled 检测与轮询） ===
            new ProtectedSystemTarget("Game.Tools.AreaToolSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Tools.ValidationSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Tools.TelecomPreviewSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.UI.Tooltip.LandValueTooltipSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.UI.Tooltip.TempWaterPumpingTooltipSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.UI.InGame.AverageHappinessSection", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.UI.InGame.PollutionInfoviewUISystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Audio.AudioGroupingSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),
            new ProtectedSystemTarget("Game.Audio.WeatherAudioSystem", ProtectedSystemRole.ExpandedOnlyCellMapAuxHost, GroupCellMap),

            // === 5. CellOnly 双重归属系统（2 个：ModeA–D 下硬冲突无一键关组，None 下受 EnableHouseholdPropertyEcoSystem 控制） ===
            new ProtectedSystemTarget("Game.Simulation.HouseholdFindPropertySystem", ProtectedSystemRole.CellOnlyDualRole, GroupHouseholdProperty, KeyHouseholdProperty),
            new ProtectedSystemTarget("Game.Simulation.RentAdjustSystem", ProtectedSystemRole.CellOnlyDualRole, GroupHouseholdProperty, KeyHouseholdProperty),

            // === 6. 纯 Shared 经济替换系统与禁用项（全模式可通过关闭对应经济分组开关规避） ===
            new ProtectedSystemTarget("Game.Simulation.HouseholdBehaviorSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupHouseholdProperty, KeyHouseholdProperty),
            new ProtectedSystemTarget("Game.Simulation.CitizenFindJobSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupJobSearch, KeyJobSearch),
            new ProtectedSystemTarget("Game.Simulation.FindJobSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupJobSearch, KeyJobSearch),
            new ProtectedSystemTarget("Game.Simulation.TripNeededSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupResourceBuyer, KeyResourceBuyer),
            new ProtectedSystemTarget("Game.Simulation.ServiceCoverageSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupResourceBuyer, KeyResourceBuyer),
            new ProtectedSystemTarget("Game.Simulation.ResourceBuyerSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupResourceBuyer, KeyResourceBuyer),
            new ProtectedSystemTarget("Game.Simulation.ResidentAISystem", ProtectedSystemRole.SharedEconomyReplaced, GroupResidentAI, KeyResidentAI),
            new ProtectedSystemTarget("Game.Simulation.ResidentAISystem+Actions", ProtectedSystemRole.SharedEconomyReplaced, GroupResidentAI, KeyResidentAI),
            new ProtectedSystemTarget("Game.Simulation.PersonalCarAISystem", ProtectedSystemRole.SharedEconomyReplaced, GroupDownstreamAI, KeyDownstreamAI),
            new ProtectedSystemTarget("Game.Simulation.PersonalCarAISystem+Actions", ProtectedSystemRole.SharedEconomyReplaced, GroupDownstreamAI, KeyDownstreamAI),
            new ProtectedSystemTarget("Game.Simulation.TaxiAISystem", ProtectedSystemRole.SharedEconomyReplaced, GroupDownstreamAI, KeyDownstreamAI),
            new ProtectedSystemTarget("Game.Simulation.LeisureSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupDownstreamAI, KeyDownstreamAI),
            new ProtectedSystemTarget("Game.Simulation.FindSchoolSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupDownstreamAI, KeyDownstreamAI),
            new ProtectedSystemTarget("Game.Simulation.NetUpkeepSystem", ProtectedSystemRole.SharedEconomyReplaced, GroupEconomy, KeyEconomyFix),

            // === 7. 纯 Shared 需求 Job Transpiler 宿主（3 个） ===
            new ProtectedSystemTarget("Game.Simulation.ResidentialDemandSystem", ProtectedSystemRole.SharedDemandHost, GroupDemand, KeyDemand),
            new ProtectedSystemTarget("Game.Simulation.IndustrialDemandSystem", ProtectedSystemRole.SharedDemandHost, GroupDemand, KeyDemand),
            new ProtectedSystemTarget("Game.Simulation.CommercialDemandSystem", ProtectedSystemRole.SharedDemandHost, GroupDemand, KeyDemand),
        };

        #endregion

        #region Dual Harmony Patched Methods

        /// <summary>
        /// MapExt 显式声明的全部 Harmony 目标方法（不含 <c>CellMapSystemPatchManager</c> 按 <c>ldsfld</c> 自动扫描的客户端方法，
        /// 后者在自检中按补丁声明类与 <see cref="CellMapFieldPatchNamespaces"/> 单独归类校验）。
        /// </summary>
        public static readonly PatchedMethodTarget[] PatchedMethods = BuildPatchedMethods();

        private static PatchedMethodTarget[] BuildPatchedMethods()
        {
            var list = new List<PatchedMethodTarget>(128)
            {
                // === A. MapExtPDX_modes：VanillaRecipe 与 ExpandedRecipe 共挂（10 个目标方法） ===
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "IsValidHeightmapFormat", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "CreateDefaultHeightmap", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "ToR16", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.UI.Editor.TerrainPanelSystem", "DisplayHeightmapError", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.UI.Editor.TerrainPanelSystem", "OnLoadWorldHeightmap", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "InitializeTerrainData", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "OnSimulateGPU", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "TerrainWillChange", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.SnowSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "ApplyBrush", HarmonyOwnerModes, PatchTargetScope.AllModes),

                // === B. MapExtPDX_modes：仅 ExpandedRecipe 挂载（地形、水模拟、航线、28→57 类型路由） ===
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "FinalizeTerrainData", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "GetTerrainBounds", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "GetHeightData", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "UpdateGPUReadback", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "RenderCascades", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "DownSampleHeightMap", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.TerrainSystem", "GetCascadeTexture", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                new PatchedMethodTarget("Game.Simulation.WaterSystem", "InitTextures", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "get_MapSize", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "get_CellSize", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "get_WaveSpeed", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "get_BackdropCellSize", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "CalculateSourceMultiplier", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "InitBackdropTexture", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSystem", "HasWater", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                new PatchedMethodTarget("Game.Simulation.WaterSimulation", "ResetToLevel", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulation", "SourceStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulation", "EvaporateStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulation", "VelocityStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulation", "DepthStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                new PatchedMethodTarget("Game.Simulation.WaterSimulationLegacy", "SourceStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulationLegacy", "EvaporateStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulationLegacy", "VelocityStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulationLegacy", "DepthStep", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterSimulationLegacy", "BorderCircleIntersection", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                new PatchedMethodTarget("Game.Simulation.WaterLevelChangeSystem", "get_TsunamiEndDelay", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterLevelChangeSystem", "GetMinimumDelayAt", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                new PatchedMethodTarget(BaseDataReaderGenericTypeName, "GetReadbackBounds", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.SurfaceDataReader", "GetReadbackBounds", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.HeightDataReader", "GetReadbackBounds", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                new PatchedMethodTarget("Game.Net.AirwaySystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Colossal.Serialization.Entities.SystemSerializerLibrary", "Initialize", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                // CellMap 字段关键污染写入者
                new PatchedMethodTarget("Game.Simulation.BuildingPollutionAddSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.NetPollutionSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                // 27 个 CellMap ReBurst 宿主目标方法
                new PatchedMethodTarget("Game.Simulation.TelecomCoverageSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Tools.TelecomPreviewSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WindSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WindSimulationSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.AttractionSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Audio.AudioGroupingSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.CarNavigationSystem+Actions", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.GroundWaterPollutionSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.UI.Tooltip.LandValueTooltipSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.ObjectPolluteSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.SpawnableAmbienceSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.ZoneSpawnSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.CitizenHappinessSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.UI.InGame.AverageHappinessSection", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.UI.InGame.PollutionInfoviewUISystem", "PerformUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.PollutionTriggerSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Rendering.NetColorSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Tools.ValidationSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.PowerPlantAISystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterPumpingStationAISystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.UI.Tooltip.TempWaterPumpingTooltipSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Tools.AreaToolSystem", "UpdateDefinitions", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Areas.MapTileSystem", "LegacyGenerateMapTiles", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.FloodCheckSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterDangerSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Simulation.WaterLevelChangeSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),
                new PatchedMethodTarget("Game.Audio.WeatherAudioSystem", "OnUpdate", HarmonyOwnerModes, PatchTargetScope.ExpandedModesOnly),

                // === C. MapExtPDX_global：6 个全模式必挂存档入口闸门 ===
                new PatchedMethodTarget("Game.UI.Menu.MenuUISystem", "GetSaveInfo", HarmonyOwnerGlobal, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.UI.Menu.MenuUISystem", "SafeLoadGame", HarmonyOwnerGlobal, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.UI.Menu.MenuUISystem", "SafeContinueGame", HarmonyOwnerGlobal, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.UI.Menu.MenuUISystem", "SafeNewGame", HarmonyOwnerGlobal, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.UI.Menu.MenuUISystem", "SafeQuickLoad", HarmonyOwnerGlobal, PatchTargetScope.AllModes),
                new PatchedMethodTarget("Game.SceneFlow.GameManager", "Load", HarmonyOwnerGlobal, PatchTargetScope.AllModes),

                // === D. MapExtPDX_global：经济与寻路条件补丁 ===
                new PatchedMethodTarget("Game.Simulation.PathfindSetupSystem", "FindTargets", HarmonyOwnerGlobal, PatchTargetScope.FindTargets),
                new PatchedMethodTarget("Game.Simulation.ServiceCoverageSystem", "SetupPathfindMethods", HarmonyOwnerGlobal, PatchTargetScope.EconomyResourceBuyer),
                new PatchedMethodTarget("Game.Simulation.ResidentialDemandSystem", "OnUpdate", HarmonyOwnerGlobal, PatchTargetScope.EconomyDemand),
                new PatchedMethodTarget("Game.Simulation.IndustrialDemandSystem", "OnUpdate", HarmonyOwnerGlobal, PatchTargetScope.EconomyDemand),
                new PatchedMethodTarget("Game.Simulation.CommercialDemandSystem", "OnUpdate", HarmonyOwnerGlobal, PatchTargetScope.EconomyDemand),
            };

            // === E. MapExtPDX_global：12 个 CellMapSystem<TCell> 重定向前缀补丁（GetMap / GetData / AddReader / AddWriter） ===
            string[] redirectMethods = { "GetMap", "GetData", "AddReader", "AddWriter" };
            foreach (string cellType in ReplacedCellMapCellTypes)
            {
                PatchTargetScope scope = cellType == LandValueCellTypeName
                    ? PatchTargetScope.AllModes
                    : PatchTargetScope.ExpandedModesOnly;
                foreach (string method in redirectMethods)
                {
                    list.Add(new PatchedMethodTarget(CellMapGenericTypeName, method, HarmonyOwnerGlobal, scope, cellType));
                }
            }

            return list.ToArray();
        }

        #endregion

        #region Known Mod Compatibility & Runtime Auto-Disable Rules

        /// <summary>
        /// 按名称兜底的补充兼容性规则表（已瘦身为仅保留静态单点字节码无法单独完整表达的 2 项交集说明；
        /// 其余系统替换、宿主停用、<c>CellMapSystem.AddWriter</c>、动态补丁、跨阶段注册与写 <c>WaterSystem.WaterSimSpeed</c>
        /// 一律由字节码从已安装版本实时测定）。
        /// <c>RWH</c> 不停用受保护系统，只持续改写 MapExt 只读的建筑户数与经济参数，字节码测不出交集；
        /// 规则说明的是游戏内按名自动停用找房与租金系统组（见 <see cref="RuntimeAutoDisableTargets"/>）。
        /// </summary>
        public static readonly KnownModConflictRule[] KnownModRules =
        {
            new KnownModConflictRule(
                ruleId: "RWH",
                pdxModId: "87755",
                assemblyNames: new[] { "RWH", "RealisticWorkplacesAndHouseholds" },
                auditedVersion: "2.0",
                expandedModeSeverity: ConflictSeverity.Warning,
                vanillaModeSeverity: ConflictSeverity.Warning,
                mitigatingToggleKeys: new[] { KeyHouseholdProperty },
                canMitigateInExpandedModes: true),

            new KnownModConflictRule(
                ruleId: "RoadBuilderDualInstall",
                pdxModId: "87190",
                assemblyNames: new[] { "RoadBuilder" },
                auditedVersion: "0.8.0",
                expandedModeSeverity: ConflictSeverity.Warning,
                vanillaModeSeverity: ConflictSeverity.Warning,
                requireDualLocalAndPdxInstall: true),
        };

        /// <summary>
        /// 游戏启动期（<c>Mod.OnLoad</c>、替换系统注册前）自动停用重叠经济分组的已知模组，游戏内停哪些分组只看这张表
        /// （包含工具端已改由字节码实时测定的 <c>RealisticPathFinding</c>、<c>Time2Work</c> 与 <c>RealisticParking</c>）。
        /// 顺序即判定与日志顺序：同一分组只记在最先命中的模组名下。
        /// </summary>
        public static readonly RuntimeAutoDisableTarget[] RuntimeAutoDisableTargets =
        {
            new RuntimeAutoDisableTarget(
                targetId: "RealisticPathFinding",
                pdxModId: "121226",
                assemblyNames: new[] { "RealisticPathFinding" },
                autoDisableGroups: new[] { GroupResidentAI, GroupResourceBuyer }),

            new RuntimeAutoDisableTarget(
                targetId: "Time2Work",
                pdxModId: "77171",
                assemblyNames: new[] { "Time2Work", "NightShift" },
                autoDisableGroups: new[] { GroupDownstreamAI },
                overlapNote: "LeisureSystem"),

            new RuntimeAutoDisableTarget(
                targetId: "RealisticParking",
                pdxModId: "87313",
                assemblyNames: new[] { "RealisticParking" },
                autoDisableGroups: new[] { GroupDownstreamAI },
                overlapNote: "PersonalCarAI"),

            new RuntimeAutoDisableTarget(
                targetId: "RWH",
                pdxModId: "87755",
                assemblyNames: new[] { "RWH", "RealisticWorkplacesAndHouseholds" },
                autoDisableGroups: new[] { GroupHouseholdProperty },
                overlapNote: "BuildingPropertyData"),
        };

        #endregion

        #region Lookup Indexes & Evaluation Helpers

        public const string UpdateSystemTypeName = "Game.UpdateSystem";
        public const string UpdateGroupSystemTypeName = "Game.Simulation.UpdateGroupSystem";

        /// <summary>
        /// 原版 <c>WaterSystem.WaterSimSpeed</c>（自动属性）的声明类型与 setter 名，供工具端字节码检测外部写入。
        /// MapExt 在各模式、各水模拟档位都会写它（<c>PatchSet2WaterOpt</c> 的档位节流、暂停冻结与后置恢复），
        /// 定级与 <c>.coc</c> 设定无关：外部模组写同一属性时只作备注。该值只决定每帧推进的模拟步数，不写入存档。
        /// </summary>
        public const string WaterSystemTypeName = "Game.Simulation.WaterSystem";

        public const string WaterSimSpeedSetterName = "set_WaterSimSpeed";

        private static readonly Dictionary<string, ProtectedSystemTarget> s_SystemsByName = BuildSystemMap();
        private static readonly HashSet<string> s_SensitiveTypes = BuildSensitiveTypeSet();

        private static Dictionary<string, ProtectedSystemTarget> BuildSystemMap()
        {
            var map = new Dictionary<string, ProtectedSystemTarget>(ProtectedSystems.Length, StringComparer.Ordinal);
            foreach (ProtectedSystemTarget item in ProtectedSystems)
            {
                map[item.FullTypeName] = item;
            }

            return map;
        }

        private static HashSet<string> BuildSensitiveTypeSet()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProtectedSystemTarget sys in ProtectedSystems)
            {
                set.Add(sys.FullTypeName);
                int plus = sys.FullTypeName.IndexOf('+');
                if (plus > 0)
                {
                    set.Add(sys.FullTypeName.Substring(0, plus));
                }
            }

            foreach (PatchedMethodTarget patch in PatchedMethods)
            {
                set.Add(patch.TargetTypeName);
                int plus = patch.TargetTypeName.IndexOf('+');
                if (plus > 0)
                {
                    set.Add(patch.TargetTypeName.Substring(0, plus));
                }

                if (patch.GenericArgumentTypeName != null)
                {
                    set.Add(patch.GenericArgumentTypeName);
                }
            }

            set.Add("Unity.Entities.ComponentSystemBase");
            set.Add("Game.GameSystemBase");
            set.Add(UpdateSystemTypeName);
            set.Add(UpdateGroupSystemTypeName);
            return set;
        }

        /// <summary>第 2 级元数据粗筛所用的敏感类型名全集。</summary>
        public static IReadOnlyCollection<string> SensitiveTypeNames => s_SensitiveTypes;

        /// <summary>该字符串是否为受保护系统或补丁目标的完整类型名（供动态补丁字符串解析匹配）。</summary>
        public static bool IsTargetSystemOrPatchTypeName(string candidate)
        {
            if (string.IsNullOrEmpty(candidate) || candidate.IndexOf('.') <= 0)
            {
                return false;
            }

            if (candidate == "Unity.Entities.ComponentSystemBase" ||
                candidate == "Game.GameSystemBase" ||
                candidate == UpdateSystemTypeName ||
                candidate == UpdateGroupSystemTypeName)
            {
                return false;
            }

            return s_SensitiveTypes.Contains(candidate);
        }

        /// <summary>是否为本 Mod 或自动休眠的配套子 Mod <c>EconomyEX</c>。</summary>
        public static bool IsSelfOrCompanion(string? assemblyName, string? pdxModId)
        {
            if (!string.IsNullOrEmpty(pdxModId) &&
                (pdxModId == MapExtPdxModId || pdxModId == EconomyExPdxModId))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                foreach (string name in SelfAndCompanionAssemblies)
                {
                    if (string.Equals(assemblyName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>按完整类型名查找受保护系统或宿主定义。</summary>
        public static ProtectedSystemTarget? FindProtectedSystem(string fullTypeName) =>
            s_SystemsByName.TryGetValue(fullTypeName, out ProtectedSystemTarget? target) ? target : null;

        /// <summary>该类型是否属于静态 <c>set_Enabled</c> 篡改检测保护名单（已排除 9 个工具/UI/音频宿主与 <c>TerrainSystem</c>/<c>WaterSystem</c>/<c>AirwaySystem</c>）。</summary>
        public static bool IsProtectedEnabledTarget(string fullTypeName) =>
            s_SystemsByName.TryGetValue(fullTypeName, out ProtectedSystemTarget? target) && target.CheckStaticEnabledWrite;

        /// <summary>
        /// 判断某个被替换的原版系统在给定模式下是否计入游戏内 CRITICAL 严重项：仅限被 MapExt 替换的 12 个
        /// <c>CellMapSystem&lt;T&gt;</c>（全模式 <c>LandValueSystem</c> 与扩展模式下的其余 11 个），原版重新运行会造成网格尺寸错配。
        /// C1/D1 <c>CellOnly</c> 系统不计入：其原版读取的地图经重定向后不小于原版尺寸，取格函数有边界检查或钳制，
        /// 重新运行造成的是取值错位与重复结算，游戏内只记普通冲突与警告；工具端仍按无法靠关闭经济分组规避定为硬冲突。
        /// </summary>
        public static bool IsCriticalCellMapVanilla(string? fullTypeName, bool isExpandedMode)
        {
            if (string.IsNullOrEmpty(fullTypeName) ||
                !s_SystemsByName.TryGetValue(fullTypeName!, out ProtectedSystemTarget? target))
            {
                return false;
            }

            if (target.Role == ProtectedSystemRole.AlwaysReplacedCellMap)
            {
                return true;
            }

            return isExpandedMode && target.Role == ProtectedSystemRole.ExpandedOnlyCellMap;
        }

        /// <summary>
        /// 按 <c>(PatchMode, System)</c> 二维矩阵计算外部改写该系统 <c>Enabled</c> 时的冲突等级与可一键关闭开关。
        /// <list type="bullet">
        /// <item>模拟核心宿主（<c>ExpandedOnlyCellMapCoreHost</c>）被停用属局部功能偏差，扩展模式下定为 <c>Warning</c>（不给关组按钮、不拦截启动）；</item>
        /// <item><c>CellOnlyDualRole</c>（含 <c>RentAdjustSystem</c>）在扩展模式下保留 <c>Hard</c> 硬冲突；</item>
        /// <item>若对宿主仅赋常量 <c>Enabled = true</c>（<paramref name="constantEnabledValue"/> 为 true），对本就保持启用的宿主无害，返回 <c>None</c>。</item>
        /// </list>
        /// </summary>
        public static SystemConflictEvaluation EvaluateSystemConflict(
            ProtectedSystemTarget target,
            ConflictEvalSettings settings,
            bool? constantEnabledValue = null)
        {
            if (constantEnabledValue == true &&
                (target.Role == ProtectedSystemRole.ExpandedOnlyCellMapCoreHost ||
                 target.Role == ProtectedSystemRole.SharedDemandHost))
            {
                return new SystemConflictEvaluation(ConflictSeverity.None, null);
            }

            switch (target.Role)
            {
                case ProtectedSystemRole.AlwaysReplacedCellMap:
                    return new SystemConflictEvaluation(ConflictSeverity.Hard, null);

                case ProtectedSystemRole.ExpandedOnlyCellMap:
                    return settings.IsExpandedMode
                        ? new SystemConflictEvaluation(ConflictSeverity.Hard, null)
                        : new SystemConflictEvaluation(ConflictSeverity.None, null);

                case ProtectedSystemRole.ExpandedOnlyCellMapCoreHost:
                    return settings.IsExpandedMode
                        ? new SystemConflictEvaluation(ConflictSeverity.Warning, null)
                        : new SystemConflictEvaluation(ConflictSeverity.None, null);

                case ProtectedSystemRole.ExpandedOnlyCellMapAuxHost:
                    return new SystemConflictEvaluation(ConflictSeverity.None, null);

                case ProtectedSystemRole.CellOnlyDualRole:
                    // ModeA–D 下即使关闭经济组仍必注册 *_CellOnly 并禁用原版，保留硬冲突且严禁提供关组规避按钮
                    if (settings.IsExpandedMode)
                    {
                        return new SystemConflictEvaluation(ConflictSeverity.Hard, null);
                    }

                    return settings.IsToggleEffective(KeyHouseholdProperty)
                        ? new SystemConflictEvaluation(ConflictSeverity.Warning, KeyHouseholdProperty)
                        : new SystemConflictEvaluation(ConflictSeverity.Mitigated, null);

                case ProtectedSystemRole.SharedEconomyReplaced:
                case ProtectedSystemRole.SharedDemandHost:
                    if (target.MitigatingToggleKey == null)
                    {
                        return new SystemConflictEvaluation(ConflictSeverity.Warning, null);
                    }

                    if (!settings.IsToggleEffective(target.MitigatingToggleKey))
                    {
                        return new SystemConflictEvaluation(ConflictSeverity.Mitigated, null);
                    }

                    // NetUpkeepSystem 挂在总开关上：给提示但不提供一键关掉整个经济总开关的激进按钮
                    string? oneClickKey = target.MitigatingToggleKey == KeyEconomyFix ? null : target.MitigatingToggleKey;
                    return new SystemConflictEvaluation(ConflictSeverity.Warning, oneClickKey);

                default:
                    return new SystemConflictEvaluation(ConflictSeverity.None, null);
            }
        }

        /// <summary>评估外部模组对已被 MapExt 停用替换的原版系统挂载生命周期 Harmony 补丁（如 <c>ServiceCoverageSystem.OnUpdate</c>）时的二维定级。</summary>
        public static SystemConflictEvaluation EvaluateReplacedSystemPatch(
            ProtectedSystemTarget target,
            ConflictEvalSettings settings)
        {
            switch (target.Role)
            {
                case ProtectedSystemRole.AlwaysReplacedCellMap:
                    return new SystemConflictEvaluation(ConflictSeverity.Warning, null);

                case ProtectedSystemRole.ExpandedOnlyCellMap:
                    return settings.IsExpandedMode
                        ? new SystemConflictEvaluation(ConflictSeverity.Warning, null)
                        : new SystemConflictEvaluation(ConflictSeverity.None, null);

                case ProtectedSystemRole.CellOnlyDualRole:
                    if (settings.IsExpandedMode)
                    {
                        return new SystemConflictEvaluation(ConflictSeverity.Warning, null);
                    }

                    return settings.IsToggleEffective(KeyHouseholdProperty)
                        ? new SystemConflictEvaluation(ConflictSeverity.Warning, KeyHouseholdProperty)
                        : new SystemConflictEvaluation(ConflictSeverity.Mitigated, null);

                case ProtectedSystemRole.SharedEconomyReplaced:
                    if (target.MitigatingToggleKey == null)
                    {
                        return new SystemConflictEvaluation(ConflictSeverity.Warning, null);
                    }

                    if (!settings.IsToggleEffective(target.MitigatingToggleKey))
                    {
                        return new SystemConflictEvaluation(ConflictSeverity.Mitigated, null);
                    }

                    string? oneClickKey = target.MitigatingToggleKey == KeyEconomyFix ? null : target.MitigatingToggleKey;
                    return new SystemConflictEvaluation(ConflictSeverity.Warning, oneClickKey);

                default:
                    return new SystemConflictEvaluation(ConflictSeverity.None, null);
            }
        }

        /// <summary>查找外部 Harmony 补丁命中的本 Mod 目标方法（支持泛型闭合参数精确匹配或开放泛型回退）。</summary>
        public static PatchedMethodTarget? FindPatchedMethod(
            string targetTypeName,
            string methodName,
            string? genericArgumentTypeName = null)
        {
            PatchedMethodTarget? fallback = null;
            foreach (PatchedMethodTarget item in PatchedMethods)
            {
                if (item.TargetTypeName != targetTypeName || item.MethodName != methodName)
                {
                    continue;
                }

                if (item.GenericArgumentTypeName == null)
                {
                    return item;
                }

                if (genericArgumentTypeName != null && item.GenericArgumentTypeName == genericArgumentTypeName)
                {
                    return item;
                }

                // 若未提供泛型实参（例如外部补丁打在开放基类或仅解析出 CellMapSystem`1），优先保留全模式项作为保守匹配
                if (fallback == null || item.Scope == PatchTargetScope.AllModes)
                {
                    fallback = item;
                }
            }

            return fallback;
        }

        /// <summary>判断某个 Harmony 目标方法在给定 <c>.coc</c> 设定下是否处于活跃挂载范围，并返回重叠补丁定级。</summary>
        public static SystemConflictEvaluation EvaluatePatchOverlap(
            PatchedMethodTarget target,
            ConflictEvalSettings settings,
            bool isTranspilerOrBlockingPrefix)
        {
            bool active;
            string? mitigatingKey = null;

            switch (target.Scope)
            {
                case PatchTargetScope.AllModes:
                    active = true;
                    break;
                case PatchTargetScope.ExpandedModesOnly:
                    active = settings.IsExpandedMode;
                    break;
                case PatchTargetScope.EconomyDemand:
                    active = settings.IsToggleEffective(KeyDemand);
                    mitigatingKey = KeyDemand;
                    break;
                case PatchTargetScope.EconomyHouseholdProperty:
                    active = settings.IsToggleEffective(KeyHouseholdProperty);
                    mitigatingKey = settings.IsExpandedMode ? null : KeyHouseholdProperty;
                    break;
                case PatchTargetScope.EconomyResourceBuyer:
                    active = settings.IsToggleEffective(KeyResourceBuyer);
                    mitigatingKey = KeyResourceBuyer;
                    break;
                case PatchTargetScope.FindTargets:
                    if (settings.IsExpandedMode)
                    {
                        active = true;
                    }
                    else
                    {
                        active = settings.IsToggleEffective(KeyHouseholdProperty)
                                 || settings.IsToggleEffective(KeyResourceBuyer)
                                 || settings.IsToggleEffective(KeyDownstreamAI);
                    }
                    break;
                default:
                    active = false;
                    break;
            }

            if (!active)
            {
                // 非阻断补丁（Postfix / void Prefix / Finalizer）在 MapExt 未挂载目标补丁时完全无交集，不再标为已规避
                if (!isTranspilerOrBlockingPrefix)
                {
                    return new SystemConflictEvaluation(ConflictSeverity.None, null);
                }

                // 若因关闭对应经济组而未挂载，记为已规避；因 None 模式未挂扩展补丁则记为无交集
                bool isEcoScope = target.Scope == PatchTargetScope.EconomyDemand
                                  || target.Scope == PatchTargetScope.EconomyHouseholdProperty
                                  || target.Scope == PatchTargetScope.EconomyResourceBuyer
                                  || (target.Scope == PatchTargetScope.FindTargets && !settings.IsExpandedMode);
                return new SystemConflictEvaluation(isEcoScope ? ConflictSeverity.Mitigated : ConflictSeverity.None, null);
            }

            if (!isTranspilerOrBlockingPrefix)
            {
                return new SystemConflictEvaluation(ConflictSeverity.Info, null);
            }

            return new SystemConflictEvaluation(ConflictSeverity.Warning, mitigatingKey);
        }

        /// <summary>按 PDX Mod ID 或托管程序集名匹配已知冲突规则。</summary>
        public static KnownModConflictRule? MatchKnownRule(string? pdxModId, string? assemblyName)
        {
            if (!string.IsNullOrEmpty(pdxModId))
            {
                foreach (KnownModConflictRule rule in KnownModRules)
                {
                    if (rule.PdxModId == pdxModId)
                    {
                        return rule;
                    }

                    foreach (string alt in rule.AlternatePdxModIds)
                    {
                        if (alt == pdxModId)
                        {
                            return rule;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                foreach (KnownModConflictRule rule in KnownModRules)
                {
                    foreach (string asm in rule.AssemblyNames)
                    {
                        if (string.Equals(asm, assemblyName, StringComparison.OrdinalIgnoreCase))
                        {
                            return rule;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>按 PDX Mod ID 或托管程序集名匹配游戏启动期自动停用分组目标。</summary>
        public static RuntimeAutoDisableTarget? MatchRuntimeAutoDisableTarget(string? pdxModId, string? assemblyName)
        {
            if (!string.IsNullOrEmpty(pdxModId))
            {
                foreach (RuntimeAutoDisableTarget item in RuntimeAutoDisableTargets)
                {
                    if (item.PdxModId == pdxModId)
                    {
                        return item;
                    }
                }
            }

            if (!string.IsNullOrEmpty(assemblyName))
            {
                foreach (RuntimeAutoDisableTarget item in RuntimeAutoDisableTargets)
                {
                    foreach (string asm in item.AssemblyNames)
                    {
                        if (string.Equals(asm, assemblyName, StringComparison.OrdinalIgnoreCase))
                        {
                            return item;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 按启动期检测到的已知模组算出本会话要自动停用的经济分组：按 <see cref="RuntimeAutoDisableTargets"/> 的顺序，
        /// 只停设定里开着的分组，同一分组只停一次。
        /// </summary>
        /// <param name="detectedTargetIds">启动期指纹扫描命中的标识（与 <see cref="RuntimeAutoDisableTarget.TargetId"/> 比对，其他标识忽略）。</param>
        /// <param name="isGroupEnabled">按分组名返回该分组在设定里是否开着。</param>
        public static List<RuntimeAutoDisableDecision> PlanRuntimeAutoDisable(
            ICollection<string> detectedTargetIds,
            Func<string, bool> isGroupEnabled)
        {
            var decisions = new List<RuntimeAutoDisableDecision>();
            var plannedGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuntimeAutoDisableTarget target in RuntimeAutoDisableTargets)
            {
                if (!detectedTargetIds.Contains(target.TargetId))
                {
                    continue;
                }

                foreach (string group in target.AutoDisableGroups)
                {
                    if (isGroupEnabled(group) && plannedGroups.Add(group))
                    {
                        decisions.Add(new RuntimeAutoDisableDecision(group, target));
                    }
                }
            }

            return decisions;
        }

        /// <summary>按当前 <c>.coc</c> 设定评估补充规则的等级与可一键关闭开关。</summary>
        public static ConflictSeverity EvaluateKnownRule(
            KnownModConflictRule rule,
            ConflictEvalSettings settings,
            out string[] oneClickDisableKeys)
        {
            oneClickDisableKeys = Array.Empty<string>();

            ConflictSeverity baseSeverity = settings.IsExpandedMode
                ? rule.ExpandedModeSeverity
                : rule.VanillaModeSeverity;

            if (baseSeverity == ConflictSeverity.Hard || baseSeverity == ConflictSeverity.None || baseSeverity == ConflictSeverity.Info)
            {
                return baseSeverity;
            }

            if (rule.MitigatingToggleKeys.Length == 0)
            {
                return baseSeverity;
            }

            bool canMitigateHere = !settings.IsExpandedMode || rule.CanMitigateInExpandedModes;
            var activeKeys = new List<string>(rule.MitigatingToggleKeys.Length);
            foreach (string key in rule.MitigatingToggleKeys)
            {
                if (settings.IsToggleEffective(key))
                {
                    activeKeys.Add(key);
                }
            }

            if (!canMitigateHere)
            {
                return ConflictSeverity.Warning;
            }

            if (activeKeys.Count == 0)
            {
                return ConflictSeverity.Mitigated;
            }

            oneClickDisableKeys = activeKeys.ToArray();
            return ConflictSeverity.Warning;
        }

        /// <summary>比较两个点分版本号，判断 <paramref name="installedVersion"/> 是否严格高于 <paramref name="auditedVersion"/>。</summary>
        public static bool IsVersionNewerThanAudited(string? installedVersion, string? auditedVersion)
        {
            if (string.IsNullOrWhiteSpace(installedVersion) || string.IsNullOrWhiteSpace(auditedVersion))
            {
                return false;
            }

            int[]? installed = TryParseVersionParts(installedVersion!);
            int[]? audited = TryParseVersionParts(auditedVersion!);
            if (installed == null || audited == null)
            {
                return false;
            }

            int maxLen = Math.Max(installed.Length, audited.Length);
            for (int i = 0; i < maxLen; i++)
            {
                int a = i < installed.Length ? installed[i] : 0;
                int b = i < audited.Length ? audited[i] : 0;
                if (a != b)
                {
                    return a > b;
                }
            }

            return false;
        }

        private static int[]? TryParseVersionParts(string raw)
        {
            string trimmed = raw.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            // 截断 +build 或 -beta 后缀
            int suffixIdx = trimmed.IndexOfAny(new[] { '+', '-', ' ' });
            if (suffixIdx > 0)
            {
                trimmed = trimmed.Substring(0, suffixIdx);
            }

            string[] tokens = trimmed.Split('.');
            if (tokens.Length == 0)
            {
                return null;
            }

            var parts = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i], out int val) || val < 0)
                {
                    return null;
                }

                parts[i] = val;
            }

            return parts;
        }

        #endregion
    }
}
