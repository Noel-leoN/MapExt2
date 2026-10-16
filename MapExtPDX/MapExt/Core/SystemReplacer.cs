// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using HarmonyLib;
using MapExtPDX.MapExt.ReBurstSystem.Core;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 系统替换的编排器：按模式登记替换声明，交给 <see cref="ReplacementRegistrar"/> 按顺序执行，最后注册工具与 UI 系统。
    ///
    /// <para>扩展模式（ModeA–D）的声明在各模式的 <c>ModeSystems.Declare</c>
    /// （在 CellSystems/ModeA_57km 编写，同步到 B/C/D）；原版尺寸模式与各模式共用的经济系统声明在本文件。
    /// 新增或调整系统替换一律改声明，读档重新禁用与诊断会随之同步。</para>
    /// </summary>
    public static class SystemReplacer
    {
        private const string Tag = nameof(SystemReplacer);

        // === Fields ===

        #region Fields

        /// <summary>本会话的替换注册表；Apply 之前为 null。读档时的重新禁用与诊断都从它派生。</summary>
        internal static ReplacementRegistrar Registrar { get; private set; }

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// [BUGFIX] 重新禁用被引擎重新启用的原版系统。
        /// 退出主菜单后 CS2 引擎会重置所有系统的 Enabled 状态为 true，
        /// 但 Apply() 只在 Mod.OnLoad() 中调用一次。
        /// 此方法在读档开始与完成时调用；要禁用的正是本会话各条替换声明取代的原版系统。
        /// </summary>
        public static void ReDisableVanillaSystems(Unity.Entities.World world)
        {
            if (world == null || !world.IsCreated || Registrar == null) return;

            int count = Registrar.ReDisable(world);
            if (count > 0)
                ModLog.Warn(Tag, $"[BUGFIX] 重新禁用了 {count} 个被引擎重新启用的原版系统");
            else
                ModLog.Ok(Tag, "所有原版系统已正确禁用，无需修复");
        }

        public static void Apply(UpdateSystem updateSystem, Harmony globalPatcher, AppliedConfig config)
        {
            // === 设置快照日志（Release 级别，用于诊断 .coc 缓存不一致） ===
            ModLog.Info(Tag,
                $"Apply: CV={PatchManager.CurrentCoreValue}, EcoFix={config.EconomyFix}, " +
                $"Household={config.HouseholdProperty}, Job={config.JobSearch}, " +
                $"ResBuyer={config.ResourceBuyer}, ResidentAI={config.ResidentAI}, " +
                $"DownAI={config.DownstreamAI}, Demand={config.Demand}");

            // === 替换声明：先模式部分，再各模式共用的经济系统；声明顺序即同一 phase 内的更新顺序 ===
            var registrar = new ReplacementRegistrar();
            switch (config.CoreValue)
            {
                case 4: ModeA.ModeSystems.Declare(registrar, config); break; // 57km
                case 2: ModeB.ModeSystems.Declare(registrar, config); break; // 28km
                case 8: ModeC.ModeSystems.Declare(registrar, config); break; // 114km
                case 3: ModeD.ModeSystems.Declare(registrar, config); break; // 43km
                case 1: DeclareVanillaMode(registrar, config); break;        // 原版尺寸 ModeE (None)
            }

            DeclareShared(registrar, config);

            Registrar = registrar;
            registrar.Apply(updateSystem, globalPatcher);

            // --- NoDogs 2.0 性能工具系统 ---
            updateSystem.UpdateAt<EcoShared.P1_NoDogsPatchSystem>(SystemUpdatePhase.GameSimulation);
            // P2 Editor Collision Override
            updateSystem.UpdateAt<EcoShared.P2_EditorCollisionOverrideSystem>(SystemUpdatePhase.ToolUpdate);
            // P3 购车停车救援系统（修复原版 InitializeSystem.FindParkingSpace 失败后无兜底的Bug）
            updateSystem.UpdateAt<EcoShared.P3_VehiclePurchaseRescueSystem>(SystemUpdatePhase.GameSimulation);

            // === UI 和监控系统（无条件注册，不受 isEnableEconomyFix 约束） ===
            updateSystem.UpdateAt<MapExtPDX.UI.MapExtUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<MapExtPDX.UI.PopulationDiagnosticSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<MapExtPDX.MapExt.Core.ConflictMonitoringSystem>(SystemUpdatePhase.MainLoop);
            updateSystem.UpdateAt<MapExtPDX.UI.Q2_CityStatsSystem>(SystemUpdatePhase.GameSimulation);
        }

        #endregion

        // === Declarations ===

        #region Declarations

        /// <summary>原版尺寸模式（ModeE / None）：只替换地价系统，其余 CellMap 尺寸与原版一致、仍由原版负责。</summary>
        private static void DeclareVanillaMode(ReplacementRegistrar r, AppliedConfig c)
        {
            const string household = AppliedConfig.GroupHouseholdProperty;
            const SystemUpdatePhase sim = SystemUpdatePhase.GameSimulation;

            // --- LandValueSystemRemake + UI设置 ---
            r.Replace<ModeE.LandValueSystemMod>(ReplacementRegistrar.GroupCellMap, typeof(Game.Simulation.LandValueSystem))
                .At(sim).WithPatches(typeof(ModeE.LandValueSystemMod.Patches));

            // --- Mode-specific 经济系统 (C1/D1) ---
            if (c.EconomyFix && c.HouseholdProperty)
            {
                // FindTargets 补丁先于两个系统注册，保持原有顺序
                r.Replace<ModeE.HouseholdFindPropertySystemMod>(household, typeof(Game.Simulation.HouseholdFindPropertySystem))
                    .WithPatches(typeof(ModeE.PathfindSetupSystem_FindTargets_Patch)).At(sim);
                r.Replace<ModeE.RentAdjustSystemMod>(household, typeof(Game.Simulation.RentAdjustSystem))
                    .At(sim);
            }

            // Job通用替换修补ResidentialDemand/CommerialDemand/IndustrialDemand
            if (c.EconomyFix && c.Demand)
                r.DemandJobs(JobPatchDefinitions.GetEcoSystemTargets(c.Mode));
        }

        /// <summary>各模式共用的经济系统：与地图尺寸无关，只登记一次。</summary>
        private static void DeclareShared(ReplacementRegistrar r, AppliedConfig c)
        {
            if (!c.EconomyFix) return;

            const SystemUpdatePhase sim = SystemUpdatePhase.GameSimulation;

            // === 原版殘骸系統禁用 ===
            // NetUpkeepJob 是舊路網維護費機制的殘骸：Execute 把 GetUpkeepCost 的結果累加到區域
            // 變數後直接丟棄，4 個欄位全 [ReadOnly]，零產出零副作用（OnCreate 建的 m_CitySystem
            // 在 OnUpdate 從未使用，是舊版把總額寫到 City entity 的殘留痕跡）。
            // 功能已由 CityServiceBudgetSystem.NetServiceBudgetJob 接手——它的 query 條件與
            // NetUpkeepSystem.m_UpkeepQuery 逐字相同（連變數名都叫 m_NetUpkeepQuery），
            // 但把費用寫進 CollectedCityServiceBudgetData.m_BaseCost 並按 service 歸屬。
            // 禁用省下每 512 模擬幀對 1/16 路網的白跑遍歷（量級極小，但確實是白燒）。
            // Job 帶 [BurstCompile] 且原版 AOT，Harmony 打不到 Execute，只能從系統層關。
            r.DisableOnly(ReplacementRegistrar.GroupEconomy, typeof(Game.Simulation.NetUpkeepSystem));

            if (c.HouseholdProperty)
            {
                r.Replace<EcoShared.HouseholdBehaviorSystemMod>(AppliedConfig.GroupHouseholdProperty,
                    typeof(Game.Simulation.HouseholdBehaviorSystem)).At(sim);
            }

            if (c.JobSearch)
            {
                const string job = AppliedConfig.GroupJobSearch;
                r.Replace<EcoShared.CitizenFindJobSystemMod>(job, typeof(Game.Simulation.CitizenFindJobSystem)).At(sim);
                r.Replace<EcoShared.FindJobSystemMod>(job, typeof(Game.Simulation.FindJobSystem)).At(sim);
            }

            if (c.ResourceBuyer)
            {
                const string resourceBuyer = AppliedConfig.GroupResourceBuyer;
                // 寻路优化系统
                r.Replace<EcoShared.TripNeededSystemMod>(resourceBuyer, typeof(Game.Simulation.TripNeededSystem)).At(sim);
                // 补丁拦截 Game.Tools 等外部系统对 SetupPathfindMethods 的调用，只作用于该方法、与系统注册先后无关
                r.Replace<EcoShared.ServiceCoverageSystemMod>(resourceBuyer, typeof(Game.Simulation.ServiceCoverageSystem))
                    .At(sim).WithPatches(typeof(EcoShared.ServiceCoverageSystem_SetupPathfindMethods_Patch));
                r.Replace<EcoShared.ResourceBuyerSystemMod>(resourceBuyer, typeof(Game.Simulation.ResourceBuyerSystem)).At(sim);

                // === [P1] ResourceSeller 尋路 Early Exit ===
                // 只接管 SetupTargetType.ResourceSeller，其餘類型放行（含 C1 各 Mode 的 FindHome patch）。
                r.FindTargetsHandler(resourceBuyer, Game.Pathfind.SetupTargetType.ResourceSeller,
                    new EcoShared.ResourceSellerHandler());
            }

            if (c.ResidentAI)
            {
                r.Replace<EcoShared.ResidentAISystemMod>(AppliedConfig.GroupResidentAI,
                        typeof(Game.Simulation.ResidentAISystem), typeof(Game.Simulation.ResidentAISystem.Actions))
                    .At(sim).WithActions<EcoShared.ResidentAISystemMod.Actions>(sim);
            }

            if (c.DownstreamAI)
            {
                const string downstream = AppliedConfig.GroupDownstreamAI;
                const SystemUpdatePhase load = SystemUpdatePhase.LoadSimulation;

                // F2: 私家车AI（含 Actions 子系统）；存档加载阶段另注册一份（对齐原版 SystemOrder.cs L620-621）
                r.Replace<EcoShared.PersonalCarAISystemMod>(downstream,
                        typeof(Game.Simulation.PersonalCarAISystem), typeof(Game.Simulation.PersonalCarAISystem.Actions))
                    .At(sim).WithActions<EcoShared.PersonalCarAISystemMod.ActionsMod>(sim)
                    .At(load).WithActions<EcoShared.PersonalCarAISystemMod.ActionsMod>(load);
                // F3: 出租车AI
                r.Replace<EcoShared.TaxiAISystemMod>(downstream, typeof(Game.Simulation.TaxiAISystem)).At(sim);
                // F5: 休闲系统
                r.Replace<EcoShared.LeisureSystemMod>(downstream, typeof(Game.Simulation.LeisureSystem)).At(sim);
                // F6: 找学校系统
                r.Replace<EcoShared.FindSchoolSystemMod>(downstream, typeof(Game.Simulation.FindSchoolSystem)).At(sim);

                // === [P2] Leisure 尋路 Early Exit ===
                // 掛在 dispatcher 上，只接管 SetupTargetType.Leisure。
                r.FindTargetsHandler(downstream, Game.Pathfind.SetupTargetType.Leisure, new EcoShared.LeisureHandler());
            }
        }

        #endregion
    }
}
