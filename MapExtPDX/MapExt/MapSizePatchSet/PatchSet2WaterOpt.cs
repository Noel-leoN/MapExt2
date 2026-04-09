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
    /// 拦截 WaterSystem.OnSimulateGPU，根据频率控制调度，并精细开关各种耗性能特性。
    /// 可以实时调整，无需重启游戏。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystemOptRuntimePatch
    {
        private static int s_frameCounter = 0;

        private static readonly System.Reflection.FieldInfo s_SimulateBackdropField = AccessTools.Field(typeof(WaterSystem), "m_SimulateBackdrop");

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance, CommandBuffer cmd)
        {
            // 反射失败时不干预原版逻辑 (防止游戏更新后字段重命名导致 NullRef)
            if (s_SimulateBackdropField == null)
                return true;

            var quality = ResolutionManager.WaterSimQuality;
            bool targetSimulateBackdrop = true;
            bool targetBlurFlowMap = true;
            bool skipFrame = false;

            switch (quality)
            {
                case WaterSimQualitySetting.Paused_NoFlow:
                    targetSimulateBackdrop = false;
                    targetBlurFlowMap = false;
                    skipFrame = true;
                    break;

                case WaterSimQualitySetting.Minimal_Every4Frames:
                    targetSimulateBackdrop = false;
                    targetBlurFlowMap = false;
                    
                    s_frameCounter++;
                    if (s_frameCounter % 4 != 0)
                    {
                        skipFrame = true;
                    }
                    else
                    {
                        // Reset counter to avoid overflow
                        if (s_frameCounter >= 10000) s_frameCounter = 0;
                    }
                    break;

                case WaterSimQualitySetting.Reduced_NoBackdrop:
                    targetSimulateBackdrop = false;
                    targetBlurFlowMap = true;
                    break;

                case WaterSimQualitySetting.Vanilla_EveryFrame:
                default:
                    targetSimulateBackdrop = true;
                    targetBlurFlowMap = true;
                    break;
            }

            // 更新状态项（避免调用 public setter 触发 OnBackdropActiveChanged 导致 DivideByZeroException）
            bool currentSimulateBackdrop = __instance.simulateBackdrop;
            if (currentSimulateBackdrop != targetSimulateBackdrop)
            {
                s_SimulateBackdropField.SetValue(__instance, targetSimulateBackdrop);
                // ModLog.Info("WaterSystemOpt", $"Updated m_SimulateBackdrop = {targetSimulateBackdrop}");
            }

            if (__instance.BlurFlowMap != targetBlurFlowMap)
            {
                __instance.BlurFlowMap = targetBlurFlowMap;
            }

            if (skipFrame)
            {
                // 如果跳过此帧，则拦截执行原始方法的其余部分
                return false;
            }

            return true;
        }
    }
}
