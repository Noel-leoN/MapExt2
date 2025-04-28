// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Game.Simulation;
using HarmonyLib;
// using UnityEngine; // Required for Debug.Log

namespace MapExtPDX.Patches
{
    public static class WaterSystemPatcherHelper
    {
        // --- Log Configuration ---
#if RELEASE
        private static bool enableDebugLogging = false; // Set to false for release builds

        private static void Log(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Logger.Info($"[WaterMapScaler] {message}");
            }
        }
        private static void Warn(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Logger.Warn($"[WaterMapScaler] {message}");
            }
        }
        private static void Error(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Logger.Error($"[WaterMapScaler] {message}");
            }
        }
#endif

#if DEBUG
        private static bool enableDebugLogging = true; // Set to false for release builds

        private static void Log(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Logger.Info($"[WaterMapScaler] {message}");
            }
        }
        private static void Warn(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Logger.Warn($"[WaterMapScaler] {message}");
            }
        }
        private static void Error(string message)
        {
            if (enableDebugLogging)
            {
                Mod.Logger.Error($"[WaterMapScaler] {message}");
            }
        }
#endif

        // --- Configuration ---
        // Set this multiplier (e.g., 2f, 4f, 8f, 16f)
        // Ideally, load this from a config file in a real mod
        public const int MapScaleMultiplier = MapSizeMultiplier.Value;

        // --- Original Values ---
        public const int OriginalKMapSize = MapSizeMultiplier.OriginalMapSizeValueInt;
        public const float OriginalKCellSize = 7.0f;

        // --- New Calculated Values ---
        public static float NewKCellSize => OriginalKCellSize * MapScaleMultiplier;
        // Calculate new map size based on the new cell size and assumed original texture width
        // We assume m_TexSize remains 2048x2048 as initialized in the original code

        // public static float NewKMapSizeF => 2048f * NewKCellSize;
        // 直接使用terrain值
        public static float NewKMapSizeF => MapSizeMultiplier.NewMapSizeValue;
        public static int NewKMapSizeI => MapSizeMultiplier.NewMapSizeValueInt; // For contexts needing int

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

            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                // Check for loading kMapSize (static int)
                if (codes[i].LoadsField(_kMapSizeField))
                {
                    // Determine if the next instruction converts it to float
                    bool convertsToFloat = (i + 1 < codes.Count && codes[i + 1].opcode == OpCodes.Conv_R4);

                    if (convertsToFloat)
                    {
                        // Replace 'ldsfld kMapSize' with 'ldc.r4 NewKMapSizeF'
                        codes[i].opcode = OpCodes.Ldc_R4;
                        codes[i].operand = NewKMapSizeF;
                        // Remove the now redundant 'conv.r4' instruction
                        codes.RemoveAt(i + 1);
                        // Note: Loop counter doesn't need adjustment because RemoveAt shifts subsequent indices
                        patched = true;
                        Log($" Patched kMapSize (as float) {codes[i].operand} in {methodName}");
                    }
                    else
                    {
                        // Replace 'ldsfld kMapSize' with 'ldc.i4 NewKMapSizeI'
                        codes[i].opcode = OpCodes.Ldc_I4;
                        codes[i].operand = NewKMapSizeI;
                        patched = true;
                        Log($" Patched kMapSize (as int) to {codes[i].operand} in {methodName}");
                    }
                }
                // Check for loading kCellSize (static float)
                else if (codes[i].LoadsField(_kCellSizeField))
                {
                    // kCellSize is float, usually needs float. Replace 'ldsfld kCellSize' with 'ldc.r4 NewKCellSize'
                    codes[i].opcode = OpCodes.Ldc_R4;
                    codes[i].operand = NewKCellSize;
                    patched = true;
                    Log($" Patched kCellSize in {methodName}");
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

