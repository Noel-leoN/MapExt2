// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using Game.Simulation;

    /// <summary>
    /// BaseDataReader基类/SurfaceDataReader派生类/HeightDataReader派生类修补GetReadbackBounds方法
    /// v1.4.2f版本将水系统SurfaceDataReader改变为BaseDataReader基类下的子类，并在基类GetReadbackBounds方法引用WaterSystem.kMapSize字段
    /// </summary>
    public static class WaterSystem_BaseDataReader_Patch
    {
        // --- 日志封装 ---
        private static readonly string modName = Mod.ModName;
        private static readonly string patchTypename = nameof(WaterSystem_BaseDataReader_Patch);
        private static void Info(string message) => Mod.Info($" {(modName)}.{patchTypename}:{message}");
        private static void Warn(string message) => Mod.Warn($" {(modName)}.{patchTypename}:{message}");
        private static void Error(string message) => Mod.Error($" {(modName)}.{patchTypename}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {(Mod.ModName)}.{patchTypename}:{message}");

        // --- 应用补丁 ---
        public static void Apply(Harmony harmony)
        {
            try
            {
                Info($"Starting to patch BaseDataReader.GetReadbackBounds...");

                // 1. 使用字符串名称反射获取 Internal 类型
                // 注意：字符串必须包含完整的命名空间
                Type surfaceType = AccessTools.TypeByName("Game.Simulation.SurfaceDataReader");
                Type heightType = AccessTools.TypeByName("Game.Simulation.HeightDataReader");

                if (surfaceType == null)
                {
                    Error("Failed to find type: Game.Simulation.SurfaceDataReader. It might be renamed or moved.");
                    return;
                }
                if (heightType == null)
                {
                    Error("Failed to find type: Game.Simulation.HeightDataReader. It might be renamed or moved.");
                    return;
                }

                // 2. 获取它们的基类 (即 BaseDataReader<T,V> 的具体实例)
                Type surfaceBaseType = surfaceType.BaseType;
                Type heightBaseType = heightType.BaseType;

                if (surfaceBaseType == null || heightBaseType == null)
                {
                    Error("Failed to resolve BaseDataReader generic types from subclasses.");
                    return;
                }

                Info($"Resolved types. SurfaceBase: {surfaceBaseType.Name}, HeightBase: {heightBaseType.Name}");

                // 3. 定义目标方法的参数签名: (out int2 pos, out int2 size)
                // 注意：Unity.Mathematics.int2 是 struct，所以需要 MakeByRefType
                Type[] paramTypes = new Type[] { typeof(Unity.Mathematics.int2).MakeByRefType(), typeof(Unity.Mathematics.int2).MakeByRefType() };

                // 4. 获取 Internal 基类中的 Internal 方法
                MethodInfo targetSurfaceMethod = AccessTools.Method(surfaceBaseType, "GetReadbackBounds", paramTypes);
                MethodInfo targetHeightMethod = AccessTools.Method(heightBaseType, "GetReadbackBounds", paramTypes);

                // 5. 获取 Transpiler
                var transpiler = new HarmonyMethod(typeof(WaterSystem_BaseDataReader_Patch), nameof(TargetTranspiler));

                // 6. 执行修补
                if (targetSurfaceMethod != null)
                {
                    harmony.Patch(targetSurfaceMethod, transpiler: transpiler);
                    Info($"Patched {surfaceBaseType.Name}.GetReadbackBounds successfully.");
                }
                else
                {
                    Error($"Could not find GetReadbackBounds in {surfaceBaseType.Name}.");
                }

                if (targetHeightMethod != null)
                {
                    harmony.Patch(targetHeightMethod, transpiler: transpiler);
                    Info($"Patched {heightBaseType.Name}.GetReadbackBounds successfully.");
                }
                else
                {
                    Error($"Could not find GetReadbackBounds in {heightBaseType.Name}.");
                }
            }
            catch (Exception e)
            {
                Error(e, "Critical error while applying WaterSystem patches.");
            }
        }

        // --- Transpiler 逻辑 ---
        private static IEnumerable<CodeInstruction> TargetTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            // 目标：将 WaterSystem.kMapSize (field) 替换为 PatchManager.CurrentMapSize (property getter)

            // 获取 WaterSystem.kMapSize 字段
            var originalField = AccessTools.Field(typeof(WaterSystem), nameof(WaterSystem.kMapSize));

            // 获取 PatchManager.CurrentMapSize 的 Getter
            var replacementMethod = AccessTools.PropertyGetter(typeof(PatchManager), nameof(PatchManager.CurrentCoreValue));

            if (originalField == null)
            {
                Error($"Could not find field WaterSystem.kMapSize. Patch aborted for {original.DeclaringType?.Name}.");
                foreach (var instruction in instructions) yield return instruction;
                yield break;
            }

            if (replacementMethod == null)
            {
                Error($"Could not find property getter PatchManager.CurrentMapSize. Patch aborted for {original.DeclaringType?.Name}.");
                foreach (var instruction in instructions) yield return instruction;
                yield break;
            }

            int replaceCount = 0;

            foreach (var instruction in instructions)
            {
                // 检查指令是否是加载静态字段 kMapSize
                if (instruction.LoadsField(originalField))
                {
                    // 替换为调用 PatchManager.CurrentMapSize 的 getter 方法
                    yield return new CodeInstruction(OpCodes.Call, replacementMethod);
                    replaceCount++;
                }
                else
                {
                    yield return instruction;
                }
            }

            if (replaceCount > 0)
            {
                Info($"Successfully replaced {replaceCount} occurrences of kMapSize in {original.DeclaringType?.Name}.");
            }
            else
            {
                Warn($"No occurrences of kMapSize found to replace in {original.DeclaringType?.Name}. This might indicate the game code has changed.");
            }
        }
    }
}


