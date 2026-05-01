// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using Game.Simulation;
using Game.Tools;
using EconomyEX.Systems;
using Unity.Entities;
using HarmonyLib;

namespace EconomyEX.Helpers
{
    /// <summary>
    /// Manages the registration and lifecycle (Enable/Disable) of EconomyEX systems.
    /// 注意: A1-A3 需求系统(Residential/Commercial/Industrial)仅含 Job 结构体，
    /// 通过 Harmony Transpiler 替换原版 Job，不做系统替换注册。
    /// E1-E3: 出行/服务覆盖/资源采购系统 (ECS 替换)
    /// F1: 居民AI系统 (ECS 替换，含嵌套 Actions 子系统)
    /// </summary>
    public static class SystemRegistrar
    {
        public static void RegisterSystems(UpdateSystem updateSystem)
        {
            // Register Mod Systems to the Game Update Loop
            // Phase: GameSimulation (Main Simulation Loop)
            
            // === 系统替换 (System Replacement) ===
            // B 系列: 求职系统
            updateSystem.UpdateAt<CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

            // C 系列: 家庭行为系统
            updateSystem.UpdateAt<HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);

            // D 系列: 租金与地价系统
            updateSystem.UpdateAt<RentAdjustSystemMod>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<LandValueSystemMod>(SystemUpdatePhase.GameSimulation);

            // E 系列: 出行/服务覆盖/资源采购系统
            updateSystem.UpdateAt<TripNeededSystemMod>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<ServiceCoverageSystemMod>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<ResourceBuyerSystemMod>(SystemUpdatePhase.GameSimulation);

            // F 系列: 居民AI系统 (含嵌套 Actions 子系统)
            updateSystem.UpdateAt<ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAfter<ResidentAISystemMod.Actions, ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);

            // G 系列: 诊断系统 (按需查询，默认禁用)
            updateSystem.UpdateAt<PopulationDiagnosticSystem>(SystemUpdatePhase.GameSimulation);

            // P 系列: 工具系统 (独立于经济开关)
            updateSystem.UpdateAt<P2_EditorCollisionOverrideSystem>(SystemUpdatePhase.ToolUpdate);

            // 注意: A1-A3 需求系统仅通过 JobPatchHelper Transpiler 替换 Job，无需注册系统

            // Initial State: DISABLED (Waiting for Map Size Check)
            DisableEconomySystems(isInitialStandby: true);
        }

        /// <summary>
        /// Enables EconomyEX systems and Disables Vanilla counterparts.
        /// Call this when Vanilla Map is detected.
        /// </summary>
        public static void EnableEconomySystems()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // 1. Disable Vanilla Systems (仅禁用有替换系统的原版)
            SetSystemEnabled<HouseholdFindPropertySystem>(world, false);
            SetSystemEnabled<HouseholdBehaviorSystem>(world, false);
            SetSystemEnabled<CitizenFindJobSystem>(world, false);
            SetSystemEnabled<FindJobSystem>(world, false);
            SetSystemEnabled<RentAdjustSystem>(world, false);
            SetSystemEnabled<LandValueSystem>(world, false);
            SetSystemEnabled<TripNeededSystem>(world, false);
            SetSystemEnabled<ServiceCoverageSystem>(world, false);
            SetSystemEnabled<ResourceBuyerSystem>(world, false);
            SetSystemEnabled<ResidentAISystem>(world, false);
            SetSystemEnabled<ResidentAISystem.Actions>(world, false);
            // 注意: ResidentialDemandSystem/CommercialDemandSystem/IndustrialDemandSystem
            // 保持启用 — 它们的 Job 会被 Transpiler 替换

            // 2. Enable Mod Systems
            SetSystemEnabled<HouseholdFindPropertySystemMod>(world, true);
            SetSystemEnabled<HouseholdBehaviorSystemMod>(world, true);
            SetSystemEnabled<CitizenFindJobSystemMod>(world, true);
            SetSystemEnabled<FindJobSystemMod>(world, true);
            SetSystemEnabled<RentAdjustSystemMod>(world, true);
            SetSystemEnabled<LandValueSystemMod>(world, true);
            SetSystemEnabled<TripNeededSystemMod>(world, true);
            SetSystemEnabled<ServiceCoverageSystemMod>(world, true);
            SetSystemEnabled<ResourceBuyerSystemMod>(world, true);
            SetSystemEnabled<ResidentAISystemMod>(world, true);
            SetSystemEnabled<ResidentAISystemMod.Actions>(world, true);

            Mod.Info("EconomyEX Systems ENABLED. Vanilla Systems DISABLED.");
        }

        /// <summary>
        /// Disables EconomyEX systems and Restores Vanilla counterparts.
        /// Call this when Large Map is detected or Mod is unloading.
        /// </summary>
        /// <param name="isInitialStandby">true=初始注册后的待机状态，false=运行时主动停用</param>
        public static void DisableEconomySystems(bool isInitialStandby = false)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            // 1. Restore Vanilla Systems
            SetSystemEnabled<HouseholdFindPropertySystem>(world, true);
            SetSystemEnabled<HouseholdBehaviorSystem>(world, true);
            SetSystemEnabled<CitizenFindJobSystem>(world, true);
            SetSystemEnabled<FindJobSystem>(world, true);
            SetSystemEnabled<RentAdjustSystem>(world, true);
            SetSystemEnabled<LandValueSystem>(world, true);
            SetSystemEnabled<TripNeededSystem>(world, true);
            SetSystemEnabled<ServiceCoverageSystem>(world, true);
            SetSystemEnabled<ResourceBuyerSystem>(world, true);
            SetSystemEnabled<ResidentAISystem>(world, true);
            SetSystemEnabled<ResidentAISystem.Actions>(world, true);

            // 2. Disable Mod Systems
            SetSystemEnabled<HouseholdFindPropertySystemMod>(world, false);
            SetSystemEnabled<HouseholdBehaviorSystemMod>(world, false);
            SetSystemEnabled<CitizenFindJobSystemMod>(world, false);
            SetSystemEnabled<FindJobSystemMod>(world, false);
            SetSystemEnabled<RentAdjustSystemMod>(world, false);
            SetSystemEnabled<LandValueSystemMod>(world, false);
            SetSystemEnabled<TripNeededSystemMod>(world, false);
            SetSystemEnabled<ServiceCoverageSystemMod>(world, false);
            SetSystemEnabled<ResourceBuyerSystemMod>(world, false);
            SetSystemEnabled<ResidentAISystemMod>(world, false);
            SetSystemEnabled<ResidentAISystemMod.Actions>(world, false);

            if (isInitialStandby)
                Mod.Info("EconomyEX Systems registered (STANDBY). Awaiting map size verification...");
            else
                Mod.Info("EconomyEX Systems DISABLED. Framework restored to Vanilla.");
        }

        private static void SetSystemEnabled<T>(World world, bool enabled) where T : GameSystemBase
        {
            var system = world.GetOrCreateSystemManaged<T>();
            if (system != null)
            {
                system.Enabled = enabled;
            }
        }
    }
}
