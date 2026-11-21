using MapExTools.Core.Appearance;
using MapExTools.Core.Localization;

namespace MapExTools.Tests.Appearance;

public sealed class FontSpecTests
{
    [Theory]
    [InlineData("en-US", "en-US")]
    [InlineData("en-us", "en-US")]
    [InlineData("zh-HANS", "zh-HANS")]
    [InlineData("zh-hans", "zh-HANS")]
    [InlineData("zh-HANT", "zh-TW")]
    [InlineData("ZH-hant", "zh-TW")]
    [InlineData("ja-JP", "en-US")]
    [InlineData("", "en-US")]
    [InlineData(null, "en-US")]
    public void GetXmlLanguageTag_MapsExpectedTag(string? uiLanguage, string expected)
    {
        Assert.Equal(expected, FontSpec.GetXmlLanguageTag(uiLanguage));
    }

    [Theory]
    [InlineData(UiLanguage.English, "Segoe UI Variable Text, Segoe UI")]
    [InlineData(UiLanguage.SimplifiedChinese, "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI")]
    [InlineData(UiLanguage.TraditionalChinese, "Microsoft JhengHei UI, Segoe UI Variable Text, Segoe UI")]
    [InlineData("unknown", "Segoe UI Variable Text, Segoe UI")]
    [InlineData(null, "Segoe UI Variable Text, Segoe UI")]
    public void GetDefaultFontChain_ReturnsExpectedChain_WithoutBareSegoeUiVariable(string? uiLanguage, string expected)
    {
        string chain = FontSpec.GetDefaultFontChain(uiLanguage);
        Assert.Equal(expected, chain);
        Assert.Contains("Segoe UI Variable Text, Segoe UI", chain);

        string[] parts = chain.Split(',', StringSplitOptions.TrimEntries);
        Assert.DoesNotContain("Segoe UI Variable", parts, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\t\r\n", null)]
    [InlineData(" Noto Sans SC ", "Noto Sans SC")]
    [InlineData("Segoe UI, Arial", "Segoe UI")]
    [InlineData(", Arial", null)]
    [InlineData("Noto\r\n Sans\t SC", "Noto Sans SC")]
    [InlineData("./Fonts/#My Font", null)]
    [InlineData(@"C:\Fonts\a.ttf", null)]
    [InlineData("file:///C:/a.ttf#A", null)]
    [InlineData("#CustomFont", null)]
    public void NormalizeConfiguredFamily_SanitizesAndRejectsUriOrPathSyntax(string? raw, string? expected)
    {
        Assert.Equal(expected, FontSpec.NormalizeConfiguredFamily(raw));
    }

    [Theory]
    [InlineData(null, UiLanguage.English, "Segoe UI Variable Text, Segoe UI")]
    [InlineData("   ", UiLanguage.SimplifiedChinese, "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI")]
    [InlineData("Noto Sans SC", UiLanguage.SimplifiedChinese, "Noto Sans SC, Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI")]
    [InlineData("microsoft yahei ui", UiLanguage.SimplifiedChinese, "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI")]
    [InlineData("SEGOE UI VARIABLE TEXT", UiLanguage.English, "Segoe UI Variable Text, Segoe UI")]
    [InlineData("Segoe UI", UiLanguage.English, "Segoe UI, Segoe UI Variable Text, Segoe UI")]
    [InlineData("./Fonts/#Bad", UiLanguage.TraditionalChinese, "Microsoft JhengHei UI, Segoe UI Variable Text, Segoe UI")]
    public void BuildEffectiveFontChain_PrependsCustomAndDeduplicatesPrimary(string? custom, string uiLanguage, string expected)
    {
        Assert.Equal(expected, FontSpec.BuildEffectiveFontChain(custom, uiLanguage));
    }

    [Theory]
    [InlineData("Segoe UI Emoji", true)]
    [InlineData("segoe ui symbol", true)]
    [InlineData("Segoe UI Historic", true)]
    [InlineData("HoloLens MDL2 Assets", true)]
    [InlineData("Segoe UI", false)]
    [InlineData("Segoe UI Variable Text", false)]
    [InlineData("Microsoft YaHei UI", false)]
    [InlineData("Microsoft JhengHei UI", false)]
    [InlineData("SimSun", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsExcludedNonTextFontName_FiltersExpectedSystemFonts(string? familyName, bool expected)
    {
        Assert.Equal(expected, FontSpec.IsExcludedNonTextFontName(familyName));
    }
}
