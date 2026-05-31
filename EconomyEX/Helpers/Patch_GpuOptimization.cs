// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace EconomyEX.Helpers
{
    using Game.Simulation;
    using HarmonyLib;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Rendering;

    // === 优化 1: Backdrop 禁用 ===
    // 在 InitializeTerrainData 入口处将 worldMap 参数设为 null
    // 这样 SetWorldHeightmap(null) → DestroyWorldMap() → worldHeightmap = null
    // 然后 FinalizeTerrainData 中 worldHeightmap == null → baseLod = 0 → 无 Backdrop 路径
    // 性能收益: 每帧节省 ~0.5-2ms GPU + ~37MB VRAM + CPU 阻塞消除
    [HarmonyPatch(typeof(TerrainSystem), "InitializeTerrainData")]
    internal static class TerrainBackdropDisablePatch
    {
        private const string Tag = "GpuOpt";

        [HarmonyPrefix]
        public static void Prefix(
            ref Texture2D worldMap,
            ref float2 inMapSize, ref float2 inWorldSize,
            ref float2 inMapCorner, ref float2 inWorldCorner)
        {
            // 仅在用户启用 Backdrop 禁用时生效
            if (Mod.Instance?.Settings?.DisableWorldBackdrop != true) return;

            if (worldMap != null)
            {
                // 销毁反序列化创建的 worldMap 纹理，防止 GPU 内存泄漏
                Object.Destroy(worldMap);
                worldMap = null;

                // 同步 WorldSize = MapSize，确保 FinalizeTerrainData 中 baseLod = 0
                inWorldSize = inMapSize;
                inWorldCorner = inMapCorner;

                ModLog.Patch(Tag,
                    $"Backdrop disabled: worldMap nullified in InitializeTerrainData. " +
                    $"MapSize={inMapSize}, WorldSize forced to MapSize");
            }
        }
    }

    // === 优化 2: 水模拟运行时性能优化 ===
    // 拦截 WaterSystem.OnSimulateGPU，通过 WaterSimSpeed 控制模拟频率。
    // 可以实时调整，无需重启游戏。
    //
    // 设计原则:
    // 1. Prefix 始终 return true，不跳过 OnSimulateGPU。
    //    因为 Simulate() 末尾的 Active Tiles 更新、帧计数递增等维护逻辑
    //    必须每帧执行，否则水流扩散被 GPU 剔除冻结。
    // 2. 不干预 m_SimulateBackdrop。
    //    直接操控 m_SimulateBackdrop 字段会绕过 InitBackdropTexture()，
    //    导致 m_depthsBackdropReader.m_sourceTexture = null，
    //    切换回 Vanilla 时 CheckReadbacks → ExecuteReadBack 抛出 NullRef。
    // 3. 频率控制仅通过 WaterSimSpeed 属性实现。
    //    WaterSimSpeed=0 时 Simulate() 内部会跳过模拟循环，
    //    但仍保持 m_NextSimulationFrame 递增和 Active Tiles 更新。
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystemOptRuntimePatch
    {
        private static int s_frameCounter = 0;

        [HarmonyPrefix]
        static bool Prefix(WaterSystem __instance, CommandBuffer cmd)
        {
            var quality = Mod.Instance?.Settings?.WaterSimQuality ?? WaterSimQualitySetting.Vanilla_EveryFrame;

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
}
