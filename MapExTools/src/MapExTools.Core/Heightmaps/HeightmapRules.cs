namespace MapExTools.Core.Heightmaps;

/// <summary>
/// 图片在游戏里会被读成什么。游戏用原生读图器取通道数与位深（<c>ImageAsset.Load</c>），16 位的图经
/// <c>TextureImporter.Ensure16BitsPerChannel</c>：单通道建成 <c>R16_UNorm</c>，其余一律建成 <c>R16G16B16A16_UNorm</c>。
/// </summary>
public enum HeightmapPixelKind
{
    /// <summary>16 位单通道（灰度）：游戏读成 R16_UNorm，直接作为高度。</summary>
    Gray16,

    /// <summary>16 位四通道（RGBA）：游戏读成 RGBA64，导入时只取 R 通道（<c>TerrainSystem.ToR16</c>）。</summary>
    Rgba16,

    /// <summary>
    /// 16 位双通道（灰度＋透明）或三通道（RGB）：游戏也按 RGBA64 建贴图，每像素只有 4 或 6 字节，与 8 字节对不上，
    /// 读不进来（源码推断，原生读图器的行为看不到）。
    /// </summary>
    Other16,

    /// <summary>8 位及以下（含 JPG）：游戏不认。</summary>
    EightBit,

    /// <summary>浮点、32 位等其他格式。</summary>
    Unsupported,
}

/// <summary>高度图不能导入游戏的原因，可同时有两个。</summary>
[Flags]
public enum HeightmapIssues
{
    None = 0,

    /// <summary>尺寸不是 4096×4096。</summary>
    WrongSize = 1,

    /// <summary>不是 16 位灰度或 16 位 RGBA。</summary>
    WrongFormat = 2,
}

/// <summary>
/// 高度图规则，照游戏的地形导入（计划 4.2）：<c>TerrainSystem.IsValidHeightmapFormat</c>（4096×4096，R16 或 RGBA64）、
/// <c>kDefaultHeightScaleOffset</c>（高度比例 4096 m、偏移 0）、编辑器高度比例输入框的范围（200–10000 m）、
/// <c>ImageAsset.kExtensions</c>（可选的扩展名）。
/// </summary>
public static class HeightmapRules
{
    /// <summary>游戏要求的边长（<c>TerrainSystem.kDefaultHeightmapWidth</c>／<c>Height</c>）。</summary>
    public const int RequiredSize = 4096;

    /// <summary>原版默认高度比例（米），即 16 位最大值对应的海拔。</summary>
    public const double DefaultHeightScale = 4096;

    /// <summary>编辑器里高度比例的下限（<c>TerrainPanelSystem</c> 的输入框）。</summary>
    public const double MinHeightScale = 200;

    /// <summary>编辑器里高度比例的上限。</summary>
    public const double MaxHeightScale = 10000;

    /// <summary>16 位原始值的最大值。</summary>
    public const int MaxRaw = ushort.MaxValue;

    /// <summary>显示内容的像素上限：比 4096×4096 大的图只显示文件头信息，游戏本来也不认。</summary>
    public const long MaxDisplayPixels = (long)RequiredSize * RequiredSize;

    /// <summary>游戏认的图片扩展名（<c>ImageAsset.kExtensions</c>）。JPG 只有 8 位，列出来但一定不符。</summary>
    public static readonly IReadOnlyList<string> Extensions = [".png", ".tif", ".tiff", ".jpg", ".jpeg"];

    /// <summary>按通道数与每通道位数归类。</summary>
    public static HeightmapPixelKind Classify(int channels, int bitsPerChannel, bool isFloat)
    {
        if (isFloat)
            return HeightmapPixelKind.Unsupported;
        if (bitsPerChannel is > 0 and <= 8)
            return HeightmapPixelKind.EightBit;
        if (bitsPerChannel != 16)
            return HeightmapPixelKind.Unsupported;

        return channels switch
        {
            1 => HeightmapPixelKind.Gray16,
            4 => HeightmapPixelKind.Rgba16,
            2 or 3 => HeightmapPixelKind.Other16,
            _ => HeightmapPixelKind.Unsupported,
        };
    }

    /// <summary>能否导入游戏：尺寸须为 4096×4096，格式须为 16 位灰度或 16 位 RGBA。</summary>
    public static HeightmapIssues Validate(int width, int height, HeightmapPixelKind kind)
    {
        HeightmapIssues issues = HeightmapIssues.None;
        if (width != RequiredSize || height != RequiredSize)
            issues |= HeightmapIssues.WrongSize;
        if (kind is not (HeightmapPixelKind.Gray16 or HeightmapPixelKind.Rgba16))
            issues |= HeightmapIssues.WrongFormat;
        return issues;
    }

    /// <summary>高度比例是否在编辑器允许的范围内。</summary>
    public static bool IsValidHeightScale(double heightScale) =>
        double.IsFinite(heightScale) && heightScale >= MinHeightScale && heightScale <= MaxHeightScale;

    /// <summary>原始值换算成海拔（米）：原始值 ÷ 65535 × 高度比例（偏移为 0）。</summary>
    public static double ToElevation(int raw, double heightScale) => raw / (double)MaxRaw * heightScale;

    /// <summary>
    /// 海平面对应的原始值门槛：原始值不小于它的点在海平面或以上（算作陆地）。海平面不高于 0 时为 0（全是陆地），
    /// 高于高度比例时为 65536（全是水）。
    /// </summary>
    public static int SeaLevelThreshold(double seaLevel, double heightScale)
    {
        if (!double.IsFinite(seaLevel) || seaLevel <= 0)
            return 0;
        if (seaLevel > heightScale)
            return MaxRaw + 1;

        int threshold = (int)Math.Clamp(Math.Ceiling(seaLevel / heightScale * MaxRaw), 0, MaxRaw + 1);

        // 浮点换算可能差一：以 ToElevation 的结果为准修正
        while (threshold > 0 && ToElevation(threshold - 1, heightScale) >= seaLevel)
            threshold--;
        while (threshold <= MaxRaw && ToElevation(threshold, heightScale) < seaLevel)
            threshold++;
        return threshold;
    }
}
