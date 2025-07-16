// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// using UnityEngine;// For Debug.Log
using Game.Simulation;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    [HarmonyPatch]
    public static class CellMapSystem_KMapSize_Method_Patches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Method_Patches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Method_Patches)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Method_Patches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Method_Patches)}:{message}");

        const string TARGET_FIELD_NAME = "kMapSize";

        static readonly Type targetGenericTypeDef = typeof(CellMapSystem<>); // Cache the open generic type

        // --- Helper Method ---
        private static IEnumerable<CodeInstruction> PatchKMapSizeFieldLoad(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase originalMethod)
        {
            // --- Core Configuration ---
            int MapScaleMultiplier = PatchManager.CurrentCoreValue;

            // --- Original Values ---
            int OriginalKMapSize = PatchManager.OriginalMapSize;

            int NEW_MAP_SIZE = MapScaleMultiplier * OriginalKMapSize;

            bool patched = false;
            var instructionList = instructions.ToList();
            var targetGenericTypeDef = typeof(CellMapSystem<>);

            for (int i = 0; i < instructionList.Count; i++)
            {
                var instruction = instructionList[i];
                if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo operandField)
                {
                    if (operandField.Name == TARGET_FIELD_NAME &&
                        operandField.DeclaringType != null &&
                        operandField.DeclaringType.IsGenericType &&
                        operandField.DeclaringType.GetGenericTypeDefinition() == targetGenericTypeDef)
                    {
                        CodeInstruction nextInstruction = i + 1 < instructionList.Count ? instructionList[i + 1] : null;
                        bool needsFloat = nextInstruction?.opcode == OpCodes.Conv_R4;

                        if (needsFloat)
                        {
                            var replacementLoad = new CodeInstruction(OpCodes.Ldc_R4, (float)NEW_MAP_SIZE);
                            replacementLoad.labels.AddRange(instruction.labels);
                            instructionList[i] = replacementLoad;

                            if (nextInstruction.labels.Any())
                            {
                                if (i + 2 < instructionList.Count)
                                {
                                    instructionList[i + 2].labels.AddRange(nextInstruction.labels);
                                }
                                else
                                {
                                    Error($"警告！Could not transfer labels from Conv_R4 in {originalMethod.Name}. End of method reached?");

                                }
                            }
                            instructionList.RemoveAt(i + 1);
                        }
                        else
                        {
                            var replacementLoad = new CodeInstruction(OpCodes.Ldc_I4, NEW_MAP_SIZE);
                            replacementLoad.labels.AddRange(instruction.labels);
                            instructionList[i] = replacementLoad;
                        }
                        patched = true;
#if DEBUG
                        Info($"Patched ldsfld {TARGET_FIELD_NAME} in {originalMethod.DeclaringType?.FullName}.{originalMethod.Name} with {NEW_MAP_SIZE}");
#endif
                    }
                }
            }

            if (!patched)
            {
                Warn($"警告！Did not find ldsfld for '{TARGET_FIELD_NAME}' in method: {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}");
            }

            return instructionList.AsEnumerable();
        }


        // --- Patching for CellMapSystem<AirPollution> ---

        [HarmonyPatch(typeof(CellMapSystem<AirPollution>), "GetCellCenter", new Type[] { typeof(int), typeof(int) })]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> AirPollution_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<AirPollution>), "GetData")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> AirPollution_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<AvailabilityInfoCell> ---

        [HarmonyPatch(typeof(CellMapSystem<AvailabilityInfoCell>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> AvailabilityInfoCell_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<AvailabilityInfoCell>), nameof(CellMapSystem<AvailabilityInfoCell>.GetData))] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> AvailabilityInfoCell_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<GroundPollution> ---

        [HarmonyPatch(typeof(CellMapSystem<GroundPollution>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GroundPollution_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<GroundPollution>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GroundPollution_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<GroundWater> ---

        [HarmonyPatch(typeof(CellMapSystem<GroundWater>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GroundWater_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<GroundWater>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GroundWater_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<LandValueCell> ---

        [HarmonyPatch(typeof(CellMapSystem<LandValueCell>), "GetCellCenter", new Type[] { typeof(int), /* 源 */ typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> LandValueCell_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<LandValueCell>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> LandValueCell_GetData(IEnumerable<CodeInstruction> instructions, /* 标志位 */ ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<NaturalResourceCell> ---

        [HarmonyPatch(typeof(CellMapSystem<NaturalResourceCell>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> NaturalResourceCell_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<NaturalResourceCell>), "GetCellCenter", new Type[] { typeof(int2), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> NaturalResourceCell_Center2(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<NaturalResourceCell>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> NaturalResourceCell_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<NoisePollution> ---

        [HarmonyPatch(typeof(CellMapSystem<NoisePollution>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> NoisePollution_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<NoisePollution>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> NoisePollution_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<PopulationCell> ---

        [HarmonyPatch(typeof(CellMapSystem<PopulationCell>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> PopulationCell_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<PopulationCell>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> PopulationCell_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<SoilWater> ---

        [HarmonyPatch(typeof(CellMapSystem<SoilWater>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SoilWater_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<SoilWater>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SoilWater_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<ZoneAmbienceCell> ---

        [HarmonyPatch(typeof(CellMapSystem<ZoneAmbienceCell>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ZoneAmbienceCell_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<ZoneAmbienceCell>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ZoneAmbienceCell_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<TerrainAttractiveness> ---

        [HarmonyPatch(typeof(CellMapSystem<TerrainAttractiveness>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TerrainAttractiveness_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<TerrainAttractiveness>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TerrainAttractiveness_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<TrafficAmbienceCell> ---

        [HarmonyPatch(typeof(CellMapSystem<TrafficAmbienceCell>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TrafficAmbienceCell_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<TrafficAmbienceCell>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TrafficAmbienceCell_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<Wind> ---

        [HarmonyPatch(typeof(CellMapSystem<Wind>), "GetCellCenter", new Type[] { typeof(int), typeof(int) }/* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Wind_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<Wind>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Wind_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        // --- Patching for CellMapSystem<TelecomCoverage> ---

        [HarmonyPatch(typeof(CellMapSystem<TelecomCoverage>), "GetCellCenter", new Type[] { typeof(int2), typeof(int) } /* Add params if any */)]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TelecomCoverage_Center(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);

        [HarmonyPatch(typeof(CellMapSystem<TelecomCoverage>), "GetData")] // Adjust params as needed
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TelecomCoverage_GetData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
            PatchKMapSizeFieldLoad(instructions, gen, original);
    }
}
