// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    // =========================================================================
    // region: WaterSystem Transpiler Patches
    // =========================================================================

    [HarmonyPatch(typeof(WaterSystem))]
    static class WaterSystemMethodPatches
    {
        private const string Tag = "WaterPatch";

        // v2.1.1重新启用
        // Essential for initializing SurfaceDataReaders correctly
        // 配合使用反射强制重新调用一次
        [HarmonyPatch("InitTextures")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> InitTextures_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            ModLog.Patch(Tag, "Applying Transpiler to WaterSystem.InitTextures");

            // 替换 kMapSize / kCellSize (已验证可靠)
            var patched = WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());

            // TODO [Phase 2]: m_TexSize 2048→WaterTextureSize 替换
            // 现阶段暂不修改 m_TexSize, 因为:
            //   1. CellSize 当前假设 m_TexSize=2048
            //   2. m_TexSize 和 CellSize 必须原子性同步变更
            //   3. ldc.i4 2048 替换的可靠性需要进一步验证
            // 完整方案: 需要同时修改 CellSize + m_TexSize + 验证 + 后备回退

            return patched;
        }

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_MapSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_MapSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.get_MapSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_CellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_CellSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.get_CellSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("get_WaveSpeed")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_WaveSpeed_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.get_WaveSpeed");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.3.6f版本增加
        [HarmonyPatch("get_BackdropCellSize")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_BackdropCellSize_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.get_BackdropCellSize");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.CalculateSourceMultiplier))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CalculateSourceMultiplier_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.CalculateSourceMultiplier");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.4.2f版本增加-mod v2.1.1
        [HarmonyPatch(typeof(WaterSystem), "InitBackdropTexture")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> InitBackdropTexture_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.InitBackdropTexture");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 1.3.6f版本已弃用，暂时不动
        [HarmonyPatch("HasWater")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> HasWater_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSystem.HasWater");
#endif
            // This method calls GetCell which uses GetCellCoords which uses mapSize.
            // Patching HasWater ensures the correct mapSize is passed down.
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
    }

    // =========================================================================
    // region: WaterSimulation Transpiler Patches
    // =========================================================================

    [HarmonyPatch(typeof(WaterSimulation))]
    static class WaterSimulationMethodPatches
    {
        private const string Tag = "WaterSimPatch";

        // v1.3.6f版本变动
        [HarmonyPatch(nameof(WaterSimulation.ResetToLevel))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> ResetToLevel_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulation.ResetToLevel");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.SourceStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulation.SourceStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.EvaporateStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulation.EvaporateStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.VelocityStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulation.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulation.DepthStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulation.DepthStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补
    }

    // =========================================================================
    // region: WaterSimulationLegacy Transpiler Patches
    // =========================================================================

    [HarmonyPatch(typeof(WaterSimulationLegacy))]
    static class WaterSimulationLegacyMethodPatches
    {
        private const string Tag = "WaterSimLegPatch";

        // v1.3.6f版本变动      

        [HarmonyPatch(nameof(WaterSimulationLegacy.SourceStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> SourceStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulationLegacy.SourceStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.EvaporateStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> EvaporateStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulationLegacy.EvaporateStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.VelocityStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> VelocityStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulationLegacy.VelocityStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch(nameof(WaterSimulationLegacy.DepthStep))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> DepthStep_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulationLegacy.DepthStep");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        [HarmonyPatch("BorderCircleIntersection")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> BorderCircleIntersection_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterSimulationLegacy.BorderCircleIntersection");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // 注意序列化/反序列化是否需要修补
    }

    // =========================================================================
    // region: WaterLevelChangeSystem Transpiler Patches
    // =========================================================================

    [HarmonyPatch(typeof(WaterLevelChangeSystem))]
    static class WaterLevelChangeSystemMethodPatches
    {
        private const string Tag = "WaterLevelPatch";

        // Properties are compiled to get_ / set_ methods
        [HarmonyPatch("get_TsunamiEndDelay")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> get_TsunamiEndDelay_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterLevelChangeSystem.get_TsunamiEndDelay");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }

        // vanilla未启用
        [HarmonyPatch("GetMinimumDelayAt")]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> GetMinimumDelayAt_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
#if DEBUG
            ModLog.Debug(Tag, "Transpiler: WaterLevelChangeSystem.GetMinimumDelayAt");
#endif
            return WaterSystemPatcherHelper.PatchMethodInstructions(instructions, WaterSystemPatcherHelper.GetCurrentMethodName());
        }
    }

    // =========================================================================
    // region: Shared Transpiler Helper (kMapSize / kCellSize IL替换)
    // =========================================================================

    /// <summary>
    /// Water 系列 Transpiler 的共享 IL 替换引擎。
    /// 扫描 IL 指令流，将 WaterSystem.kMapSize / kCellSize 字段加载替换为缩放后的常量。
    /// </summary>
    static class WaterSystemPatcherHelper
    {
        private const string Tag = "WaterHelper";

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
                ModLog.Warn(Tag, $"Failed to find kMapSize or kCellSize fields. Cannot patch {methodName}.");
                return instructions; // Return original instructions
            }

            // --- Configuration ---
            int MapScaleMultiplier = PatchManager.CurrentCoreValue;
            int OriginalKMapSize = PatchManager.OriginalMapSize;

            // --- 新 Water kMapSize ---
            int NewKMapSizeI = MapScaleMultiplier * OriginalKMapSize;
            float NewKMapSizeF = MapScaleMultiplier * OriginalKMapSize;

            // --- 新 Water CellSize ---
            // 新公式: CellSize = kMapSize_scaled / WaterTextureSize
            // 确保 kMapSize = kCellSize * m_TexSize 恒等式成立
            // 例: ModeA(57km) + Water2048: 57344 / 2048 = 28 (原版行为)
            float NewKCellSize = ResolutionManager.GetWaterCellSize(NewKMapSizeI);

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
                        patched = true;
#if DEBUG
                        ModLog.Debug(Tag, $"Patched kMapSize (as float) {codes[i].operand} in {methodName}");
#endif
                    }
                    else
                    {
                        // Replace 'ldsfld kMapSize' with 'ldc.i4 NewKMapSizeI'
                        codes[i].opcode = OpCodes.Ldc_I4;
                        codes[i].operand = NewKMapSizeI;
                        patched = true;
#if DEBUG
                        ModLog.Debug(Tag, $"Patched kMapSize (as int) to {codes[i].operand} in {methodName}");
#endif
                    }
                }
                // Check for loading kCellSize (static float)
                else if (codes[i].LoadsField(_kCellSizeField))
                {
                    // kCellSize is float. Replace 'ldsfld kCellSize' with 'ldc.r4 NewKCellSize'
                    codes[i].opcode = OpCodes.Ldc_R4;
                    codes[i].operand = NewKCellSize;
                    patched = true;
#if DEBUG
                    ModLog.Debug(Tag, $"Patched kCellSize to {codes[i].operand} in {methodName}");
#endif
                }
            }

            if (!patched)
            {
                ModLog.Warn(Tag, $"Method {methodName} targeted but no kMapSize/kCellSize found to patch.");
            }

            return codes;
        }

        public static string GetCurrentMethodName([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            return memberName;
        }
    }

}
