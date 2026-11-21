using MapExTools.Core.Localization;

namespace MapExTools.Core.Appearance;

/// <summary>
/// 三语默认字体回退链、`XmlLanguage` 映射与自定义字族名规范化规则（无 WPF 依赖，供 Core 配置与单元测试共用）。
/// </summary>
public static class FontSpec
{
    public const string EnglishDefaultChain = "Segoe UI Variable Text, Segoe UI";
    public const string SimplifiedChineseDefaultChain = "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI";
    public const string TraditionalChineseDefaultChain = "Microsoft JhengHei UI, Segoe UI Variable Text, Segoe UI";

    // GlyphTypeface.Symbol 为 false、但不适合作为界面正文字体的系统字库黑名单（D5）
    private static readonly HashSet<string> s_excludedNonTextFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Segoe UI Emoji",
        "Segoe UI Symbol",
        "Segoe UI Historic",
        "HoloLens MDL2 Assets",
    };

    /// <summary>
    /// 将界面语言映射为 WPF <c>FrameworkElement.Language</c> 所用的标签（D3）：
    /// <c>zh-HANT</c> 显式用 <c>zh-TW</c>（避免隐式落入 <c>zh-HK</c> 增补字集），未知语言回退 <c>en-US</c>。
    /// </summary>
    public static string GetXmlLanguageTag(string? uiLanguage) =>
        UiLanguage.Normalize(uiLanguage) switch
        {
            UiLanguage.SimplifiedChinese => "zh-HANS",
            UiLanguage.TraditionalChinese => "zh-TW",
            _ => "en-US",
        };

    /// <summary>返回指定界面语言的默认正文字体回退链（D2、D9）。</summary>
    public static string GetDefaultFontChain(string? uiLanguage) =>
        UiLanguage.Normalize(uiLanguage) switch
        {
            UiLanguage.SimplifiedChinese => SimplifiedChineseDefaultChain,
            UiLanguage.TraditionalChinese => TraditionalChineseDefaultChain,
            _ => EnglishDefaultChain,
        };

    /// <summary>
    /// 规范化配置中的自定义字族名（1.2）：
    /// ① 删除控制字符（含换行、制表）；② 仅保留首个逗号前的字族名；③ 去首尾空白；
    /// ④ 为空或含 <c>#</c>、<c>/</c>、<c>\</c>（文件／URI 加载语法，方案 A 不支持）时返回 <c>null</c>。
    /// </summary>
    public static string? NormalizeConfiguredFamily(string? family)
    {
        if (string.IsNullOrEmpty(family))
            return null;

        // ① 过滤控制字符
        Span<char> buffer = family.Length <= 256 ? stackalloc char[family.Length] : new char[family.Length];
        int len = 0;
        foreach (char c in family)
        {
            if (!char.IsControl(c))
                buffer[len++] = c;
        }

        ReadOnlySpan<char> cleaned = buffer[..len];

        // ② 只保留第一个逗号之前的部分
        int comma = cleaned.IndexOf(',');
        if (comma >= 0)
            cleaned = cleaned[..comma];

        // ③ 去除首尾空白
        cleaned = cleaned.Trim();

        // ④ 为空或含文件／URI 语法字符时视为无效
        if (cleaned.IsEmpty || cleaned.IndexOfAny('#', '/', '\\') >= 0)
            return null;

        return cleaned.ToString();
    }

    /// <summary>
    /// 生成最终传给 WPF <c>FontFamily</c> 的复合回退链：
    /// 自定义字族为空时返回默认链；非空时拼接在默认链之前（与默认链首项忽略大小写相同时不重复拼接）。
    /// </summary>
    public static string BuildEffectiveFontChain(string? customFamily, string? uiLanguage)
    {
        string defaultChain = GetDefaultFontChain(uiLanguage);
        string? normalized = NormalizeConfiguredFamily(customFamily);
        if (normalized == null)
            return defaultChain;

        int comma = defaultChain.IndexOf(',');
        ReadOnlySpan<char> primary = comma >= 0 ? defaultChain.AsSpan(0, comma).Trim() : defaultChain.AsSpan().Trim();
        if (primary.Equals(normalized.AsSpan(), StringComparison.OrdinalIgnoreCase))
            return defaultChain;

        return $"{normalized}, {defaultChain}";
    }

    /// <summary>是否为应排除在正文字体选择器之外的非正文系统字库（D5）。</summary>
    public static bool IsExcludedNonTextFontName(string? familyName) =>
        !string.IsNullOrWhiteSpace(familyName) && s_excludedNonTextFonts.Contains(familyName.Trim());
}
