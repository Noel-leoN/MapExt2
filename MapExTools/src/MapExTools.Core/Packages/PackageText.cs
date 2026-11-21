using System.Globalization;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Core.Packages;

/// <summary>界面上的醒目程度，决定文字颜色。</summary>
public enum PackageTone
{
    Normal,

    /// <summary>匹配。</summary>
    Good,

    /// <summary>可转换。</summary>
    Accent,

    /// <summary>次要信息（未知、没有预览图）。</summary>
    Muted,

    /// <summary>要留意：开启转换后可转换、会拉伸、不校验、guid 重复。</summary>
    Warning,

    /// <summary>不符、损坏。</summary>
    Error,
}

/// <summary>详情栏的一行：标签与取值。</summary>
public sealed record DetailLine(string Label, string Value, PackageTone Tone = PackageTone.Normal);

/// <summary>
/// 存档与地图页的文字（计划 4.1），按界面语言生成。判定都来自 <see cref="SaveGateRules"/>，这里只负责措辞；
/// 数字与日期一律按固定格式写，不随系统区域变化。
/// </summary>
public static class PackageText
{
    /// <summary>模式名加边长，如 <c>ModeA 57km</c>；None 模式注明原版。</summary>
    public static string ModeLabel(StartupModeInfo mode, string language)
    {
        ArgumentNullException.ThrowIfNull(mode);

        string label = Invariant($"{mode.Name} {mode.MapSize / 1000}km");
        return mode.CoreValue == SaveGateRules.NoneCoreValue ? Format(language, "Saves.ModeVanilla", label) : label;
    }

    /// <summary>按 CoreValue 取模式标签；不是现行模式时写出 CV。</summary>
    public static string CoreValueLabel(int coreValue, string language)
    {
        StartupModeInfo? mode = StartupKeys.FindModeByCoreValue(coreValue);
        return mode != null ? ModeLabel(mode, language) : Format(language, "Saves.UnknownCv", coreValue);
    }

    /// <summary>页面顶部：设定的模式与转换开关。</summary>
    public static string Header(GateSettings settings, string language)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return Format(language, "Saves.Header",
            CoreValueLabel(settings.CurrentCoreValue, language),
            Strings.Get(language, settings.ConversionEnabled ? "Saves.On" : "Saves.Off"));
    }

    /// <summary>列表与详情的标题：存档取文件名，地图取界面语言的标题。</summary>
    public static string Title(CokPackageInfo package, string language)
    {
        ArgumentNullException.ThrowIfNull(package);
        return package.Map?.GetTitle(language) is { Length: > 0 } title ? title : package.Name;
    }

    /// <summary>存档的模式说明；<paramref name="brief"/> 为 true 时给列表用的短写法。</summary>
    public static string SaveMode(SaveModeTag tag, string language, bool brief)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.Kind switch
        {
            SaveTagKind.Vanilla => Strings.Get(language, brief ? "Saves.Tag.VanillaShort" : "Saves.Tag.Vanilla"),
            SaveTagKind.Legacy => Format(language, brief ? "Saves.Tag.LegacyShort" : "Saves.Tag.Legacy", CoreValueLabel(tag.CoreValue, language)),
            SaveTagKind.Tagged => brief
                ? CoreValueLabel(tag.CoreValue, language)
                : Format(language, "Saves.Tag.Tagged", CoreValueLabel(tag.CoreValue, language), tag.CoreValue),
            _ => Strings.Get(language, brief ? "Saves.Tag.UnparsableShort" : "Saves.Tag.Unparsable"),
        };
    }

    /// <summary>地图按面积推断的模式说明。</summary>
    public static string MapMode(MapModeInference inference, string language, bool brief)
    {
        ArgumentNullException.ThrowIfNull(inference);

        if (inference.Trusted)
        {
            string mode = CoreValueLabel(inference.CoreValue, language);
            return brief ? mode : Format(language, "Saves.Map.Inferred", mode, inference.Ratio);
        }

        if (!double.IsFinite(inference.Ratio))
            return Strings.Get(language, brief ? "Saves.Map.UnknownShort" : "Saves.Map.NoArea");
        return Format(language, brief ? "Saves.Map.RatioShort" : "Saves.Map.Untrusted", inference.Ratio);
    }

    /// <summary>包的模式说明：存档按标签，地图按面积；读不出来的包为问题说明。</summary>
    public static string Mode(CokPackageInfo package, string language, bool brief)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Save != null)
            return SaveMode(package.Save.Mode, language, brief);
        if (package.Map != null)
            return MapMode(package.Map.Mode, language, brief);
        return brief ? Strings.Get(language, "Saves.Rel.Damaged") : Problem(package, language);
    }

    /// <summary>关系的短标签；<paramref name="relation"/> 为 null 表示包读不出来。</summary>
    public static string RelationLabel(PackageRelation? relation, string language) =>
        Strings.Get(language, relation is { } r ? "Saves.Rel." + r : "Saves.Rel.Damaged");

    public static PackageTone RelationTone(PackageRelation? relation) => relation switch
    {
        PackageRelation.Match => PackageTone.Good,
        PackageRelation.Convertible => PackageTone.Accent,
        PackageRelation.ConvertibleWhenEnabled or PackageRelation.Stretched or PackageRelation.Unchecked => PackageTone.Warning,
        PackageRelation.Unknown => PackageTone.Muted,
        _ => PackageTone.Error,
    };

    /// <summary>关系的说明：游戏里载入时会怎样。</summary>
    public static string RelationHint(CokPackageInfo package, PackageRelation relation, GateSettings? settings, string language)
    {
        ArgumentNullException.ThrowIfNull(package);

        bool isMap = package.Map != null;
        switch (relation)
        {
            case PackageRelation.Match:
                return Strings.Get(language, "Saves.Hint.Match");
            case PackageRelation.Convertible:
                return Strings.Get(language, isMap ? "Saves.Hint.ConvertibleMap" : "Saves.Hint.ConvertibleSave");
            case PackageRelation.ConvertibleWhenEnabled:
                return Strings.Get(language, "Saves.Hint.ConvertibleWhenEnabled");
            case PackageRelation.Stretched:
                string hint = Strings.Get(language, "Saves.Hint.Stretched");
                return settings != null && SaveGateRules.CanConvertTo(settings.CurrentCoreValue) && !settings.ConversionEnabled
                    ? hint + " " + Strings.Get(language, "Saves.Hint.StretchedConvertible")
                    : hint;
            case PackageRelation.Mismatch:
                return Strings.Get(language, isMap ? "Saves.Hint.MismatchMap" : "Saves.Hint.MismatchSave");
            case PackageRelation.Unchecked:
                return Strings.Get(language, "Saves.Hint.Unchecked");
            default:
                return Strings.Get(language, settings == null ? "Saves.Hint.UnknownSettings" : "Saves.Hint.UnknownMap");
        }
    }

    /// <summary>读不出来的原因。</summary>
    public static string Problem(CokPackageInfo package, string language)
    {
        ArgumentNullException.ThrowIfNull(package);
        return Format(language, "Saves.Problem." + package.Problem, package.ProblemDetail ?? string.Empty);
    }

    /// <summary>
    /// 详情栏的各行（预览图一行另由界面按解码结果生成，见 <see cref="Preview"/>）。
    /// 文件路径写成相对用户数据目录的形式，太长时界面会截断。
    /// </summary>
    public static IReadOnlyList<DetailLine> Details(CokPackageInfo package, GateSettings? settings, string language, string userDataRoot)
    {
        ArgumentNullException.ThrowIfNull(package);

        var lines = new List<DetailLine>();
        void Add(string labelKey, string? value, PackageTone tone = PackageTone.Normal)
        {
            if (!string.IsNullOrEmpty(value))
                lines.Add(new DetailLine(Strings.Get(language, labelKey), value, tone));
        }

        if (package.Problem != PackageProblem.None)
        {
            Add("Saves.D.Problem", Problem(package, language), PackageTone.Error);
        }
        else
        {
            if (package.Save is { } save)
            {
                Add("Saves.D.City", save.CityName);
                Add("Saves.D.Population", save.Population?.ToString("N0", CultureInfo.InvariantCulture));
                Add("Saves.D.Money", save.Money?.ToString("N0", CultureInfo.InvariantCulture));
                Add("Saves.D.MapUsed", save.MapName);
            }
            else if (package.Map is { } map)
            {
                Add("Saves.D.MapName", map.DisplayName);
                Add("Saves.D.Area", map.Area is float area && float.IsFinite(area) ? Invariant($"{area / 1e6:N1} km²") : null);
                Add("Saves.D.Theme", map.Theme);
            }

            Add("Saves.D.Mode", Mode(package, language, brief: false));
            PackageRelation? relation = SaveGateRules.Evaluate(package, settings);
            if (relation is { } r)
            {
                Add("Saves.D.Relation", RelationLabel(r, language) + " · " + RelationHint(package, r, settings, language), RelationTone(r));
            }

            if (package.Save?.AutoSave == true)
                Add("Saves.D.AutoSave", Strings.Get(language, "Saves.Yes"));
        }

        Add("Saves.D.Modified", package.LastWriteTime == default ? null : package.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Add("Saves.D.Size", FormatSize(package.Length));
        Add("Saves.D.File", RelativeTo(userDataRoot, package.Path));
        Add("Saves.D.Guid", package.MetadataGuid);
        if (package.DuplicatePaths.Count > 0)
            Add("Saves.D.Duplicate", string.Join(Environment.NewLine, package.DuplicatePaths.Select(p => RelativeTo(userDataRoot, p))), PackageTone.Warning);

        return lines;
    }

    /// <summary>
    /// 预览图一行：没有预览图、找不到条目、解码结果或读取错误。<paramref name="result"/> 与 <paramref name="error"/> 都为 null 表示还在读取。
    /// </summary>
    public static DetailLine Preview(CokPackageInfo package, TextureDecodeResult? result, string? error, string language)
    {
        ArgumentNullException.ThrowIfNull(package);

        string label = Strings.Get(language, "Saves.D.Preview");
        if (package.PreviewGuid == null)
            return new DetailLine(label, Strings.Get(language, "Saves.Preview.None"), PackageTone.Muted);
        if (package.PreviewEntry == null)
            return new DetailLine(label, Format(language, "Saves.Preview.Missing", package.PreviewGuid), PackageTone.Warning);
        if (error != null)
            return new DetailLine(label, Format(language, "Saves.Preview.Error", error), PackageTone.Warning);
        if (result == null)
            return new DetailLine(label, Strings.Get(language, "Saves.Preview.Loading"), PackageTone.Muted);

        TextureHeader? h = result.Header;
        return result.Status switch
        {
            TextureDecodeStatus.Decoded => new DetailLine(label, Format(language, "Saves.Preview.Decoded", h!.Width, h.Height, h.Version,
                Strings.Get(language, result.Compressed ? "Saves.Preview.Zstd" : "Saves.Preview.Raw"))),
            TextureDecodeStatus.Placeholder => new DetailLine(label, Strings.Get(language, "Saves.Preview.Placeholder"), PackageTone.Muted),
            TextureDecodeStatus.UnsupportedFormat => new DetailLine(label,
                Format(language, "Saves.Preview.UnsupportedFormat", h!.Width, h.Height, (object?)h.FormatName ?? h.Format), PackageTone.Muted),
            TextureDecodeStatus.UnsupportedLayout => new DetailLine(label, Strings.Get(language, "Saves.Preview.UnsupportedLayout"), PackageTone.Muted),
            TextureDecodeStatus.TooLarge => new DetailLine(label, Format(language, "Saves.Preview.TooLarge", h!.Width, h.Height), PackageTone.Muted),
            _ => new DetailLine(label, Format(language, "Saves.Preview.Corrupt", result.Detail ?? string.Empty), PackageTone.Warning),
        };
    }

    /// <summary>文件大小：1 KB 以下写字节数，其余为 KB／MB／GB，保留一位小数。</summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => Invariant($"{bytes} B"),
        < 1024 * 1024 => Invariant($"{bytes / 1024.0:0.0} KB"),
        < 1024L * 1024 * 1024 => Invariant($"{bytes / (1024.0 * 1024):0.0} MB"),
        _ => Invariant($"{bytes / (1024.0 * 1024 * 1024):0.0} GB"),
    };

    /// <summary>在用户数据目录下时写成相对路径（如 <c>Saves\7656…\a.cok</c>），否则原样返回。</summary>
    public static string RelativeTo(string root, string path)
    {
        if (string.IsNullOrEmpty(root))
            return path;

        string relative = Path.GetRelativePath(root, path);
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative) ? path : relative;
    }

    private static string Format(string language, string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
