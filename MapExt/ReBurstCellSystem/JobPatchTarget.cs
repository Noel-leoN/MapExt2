// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using System.Collections.Generic;

namespace MapExtPDX.MapExt.ReBurstSystem
{
    public class JobPatchTarget
    {
        public string TargetAssemblyName { get; set; }
        public string TargetTypeName { get; set; }
        public string TargetMethodName { get; set; }        
        public string OriginalJobFullName { get; set; }
        public string ReplacementJobFullName { get; set; }
        public string[] MethodParamTypes { get; set; } // 新增：支持重载

        // 构造函数
        public JobPatchTarget(
            string targetType,
            string targetMethod,
            string[] methodParams, // 新增参数
            string originalJob,
            // string replacementAsm, // 暂未使用，用于跨mod替换
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
            // 自动替换 / 为 + 以适应嵌套类反射语法
            OriginalJobFullName = originalJob.Replace('/', '+'); // Harmony/Reflection uses '+' for nested types
            // ReplacementJobAssemblyName = replacementAsm;

            // 获取ReBurstJob所在命名空间名称；
            string finalReplacementNamespace = replacementNamespacePattern
                .Replace("{MODE_PLACEHOLDER}", modeIdentifier) // 若使用ModeA/B/C/D区分
                .Replace("{CORE_VALUE_PLACEHOLDER}", currentCoreValueForMode.ToString()); // 若使用核心设置值区分
            ReplacementJobFullName = $"{finalReplacementNamespace}.{replacementJobBaseName}".Replace('/', '+');// Ensure consistency
        }

        public static class JobPatchDefinitions
        {
            // --- 定义ReBurstJob基本名称集 ---
            // Job名称的通用部分
            private class BaseJobTargetInfo
            {
                public string TargetType { get; }
                public string TargetMethod { get; }
                public string OriginalJob { get; }
                public string ReplacementJobBaseName { get; }
                public string[] MethodParamTypes { get; } // 新增属性

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

            // 集中定义(对象类完整名/对象方法名/原Job全名/替换Job的不含命名空间的类型名)
            // 注意某些Job被不同的系统调用，因此需要完整列出
            private static readonly List<BaseJobTargetInfo> AllBaseTargets = new List<BaseJobTargetInfo>
    {
        // ===== ReBurstCellMapClosed =====
        // v2.2.0 改为ECS替换

        //new BaseJobTargetInfo(
        //    "Game.Simulation.AirPollutionSystem",
        //    "OnUpdate",
        //    "Game.Simulation.AirPollutionSystem/AirPollutionMoveJob",
        //    "AirPollutionMoveJob"
        //),

        //new BaseJobTargetInfo(
        //    "Game.Simulation.AvailabilityInfoToGridSystem",
        //    "OnUpdate",
        //    "Game.Simulation.AvailabilityInfoToGridSystem/AvailabilityInfoToGridJob",
        //    "AvailabilityInfoToGridJob"
        //),
        
        // v2.2.0 改为ECS替换
        //new BaseJobTargetInfo(
        //    "Game.Simulation.LandValueSystem",
        //    "OnUpdate",
        //    "Game.Simulation.LandValueSystem/LandValueMapUpdateJob",
        //    "LandValueMapUpdateJob"
        //),

        //new BaseJobTargetInfo(
        //    "Game.Simulation.NoisePollutionSystem",
        //    "OnUpdate",
        //    "Game.Simulation.NoisePollutionSystem/NoisePollutionSwapJob",
        //    "NoisePollutionSwapJob"
        //),
        //new BaseJobTargetInfo(
        //    "Game.Simulation.PopulationToGridSystem",
        //    "OnUpdate",
        //    "Game.Simulation.PopulationToGridSystem/PopulationToGridJob",
        //    "PopulationToGridJob"
        //),
        //new BaseJobTargetInfo(
        //    "Game.Simulation.SoilWaterSystem",
        //    "OnUpdate",
        //    "Game.Simulation.SoilWaterSystem/SoilWaterTickJob",
        //    "SoilWaterTickJob"
        //),
        //new BaseJobTargetInfo(
        //    "Game.Simulation.TerrainAttractivenessSystem",
        //    "OnUpdate",
        //    "Game.Simulation.TerrainAttractivenessSystem/TerrainAttractivenessPrepareJob",
        //    "TerrainAttractivenessPrepareJob"
        //),
        //new BaseJobTargetInfo(
        //    "Game.Simulation.TerrainAttractivenessSystem",
        //    "OnUpdate",
        //    "Game.Simulation.TerrainAttractivenessSystem/TerrainAttractivenessJob",
        //    "TerrainAttractivenessJob"
        //),

        // ===== ReBurstCellMapClosed2 =====
        // CellMapSystem<T>派生类 + WindSystem相关
        new BaseJobTargetInfo(
            "Game.Simulation.TelecomCoverageSystem",
            "OnUpdate",
            "Game.Simulation.TelecomCoverageSystem/TelecomCoverageJob",
            "TelecomCoverageJob"
        ),
        new BaseJobTargetInfo(
            "Game.Tools.TelecomPreviewSystem",
            "OnUpdate",
            "Game.Simulation.TelecomCoverageSystem/TelecomCoverageJob",
            "TelecomCoverageJob"
        ),
        
        new BaseJobTargetInfo(
            "Game.Simulation.WindSystem",
            "OnUpdate",
            "Game.Simulation.WindSystem/WindCopyJob",
            "WindCopyJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.WindSimulationSystem",
            "OnUpdate",
            "Game.Simulation.WindSimulationSystem/UpdateWindVelocityJob",
            "UpdateWindVelocityJob"
        ),
        // v2.2.0增加kTextureSize修复
        new BaseJobTargetInfo(
            "Game.Simulation.WindSimulationSystem",
            "OnUpdate",
            "Game.Simulation.WindSimulationSystem/UpdatePressureJob",
            "UpdatePressureJob"
        ),

        // ===== ReBurstCellMapRef =====
        new BaseJobTargetInfo(
            "Game.Simulation.AttractionSystem",
            "OnUpdate",
            "Game.Simulation.AttractionSystem/AttractivenessJob",
            "AttractivenessJob"
        ),
        new BaseJobTargetInfo(
            "Game.Audio.AudioGroupingSystem",
            "OnUpdate",
            "Game.Audio.AudioGroupingSystem/AudioGroupingJob",
            "AudioGroupingJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.CarNavigationSystem+Actions",
            "OnUpdate",
            "Game.Simulation.CarNavigationSystem/ApplyTrafficAmbienceJob",
            "ApplyTrafficAmbienceJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.GroundWaterPollutionSystem",
            "OnUpdate",
            "Game.Simulation.GroundWaterPollutionSystem/PolluteGroundWaterJob",
            "PolluteGroundWaterJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.Tooltip.LandValueTooltipSystem",
            "OnUpdate",
            "Game.UI.Tooltip.LandValueTooltipSystem/LandValueTooltipJob",
            "LandValueTooltipJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.ObjectPolluteSystem",
            "OnUpdate",
            "Game.Simulation.ObjectPolluteSystem/ObjectPolluteJob",
            "ObjectPolluteJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.SpawnableAmbienceSystem",
            "OnUpdate",
            "Game.Simulation.SpawnableAmbienceSystem/SpawnableAmbienceJob",
            "SpawnableAmbienceJob"
        ),
        // v2.2.0版本补遗漏
        new BaseJobTargetInfo(
            "Game.Simulation.SpawnableAmbienceSystem",
            "OnUpdate",
            "Game.Simulation.SpawnableAmbienceSystem/EmitterAmbienceJob",
            "EmitterAmbienceJob"
        ),        
        new BaseJobTargetInfo(
            "Game.Simulation.ZoneSpawnSystem",
            "OnUpdate",
            "Game.Simulation.ZoneSpawnSystem/EvaluateSpawnAreas",
            "EvaluateSpawnAreas"
        ),

        // ========== ReBurstCellMapRef2 ==========
        // CitizenHappinessSystem相关
        // v2.0.2版新增修复
        new BaseJobTargetInfo(
            "Game.Simulation.CitizenHappinessSystem",
            "OnUpdate",
            "Game.Simulation.CitizenHappinessSystem/CitizenHappinessJob",
            "CitizenHappinessJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.InGame.AverageHappinessSection",
            "OnUpdate",
            "Game.UI.InGame.AverageHappinessSection/CountHappinessJob",
            "CountHappinessJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.InGame.AverageHappinessSection",
            "OnUpdate",
            "Game.UI.InGame.AverageHappinessSection/CountDistrictHappinessJob",
            "CountDistrictHappinessJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.InGame.PollutionInfoviewUISystem",
            "PerformUpdate",
            "Game.UI.InGame.PollutionInfoviewUISystem/CalculateAveragePollutionJob",
            "CalculateAveragePollutionJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.PollutionTriggerSystem",
            "OnUpdate",
            "Game.Simulation.PollutionTriggerSystem/CalculateAverageAirPollutionJob",
            "CalculateAverageAirPollutionJob"
        ),

        // ========= ReBurstCellMapSub ===========
        // v2.0.3版新增修复
        new BaseJobTargetInfo(
            "Game.Rendering.NetColorSystem",
            "OnUpdate",
            "Game.Rendering.NetColorSystem/UpdateEdgeColorsJob",
            "UpdateEdgeColorsJob"
        ),
        // v2.1.1新增补遗
        new BaseJobTargetInfo(
            "Game.Tools.ValidationSystem",
            "OnUpdate",
            "Game.Tools.ValidationSystem/ValidationJob",
            "ValidationJob"
        ),
        // v2.0.2版本暂时移除，疑似造成卡顿
        // v2.1.1 mod重新添加(仍需关注反编译代码是否完整)
        new BaseJobTargetInfo(
            "Game.Simulation.PowerPlantAISystem",
            "OnUpdate",
            "Game.Simulation.PowerPlantAISystem/PowerPlantTickJob",
            "PowerPlantTickJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.WaterPumpingStationAISystem",
            "OnUpdate",
            "Game.Simulation.WaterPumpingStationAISystem/PumpTickJob",
            "PumpTickJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.Tooltip.TempWaterPumpingTooltipSystem",
            "OnUpdate",
            "Game.UI.Tooltip.TempWaterPumpingTooltipSystem/TempJob",
            "TempJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.Tooltip.TempWaterPumpingTooltipSystem",
            "OnUpdate",
            "Game.UI.Tooltip.TempWaterPumpingTooltipSystem/GroundWaterPumpJob",
            "GroundWaterPumpJob"
        ),
        new BaseJobTargetInfo(
            "Game.UI.Tooltip.TempWaterPumpingTooltipSystem",
            "OnUpdate",
            "Game.UI.Tooltip.TempWaterPumpingTooltipSystem/GroundWaterReservoirJob",
            "GroundWaterReservoirJob"
        ),

        // ======== MapTile 相关 ========
        new BaseJobTargetInfo(
            "Game.Tools.AreaToolSystem",
            "UpdateDefinitions",
            "Game.Tools.AreaToolSystem/CreateDefinitionsJob",
            "CreateDefinitionsJob"
        ),
        new BaseJobTargetInfo(
            "Game.Areas.MapTileSystem",
            "LegacyGenerateMapTiles",
            "Game.Areas.MapTileSystem/GenerateMapTilesJob",
            "GenerateMapTilesJob"
        ),

        // ======== WaterSystem 相关 ========
        new BaseJobTargetInfo(
            "Game.Simulation.FloodCheckSystem",
            "OnUpdate",
            "Game.Simulation.FloodCheckSystem/FloodCheckJob",
            "FloodCheckJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.WaterDangerSystem",
            "OnUpdate",
            "Game.Simulation.WaterDangerSystem/WaterDangerJob",
            "WaterDangerJob"
        ),
        new BaseJobTargetInfo(
            "Game.Simulation.WaterLevelChangeSystem",
            "OnUpdate",
            "Game.Simulation.WaterLevelChangeSystem/WaterLevelChangeJob",
            "WaterLevelChangeJob"
        ),        
        new BaseJobTargetInfo(
            "Game.Audio.WeatherAudioSystem",
            "OnUpdate",
            "Game.Audio.WeatherAudioSystem/WeatherAudioJob",
            "WeatherAudioJob"
        ),

            // ... add more base targets here ...
    };

            // Define namespace pattern
            // Option 1: Using Mode Letter (A, B, C)
            private const string ReplacementNamespacePatternMode = "MapExtPDX.MapExt.ReBurstSystemMode{MODE_PLACEHOLDER}";
            // Option 2: Using CoreValue directly
            //private const string ReplacementNamespacePatternCV = "MyModName.Patches.CoreValue{CORE_VALUE_PLACEHOLDER}";


            public static List<JobPatchTarget> GetTargetsForMode(PatchModeSetting mode)
            {
                var concreteTargets = new List<JobPatchTarget>();
                int coreValue = PatchManager.CurrentCoreValue;
                string modeIdentifier = GetModeIdentifier(mode); // Helper to get "A", "B", or "CoreValue2" etc.

                // Choose your pattern
                string currentNamespacePattern = ReplacementNamespacePatternMode;
                // string currentNamespacePattern = ReplacementNamespacePatternCV;

                if (mode == PatchModeSetting.None) // No targets for "None"
                {
                    Mod.Info($"[JobPatchDefinitions] No BurstJob targets for 'None' mode.");
                    return concreteTargets;
                }

                Mod.Info($"[JobPatchDefinitions] Generating targets for Mode: {mode}, CoreValue: {coreValue}, Identifier: {modeIdentifier}");

                foreach (var baseInfo in AllBaseTargets)
                {
                    concreteTargets.Add(new JobPatchTarget(
                        baseInfo.TargetType,
                        baseInfo.TargetMethod,
                        baseInfo.MethodParamTypes,
                        baseInfo.OriginalJob,
                        baseInfo.ReplacementJobBaseName,
                        currentNamespacePattern, // Pass the chosen pattern
                        coreValue,
                        modeIdentifier, // Pass the string to fill the placeholder (e.g., "A" or "2")
                        targetAsmHint: null // Or determine this if needed
                    ));
                }

                Mod.Info($"[JobPatchDefinitions] Generated {concreteTargets.Count} concrete targets.");
                return concreteTargets;
            }

            // Helper to get a string identifier for the mode (could be in PatchManager too)
            private static string GetModeIdentifier(PatchModeSetting mode)
            {
                switch (mode)
                {
                    // If using Mode Letters in namespace:
                    case PatchModeSetting.ModeA: return "A";
                    case PatchModeSetting.ModeB: return "B";
                    case PatchModeSetting.ModeC: return "C";
                    case PatchModeSetting.None: return "E";
                    // case PatchModeSetting.ModeD: return "D";
                    // If using CoreValue string in namespace:
                    // case Setting.PatchModeSetting.ModeA: return PatchManager.GetCoreValueForMode(mode).ToString(); // "2"
                    // case Setting.PatchModeSetting.ModeB: return PatchManager.GetCoreValueForMode(mode).ToString(); // "4"
                    default: return "Unknown";
                }
            }
        }

        public static List<JobPatchTarget> GetPatchTargets()
        {
            // Get the current mode from PatchManager (assuming _currentMode is accessible or a getter exists)
            PatchModeSetting currentModeEnum = PatchManager.CurrentMode;
            return JobPatchDefinitions.GetTargetsForMode(currentModeEnum);
        }


    } // end of class
} // namespace
