// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using Game;
using Game.Areas;
using Game.Audio;
using Game.Rendering;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Game.UI.InGame;
using Game.UI.Localization;
using Game.UI.Tooltip;
using Unity.Entities;
using EcoShared = MapExtPDX.EcoShared;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 全系统冲突监控：加载存档后执行有限窗口检测（3 轮），然后永久停止。
    /// 如果检测到原版系统被其他 Mod 重新启用，则报告冲突、自动 disable 对应系统组并弹窗提示。
    /// 仅在已加载存档 (GameMode.Game) 且 isEnableEconomyFix=true 时有效。
    /// 采用"二次确认"机制：首次检测到异常时尝试自行修复（重新禁用），
    /// 仅在连续两次检测到同一系统被重新启用时才确认为真正冲突。
    /// </summary>
    public partial class ConflictMonitoringSystem : SystemBase
    {
        private const string Tag = "ConflictMonitor";

        #region Constants and Fields

        private int _ticker = 0;
        private const int CheckInterval = 300; // 约每5秒检查一次 (60fps)
        private int _startupDelay = 600; // 启动后等待约10秒再开始检测，避免误报
        private bool _wasInGame = false;

        // === 有限窗口控制 ===
        private const int MaxRounds = 3;
        private int _currentRound = 0;
        private bool _completed = false;
        private bool _dialogShown = false;

        /// <summary>首次检测到异常的系统名称集合，用于二次确认</summary>
        private readonly HashSet<string> _pendingVerification = new HashSet<string>();

        #endregion

        protected override void OnUpdate()
        {
            // 仅在游戏内运行（排除主菜单、编辑器等场景）
            if (GameManager.instance.gameMode != GameMode.Game)
            {
                // 退出游戏模式时重置状态，下次进入时重新检测
                if (_wasInGame)
                {
                    _wasInGame = false;
                    _completed = false;
                    _currentRound = 0;
                    _dialogShown = false;
                }
                return;
            }

            // 有限窗口：3 轮检测完毕后永久停止
            if (_completed) return;

            var settings = Mod.Instance?.CurrentSettings;
            if (settings == null || !settings.isEnableEconomyFix) return;

            // 从非游戏模式进入游戏模式时，重置启动延迟和待验证状态
            if (!_wasInGame)
            {
                _wasInGame = true;
                _startupDelay = 600;
                _ticker = 0;
                _currentRound = 0;
                _completed = false;
                _dialogShown = false;
                _pendingVerification.Clear();
                return;
            }

            // 启动延迟：等待所有系统完成初始化
            if (_startupDelay > 0)
            {
                _startupDelay--;
                return;
            }

            // 原版存档转换期间不检测（VanillaSaveConversionSystem 在 OnGameLoaded 同步执行，
            // 但为安全起见仍做前置守卫）
            if (SaveLoadSystem.VanillaConversionState.PendingConversion) return;

            _ticker++;
            if (_ticker < CheckInterval) return;
            _ticker = 0;

            _currentRound++;
            CheckForConflicts();

            if (_currentRound >= MaxRounds)
            {
                _completed = true;
                ModLog.Ok(Tag, $"有限窗口检测完成（{MaxRounds} 轮），停止监控");
            }
        }

        /// <summary>
        /// 供 UI 按钮调用的即时检查入口，跳过计时器和延迟直接执行冲突检测。
        /// 不受有限窗口限制。
        /// </summary>
        public void ForceCheck()
        {
            if (GameManager.instance.gameMode != GameMode.Game) return;
            var settings = Mod.Instance?.CurrentSettings;
            if (settings == null || !settings.isEnableEconomyFix) return;
            CheckForConflicts();
        }

        private void CheckForConflicts()
        {
            // 使用 this.World 确保在同一 World 上下文中检查系统状态
            var world = this.World;
            if (world == null || !world.IsCreated) return;

            var settings = Mod.Instance?.CurrentSettings;
            if (settings == null) return;

            // 基础监控系统数（原版系统检查）
            const int BaseSystemCount = 13;
            // 反向检测系统数（MapExt 自身替换系统被外部禁用）
            int reverseCheckCount = 0;
            // Job 宿主系统检测数
            int jobHostCheckCount = 0;

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

            // --- F 系列: 下游AI系统（私家车/出租车/休闲/学校）---
            if (settings.EnableDownstreamAIEcoSystem)
            {
                CheckVanillaSystem<PersonalCarAISystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<TaxiAISystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<LeisureSystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckVanillaSystem<FindSchoolSystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
            }

            // === 反向检测: MapExt 自身替换系统是否被外部 Mod 禁用 ===
            // 仅在检测到已知冲突 Mod 存在时执行，避免无意义开销
            if (ModConflictDetector.IsScanned)
            {
                if (ModConflictDetector.HasRealisticPathFinding && settings.EnableResidentAIEcoSystem)
                {
                    CheckModSystem<EcoShared.ResidentAISystemMod>(world, "ResidentAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
                    reverseCheckCount++;
                }

                if (ModConflictDetector.HasRealisticPathFinding && settings.EnableResourceBuyerEcoSystem)
                {
                    CheckModSystem<EcoShared.TripNeededSystemMod>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
                    CheckModSystem<EcoShared.ResourceBuyerSystemMod>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked, conflictGroups);
                    reverseCheckCount += 2;
                }

                if (ModConflictDetector.HasTime2Work && settings.EnableDownstreamAIEcoSystem)
                {
                    CheckModSystem<EcoShared.LeisureSystemMod>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
                    reverseCheckCount++;
                }

                if (ModConflictDetector.HasRealisticParking && settings.EnableDownstreamAIEcoSystem)
                {
                    CheckModSystem<EcoShared.PersonalCarAISystemMod>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked, conflictGroups);
                    reverseCheckCount++;
                }
            }

            // === Job Transpiler 宿主系统检测 ===
            // Demand 系统使用 Harmony Transpiler 替换内部 Job，
            // 如果宿主系统被外部 Mod 禁用，Transpiler 将打在死代码上。
            if (settings.EnableDemandEcoSystem)
            {
                CheckModSystem<ResidentialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<IndustrialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<CommercialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked, conflictGroups);
                jobHostCheckCount = 3;
            }

            // === CellSystem Transpiler 宿主系统检测 ===
            // CellMap 扩展使用 Harmony Transpiler 原地修改原版系统内部 Job，
            // 原版系统保持 Enabled=true。如果任何 Mod 禁用这些宿主系统，
            // Transpiler 代码不会执行，CellMap 尺寸不匹配将导致越界崩溃。
            // 仅在扩展地图模式下检测（None 模式不使用 CellSystem 补丁）。
            int cellSystemCheckCount = 0;
            if (PatchManager.CurrentMode != PatchModeSetting.None)
            {
                // --- 模拟核心 ---
                CheckModSystem<TelecomCoverageSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<WindSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<WindSimulationSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<AttractionSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<GroundWaterPollutionSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<ObjectPolluteSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<ZoneSpawnSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<CitizenHappinessSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<PollutionTriggerSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<PowerPlantAISystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<WaterPumpingStationAISystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<FloodCheckSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<WaterDangerSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<WaterLevelChangeSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                // --- 渲染/工具 ---
                CheckModSystem<NetColorSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<ValidationSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<AreaToolSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<MapTileSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<SpawnableAmbienceSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                // --- UI/音频/导航 ---
                CheckModSystem<AudioGroupingSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<WeatherAudioSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<CarNavigationSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<TelecomPreviewSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<LandValueTooltipSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<TempWaterPumpingTooltipSystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<AverageHappinessSection>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                CheckModSystem<PollutionInfoviewUISystem>(world, "CellSystem", conflicts, ref okCount, ref totalChecked, conflictGroups);
                cellSystemCheckCount = 27;
            }

            // === 更新 UI 状态 ===
            int totalSystemCount = BaseSystemCount + reverseCheckCount + jobHostCheckCount + cellSystemCheckCount;
            int skipped = totalSystemCount - totalChecked;

            if (conflicts.Count > 0)
            {
                string warningMsg = $"[!] {conflicts.Count} Conflicts: {string.Join(", ", conflicts)} re-enabled";
                SetWarning(settings, warningMsg);
                SetStatusReport(settings, $"[!] {okCount}/{totalSystemCount} OK, {conflicts.Count} conflicts");

                // === 自动 disable 冲突的系统组 ===
                AutoDisableConflictGroups(settings, conflictGroups);

                ModLog.Warn(Tag, warningMsg);

                // 仅在最终轮或 ForceCheck 时弹窗（避免中间轮弹窗后又发现新冲突）
                if (!_dialogShown && (_currentRound >= MaxRounds || _completed))
                {
                    _dialogShown = true;
                    ShowConflictDialog(conflicts);
                }
            }
            else if (totalChecked > 0)
            {
                SetWarning(settings, "None");
                string statusText = skipped > 0
                    ? $"{okCount}/{totalSystemCount} OK ({skipped} off)"
                    : $"{okCount}/{totalSystemCount} OK";
                SetStatusReport(settings, statusText);
            }
            else
            {
                SetWarning(settings, "None");
                SetStatusReport(settings, $"0/{totalSystemCount} (all off)");
            }
        }

        // === Helpers ===

        /// <summary>
        /// 检查单个原版系统是否被意外重新启用。
        /// 采用"二次确认"机制避免误报：
        /// - 首次检测到异常：尝试自行修复（重新禁用），标记为待验证，不报告冲突
        /// - 二次检测到异常：确认为真正冲突（另一个 Mod 在持续重新启用）
        /// </summary>
        private void CheckVanillaSystem<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked,
            HashSet<string> conflictGroups) where T : GameSystemBase
        {
            totalChecked++;
            string systemName = typeof(T).Name;

            // 使用 GetExistingSystemManaged 而非 GetOrCreateSystemManaged
            // 避免意外创建未注册的系统实例
            var system = world.GetExistingSystemManaged<T>();
            if (system == null)
            {
                // 系统不存在 = 不是冲突（可能是原版系统被完全移除了）
                _pendingVerification.Remove(systemName);
                okCount++;
                return;
            }

            if (system.Enabled)
            {
                // 尝试自行修复：重新禁用原版系统
                system.Enabled = false;

                if (_pendingVerification.Contains(systemName))
                {
                    // 上次已尝试修复但系统再次被启用 → 确认为真正冲突
                    conflicts.Add(systemName);
                    conflictGroups.Add(group);
                    _pendingVerification.Remove(systemName);
                }
                else
                {
                    // 首次检测到启用状态，标记为待验证，暂不报告
                    _pendingVerification.Add(systemName);
                    okCount++;
                }
            }
            else
            {
                // 已正确禁用，清除待验证状态
                _pendingVerification.Remove(systemName);
                okCount++;
            }
        }

        /// <summary>
        /// 反向检测：检查 MapExt 自己的替换系统（或 Job 宿主系统）是否被外部 Mod 禁用。
        /// 逻辑与 CheckVanillaSystem 相反：期望 system.Enabled == true。
        /// 同时用于 Demand 系统 Job Transpiler 宿主检测。
        /// </summary>
        private void CheckModSystem<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked,
            HashSet<string> conflictGroups) where T : GameSystemBase
        {
            totalChecked++;
            string systemName = typeof(T).Name;

            var system = world.GetExistingSystemManaged<T>();
            if (system == null)
            {
                // 系统未注册（该组未启用），不是冲突
                okCount++;
                return;
            }

            if (!system.Enabled)
            {
                // MapExt 的替换系统或 Job 宿主被外部 Mod 禁用了
                system.Enabled = true; // 尝试恢复

                string key = "mod_" + systemName;
                if (_pendingVerification.Contains(key))
                {
                    // 二次确认: 持续被禁用 → 真正冲突
                    conflicts.Add(systemName + " (disabled by external mod)");
                    conflictGroups.Add(group);
                    _pendingVerification.Remove(key);
                }
                else
                {
                    _pendingVerification.Add(key);
                    okCount++;
                }
            }
            else
            {
                _pendingVerification.Remove("mod_" + systemName);
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
                    case "DownstreamAI":
                        if (settings.EnableDownstreamAIEcoSystem)
                        {
                            settings.EnableDownstreamAIEcoSystem = false;
                            ModLog.Warn(Tag, "Auto-disabled DownstreamAI group due to conflict.");
                        }
                        break;
                    case "Demand":
                        if (settings.EnableDemandEcoSystem)
                        {
                            settings.EnableDemandEcoSystem = false;
                            ModLog.Warn(Tag, "Auto-disabled Demand group due to Job host conflict.");
                        }
                        break;
                    case "CellSystem":
                        // CellSystem 无用户开关可禁用，仅记录严重警告
                        // CheckModSystem 已尝试重新启用宿主系统
                        ModLog.Error(Tag, "CRITICAL: CellSystem host system disabled by external mod! Map data corruption risk.");
                        break;
                }
            }
        }

        // === UI 弹窗 ===

        /// <summary>显示冲突检测弹窗，提示用户退出到主菜单</summary>
        private static void ShowConflictDialog(List<string> conflicts)
        {
            try
            {
                string conflictList = string.Join("\n  - ", conflicts);
                string message =
                    $"Conflicting mods detected:\n  - {conflictList}\n\n" +
                    "Affected economy subsystems have been auto-disabled to protect your save.\n" +
                    "It is recommended to exit and remove the conflicting mods.\n\n" +
                    "Exit to main menu?";

                var dialog = new ConfirmationDialog(
                    LocalizedString.Value("MapExt: Mod Conflict Detected"),
                    LocalizedString.Value(message),
                    LocalizedString.Value("Exit to Menu"),
                    LocalizedString.Value("Continue Playing"));

                // 延迟显示，避免与其他 mod 的 OnGameLoaded 对话框冲突
                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, (int result) =>
                    {
                        if (result == 0)
                        {
                            ModLog.Info(Tag, "用户确认退出主菜单");
                            GameManager.QuitGame();
                        }
                        else
                        {
                            ModLog.Info(Tag, "用户选择继续游戏（冲突系统组已自动禁用）");
                        }
                    });
                    return true; // 一次性回调，返回 true 注销
                });
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag, $"显示冲突弹窗失败: {ex.Message}");
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
