// Copyright (c) 2024 Noel2(Noel-leoN)
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
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.AvailabilityInfoToGridSystem>().Enabled =
                    false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.GroundPollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.GroundWaterSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.NaturalResourceSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.NoisePollutionSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.PopulationToGridSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.SoilWaterSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TerrainAttractivenessSystem>().Enabled =
                    false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TrafficAmbienceSystem>().Enabled = false;
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ZoneAmbienceSystem>().Enabled = false;

                // v2.2.1版本起强制替换
                // 经济系统 ECS替换
                // if (setting.isEnableEconomyFix == true) // For Large Maps, enforcement is required
                {
                    // ResidentialDemand/CommercialDemandSystem/IndustrialDemand/RentAdjust 采用Job通用替换修补，无需禁用原系统

                    if (setting.isEnableEconomyFix && setting.EnableHouseholdPropertyEcoSystem)
                    {
                        // updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdSpawnSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.RentAdjustSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdFindPropertySystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdBehaviorSystem>().Enabled = false;
                    }

                    if (setting.isEnableEconomyFix && setting.EnableJobSearchEcoSystem)
                    {
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;
                    }

                    if (setting.isEnableEconomyFix && setting.EnableResourceBuyerEcoSystem)
                    {
                        // 寻路优化系统 ECS替换
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TripNeededSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ServiceCoverageSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ResourceBuyerSystem>().Enabled = false;

                        // Harmony修补：拦截 Game.Tools 等外部系统对 SetupPathfindMethods 的调用
                        globalPatcher.CreateClassProcessor(typeof(ModeA.ServiceCoverageSystem_SetupPathfindMethods_Patch)).Patch();
                    }

                    if (setting.isEnableEconomyFix && setting.EnableResidentAIEcoSystem)
                    {
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ResidentAISystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ResidentAISystem.Actions>().Enabled = false;
                    }
                }
            }

            // 57km ModeA
            if (PatchManager.CurrentCoreValue == 4)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<ModeA.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ModeA.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<ModeA.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<ModeA.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ModeA.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ModeA.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ModeA.SoilWaterSystemMod>>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ModeA.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ModeA.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeA.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ModeA.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeA.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                if (setting.isEnableEconomyFix)
                {
                    if (setting.EnableHouseholdPropertyEcoSystem)
                    {
                        // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                        globalPatcher.CreateClassProcessor(typeof(ModeA.PathfindSetupSystem_FindTargets_Patch)).Patch();
                        updateSystem.UpdateAt<ModeA.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeA.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeA.RentAdjustSystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                    else
                    {
                        updateSystem.UpdateAt<ModeA.HouseholdFindPropertySystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeA.RentAdjustSystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableJobSearchEcoSystem)
                    {
                        updateSystem.UpdateAt<ModeA.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeA.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResourceBuyerEcoSystem)
                    {
                        // 寻路优化系统
                        updateSystem.UpdateAt<ModeA.TripNeededSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeA.ServiceCoverageSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeA.ResourceBuyerSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResidentAIEcoSystem)
                    {
                        updateSystem.UpdateAt<ModeA.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAfter<ModeA.ResidentAISystemMod.Actions, ModeA.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                }
                else
                {
                    updateSystem.UpdateAt<ModeA.HouseholdFindPropertySystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                    updateSystem.UpdateAt<ModeA.RentAdjustSystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                }

                if (setting.isEnableEconomyFix && setting.EnableDemandEcoSystem)
                {
                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher, JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.ModeA));
                }
            }

            // 28km ModeB
            if (PatchManager.CurrentCoreValue == 2)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<ModeB.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ModeB.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<ModeB.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<ModeB.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ModeB.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ModeB.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ModeB.SoilWaterSystemMod>>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ModeB.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ModeB.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeB.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ModeB.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeB.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================

                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                if (setting.isEnableEconomyFix)
                {
                    if (setting.EnableHouseholdPropertyEcoSystem)
                    {
                        // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                        globalPatcher.CreateClassProcessor(typeof(ModeB.PathfindSetupSystem_FindTargets_Patch)).Patch();
                        updateSystem.UpdateAt<ModeB.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeB.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeB.RentAdjustSystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                    else
                    {
                        updateSystem.UpdateAt<ModeB.HouseholdFindPropertySystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeB.RentAdjustSystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableJobSearchEcoSystem)
                    {
                        updateSystem.UpdateAt<ModeB.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeB.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResourceBuyerEcoSystem)
                    {
                        // 寻路优化系统
                        updateSystem.UpdateAt<ModeB.TripNeededSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeB.ServiceCoverageSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeB.ResourceBuyerSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResidentAIEcoSystem)
                    {
                        updateSystem.UpdateAt<ModeB.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAfter<ModeB.ResidentAISystemMod.Actions, ModeB.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                }
                else
                {
                    updateSystem.UpdateAt<ModeB.HouseholdFindPropertySystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                    updateSystem.UpdateAt<ModeB.RentAdjustSystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                }

                if (setting.isEnableEconomyFix && setting.EnableDemandEcoSystem)
                {
                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher, JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.ModeB));
                }
            }

            // 114km ModeC
            if (PatchManager.CurrentCoreValue == 8)
            {
                // ==================================================
                // --- CellMapSystem<T> ECS替换 ---
                // ==================================================
                updateSystem.UpdateAt<ModeC.AirPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.AirPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.AvailabilityInfoToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ModeC.AvailabilityInfoToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.GroundPollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.GroundPollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.GroundWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.GroundWaterSystemMod.Patches))
                    .Patch();


                updateSystem.UpdateAt<ModeC.NaturalResourceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                updateSystem.UpdateAt<ModeC.NaturalResourceSystemMod>(SystemUpdatePhase
                    .EditorSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ModeC.NaturalResourceSystemMod>>(
                    SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ModeC.NaturalResourceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.NoisePollutionSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.NoisePollutionSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.PopulationToGridSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.PopulationToGridSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.SoilWaterSystemMod>(SystemUpdatePhase.GameSimulation);
                updateSystem.UpdateAfter<PostDeserialize<ModeC.SoilWaterSystemMod>>(SystemUpdatePhase.Deserialize);
                globalPatcher.CreateClassProcessor(typeof(ModeC.SoilWaterSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.TerrainAttractivenessSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher
                    .CreateClassProcessor(typeof(ModeC.TerrainAttractivenessSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.TrafficAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.TrafficAmbienceSystemMod.Patches))
                    .Patch();

                updateSystem.UpdateAt<ModeC.ZoneAmbienceSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.ZoneAmbienceSystemMod.Patches))
                    .Patch();

                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ModeC.LandValueSystemMod>(SystemUpdatePhase.GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeC.LandValueSystemMod.Patches))
                    .Patch();
                // updateSystem.UpdateAt<LandValueConfigSyncSystem>(SystemUpdatePhase.GameSimulation);
                // ======================================================


                // ======================
                // --- 经济系统 ECS替换 ---
                // ======================

                if (setting.isEnableEconomyFix)
                {
                    if (setting.EnableHouseholdPropertyEcoSystem)
                    {
                        // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                        globalPatcher.CreateClassProcessor(typeof(ModeC.PathfindSetupSystem_FindTargets_Patch)).Patch();
                        updateSystem.UpdateAt<ModeC.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeC.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeC.RentAdjustSystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                    else
                    {
                        updateSystem.UpdateAt<ModeC.HouseholdFindPropertySystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeC.RentAdjustSystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableJobSearchEcoSystem)
                    {
                        updateSystem.UpdateAt<ModeC.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeC.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResourceBuyerEcoSystem)
                    {
                        // 寻路优化系统
                        updateSystem.UpdateAt<ModeC.TripNeededSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeC.ServiceCoverageSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeC.ResourceBuyerSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResidentAIEcoSystem)
                    {
                        updateSystem.UpdateAt<ModeC.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAfter<ModeC.ResidentAISystemMod.Actions, ModeC.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                }
                else
                {
                    updateSystem.UpdateAt<ModeC.HouseholdFindPropertySystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                    updateSystem.UpdateAt<ModeC.RentAdjustSystemMod_CellOnly>(SystemUpdatePhase.GameSimulation);
                }

                if (setting.isEnableEconomyFix && setting.EnableDemandEcoSystem)
                {
                    // Job通用替换修补ResidentialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher, JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.ModeC));
                }
            }

            // vanilla ModeE (None)
            if (PatchManager.CurrentCoreValue == 1)
            {
                // === 原系统禁用 ===
                updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.LandValueSystem>().Enabled = false;

                // === 自定义系统启===
                // --- LandValueSystemRemake + UI设置 ---
                updateSystem.UpdateAt<ModeE.LandValueSystemMod>(SystemUpdatePhase
                    .GameSimulation);
                globalPatcher.CreateClassProcessor(typeof(ModeE.LandValueSystemMod.Patches))
                    .Patch();

                if (setting.isEnableEconomyFix)
                {
                    if (setting.EnableHouseholdPropertyEcoSystem)
                    {
                        // === 原系统禁用 === (Economy)
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.RentAdjustSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdFindPropertySystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.HouseholdBehaviorSystem>().Enabled = false;

                        // HarmonyPrefix修补CitizenPathfindSetup.SetupFindHomeJob(HouseholdFindPropertySystem关联)
                        globalPatcher.CreateClassProcessor(typeof(ModeE.PathfindSetupSystem_FindTargets_Patch)).Patch();

                        updateSystem.UpdateAt<ModeE.HouseholdFindPropertySystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeE.HouseholdBehaviorSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeE.RentAdjustSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableJobSearchEcoSystem)
                    {
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;

                        updateSystem.UpdateAt<ModeE.CitizenFindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeE.FindJobSystemMod>(SystemUpdatePhase.GameSimulation);
                    }

                    if (setting.EnableResourceBuyerEcoSystem)
                    {
                        // 寻路优化系统
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.TripNeededSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ServiceCoverageSystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ResourceBuyerSystem>().Enabled = false;

                        updateSystem.UpdateAt<ModeE.TripNeededSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeE.ServiceCoverageSystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAt<ModeE.ResourceBuyerSystemMod>(SystemUpdatePhase.GameSimulation);

                        // Harmony修补：拦截 Game.Tools 等外部系统对 SetupPathfindMethods 的调用
                        globalPatcher.CreateClassProcessor(typeof(ModeE.ServiceCoverageSystem_SetupPathfindMethods_Patch)).Patch();
                    }

                    if (setting.EnableResidentAIEcoSystem)
                    {
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ResidentAISystem>().Enabled = false;
                        updateSystem.World.GetOrCreateSystemManaged<Game.Simulation.ResidentAISystem.Actions>().Enabled = false;
                        updateSystem.UpdateAt<ModeE.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                        updateSystem.UpdateAfter<ModeE.ResidentAISystemMod.Actions, ModeE.ResidentAISystemMod>(SystemUpdatePhase.GameSimulation);
                    }
                }

                if (setting.isEnableEconomyFix && setting.EnableDemandEcoSystem)
                {
                    // Job通用替换修补ResidentialDemand/CommerialDemand/IndustrialDemand/RentAdjust
                    JobPatchHelper.Apply(globalPatcher, JobPatchDefinitions.GetEcoSystemTargets(PatchModeSetting.None));
                }
            }
        }
    }
}



