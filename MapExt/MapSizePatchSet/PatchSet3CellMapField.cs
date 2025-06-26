// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Colossal.Logging;

// using UnityEngine; // For Debug.Log
using Game.Simulation;
using Game.UI.Tooltip;
using HarmonyLib;
using MapExtPDX.MapExt.MapSizePatchSet;
using Unity.Collections;
using Unity.Mathematics;


/// <summary>
/// 此class修补所有直接调用CellMapSystem<T>.kMapSize的外部托管代码方法
/// </summary>

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    [HarmonyPatch]
    public static class CellMapSystem_KMapSize_Field_Patches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Field_Patches)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Field_Patches)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Field_Patches)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(CellMapSystem_KMapSize_Field_Patches)}:{message}");


        const string TARGET_FIELD_NAME = "kMapSize";

        static readonly Type targetGenericTypeDef = typeof(CellMapSystem<>); // Cache the open generic type

        // --- 通用替换工具 ---
        public static IEnumerable<CodeInstruction> PatchKMapSizeFieldLoad(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase originalMethod)
        {
            // --- CV ---
            int MapScaleMultiplier = PatchManager.CurrentCoreValue;

            // --- Original Values ---
            int OriginalKMapSize = PatchManager.OriginalMapSize;

            int NEW_MAP_SIZE = MapScaleMultiplier * OriginalKMapSize;


            bool patched = false;
            var instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                var instruction = instructionList[i];

                if (instruction.opcode == OpCodes.Ldsfld && instruction.operand is FieldInfo operandField)
                {
                    // Check 1: 字段名是否kMapSize
                    // Check 2: 字段是否属于泛型封闭类型
                    // Check 3: 泛型是否属于CellMapSystem<>
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
                            replacementLoad.labels.AddRange(instruction.labels); // Preserve labels from ldsfld
                            instructionList[i] = replacementLoad;

                            if (nextInstruction.labels.Any())
                            {
                                if (i + 2 < instructionList.Count)
                                {
                                    instructionList[i + 2].labels.AddRange(nextInstruction.labels);
                                }
                                else
                                {
                                    Warn($"警告！Could not transfer labels from Conv_R4 after patching ldsfld in {originalMethod.Name}.");
                                    // Option: Add a Noop to hold labels if necessary.
                                }
                            }
                            instructionList.RemoveAt(i + 1);
                        }
                        else
                        {
                            var replacementLoad = new CodeInstruction(OpCodes.Ldc_I4, NEW_MAP_SIZE);
                            replacementLoad.labels.AddRange(instruction.labels); // Preserve labels
                            instructionList[i] = replacementLoad;
                        }
                        patched = true;
#if DEBUG
                        Info($"Patched external ldsfld {TARGET_FIELD_NAME} in {originalMethod.DeclaringType?.FullName}.{originalMethod.Name} with {NEW_MAP_SIZE}");
#endif
                    }
                }
            }

            if (!patched && originalMethod != null) // Check originalMethod != null for safety
            {
                Warn($"警告！Did not find ldsfld for '{TARGET_FIELD_NAME}' (from {targetGenericTypeDef.Name}) in external method: {originalMethod.DeclaringType?.FullName}.{originalMethod.Name}");
            }
            else if (!patched)
            {
                Warn($"警告！Did not find ldsfld for '{TARGET_FIELD_NAME}' (from {targetGenericTypeDef.Name}) in an unspecified method (originalMethod was null).");
            }

            return instructionList.AsEnumerable();
        }

        ///
        /// 修补所有 CellMapSystem<T>.kMapSize 的引用
        /// 

        // Patching AirPollutionSystemGetPollution
        [HarmonyPatch(typeof(AirPollutionSystem), "GetPollution")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_AirPollutionSystemGetPollution(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching AvailabilityInfoToGridSystemGetAvailabilityInfo
        [HarmonyPatch(typeof(AvailabilityInfoToGridSystem), "GetAvailabilityInfo")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_AvailabilityInfoToGridSystemGetAvailabilityInfo(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching BuildingPollutionAddSystemOnUpdate
        [HarmonyPatch(typeof(BuildingPollutionAddSystem), "OnUpdate")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_BuildingPollutionAddSystemOnUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching GroundPollutionSystemGetPollution
        [HarmonyPatch(typeof(GroundPollutionSystem), "GetPollution")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_GroundPollutionSystemGetPollution(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching GroundWaterSystemTryGetCell
        [HarmonyPatch(typeof(GroundWaterSystem), "TryGetCell")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_GroundWaterSystemTryGetCell(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching GroundWaterSystemGetGroundWater
        [HarmonyPatch(typeof(GroundWaterSystem), "GetGroundWater", new Type[] { typeof(float3), typeof(NativeArray<GroundWater>)
})]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_GroundWaterSystemGetGroundWater(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching GroundWaterSystemConsumeGroundWater
        [HarmonyPatch(typeof(GroundWaterSystem), "ConsumeGroundWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_GroundWaterSystemConsumeGroundWater(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            // 请不要剽窃！
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching LandValueSystemGetCellIndex
        [HarmonyPatch(typeof(LandValueSystem), "GetCellIndex")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_LandValueSystemGetCellIndex(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        /// <summary>
        /// !!! 直接在burst job修改内部参数，避免双重transpiler可能造成问题！
        /// </summary>
        // Patching LandValueSystemOnUpdate
        /*
        [HarmonyPatch(typeof(LandValueSystem), "OnUpdate")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_LandValueSystemOnUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }
        */

        // Patching NaturalResourceSystemResourceAmountToArea
        [HarmonyPatch(typeof(NaturalResourceSystem), "ResourceAmountToArea")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_NaturalResourceSystemResourceAmountToArea(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching NetPollutionSystemOnUpdate
        [HarmonyPatch(typeof(NetPollutionSystem), "OnUpdate")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_NetPollutionSystemOnUpdate(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching NoisePollutionSystemGetPollution
        [HarmonyPatch(typeof(NoisePollutionSystem), "GetPollution")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_NoisePollutionSystemGetPollution(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching PopulationToGridSystemGetPopulation
        [HarmonyPatch(typeof(PopulationToGridSystem), "GetPopulation")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_PopulationToGridSystemGetPopulation(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching SoilWaterSystemGetSoilWater
        [HarmonyPatch(typeof(SoilWaterSystem), "GetSoilWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_SoilWaterSystemGetSoilWater(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching TempExtractorTooltipSystem
        [HarmonyPatch(typeof(TempExtractorTooltipSystem), "FindResource")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_TempExtractorTooltipSystem(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching TerrainAttractivenessSystemGetAttractiveness
        [HarmonyPatch(typeof(TerrainAttractivenessSystem), "GetAttractiveness")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_TerrainAttractivenessSystemGetAttractiveness(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching WindDebugSystem
        //[HarmonyPatch(typeof(WindDebugSystem).GetNestedType("WindGizmoJob", BindingFlags.NonPublic), "Execute")]
        //[HarmonyTranspiler]
        //static IEnumerable<CodeInstruction> Transpile_WindDebugSystem(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original) =>
        // Reuse the same transpiler logic
        // PatchKMapSizeFieldLoad(instructions, gen, original);

        // Patching WindSimulationSystemGetCellCenter
        [HarmonyPatch(typeof(WindSimulationSystem), "GetCellCenter")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_WindSimulationSystemGetCellCenter(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching WindSystemGetWind
        [HarmonyPatch(typeof(WindSystem), "GetWind")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpile_WindSystemGetWind(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            // Reuse the same transpiler logic
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }

        // Patching ZoneAmbienceSystemGetZoneAmbienceNear
        // [HarmonyPatch(typeof(ZoneAmbienceSystem), "GetZoneAmbienceNear")]
        // [HarmonyTranspiler]
        // static IEnumerable<CodeInstruction> Transpile_ZoneAmbienceSystemGetZoneAmbienceNear(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        // {
        // Reuse the same transpiler logic
        //  return PatchKMapSizeFieldLoad(instructions, gen, original);
        // }

        /// BurstJob直接调用kMapSize,需进行Job替换
        /// CarNavigationSystem+ApplyTrafficAmbienceJob
        /// PopulationToGridSystem+PopulationToGridJob
        /// SoilWaterSystem+SoilWaterTickJob
        /// SpawnableAmbienceSystem+SpawnableAmbienceJob
        /// TelecomCoverageSystem+TelecomCoverageJob
        /// AttractionSystem+AttractivenessJob
        /// LandValueTooltipSystem+LandValueTooltipJob
        /// AudioGroupingSystem+AudioGroupingJob

        /// 字段替换模板
        /// 引用请注明出处
        /*
        [HarmonyPatch(typeof(AnotherExternalClass), "SomeMethodName")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TranspileAnotherExternalClass_SomeMethod(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            return PatchKMapSizetFieldLoad(instructions, gen, original);
        }

        [HarmonyPatch(typeof(YetAnotherClass), "ProcessData", new Type[] { typeof(int), typeof(string) })] 
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TranspileYetAnotherClass_ProcessData(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            return PatchKMapSizeFieldLoad(instructions, gen, original);
        }
        */
    }
}
