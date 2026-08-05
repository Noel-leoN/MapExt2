// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using Colossal.AssetPipeline.Native;
using Colossal.IO.AssetDatabase;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// 高度圖匯出方向。
    /// 遊戲內部高度圖的行序與 PNG 檔案格式的行序約定相反
    /// （資料 row 0 = 世界 -Z；PNG row 0 = 圖像頂部），
    /// 故提供翻轉選項以配合不同用途。實際所需方向以遊戲內實測為準。
    /// </summary>
    public enum HeightmapExportOrientation
    {
        /// <summary>原生行序，不做任何變換。</summary>
        Native,

        /// <summary>垂直翻轉（南北對調）。</summary>
        FlipVertical,

        /// <summary>水平翻轉（東西對調）。</summary>
        FlipHorizontal,

        /// <summary>旋轉 180 度（等同垂直＋水平同時翻轉）。</summary>
        Rotate180,

        /// <summary>四種方向各輸出一份，用於一次性比對定位正確方向。</summary>
        All,
    }

    /// <summary>
    /// 地形高度圖匯出工具。
    ///
    /// <para><b>資料來源</b>：讀取 <c>TerrainSystem.m_Heightmap</c>（R16_UNorm RenderTexture），
    /// 與地圖編輯器「匯出高度圖」同源，內容為<b>純地形</b>。
    /// 這與遊戲 Dev Debug 選單的 <c>ExportHeightMap</c> 不同——後者讀 <c>m_CPUHeights</c>
    /// （即 <c>m_HeightmapCascade</c> 的 GPU readback），其中已烘焙建築／道路／地塊的壓平結果，
    /// 若拿去重新匯入會把那些壓平痕跡永久固化進地形。</para>
    ///
    /// <para><b>輸出格式</b>：16-bit 單通道 PNG（編輯器可直接匯入）＋ 可選的原始 RAW。
    /// 編輯器僅接受 <c>ImageAsset.kExtensions</c>（.png/.tif/.tiff/.jpg/.jpeg），不支援 .raw，
    /// 故 PNG 是唯一能回流編輯器的格式。</para>
    /// </summary>
    public static class HeightmapExporter
    {
        private const string Tag = "HeightmapExport";

        // === Constants and Fields ===

        #region Constants and Fields

        /// <summary>編輯器高度圖資料夾名稱，與 <c>TerrainPanelSystem.kHeightmapFolder</c> 一致。</summary>
        private const string kHeightmapFolder = "Heightmaps";

        /// <summary>PNG 無損壓縮等級，與原版編輯器匯出的預設值一致。</summary>
        private const int kPngCompressionLevel = 4;

        /// <summary>快取的 <c>TerrainSystem.m_Heightmap</c> 反射欄位。</summary>
        private static System.Reflection.FieldInfo s_HeightmapField;

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// 匯出當前地形高度圖。
        /// </summary>
        /// <param name="orientation">輸出方向。</param>
        /// <param name="alsoExportRaw">是否額外輸出一份原始 RAW（無檔頭 16-bit little-endian）。</param>
        /// <returns>供設定頁顯示的單行結果摘要。</returns>
        public static string Export(HeightmapExportOrientation orientation, bool alsoExportRaw)
        {
            try
            {
                var terrainSystem = World.DefaultGameObjectInjectionWorld
                    ?.GetExistingSystemManaged<TerrainSystem>();
                if (terrainSystem == null)
                {
                    ModLog.Warn(Tag, "TerrainSystem 不存在，請先載入地圖或存檔。");
                    return "TerrainSystem not found — load a map or save first.";
                }

                RenderTexture heightmap = GetHeightmap(terrainSystem);
                if (heightmap == null)
                {
                    ModLog.Warn(Tag, "m_Heightmap 尚未建立，請先載入地圖或存檔。");
                    return "Heightmap not ready — load a map or save first.";
                }

                int width = heightmap.width;
                int height = heightmap.height;

                // GPU → CPU：R16_UNorm 逐 texel 對應 ushort
                var heights = new NativeArray<ushort>(width * height, Allocator.Persistent);
                try
                {
                    var request = AsyncGPUReadback.RequestIntoNativeArray(ref heights, heightmap);
                    request.WaitForCompletion();
                    if (request.hasError)
                    {
                        ModLog.Error(Tag, "AsyncGPUReadback 失敗，無法讀取高度圖。");
                        return "GPU readback failed.";
                    }

                    return WriteAllOutputs(heights, width, height, orientation, alsoExportRaw);
                }
                finally
                {
                    heights.Dispose();
                }
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "高度圖匯出失敗。");
                return $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// 取得匯出目標資料夾（<c>{UserData}/Heightmaps</c>），即編輯器「匯入高度圖」讀取的位置。
        /// </summary>
        public static string GetExportDirectory()
        {
            return Path.Combine(Colossal.PSI.Environment.EnvPath.kUserDataPath, kHeightmapFolder);
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>
        /// 依方向設定寫出所有檔案，回傳結果摘要。
        /// </summary>
        private static string WriteAllOutputs(
            NativeArray<ushort> heights, int width, int height,
            HeightmapExportOrientation orientation, bool alsoExportRaw)
        {
            string dir = GetExportDirectory();
            Directory.CreateDirectory(dir);

            // 時間戳避免覆蓋歷史匯出
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = $"TerrainExport_{stamp}";

            int written = 0;

            if (orientation == HeightmapExportOrientation.All)
            {
                // 一次輸出四份，檔名帶方向後綴供比對
                written += WriteOriented(heights, width, height,
                    HeightmapExportOrientation.Native, Path.Combine(dir, $"{baseName}_native.png")) ? 1 : 0;
                written += WriteOriented(heights, width, height,
                    HeightmapExportOrientation.FlipVertical, Path.Combine(dir, $"{baseName}_flipV.png")) ? 1 : 0;
                written += WriteOriented(heights, width, height,
                    HeightmapExportOrientation.FlipHorizontal, Path.Combine(dir, $"{baseName}_flipH.png")) ? 1 : 0;
                written += WriteOriented(heights, width, height,
                    HeightmapExportOrientation.Rotate180, Path.Combine(dir, $"{baseName}_rot180.png")) ? 1 : 0;
            }
            else
            {
                written += WriteOriented(heights, width, height,
                    orientation, Path.Combine(dir, $"{baseName}.png")) ? 1 : 0;
            }

            // RAW 一律輸出原生行序：它面向外部程式化處理，不做方向假設
            if (alsoExportRaw)
            {
                string rawPath = Path.Combine(dir, $"{baseName}.raw");
                var rawBytes = new NativeSlice<ushort>(heights).SliceConvert<byte>();
                var managed = new byte[rawBytes.Length];
                rawBytes.CopyTo(managed);
                File.WriteAllBytes(rawPath, managed);
                written++;
            }

            if (written == 0)
            {
                return "Export produced no files — see log.";
            }

            // 讓編輯器「從磁碟選擇」下次直接定位到此目錄
            try
            {
                Game.Settings.SharedSettings.instance.editor.lastHeightMapDirectory = dir;
            }
            catch (Exception ex)
            {
                // 非致命：檔案已寫出，只是選擇器預設路徑沒更新
                ModLog.Warn(Tag, $"更新 lastHeightMapDirectory 失敗（不影響匯出）: {ex.Message}");
            }

            ModLog.Ok(Tag, $"已匯出 {written} 個檔案至 {dir}（{width}×{height}, 16-bit）");
            return $"Exported {written} file(s) — {width}x{height} 16-bit → {dir}";
        }

        /// <summary>
        /// 依指定方向變換後寫出單一 PNG。
        /// </summary>
        private static bool WriteOriented(
            NativeArray<ushort> src, int width, int height,
            HeightmapExportOrientation orientation, string path)
        {
            if (orientation == HeightmapExportOrientation.Native)
            {
                return WritePng(src, width, height, path);
            }

            var transformed = new NativeArray<ushort>(src.Length, Allocator.Temp);
            try
            {
                Transform(src, transformed, width, height, orientation);
                return WritePng(transformed, width, height, path);
            }
            finally
            {
                transformed.Dispose();
            }
        }

        /// <summary>
        /// 依方向對高度資料做行／列重排。
        /// </summary>
        private static void Transform(
            NativeArray<ushort> src, NativeArray<ushort> dst,
            int width, int height, HeightmapExportOrientation orientation)
        {
            bool flipY = orientation == HeightmapExportOrientation.FlipVertical
                      || orientation == HeightmapExportOrientation.Rotate180;
            bool flipX = orientation == HeightmapExportOrientation.FlipHorizontal
                      || orientation == HeightmapExportOrientation.Rotate180;

            // 純垂直翻轉可整行搬移，避開逐像素迴圈
            if (flipY && !flipX)
            {
                for (int y = 0; y < height; y++)
                {
                    NativeArray<ushort>.Copy(src, y * width, dst, (height - 1 - y) * width, width);
                }
                return;
            }

            for (int y = 0; y < height; y++)
            {
                int srcRow = y * width;
                int dstRow = (flipY ? (height - 1 - y) : y) * width;
                for (int x = 0; x < width; x++)
                {
                    dst[dstRow + (flipX ? (width - 1 - x) : x)] = src[srcRow + x];
                }
            }
        }

        /// <summary>
        /// 以 16-bit 單通道 PNG 寫出高度資料。
        /// </summary>
        private static unsafe bool WritePng(
            NativeArray<ushort> data, int width, int height, string path)
        {
            byte[] png = TextureUtilities.SaveImage(
                pixelDataPtr: (IntPtr)data.GetUnsafeReadOnlyPtr(),
                pixelDataSize: data.Length * sizeof(ushort),
                width: width,
                height: height,
                channels: 1,
                bitsPerChannel: 16,
                format: NativeTextures.ImageFileFormat.PNG,
                compressionLevel: kPngCompressionLevel);

            if (png == null)
            {
                ModLog.Error(Tag, $"PNG 編碼失敗: {Path.GetFileName(path)}");
                return false;
            }

            File.WriteAllBytes(path, png);
            return true;
        }

        /// <summary>
        /// 透過反射取得 <c>TerrainSystem.m_Heightmap</c>（快取 FieldInfo）。
        /// </summary>
        private static RenderTexture GetHeightmap(TerrainSystem instance)
        {
            if (s_HeightmapField == null)
            {
                s_HeightmapField = AccessTools.Field(typeof(TerrainSystem), "m_Heightmap");
                if (s_HeightmapField == null)
                {
                    ModLog.Error(Tag, "無法取得 TerrainSystem.m_Heightmap 欄位");
                    return null;
                }
            }

            var rt = s_HeightmapField.GetValue(instance) as RenderTexture;
            return (rt != null && rt.IsCreated()) ? rt : null;
        }

        #endregion
    }
}
