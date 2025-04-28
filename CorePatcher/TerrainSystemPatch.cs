// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Collections.Generic;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;
using Unity.Mathematics;

namespace MapExtPDX.Patches
{
    [HarmonyPatch(typeof(TerrainSystem))]
    public static class TerrainSystem_Methods_Patch
    {
        // --- Log Configuration ---
        private static bool enableDebugLogging = true; // 保持Release输出错误日志(仅Warn)

        private static void Log(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Log($"[TerrainSystem] {message}");
            }
        }
        private static void Warn(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Warn($"[TerrainSystem] {message}");
            }
        }
        private static void Error(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Error($"[TerrainSystem] {message}");
            }
        }

        // FinalizeTerrainData (改变引入默认值，仅修改此处即可，不需要同时修补其他方法)
        // 该方法调用频次较低，使用Prefix简化维护 
        // Target the FinalizeTerrainData method
        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        public static void Prefix(ref float2 inMapCorner, ref float2 inMapSize, ref float2 inWorldCorner, ref float2 inWorldSize)
        {
            int scalefactor = MapSizeMultiplier.Value;
            float baseSize = 14336f;
            if (math.abs(inMapSize.x - baseSize) < 1f)
            {
                inMapSize *= scalefactor;
                inWorldSize *= scalefactor;
                inMapCorner = -0.5f * inMapSize;
                inWorldCorner = -0.5f * inWorldSize;
                Log($"FinalizeTerrainData Prefix to patch! (Expected value: {inMapSize} , {inMapCorner} , {inWorldSize} , {inWorldCorner})");
            }

        } // FinalizeTerrainData method


        // Target the GetTerrainBounds method
        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int patches = 0;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Look for the instruction loading the specific float constant 14336f
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == MapSizeMultiplier.OriginalMapSizeValue)
                {
                    // Replace the operand (the constant value) with our new dimension
                    codes[i].operand = MapSizeMultiplier.NewMapSizeValue;
                    patches++;
                }
            }

            if (patches == 0)
            {
                Warn($"警告！GetTerrainBounds_Transpiler did not find any instructions to patch! (Expected value: {MapSizeMultiplier.NewMapSizeValue})");
            }
            else
            {
#if DEBUG
                Log($"GetTerrainBounds_Transpiler applied {patches} patch(es).");
#endif
            }

            return codes;
        } // GetTerrainBounds method

        // Target the GetHeightData method
        [HarmonyPatch(nameof(TerrainSystem.GetHeightData))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetHeightData_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //ExpandTerrainMod.log.Info("Applying Transpiler to TerrainSystem.GetHeightData");
            int patches = 0;

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                // Look for the instruction loading the specific float constant 14336f
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == MapSizeMultiplier.OriginalMapSizeValue)
                {
#if DEBUG
                    Log($"Patching instruction {i} in GetHeightData: Replacing {MapSizeMultiplier.OriginalMapSizeValue} with {MapSizeMultiplier.NewMapSizeValue}");
#endif
                    // Replace the operand (the constant value) with our new dimension
                    codes[i].operand = MapSizeMultiplier.NewMapSizeValue;
                    patches++;
                    // This method uses the value twice (for x and z in the float3 size).
                    // The loop will continue and find the second instance too.
                }
            }

            if (patches == 0)
            {
                Warn($"警告！GetHeightData_Transpiler did not find any instructions to patch! (Expected value: {MapSizeMultiplier.OriginalMapSizeValue})");
            }
            else
            {
#if DEBUG
                Log($"GetHeightData_Transpiler applied {patches} patch(es).");
#endif
            }


            return codes;
        } // GetHeightData method

    } // Terrain System Patch class

} // namespace


