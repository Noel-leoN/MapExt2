namespace MapExTools.Core.Heightmaps;

/// <summary>高度图的三种视图（计划 4.2）。</summary>
public enum HeightmapView
{
    /// <summary>原始灰度：原始值 0–65535 线性对应黑到白。</summary>
    Raw,

    /// <summary>自动拉伸：这张图 0.5% 到 99.5% 分位的原始值线性对应黑到白，两端以外的裁成纯黑、纯白。</summary>
    Stretched,

    /// <summary>按海拔分层着色：海平面以下按深度分 6 级蓝，以上按海拔分 6 级红，海岸线描黑。</summary>
    Layered,
}

/// <summary>图例里一项的种类。</summary>
public enum LegendKind
{
    /// <summary>原始灰度视图的黑端（0 m）。</summary>
    Black,

    /// <summary>原始灰度视图的白端（高度比例）。</summary>
    White,

    /// <summary>拉伸视图的黑端：这个海拔及以下都是纯黑。</summary>
    BlackAtMost,

    /// <summary>拉伸视图的白端：这个海拔及以上都是纯白。</summary>
    WhiteAtLeast,

    /// <summary>陆地的一级（海拔区间）。</summary>
    Land,

    /// <summary>海岸线（海平面）。</summary>
    Coast,

    /// <summary>水下的一级（海拔区间）。</summary>
    Water,
}

/// <summary>图例的一项。<paramref name="Color"/> 为 0xAARRGGBB；<paramref name="From"/>、<paramref name="To"/> 为海拔（米），单点时相等。</summary>
public sealed record LegendEntry(LegendKind Kind, uint Color, double From, double To);

/// <summary>分层着色的区间：门槛与陆地、水下各自分级的海拔范围。</summary>
/// <param name="SeaThreshold">海平面对应的原始值门槛（<see cref="HeightmapRules.SeaLevelThreshold"/>）：不小于它的是陆地。</param>
/// <param name="LandFrom">陆地分级的下端：海平面与最低点里较高的那个。</param>
/// <param name="WaterFrom">水下分级的上端（最浅）：海平面与最高点里较低的那个。</param>
public sealed record LayeredScale(
    double SeaLevel, int SeaThreshold, bool HasLand, bool HasWater, double LandFrom, double LandTo, double WaterFrom, double WaterTo);

/// <summary>
/// 把 16 位原始值画成 32 位颜色（计划 4.2）。先按视图建 65536 项的查找表，再逐像素查表，按行并行。
///
/// <para><b>分层着色的颜色</b>按 dataviz 规范的发散配对（蓝 ↔ 红，中点在海平面）：水下取规范调色板顺序蓝的 200–700 六级；
/// 陆地按这六级的 OKLCH 亮度、以规范红色 <c>#e34948</c> 的色相推算（第 400 级推算出 <c>#e24a48</c>，与锚点各通道最多差 1）。
/// 两侧亮度单调、相邻亮度差至少 0.094、单一色相，用规范的校验脚本 <c>--ordinal</c> 核对过；最浅一级贴近背景的对比度检查
/// 只适用于离散图形，铺满的栅格按规范的顺序色阶处理，可以接近背景色。图像本身铺满画面，不随浅色／深色主题换色。</para>
/// </summary>
public static class HeightmapRenderer
{
    /// <summary>每侧的级数。</summary>
    public const int BandCount = 6;

    /// <summary>海岸线：规范的主墨色。</summary>
    public const uint CoastColor = 0xFF0B0B0B;

    /// <summary>水下六级，由浅到深（规范顺序蓝 200、300、400、500、600、700）。</summary>
    public static readonly IReadOnlyList<uint> WaterBands = [0xFF9EC5F4, 0xFF6DA7EC, 0xFF3987E5, 0xFF256ABF, 0xFF184F95, 0xFF0D366B];

    /// <summary>陆地六级，由低到高（与水下同级同亮度的红）。</summary>
    public static readonly IReadOnlyList<uint> LandBands = [0xFFF8AAA3, 0xFFEE7E77, 0xFFE24A48, 0xFFBB3032, 0xFF911E22, 0xFF681014];

    /// <summary>
    /// 灰度视图的查找表：原始值 → 亮度。原始视图按 0–65535，拉伸视图按这张图的 0.5%–99.5% 分位（<see cref="HeightmapStats.StretchLow"/>）；
    /// 全图同值时为中灰。
    /// </summary>
    public static byte[] BuildGrayLut(HeightmapView view, HeightmapStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        byte[] lut = new byte[HeightmapRules.MaxRaw + 1];
        int low = view == HeightmapView.Stretched ? stats.StretchLow : 0;
        int high = view == HeightmapView.Stretched ? stats.StretchHigh : HeightmapRules.MaxRaw;
        int span = high - low;
        for (int raw = 0; raw < lut.Length; raw++)
        {
            lut[raw] = span <= 0 ? (byte)128
                : raw <= low ? (byte)0
                : raw >= high ? (byte)255
                : (byte)(((long)(raw - low) * 255 + span / 2) / span);
        }

        return lut;
    }

    /// <summary>
    /// 分层着色的区间：陆地从"海平面与最低点里较高者"到最高点分 6 级，水下从"海平面与最高点里较低者"到最低点分 6 级，
    /// 各级都落在图里实际有的高度上。
    /// </summary>
    public static LayeredScale CreateLayeredScale(HeightmapStats stats, double heightScale, double seaLevel)
    {
        ArgumentNullException.ThrowIfNull(stats);

        int threshold = HeightmapRules.SeaLevelThreshold(seaLevel, heightScale);
        double min = stats.MinElevation(heightScale);
        double max = stats.MaxElevation(heightScale);
        double sea = double.IsFinite(seaLevel) ? seaLevel : 0;
        return new LayeredScale(
            sea,
            threshold,
            HasLand: stats.Max >= threshold,
            HasWater: stats.Min < threshold,
            LandFrom: Math.Max(sea, min),
            LandTo: max,
            WaterFrom: Math.Min(sea, max),
            WaterTo: min);
    }

    /// <summary>分层着色的查找表：原始值 → 0xAARRGGBB。</summary>
    public static uint[] BuildLayeredLut(LayeredScale scale, double heightScale)
    {
        ArgumentNullException.ThrowIfNull(scale);

        uint[] lut = new uint[HeightmapRules.MaxRaw + 1];
        for (int raw = 0; raw < lut.Length; raw++)
        {
            double elevation = HeightmapRules.ToElevation(raw, heightScale);
            lut[raw] = raw >= scale.SeaThreshold
                ? LandBands[Band(elevation - scale.LandFrom, scale.LandTo - scale.LandFrom)]
                : WaterBands[Band(scale.WaterFrom - elevation, scale.WaterFrom - scale.WaterTo)];
        }

        return lut;
    }

    /// <summary>图例：灰度视图为黑、白两端对应的海拔；分层视图为陆地由高到低、海岸线、水下由浅到深。</summary>
    public static IReadOnlyList<LegendEntry> BuildLegend(HeightmapView view, HeightmapStats stats, double heightScale, LayeredScale? scale)
    {
        ArgumentNullException.ThrowIfNull(stats);

        if (view == HeightmapView.Stretched)
        {
            double black = HeightmapRules.ToElevation(stats.StretchLow, heightScale);
            double white = HeightmapRules.ToElevation(stats.StretchHigh, heightScale);
            return
            [
                new LegendEntry(LegendKind.WhiteAtLeast, 0xFFFFFFFF, white, white),
                new LegendEntry(LegendKind.BlackAtMost, 0xFF000000, black, black),
            ];
        }

        if (view != HeightmapView.Layered || scale == null)
        {
            return
            [
                new LegendEntry(LegendKind.White, 0xFFFFFFFF, heightScale, heightScale),
                new LegendEntry(LegendKind.Black, 0xFF000000, 0, 0),
            ];
        }

        var entries = new List<LegendEntry>();
        if (scale.HasLand)
        {
            double step = (scale.LandTo - scale.LandFrom) / BandCount;
            for (int band = BandCount - 1; band >= 0; band--)
                entries.Add(new LegendEntry(LegendKind.Land, LandBands[band], scale.LandFrom + band * step, scale.LandFrom + (band + 1) * step));
        }

        if (scale.HasLand && scale.HasWater)
            entries.Add(new LegendEntry(LegendKind.Coast, CoastColor, scale.SeaLevel, scale.SeaLevel));

        if (scale.HasWater)
        {
            double step = (scale.WaterFrom - scale.WaterTo) / BandCount;
            for (int band = 0; band < BandCount; band++)
                entries.Add(new LegendEntry(LegendKind.Water, WaterBands[band], scale.WaterFrom - band * step, scale.WaterFrom - (band + 1) * step));
        }

        return entries;
    }

    /// <summary>灰度查找表转成 32 位颜色（R=G=B），三种视图都画进同一张 Bgr32 位图，界面只留一个位图缓冲。</summary>
    public static uint[] ToColorLut(byte[] grayLut)
    {
        ArgumentNullException.ThrowIfNull(grayLut);
        return Array.ConvertAll(grayLut, g => 0xFF000000u | ((uint)g << 16) | ((uint)g << 8) | g);
    }

    /// <summary>
    /// 按查找表画成 32 位彩色（Bgr32／Bgra32 的内存布局）。<paramref name="seaThreshold"/> 在 1–65535 之间时再把海岸线描黑：
    /// 陆地像素（原始值不小于门槛）的上下左右有水，就是海岸线；灰度视图传 0，不描。
    /// </summary>
    public static void Render(ushort[] samples, int width, int height, uint[] lut, int seaThreshold, uint[] destination)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(lut);
        ArgumentNullException.ThrowIfNull(destination);
        if ((long)width * height != samples.Length || destination.Length < samples.Length)
            throw new ArgumentException("尺寸与数据长度不符", nameof(samples));

        Parallel.For(0, height, y => RenderRow(samples, width, height, y, lut, seaThreshold, destination.AsSpan(y * width, width)));
    }

    /// <summary>
    /// 画第 <paramref name="y"/> 行，规则同 <see cref="Render"/>。界面按行直接写进位图的后台缓冲，不必先画进一整张中间缓冲。
    /// </summary>
    public static void RenderRow(ushort[] samples, int width, int height, int y, uint[] lut, int seaThreshold, Span<uint> destination)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(lut);
        if ((long)width * height != samples.Length)
            throw new ArgumentException("尺寸与数据长度不符", nameof(samples));
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, width, nameof(destination));

        bool drawCoast = seaThreshold is > 0 and <= HeightmapRules.MaxRaw;
        int row = y * width;
        for (int x = 0; x < width; x++)
        {
            int i = row + x;
            int raw = samples[i];
            uint color = lut[raw];
            if (drawCoast && raw >= seaThreshold
                && ((x > 0 && samples[i - 1] < seaThreshold)
                    || (x < width - 1 && samples[i + 1] < seaThreshold)
                    || (y > 0 && samples[i - width] < seaThreshold)
                    || (y < height - 1 && samples[i + width] < seaThreshold)))
            {
                color = CoastColor;
            }

            destination[x] = color;
        }
    }

    /// <summary>区间内的位置换算成级别 0–5；区间为 0 时都算第 0 级。</summary>
    private static int Band(double offset, double span)
    {
        if (!(span > 0))
            return 0;
        return Math.Clamp((int)Math.Floor(offset / span * BandCount), 0, BandCount - 1);
    }
}
