// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using Unity.Jobs;
using Unity.Mathematics;
using Game.Simulation;
// using UnityEngine; // 如果使用 Debug.Log

namespace MapExtPDX.Patches
{
    [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.GetSurfaceData))]
    static class WaterSystem_GetSurfaceData_Patch
    {
        // 静态辅助方法，用于创建修改后的 WaterSurfaceData 实例
        static WaterSurfaceData ModifyResultData(WaterSurfaceData originalData)
        {
            // Mod.Logger.Info($"Original Data: scale={originalData.scale}, offset={originalData.offset}");

            float scalex = originalData.scale.x / MapSizeMultiplier.Value;
            float scaley = originalData.scale.y;
            float scalez = originalData.scale.z / MapSizeMultiplier.Value;
            float3 newScale = new float3(scalex, scaley, scalez);

            float offsetx = originalData.offset.x * MapSizeMultiplier.Value;
            float offsety = originalData.offset.y;
            float offsetz = originalData.offset.z * MapSizeMultiplier.Value;
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

        // Harmony转译逻辑
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
                Mod.Logger.Error($"[WaterSystem_GetSurfaceData] Could not find internal type '{readerTypeName}' using AccessTools.TypeByName. Ensure the full namespace and class name are correct.");
                return instructions;
            }

            // 2. 找到被包装方法调用的 SurfaceDataReader.GetSurfaceData 方法的信息
            try
            {
                targetMethodCall = AccessTools.Method(readerType, "GetSurfaceData", new Type[] { typeof(JobHandle).MakeByRefType() });

                if (targetMethodCall == null)
                {
                    Mod.Logger.Error($"[WaterSystem_GetSurfaceData] Could not find MethodInfo for '{readerType.Name}.GetSurfaceData(out JobHandle)'. Check method name and parameters.");
                    return instructions;
                }
            }
            catch (Exception ex)
            {
                Mod.Logger.Error($"[WaterSystem_GetSurfaceData] Error finding '{readerType.Name}.GetSurfaceData' MethodInfo: {ex.Message}");
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
                Mod.Logger.Error($"[WaterSystem_GetSurfaceData] Could not find the call to '{targetMethodCall.Name}' in '{originalMethod.Name}' IL code or insertion point is invalid.");
                return codes;
            }

            // 4. 准备并插入调用辅助方法的指令
            var modifyHelperMethod = AccessTools.Method(typeof(WaterSystem_GetSurfaceData_Patch), nameof(ModifyResultData));
            if (modifyHelperMethod == null)
            {
                Mod.Logger.Error($"[WaterSystem_GetSurfaceData] Could not find MethodInfo for the helper method '{nameof(ModifyResultData)}'.");
                return codes;
            }
            var callHelperInstruction = new CodeInstruction(OpCodes.Call, modifyHelperMethod);
            codes.Insert(insertionIndex, callHelperInstruction);

#if DEBUG
            Mod.Logger.Info($"[WaterSystem_GetSurfaceData] Successfully transpiled wrapper method '{originalMethod.Name}' to modify the result of the internal call."); 
#endif

            return codes.AsEnumerable();
        }
    }

}

