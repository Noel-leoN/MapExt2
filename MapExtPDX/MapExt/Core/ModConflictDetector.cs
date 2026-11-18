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

        /// <summary>Water Features — 与非原版水模拟档位共用部分 WaterSystem 参数字段</summary>
        public static bool HasWaterFeatures { get; private set; }

        /// <summary>是否已完成扫描</summary>
        public static bool IsScanned { get; private set; }

        private static readonly List<string> s_DetectedSummaries = new List<string>();

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

                anyKnownDetected = true;
                RecordRuleHit(matchedId, modDisplay);
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

        private static void RecordRuleHit(string ruleId, string modDisplay)
        {
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
                case "WaterFeatures":
                    HasWaterFeatures = true;
                    AddSummaryUnique("WaterFeatures (WaterSystem params)");
                    ModLog.Warn(Tag, $"检测到潜在冲突 Mod: {modDisplay} (与非原版水模拟档位共用部分 WaterSystem 参数)");
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
        /// 无冲突返回 "None"。
        /// </summary>
        public static string GetConflictReport(ModSettings settings)
        {
            if (!IsScanned) return "Not scanned";

            var conflicts = new List<string>();
            bool isExpanded = settings.PatchModeChoice != PatchModeSetting.None;

            if (HasRealisticPathFinding)
            {
                conflicts.Add("RealisticPathFinding (ResidentAI / ResourceBuyer)");
            }

            if (HasTime2Work)
            {
                if (isExpanded)
                    conflicts.Add("Time2Work (RentAdjustSystem / AttractionSystem)");
                else if (settings.EnableDownstreamAIEcoSystem || settings.EnableHouseholdPropertyEcoSystem)
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

            if (HasWaterFeatures && settings.WaterSimQuality != WaterSimQualitySetting.Vanilla_EveryFrame)
            {
                conflicts.Add("WaterFeatures (WaterSystem params)");
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
        /// 严格保留 <c>"Auto-disabled "</c> 日志前缀。
        /// </summary>
        public static List<string> AutoDisableConflictGroups(ModSettings settings)
        {
            if (!IsScanned) return new List<string>();

            var disabled = new List<string>();
            var reasons = new List<string>();

            // RPF 替换 ResidentAISystem + ResourceBuyerSystem
            if (HasRealisticPathFinding)
            {
                if (settings.EnableResidentAIEcoSystem)
                {
                    disabled.Add(AppliedConfig.GroupResidentAI);
                    reasons.Add("ResidentAI (RealisticPathFinding)");
                    ModLog.Warn(Tag, "Auto-disabled ResidentAI group (conflict: RealisticPathFinding)");
                }
                if (settings.EnableResourceBuyerEcoSystem)
                {
                    disabled.Add(AppliedConfig.GroupResourceBuyer);
                    reasons.Add("ResourceBuyer (RealisticPathFinding)");
                    ModLog.Warn(Tag, "Auto-disabled ResourceBuyer group (conflict: RealisticPathFinding)");
                }
            }

            // Time2Work 替换 LeisureSystem（属于 DownstreamAI 组）
            if (HasTime2Work && settings.EnableDownstreamAIEcoSystem)
            {
                disabled.Add(AppliedConfig.GroupDownstreamAI);
                reasons.Add("DownstreamAI (Time2Work)");
                ModLog.Warn(Tag, "Auto-disabled DownstreamAI group (conflict: Time2Work -> LeisureSystem)");
            }

            // RealisticParking 替换 PersonalCarAISystem（也属于 DownstreamAI 组）
            if (HasRealisticParking && settings.EnableDownstreamAIEcoSystem &&
                !disabled.Contains(AppliedConfig.GroupDownstreamAI))
            {
                disabled.Add(AppliedConfig.GroupDownstreamAI);
                reasons.Add("DownstreamAI (RealisticParking)");
                ModLog.Warn(Tag, "Auto-disabled DownstreamAI group (conflict: RealisticParking -> PersonalCarAI)");
            }

            // RWH 修改 BuildingPropertyData（影响 HouseholdProperty 组）
            if (HasRWH && settings.EnableHouseholdPropertyEcoSystem)
            {
                disabled.Add(AppliedConfig.GroupHouseholdProperty);
                reasons.Add("HouseholdProperty (RWH)");
                ModLog.Warn(Tag, "Auto-disabled HouseholdProperty group (conflict: RWH -> BuildingPropertyData)");
            }

            if (disabled.Count > 0)
            {
                bool Active(bool enabledInSettings, string group) => enabledInSettings && !disabled.Contains(group);

                var activeGroups = new List<string>();
                if (Active(settings.EnableDemandEcoSystem, AppliedConfig.GroupDemand)) activeGroups.Add("Demand");
                if (Active(settings.EnableJobSearchEcoSystem, AppliedConfig.GroupJobSearch)) activeGroups.Add("JobSearch");
                if (Active(settings.EnableHouseholdPropertyEcoSystem, AppliedConfig.GroupHouseholdProperty)) activeGroups.Add("HouseholdProperty");
                if (Active(settings.EnableResourceBuyerEcoSystem, AppliedConfig.GroupResourceBuyer)) activeGroups.Add("ResourceBuyer");
                if (Active(settings.EnableResidentAIEcoSystem, AppliedConfig.GroupResidentAI)) activeGroups.Add("ResidentAI");
                if (Active(settings.EnableDownstreamAIEcoSystem, AppliedConfig.GroupDownstreamAI)) activeGroups.Add("DownstreamAI");

                settings._systemStatusTooltip =
                    $"Disabled: {string.Join(", ", reasons)}\n" +
                    $"Active: {(activeGroups.Count > 0 ? string.Join(", ", activeGroups) : "None")}";

                ModLog.Warn(Tag, $"共自动禁用 {disabled.Count} 个系统组: {string.Join(", ", disabled)}");
            }
            else
            {
                settings._systemStatusTooltip = "All economy system groups active. No conflicts.";
            }

            return disabled;
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
