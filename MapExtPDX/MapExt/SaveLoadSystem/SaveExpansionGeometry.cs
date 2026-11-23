// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

namespace MapExtPDX.SaveLoadSystem
{
    /// <summary>
    /// 28 km → 57 km 存档扩展的纯几何换算库。
    ///
    /// <para>只依赖标准 <c>System</c> 基础类型（刻意不使用 <c>Unity.Mathematics</c>），
    /// 以便生产代码与独立几何校验脚本通过 <c>Add-Type</c> 共用同一份实现，避免测试与生产各算一套。</para>
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

        /// <summary>
        /// 世界坐标（单轴）换算成一张贴图上的像素坐标（连续值，像素左缘为整数）。
        /// 贴图覆盖 <c>[corner, corner + size)</c>，宽 <paramref name="width"/> 像素。
        /// 用于按背景的实际世界范围裁剪：把目标地图的边界换算到源背景上，得到采样窗口。
        /// 例如标准 28 km 背景（世界 114,688 m，左下角 -57,344）上，57 km 地图的边界 ±28,672 m 得 1024 与 3072。
        /// </summary>
        public static double WorldToPixel(double world, double corner, double size, int width)
        {
            return (world - corner) / size * width;
        }

        /// <summary>
        /// 海道底高（28→57 没有背景地形时）：对原边界一条边的高度剖面沿边做宽
        /// <c>2 × halfWidth + 1</c> 的最小值滤波，滤波后低于 <paramref name="seaLevel"/> 的位置返回该值（海道底），
        /// 其余返回 -1（不开道）。最小值滤波把一两格宽的海口加宽，免得被比地形粗一倍的水面网格平均成陆地。
        /// 例如剖面 <c>[120, 90, 120]</c>、海平面 100、半宽 1 得 <c>[90, 90, 90]</c>。
        /// </summary>
        public static int[] SeaChannelDepths(ushort[] profile, int seaLevel, int halfWidth)
        {
            var depths = new int[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                int lo = System.Math.Max(0, i - halfWidth);
                int hi = System.Math.Min(profile.Length - 1, i + halfWidth);
                int min = int.MaxValue;
                for (int j = lo; j <= hi; j++)
                {
                    if (profile[j] < min) min = profile[j];
                }

                depths[i] = min < seaLevel ? min : -1;
            }

            return depths;
        }
    }
}
