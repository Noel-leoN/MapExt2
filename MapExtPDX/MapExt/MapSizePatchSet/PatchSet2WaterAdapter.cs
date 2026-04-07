// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using UnityEngine;
    using UnityEngine.Experimental.Rendering;
    using UnityEngine.Rendering;

    /// <summary>
    /// Layer 2: 水模拟地形欺骗 (v9 - 方法拦截 + 全局变量双通道)。
    ///
    /// 根因分析:
    /// WaterSimulation 通过以下两个通道读取地形级联纹理:
    ///   1. cmd.SetComputeTextureParam(..., m_TerrainSystem.GetCascadeTexture())
    ///      → 显式绑定到 compute kernel 参数 _Terrain
    ///   2. colossal_TerrainTextureArray 全局变量
    ///      → shader include 中可能引用
    ///
    /// 方案: 在 WaterSystem.OnSimulateGPU 窗口内:
    ///   - 拦截 GetCascadeTexture() 返回降采样 4096 版本 (通道1)
    ///   - 临时替换全局变量为 4096 版本 (通道2)
    ///   - Postfix 恢复一切
    ///
    /// 不修改 TerrainSystem 的任何字段。m_HeightmapCascade 始终保持 8192。
    /// </summary>
    internal static class WaterTerrainSwap
    {
        private const string Tag = "WaterTerrainSwap";

        #region Fields

        // 降采样资源
        private static RenderTexture s_DownsampledCascade;
        private static RenderTexture s_TempSrc;
        private static RenderTexture s_TempDst;

        // 全局变量保存/恢复
        private static Vector4 s_SavedGlobalLimit;
        private static Texture s_OrigCascade;

        private static readonly int s_ID_Array = Shader.PropertyToID("colossal_TerrainTextureArray");
        private static readonly int s_ID_Limit = Shader.PropertyToID("colossal_TerrainCascadeLimit");

        // 拦截开关
        private static bool s_InterceptActive;
        private static TerrainSystem s_TerrainSystem;
        private static int s_TargetSize;
        private static int s_LogCount;

        public static bool IsIntercepting => s_InterceptActive;
        public static RenderTexture DownsampledCascade => s_DownsampledCascade;

        #endregion

        #region Lifecycle

        /// <summary>
        /// OnSimulateGPU Prefix: 降采样 + 激活拦截 + 交换全局变量
        /// </summary>
        public static void BeforeSimulateGPU()
        {
            if (!ResolutionManager.NeedsDownsampleForWater)
                return;

            s_TargetSize = ResolutionManager.WaterTerrainResolution; // 4096

            // 获取 TerrainSystem
            if (s_TerrainSystem == null)
            {
                s_TerrainSystem = Unity.Entities.World.DefaultGameObjectInjectionWorld?
                    .GetExistingSystemManaged<TerrainSystem>();
            }
            if (s_TerrainSystem == null)
                return;

            // 获取原始级联纹理 (8192)
            var cascadeTex = s_TerrainSystem.GetCascadeTexture();
            if (cascadeTex == null || !(cascadeTex is RenderTexture srcCascade)
                || !srcCascade.IsCreated())
            {
                if (s_LogCount++ % 300 == 0)
                    ModLog.Warn(Tag, "Cascade not available yet");
                return;
            }

            if (srcCascade.width <= s_TargetSize)
                return;

            // 延迟创建降采样资源
            if (s_DownsampledCascade == null || !s_DownsampledCascade.IsCreated())
            {
                CreateResources(srcCascade.width);
                ModLog.Ok(Tag, $"ACTIVE: cascade {srcCascade.width} → {s_TargetSize} " +
                    "(GetCascadeTexture intercept + global swap)");
            }

            // 逐 slice 降采样
            for (int slice = 0; slice < 4; slice++)
            {
                Graphics.CopyTexture(srcCascade, slice, 0, s_TempSrc, 0, 0);
                Graphics.Blit(s_TempSrc, s_TempDst);
                Graphics.CopyTexture(s_TempDst, 0, 0, s_DownsampledCascade, slice, 0);
            }

            // 保存原始全局变量
            s_OrigCascade = srcCascade;
            s_SavedGlobalLimit = Shader.GetGlobalVector(s_ID_Limit);

            // 替换全局变量 (通道2)
            Shader.SetGlobalTexture(s_ID_Array, s_DownsampledCascade);
            Shader.SetGlobalVector(s_ID_Limit,
                new Vector4(0.5f / s_TargetSize, 0.5f / s_TargetSize, 0f, 0f));

            // 激活 GetCascadeTexture 拦截 (通道1)
            s_InterceptActive = true;
        }

        /// <summary>
        /// OnSimulateGPU Postfix: 恢复一切
        /// </summary>
        public static void AfterSimulateGPU()
        {
            if (!s_InterceptActive)
                return;

            s_InterceptActive = false;

            // 恢复全局变量
            if (s_OrigCascade != null)
                Shader.SetGlobalTexture(s_ID_Array, s_OrigCascade);
            Shader.SetGlobalVector(s_ID_Limit, s_SavedGlobalLimit);
            s_OrigCascade = null;
        }

        #endregion

        #region Helpers

        private static void CreateResources(int srcSize)
        {
            s_DownsampledCascade = new RenderTexture(s_TargetSize, s_TargetSize, 0, GraphicsFormat.R16_UNorm)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                enableRandomWrite = false,
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterTerrainDownsampled"
            };
            s_DownsampledCascade.Create();

            s_TempSrc = new RenderTexture(srcSize, srcSize, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterTerrainTempSrc"
            };
            s_TempSrc.Create();

            s_TempDst = new RenderTexture(s_TargetSize, s_TargetSize, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterTerrainTempDst"
            };
            s_TempDst.Create();
        }

        public static void Dispose()
        {
            s_InterceptActive = false;
            SafeDestroy(ref s_DownsampledCascade);
            SafeDestroy(ref s_TempSrc);
            SafeDestroy(ref s_TempDst);
            s_TerrainSystem = null;
        }

        private static void SafeDestroy(ref RenderTexture rt)
        {
            if (rt != null)
            {
                if (rt.IsCreated()) rt.Release();
                Object.DestroyImmediate(rt);
                rt = null;
            }
        }

        #endregion
    }

    // ========================================================================
    // Harmony Patches
    // ========================================================================

    /// <summary>
    /// WaterSystem.OnSimulateGPU 前后控制拦截窗口。
    /// GPU 水模拟 (VelocityStep, DepthStep 等) 在此方法中通过 CommandBuffer 驱动。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystem_OnSimulateGPU_Patch
    {
        [HarmonyPrefix]
        static void Prefix() => WaterTerrainSwap.BeforeSimulateGPU();

        [HarmonyPostfix]
        static void Postfix() => WaterTerrainSwap.AfterSimulateGPU();
    }

    /// <summary>
    /// 拦截 GetCascadeTexture: 在 GPU 水模拟期间返回降采样 4096 版本。
    /// WaterSimulation 通过 cmd.SetComputeTextureParam(..., GetCascadeTexture()) 
    /// 显式绑定纹理到 compute kernel，必须在此拦截。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.GetCascadeTexture))]
    internal static class TerrainSystem_GetCascadeTexture_Patch
    {
        [HarmonyPrefix]
        static bool Prefix(ref Texture __result)
        {
            if (WaterTerrainSwap.IsIntercepting && WaterTerrainSwap.DownsampledCascade != null)
            {
                __result = WaterTerrainSwap.DownsampledCascade;
                return false;
            }
            return true;
        }
    }
}
