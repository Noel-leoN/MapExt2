// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;
    using UnityEngine.Experimental.Rendering;
    using UnityEngine.Rendering;

    /// <summary>
    /// Layer 2: 级联纹理降采样 (v6 - Postfix 架构)。
    /// 在 FinalizeTerrainData 完成后，如果级联 > WaterTerrainResolution(4096)，
    /// 直接将 m_HeightmapCascade 和 m_CPUHeights 降采样到 4096。
    /// 所有系统（水模拟、渲染、CPU查询）都原生看到 4096 级联。
    /// 无需运行时交换、无需 Getter 拦截、无需全局变量替换。
    ///
    /// m_Heightmap (8192) 保持不变，用于：
    ///   - colossal_TerrainTexture 高精度地形渲染
    ///   - m_HeightmapDepth 也保持 8192
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "FinalizeTerrainData")]
    internal static class TerrainCascadeDownsamplePatch
    {
        private const string Tag = "CascadeCap";

        [HarmonyPostfix]
        static void Postfix(TerrainSystem __instance)
        {
            int maxCascade = ResolutionManager.WaterTerrainResolution; // 4096

            var traverse = Traverse.Create(__instance);
            var cascadeField = traverse.Field("m_HeightmapCascade");
            var objLayerField = traverse.Field("m_HeightmapObjectsLayer");
            var cpuHeightsField = traverse.Field("m_CPUHeights");

            var cascade = cascadeField.GetValue<RenderTexture>();
            if (cascade == null || cascade.width <= maxCascade)
                return; // 无需降采样

            int srcSize = cascade.width;
            ModLog.Patch(Tag, $"Downsampling cascade: {srcSize} → {maxCascade}");

            // === 1. 创建新的 4096 级联 ===
            var newCascade = new RenderTexture(maxCascade, maxCascade, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                enableRandomWrite = false,
                name = "TerrainHeightsCascade",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 4
            };
            newCascade.Create();

            // === 2. 逐 slice 降采样 ===
            var tempSrc = RenderTexture.GetTemporary(srcSize, srcSize, 0, RenderTextureFormat.R16);
            var tempDst = RenderTexture.GetTemporary(maxCascade, maxCascade, 0, RenderTextureFormat.R16);

            for (int slice = 0; slice < 4; slice++)
            {
                Graphics.CopyTexture(cascade, slice, 0, tempSrc, 0, 0);
                Graphics.Blit(tempSrc, tempDst);
                Graphics.CopyTexture(tempDst, 0, 0, newCascade, slice, 0);
            }

            RenderTexture.ReleaseTemporary(tempSrc);
            RenderTexture.ReleaseTemporary(tempDst);

            // === 3. 替换级联字段 ===
            cascade.Release();
            Object.DestroyImmediate(cascade);
            cascadeField.SetValue(newCascade);

            // === 4. 降采样 ObjectsLayer ===
            var objLayer = objLayerField.GetValue<RenderTexture>();
            if (objLayer != null && objLayer.width > maxCascade)
            {
                var newObjLayer = new RenderTexture(maxCascade, maxCascade, 0, objLayer.graphicsFormat)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    enableRandomWrite = false,
                    name = "TerrainHeightsObjectsLayer",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    dimension = TextureDimension.Tex2D
                };
                newObjLayer.Create();
                Graphics.Blit(objLayer, newObjLayer);
                objLayer.Release();
                Object.DestroyImmediate(objLayer);
                objLayerField.SetValue(newObjLayer);
            }

            // === 4b. 重建 HeightmapDepth 以匹配级联尺寸 ===
            // TerrainSystem.OnUpdate 使用 SetRenderTarget(cascade, slice, depth, 0)
            // color 和 depth 尺寸必须一致，否则每帧产生错误
            var depthField = traverse.Field("m_HeightmapDepth");
            var depth = depthField.GetValue<RenderTexture>();
            if (depth != null && depth.width > maxCascade)
            {
                var newDepth = new RenderTexture(maxCascade, maxCascade, 16,
                    RenderTextureFormat.Depth, RenderTextureReadWrite.Linear)
                {
                    name = "HeightmapDepth"
                };
                newDepth.Create();
                depth.Release();
                Object.DestroyImmediate(depth);
                depthField.SetValue(newDepth);
                ModLog.Ok(Tag, $"HeightmapDepth resized: {srcSize} → {maxCascade}");
            }

            // === 5. 降采样 CPUHeights 以匹配新级联尺寸 ===
            // GetHeightData 验证: m_CPUHeights.Length == cascade.width * cascade.height
            var cpuHeights = cpuHeightsField.GetValue<NativeArray<ushort>>();
            if (cpuHeights.IsCreated && cpuHeights.Length == srcSize * srcSize)
            {
                int newLen = maxCascade * maxCascade;
                var newCpuHeights = new NativeArray<ushort>(newLen, Allocator.Persistent);
                int ratio = srcSize / maxCascade; // 2

                // 点采样降采样 (简单、快速、无精度损失)
                for (int y = 0; y < maxCascade; y++)
                {
                    for (int x = 0; x < maxCascade; x++)
                    {
                        newCpuHeights[y * maxCascade + x] = cpuHeights[(y * ratio) * srcSize + (x * ratio)];
                    }
                }

                cpuHeights.Dispose();
                cpuHeightsField.SetValue(newCpuHeights);
                ModLog.Ok(Tag, $"CPUHeights downsampled: {srcSize}² → {maxCascade}² (ratio={ratio})");
            }

            // === 6. 更新全局着色器变量 ===
            Shader.SetGlobalTexture("colossal_TerrainTextureArray", newCascade);
            Shader.SetGlobalVector("colossal_TerrainCascadeLimit",
                new Vector4(0.5f / maxCascade, 0.5f / maxCascade, 0f, 0f));

            ModLog.Ok(Tag, $"Cascade capped: {srcSize} → {maxCascade}, all references updated");
        }
    }
}
