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
    /// Layer 2: 地形欺骗适配器 (v5)。
    /// 水模拟 ComputeShader 硬编码 4096/2048 (terrain/water)。
    /// 当地形级联纹理实际尺寸 > 4096 时，在水 GPU 模拟期间：
    ///   1. 拦截 GetCascadeTexture/GetObjectsLayerTexture 返回降采样 4096 版本
    ///   2. 临时替换全局着色器变量 colossal_TerrainTextureArray / CascadeLimit
    ///      （计算着色器可能通过 #include 读取全局纹理而非显式参数）
    /// </summary>
    public static class TerrainWaterAdapter
    {
        private const string Tag = "WaterAdapter";

        #region Fields

        private static RenderTexture s_DownsampledCascade;
        private static RenderTexture s_DownsampledObjectsLayer;
        private static RenderTexture s_TempSrcSlice;
        private static RenderTexture s_TempDstSlice;

        private static Traverse s_CascadeField;
        private static Traverse s_ObjectsLayerField;

        private static int s_CascadeSize;
        private static int s_SourceCascadeSize;
        private static int s_CallCount;

        // Getter 拦截开关
        private static bool s_InterceptActive;

        // 保存原始全局着色器变量
        private static Texture s_SavedGlobalCascadeArray;
        private static Vector4 s_SavedGlobalCascadeLimit;

        // 全局着色器变量 ID (缓存)
        private static readonly int s_ID_CascadeArray = Shader.PropertyToID("colossal_TerrainTextureArray");
        private static readonly int s_ID_CascadeLimit = Shader.PropertyToID("colossal_TerrainCascadeLimit");

        #endregion

        #region State

        private enum AdapterState { Idle, Pending, Active, Skipped }
        private static AdapterState s_State = AdapterState.Idle;

        public static bool IsIntercepting => s_InterceptActive;
        public static RenderTexture DownsampledCascade => s_DownsampledCascade;
        public static RenderTexture DownsampledObjectsLayer => s_DownsampledObjectsLayer;

        #endregion

        #region Lifecycle

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
            s_CallCount = 0;
            s_InterceptActive = false;
            s_State = AdapterState.Pending;
            ModLog.Info(Tag, $"Deferred init requested (target cascade={s_CascadeSize})");
        }

        private static bool TryActivate()
        {
            var actualCascade = s_CascadeField.GetValue<RenderTexture>();
            if (actualCascade == null || !actualCascade.IsCreated())
            {
                if (s_CallCount % 100 == 0)
                    ModLog.Warn(Tag, $"[frame {s_CallCount}] Waiting for cascade...");
                return false;
            }

            s_SourceCascadeSize = actualCascade.width;

            if (s_SourceCascadeSize <= s_CascadeSize)
            {
                ModLog.Ok(Tag, $"No adaptation needed (cascade={s_SourceCascadeSize} ≤ {s_CascadeSize})");
                s_State = AdapterState.Skipped;
                return false;
            }

            // === 创建降采样 RT ===
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

            var origObjLayer = s_ObjectsLayerField.GetValue<RenderTexture>();
            var objFormat = origObjLayer != null ? origObjLayer.graphicsFormat : GraphicsFormat.R16_UNorm;
            s_DownsampledObjectsLayer = new RenderTexture(s_CascadeSize, s_CascadeSize, 0, objFormat)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterObjectsLayer"
            };
            s_DownsampledObjectsLayer.Create();

            s_TempSrcSlice = new RenderTexture(s_SourceCascadeSize, s_SourceCascadeSize, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterTempSrc"
            };
            s_TempSrcSlice.Create();

            s_TempDstSlice = new RenderTexture(s_CascadeSize, s_CascadeSize, 0, GraphicsFormat.R16_UNorm)
            {
                hideFlags = HideFlags.HideAndDontSave,
                name = "WaterAdapterTempDst"
            };
            s_TempDstSlice.Create();

            s_State = AdapterState.Active;
            ModLog.Ok(Tag, $"ACTIVATED: cascade {s_SourceCascadeSize} → {s_CascadeSize} " +
                $"(method intercept + global shader swap)");
            return true;
        }

        public static void Dispose()
        {
            s_InterceptActive = false;
            SafeDestroy(ref s_DownsampledCascade);
            SafeDestroy(ref s_DownsampledObjectsLayer);
            SafeDestroy(ref s_TempSrcSlice);
            SafeDestroy(ref s_TempDstSlice);
            s_CascadeField = null;
            s_ObjectsLayerField = null;
            s_State = AdapterState.Idle;
        }

        #endregion

        #region Per-Frame Operations

        /// <summary>
        /// OnSimulateGPU Prefix: 降采样 + 激活拦截 + 替换全局着色器变量
        /// </summary>
        public static void OnBeforeSimulateGPU()
        {
            s_CallCount++;

            if (s_State == AdapterState.Pending)
                TryActivate();

            if (s_State != AdapterState.Active) return;

            UpdateCascade();
            SwapInGlobals();
            s_InterceptActive = true;
        }

        /// <summary>
        /// OnSimulateGPU Postfix: 恢复全局着色器变量 + 关闭拦截
        /// </summary>
        public static void OnAfterSimulateGPU()
        {
            if (s_State != AdapterState.Active) return;

            s_InterceptActive = false;
            SwapOutGlobals();
        }

        private static void UpdateCascade()
        {
            var originalCascade = s_CascadeField.GetValue<RenderTexture>();
            if (originalCascade == null || !originalCascade.IsCreated()) return;

            // 逐 slice 降采样 cascade (Tex2DArray → 2D → Blit → 2D → Tex2DArray)
            for (int slice = 0; slice < 4; slice++)
            {
                Graphics.CopyTexture(originalCascade, slice, 0, s_TempSrcSlice, 0, 0);
                Graphics.Blit(s_TempSrcSlice, s_TempDstSlice);
                Graphics.CopyTexture(s_TempDstSlice, 0, 0, s_DownsampledCascade, slice, 0);
            }

            // 降采样 ObjectsLayer
            var origObjLayer = s_ObjectsLayerField.GetValue<RenderTexture>();
            if (origObjLayer != null && origObjLayer.IsCreated())
            {
                Graphics.Blit(origObjLayer, s_DownsampledObjectsLayer);
            }
        }

        /// <summary>
        /// 临时替换全局着色器变量为降采样版本。
        /// 计算着色器可能通过 #include 引用 colossal_TerrainTextureArray 等全局纹理。
        /// </summary>
        private static void SwapInGlobals()
        {
            // 保存原始值
            s_SavedGlobalCascadeArray = Shader.GetGlobalTexture(s_ID_CascadeArray);
            s_SavedGlobalCascadeLimit = Shader.GetGlobalVector(s_ID_CascadeLimit);

            // 替换为降采样版本
            Shader.SetGlobalTexture(s_ID_CascadeArray, s_DownsampledCascade);
            Shader.SetGlobalVector(s_ID_CascadeLimit,
                new Vector4(0.5f / s_CascadeSize, 0.5f / s_CascadeSize, 0f, 0f));
        }

        /// <summary>
        /// 恢复原始全局着色器变量。
        /// </summary>
        private static void SwapOutGlobals()
        {
            if (s_SavedGlobalCascadeArray != null)
            {
                Shader.SetGlobalTexture(s_ID_CascadeArray, s_SavedGlobalCascadeArray);
                s_SavedGlobalCascadeArray = null;
            }
            Shader.SetGlobalVector(s_ID_CascadeLimit, s_SavedGlobalCascadeLimit);
        }

        #endregion

        #region Helpers

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
    /// WaterSystem.OnSimulateGPU 前后控制拦截窗口 + 全局变量交换。
    /// GPU 水模拟在此方法中执行（OnUpdate 只调度 CPU SourceJob）。
    /// </summary>
    [HarmonyPatch(typeof(WaterSystem), "OnSimulateGPU")]
    internal static class WaterSystem_OnSimulateGPU_AdapterPatch
    {
        [HarmonyPrefix]
        static void Prefix()
        {
            TerrainWaterAdapter.OnBeforeSimulateGPU();
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            TerrainWaterAdapter.OnAfterSimulateGPU();
        }
    }

    /// <summary>
    /// 拦截 GetCascadeTexture: 在 GPU 水模拟期间返回降采样 4096 版本。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.GetCascadeTexture))]
    internal static class TerrainSystem_GetCascadeTexture_Patch
    {
        [HarmonyPrefix]
        static bool Prefix(ref Texture __result)
        {
            if (TerrainWaterAdapter.IsIntercepting && TerrainWaterAdapter.DownsampledCascade != null)
            {
                __result = TerrainWaterAdapter.DownsampledCascade;
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 拦截 GetObjectsLayerTexture: 同上。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), nameof(TerrainSystem.GetObjectsLayerTexture))]
    internal static class TerrainSystem_GetObjectsLayerTexture_Patch
    {
        [HarmonyPrefix]
        static bool Prefix(ref Texture __result)
        {
            if (TerrainWaterAdapter.IsIntercepting && TerrainWaterAdapter.DownsampledObjectsLayer != null)
            {
                __result = TerrainWaterAdapter.DownsampledObjectsLayer;
                return false;
            }
            return true;
        }
    }
}
