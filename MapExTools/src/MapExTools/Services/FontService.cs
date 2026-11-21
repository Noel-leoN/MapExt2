using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using MapExTools.Core.Appearance;
using MapExTools.Core.Localization;

namespace MapExTools.Services;

/// <summary>字体下拉框中的单个选项。<see cref="ConfigName"/> 为 null 表示「默认（跟随语言）」。</summary>
internal sealed record FontOption(string? ConfigName, string DisplayName, bool IsMissing = false)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// 系统已安装字体的懒加载枚举、多语言显示名解析与窗口全局字体应用服务（方案 A）。
/// </summary>
internal static class FontService
{
    private static readonly XmlLanguage s_enUsXmlLang = XmlLanguage.GetLanguage("en-US");
    private static readonly string[] s_hansTags = ["zh-cn", "zh-hans", "zh-sg"];
    private static readonly string[] s_hantTags = ["zh-tw", "zh-hk", "zh-hant", "zh-mo"];
    private static readonly string[] s_enTags = ["en-us"];

    private static IReadOnlyList<CachedFontFamily>? s_cachedFamilies;

    private sealed record CachedFontFamily(string ConfigName, IReadOnlyDictionary<string, string> NamesByTag);

    /// <summary>套用窗口的 <c>XmlLanguage</c>、正文字体回退链，并覆盖 Fluent <c>ToolTip</c> 所绑定的状态栏字体资源键。</summary>
    public static void Apply(Window window, string? customFamily, string uiLanguage)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Language = XmlLanguage.GetLanguage(FontSpec.GetXmlLanguageTag(uiLanguage));
        var fontFamily = new FontFamily(FontSpec.BuildEffectiveFontChain(customFamily, uiLanguage));
        window.FontFamily = fontFamily;

        if (Application.Current is { } app)
            app.Resources[SystemFonts.StatusFontFamilyKey] = fontFamily;
    }

    /// <summary>
    /// 构造下拉框未展开前的单个选中占位项（绝不枚举 <see cref="Fonts.SystemFontFamilies"/>）：
    /// 默认字体直接返回默认项；已配置自定义字族时，若尚未缓存则仅对该单字族做轻量探测以显示本地化名或「未安装」标记。
    /// </summary>
    public static FontOption CreateSelectedPlaceholder(string uiLanguage, string? configuredFamily)
    {
        string lang = UiLanguage.Normalize(uiLanguage) ?? UiLanguage.English;
        string? normalized = FontSpec.NormalizeConfiguredFamily(configuredFamily);
        if (normalized == null)
            return new FontOption(null, Strings.Get(lang, "Settings.FontDefault"));

        if (s_cachedFamilies is { } cached)
        {
            CachedFontFamily? hit = cached.FirstOrDefault(f => MatchesFamily(f, normalized));
            return hit != null
                ? new FontOption(hit.ConfigName, ResolveDisplayName(hit, lang))
                : new FontOption(normalized, FormatMissing(lang, normalized), IsMissing: true);
        }

        // 未枚举全量系统字体时，仅针对当前配置的单个字族读取一次本地化名称表（< 1 ms，零全量枚举）
        if (TryInspectSingleFamily(normalized) is { } single && MatchesFamily(single, normalized))
            return new FontOption(single.ConfigName, ResolveDisplayName(single, lang));

        return new FontOption(normalized, FormatMissing(lang, normalized), IsMissing: true);
    }

    /// <summary>
    /// 首次调用时枚举并缓存本机可用正文字族（每族仅探测第一个可用 <see cref="GlyphTypeface"/>），
    /// 返回按当前界面语言本地化并排序的下拉选项列表及当前应选中的项。
    /// </summary>
    public static (IReadOnlyList<FontOption> Options, FontOption Selected) GetOptions(string uiLanguage, string? configuredFamily)
    {
        string lang = UiLanguage.Normalize(uiLanguage) ?? UiLanguage.English;
        string? normalized = FontSpec.NormalizeConfiguredFamily(configuredFamily);
        IReadOnlyList<CachedFontFamily> families = EnsureCachedFamilies();

        StringComparer sortComparer = GetSortComparer(lang);
        var installedOptions = new List<(CachedFontFamily Family, FontOption Option)>(families.Count);
        foreach (CachedFontFamily family in families)
        {
            installedOptions.Add((family, new FontOption(family.ConfigName, ResolveDisplayName(family, lang))));
        }

        installedOptions.Sort((a, b) =>
        {
            int cmp = sortComparer.Compare(a.Option.DisplayName, b.Option.DisplayName);
            return cmp != 0 ? cmp : StringComparer.OrdinalIgnoreCase.Compare(a.Option.ConfigName, b.Option.ConfigName);
        });

        var defaultOption = new FontOption(null, Strings.Get(lang, "Settings.FontDefault"));
        var result = new List<FontOption>(installedOptions.Count + 2) { defaultOption };

        FontOption? selected = normalized == null ? defaultOption : null;
        foreach ((CachedFontFamily family, FontOption option) in installedOptions)
        {
            if (selected == null && normalized != null && MatchesFamily(family, normalized))
                selected = option;
            result.Add(option);
        }

        if (selected == null && normalized != null)
        {
            var missingOption = new FontOption(normalized, FormatMissing(lang, normalized), IsMissing: true);
            result.Insert(1, missingOption);
            selected = missingOption;
        }

        return (result, selected ?? defaultOption);
    }

    private static IReadOnlyList<CachedFontFamily> EnsureCachedFamilies()
    {
        if (s_cachedFamilies is { } existing)
            return existing;

        var list = new List<CachedFontFamily>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (FontFamily family in Fonts.SystemFontFamilies)
        {
            if (TryInspectFamily(family) is not { } inspected)
                continue;
            if (seen.Add(inspected.ConfigName))
                list.Add(inspected);
        }

        s_cachedFamilies = list;
        return list;
    }

    private static CachedFontFamily? TryInspectSingleFamily(string familyName)
    {
        try
        {
            return TryInspectFamily(new FontFamily(familyName));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// 按 D5 检验单个 <see cref="FontFamily"/>：取到第一个可用 <see cref="GlyphTypeface"/> 即按其 <c>Symbol</c> 判定并停止；
    /// 全部取不到（如 <c>Global *</c> 复合字族）、<c>Symbol == true</c> 或命中非正文黑名单则返回 null。
    /// </summary>
    private static CachedFontFamily? TryInspectFamily(FontFamily family)
    {
        bool foundGlyph = false;
        bool isSymbol = false;
        foreach (Typeface typeface in family.GetTypefaces())
        {
            if (typeface.TryGetGlyphTypeface(out GlyphTypeface? glyph) && glyph != null)
            {
                foundGlyph = true;
                isSymbol = glyph.Symbol;
                break;
            }
        }

        if (!foundGlyph || isSymbol)
            return null;

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<XmlLanguage, string> pair in family.FamilyNames)
        {
            if (!string.IsNullOrWhiteSpace(pair.Value))
                names[pair.Key.IetfLanguageTag] = pair.Value.Trim();
        }

        string configName = family.FamilyNames.TryGetValue(s_enUsXmlLang, out string? enName) && !string.IsNullOrWhiteSpace(enName)
            ? enName.Trim()
            : family.Source.Trim();

        if (string.IsNullOrWhiteSpace(configName) || FontSpec.IsExcludedNonTextFontName(configName))
            return null;

        return new CachedFontFamily(configName, names);
    }

    private static bool MatchesFamily(CachedFontFamily family, string candidate)
    {
        if (string.Equals(family.ConfigName, candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (string localized in family.NamesByTag.Values)
        {
            if (string.Equals(localized, candidate, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ResolveDisplayName(CachedFontFamily family, string uiLanguage)
    {
        string[] preferredTags = uiLanguage switch
        {
            UiLanguage.SimplifiedChinese => s_hansTags,
            UiLanguage.TraditionalChinese => s_hantTags,
            _ => s_enTags,
        };

        foreach (string tag in preferredTags)
        {
            if (family.NamesByTag.TryGetValue(tag, out string? name) && !string.IsNullOrWhiteSpace(name))
                return name;
        }

        return family.ConfigName;
    }

    private static StringComparer GetSortComparer(string uiLanguage)
    {
        string cultureName = uiLanguage switch
        {
            UiLanguage.SimplifiedChinese => "zh-CN",
            UiLanguage.TraditionalChinese => "zh-TW",
            _ => "en-US",
        };
        return StringComparer.Create(CultureInfo.GetCultureInfo(cultureName), ignoreCase: true);
    }

    private static string FormatMissing(string uiLanguage, string familyName) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(uiLanguage, "Settings.FontMissing"), familyName);
}
