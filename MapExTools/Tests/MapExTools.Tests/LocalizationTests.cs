using System.Globalization;
using System.Text.RegularExpressions;
using MapExTools.Core.Localization;

namespace MapExTools.Tests;

public partial class LocalizationTests
{
    [GeneratedRegex(@"\{(\d+)[^}]*\}")]
    private static partial Regex PlaceholderRegex();

    public static TheoryData<string> AllKeys()
    {
        var data = new TheoryData<string>();
        foreach (string key in Strings.Keys)
            data.Add(key);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EveryKey_HasThreeNonEmptyVersionsWithSamePlaceholders(string key)
    {
        IReadOnlyList<string> values = Strings.GetAll(key);
        Assert.Equal(UiLanguage.All.Count, values.Count);
        Assert.All(values, v => Assert.False(string.IsNullOrWhiteSpace(v)));

        string Placeholders(string s) => string.Join(",", PlaceholderRegex().Matches(s).Select(m => m.Groups[1].Value).Order());
        string english = Placeholders(values[0]);
        Assert.All(values, v => Assert.Equal(english, Placeholders(v)));
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKey()
    {
        Assert.Equal("No.Such.Key", Strings.Get(UiLanguage.English, "No.Such.Key"));
    }

    [Fact]
    public void Get_UnknownLanguage_FallsBackToEnglish()
    {
        Assert.Equal(Strings.Get(UiLanguage.English, "Tab.Logs"), Strings.Get("fr-FR", "Tab.Logs"));
    }

    [Theory]
    [InlineData("zh-HANS", "zh-HANS")]
    [InlineData("ZH-hant", "zh-HANT")]
    [InlineData(" en-us ", "en-US")]
    [InlineData("zh-CN", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Normalize(string? input, string? expected)
    {
        Assert.Equal(expected, UiLanguage.Normalize(input));
    }

    [Theory]
    [InlineData("zh-CN", "zh-HANS")]
    [InlineData("zh-SG", "zh-HANS")]
    [InlineData("zh-Hans", "zh-HANS")]
    [InlineData("zh-TW", "zh-HANT")]
    [InlineData("zh-HK", "zh-HANT")]
    [InlineData("zh-MO", "zh-HANT")]
    [InlineData("zh-Hant", "zh-HANT")]
    [InlineData("zh", "zh-HANS")]
    [InlineData("en-GB", "en-US")]
    [InlineData("ja-JP", "en-US")]
    public void FromCulture(string culture, string expected)
    {
        Assert.Equal(expected, UiLanguage.FromCulture(CultureInfo.GetCultureInfo(culture)));
    }
}
