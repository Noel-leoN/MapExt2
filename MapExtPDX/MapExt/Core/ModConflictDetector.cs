// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.SceneFlow;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 启动时 Mod 指纹检测器与已加载代码 Mod 查询入口。
    ///
    /// <para><b>两阶段枚举过滤分离约束</b>：
    /// <list type="number">
    /// <item><see cref="ScanLoadedMods"/> 在 <c>Mod.OnLoad</c> 阶段运行，此时排在本地 <c>Mods/MapExt2</c> 后面的全部 PDX 订阅 Mod
    /// 尚处于 <c>State.Unknown</c>，故只过滤 <c>isValid &amp;&amp; asset.isMod &amp;&amp; asset.isUnique &amp;&amp; asset.canBeLoaded</c>，
    /// <b>严禁附加 <c>state == Loaded</c></b>；</item>
    /// <item><see cref="GetLoadedModNames"/> 供进城后冲突弹窗兜底展示，在弹窗触发时<b>实时枚举</b>
    /// <c>state == Loaded &amp;&amp; asset.isMod</c>，既涵盖全部订阅 Mod，又剔除随包引用类库（<c>!isMod</c>）与加载失败项噪音。</item>
    /// </list></para>
    /// </summary>
    public static class ModConflictDetector
    {
        private const string Tag = nameof(ModConflictDetector);

        // === 检测结果缓存（仅收录与 MapExt 存在系统替换或共享参数交集的模组） ===

        /// <summary>Realistic Workplaces and Households — 与找房与租金系统组的输入参数存在交集</summary>
        public static bool HasRWH { get; private set; }

        /// <summary>Realistic PathFinding — 与居民 AI 及消费采购寻路系统组替换同一批原版系统</summary>
        public static bool HasRealisticPathFinding { get; private set; }

        /// <summary>Time2Work (Realistic Trips) — 与休闲、吸引力及租金调整系统存在运行交集</summary>
        public static bool HasTime2Work { get; private set; }

        /// <summary>Realistic Parking — 与下游 AI 组的 PersonalCarAISystem 替换存在交集</summary>
        public static bool HasRealisticParking { get; private set; }

        /// <summary>是否已完成扫描</summary>
        public static bool IsScanned { get; private set; }

        private static readonly List<string> s_DetectedSummaries = new List<string>();

        /// <summary>启动期命中的规则与自动停用目标标识，供 <see cref="AutoDisableConflictGroups"/> 查共用清单。</summary>
        private static readonly HashSet<string> s_DetectedRuleIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>设定页工具提示列出仍启用的经济分组时的顺序。</summary>
        private static readonly string[] s_EconomyGroupOrder =
        {
            AppliedConfig.GroupDemand,
            AppliedConfig.GroupJobSearch,
            AppliedConfig.GroupHouseholdProperty,
            AppliedConfig.GroupResourceBuyer,
            AppliedConfig.GroupResidentAI,
            AppliedConfig.GroupDownstreamAI,
        };

        // === 扫描入口 ===

        /// <summary>
        /// 在 <c>Mod.OnLoad</c> 阶段调用，通过 <c>modManager</c> 匹配存在系统或参数交集的已知模组指纹。
        /// 仅执行一次；保留固定日志前缀以向后兼容外部工具 <c>MapExtLogSummary</c>。
        /// </summary>
        public static void ScanLoadedMods()
        {
            if (IsScanned) return;

            ModManager mm = GameManager.instance?.modManager;
            if (mm == null)
            {
                IsScanned = true;
                return;
            }

            bool anyKnownDetected = false;

            foreach (ModManager.ModInfo modInfo in mm)
            {
                // 启动期过滤谓词：剔除无效项、非代码主入口的随包引用库、同名败选副本与缺前置死 Mod；严禁检查 state == Loaded！
                if (!modInfo.isValid) continue;
                ExecutableAsset asset = modInfo.asset;
                if (asset == null || !asset.isMod || !asset.isUnique || !asset.canBeLoaded) continue;

                string asmName = asset.name;
                if (string.IsNullOrEmpty(asmName)) continue;

                string pdxId = null;
                try
                {
                    SourceMeta meta = asset.GetMeta();
                    pdxId = meta.platformID;
                }
                catch
                {
                    // 忽略元数据读取异常
                }

                if (ConflictTargets.IsSelfOrCompanion(asmName, pdxId)) continue;

                string modDisplay = ModAttributionHelper.FormatExecutableAsset(asset);
                string matchedId = MatchStartupFingerprintId(pdxId, asmName);
                if (matchedId == null) continue;

                RecordRuleHit(matchedId, modDisplay);
                anyKnownDetected = true;
            }

            IsScanned = true;

            if (!anyKnownDetected)
            {
                ModLog.Ok(Tag, "未检测到已知冲突 Mod");
            }
        }

        private static string MatchStartupFingerprintId(string pdxId, string asmName)
        {
            RuntimeAutoDisableTarget autoTarget = ConflictTargets.MatchRuntimeAutoDisableTarget(pdxId, asmName);
            if (autoTarget != null)
                return autoTarget.TargetId;

            KnownModConflictRule knownRule = ConflictTargets.MatchKnownRule(pdxId, asmName);
            if (knownRule != null && !knownRule.RequireDualLocalAndPdxInstall)
                return knownRule.RuleId;

            // 兼容旧版子串匹配兜底
            if (asmName.Contains("RealisticWorkplacesAndHouseholds"))
                return "RWH";
            if (asmName.Contains("RealisticPathFinding"))
                return "RealisticPathFinding";
            if (asmName.Contains("Time2Work"))
                return "Time2Work";
            if (asmName.Contains("RealisticParking"))
                return "RealisticParking";

            return null;
        }

        /// <summary>记录一次规则命中并写日志。</summary>
        private static void RecordRuleHit(string ruleId, string modDisplay)
        {
            s_DetectedRuleIds.Add(ruleId);

            switch (ruleId)
            {
                case "RWH":
                    HasRWH = true;
                    AddSummaryUnique("RWH (HouseholdProperty)");
                    ModLog.Warn(Tag, $"检测到潜在冲突 Mod: {modDisplay} (与找房与租金系统组共用建筑与经济参数)");
                    break;
                case "RealisticPathFinding":
                    HasRealisticPathFinding = true;
                    AddSummaryUnique("RealisticPathFinding (ResidentAI, ResourceBuyer)");
                    ModLog.Warn(Tag, $"检测到冲突 Mod: {modDisplay} (与居民 AI 及消费采购系统组替换相同原版系统)");
                    break;
                case "Time2Work":
                    HasTime2Work = true;
                    AddSummaryUnique("Time2Work (LeisureSystem, RentAdjustSystem)");
                    ModLog.Warn(Tag, $"检测到潜在冲突 Mod: {modDisplay} (与休闲、吸引力及租金调整系统存在交集)");
                    break;
                case "RealisticParking":
                    HasRealisticParking = true;
                    AddSummaryUnique("RealisticParking (PersonalCarAISystem)");
                    ModLog.Warn(Tag, $"检测到潜在冲突 Mod: {modDisplay} (与下游 AI 组同样替换 PersonalCarAISystem)");
                    break;
            }
        }

        private static void AddSummaryUnique(string item)
        {
            if (!s_DetectedSummaries.Contains(item))
                s_DetectedSummaries.Add(item);
        }

        // === 冲突报告生成 ===

        /// <summary>
        /// 返回与 MapExt 当前启用的系统组或模式存在直接交集的已知 Mod 列表（仅陈述 MapExt 一侧交集事实）。
        /// 无冲突返回 "None"。经济分组只在经济修复总开关也开着时才算启用（总开关关闭时共用经济系统一个都不注册）。
        /// </summary>
        public static string GetConflictReport(ModSettings settings)
        {
            if (!IsScanned) return "Not scanned";

            var conflicts = new List<string>();
            bool isExpanded = settings.PatchModeChoice != PatchModeSetting.None;
            bool economyFix = settings.isEnableEconomyFix;

            // 交集只在居民 AI 与消费采购两组替换的 4 个原版系统
            if (HasRealisticPathFinding && economyFix &&
                (settings.EnableResidentAIEcoSystem || settings.EnableResourceBuyerEcoSystem))
            {
                conflicts.Add("RealisticPathFinding (ResidentAI / ResourceBuyer)");
            }

            if (HasTime2Work)
            {
                // 扩展模式下 RentAdjustSystem 必替（CellOnly），与分组无关；原版尺寸下只随下游 AI 与找房两组出现
                if (isExpanded)
                    conflicts.Add("Time2Work (RentAdjustSystem / AttractionSystem)");
                else if (economyFix && (settings.EnableDownstreamAIEcoSystem || settings.EnableHouseholdPropertyEcoSystem))
                    conflicts.Add("Time2Work (LeisureSystem / RentAdjustSystem)");
            }

            if (HasRealisticParking && settings.isEnableEconomyFix && settings.EnableDownstreamAIEcoSystem)
            {
                conflicts.Add("RealisticParking (PersonalCarAISystem)");
            }

            if (HasRWH && settings.isEnableEconomyFix && settings.EnableHouseholdPropertyEcoSystem)
            {
                conflicts.Add("RWH (HouseholdProperty)");
            }

            return conflicts.Count > 0 ? string.Join(", ", conflicts) : "None";
        }

        /// <summary>供 OptionUI 的 DetectedConflictMods 字段显示。</summary>
        public static string GetDetectedModsSummary()
        {
            if (!IsScanned) return "Not scanned";
            return s_DetectedSummaries.Count > 0 ? string.Join("; ", s_DetectedSummaries) : "None";
        }

        // === 启动时自动禁用冲突系统组 ===

        /// <summary>
        /// 在 <c>Mod.OnLoad</c> 阶段、<c>SystemReplacer.Apply</c> 之前调用。
        /// 根据已检测到的冲突 Mod 指纹，算出本会话要自动停用的经济分组，交给 <see cref="AppliedConfig.Capture"/>。
        /// 停哪些分组只看共用清单 <see cref="ConflictTargets.RuntimeAutoDisableTargets"/>，此处不另写规则；
        /// 只停本会话会生效的分组（见 <see cref="IsGroupEffective"/>），经济修复总开关关闭时不停用、不推通知。
        /// 严格保留 <c>"Auto-disabled "</c> 日志前缀。
        /// </summary>
        public static List<string> AutoDisableConflictGroups(ModSettings settings)
        {
            if (!IsScanned) return new List<string>();

            var disabled = new List<string>();
            var reasons = new List<string>();

            List<RuntimeAutoDisableDecision> decisions = ConflictTargets.PlanRuntimeAutoDisable(
                s_DetectedRuleIds,
                group => IsGroupEffective(settings, group));
            foreach (RuntimeAutoDisableDecision decision in decisions)
            {
                RuntimeAutoDisableTarget target = decision.Target;
                disabled.Add(decision.Group);
                reasons.Add($"{decision.Group} ({target.TargetId})");

                string conflict = target.OverlapNote == null
                    ? target.TargetId
                    : $"{target.TargetId} -> {target.OverlapNote}";
                ModLog.Warn(Tag, $"Auto-disabled {decision.Group} group (conflict: {conflict})");
            }

            if (disabled.Count > 0)
            {
                var activeGroups = new List<string>();
                foreach (string group in s_EconomyGroupOrder)
                {
                    if (IsGroupEffective(settings, group) && !disabled.Contains(group))
                        activeGroups.Add(group);
                }

                settings._systemStatusTooltip =
                    $"Disabled: {string.Join(", ", reasons)}\n" +
                    $"Active: {(activeGroups.Count > 0 ? string.Join(", ", activeGroups) : "None")}";

                ModLog.Warn(Tag, $"共自动禁用 {disabled.Count} 个系统组: {string.Join(", ", disabled)}");
            }
            else if (!settings.isEnableEconomyFix)
            {
                settings._systemStatusTooltip = "Economy fix is off. No economy system groups are active.";
            }
            else
            {
                settings._systemStatusTooltip = "All economy system groups active. No conflicts.";
            }

            return disabled;
        }

        /// <summary>
        /// 经济分组在本会话是否会生效：经济修复总开关与分组自身的开关都开着。
        /// 总开关关闭时 <c>SystemReplacer</c> 不注册任何共用经济系统，此时停用分组或报冲突都只是误报。
        /// </summary>
        private static bool IsGroupEffective(ModSettings settings, string group)
        {
            if (!settings.isEnableEconomyFix)
                return false;

            switch (group)
            {
                case AppliedConfig.GroupDemand: return settings.EnableDemandEcoSystem;
                case AppliedConfig.GroupJobSearch: return settings.EnableJobSearchEcoSystem;
                case AppliedConfig.GroupHouseholdProperty: return settings.EnableHouseholdPropertyEcoSystem;
                case AppliedConfig.GroupResourceBuyer: return settings.EnableResourceBuyerEcoSystem;
                case AppliedConfig.GroupResidentAI: return settings.EnableResidentAIEcoSystem;
                case AppliedConfig.GroupDownstreamAI: return settings.EnableDownstreamAIEcoSystem;
                default: return false;
            }
        }

        /// <summary>
        /// 进城弹窗兜底列表：<b>在调用时实时枚举</b> <c>state == Loaded &amp;&amp; asset.isMod</c> 的非本工程代码 Mod 显示名，
        /// 彻底排除 <c>OnLoad</c> 早期的 <c>State.Unknown</c> 截断与随包辅助类库（<c>!isMod</c>）噪音。
        /// </summary>
        public static string GetLoadedModNames()
        {
            try
            {
                ModManager mm = GameManager.instance?.modManager;
                if (mm == null) return "(none)";

                var names = new List<string>();
                foreach (ModManager.ModInfo modInfo in mm)
                {
                    if (!modInfo.isValid || modInfo.state != ModManager.ModInfo.State.Loaded) continue;
                    ExecutableAsset asset = modInfo.asset;
                    if (asset == null || !asset.isMod) continue;

                    string pdxId = null;
                    try { pdxId = asset.GetMeta().platformID; }
                    catch { /* ignore */ }

                    if (ConflictTargets.IsSelfOrCompanion(asset.name, pdxId)) continue;

                    string formatted = ModAttributionHelper.FormatExecutableAsset(asset);
                    if (!names.Contains(formatted))
                        names.Add(formatted);
                }

                names.Sort(StringComparer.OrdinalIgnoreCase);
                return names.Count > 0 ? string.Join(", ", names) : "(none)";
            }
            catch
            {
                return "(none)";
            }
        }

        /// <summary>向后兼容别名，转发至实时枚举 <see cref="GetLoadedModNames"/>。</summary>
        public static string GetAllModNames() => GetLoadedModNames();
    }
}
