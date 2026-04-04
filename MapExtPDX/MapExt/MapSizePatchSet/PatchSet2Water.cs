// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.Simulation;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace MapExtPDX.MapExt.MapSizePatchSet
{

    [HarmonyPatch(typeof(WaterSystem))]
    static class WaterSystemMethodPatches
    {
        // --- 日志封装 ---
        private static readonly string patchTypename = nameof(WaterSystemMethodPatches);
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{patchTypename}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{patchTypename}:{message}");
        private static void Error(string message) => Mod.Error($" {(Mod.ModName)}.{patchTypename}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{patchTypename}:{message}");

        // v2.1.1重新启用
        // Essential for initializing SurfaceDataReaders correctly
        // 配合使用反射强制重新调用一次
        // v2.x.x: 额外替换 m_TexSize 硬编码的 2048 为 ResolutionManager.WaterTextureSize
        [HarmonyPatch("InitTextures")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> InitTextures_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            Info($" Applying Transpiler to WaterSystem.InitTextures");

            // 第一步：用通用 helper 替换 kMapSize / kCellSize
            var patched = WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());

            // 第二步：替换 m_TexSize = new int2(2048, 2048) 中的硬编码 2048
            // InitTextures() 中 m_TexSize 赋值编译为两个 ldc.i4 2048 指令
            int waterTexSize = MapExtPDX.MapExt.Core.ResolutionManager.WaterTextureSize;
            if (waterTexSize != 2048) // 仅在修改了分辨率时才替换
            {
                var codes = new System.Collections.Generic.List<CodeInstruction>(patched);
                int replacedCount = 0;
                for (int i = 0; i < codes.Count; i++)
                {
                    // 检查 ldc.i4 2048 指令
                    if (codes[i].opcode == System.Reflection.Emit.OpCodes.Ldc_I4 &&
                        codes[i].operand is int val && val == 2048)
                    {
                        codes[i].operand = waterTexSize;
                        replacedCount++;
                        Info($"  Patched ldc.i4 2048 → {waterTexSize} at index {i}");
                    }
                }
                Info($"  InitTextures: replaced {replacedCount} occurrences of 2048 with {waterTexSize}");
                return codes;
            }

            return patched;
        }

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_MapSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_MapSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_CellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_CellSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_WaveSpeed")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_WaveSpeed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_WaveSpeed");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.3.6f版本增加
        [HarmonyPatch("get_BackdropCellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_BackdropCellSize(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_BackdropCellSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.CalculateSourceMultiplier))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CalculateSourceMultiplier_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.CalculateSourceMultiplier");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.4.2f版本增加-mod v2.1.1
        [HarmonyPatch(typeof(WaterSystem), "InitBackdropTexture")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> InitBackdropTexture_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.InitBackdropTexture");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }



        // 1.3.6f版本已弃用，暂时不动
        [HarmonyPatch("HasWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HasWater_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.HasWater");
#endif
            // This method calls GetCell which uses GetCellCoords which uses mapSize.
            // Patching HasWater ensures the correct mapSize is passed down.
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

    }

    [HarmonyPatch(typeof(WaterSimulation))]
    static class WaterSimulationMethodPatches
    {
        // --- 日志封装 ---
        private static readonly string patchTypename = nameof(WaterSimulationMethodPatches);
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{patchTypename}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{patchTypename}:{message}");
        private static void Error(string message) => Mod.Error($" {(Mod.ModName)}.{patchTypename}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{patchTypename}:{message}");

        // v1.3.6f版本变动
        [HarmonyPatch(nameof(WaterSimulation.ResetToLevel))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetToLevel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.ResetToLevel");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.SourceStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.SourceStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.EvaporateStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.EvaporateStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.VelocityStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.DepthStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.DepthStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补
    }

    [HarmonyPatch(typeof(WaterSimulationLegacy))]
    static class WaterSimulationLegacyMethodPatches
    {
        // --- 日志封装 ---
        private static readonly string patchTypename = nameof(WaterSimulationLegacyMethodPatches);
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{patchTypename}:{message}");
        private static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{patchTypename}:{message}");
        private static void Error(string message) => Mod.Error($" {(Mod.ModName)}.{patchTypename}:{message}");
        private static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{patchTypename}:{message}");

        // v1.3.6f版本变动      

        [HarmonyPatch(nameof(WaterSimulationLegacy.SourceStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.SourceStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.EvaporateStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.EvaporateStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.VelocityStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.DepthStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.DepthStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("BorderCircleIntersection")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BorderCircleIntersection_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.BorderCircleIntersection");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补
    }

    [HarmonyPatch(typeof(WaterLevelChangeSystem))]
    static class WaterLevelChangeSystemMethodPatches
    {
        private static void Info(string message) => Mod.Info($" {Mod.ModName}.{nameof(WaterLevelChangeSystemMethodPatches)}:{message}");

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_TsunamiEndDelay")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_MapSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.get_MapSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // vanilla未启用
        [HarmonyPatch("GetMinimumDelayAt")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetMinimumDelayAt_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            Info($" Applying Transpiler to WaterSystem.GetMinimumDelayAt");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
    }

}
