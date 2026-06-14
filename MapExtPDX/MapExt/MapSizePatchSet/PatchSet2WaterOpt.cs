// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using UnityEngine.Rendering;

    /// <summary>
    /// 水模拟运行时性能优化补丁。
    /// 拦截 WaterSystem.OnSimulateGPU，通过 WaterSimSpeed 控制模拟频率。
    /// 
    /// 同时包含 Editor 水速横跳修复：
    /// TerrainWillChange() 在 Editor 中被每帧调用（MapExt 地形补丁导致），
    /// 每次设 WaterSimSpeed=0。Simulate() 在 counter 归零时恢复 speed=1。
    /// 此 0/1 交替导致原版 Editor Water 面板（DebugSystem）显示横跳。
    /// 
    /// 修复策略：在 Postfix 中检测 speed 是否被 TerrainWillChange 瞬态置 0，
    /// 若 Simulate 已将其恢复为 1（说明 terrain change 已处理完毕），
    /// 则保持该恢复值。这个方案不依赖对 TerrainWillChange 的 hook
    /// （该方法可能被 JIT 内联导致 Harmony detour 无效）。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystemOptRuntimePatch
    {
        private static int s_frameCounter = 0;

        /// <summary>
        /// 记录用户设定的 speed 值（> 1 时才更新）。
        /// TerrainWillChange 只设 0，Simulate() 只设 0/1，
        /// 因此 speed > 1 只可能来自用户。
        /// </summary>
        internal static int StableSpeed = 1;

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance, CommandBuffer cmd)
        {
            // === 捕获用户意图值（在 Simulate 覆盖前） ===
            // Postfix 每帧将 speed 恢复为 StableSpeed，
            // 所以正常帧间 preSpeed 只可能是 StableSpeed 值或 0（TerrainWillChange）。
            // 若 preSpeed 不等于 StableSpeed 且 > 0，则必定是用户通过 Editor 面板设置的新值。
            int preSpeed = __instance.WaterSimSpeed;
            if (preSpeed > 0 && preSpeed != StableSpeed)
            {
                StableSpeed = preSpeed;
            }

            var quality = ResolutionManager.WaterSimQuality;

            // Vanilla 模式：完全不干预，零开销直通原版
            if (quality == WaterSimQualitySetting.Vanilla_EveryFrame)
                return true;

            switch (quality)
            {
                case WaterSimQualitySetting.Paused_NoFlow:
                    // 完全暂停模拟，但 Simulate() 仍然执行维护逻辑
                    __instance.WaterSimSpeed = 0;
                    __instance.BlurFlowMap = false;
                    break;

                case WaterSimQualitySetting.Minimal_Every4Frames:
                    // 每 4 帧执行一次完整 GPU 模拟，其余帧仅维护状态
                    s_frameCounter++;
                    if (s_frameCounter % 4 != 0)
                    {
                        __instance.WaterSimSpeed = 0;
                    }
                    else
                    {
                        __instance.WaterSimSpeed = 1;
                        if (s_frameCounter >= 10000) s_frameCounter = 0;
                    }
                    __instance.BlurFlowMap = false;
                    break;

                case WaterSimQualitySetting.Reduced_NoBackdrop:
                    // 每帧模拟，关闭 Flow Blur 节省部分 GPU 开销
                    __instance.BlurFlowMap = true;
                    break;

                case WaterSimQualitySetting.Vanilla_EveryFrame:
                default:
                    // 完全原版行为，不干预任何参数
                    __instance.BlurFlowMap = true;
                    break;
            }

            // 始终执行原版 OnSimulateGPU，不跳帧
            return true;
        }

        /// <summary>
        /// Postfix: 修复 Editor 水速横跳。
        /// 
        /// 机制：TerrainWillChange() 可能被 JIT 内联到 TerrainSystem.UpdateCascades 中，
        /// 导致 Harmony 无法 hook 它。因此在 OnSimulateGPU 完成后修复 speed 值。
        /// 
        /// Simulate() 内部流程（每帧）：
        /// 1. counter > 0: counter--, 当 counter==0 时设 speed=1（terrain 恢复完成）
        /// 2. speed > 0: 执行水模拟
        /// 3. OnSimulateGPU 返回后：TerrainWillChange 可能设 speed=0, counter=1
        /// 
        /// Postfix 在步骤 2 之后、步骤 3 之前执行。
        /// 此时 speed 应为 Simulate() 的最终意图值。
        /// 如果 speed=0 且非用户手动暂停（Paused_NoFlow），则恢复为 StableSpeed。
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(WaterSystem __instance)
        {
            int postSpeed = __instance.WaterSimSpeed;
            
            if (postSpeed == 0 && ResolutionManager.WaterSimQuality != WaterSimQualitySetting.Paused_NoFlow)
            {
                // speed=0 来自 TerrainWillChange 的瞬态重置 → 恢复用户值
                __instance.WaterSimSpeed = StableSpeed;
            }
            else if (postSpeed == 1 && StableSpeed > 1)
            {
                // Simulate() L1430 硬编码 speed=1（counter→0），
                // 但用户通过 Editor 面板设了更高值 → 恢复用户值
                __instance.WaterSimSpeed = StableSpeed;
            }
            // postSpeed > 1 → 用户刚设的新值，不干预
            // postSpeed == 1 && StableSpeed == 1 → 默认状态，不干预
        }
    }

    /// <summary>
    /// TerrainWillChange 防重复补丁（可能因 JIT 内联而不生效，
    /// 但保留作为第一道防线。主要修复逻辑在 OnSimulateGPU Postfix 中）。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.TerrainWillChange))]
    internal static class WaterSystem_TerrainWillChange_Patch
    {
        private static System.Reflection.FieldInfo s_counterField;
        private static bool s_fieldResolved = false;

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance)
        {
            if (!s_fieldResolved)
            {
                s_counterField = AccessTools.Field(typeof(WaterSystem), "m_terrainChangeCounter");
                s_fieldResolved = true;
            }

            if (s_counterField == null)
                return true;

            // 只设 counter，不碰 speed
            s_counterField.SetValue(__instance, 1);
            return false;
        }
    }
}
