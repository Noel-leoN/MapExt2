﻿// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using System.Collections.Generic;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.MapExt.ReBurstSystem.Core
{
    /// <summary>
    /// 定义单个替换目标的数据结
    /// </summary>
    public class JobPatchTarget
    {
        public string TargetAssemblyName { get; set; }
        public string TargetTypeName { get; set; }
        public string TargetMethodName { get; set; }
        public string OriginalJobFullName { get; set; }
        public string ReplacementJobFullName { get; set; }
        public string[] MethodParamTypes { get; set; } // 支持重载

        public JobPatchTarget(
            string targetType,
            string targetMethod,
            string[] methodParams,
            string originalJob,
            string replacementJobBaseName,
            string replacementNamespacePattern,
            int currentCoreValueForMode,
            string modeIdentifier,
            string targetAsmHint = null)
        {
            TargetAssemblyName = targetAsmHint;
            TargetTypeName = targetType;
            TargetMethodName = targetMethod;
            MethodParamTypes = methodParams;

            // 自动替换 / + 以适应嵌套类反射语
            OriginalJobFullName = originalJob.Replace('/', '+');

            // 格式化替换目标的命名空间
            string finalReplacementNamespace = replacementNamespacePattern
                .Replace("{MODE_PLACEHOLDER}", modeIdentifier)
                .Replace("{CORE_VALUE_PLACEHOLDER}", currentCoreValueForMode.ToString());

            ReplacementJobFullName = $"{finalReplacementNamespace}.{replacementJobBaseName}".Replace('/', '+');
        }
    }

    /// <summary>
    /// 静态定义所有需要替换的Job列表
    /// </summary>
    public static class JobPatchDefinitions
    {
        // Name Patterns
        private const string PatternCellSystem = "MapExtPDX.MapExt.ReBurstSystemMode{MODE_PLACEHOLDER}";
        private const string PatternEcoSystem = "MapExtPDX.MapExt.ReBurstEcoSystemMode{MODE_PLACEHOLDER}";

        private class BaseJobTargetInfo
        {
            public string TargetType { get; }
            public string TargetMethod { get; }
            public string OriginalJob { get; }
            public string ReplacementJobBaseName { get; }
            public string[] MethodParamTypes { get; }

            public BaseJobTargetInfo(
                string targetType,
                string targetMethod,
                string originalJob,
                string replacementJobBaseName,
                string[] methodParams = null)
            {
                TargetType = targetType;
                TargetMethod = targetMethod;
                OriginalJob = originalJob;
                ReplacementJobBaseName = replacementJobBaseName;
                MethodParamTypes = methodParams;
            }
        }

        // =========================================================================================
        // LIST A: CellMapSystem 相关
        // =========================================================================================
        private static readonly List<BaseJobTargetInfo> CellSystemTargets = new List<BaseJobTargetInfo>
        {
             // ===== ReBurstCellMapClosed2 =====
            new BaseJobTargetInfo("Game.Simulation.TelecomCoverageSystem", "OnUpdate", "Game.Simulation.TelecomCoverageSystem/TelecomCoverageJob", "TelecomCoverageJob"),
            new BaseJobTargetInfo("Game.Tools.TelecomPreviewSystem", "OnUpdate", "Game.Simulation.TelecomCoverageSystem/TelecomCoverageJob", "TelecomCoverageJob"),
            new BaseJobTargetInfo("Game.Simulation.WindSystem", "OnUpdate", "Game.Simulation.WindSystem/WindCopyJob", "WindCopyJob"),
            new BaseJobTargetInfo("Game.Simulation.WindSimulationSystem", "OnUpdate", "Game.Simulation.WindSimulationSystem/UpdateWindVelocityJob", "UpdateWindVelocityJob"),
            new BaseJobTargetInfo("Game.Simulation.WindSimulationSystem", "OnUpdate", "Game.Simulation.WindSimulationSystem/UpdatePressureJob", "UpdatePressureJob"),

            // ===== ReBurstCellMapRef =====
            new BaseJobTargetInfo("Game.Simulation.AttractionSystem", "OnUpdate", "Game.Simulation.AttractionSystem/AttractivenessJob", "AttractivenessJob"),
            new BaseJobTargetInfo("Game.Audio.AudioGroupingSystem", "OnUpdate", "Game.Audio.AudioGroupingSystem/AudioGroupingJob", "AudioGroupingJob"),
            new BaseJobTargetInfo("Game.Simulation.CarNavigationSystem+Actions", "OnUpdate", "Game.Simulation.CarNavigationSystem/ApplyTrafficAmbienceJob", "ApplyTrafficAmbienceJob"),
            new BaseJobTargetInfo("Game.Simulation.GroundWaterPollutionSystem", "OnUpdate", "Game.Simulation.GroundWaterPollutionSystem/PolluteGroundWaterJob", "PolluteGroundWaterJob"),
            new BaseJobTargetInfo("Game.UI.Tooltip.LandValueTooltipSystem", "OnUpdate", "Game.UI.Tooltip.LandValueTooltipSystem/LandValueTooltipJob", "LandValueTooltipJob"),
            new BaseJobTargetInfo("Game.Simulation.ObjectPolluteSystem", "OnUpdate", "Game.Simulation.ObjectPolluteSystem/ObjectPolluteJob", "ObjectPolluteJob"),
            new BaseJobTargetInfo("Game.Simulation.SpawnableAmbienceSystem", "OnUpdate", "Game.Simulation.SpawnableAmbienceSystem/SpawnableAmbienceJob", "SpawnableAmbienceJob"),
            new BaseJobTargetInfo("Game.Simulation.SpawnableAmbienceSystem", "OnUpdate", "Game.Simulation.SpawnableAmbienceSystem/EmitterAmbienceJob", "EmitterAmbienceJob"),
            new BaseJobTargetInfo("Game.Simulation.ZoneSpawnSystem", "OnUpdate", "Game.Simulation.ZoneSpawnSystem/EvaluateSpawnAreas", "EvaluateSpawnAreas"),

            // ===== ReBurstCellMapRef2 (CitizenHappiness) =====
            new BaseJobTargetInfo("Game.Simulation.CitizenHappinessSystem", "OnUpdate", "Game.Simulation.CitizenHappinessSystem/CitizenHappinessJob", "CitizenHappinessJob"),
            new BaseJobTargetInfo("Game.UI.InGame.AverageHappinessSection", "OnUpdate", "Game.UI.InGame.AverageHappinessSection/CountHappinessJob", "CountHappinessJob"),
            new BaseJobTargetInfo("Game.UI.InGame.AverageHappinessSection", "OnUpdate", "Game.UI.InGame.AverageHappinessSection/CountDistrictHappinessJob", "CountDistrictHappinessJob"),
            new BaseJobTargetInfo("Game.UI.InGame.PollutionInfoviewUISystem", "PerformUpdate", "Game.UI.InGame.PollutionInfoviewUISystem/CalculateAveragePollutionJob", "CalculateAveragePollutionJob"),
            new BaseJobTargetInfo("Game.Simulation.PollutionTriggerSystem", "OnUpdate", "Game.Simulation.PollutionTriggerSystem/CalculateAverageAirPollutionJob", "CalculateAverageAirPollutionJob"),

            // ===== ReBurstCellMapSub =====
            new BaseJobTargetInfo("Game.Rendering.NetColorSystem", "OnUpdate", "Game.Rendering.NetColorSystem/UpdateEdgeColorsJob", "UpdateEdgeColorsJob"),
            new BaseJobTargetInfo("Game.Tools.ValidationSystem", "OnUpdate", "Game.Tools.ValidationSystem/ValidationJob", "ValidationJob"),
            new BaseJobTargetInfo("Game.Simulation.PowerPlantAISystem", "OnUpdate", "Game.Simulation.PowerPlantAISystem/PowerPlantTickJob", "PowerPlantTickJob"),
            new BaseJobTargetInfo("Game.Simulation.WaterPumpingStationAISystem", "OnUpdate", "Game.Simulation.WaterPumpingStationAISystem/PumpTickJob", "PumpTickJob"),
            new BaseJobTargetInfo("Game.UI.Tooltip.TempWaterPumpingTooltipSystem", "OnUpdate", "Game.UI.Tooltip.TempWaterPumpingTooltipSystem/TempJob", "TempJob"),
            new BaseJobTargetInfo("Game.UI.Tooltip.TempWaterPumpingTooltipSystem", "OnUpdate", "Game.UI.Tooltip.TempWaterPumpingTooltipSystem/GroundWaterPumpJob", "GroundWaterPumpJob"),
            new BaseJobTargetInfo("Game.UI.Tooltip.TempWaterPumpingTooltipSystem", "OnUpdate", "Game.UI.Tooltip.TempWaterPumpingTooltipSystem/GroundWaterReservoirJob", "GroundWaterReservoirJob"),

            // ===== MapTile =====
            new BaseJobTargetInfo("Game.Tools.AreaToolSystem", "UpdateDefinitions", "Game.Tools.AreaToolSystem/CreateDefinitionsJob", "CreateDefinitionsJob"),
            new BaseJobTargetInfo("Game.Areas.MapTileSystem", "LegacyGenerateMapTiles", "Game.Areas.MapTileSystem/GenerateMapTilesJob", "GenerateMapTilesJob"),

            // ===== WaterSystem =====
            new BaseJobTargetInfo("Game.Simulation.FloodCheckSystem", "OnUpdate", "Game.Simulation.FloodCheckSystem/FloodCheckJob", "FloodCheckJob"),
            new BaseJobTargetInfo("Game.Simulation.WaterDangerSystem", "OnUpdate", "Game.Simulation.WaterDangerSystem/WaterDangerJob", "WaterDangerJob"),
            new BaseJobTargetInfo("Game.Simulation.WaterLevelChangeSystem", "OnUpdate", "Game.Simulation.WaterLevelChangeSystem/WaterLevelChangeJob", "WaterLevelChangeJob"),
            new BaseJobTargetInfo("Game.Audio.WeatherAudioSystem", "OnUpdate", "Game.Audio.WeatherAudioSystem/WeatherAudioJob", "WeatherAudioJob"),
        };

        // =========================================================================================
        // LIST B: Economy System 相关
        // =========================================================================================
        private static readonly List<BaseJobTargetInfo> EcoSystemTargets = new List<BaseJobTargetInfo>
        {
            new BaseJobTargetInfo("Game.Simulation.RentAdjustSystem", "OnUpdate", "Game.Simulation.RentAdjustSystem/AdjustRentJob", "AdjustRentJob"),
            new BaseJobTargetInfo("Game.Simulation.ResidentialDemandSystem", "OnUpdate", "Game.Simulation.ResidentialDemandSystem/UpdateResidentialDemandJob", "UpdateResidentialDemandJob"),
            new BaseJobTargetInfo("Game.Simulation.IndustrialDemandSystem", "OnUpdate", "Game.Simulation.IndustrialDemandSystem/UpdateIndustrialDemandJob", "UpdateIndustrialDemandJob"),
            new BaseJobTargetInfo("Game.Simulation.CommercialDemandSystem", "OnUpdate", "Game.Simulation.CommercialDemandSystem/UpdateCommercialDemandJob", "UpdateCommercialDemandJob"),
        };

        // =========================================================================================
        // PUBLIC API
        // =========================================================================================

        public static List<JobPatchTarget> GetCellSystemTargets(PatchModeSetting mode)
        {
            return GenerateConcreteTargets(CellSystemTargets, mode, PatternCellSystem);
        }

        public static List<JobPatchTarget> GetEcoSystemTargets(PatchModeSetting mode)
        {
            return GenerateConcreteTargets(EcoSystemTargets, mode, PatternEcoSystem);
        }

        private static List<JobPatchTarget> GenerateConcreteTargets(List<BaseJobTargetInfo> baseList, PatchModeSetting mode, string pattern)
        {
            var results = new List<JobPatchTarget>();
            if (mode == PatchModeSetting.None && pattern == PatternCellSystem) return results; // CellSystem patches ignored in Vanilla

            // Note: Eco patches might be needed even in Vanilla (ModeE) if enabled.

            int coreValue = PatchManager.CurrentCoreValue;
            string modeIdentifier = GetModeIdentifier(mode);

            // Mod.Info($"[Core] Generating targets for {mode} ({modeIdentifier}). Pattern: {pattern}");

            foreach (var baseInfo in baseList)
            {
                results.Add(new JobPatchTarget(
                    baseInfo.TargetType,
                    baseInfo.TargetMethod,
                    baseInfo.MethodParamTypes,
                    baseInfo.OriginalJob,
                    baseInfo.ReplacementJobBaseName,
                    pattern,
                    coreValue,
                    modeIdentifier
                ));
            }
            return results;
        }

        private static string GetModeIdentifier(PatchModeSetting mode)
        {
            switch (mode)
            {
                case PatchModeSetting.ModeA: return "A";
                case PatchModeSetting.ModeB: return "B";
                case PatchModeSetting.ModeC: return "C";
                case PatchModeSetting.None: return "E"; // E for Eco-Only / None
                default: return "A";
            }
        }
    }
}
