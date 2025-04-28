// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using System.Collections.Generic;

namespace MapExtPDX
{
    public class JobPatchTarget
    {
        public string TargetAssemblyName { get; set; } // e.g., "Game"
        public string TargetTypeName { get; set; }     // e.g., "Game.Simulation.AvailabilityInfoToGridSystem"
        public string TargetMethodName { get; set; }   // e.g., "OnUpdate"
        public string OriginalJobFullName { get; set; } // e.g., "Game.Simulation.AvailabilityInfoToGridSystem/AvailabilityInfoToGridJob" (注意嵌套类型用'/')
                                                        // public string ReplacementJobAssemblyName { get; set; } // e.g., "MapExtPDX(burst job AOT库)" (无后缀名 .dll)
        public string ReplacementJobFullName { get; set; } // e.g., "MapExtPDX.MyCustomJob" // 自定义库中未使用嵌套则用"."
        public bool IsParallelFor { get; set; } // Schedule标志位 

        // public bool IsParallelFor { get; set; }
        // Add other flags if needed (e.g., IsJobChunk, Schedule variant type)

        // 可以添加更多标志来辅助定位 Schedule 方法或处理特殊情况
        // public bool IsParallelFor { get; set; } = true; // 默认为 IJobParallelFor
        // public string ScheduleMethodHint { get; set; } = "Schedule"; // 帮助识别 Schedule 调用

        // 构造函数或其他方法可以简化创建
        public JobPatchTarget(string targetType, string targetMethod, string originalJob, string replacementAsm, string replacementJob, string targetAsmHint = null)
        {
            TargetAssemblyName = targetAsmHint;
            TargetTypeName = targetType;
            TargetMethodName = targetMethod;
            OriginalJobFullName = originalJob.Replace('/', '+'); // Harmony/Reflection uses '+' for nested types
                                                                 // ReplacementJobAssemblyName = replacementAsm;
            ReplacementJobFullName = replacementJob.Replace('/', '+'); // Ensure consistency
        }

        public static List<JobPatchTarget> GetPatchTargets()
        {
            // --- 1. 定义所有需要应用的BurstJob补丁目标 ---
            return new List<JobPatchTarget>
            {
            new JobPatchTarget(
                targetType: "Game.Simulation.AirPollutionSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.AirPollutionSystem/AirPollutionMoveJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.AirPollutionMoveJob" // 替换 Job 的完整类型名
            ),
            // 补丁目标：修改 AvailabilityInfoToGridSystem.OnUpdate
            new JobPatchTarget(
                targetType: "Game.Simulation.AvailabilityInfoToGridSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.AvailabilityInfoToGridSystem/AvailabilityInfoToGridJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.AvailabilityInfoToGridJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.LandValueSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.LandValueSystem/LandValueMapUpdateJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.LandValueMapUpdateJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.NoisePollutionSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.NoisePollutionSystem/NoisePollutionSwapJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.NoisePollutionSwapJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.PopulationToGridSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.PopulationToGridSystem/PopulationToGridJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.PopulationToGridJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.SoilWaterSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.SoilWaterSystem/SoilWaterTickJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.SoilWaterTickJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.TelecomCoverageSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.TelecomCoverageSystem/TelecomCoverageJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.TelecomCoverageJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Tools.TelecomPreviewSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.TelecomCoverageSystem/TelecomCoverageJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.TelecomCoverageJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.TerrainAttractivenessSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.TerrainAttractivenessSystem/TerrainAttractivenessPrepareJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.TerrainAttractivenessPrepareJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.TerrainAttractivenessSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.TerrainAttractivenessSystem/TerrainAttractivenessJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.TerrainAttractivenessJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.WindSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.WindSystem/WindCopyJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.WindCopyJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.AttractionSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.AttractionSystem/AttractivenessJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.AttractivenessJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Audio.AudioGroupingSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Audio.AudioGroupingSystem/AudioGroupingJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.AudioGroupingJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.CarNavigationSystem+Actions",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.CarNavigationSystem/ApplyTrafficAmbienceJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.ApplyTrafficAmbienceJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.CitizenHappinessSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.CitizenHappinessSystem/CitizenHappinessJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.CitizenHappinessJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.GroundWaterPollutionSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.GroundWaterPollutionSystem/PolluteGroundWaterJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.PolluteGroundWaterJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.UI.Tooltip.LandValueTooltipSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.UI.Tooltip.LandValueTooltipSystem/LandValueTooltipJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.LandValueTooltipJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.ObjectPolluteSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.ObjectPolluteSystem/ObjectPolluteJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.ObjectPolluteJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.SpawnableAmbienceSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.SpawnableAmbienceSystem/SpawnableAmbienceJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.SpawnableAmbienceJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.WindSimulationSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.WindSimulationSystem/UpdateWindVelocityJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.UpdateWindVelocityJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.ZoneSpawnSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.ZoneSpawnSystem/EvaluateSpawnAreas", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.EvaluateSpawnAreas" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Tools.AreaToolSystem",
                targetMethod: "UpdateDefinitions",
                originalJob: "Game.Tools.AreaToolSystem/CreateDefinitionsJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.CreateDefinitionsJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Areas.MapTileSystem",
                targetMethod: "LegacyGenerateMapTiles",
                originalJob: "Game.Areas.MapTileSystem/GenerateMapTilesJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.GenerateMapTilesJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.FloodCheckSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.FloodCheckSystem/FloodCheckJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.FloodCheckJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.WaterDangerSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.WaterDangerSystem/WaterDangerJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.WaterDangerJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.WaterLevelChangeSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.WaterLevelChangeSystem/WaterLevelChangeJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.WaterLevelChangeJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Simulation.WaterSourceInitializeSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Simulation.WaterSourceInitializeSystem/InitializeWaterSourcesJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.InitializeWaterSourcesJob" // 替换 Job 的完整类型名
            ),
            new JobPatchTarget(
                targetType: "Game.Audio.WeatherAudioSystem",
                targetMethod: "OnUpdate",
                originalJob: "Game.Audio.WeatherAudioSystem/WeatherAudioJob", // 嵌套类型用 /
                replacementAsm: "MapExtPDX", // 替换 Job 所在的程序集名
                replacementJob: "MapExtPDX.WeatherAudioJob" // 替换 Job 的完整类型名
            ),

            // --- 在这里添加更多的补丁目标 ---
            // 例如，如果要修改另一个 System 的另一个 Job：
            // new JobPatchTarget(
            //     targetAsm: "Game.Simulation.dll", // 假设在同一个程序集
            //     targetType: "Game.Simulation.AnotherSystem",
            //     targetMethod: "AnotherMethodToPatch",
            //     originalJob: "Game.Simulation.AnotherSystem/OriginalJobX",
            //     replacementAsm: "YourMod.CustomJobs", // 可以是同一个，也可以是不同的自定义程序集(only for MonoCecil)
            //     replacementJob: "YourMod.CustomJobs.MyCustomJobX"
            // ),
            // 例如，如果目标在不同的程序集：
            // new JobPatchTarget(
            //     targetAsm: "Game.Rendering.dll",
            //     targetType: "Game.Rendering.RenderingSystem",
            //     targetMethod: "OnUpdate",
            //     originalJob: "Game.Rendering.RenderingSystem/RenderingJob",
            //     replacementAsm: "YourMod.CustomJobs",
            //     replacementJob: "YourMod.CustomJobs.MyRenderingJob"
            // )
            }; // 列表结束；
        } // end of JobList


    } // end of class
} // namespace
