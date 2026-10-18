// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game;
using MapExtPDX.MapExt.Core;
using MapExtPDX.MapExt.ReBurstSystem.Core;

namespace MapExtPDX.ModeA
{
    /// <summary>
    /// 本扩展模式的替换声明：在 ModeA 编写，建置时由 ModeSources.Generate.ps1 生成到 B/C/D（类型名不加限定，在各模式命名空间内解析）。
    ///
    /// <para><b>声明顺序即同一 phase 内的更新顺序</b>，须与重构前 SystemReplacer 的注册顺序一致；
    /// 每条声明自带它取代的原版系统，注册、读档重新禁用与诊断共用这份清单。</para>
    /// </summary>
    public static class ModeSystems
    {
        // === Public API ===

        #region Public API

        /// <summary>按本会话的生效配置登记本模式的替换声明。</summary>
        public static void Declare(ReplacementRegistrar r, AppliedConfig c)
        {
            const string cellMap = ReplacementRegistrar.GroupCellMap;
            const string household = AppliedConfig.GroupHouseholdProperty;
            const SystemUpdatePhase sim = SystemUpdatePhase.GameSimulation;

            // === CellMapSystem<T> 替换 ===
            r.Replace<AirPollutionSystemMod>(cellMap, typeof(Game.Simulation.AirPollutionSystem))
                .At(sim).WithPatches(typeof(AirPollutionSystemMod.Patches));
            r.Replace<AvailabilityInfoToGridSystemMod>(cellMap, typeof(Game.Simulation.AvailabilityInfoToGridSystem))
                .At(sim).WithPatches(typeof(AvailabilityInfoToGridSystemMod.Patches));
            r.Replace<GroundPollutionSystemMod>(cellMap, typeof(Game.Simulation.GroundPollutionSystem))
                .At(sim).WithPatches(typeof(GroundPollutionSystemMod.Patches));
            r.Replace<GroundWaterSystemMod>(cellMap, typeof(Game.Simulation.GroundWaterSystem))
                .At(sim).WithPatches(typeof(GroundWaterSystemMod.Patches));
            r.Replace<NaturalResourceSystemMod>(cellMap, typeof(Game.Simulation.NaturalResourceSystem))
                .At(sim).At(SystemUpdatePhase.EditorSimulation).WithPostDeserialize()
                .WithPatches(typeof(NaturalResourceSystemMod.Patches));
            r.Replace<NoisePollutionSystemMod>(cellMap, typeof(Game.Simulation.NoisePollutionSystem))
                .At(sim).WithPatches(typeof(NoisePollutionSystemMod.Patches));
            r.Replace<PopulationToGridSystemMod>(cellMap, typeof(Game.Simulation.PopulationToGridSystem))
                .At(sim).WithPatches(typeof(PopulationToGridSystemMod.Patches));
            // 原版未注册 GameSimulation（无下游消费者），只保留 Deserialize＋补丁以确保 CellMap 扩容
            r.Replace<SoilWaterSystemMod>(cellMap, typeof(Game.Simulation.SoilWaterSystem))
                .WithPostDeserialize().WithPatches(typeof(SoilWaterSystemMod.Patches));
            r.Replace<TerrainAttractivenessSystemMod>(cellMap, typeof(Game.Simulation.TerrainAttractivenessSystem))
                .At(sim).WithPatches(typeof(TerrainAttractivenessSystemMod.Patches));
            r.Replace<TrafficAmbienceSystemMod>(cellMap, typeof(Game.Simulation.TrafficAmbienceSystem))
                .At(sim).WithPatches(typeof(TrafficAmbienceSystemMod.Patches));
            r.Replace<ZoneAmbienceSystemMod>(cellMap, typeof(Game.Simulation.ZoneAmbienceSystem))
                .At(sim).WithPatches(typeof(ZoneAmbienceSystemMod.Patches));
            r.Replace<LandValueSystemMod>(cellMap, typeof(Game.Simulation.LandValueSystem))
                .At(sim).WithPatches(typeof(LandValueSystemMod.Patches));

            // === 找房与租金（C1/D1）===
            if (c.EconomyFix && c.HouseholdProperty)
            {
                // FindTargets 补丁先于两个系统注册，保持原有顺序
                r.Replace<HouseholdFindPropertySystemMod>(household, typeof(Game.Simulation.HouseholdFindPropertySystem))
                    .WithPatches(typeof(PathfindSetupSystem_FindTargets_Patch)).At(sim);
                r.Replace<RentAdjustSystemMod>(household, typeof(Game.Simulation.RentAdjustSystem))
                    .At(sim);
            }
            else
            {
                // CellOnly：经济总开关或找房组关闭时只修正 CellMap 尺寸。
                // 声明自带原版禁用——原先这里漏了，原版与 CellOnly 同时运行（审查报告 P0-1）
                r.Replace<HouseholdFindPropertySystemMod_CellOnly>(cellMap, typeof(Game.Simulation.HouseholdFindPropertySystem))
                    .At(sim);
                r.Replace<RentAdjustSystemMod_CellOnly>(cellMap, typeof(Game.Simulation.RentAdjustSystem))
                    .At(sim);
            }

            // === 需求 Job 置换 ===
            if (c.EconomyFix && c.Demand)
                r.DemandJobs(JobPatchDefinitions.GetEcoSystemTargets(c.Mode));
        }

        #endregion
    }
}
