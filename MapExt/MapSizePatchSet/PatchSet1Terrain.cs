// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.MapSizePatchSet
{

    [HarmonyPatch(typeof(TerrainSystem))]
    public static class TerrainSystemPatches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(TerrainSystemPatches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(TerrainSystemPatches)}:{message}");
        private static void Error(string message) => Mod.Error($" {(Mod.ModName)}.{nameof(TerrainSystemPatches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(TerrainSystemPatches)}:{message}");

        // FinalizeTerrainData (改变引入默认值，仅修改此处即可，不需要同时修补其他方法)
        // 该方法调用频次较低，使用Prefix简化维护 
        // Target the FinalizeTerrainData method
        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        public static void FinalizeTerrainData_Prefix(ref float2 inMapCorner, ref float2 inMapSize, ref float2 inWorldCorner, ref float2 inWorldSize)
        {
            int patches = 0;

            int scalefactor = PatchManager.CurrentCoreValue;
            float baseSize = PatchManager.OriginalMapSize;

            if (math.abs(inMapSize.x - baseSize) < 1f)
            {
                inMapSize *= scalefactor;
                inWorldSize *= scalefactor;
                inMapCorner = -0.5f * inMapSize;
                inWorldCorner = -0.5f * inWorldSize;

                patches++;                
            }

            if (patches != 0)
            {                
#if DEBUG
                Info($"FinalizeTerrainData Prefix applied {patches} patch(es). (Expected value: {inMapSize} , {inMapCorner} , {inWorldSize} , {inWorldCorner})");
#endif
            }

        } // FinalizeTerrainData method


        // Target the GetTerrainBounds method
        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int scalefactor = PatchManager.CurrentCoreValue;
            float baseSize = PatchManager.OriginalMapSize;
            float newSize = scalefactor * baseSize;

            int patches = 0;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Look for the instruction loading the specific float constant 14336f
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == baseSize)
                {
                    // Replace the operand (the constant value) with our new dimension
                    codes[i].operand = newSize;
                    patches++;
                }
            }

            if (patches == 0)
            {
                Warn($"警告！GetTerrainBounds_Transpiler did not find any instructions to patch! (Expected value: {newSize})");
            }
            else
            {
#if DEBUG
                Info($"GetTerrainBounds_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
#endif
            }

            return codes;
        } // GetTerrainBounds method


        // Target the GetHeightData method
        [HarmonyPatch(nameof(TerrainSystem.GetHeightData))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetHeightData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int scalefactor = PatchManager.CurrentCoreValue;
            float baseSize = PatchManager.OriginalMapSize;
            float newSize = scalefactor * baseSize;

            // log.Info("Applying Transpiler to TerrainSystem.GetHeightData");

            int patches = 0;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Look for the instruction loading the specific float constant 14336f
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == baseSize)
                {
#if DEBUG
                    Info($"Patching instruction {i} in GetHeightData: Replacing {baseSize} with {newSize}");
#endif
                    // Replace the operand (the constant value) with our new dimension
                    codes[i].operand = newSize;
                    patches++;
                    // This method uses the value twice (for x and z in the float3 size).
                    // The loop will continue and find the second instance too.
                }
            }

            if (patches == 0)
            {
                Warn($"警告！GetHeightData_Transpiler did not find any instructions to patch! (Expected value: {newSize})");
            }
            else
            {
#if DEBUG
                Info($"GetHeightData_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
#endif
            }


            return codes;
        } // GetHeightData method

    }
}

