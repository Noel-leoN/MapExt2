// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 28 km → 57 km 存档扩展的纯几何换算库。
    ///
    /// <para>只依赖标准 <c>System</c> 基础类型（刻意不使用 <c>Unity.Mathematics</c>），
    /// 以便生产代码与 <c>.agents/scripts/Test-SaveExpansion.ps1</c> 的
    /// <c>Add-Type</c> 校验脚本共用同一份实现——避免夹具与生产各算一套。</para>
    ///
    /// <para>坐标约定与 <c>XCellMapSystemRe</c> 一致：地图以原点为中心，
    /// 第 <c>i</c> 个 cell 的中心世界坐标为
    /// <c>-0.5 * mapSize + (i + 0.5) * (mapSize / gridSize)</c>。</para>
    /// </summary>
    public static class SaveExpansionGeometry
    {
        /// <summary>单个 cell 的边长（世界单位）。</summary>
        public static double CellSize(double mapSize, int gridSize)
        {
            return mapSize / gridSize;
        }

        /// <summary>第 <paramref name="index"/> 个 cell 的中心世界坐标（单轴）。</summary>
        public static double CellCenterWorld(int index, int gridSize, double mapSize)
        {
            return -0.5 * mapSize + (index + 0.5) * CellSize(mapSize, gridSize);
        }

        /// <summary>
        /// 等格距嵌入的起始下标：源与目标 cell 边长相同时，源网格居中放入目标网格。
        /// 例如 512²→1024² 得 256，256²→512² 得 128。
        /// </summary>
        public static int EmbedOffset(int targetGridSize, int sourceGridSize)
        {
            return (targetGridSize - sourceGridSize) / 2;
        }

        /// <summary>
        /// 降采样嵌入的目标宽度：<c>targetWidth * sourceMapSize / targetMapSize</c>。
        /// 例如地形 detail 4096²、源 28,672 m、目标 57,344 m 得 2048。
        /// </summary>
        public static int EmbedSize(int targetWidth, double sourceMapSize, double targetMapSize)
        {
            return (int)System.Math.Round(targetWidth * sourceMapSize / targetMapSize);
        }

        /// <summary>降采样嵌入的起始下标（居中）。</summary>
        public static int EmbedStart(int targetWidth, int embedSize)
        {
            return (targetWidth - embedSize) / 2;
        }

        /// <summary>
        /// 源 cell 与目标 cell 的世界坐标是否等价（单轴，含容差）。
        /// 用于验证「目标 cell (x + offset) 的世界坐标 == 源 cell x 的世界坐标」。
        /// </summary>
        public static bool AreWorldEquivalent(
            int sourceIndex, int targetIndex,
            int sourceGridSize, int targetGridSize,
            double sourceMapSize, double targetMapSize,
            double tolerance)
        {
            double source = CellCenterWorld(sourceIndex, sourceGridSize, sourceMapSize);
            double target = CellCenterWorld(targetIndex, targetGridSize, targetMapSize);
            return System.Math.Abs(source - target) <= tolerance;
        }

        /// <summary>
        /// 把降采样嵌入窗口 <c>[embedStart, embedStart + embedSize)</c> 换算回源网格的采样区间
        /// <c>[srcStart, srcEnd)</c>（以源像素为单位），供 Box Filter 换算裁剪范围。
        /// 例如 4096² 源背景裁剪中心 2048² → <c>[1024, 3072)</c>。
        /// </summary>
        public static void EmbedToSourceRange(
            int embedStart, int embedSize, int targetWidth, int sourceWidth,
            out double srcStart, out double srcEnd)
        {
            double ratio = (double)sourceWidth / targetWidth;
            srcStart = embedStart * ratio;
            srcEnd = (embedStart + embedSize) * ratio;
        }
    }
}
