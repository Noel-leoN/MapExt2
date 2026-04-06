// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    using Game.Simulation;
    using HarmonyLib;
    using MapExtPDX.MapExt.Core;
    using Unity.Entities;
    using UnityEngine;
    using UnityEngine.Experimental.Rendering;
    using UnityEngine.Rendering;

    /// <summary>
    /// Layer 2: 地形欺骗适配器。
    /// 水模拟 ComputeShader 硬编码 4096/2048 (terrain/water)。
    /// 当地形级联纹理实际尺寸 > 4096 时，在水模拟执行期间，
    /// 将级联纹理临时替换为降采样到 4096 的版本。
    /// 
    /// 关键设计：延迟初始化。
    /// 级联纹理在 TerrainSystem.OnUpdate 中创建，
    /// 远晚于 Mod 加载时机，因此在首次 WaterSystem.OnUpdate 时
    /// 才读取实际尺寸并决定是否激活。
    /// </summary>
    public static class TerrainWaterAdapter
    {
        private const string Tag = "WaterAdapter";

        #region Fields

        // 降采样级联纹理 (Tex2DArray, 4 slices, R16_UNorm)
        private static RenderTexture s_DownsampledCascade;

        // 降采样 ObjectsLayer
        private static RenderTexture s_DownsampledObjectsLayer;

        // 临时 2D RT (用于逐 slice 提取 + Blit)
        private static RenderTexture s_TempSrcSlice;
        private static RenderTexture s_TempDstSlice;

        // 原始纹理引用 (用于 SwapOut 恢复)
        private static Texture s_OriginalCascade;
        private static Texture s_OriginalObjectsLayer;

        // TerrainSystem 的 Traverse 缓存
        private static Traverse s_CascadeField;
        private static Traverse s_ObjectsLayerField;

        // 目标级联尺寸 (4096, 着色器硬编码)
        private static int s_CascadeSize;

        #endregion

        #region State

        /// <summary>延迟初始化状态</summary>
        private enum AdapterState
        {
            /// <summary>未请求</summary>
            Idle,
            /// <summary>已请求，等待首次 OnUpdate 时实际初始化</summary>
            Pending,
            /// <summary>已初始化且激活（级联 > 4096）</summary>
            Active,
            /// <summary>已初始化但无需适配（级联 ≤ 4096）</summary>
            Skipped
        }

        private static AdapterState s_State = AdapterState.Idle;

        /// <summary>是否已激活且需要每帧交换</summary>
        public static bool IsActive => s_State == AdapterState.Active;

        #endregion

        #region Lifecycle

        /// <summary>
        /// 请求延迟初始化。在 WaterSystemReinitializer.Execute() 完成后调用。
        /// 不创建任何 RT，仅缓存 Traverse 并标记为 Pending。
        /// 实际 RT 创建延迟到首次 OnUpdate。
        /// </summary>
        public static void RequestInitialize()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var terrainSystem = world?.GetExistingSystemManaged<TerrainSystem>();
            if (terrainSystem == null)
            {
                ModLog.Error(Tag, "TerrainSystem not found!");
                return;
            }

            Dispose();

            var traverse = Traverse.Create(terrainSystem);
            s_CascadeField = traverse.Field("m_HeightmapCascade");
            s_ObjectsLayerField = traverse.Field("m_HeightmapObjectsLayer");

            if (!s_CascadeField.FieldExists() || !s_ObjectsLayerField.FieldExists())
            {
                ModLog.Error(Tag, "Could not find cascade/objectsLayer fields!");
                return;
            }

            s_CascadeSize = ResolutionManager.WaterTerrainResolution; // 4096
            s_State = AdapterState.Pending;
            ModLog.Info(Tag, $"Deferred init requested (target cascade={s_CascadeSize})");
        }

        /// <summary>
        /// 延迟初始化：在首次 OnUpdate 时调用。
        /// 读取实际级联纹理尺寸，决定是否需要适配。
        /// </summary>
        /// <returns>true: 激活成功; false: 无需适配或失败</returns>
        private static bool TryActivate()
        {
            var actualCascade = s_CascadeField.GetValue<RenderTexture>();
            if (actualCascade == null || !actualCascade.IsCreated())
            {
                // 级联仍未创建，继续等待
                return false;
            }

            int sourceSize = actualCascade.width;

            // 实际级联 ≤ 目标尺寸 → 无需适配
            if (sourceSize <= s_CascadeSize)
            {
                ModLog.Info(Tag, $"No adaptation needed (cascade={sourceSize}, target={s_CascadeSize})");
                s_State = AdapterState.Skipped;
                return false;
            }

            // === 创建降采样 RT ===
            ModLog.Patch(Tag, $"Activating: cascade {sourceSize} → {s_CascadeSize}");

            // 降采样级联 (Tex2DArray, 4 slices)
            s_DownsampledCascade = new RenderTexture(s_CascadeSize, s_CascadeSize, 0, GraphicsFormat.R16_UNorm)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = 4,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                enableRandomWrite = false,
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterCascade"
            };
            s_DownsampledCascade.Create();

            // 降采样 ObjectsLayer
            var origObjLayer = s_ObjectsLayerField.GetValue<RenderTexture>();
            var objFormat = GraphicsFormat.R8G8B8A8_UNorm;
            if (origObjLayer is RenderTexture objRT)
            {
                objFormat = objRT.graphicsFormat;
            }
            s_DownsampledObjectsLayer = new RenderTexture(s_CascadeSize, s_CascadeSize, 0, objFormat)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterObjectsLayer"
            };
            s_DownsampledObjectsLayer.Create();

            // 临时 RT: 源端匹配实际级联尺寸
            s_TempSrcSlice = new RenderTexture(sourceSize, sourceSize, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterTempSrc"
            };
            s_TempSrcSlice.Create();

            // 临时 RT: 目标端匹配降采样尺寸
            s_TempDstSlice = new RenderTexture(s_CascadeSize, s_CascadeSize, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterTempDst"
            };
            s_TempDstSlice.Create();

            s_State = AdapterState.Active;
            ModLog.Ok(Tag, $"Activated: cascade {sourceSize} → {s_CascadeSize}");
            return true;
        }

        /// <summary>释放所有 GPU 资源</summary>
        public static void Dispose()
        {
            SafeDestroy(ref s_DownsampledCascade);
            SafeDestroy(ref s_DownsampledObjectsLayer);
            SafeDestroy(ref s_TempSrcSlice);
            SafeDestroy(ref s_TempDstSlice);
            s_CascadeField = null;
            s_ObjectsLayerField = null;
            s_OriginalCascade = null;
            s_OriginalObjectsLayer = null;
            s_State = AdapterState.Idle;
        }

        #endregion

        #region Per-Frame Operations

        /// <summary>
        /// Prefix 入口: 处理延迟初始化 + 降采样 + 交换。
        /// </summary>
        public static void OnBeforeWaterUpdate()
        {
            // 延迟初始化
            if (s_State == AdapterState.Pending)
            {
                TryActivate();
            }

            // 仅在激活状态下执行交换
            if (s_State != AdapterState.Active) return;

            UpdateCascade();
            SwapIn();
        }

        /// <summary>
        /// Postfix 入口: 恢复原始纹理。
        /// </summary>
        public static void OnAfterWaterUpdate()
        {
            if (s_State != AdapterState.Active) return;
            SwapOut();
        }

        private static void UpdateCascade()
        {
            var originalCascade = s_CascadeField.GetValue<RenderTexture>();
            if (originalCascade == null || !originalCascade.IsCreated()) return;

            for (int slice = 0; slice < 4; slice++)
            {
                Graphics.CopyTexture(originalCascade, slice, 0, s_TempSrcSlice, 0, 0);
                Graphics.Blit(s_TempSrcSlice, s_TempDstSlice);
                Graphics.CopyTexture(s_TempDstSlice, 0, 0, s_DownsampledCascade, slice, 0);
            }

            var origObjLayer = s_ObjectsLayerField.GetValue<RenderTexture>();
            if (origObjLayer != null && origObjLayer.IsCreated())
            {
                Graphics.Blit(origObjLayer, s_DownsampledObjectsLayer);
            }
        }

        private static void SwapIn()
        {
            s_OriginalCascade = s_CascadeField.GetValue<RenderTexture>();
            s_OriginalObjectsLayer = s_ObjectsLayerField.GetValue<RenderTexture>();

            s_CascadeField.SetValue(s_DownsampledCascade);
            s_ObjectsLayerField.SetValue(s_DownsampledObjectsLayer);
        }

        private static void SwapOut()
        {
            if (s_OriginalCascade == null) return;

            s_CascadeField.SetValue(s_OriginalCascade);
            s_ObjectsLayerField.SetValue(s_OriginalObjectsLayer);

            s_OriginalCascade = null;
            s_OriginalObjectsLayer = null;
        }

        #endregion

        #region Helpers

        private static void SafeDestroy(ref RenderTexture rt)
        {
            if (rt != null)
            {
                if (rt.IsCreated()) rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                rt = null;
            }
        }

        #endregion
    }

    // ========================================================================
    // Harmony Patch
    // ========================================================================

    [HarmonyPatch(typeof(WaterSystem), "OnUpdate")]
    internal static class WaterSystem_OnUpdate_AdapterPatch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            TerrainWaterAdapter.OnBeforeWaterUpdate();
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            TerrainWaterAdapter.OnAfterWaterUpdate();
        }
    }
}
