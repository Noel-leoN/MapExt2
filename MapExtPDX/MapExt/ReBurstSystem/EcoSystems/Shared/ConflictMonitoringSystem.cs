// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using Game;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Entities;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.EcoShared
{
    /// <summary>
    /// 全系统冲突监控：定期检查 MapExtPDX 经济系统替换对的运行状态。
    /// 如果检测到原版系统被其他 Mod 重新启用，则报告冲突并自动 disable 对应系统组。
    /// 仅在已加载存档 (GameMode.Game) 且 isEnableEconomyFix=true 时有效。
    /// </summary>
    public partial class ConflictMonitoringSystem : SystemBase
    {
        private const string Tag = "ConflictMonitor";
        private int _ticker = 0;
        private const int CheckInterval = 300; // 约每5秒检查一次 (60fps)
        private int _startupDelay = 600; // 启动后等待约10秒再开始检测，避免误报

        protected override void OnUpdate()
        {
            // 仅在游戏内运行（排除主菜单、编辑器等场景）
            if (GameManager.instance.gameMode != GameMode.Game) return;

            var settings = Mod.Instance?.CurrentSettings;
            if (settings == null || !settings.isEnableEconomyFix) return;

            // 启动延迟：等待所有系统完成初始化
            if (_startupDelay > 0)
            {
                _startupDelay--;
                return;
            }

            _ticker++;
            if (_ticker < CheckInterval) return;
            _ticker = 0;

            CheckForConflicts();
        }

        private void CheckForConflicts()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var settings = Mod.Instance?.CurrentSettings;
            if (settings == null) return;

            // === 逐组检测原版系统（应为 Disabled） ===
            var conflicts = new List<string>();
            int okCount = 0;
            int totalChecked = 0;
            var conflictGroups = new HashSet<string>();

            // --- B 系列: 求职系统 ---
            if (settings.EnableJobSearchEcoSystem)
            {
                CheckVanillaSystem<CitizenFindJobSystem>(world, "JobSearch", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<FindJobSystem>(world, "JobSearch", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // --- C+D 系列: 家庭行为 + 租金系统 ---
            if (settings.EnableHouseholdPropertyEcoSystem)
            {
                CheckVanillaSystem<HouseholdFindPropertySystem>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<HouseholdBehaviorSystem>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<RentAdjustSystem>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // --- E 系列: 出行/服务覆盖/资源采购系统 ---
            if (settings.EnableResourceBuyerEcoSystem)
            {
                CheckVanillaSystem<TripNeededSystem>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<ServiceCoverageSystem>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<ResourceBuyerSystem>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // --- F 系列: 居民AI系统 ---
            if (settings.EnableResidentAIEcoSystem)
            {
                CheckVanillaSystem<ResidentAISystem>(world, "ResidentAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // === 更新 UI 状态 ===
            if (conflicts.Count > 0)
            {
                string warningMsg = $"⚠ {conflicts.Count} Conflicts: {string.Join(", ", conflicts)} re-enabled";
                SetWarning(settings, warningMsg);
                SetStatusReport(settings, $"⚠ {okCount}/{totalChecked} OK, {conflicts.Count} conflicts");

                // === 自动 disable 冲突的系统组 ===
                AutoDisableConflictGroups(settings, conflictGroups);

                ModLog.Warn(Tag, warningMsg);
            }
            else if (totalChecked > 0)
            {
                SetWarning(settings, "None");
                SetStatusReport(settings, $"✅ {okCount}/{totalChecked} OK");
            }
            else
            {
                SetWarning(settings, "None");
                SetStatusReport(settings, "All eco-subsystems disabled by user");
            }
        }

        // === Helpers ===

        /// <summary>检查单个原版系统是否被意外重新启用</summary>
        private static void CheckVanillaSystem<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked,
            HashSet<string> conflictGroups) where T : GameSystemBase
        {
            totalChecked++;
            // 使用 GetExistingSystemManaged 而非 GetOrCreateSystemManaged
            // 避免意外创建未注册的系统实例
            var system = world.GetExistingSystemManaged<T>();
            if (system == null)
            {
                // 系统不存在 = 不是冲突（可能是原版系统被完全移除了）
                okCount++;
                return;
            }

            if (system.Enabled)
            {
                conflicts.Add(typeof(T).Name);
                conflictGroups.Add(group);
            }
            else
            {
                okCount++;
            }
        }

        /// <summary>检测到冲突时，自动关闭对应系统组的 Settings 开关</summary>
        private static void AutoDisableConflictGroups(ModSettings settings, HashSet<string> groups)
        {
            foreach (var group in groups)
            {
                switch (group)
                {
                    case "JobSearch":
                        if (settings.EnableJobSearchEcoSystem)
                        {
                            settings.EnableJobSearchEcoSystem = false;
                            ModLog.Warn(Tag, "Auto-disabled JobSearch group due to conflict.");
                        }
                        break;
                    case "HouseholdProperty":
                        if (settings.EnableHouseholdPropertyEcoSystem)
                        {
                            settings.EnableHouseholdPropertyEcoSystem = false;
                            ModLog.Warn(Tag, "Auto-disabled HouseholdProperty group due to conflict.");
                        }
                        break;
                    case "ResourceBuyer":
                        if (settings.EnableResourceBuyerEcoSystem)
                        {
                            settings.EnableResourceBuyerEcoSystem = false;
                            ModLog.Warn(Tag, "Auto-disabled ResourceBuyer group due to conflict.");
                        }
                        break;
                    case "ResidentAI":
                        if (settings.EnableResidentAIEcoSystem)
                        {
                            settings.EnableResidentAIEcoSystem = false;
                            ModLog.Warn(Tag, "Auto-disabled ResidentAI group due to conflict.");
                        }
                        break;
                }
            }
        }

        /// <summary>仅在值变化时赋值，避免频繁触发 UI 刷新</summary>
        private static void SetWarning(ModSettings settings, string value)
        {
            if (settings.ConflictWarning != value)
                settings.ConflictWarning = value;
        }

        private static void SetStatusReport(ModSettings settings, string value)
        {
            if (settings.SystemStatusReport != value)
                settings.SystemStatusReport = value;
        }
    }
}
