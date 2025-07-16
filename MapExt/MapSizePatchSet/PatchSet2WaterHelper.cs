// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.Simulation;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
// using UnityEngine; // Required for Debug.Log

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    public static class WaterSystemPatcherHelper
    {
        private static void Info(string message) => Mod.Info($" {nameof(Mod.ModName)}.{nameof(WaterSystemPatcherHelper)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {nameof(Mod.ModName)}.{nameof(WaterSystemPatcherHelper)}:{message}");
        private static void Error(string message) => Mod.Error($" {nameof(Mod.ModName)}.{nameof(WaterSystemPatcherHelper)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {nameof(Mod.ModName)}.{nameof(WaterSystemPatcherHelper)}:{message}");


        // --- Reflection Info (cached for performance) ---
        private static readonly FieldInfo _kMapSizeField = AccessTools.Field(typeof(WaterSystem), "kMapSize");
        private static readonly FieldInfo _kCellSizeField = AccessTools.Field(typeof(WaterSystem), "kCellSize");

        // Check if fields were found (important for debugging)
        public static bool FieldsFound => _kMapSizeField != null && _kCellSizeField != null;

        // --- Core Transpiler Logic ---
        public static IEnumerable<CodeInstruction> PatchMethodInstructions(IEnumerable<CodeInstruction> instructions, string methodName)
        {
            if (!FieldsFound)
            {
                Warn($" Failed to find kMapSize or kCellSize fields. Cannot patch {methodName}.");
                return instructions; // Return original instructions
            }

            // --- Configuration ---
            // 缓存CoreValue到局部变量
            int MapScaleMultiplier = PatchManager.CurrentCoreValue;

            // --- 原始Water kMapSize ---
            int OriginalKMapSize = PatchManager.OriginalMapSize;

            // --- 原始Water CellSize ---
            float OriginalKCellSize = 7f;

            // --- 修补为新的Water kMapSize ---
            // 原始整数形式
            int NewKMapSizeI = MapScaleMultiplier * OriginalKMapSize;
            // 浮点形式适配上下文
            float NewKMapSizeF = MapScaleMultiplier * OriginalKMapSize;

            // --- 修补为新的Water CellSize ---
            float NewKCellSize = OriginalKCellSize * MapScaleMultiplier;
            // Calculate new map size based on the new cell size and assumed original texture width
            // assume m_TexSize remains 2048x2048 as initialized in the original code
            // 遵循 MapSize = TexSize * CellSize
            // public static float NewKMapSizeF => 2048f * NewKCellSize;

            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                // Check for loading kMapSize (static int)
                if (codes[i].LoadsField(_kMapSizeField))
                {
                    // Determine if the next instruction converts it to float
                    bool convertsToFloat = i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Conv_R4;

                    if (convertsToFloat)
                    {
                        // Replace 'ldsfld kMapSize' with 'ldc.r4 NewKMapSizeF'
                        codes[i].opcode = OpCodes.Ldc_R4;
                        codes[i].operand = NewKMapSizeF;
                        // Remove the now redundant 'conv.r4' instruction
                        codes.RemoveAt(i + 1);
                        // Note: Loop counter doesn't need adjustment because RemoveAt shifts subsequent indices
                        patched = true;
#if DEBUG
                        Info($" Patched kMapSize (as float) {codes[i].operand} in {methodName}"); 
#endif
                    }
                    else
                    {
                        // Replace 'ldsfld kMapSize' with 'ldc.i4 NewKMapSizeI'
                        codes[i].opcode = OpCodes.Ldc_I4;
                        codes[i].operand = NewKMapSizeI;
                        patched = true;
#if DEBUG
                        Info($" Patched kMapSize (as int) to {codes[i].operand} in {methodName}"); 
#endif
                    }
                }
                // Check for loading kCellSize (static float)
                else if (codes[i].LoadsField(_kCellSizeField))
                {
                    // kCellSize is float, usually needs float. Replace 'ldsfld kCellSize' with 'ldc.r4 NewKCellSize'
                    codes[i].opcode = OpCodes.Ldc_R4;
                    codes[i].operand = NewKCellSize;
                    patched = true;
#if DEBUG
                    Info($" Patched kCellSize to {codes[i].operand} in {methodName}"); 
#endif
                }
            }

            if (!patched)
            {
                Warn($" Method {methodName} was targeted, but no kMapSize/kCellSize loads were found/patched.");
            }

            return codes;
        }

        public static string GetCurrentMethodName([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            return memberName;
        }
    }
}

