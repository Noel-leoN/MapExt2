// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using Colossal.Serialization.Entities;
using Game;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI;
using Game.UI.Localization;
using Unity.Entities;
using EconomyEX.Settings;

namespace EconomyEX.Helpers
{
    /// <summary>
    /// 被动诊断系统：不在 OnUpdate 中运行，零运行时开销。
    /// 首次进入游戏时自动执行一次诊断，或由用户通过 RefreshStatus 按钮触发。
    /// 当 MapExtPDX 同时启用时，EcoEX 完全跳过（由 MapExtPDX 负责）。
    /// </summary>
    public partial class ConflictMonitoringSystem : GameSystemBase
    {
        private const string Tag = "ConflictMonitor";

        #region Fields

        /// <summary>是否已执行过首次诊断</summary>
        private bool _initialCheckDone = false;

        /// <summary>MapExtPDX 是否同时启用</summary>
        private bool _mapExtActive = false;

        #endregion

        protected override void OnCreate()
        {
            base.OnCreate();
            _mapExtActive = ModConflictDetector.HasMapExt;
            if (_mapExtActive)
            {
                ModLog.Info(Tag, "MapExtPDX 已加载，EcoEX 冲突检测委托给 MapExtPDX");
            }
            else
            {
                ModLog.Ok(Tag, "ConflictMonitoringSystem 已创建（被动诊断模式）");
            }
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if (mode == GameMode.Game && !_mapExtActive && !_initialCheckDone)
            {
                _initialCheckDone = true;
                ModLog.Ok(Tag, "游戏加载完成，执行首次系统状态诊断");
                RunDiagnostics();
            }
        }

        /// <summary>被动模式：OnUpdate 不做任何工作</summary>
        protected override void OnUpdate() { }

        /// <summary>供 RefreshStatus 按钮调用</summary>
        public void ForceCheck()
        {
            if (_mapExtActive) return;
            if (GameManager.instance.gameMode != GameMode.Game) return;
            RunDiagnostics();
        }

        #region Diagnostics

        private void RunDiagnostics()
        {
            var world = this.World;
            if (world == null || !world.IsCreated) return;

            var settings = EconomyEX.Mod.Instance?.Settings;
            if (settings == null) return;

            var conflicts = new List<string>();
            int okCount = 0;
            int totalChecked = 0;

            if (settings.EnableEconomyFix)
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
                if (settings.EnableDemandEcoSystem)
                {
                    CheckModEnabled<ResidentialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked);
                    CheckModEnabled<IndustrialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked);
                    CheckModEnabled<CommercialDemandSystem>(world, "Demand", conflicts, ref okCount, ref totalChecked);
                }
            }

            // 更新 UI 状态
            if (conflicts.Count > 0)
            {
                settings._systemStatusReport = $"[!] {okCount}/{totalChecked} OK, {conflicts.Count} conflict(s)";
                ModLog.Warn(Tag, $"诊断发现 {conflicts.Count} 个冲突: {string.Join(", ", conflicts)}");
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

        private static void CheckVanillaDisabled<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked) where T : GameSystemBase
        {
            totalChecked++;
            var system = world.GetExistingSystemManaged<T>();
            if (system == null) { okCount++; return; }
            if (system.Enabled)
                conflicts.Add($"{typeof(T).Name} [{group}]");
            else
                okCount++;
        }

        private static void CheckModEnabled<T>(World world, string group,
            List<string> conflicts, ref int okCount, ref int totalChecked) where T : GameSystemBase
        {
            totalChecked++;
            var system = world.GetExistingSystemManaged<T>();
            if (system == null) { okCount++; return; }
            if (!system.Enabled)
                conflicts.Add($"{typeof(T).Name} [{group}]");
            else
                okCount++;
        }

        #endregion
    }
}
