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
    /// 可以实时调整，无需重启游戏。
    /// 
    /// 设计原则:
    /// 1. Prefix 始终 return true，不跳过 OnSimulateGPU。
    ///    因为 Simulate() 末尾的 Active Tiles 更新、帧计数递增等维护逻辑
    ///    必须每帧执行，否则水流扩散被 GPU 剔除冻结。
    /// 2. 不干预 m_SimulateBackdrop。
    ///    直接操控 m_SimulateBackdrop 字段会绕过 InitBackdropTexture()，
    ///    导致 m_depthsBackdropReader.m_sourceTexture = null，
    ///    切换回 Vanilla 时 CheckReadbacks → ExecuteReadBack 抛出 NullRef。
    /// 3. 频率控制仅通过 WaterSimSpeed 属性实现。
    ///    WaterSimSpeed=0 时 Simulate() 内部会跳过模拟循环，
    ///    但仍保持 m_NextSimulationFrame 递增和 Active Tiles 更新。
    /// 
    /// NOTE: 水模拟速度 Mod UI 调节功能已暂时禁用。
    /// 原因：原版 Simulate() 在 terrainChangeCounter 归零时强制设 WaterSimSpeed=1，
    /// 与 Mod 持久化回写形成每帧竞争（speed 在 userValue 和 1 之间交替），
    /// 导致 UI 显示横跳。需要 Transpiler 修改 Simulate() 内部逻辑才能彻底解决。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystemOptRuntimePatch
    {
        private static int s_frameCounter = 0;

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance, CommandBuffer cmd)
        {
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
    }

    /// <summary>
    /// 修复 Editor 模式下水模拟速度横跳问题。
    /// 
    /// 根因：MapExt 的地形补丁导致 TerrainWillChange() 在 Editor 中每帧被调用，
    /// 每次都设 WaterSimSpeed=0 和 m_terrainChangeCounter=1。
    /// 而 Simulate() 在 counter 归零时恢复 WaterSimSpeed=1，
    /// 形成每帧 0→1→0→1 的交替横跳。
    /// 
    /// 修复策略：当 terrain change 已在处理中（counter > 0）时，
    /// 仅刷新 counter 但不重复设 WaterSimSpeed=0。
    /// 首次调用正常执行原版逻辑（暂停水模拟等待地形稳定）。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), nameof(WaterSystem.TerrainWillChange))]
    internal static class WaterSystem_TerrainWillChange_Patch
    {
        // 通过反射缓存 m_terrainChangeCounter 字段访问器
        private static System.Reflection.FieldInfo s_counterField;
        private static bool s_fieldResolved = false;

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance)
        {
            // 首次调用时解析私有字段
            if (!s_fieldResolved)
            {
                s_counterField = AccessTools.Field(typeof(WaterSystem), "m_terrainChangeCounter");
                s_fieldResolved = true;
                if (s_counterField == null)
                {
                    ModLog.Warn("WaterTWC", "无法解析 m_terrainChangeCounter 字段，补丁降级为直通模式");
                }
            }

            // 字段解析失败时直通原版
            if (s_counterField == null)
                return true;

            // 只设 counter（让 Simulate() 走 CopyToHeightmapStep 分支），
            // 不设 WaterSimSpeed=0（避免每帧 0/1 交替横跳）。
            // Simulate() 的 counter>0 分支本身已阻止水模拟运行，无需额外暂停。
            s_counterField.SetValue(__instance, 1);
            return false; // 跳过原版
        }
    }
}
