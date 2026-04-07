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
    /// Layer 2: 水模拟地形欺骗 (v8 - 全局变量交换)。
    ///
    /// 设计发现：WaterSystem/WaterSimulation 不直接引用 TerrainSystem，
    /// 水 compute shader 通过全局变量 colossal_TerrainTextureArray 读取地形数据。
    ///
    /// 方案：在 WaterSystem.OnSimulateGPU 前后交换全局变量：
    ///   Prefix:  colossal_TerrainTextureArray → 降采样 4096 版本
    ///            colossal_TerrainCascadeLimit  → 基于 4096 的值
    ///   Postfix: 恢复原始值
    ///
    /// 不修改 TerrainSystem 的任何字段或内部状态。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSimGPU_TerrainSwapPatch
    {
        private const string Tag = "WaterTerrainSwap";

        #region Fields

        private static RenderTexture s_DownsampledCascade;
        private static RenderTexture s_TempSrc;
        private static RenderTexture s_TempDst;

        private static Texture s_SavedGlobalTex;
        private static Vector4 s_SavedGlobalLimit;

        private static readonly int s_ID_Array = Shader.PropertyToID("colossal_TerrainTextureArray");
        private static readonly int s_ID_Limit = Shader.PropertyToID("colossal_TerrainCascadeLimit");

        private static int s_TargetSize;
        private static bool s_Active;
        private static int s_LogCount;

        #endregion

        #region Prefix / Postfix

        [HarmonyPrefix]
        static void Prefix()
        {
            if (!ResolutionManager.NeedsDownsampleForWater)
                return;

            s_TargetSize = ResolutionManager.WaterTerrainResolution; // 4096

            // 获取当前全局纹理（即 m_HeightmapCascade）
            var globalTex = Shader.GetGlobalTexture(s_ID_Array);
            if (globalTex == null || !(globalTex is RenderTexture srcCascade))
            {
                if (s_LogCount++ % 300 == 0)
                    ModLog.Warn(Tag, "Global cascade texture not available yet");
                return;
            }

            if (srcCascade.width <= s_TargetSize)
                return; // 无需降采样

            // 延迟创建降采样 RT
            if (s_DownsampledCascade == null || !s_DownsampledCascade.IsCreated())
            {
                CreateDownsampleResources(srcCascade);
                ModLog.Ok(Tag, $"Created downsample resources: {srcCascade.width} → {s_TargetSize}");
            }

            // 降采样: 逐 slice Blit (8192 Tex2DArray → 4096 Tex2DArray)
            for (int slice = 0; slice < 4; slice++)
            {
                Graphics.CopyTexture(srcCascade, slice, 0, s_TempSrc, 0, 0);
                Graphics.Blit(s_TempSrc, s_TempDst);
                Graphics.CopyTexture(s_TempDst, 0, 0, s_DownsampledCascade, slice, 0);
            }

            // 保存并替换全局变量
            s_SavedGlobalTex = globalTex;
            s_SavedGlobalLimit = Shader.GetGlobalVector(s_ID_Limit);

            Shader.SetGlobalTexture(s_ID_Array, s_DownsampledCascade);
            Shader.SetGlobalVector(s_ID_Limit,
                new Vector4(0.5f / s_TargetSize, 0.5f / s_TargetSize, 0f, 0f));

            s_Active = true;
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            if (!s_Active)
                return;

            // 恢复原始全局变量
            if (s_SavedGlobalTex != null)
                Shader.SetGlobalTexture(s_ID_Array, s_SavedGlobalTex);
            Shader.SetGlobalVector(s_ID_Limit, s_SavedGlobalLimit);

            s_SavedGlobalTex = null;
            s_Active = false;
        }

        #endregion

        #region Helpers

        private static void CreateDownsampleResources(RenderTexture srcCascade)
        {
            int srcSize = srcCascade.width;

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

        // 供外部调用的清理方法
        public static void Dispose()
        {
            SafeDestroy(ref s_DownsampledCascade);
            SafeDestroy(ref s_TempSrc);
            SafeDestroy(ref s_TempDst);
            s_Active = false;
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
}
