// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.

using Colossal.Mathematics;
using Game.Simulation;
using Game.Tools;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// 地形笔刷道路阻挡 Patch。
    /// 目标: TerrainSystem.ApplyBrush (public)
    /// 策略: Heightmap 原生分辨率级别的区域备份 → 让 GPU 笔刷正常执行 → 逐像素还原道路对侧
    /// 完全在 heightmap 分辨率操作，不修改笔刷纹理，不产生方块伪影。
    /// </summary>
    [HarmonyPatch(typeof(TerrainSystem), "ApplyBrush")]
    public static class TerrainBrushBlockPatch
    {
        private const string Tag = "BrushRoadBlock";

        // === Constants and Fields ===

        #region Constants and Fields

        // 缓存的反射字段引用（懒加载）
        private static System.Reflection.FieldInfo s_HeightmapField;

        // Prefix → Postfix 状态传递（主线程顺序执行，无并发）
        private static bool s_Active;
        private static ushort[] s_BackupHeights;
        private static int s_PxX, s_PxY, s_W, s_H;
        private static int s_HmW, s_HmH;
        private static float2 s_PlayableArea, s_PlayableOffset;
        private static float2 s_BrushCenter;
        private static NativeList<Line2> s_RoadSegments;

        #endregion

        // === Harmony Patches ===

        #region Harmony Patches

        /// <summary>
        /// Prefix: 查询道路线段、备份 heightmap 笔刷区域到 CPU
        /// </summary>
        [HarmonyPrefix]
        static void Prefix(
            TerrainSystem __instance,
            Bounds2 area,
            Brush brush)
        {
            s_Active = false;

            // 1. 检查功能开关
            var settings = Mod.Instance?.Settings;
            if (settings == null || !settings.TerrainBrushRoadBlock) return;

            // 2. 查询道路线段
            float margin = settings.TerrainBrushRoadMargin;
            NativeList<Line2> segments;
            try
            {
                segments = RoadSegmentQuery.GetRoadSegmentsInBounds(
                    __instance.EntityManager, area, margin, Allocator.TempJob);
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag, $"道路查询失败: {ex.Message}");
                return;
            }

            if (segments.Length == 0)
            {
                segments.Dispose();
                return;
            }

            // 3. 获取 heightmap（缓存反射）
            RenderTexture heightmap = GetHeightmap(__instance);
            if (heightmap == null)
            {
                segments.Dispose();
                return;
            }

            // 4. 计算笔刷对应的 heightmap 像素矩形
            s_PlayableArea = __instance.playableArea;
            s_PlayableOffset = __instance.playableOffset;
            s_HmW = heightmap.width;
            s_HmH = heightmap.height;

            Bounds2 normArea = new Bounds2(
                (area.min - s_PlayableOffset) / s_PlayableArea,
                (area.max - s_PlayableOffset) / s_PlayableArea);

            s_PxX = (int)math.max(math.floor(normArea.min.x * s_HmW), 0);
            s_PxY = (int)math.max(math.floor(normArea.min.y * s_HmH), 0);
            int pxMaxX = (int)math.min(math.ceil(normArea.max.x * s_HmW), s_HmW - 1);
            int pxMaxY = (int)math.min(math.ceil(normArea.max.y * s_HmH), s_HmH - 1);
            s_W = pxMaxX - s_PxX + 1;
            s_H = pxMaxY - s_PxY + 1;

            if (s_W <= 0 || s_H <= 0)
            {
                segments.Dispose();
                return;
            }

            // 5. 备份 heightmap 区域到 CPU（GPU → 临时 RT → ReadPixels → ushort[]）
            try
            {
                s_BackupHeights = ReadHeightmapRegion(heightmap, s_PxX, s_PxY, s_W, s_H);
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag, $"Heightmap 备份失败: {ex.Message}");
                segments.Dispose();
                return;
            }

            // 6. 保存状态
            s_RoadSegments = segments;
            s_BrushCenter = brush.m_Position.xz;
            s_Active = true;
        }

        /// <summary>
        /// Postfix: 读取修改后的 heightmap，逐像素还原道路对侧
        /// </summary>
        [HarmonyPostfix]
        static void Postfix(TerrainSystem __instance)
        {
            if (!s_Active) return;
            s_Active = false;

            try
            {
                RenderTexture heightmap = GetHeightmap(__instance);
                if (heightmap == null) return;

                // 1. 读取修改后的 heightmap 区域
                ushort[] currentHeights = ReadHeightmapRegion(heightmap, s_PxX, s_PxY, s_W, s_H);

                // 2. 逐像素: 道路对侧 → 用 backup 恢复，道路同侧 → 保留修改
                bool anyRestored = false;
                for (int y = 0; y < s_H; y++)
                {
                    for (int x = 0; x < s_W; x++)
                    {
                        // 像素坐标 → 世界坐标
                        int hmX = s_PxX + x;
                        int hmY = s_PxY + y;
                        float2 worldPos = new float2(
                            ((float)hmX / s_HmW) * s_PlayableArea.x + s_PlayableOffset.x,
                            ((float)hmY / s_HmH) * s_PlayableArea.y + s_PlayableOffset.y);

                        // 奇偶穿越测试: brushCenter → worldPos 穿越道路中心线的次数
                        // 奇数次 = 对侧 → 阻挡; 偶数次 = 同侧 → 允许
                        if (RoadBlockMath.IsBlockedByRoad(
                            s_BrushCenter, worldPos, s_RoadSegments))
                        {
                            int idx = y * s_W + x;
                            currentHeights[idx] = s_BackupHeights[idx];
                            anyRestored = true;
                        }
                    }
                }

                // 3. 仅当有像素被还原时才写回 GPU
                if (anyRestored)
                {
                    WriteHeightmapRegion(heightmap, currentHeights, s_PxX, s_PxY, s_W, s_H);
                }
            }
            catch (System.Exception ex)
            {
                ModLog.Warn(Tag, $"Heightmap 还原失败: {ex.Message}");
            }
            finally
            {
                // 清理
                if (s_RoadSegments.IsCreated)
                    s_RoadSegments.Dispose();
                s_BackupHeights = null;
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 通过反射获取 TerrainSystem.m_Heightmap（缓存 FieldInfo）
        /// </summary>
        private static RenderTexture GetHeightmap(TerrainSystem instance)
        {
            if (s_HeightmapField == null)
            {
                s_HeightmapField = AccessTools.Field(typeof(TerrainSystem), "m_Heightmap");
                if (s_HeightmapField == null)
                {
                    ModLog.Error(Tag, "无法获取 TerrainSystem.m_Heightmap 字段");
                    return null;
                }
            }

            var rt = s_HeightmapField.GetValue(instance) as RenderTexture;
            return (rt != null && rt.IsCreated()) ? rt : null;
        }

        /// <summary>
        /// 从 heightmap RenderTexture 的指定区域读取像素数据到 CPU。
        /// 格式: R16_UNorm → ushort[]
        /// </summary>
        private static ushort[] ReadHeightmapRegion(
            RenderTexture heightmap, int px, int py, int w, int h)
        {
            // GPU → 临时 RT（区域拷贝）
            var tempRT = RenderTexture.GetTemporary(w, h, 0, heightmap.graphicsFormat);
            tempRT.filterMode = FilterMode.Point;
            Graphics.CopyTexture(heightmap, 0, 0, px, py, w, h, tempRT, 0, 0, 0, 0);

            // 临时 RT → Texture2D（ReadPixels）
            var prevActive = RenderTexture.active;
            RenderTexture.active = tempRT;

            var readTex = new Texture2D(w, h, TextureFormat.R16, false);
            readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readTex.Apply(false);

            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(tempRT);

            // NativeArray<ushort> → managed ushort[]
            var raw = readTex.GetRawTextureData<ushort>();
            var result = new ushort[raw.Length];
            Unity.Collections.NativeArray<ushort>.Copy(raw, result);

            Object.Destroy(readTex);
            return result;
        }

        /// <summary>
        /// 将 CPU 像素数据写回 heightmap RenderTexture 的指定区域。
        /// </summary>
        private static void WriteHeightmapRegion(
            RenderTexture heightmap, ushort[] data, int px, int py, int w, int h)
        {
            var writeTex = new Texture2D(w, h, TextureFormat.R16, false);
            var raw = writeTex.GetRawTextureData<ushort>();
            Unity.Collections.NativeArray<ushort>.Copy(data, raw);
            writeTex.Apply(false);

            Graphics.CopyTexture(writeTex, 0, 0, 0, 0, w, h,
                                 heightmap, 0, 0, px, py);

            Object.Destroy(writeTex);
        }

        #endregion
    }
}
