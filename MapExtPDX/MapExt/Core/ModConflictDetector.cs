// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using Game.SceneFlow;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 启动时 Mod 指纹检测器。
    /// 在 OnLoad 阶段一次性扫描已加载 Mod 列表，识别已知冲突 Mod 的存在。
    /// 检测结果缓存为静态属性，整个游戏生命周期零运行时开销。
    /// </summary>
    public static class ModConflictDetector
    {
        private const string Tag = "ConflictDetector";

        // === 检测结果缓存 ===

        /// <summary>Realistic Workplaces and Households — Prefab 数据篡改风险</summary>
        public static bool HasRWH { get; private set; }

        /// <summary>Realistic PathFinding — ResidentAI/TripNeeded/ResourceBuyer 系统替换冲突</summary>
        public static bool HasRealisticPathFinding { get; private set; }

        /// <summary>Time2Work — LeisureSystem 系统替换冲突</summary>
        public static bool HasTime2Work { get; private set; }

        /// <summary>Realistic Parking — PersonalCarAISystem 系统替换冲突</summary>
        public static bool HasRealisticParking { get; private set; }

        /// <summary>UrbanInequality — EconomyParameterData 修改风险</summary>
        public static bool HasUrbanInequality { get; private set; }

        /// <summary>是否已完成扫描</summary>
        public static bool IsScanned { get; private set; }

        /// <summary>所有已加载的非官方 Mod 名称（供 CellMap 冲突弹窗展示）</summary>
        private static readonly List<string> AllLoadedModNames = new List<string>();

        // === 扫描入口 ===

        /// <summary>
        /// 在 Mod.OnLoad 阶段调用，通过 modManager 名称匹配检测冲突 Mod。
        /// 仅执行一次，结果缓存到静态属性。
        /// </summary>
        public static void ScanLoadedMods()
        {
            if (IsScanned) return;

            foreach (var modInfo in GameManager.instance.modManager)
            {
                var name = modInfo.asset.name;
                if (string.IsNullOrEmpty(name)) continue;

                // 记录所有非 MapExt 自身的 Mod 名称（排除官方、自身和 Harmony 库）
                if (!name.Contains("MapExt") && !name.Contains("EconomyEX")
                    && !name.StartsWith("0Harmony") && !name.StartsWith("Lib.Harmony")
                    && !AllLoadedModNames.Contains(name))
                    AllLoadedModNames.Add(name);

                // RWH: 多种可能的 asset 名称
                if (name.Contains("RealisticWorkplacesAndHouseholds") || name == "RWH")
                {
                    HasRWH = true;
                    ModLog.Warn(Tag, $"检测到冲突 Mod: {name} (BuildingPropertyData 篡改风险)");
                }
                else if (name.Contains("RealisticPathFinding"))
                {
                    HasRealisticPathFinding = true;
                    ModLog.Warn(Tag, $"检测到冲突 Mod: {name} (ResidentAI/ResourceBuyer 系统替换)");
                }
                else if (name.Contains("Time2Work"))
                {
                    HasTime2Work = true;
                    ModLog.Warn(Tag, $"检测到冲突 Mod: {name} (LeisureSystem 系统替换)");
                }
                else if (name.Contains("RealisticParking"))
                {
                    HasRealisticParking = true;
                    ModLog.Warn(Tag, $"检测到冲突 Mod: {name} (PersonalCarAISystem 系统替换)");
                }
                else if (name.Contains("UrbanInequality"))
                {
                    HasUrbanInequality = true;
                    ModLog.Warn(Tag, $"检测到潜在冲突 Mod: {name} (EconomyParameterData 修改)");
                }
            }

            IsScanned = true;

            if (!HasRWH && !HasRealisticPathFinding && !HasTime2Work &&
                !HasRealisticParking && !HasUrbanInequality)
            {
                ModLog.Ok(Tag, "未检测到已知冲突 Mod");
            }
        }

        // === 冲突报告生成 ===

        /// <summary>
        /// 返回与 MapExt 当前启用的系统组存在直接冲突的 Mod 列表。
        /// 格式: "ModName (冲突维度)"，多个以 ", " 分隔。
        /// 无冲突返回 "None"。
        /// </summary>
        public static string GetConflictReport(ModSettings settings)
        {
            if (!IsScanned) return "Not scanned";

            var conflicts = new List<string>();

            if (HasRealisticPathFinding)
            {
                if (settings.EnableResidentAIEcoSystem)
                    conflicts.Add("RealisticPathFinding (ResidentAI)");
                if (settings.EnableResourceBuyerEcoSystem)
                    conflicts.Add("RealisticPathFinding (ResourceBuyer)");
            }

            if (HasTime2Work && settings.EnableDownstreamAIEcoSystem)
                conflicts.Add("Time2Work (LeisureSystem)");

            if (HasRealisticParking && settings.EnableDownstreamAIEcoSystem)
                conflicts.Add("RealisticParking (PersonalCarAI)");

            if (HasRWH && settings.EnableHouseholdPropertyEcoSystem)
                conflicts.Add("RWH (BuildingPropertyData)");

            // EconomyParameterData 多写冲突检测
            int ecoParamWriters = 0;
            var ecoParamMods = new List<string>();
            if (HasRWH) { ecoParamWriters++; ecoParamMods.Add("RWH"); }
            if (HasUrbanInequality) { ecoParamWriters++; ecoParamMods.Add("UrbanInequality"); }
            if (HasTime2Work) { ecoParamWriters++; ecoParamMods.Add("Time2Work"); }
            if (ecoParamWriters >= 2)
            {
                conflicts.Add($"EconomyParameterData ({string.Join("+", ecoParamMods)})");
            }
            else if (HasUrbanInequality && settings.isEnableEconomyFix)
            {
                // 单独的 UrbanInequality 也修改 EconomyParameterData
                conflicts.Add("UrbanInequality (EconomyParameterData)");
            }

            return conflicts.Count > 0 ? string.Join(", ", conflicts) : "None";
        }

        /// <summary>
        /// 返回所有已检测到的 ruzbeh0 系列 Mod 列表（无论是否与当前设置冲突）。
        /// 供 OptionUI 的 DetectedConflictMods 字段显示。
        /// </summary>
        public static string GetDetectedModsSummary()
        {
            if (!IsScanned) return "Not scanned";

            var detected = new List<string>();

            if (HasRWH)
                detected.Add("RWH (data: BuildingPropertyData)");
            if (HasRealisticPathFinding)
                detected.Add("RealisticPathFinding (sys: ResidentAI, ResourceBuyer)");
            if (HasTime2Work)
                detected.Add("Time2Work (sys: LeisureSystem)");
            if (HasRealisticParking)
                detected.Add("RealisticParking (sys: PersonalCarAI)");
            if (HasUrbanInequality)
                detected.Add("UrbanInequality (data: EconomyParameterData)");

            return detected.Count > 0 ? string.Join("; ", detected) : "None";
        }

        // === 启动时自动禁用冲突系统组 ===

        /// <summary>
        /// 在 Mod.OnLoad 阶段、SystemReplacer.Apply 之前调用。
        /// 根据已检测到的冲突 Mod 指纹，自动禁用对应的 Economy 系统组设置。
        /// 这确保 SystemReplacer.Apply 不会注册与其他 Mod 冲突的系统替换。
        /// 返回被禁用的组名列表（用于 UI 提示）。
        /// 同时填充 settings._systemStatusTooltip 供 InGame UI 悬停显示。
        /// </summary>
        public static List<string> AutoDisableConflictGroups(ModSettings settings)
        {
            if (!IsScanned) return new List<string>();

            var disabled = new List<string>(); // 组名
            var reasons = new List<string>();   // “组名 (conflict: ModName)”

            // RPF 替换 ResidentAISystem + ResourceBuyerSystem
            if (HasRealisticPathFinding)
            {
                if (settings.EnableResidentAIEcoSystem)
                {
                    settings.EnableResidentAIEcoSystem = false;
                    disabled.Add("ResidentAI");
                    reasons.Add("ResidentAI (RealisticPathFinding)");
                    ModLog.Warn(Tag, "Auto-disabled ResidentAI group (conflict: RealisticPathFinding)");
                }
                if (settings.EnableResourceBuyerEcoSystem)
                {
                    settings.EnableResourceBuyerEcoSystem = false;
                    disabled.Add("ResourceBuyer");
                    reasons.Add("ResourceBuyer (RealisticPathFinding)");
                    ModLog.Warn(Tag, "Auto-disabled ResourceBuyer group (conflict: RealisticPathFinding)");
                }
            }

            // Time2Work 替换 LeisureSystem（属于 DownstreamAI 组）
            if (HasTime2Work && settings.EnableDownstreamAIEcoSystem)
            {
                settings.EnableDownstreamAIEcoSystem = false;
                disabled.Add("DownstreamAI");
                reasons.Add("DownstreamAI (Time2Work)");
                ModLog.Warn(Tag, "Auto-disabled DownstreamAI group (conflict: Time2Work -> LeisureSystem)");
            }

            // RealisticParking 替换 PersonalCarAISystem（也属于 DownstreamAI 组）
            if (HasRealisticParking && settings.EnableDownstreamAIEcoSystem)
            {
                settings.EnableDownstreamAIEcoSystem = false;
                if (!disabled.Contains("DownstreamAI"))
                {
                    disabled.Add("DownstreamAI");
                    reasons.Add("DownstreamAI (RealisticParking)");
                }
                ModLog.Warn(Tag, "Auto-disabled DownstreamAI group (conflict: RealisticParking -> PersonalCarAI)");
            }

            // RWH 修改 BuildingPropertyData（影响 HouseholdProperty 组）
            if (HasRWH && settings.EnableHouseholdPropertyEcoSystem)
            {
                settings.EnableHouseholdPropertyEcoSystem = false;
                disabled.Add("HouseholdProperty");
                reasons.Add("HouseholdProperty (RWH)");
                ModLog.Warn(Tag, "Auto-disabled HouseholdProperty group (conflict: RWH -> BuildingPropertyData)");
            }

            // 填充 tooltip
            if (disabled.Count > 0)
            {
                var activeGroups = new List<string>();
                if (settings.EnableDemandEcoSystem) activeGroups.Add("Demand");
                if (settings.EnableJobSearchEcoSystem) activeGroups.Add("JobSearch");
                if (settings.EnableHouseholdPropertyEcoSystem) activeGroups.Add("HouseholdProperty");
                if (settings.EnableResourceBuyerEcoSystem) activeGroups.Add("ResourceBuyer");
                if (settings.EnableResidentAIEcoSystem) activeGroups.Add("ResidentAI");
                if (settings.EnableDownstreamAIEcoSystem) activeGroups.Add("DownstreamAI");

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
        /// 返回所有已加载的非官方 Mod 名称（排除 MapExt/EcoEX 自身）。
        /// 供 CellMap 冲突弹窗展示可疑 Mod 列表。
        /// </summary>
        public static string GetAllModNames()
        {
            return AllLoadedModNames.Count > 0
                ? string.Join(", ", AllLoadedModNames)
                : "(none)";
        }
    }
}
