// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using MapExtPDX.MapExt.Core;

    /// <summary>
    /// 地形→水适配层：当地形分辨率 > 4096 时，维护一份 4096² 降采样副本供水模拟使用。
    /// 水模拟的 Compute Shader 隐式假设地形/水分辨率 = 2:1 (原版 4096:2048)。
    /// 当地形使用 8192 时，需要提供 4096 的降采样版本给水系统，保持坐标映射正确。
    ///
    /// 调用流程:
    /// 1. PatchManager → Initialize() : 创建降采样纹理
    /// 2. TerrainSystem.FinalizeTerrainData 后 → UpdateDownsample() : 执行降采样
    /// 3. WaterSimulation 各 Step 中 → GetCascadeForWater() : 返回降采样纹理
    /// </summary>
    public static class TerrainWaterAdapter
    {
        private const string Tag = "WaterAdapter";

        // TODO [Phase 2]: 接入 TerrainSystem 生命周期
        // 需要 Harmony Postfix Hook 到 TerrainSystem.FinalizeTerrainData
        // 以驱动 UpdateDownsample() 实际执行降采样

        #region Fields

        /// <summary>降采样级联纹理 (Tex2DArray, 4 slices, R16_UNorm → 4096²)</summary>
        private static RenderTexture s_DownsampledCascade;

        /// <summary>降采样 ObjectsLayer 纹理 (2D, 4096²)</summary>
        private static RenderTexture s_DownsampledObjectsLayer;

        /// <summary>临时中转 RT，用于从 Tex2DArray slice 读取</summary>
        private static RenderTexture s_TempSliceRT;

        private static bool s_Initialized;

        #endregion

        #region Public API

        /// <summary>
        /// 初始化降采样纹理。仅在 NeedsDownsampleForWater = true 时创建。
        /// </summary>
        public static void Initialize()
        {
            if (!ResolutionManager.NeedsDownsampleForWater)
            {
                ModLog.Info(Tag, "TerrainResolution <= WaterTerrainResolution, adapter not needed.");
                s_Initialized = false;
                return;
            }

            Dispose(); // 安全清理旧资源

            int targetRes = ResolutionManager.WaterTerrainResolution; // 4096
            int sourceRes = ResolutionManager.TerrainResolution;      // 8192

            try
            {
                // === 创建降采样级联 (Tex2DArray, 4 slices) ===
                s_DownsampledCascade = new RenderTexture(targetRes, targetRes, 0, RenderTextureFormat.RHalf)
                {
                    dimension = TextureDimension.Tex2DArray,
                    volumeDepth = 4,
                    useMipMap = false,
                    autoGenerateMips = false,
                    enableRandomWrite = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "TerrainWaterAdapter_Cascade_4096"
                };
                s_DownsampledCascade.Create();

                // === 创建降采样 ObjectsLayer (2D) ===
                s_DownsampledObjectsLayer = new RenderTexture(targetRes, targetRes, 0, RenderTextureFormat.RHalf)
                {
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "TerrainWaterAdapter_ObjectsLayer_4096"
                };
                s_DownsampledObjectsLayer.Create();

                // === 创建临时中转 RT (用于 slice 读取) ===
                s_TempSliceRT = new RenderTexture(sourceRes, sourceRes, 0, RenderTextureFormat.RHalf)
                {
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "TerrainWaterAdapter_TempSlice"
                };
                s_TempSliceRT.Create();

                s_Initialized = true;
                ModLog.Ok(Tag, $"Initialized: {sourceRes}² → {targetRes}² downsampling adapter created.");
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, $"Failed to create downsampling textures: {e.Message}");
                Dispose();
            }
        }

        /// <summary>
        /// 执行降采样：将高分辨率级联纹理降采样到 4096²。
        /// 在 TerrainSystem.FinalizeTerrainData 完成后调用（通过 Postfix 挂钩）。
        /// 仅在地形数据更新时需要调用，不是每帧调用。
        /// </summary>
        /// <param name="sourceCascade">高分辨率级联纹理 (Tex2DArray, 8192²)</param>
        /// <param name="sourceObjectsLayer">高分辨率 ObjectsLayer 纹理 (2D, 8192²)</param>
        public static void UpdateDownsample(RenderTexture sourceCascade, RenderTexture sourceObjectsLayer)
        {
            if (!s_Initialized || !ResolutionManager.NeedsDownsampleForWater)
                return;

            if (sourceCascade == null)
            {
                ModLog.Warn(Tag, "sourceCascade is null, skipping downsample.");
                return;
            }

            try
            {
                // === 降采样级联纹理 (逐 slice) ===
                // Tex2DArray 不能直接 Blit，需要：
                // 1. CopyTexture 从源 slice → 临时 2D RT (同尺寸，无重采样)
                // 2. Blit 从临时 RT → 目标 slice RT (执行双线性重采样)

                int sliceCount = Mathf.Min(sourceCascade.volumeDepth, 4);

                for (int slice = 0; slice < sliceCount; slice++)
                {
                    // Step 1: 从源 Tex2DArray 的第 slice 层复制到临时 2D RT
                    // CopyTexture 要求兼容格式，只做像素搬运不重采样
                    Graphics.CopyTexture(
                        sourceCascade, slice, 0,     // src: slice index, mip 0
                        s_TempSliceRT, 0, 0          // dst: 2D RT, element 0, mip 0
                    );

                    // Step 2: Blit 从临时 RT (8192²) 到降采样目标 (4096²)
                    // Blit 会执行双线性缩放
                    // 注意: Blit 不能直接写入 Tex2DArray 的指定 slice
                    // 需要用 CommandBuffer 或创建临时 2D RT 再 CopyTexture 回去

                    // 创建一个临时的目标尺寸 RT 用于 Blit
                    var tempTarget = RenderTexture.GetTemporary(
                        ResolutionManager.WaterTerrainResolution,
                        ResolutionManager.WaterTerrainResolution,
                        0, RenderTextureFormat.RHalf);
                    tempTarget.filterMode = FilterMode.Bilinear;

                    Graphics.Blit(s_TempSliceRT, tempTarget);

                    // Step 3: 从 tempTarget 复制回 Tex2DArray 的对应 slice
                    Graphics.CopyTexture(
                        tempTarget, 0, 0,
                        s_DownsampledCascade, slice, 0
                    );

                    RenderTexture.ReleaseTemporary(tempTarget);
                }

                // === 降采样 ObjectsLayer (2D → 2D, 简单 Blit) ===
                if (sourceObjectsLayer != null)
                {
                    Graphics.Blit(sourceObjectsLayer, s_DownsampledObjectsLayer);
                }
            }
            catch (Exception e)
            {
                ModLog.Error(Tag, $"Downsample failed: {e.Message}");
            }
        }

        /// <summary>
        /// 获取给水系统用的级联纹理。
        /// 如果不需要降采样，返回原始纹理。
        /// </summary>
        /// <param name="originalCascade">TerrainSystem.GetCascadeTexture() 的原始返回值</param>
        /// <returns>水系统应该使用的纹理</returns>
        public static RenderTexture GetCascadeForWater(RenderTexture originalCascade)
        {
            if (s_Initialized && s_DownsampledCascade != null && s_DownsampledCascade.IsCreated())
                return s_DownsampledCascade;

            return originalCascade;
        }

        /// <summary>
        /// 获取给水系统用的 ObjectsLayer 纹理。
        /// </summary>
        /// <param name="originalObjectsLayer">TerrainSystem.GetObjectsLayerTexture() 的原始返回值</param>
        /// <returns>水系统应该使用的纹理</returns>
        public static RenderTexture GetObjectsLayerForWater(RenderTexture originalObjectsLayer)
        {
            if (s_Initialized && s_DownsampledObjectsLayer != null && s_DownsampledObjectsLayer.IsCreated())
                return s_DownsampledObjectsLayer;

            return originalObjectsLayer;
        }

        /// <summary>
        /// 释放所有降采样纹理资源。
        /// 在 Mod 卸载时调用。
        /// </summary>
        public static void Dispose()
        {
            if (s_DownsampledCascade != null)
            {
                s_DownsampledCascade.Release();
                UnityEngine.Object.Destroy(s_DownsampledCascade);
                s_DownsampledCascade = null;
            }

            if (s_DownsampledObjectsLayer != null)
            {
                s_DownsampledObjectsLayer.Release();
                UnityEngine.Object.Destroy(s_DownsampledObjectsLayer);
                s_DownsampledObjectsLayer = null;
            }

            if (s_TempSliceRT != null)
            {
                s_TempSliceRT.Release();
                UnityEngine.Object.Destroy(s_TempSliceRT);
                s_TempSliceRT = null;
            }

            s_Initialized = false;
        }

        #endregion
    }
}
