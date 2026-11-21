using System.Globalization;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;

namespace MapExTools.Core.Heightmaps;

/// <summary>高度图页的文字（计划 4.2），按界面语言生成；数字一律按固定格式写，不随系统区域变化。</summary>
public static class HeightmapText
{
    /// <summary>像素格式：16 位灰度、16 位 RGBA，其余写位数、样本类型与通道数。</summary>
    public static string Kind(ImageFileHeader header, string language)
    {
        ArgumentNullException.ThrowIfNull(header);
        return header.Kind switch
        {
            HeightmapPixelKind.Gray16 => Strings.Get(language, "Heightmaps.Kind.Gray16"),
            HeightmapPixelKind.Rgba16 => Strings.Get(language, "Heightmaps.Kind.Rgba16"),
            _ when header.IsFloat => Format(language, "Heightmaps.Kind.Float", header.BitsPerChannel, header.Channels),
            _ when header.IsSigned => Format(language, "Heightmaps.Kind.Signed", header.BitsPerChannel, header.Channels),
            _ => Format(language, "Heightmaps.Kind.Bits", header.BitsPerChannel, header.Channels),
        };
    }

    /// <summary>尺寸与格式，如 <c>4096×4096 · 16 位灰度</c>。</summary>
    public static string SizeAndKind(ImageFileHeader header, string language)
    {
        ArgumentNullException.ThrowIfNull(header);
        return Format(language, "Heightmaps.Size", header.Width, header.Height, Kind(header, language));
    }

    /// <summary>能否导入游戏；<paramref name="issues"/> 为 null 表示认不出文件头。<paramref name="brief"/> 为 true 时给列表用的短写法。</summary>
    public static string Issues(HeightmapIssues? issues, string language, bool brief)
    {
        if (issues is not { } value)
            return Strings.Get(language, "Heightmaps.Unrecognized");
        if (value == HeightmapIssues.None)
            return Strings.Get(language, brief ? "Heightmaps.OkShort" : "Heightmaps.Ok");
        if (value == HeightmapIssues.ResampledSize)
            return Strings.Get(language, brief ? "Heightmaps.ResampledOkShort" : "Heightmaps.ResampledOk");

        var parts = new List<string>();
        string suffix = brief ? "Short" : string.Empty;
        if (value.HasFlag(HeightmapIssues.WrongSize))
            parts.Add(Strings.Get(language, "Heightmaps.WrongSize" + suffix));
        else if (value.HasFlag(HeightmapIssues.ResampledSize))
            parts.Add(Strings.Get(language, brief ? "Heightmaps.ResampledOkShort" : "Heightmaps.ResampledNote"));
        if (value.HasFlag(HeightmapIssues.WrongFormat))
            parts.Add(Strings.Get(language, "Heightmaps.WrongFormat" + suffix));

        string sep = brief ? " · " : language == UiLanguage.English ? "; " : "；";
        return string.Join(sep, parts);
    }

    /// <summary>原生可导入为 Good，自动重采样可导入为 Accent，不可导入为 Error，认不出为 Warning。</summary>
    public static PackageTone IssuesTone(HeightmapIssues? issues) => issues switch
    {
        null => PackageTone.Warning,
        HeightmapIssues.None => PackageTone.Good,
        HeightmapIssues.ResampledSize => PackageTone.Accent,
        _ => PackageTone.Error,
    };

    /// <summary>原始值与海拔的范围。</summary>
    public static IReadOnlyList<string> Stats(HeightmapStats stats, double heightScale, string language)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return
        [
            Format(language, "Heightmaps.RawRange", stats.Min, stats.Max),
            Format(language, "Heightmaps.ElevationRange", stats.MinElevation(heightScale), stats.MaxElevation(heightScale), stats.MeanElevation(heightScale)),
        ];
    }

    /// <summary>鼠标所在像素的读数；给了海平面时附上与海平面的高差。</summary>
    public static string Hover(int x, int y, int raw, double heightScale, double? seaLevel, string language)
    {
        double elevation = HeightmapRules.ToElevation(raw, heightScale);
        string text = Format(language, "Heightmaps.Hover", x, y, raw, elevation);
        if (seaLevel is not double sea || sea == 0)
            return text;

        double delta = elevation - sea;
        return text + Format(language, delta >= 0 ? "Heightmaps.AboveSea" : "Heightmaps.BelowSea", Math.Abs(delta));
    }

    /// <summary>图例一项的文字。</summary>
    public static string Legend(LegendEntry entry, string language)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Kind switch
        {
            LegendKind.Black => Format(language, "Heightmaps.Legend.Black", entry.From),
            LegendKind.White => Format(language, "Heightmaps.Legend.White", entry.From),
            LegendKind.BlackAtMost => Format(language, "Heightmaps.Legend.BlackAtMost", entry.From),
            LegendKind.WhiteAtLeast => Format(language, "Heightmaps.Legend.WhiteAtLeast", entry.From),
            LegendKind.Coast => Format(language, "Heightmaps.Legend.Coast", entry.From),
            // 区间统一从低写到高
            _ => Format(language, "Heightmaps.Legend.Band", Math.Min(entry.From, entry.To), Math.Max(entry.From, entry.To)),
        };
    }

    /// <summary>状态栏：共几张，可导入几张（含原生 4096² 与 MapExt 自动重采样 1..8192）。认不出文件头的算不可导入。</summary>
    public static string Status(IReadOnlyCollection<HeightmapFileInfo> files, string language)
    {
        ArgumentNullException.ThrowIfNull(files);
        int ok = files.Count(f => HeightmapRules.CanImport(f.Issues));
        return Format(language, "Heightmaps.Status", files.Count, ok, files.Count - ok);
    }

    private static string Format(string language, string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
