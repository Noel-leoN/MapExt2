﻿// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.Simulation;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Jobs;
using Unity.Mathematics;
// using UnityEngine; // 如果使用 Debug.Log

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.GetSurfaceData))]
    static class WaterSystem_GetSurfaceData_Patch
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(WaterSystem_GetSurfaceData_Patch)}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{nameof(WaterSystem_GetSurfaceData_Patch)}:{message}");
        private static void Error(string message) => Mod.Error($" {Mod.ModName}.{nameof(WaterSystem_GetSurfaceData_Patch)}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{nameof(WaterSystem_GetSurfaceData_Patch)}:{message}");


        // 静态辅助方法，用于创建修改后的 WaterSurfaceData 实例
        static WaterSurfaceData ModifyResultData(WaterSurfaceData originalData)
        {
            // 局部变量缓存引用CurrentCoreValue
            int MapScaleMultiplier = PatchManager.CurrentCoreValue;

            // Mod.Logger.Info($"Original Data: scale={originalData.scale}, offset={originalData.offset}");

            float scalex = originalData.scale.x / MapScaleMultiplier;
            float scaley = originalData.scale.y;
            float scalez = originalData.scale.z / MapScaleMultiplier;
            float3 newScale = new float3(scalex, scaley, scalez);

            float offsetx = originalData.offset.x * MapScaleMultiplier;
            float offsety = originalData.offset.y;
            float offsetz = originalData.offset.z * MapScaleMultiplier;
            float3 newOffset = new float3(offsetx, offsety, offsetz);

            // 调用构造函数创建新实例
            var modifiedData = new WaterSurfaceData(
                originalData.depths,
                originalData.resolution,
                newScale,
                newOffset
            );

            // Mod.Logger.Info($"Modified Data: scale={modifiedData.scale}, offset={modifiedData.offset}");
            return modifiedData;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase originalMethod)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo targetMethodCall = null;
            Type readerType = null;

            // 1. 使用 TypeByName 按名称查找 internal class
            // !! 使用完全限定名称 !!
            const string readerTypeName = "Game.Simulation.SurfaceDataReader";
            readerType = AccessTools.TypeByName(readerTypeName);

            if (readerType == null)
            {
                Error($"[WaterSystem_GetSurfaceData] Could not find internal type '{readerTypeName}' using AccessTools.TypeByName. Ensure the full namespace and class name are correct.");
                return instructions;
            }

            // 2. 找到被包装方法调用的 SurfaceDataReader.GetSurfaceData 方法的信息
            try
            {
                targetMethodCall = AccessTools.Method(readerType, "GetSurfaceData", new Type[] { typeof(JobHandle).MakeByRefType() });

                if (targetMethodCall == null)
                {
                    Error($"[WaterSystem_GetSurfaceData] Could not find MethodInfo for '{readerType.Name}.GetSurfaceData(out JobHandle)'. Check method name and parameters.");
                    return instructions;
                }
            }
            catch (Exception ex)
            {
                Error($"[WaterSystem_GetSurfaceData] Error finding '{readerType.Name}.GetSurfaceData' MethodInfo: {ex.Message}");
                return instructions;
            }

            // 3. 查找调用 targetMethodCall 的指令 (call or callvirt)
            int insertionIndex = -1;
            for (int i = 0; i < codes.Count; i++)
            {
                if ((codes[i].opcode == OpCodes.Call || codes[i].opcode == OpCodes.Callvirt)
                    && codes[i].operand is MethodInfo calledMethod
                    && calledMethod == targetMethodCall) // 直接比较 MethodInfo 对象引用
                {
                    insertionIndex = i + 1; // 插入点是调用指令的下一条指令
                    break;
                }
            }

            if (insertionIndex == -1 || insertionIndex >= codes.Count)
            {
                Error($"[WaterSystem_GetSurfaceData] Could not find the call to '{targetMethodCall.Name}' in '{originalMethod.Name}' IL code or insertion point is invalid.");
                return codes;
            }

            // 4. 准备并插入调用辅助方法的指令
            var modifyHelperMethod = AccessTools.Method(typeof(WaterSystem_GetSurfaceData_Patch), nameof(ModifyResultData));
            if (modifyHelperMethod == null)
            {
                Error($"[WaterSystem_GetSurfaceData] Could not find MethodInfo for the helper method '{nameof(ModifyResultData)}'.");
                return codes;
            }
            var callHelperInstruction = new CodeInstruction(OpCodes.Call, modifyHelperMethod);
            codes.Insert(insertionIndex, callHelperInstruction);

#if DEBUG
            Info($"[WaterSystem_GetSurfaceData] Successfully transpiled wrapper method '{originalMethod.Name}' to modify the result of the internal call.");
#endif

            return codes.AsEnumerable();
        }
    }

}

