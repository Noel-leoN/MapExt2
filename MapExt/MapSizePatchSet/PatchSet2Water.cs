// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using Game.Simulation;
using HarmonyLib;

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
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_CellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_CellSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_WaveSpeed")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_WaveSpeed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.get_WaveSpeed");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSystem.GetDepth))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetDepth_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.GetDepth");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("HasWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HasWater_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.HasWater");
            // This method calls GetCell which uses GetCellCoords which uses mapSize.
            // Patching HasWater ensures the correct mapSize is passed down.
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

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

        [HarmonyPatch("ResetToLevel")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetToLevel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.ResetToLevel");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // Internal helper method uses kMapSize directly
        [HarmonyPatch("BorderCircleIntersection")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BorderCircleIntersection_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.BorderCircleIntersection");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("SourceStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.SourceStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("ResetActive")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetActive_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.ResetActive");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("EvaporateStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.EvaporateStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("VelocityStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.VelocityStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("DepthStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.DepthStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }


        [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.CalculateSourceMultiplier))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CalculateSourceMultiplier_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.CalculateSourceMultiplier");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
        // 注意序列化/反序列化是否需要修补

    }


}
