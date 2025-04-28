// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Collections.Generic;
using Game.Simulation;
using HarmonyLib;

namespace MapExtPDX.Patches
{
    // Patching static methods requires the type and method name
    [HarmonyPatch(typeof(WaterSystem))]
    static class WaterSystem_StaticMethods_Patches
    {
        // --- Log Configuration ---
        private static bool enableDebugLogging = true; // Set to false for release builds

        private static void Log(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Log($"[WaterSystem_Static] {message}");
            }
        }

        [HarmonyPatch("GetCellCoords")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetCellCoords_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Note: GetCellCoords takes mapSize as an argument, usually passed as kMapSize.
            // We need to patch the *caller* methods (like GetCell, GetDepth, HasWater)
            // This transpiler might not be needed directly if callers are patched.
            // Let's keep it for now in case it's called internally with kMapSize somehow.
            Log($" Applying Transpiler to WaterSystem.GetCellCoords (may be redundant if callers patched)");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSystem.GetCell))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetCell_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.GetCell");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSystem.CalculateSourceMultiplier))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CalculateSourceMultiplier_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.CalculateSourceMultiplier");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
    }

    // Patching instance methods requires just the method name
    [HarmonyPatch(typeof(WaterSystem))]
    static class WaterSystem_InstanceMethods_Patches
    {
        // --- Log Configuration ---
        private static bool enableDebugLogging = true; // Set to false for release builds

        private static void Log(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Log($"[WaterSystem_Instance] {message}");
            }
        }

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_MapSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_MapSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.get_MapSize");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_CellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_CellSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.get_MapSize");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_WaveSpeed")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_WaveSpeed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.get_WaveSpeed");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSystem.GetDepth))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetDepth_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.GetDepth");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("HasWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HasWater_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.HasWater");
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
            Log($" Applying Transpiler to WaterSystem.ResetToLevel");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // Internal helper method uses kMapSize directly
        [HarmonyPatch("BorderCircleIntersection")] // It's private, access by name
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BorderCircleIntersection_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.BorderCircleIntersection");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("SourceStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.SourceStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("ResetActive")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetActive_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.ResetActive");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("EvaporateStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.EvaporateStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("VelocityStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.VelocityStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("DepthStep")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Log($" Applying Transpiler to WaterSystem.DepthStep");
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补

    }
}
