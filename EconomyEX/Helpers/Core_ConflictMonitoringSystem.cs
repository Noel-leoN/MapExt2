// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using Game;
using Game.Simulation;
using EconomyEX.Systems;
using Unity.Entities;

namespace EconomyEX.Helpers
{
    /// <summary>
    /// 全系统冲突监控：定期检查所有被 EconomyEX 替换的 10 对系统对的运行状态。
    /// 如果检测到原版系统被其他 Mod 重新启用或 Mod 系统被禁用，则报告冲突并自动 disable 对应系统组。
    /// </summary>
    public partial class ConflictMonitoringSystem : SystemBase
    {
        private const string Tag = "ConflictMonitor";
        private int _ticker = 0;
        private const int CheckInterval = 300; // 约每5秒检查一次 (60fps)

        protected override void OnUpdate()
        {
            if (!Mod.IsActive) return;

            _ticker++;
            if (_ticker < CheckInterval) return;
            _ticker = 0;

            CheckForConflicts();
        }

        private void CheckForConflicts()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var settings = Mod.Instance?.Settings;
            if (settings == null) return;

            // === 逐组检测 ===
            var conflicts = new List<string>();
            int okCount = 0;
            int totalChecked = 0;
            var conflictGroups = new HashSet<string>();

            // --- B 系列: 求职系统 ---
            if (settings.EnableJobSearchEcoSystem)
            {
                CheckPair<CitizenFindJobSystem, CitizenFindJobSystemMod>(world, "JobSearch", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckPair<FindJobSystem, FindJobSystemMod>(world, "JobSearch", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // --- C 系列: 家庭行为系统 ---
            if (settings.EnableHouseholdPropertyEcoSystem)
            {
                CheckPair<HouseholdFindPropertySystem, HouseholdFindPropertySystemMod>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckPair<HouseholdBehaviorSystem, HouseholdBehaviorSystemMod>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
                // D 系列：租金与地价
                CheckPair<RentAdjustSystem, RentAdjustSystemMod>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckPair<LandValueSystem, LandValueSystemMod>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // --- E 系列: 出行/服务覆盖/资源采购系统 ---
            if (settings.EnableResourceBuyerEcoSystem)
            {
                CheckPair<TripNeededSystem, TripNeededSystemMod>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckPair<ServiceCoverageSystem, ServiceCoverageSystemMod>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckPair<ResourceBuyerSystem, ResourceBuyerSystemMod>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // --- F 系列: 居民AI系统 ---
            if (settings.EnableResidentAIEcoSystem)
            {
                CheckPair<ResidentAISystem, ResidentAISystemMod>(world, "ResidentAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // === 更新 UI 状态 ===
            if (conflicts.Count > 0)
            {
                string warningMsg = $"⚠ {conflicts.Count} Conflicts: {string.Join(", ", conflicts)}";
                SetWarning(warningMsg);
                SetStatusReport($"⚠ {okCount}/{totalChecked} OK, {conflicts.Count} Conflicts");

                // === 自动 disable 冲突的系统组 ===
                AutoDisableConflictGroups(settings, conflictGroups);

                Mod.Warn($"[{Tag}] {warningMsg}");
            }
            else if (totalChecked > 0)
            {
                SetWarning("None");
                SetStatusReport($"✅ {okCount}/{totalChecked} OK");
            }
            else
            {
                SetWarning("None");
                SetStatusReport("All subsystems disabled");
            }
        }

        // === Helpers ===

        /// <summary>检查一对系统（原版应禁用，Mod应启用）</summary>
        private static void CheckPair<TVanilla, TMod>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked,
            HashSet<string> conflictGroups)
            where TVanilla : GameSystemBase
            where TMod : GameSystemBase
        {
            totalChecked++;
            bool hasConflict = false;

            // 原版系统应该被禁用
            var vanillaSystem = world.GetOrCreateSystemManaged<TVanilla>();
            if (vanillaSystem != null && vanillaSystem.Enabled)
            {
                conflicts.Add($"{typeof(TVanilla).Name} re-enabled");
                hasConflict = true;
            }

            // Mod 系统应该处于启用状态
            var modSystem = world.GetOrCreateSystemManaged<TMod>();
            if (modSystem != null && !modSystem.Enabled)
            {
                conflicts.Add($"{typeof(TMod).Name} disabled");
                hasConflict = true;
            }

            if (hasConflict)
                conflictGroups.Add(group);
            else
                okCount++;
        }

        /// <summary>检测到冲突时，自动关闭对应系统组的 Settings 开关</summary>
        private static void AutoDisableConflictGroups(Settings.ModSettings settings, HashSet<string> groups)
        {
            foreach (var group in groups)
            {
                switch (group)
                {
                    case "JobSearch":
                        if (settings.EnableJobSearchEcoSystem)
                        {
                            settings.EnableJobSearchEcoSystem = false;
                            Mod.Warn($"[{Tag}] Auto-disabled JobSearch group due to conflict.");
                        }
                        break;
                    case "HouseholdProperty":
                        if (settings.EnableHouseholdPropertyEcoSystem)
                        {
                            settings.EnableHouseholdPropertyEcoSystem = false;
                            Mod.Warn($"[{Tag}] Auto-disabled HouseholdProperty group due to conflict.");
                        }
                        break;
                    case "ResourceBuyer":
                        if (settings.EnableResourceBuyerEcoSystem)
                        {
                            settings.EnableResourceBuyerEcoSystem = false;
                            Mod.Warn($"[{Tag}] Auto-disabled ResourceBuyer group due to conflict.");
                        }
                        break;
                    case "ResidentAI":
                        if (settings.EnableResidentAIEcoSystem)
                        {
                            settings.EnableResidentAIEcoSystem = false;
                            Mod.Warn($"[{Tag}] Auto-disabled ResidentAI group due to conflict.");
                        }
                        break;
                }
            }
        }

        private void SetWarning(string msg)
        {
            if (Mod.Instance?.Settings != null && Mod.Instance.Settings.ConflictWarning != msg)
            {
                Mod.Instance.Settings.ConflictWarning = msg;
            }
        }

        private void SetStatusReport(string msg)
        {
            if (Mod.Instance?.Settings != null && Mod.Instance.Settings.SystemStatusReport != msg)
            {
                Mod.Instance.Settings.SystemStatusReport = msg;
            }
        }
    }
}
