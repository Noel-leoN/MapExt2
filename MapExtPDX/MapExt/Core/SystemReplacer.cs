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
                updateSystem.UpdateAt<MapExtPDX.ModeA.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeA.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<MapExtPDX.ModeA.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<MapExtPDX.ModeA.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<MapExtPDX.ModeA.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExtPDX.ModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeA.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeA.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExtPDX.ModeA.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                // if (setting.isEnableEconomyFix == true)
                {
                    //globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExtPDX.ModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExtPDX.ModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    globalPatcher
                        .CreateClassProcessor(
                            typeof(MapExtPDX.ModeA.PathfindSetupSystem_FindTargets_Patch)).Patch();
                    updateSystem.UpdateAt<MapExtPDX.ModeA.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeA.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeA.CitizenFindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeA.FindJobSystemMod>(SystemUpdatePhase
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
                updateSystem.UpdateAt<MapExtPDX.ModeB.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeB.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<MapExtPDX.ModeB.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<MapExtPDX.ModeB.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<MapExtPDX.ModeB.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExtPDX.ModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeB.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeB.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExtPDX.ModeB.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeB.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                // if (setting.isEnableEconomyFix == true)
                {
                    //globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExtPDX.ModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExtPDX.ModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    globalPatcher
                        .CreateClassProcessor(
                            typeof(MapExtPDX.ModeB.PathfindSetupSystem_FindTargets_Patch)).Patch();
                    updateSystem.UpdateAt<MapExtPDX.ModeB.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeB.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeB.CitizenFindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeB.FindJobSystemMod>(SystemUpdatePhase
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
                updateSystem.UpdateAt<MapExtPDX.ModeC.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeC.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<MapExtPDX.ModeC.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<MapExtPDX.ModeC.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<MapExtPDX.ModeC.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                //updateSystem.UpdateAfter<MapExtPDX.ModeA.SoilWaterSystemMod>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeC.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<MapExtPDX.ModeC.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<MapExtPDX.ModeC.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeC.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================


                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                // if (setting.isEnableEconomyFix == true)
                {
                    //globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeA.CommercialDemandSystemMod.Patches)).Patch();
                    //updateSystem.UpdateAt<MapExtPDX.ModeA.CommercialDemandSystemMod>(SystemUpdatePhase.GameSimulation);

                    // 暂不替换HouseholdSpawnSystem
                    // updateSystem.UpdateAt<MapExtPDX.ModeA.HouseholdSpawnSystemMod>(SystemUpdatePhase.GameSimulation);
                    // Mod.Info($"已执行{nameof(HouseholdSpawnSystemMod)}.");

                    // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                    globalPatcher
                        .CreateClassProcessor(
                            typeof(MapExtPDX.ModeC.PathfindSetupSystem_FindTargets_Patch)).Patch();
                    updateSystem.UpdateAt<MapExtPDX.ModeC.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeC.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeC.CitizenFindJobSystemMod>(SystemUpdatePhase
                        .GameSimulation);

                    updateSystem.UpdateAt<MapExtPDX.ModeC.FindJobSystemMod>(SystemUpdatePhase
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
                updateSystem.UpdateAt<MapExtPDX.ModeE.LandValueSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(MapExtPDX.ModeE.LandValueSystemMod.Patches))
                    .Patch();

                // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                globalPatcher
                    .CreateClassProcessor(typeof(MapExtPDX.ModeE.PathfindSetupSystem_FindTargets_Patch))
                    .Patch();
                updateSystem.UpdateAt<MapExtPDX.ModeE.HouseholdFindPropertySystemMod>(SystemUpdatePhase
                    .GameSimulation);

                updateSystem.UpdateAt<MapExtPDX.ModeE.HouseholdBehaviorSystemMod>(SystemUpdatePhase
                    .GameSimulation);

                updateSystem.UpdateAt<MapExtPDX.ModeE.CitizenFindJobSystemMod>(SystemUpdatePhase
                    .GameSimulation);

                updateSystem.UpdateAt<MapExtPDX.ModeE.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);

                // Job通用替换修补ResidentialDemand/CommerialDemand/IndustrialDemand/RentAdjust
                JobPatchHelper.Apply(globalPatcher, JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.None));
            }
        }
    }
}



