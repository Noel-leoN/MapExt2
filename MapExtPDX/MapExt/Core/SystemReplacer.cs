﻿// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Game;
using Game.Serialization;
using HarmonyLib;
using MapExtPDX.MapExt.ReBurstSystem.Core;

namespace MapExtPDX.MapExt.Core
{
    public static class SystemReplacer
    {
        public static void Apply(UpdateSystem updateSystem, Harmony globalPatcher, ModSettings setting)
        {
            // Part 1:
            // --- CellMapSystem<T> ECS替换 ---
            // Telecom/Wind 等系统暂不替换

            // Part 2:
            // --- 找房/购物/找工作系统优化替换 ---
            // ECS替换HouseholdSpawnSystem
            // ECS替换HouseholdFindPropertySystem
            // ECS替换HouseholdBehaviorSystem
            // ECS替换FindJobSystem
            // ECS替换CitizenFindJobSystem

            // --- 住工商需求和租金系统优化替换 ---
            // Job通用替换修补ResidentialDemandSystemRe
            // Job通用替换修补CommerialDemandSystemRe
            // Job通用替换修补IndustrialDemandSystemRe
            // Job通用替换修补RentAdjustSystemRe

            // ModeA/B/C 共用禁用原系统逻辑
            if (PatchManager.CurrentCoreValue != 1)
            {
                // CellMapSystem<T> ECS替换
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.AirPollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.AvailabilityInfoToGridSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.GroundPollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.GroundWaterSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.NaturalResourceSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.NoisePollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.PopulationToGridSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.SoilWaterSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TerrainAttractivenessSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TrafficAmbienceSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ZoneAmbienceSystem>().Enabled = false;

                // v2.2.1版本起强制替换
                // 经济系统 ECS替换
                // if (setting.isEnableEconomyFix == true) // For Large Maps, enforcement is required
                {
                    // ResidentialDemand/CommercialDemandSystem/IndustrialDemand/RentAdjust 采用Job通用替换修补，无需禁用原系统

                    // updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdSpawnSystem>().Enabled = false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdFindPropertySystem>().Enabled =
                        false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdBehaviorSystem>().Enabled =
                        false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
                    updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;
                }

            }

            // 57km ModeA
            if (PatchManager.CurrentCoreValue == 4)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<ReBurstSystemModeA.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstSystemModeA.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<ReBurstSystemModeA.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<ReBurstSystemModeA.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ReBurstSystemModeA.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstSystemModeA.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeA.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ReBurstSystemModeA.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeA.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                // if (setting.isEnableEconomyFix == true)
                {
                    //globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    globalPatcher
                        .CreateClassProcessor(
                            typeof(ReBurstEcoSystemModeA.PathfindSetupSystem_FindTargets_Patch)).Patch();
                    updateSystem.UpdateAt<ReBurstEcoSystemModeA.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeA.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeA.CitizenFindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeA.FindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher,
                        JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.ModeA));
                }
            }

            // 28km ModeB
            if (PatchManager.CurrentCoreValue == 2)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<ReBurstSystemModeB.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstSystemModeB.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<ReBurstSystemModeB.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<ReBurstSystemModeB.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ReBurstSystemModeB.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstSystemModeB.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeB.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ReBurstSystemModeB.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeB.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                // if (setting.isEnableEconomyFix == true)
                {
                    //globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    globalPatcher
                        .CreateClassProcessor(
                            typeof(ReBurstEcoSystemModeB.PathfindSetupSystem_FindTargets_Patch)).Patch();
                    updateSystem.UpdateAt<ReBurstEcoSystemModeB.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeB.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeB.CitizenFindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeB.FindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher,
                        JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.ModeB));
                }
            }

            // 114km ModeC
            if (PatchManager.CurrentCoreValue == 8)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<ReBurstSystemModeC.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstSystemModeC.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<ReBurstSystemModeC.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<ReBurstSystemModeC.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ReBurstSystemModeC.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExt.ReBurstSystemModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstSystemModeC.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ReBurstSystemModeC.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ReBurstSystemModeC.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstSystemModeC.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================


                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                // if (setting.isEnableEconomyFix == true)
                {
                    //globalPatcher.CreateClassProcessor(typeof(MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExt.ReBurstEcoSystemModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExt.ReBurstSystemModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    globalPatcher
                        .CreateClassProcessor(
                            typeof(ReBurstEcoSystemModeC.PathfindSetupSystem_FindTargets_Patch)).Patch();
                    updateSystem.UpdateAt<ReBurstEcoSystemModeC.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeC.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeC.CitizenFindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<ReBurstEcoSystemModeC.FindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher,
                        JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.ModeC));
                }
            }

            // vanilla ModeE (None)
            if (PatchManager.CurrentCoreValue == 1 /*&& setting.isEnableEconomyFix == true*/)
            {
                // === 原系统禁用 ===
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = false;

                // updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdSpawnSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdFindPropertySystem>().Enabled =
                    false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdBehaviorSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;

                // === 自定义系统启===
                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ReBurstEcoSystemModeE.LandValueSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ReBurstEcoSystemModeE.LandValueSystemMod.Patches))
                    .Patch();

                // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                globalPatcher
                    .CreateClassProcessor(typeof(ReBurstEcoSystemModeE.PathfindSetupSystem_FindTargets_Patch))
                    .Patch();
                updateSystem.UpdateAt<ReBurstEcoSystemModeE.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                    .GameSimulation);

                updateSystem.UpdateAt<ReBurstEcoSystemModeE.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                    .GameSimulation);

                updateSystem.UpdateAt<ReBurstEcoSystemModeE.CitizenFindJobSystemMod>(SystemUpdatePhase
                    .GameSimulation);

                updateSystem.UpdateAt<ReBurstEcoSystemModeE.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                // Job通用替换修补ResidentialDemand/CommerialDemand/IndustrialDemand/RentAdjust
                JobPatchHelper.Apply(globalPatcher, JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.None));
            }
        }
    }
}
