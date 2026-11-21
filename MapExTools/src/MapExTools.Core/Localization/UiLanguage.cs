using System.Globalization;

namespace MapExTools.Core.Localization;

/// <summary>界面语言代码，与 Mod 的 <c>LocaleEN</c>／<c>LocaleHANS</c>／<c>LocaleHANT</c> 及游戏的语言 id 一致。</summary>
public static class UiLanguage
{
    public const string English = "en-US";
    public const string SimplifiedChinese = "zh-HANS";
    public const string TraditionalChinese = "zh-HANT";

    /// <summary>全部语言，顺序即 <see cref="Strings"/> 每条文案的数组下标。</summary>
    public static readonly IReadOnlyList<string> All = [English, SimplifiedChinese, TraditionalChinese];

    /// <summary>语言自称，切换语言的下拉框用它，不随界面语言变化。</summary>
    public static string GetNativeName(string language) => language switch
    {
        SimplifiedChinese => "简体中文",
        TraditionalChinese => "繁體中文",
        _ => "English",
    };

    /// <summary>认得的语言代码返回规范写法（不分大小写），否则返回 null。</summary>
    public static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return null;

        foreach (string known in All)
        {
            if (string.Equals(known, language.Trim(), StringComparison.OrdinalIgnoreCase))
                return known;
        }

        return null;
    }

    /// <summary>
    /// 按系统界面文化选默认语言：简体（zh-Hans、zh-CN、zh-SG）→ zh-HANS，
    /// 繁体（zh-Hant、zh-TW、zh-HK、zh-MO）→ zh-HANT，其余 → en-US。
    /// </summary>
    public static string FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        for (CultureInfo c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            switch (c.Name.ToLowerInvariant())
            {
                case "zh-hans":
                case "zh-cn":
                case "zh-sg":
                    return SimplifiedChinese;
                case "zh-hant":
                case "zh-tw":
                case "zh-hk":
                case "zh-mo":
                    return TraditionalChinese;
            }

            if (c.Parent == c)
                break;
        }

        // 只有中性的 "zh" 时按简体处理
        return culture.TwoLetterISOLanguageName == "zh" ? SimplifiedChinese : English;
    }

    internal static int IndexOf(string language)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i], language, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }
}
