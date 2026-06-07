// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Rendering;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI;
using Game.UI.Localization;
using Unity.Entities;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 被动诊断系统：不在 OnUpdate 中运行，零运行时开销。
    /// 首次进入游戏时自动执行一次诊断，或由用户通过 RefreshStatus 按钮触发。
    /// Economy 冲突在启动时已由 ModConflictDetector.AutoDisableConflictGroups 处理。
    /// CellMap 冲突在此处检测并弹窗警告（列出可疑 Mod 名称）。
    /// </summary>
    public partial class ConflictMonitoringSystem : GameSystemBase
    {
        private const string Tag = "ConflictMonitor";

        #region Fields

        /// <summary>是否已执行过首次诊断</summary>
        private bool _initialCheckDone = false;

        /// <summary>是否已显示过 CellMap 弹窗（避免重复弹窗）</summary>
        private bool _cellMapDialogShown = false;

        #endregion

        protected override void OnCreate()
        {
            base.OnCreate();
            ModLog.Ok(Tag, "ConflictMonitoringSystem 已创建（被动诊断模式）");
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game && !_initialCheckDone)
            {
                _initialCheckDone = true;
                ModLog.Ok(Tag, "游戏加载完成，执行首次系统状态诊断");
                RunDiagnostics();
            }
        }

        /// <summary>被动模式：OnUpdate 不做任何工作</summary>
        protected override void OnUpdate() { }

        /// <summary>
        /// 供 UI 按钮（RefreshStatus）或首次进入游戏时调用。
        /// 执行完整的系统状态诊断并更新 UI。
        /// </summary>
        public void ForceCheck()
        {
            if (GameManager.instance.gameMode != GameMode.Game) return;
            RunDiagnostics();
        }

        #region Diagnostics

        private void RunDiagnostics()
        {
            var world = this.World;
            if (world == null || !world.IsCreated) return;

            var settings = Mod.Instance?.CurrentSettings;
            if (settings == null) return;

            var conflicts = new List<string>();
            int okCount = 0;
            int totalChecked = 0;

            // === Economy 系统状态验证（仅在对应组已启用时检查）===
            if (settings.isEnableEconomyFix)
            {
                if (settings.EnableJobSearchEcoSystem)
                {
                    CheckVanillaDisabled<CitizenFindJobSystem>(world, "JobSearch", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<FindJobSystem>(world, "JobSearch", conflicts, ref okCount, ref totalChecked);
                }
                if (settings.EnableHouseholdPropertyEcoSystem)
                {
                    CheckVanillaDisabled<HouseholdFindPropertySystem>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<HouseholdBehaviorSystem>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<RentAdjustSystem>(world, "HouseholdProperty", conflicts, ref okCount, ref totalChecked);
                }
                if (settings.EnableResourceBuyerEcoSystem)
                {
                    CheckVanillaDisabled<TripNeededSystem>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<ServiceCoverageSystem>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<ResourceBuyerSystem>(world, "ResourceBuyer", conflicts, ref okCount, ref totalChecked);
                }
                if (settings.EnableResidentAIEcoSystem)
                {
                    CheckVanillaDisabled<ResidentAISystem>(world, "ResidentAI", conflicts, ref okCount, ref totalChecked);
                }
                if (settings.EnableDownstreamAIEcoSystem)
                {
                    CheckVanillaDisabled<PersonalCarAISystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<TaxiAISystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<LeisureSystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked);
                    CheckVanillaDisabled<FindSchoolSystem>(world, "DownstreamAI", conflicts, ref okCount, ref totalChecked);
                }
                if (settings.EnableDemandEcoSystem)
                {
                    CheckModEnabled<ResidentialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked);
                    CheckModEnabled<IndustrialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked);
                    CheckModEnabled<CommercialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked);
                }
            }

            // === CellMap 系统状态验证（严重级别：可能导致崩溃/存档损坏）===
            // 仅检测模拟核心系统。工具系统(AreaToolSystem)、UI 系统(Tooltip/Infoview)
            // 在非活跃时可能 Enabled=false 属于正常行为，不纳入检测以避免误报。
            var cellMapConflicts = new List<string>();
            if (PatchManager.CurrentMode != PatchModeSetting.None)
            {
                // --- 模拟核心（禁用 = Transpiler 失效 → CellMap 越界崩溃）---
                CheckModEnabled<TelecomCoverageSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<WindSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<WindSimulationSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<GroundWaterPollutionSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<ObjectPolluteSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<ZoneSpawnSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<CitizenHappinessSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<PollutionTriggerSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<PowerPlantAISystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<WaterPumpingStationAISystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<FloodCheckSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<WaterDangerSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<WaterLevelChangeSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);

                // AttractionSystem: Time2Work 会替换为 Time2WorkAttractionSystem
                // 仅在未检测到 Time2Work 时检查，避免已知兼容场景的误报
                if (!ModConflictDetector.HasTime2Work)
                {
                    CheckModEnabled<AttractionSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                }

                // --- 渲染核心（非工具/UI）---
                CheckModEnabled<NetColorSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<MapTileSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<SpawnableAmbienceSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);
                CheckModEnabled<CarNavigationSystem>(world, "CellSystem", cellMapConflicts, ref okCount, ref totalChecked);

                // --- 以下系统移除检测（容易误报）---
                // AreaToolSystem: 继承 ToolBaseSystem，工具未激活时 Enabled=false 是正常行为
                // ValidationSystem: 仅在编辑器模式使用
                // AudioGroupingSystem/WeatherAudioSystem: 音频系统，禁用不导致 CellMap 越界
                // TelecomPreviewSystem: 工具预览，非模拟核心
                // LandValueTooltipSystem/TempWaterPumpingTooltipSystem: Tooltip 系统
                // AverageHappinessSection/PollutionInfoviewUISystem: UI 面板
            }

            // === 更新 UI 状态 ===
            int totalConflicts = conflicts.Count + cellMapConflicts.Count;
            if (totalConflicts > 0)
            {
                string statusMsg = $"[!] {okCount}/{totalChecked} OK, {totalConflicts} conflict(s)";
                if (cellMapConflicts.Count > 0)
                    statusMsg += $" ({cellMapConflicts.Count} CRITICAL CellMap)";

                settings._systemStatusReport = statusMsg;
                ModLog.Warn(Tag, statusMsg);

                // CellMap 冲突 → 严重弹窗
                if (cellMapConflicts.Count > 0 && !_cellMapDialogShown)
                {
                    _cellMapDialogShown = true;
                    ShowCellMapConflictDialog(cellMapConflicts);
                }
            }
            else if (totalChecked > 0)
            {
                settings._systemStatusReport = $"{okCount}/{totalChecked} OK";
                ModLog.Ok(Tag, $"诊断完成: {okCount}/{totalChecked} OK");
            }
            else
            {
                settings._systemStatusReport = "No systems checked (all groups off)";
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 检查原版系统是否已正确禁用（期望 Enabled == false）。
        /// 用于 Economy 系统验证。
        /// </summary>
        private static void CheckVanillaDisabled<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked) where T : GameSystemBase
        {
            totalChecked++;
            var system = world.GetExistingSystemManaged<T>();
            if (system == null) { okCount++; return; }

            if (system.Enabled)
            {
                conflicts.Add($"{typeof(T).Name} [{group}]");
            }
            else
            {
                okCount++;
            }
        }

        /// <summary>
        /// 检查 MapExt 替换系统或宿主系统是否仍然启用（期望 Enabled == true）。
        /// 用于 Demand Job 宿主 和 CellMap 宿主检测。
        /// </summary>
        private static void CheckModEnabled<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked) where T : GameSystemBase
        {
            totalChecked++;
            var system = world.GetExistingSystemManaged<T>();
            if (system == null) { okCount++; return; }

            if (!system.Enabled)
            {
                conflicts.Add($"{typeof(T).Name} [{group}]");
            }
            else
            {
                okCount++;
            }
        }

        #endregion

        #region CellMap Conflict Dialog

        /// <summary>显示 CellMap 严重冲突弹窗（列出受影响系统和可疑 Mod）</summary>
        private static void ShowCellMapConflictDialog(List<string> cellMapConflicts)
        {
            try
            {
                string affectedSystems = string.Join("\n  - ", cellMapConflicts);
                string loadedMods = ModConflictDetector.GetAllModNames();

                string message =
                    $"CRITICAL: CellMap System Conflict!\n\n" +
                    $"Affected systems:\n  - {affectedSystems}\n\n" +
                    $"Loaded mods (possible sources):\n  {loadedMods}\n\n" +
                    "CellMap texture size mismatch may cause crashes or save corruption.\n" +
                    "Strongly recommend quitting and removing the conflicting mod.";

                var dialog = new ConfirmationDialog(
                    LocalizedString.Value("MapExt: CRITICAL CellMap Conflict"),
                    LocalizedString.Value(message),
                    LocalizedString.Value("Quit Game"),
                    LocalizedString.Value("Continue (Risk)"));

                Colossal.Core.MainThreadDispatcher.RegisterUpdater(() =>
                {
                    GameManager.instance.userInterface.appBindings.ShowConfirmationDialog(dialog, (int result) =>
                    {
                        if (result == 0)
                        {
                            ModLog.Error(Tag, "CellMap 冲突：用户选择退出游戏");
                            GameManager.QuitGame();
                        }
                        else
                        {
                            ModLog.Warn(Tag, "CellMap 冲突：用户选择继续（存在崩溃风险）");
                        }
                    });
                    return true; // 一次性回调
                });

                ModLog.Error(Tag, $"CRITICAL CellMap 冲突! 受影响: {string.Join(", ", cellMapConflicts)}");
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag, $"显示 CellMap 冲突弹窗失败: {ex.Message}");
            }
        }

        #endregion

        #region UI Helpers

        private static void SetStatusReport(ModSettings settings, string value)
        {
            if (settings.SystemStatusReport != value)
                settings._systemStatusReport = value;
        }

        #endregion
    }
}
