// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// ReSharper disable UnusedMember.Local

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using Unity.Mathematics;

    // Target TerrainSystem class
    // 修补FinalizeTerrainData/GetTerrainBounds/GetHeightData等三个方法
    [HarmonyPatch(typeof(TerrainSystem))]
    public static class TerrainSystemPatches
    {
        private const string Tag = "TerrainPatch";

        // FinalizeTerrainData (改变引入默认值，仅修改此处即可，不需要同时修补其他方法)
        // 该方法调用仅在加载存档后执行一次，使用Prefix简化维护 
        // Target the FinalizeTerrainData method
        [HarmonyPatch("FinalizeTerrainData")]
        [HarmonyPrefix]
        public static void FinalizeTerrainData_Prefix(ref float2 inMapCorner, ref float2 inMapSize,
            ref float2 inWorldCorner, ref float2 inWorldSize)
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
                ModLog.Patch(Tag,
                    $"FinalizeTerrainData Prefix applied {patches} patch(es). (Expected value: {inMapSize} , {inMapCorner} , {inWorldSize} , {inWorldCorner})");
            }
        } // FinalizeTerrainData method


        // Target the GetTerrainBounds method
        // 高频调用
        [HarmonyPatch(nameof(TerrainSystem.GetTerrainBounds))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> GetTerrainBounds_Transpiler(
            IEnumerable<CodeInstruction> instructions)
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
                ModLog.Warn(Tag,
                    $"GetTerrainBounds_Transpiler did not find any instructions to patch! (Expected value: {newSize})");
            }
            else
            {
#if DEBUG
                ModLog.Debug(Tag, $"GetTerrainBounds_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
#endif
            }

            return codes;
        } // GetTerrainBounds method


        // Target the GetHeightData method
        // 极高频调用
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
                    ModLog.Debug(Tag, $"Patching instruction {i} in GetHeightData: Replacing {baseSize} with {newSize}");
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
                ModLog.Warn(Tag,
                    $"GetHeightData_Transpiler did not find any instructions to patch! (Expected value: {newSize})");
            }
            else
            {
#if DEBUG
                ModLog.Debug(Tag, $"GetHeightData_Transpiler applied {patches} patch(es).(Expected value: {newSize})");
#endif
            }


            return codes;
        } // GetHeightData method
    }
}

