// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.Simulation;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace MapExtPDX.MapExt.MapSizePatchSet
{

    [HarmonyPatch(typeof(WaterSystem))]
    static class WaterSystemMethodPatches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(WaterSystemMethodPatches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(WaterSystemMethodPatches)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(WaterSystemMethodPatches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(WaterSystemMethodPatches)}:{message}");

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_MapSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_MapSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_CellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_CellSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_MapSize"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_WaveSpeed")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_WaveSpeed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_WaveSpeed"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.3.6f版本增加
        [HarmonyPatch("get_BackdropCellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_BackdropCellSize(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_BackdropCellSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.CalculateSourceMultiplier))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CalculateSourceMultiplier_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.CalculateSourceMultiplier"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.3.6f版本已弃用，暂时不动
        [HarmonyPatch("HasWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HasWater_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.HasWater"); 
#endif
            // This method calls GetCell which uses GetCellCoords which uses mapSize.
            // Patching HasWater ensures the correct mapSize is passed down.
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.3.6f版本已移除
        /*
        [HarmonyPatch(nameof(WaterSystem.GetDepth))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetDepth_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.GetDepth"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
        */

        /*
        // Essential for initializing SurfaceDataReaders correctly
        [HarmonyPatch("InitTextures")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> InitTextures_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.InitTextures");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
        */

        // 1.3.6f版本变动
        // 以下方法均从WaterSystem移动至WaterSimulation/WaterSimulationLegacy
        /*
        [HarmonyPatch("ResetToLevel")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetToLevel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.ResetToLevel"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // Internal helper method uses kMapSize directly
        [HarmonyPatch("BorderCircleIntersection")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BorderCircleIntersection_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.BorderCircleIntersection"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("SourceStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.SourceStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("ResetActive")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetActive_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.ResetActive"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("EvaporateStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.EvaporateStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("VelocityStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("DepthStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.DepthStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
        */

        // 注意序列化/反序列化是否需要修补

    }

    [HarmonyPatch(typeof(WaterSimulation))]
    static class WaterSimulationMethodPatches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(WaterSimulationMethodPatches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(WaterSimulationMethodPatches)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(WaterSimulationMethodPatches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(WaterSimulationMethodPatches)}:{message}");

        // v1.3.6f版本变动
        [HarmonyPatch(nameof(WaterSimulation.ResetToLevel))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetToLevel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.ResetToLevel"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.SourceStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.SourceStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.EvaporateStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.EvaporateStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.VelocityStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.DepthStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.DepthStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补
    }

    [HarmonyPatch(typeof(WaterSimulationLegacy))]
    static class WaterSimulationLegacyMethodPatches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(WaterSimulationLegacyMethodPatches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(WaterSimulationLegacyMethodPatches)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(WaterSimulationLegacyMethodPatches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(WaterSimulationLegacyMethodPatches)}:{message}");

        // v1.3.6f版本变动      

        [HarmonyPatch(nameof(WaterSimulationLegacy.SourceStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.SourceStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.EvaporateStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.EvaporateStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.VelocityStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.DepthStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.DepthStep"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("BorderCircleIntersection")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BorderCircleIntersection_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.BorderCircleIntersection"); 
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补
    }

    [HarmonyPatch(typeof(WaterLevelChangeSystem))]
    static class WaterLevelChangeSystemMethodPatches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(WaterLevelChangeSystemMethodPatches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(WaterLevelChangeSystemMethodPatches)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(WaterLevelChangeSystemMethodPatches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(WaterLevelChangeSystemMethodPatches)}:{message}");

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_TsunamiEndDelay")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_MapSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
    }

}
