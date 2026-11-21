using MapExTools.Core.CommandLine;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Tests.CommandLine;

public class CommandLineParserTests
{
    private static CommandLineOptions Parse(params string[] args) => CommandLineParser.Parse(args);

    private static CommandLineException Fails(params string[] args) =>
        Assert.Throws<CommandLineException>(() => CommandLineParser.Parse(args));

    [Fact]
    public void NoArguments_OpensWindow()
    {
        CommandLineOptions options = Parse();
        Assert.False(options.IsCommandMode);
        Assert.Empty(options.Settings);
        Assert.Null(options.Language);
        Assert.Null(options.UserData);
        Assert.Null(options.GameProcess);
    }

    [Fact]
    public void ModifiersOnly_StillOpenWindow()
    {
        CommandLineOptions options = Parse("--lang", "zh", "--user-data=D:\\UD", "--GAME-PROCESS", "pwsh.exe");
        Assert.False(options.IsCommandMode);
        Assert.Equal(UiLanguage.SimplifiedChinese, options.Language);
        Assert.Equal(@"D:\UD", options.UserData);
        Assert.Equal("pwsh", options.GameProcess);
    }

    [Theory]
    [InlineData("--show")]
    [InlineData("--reset")]
    [InlineData("--launch")]
    [InlineData("--check-conflicts")]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    [InlineData("/?")]
    [InlineData("--SHOW")]
    public void Flags_AreCommandMode(string flag)
    {
        Assert.True(Parse(flag).IsCommandMode);
    }

    [Fact]
    public void Flags_SetTheirProperty()
    {
        CommandLineOptions options = Parse("--show", "--reset", "--launch", "--check-conflicts", "--help");
        Assert.True(options.Show);
        Assert.True(options.Reset);
        Assert.True(options.Launch);
        Assert.True(options.CheckConflicts);
        Assert.True(options.Help);
    }

    [Theory]
    [InlineData("modeb", "ModeB")]
    [InlineData("ModeC", "ModeC")]
    [InlineData("NONE", "None")]
    [InlineData(" ModeD ", "ModeD")]
    public void Mode_IsCaseInsensitive_AndCanonical(string value, string expected)
    {
        Assert.Equal(expected, Parse("--mode", value).Settings[StartupKeys.ModeKey]);
        Assert.Equal(expected, Parse("--mode=" + value).Settings[StartupKeys.ModeKey]);
    }

    public static TheoryData<string, string> BoolOptions()
    {
        var data = new TheoryData<string, string>();
        foreach (SettingOption option in CommandLineParser.SettingOptions)
        {
            if (option.Key.Kind == StartupKeyKind.Bool)
                data.Add(option.Name, option.Key.Name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(BoolOptions))]
    public void BoolOptions_OnOff(string option, string key)
    {
        Assert.Equal(true, Parse(option, "on").Settings[key]);
        Assert.Equal(false, Parse(option + "=OFF").Settings[key]);
        Assert.Equal(CommandLineErrorKind.InvalidValue, Fails(option, "true").Kind);
    }

    [Fact]
    public void SettingOptions_CoverEveryStartupKeyInOrder()
    {
        Assert.Equal(StartupKeys.Keys.Select(k => k.Name), CommandLineParser.SettingOptions.Select(o => o.Key.Name));
        Assert.Equal(CommandLineParser.SettingOptions.Count, CommandLineParser.SettingOptions.Select(o => o.Name).Distinct().Count());
    }

    [Fact]
    public void SeveralSettings()
    {
        CommandLineOptions options = Parse("--mode", "ModeB", "--demand", "off", "--disable-load-game-validation=on");
        Assert.True(options.IsCommandMode);
        Assert.Equal(3, options.Settings.Count);
        Assert.Equal(false, options.Settings["EnableDemandEcoSystem"]);
        Assert.Equal(true, options.Settings["DisableLoadGameValidation"]);
    }

    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("EN-us", "en-US")]
    [InlineData("zh-CN", "zh-HANS")]
    [InlineData("zh-hans", "zh-HANS")]
    [InlineData("zh-TW", "zh-HANT")]
    [InlineData("ZH-HANT", "zh-HANT")]
    [InlineData("fr", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeLanguage(string? value, string? expected)
    {
        Assert.Equal(expected, CommandLineParser.NormalizeLanguage(value));
    }

    [Theory]
    [InlineData(CommandLineErrorKind.Unknown, "--foo")]
    [InlineData(CommandLineErrorKind.Unknown, "foo")]
    [InlineData(CommandLineErrorKind.Unknown, "-x")]
    [InlineData(CommandLineErrorKind.Unknown, "--")]
    [InlineData(CommandLineErrorKind.MissingValue, "--mode")]
    [InlineData(CommandLineErrorKind.MissingValue, "--mode", "--show")]
    [InlineData(CommandLineErrorKind.MissingValue, "--user-data")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--mode", "ModeX")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--mode=")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--demand", "maybe")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--lang", "fr")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--user-data", " ")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--game-process", @"C:\x\pwsh.exe")]
    [InlineData(CommandLineErrorKind.InvalidValue, "--game-process", ".exe")]
    [InlineData(CommandLineErrorKind.UnexpectedValue, "--show=1")]
    [InlineData(CommandLineErrorKind.UnexpectedValue, "--launch=yes")]
    [InlineData(CommandLineErrorKind.UnexpectedValue, "--check-conflicts=1")]
    [InlineData(CommandLineErrorKind.Duplicate, "--mode", "ModeA", "--MODE", "ModeB")]
    [InlineData(CommandLineErrorKind.Duplicate, "--show", "--show")]
    [InlineData(CommandLineErrorKind.Duplicate, "--check-conflicts", "--check-conflicts")]
    public void Errors(CommandLineErrorKind kind, params string[] args)
    {
        Assert.Equal(kind, Fails(args).Kind);
    }

    [Fact]
    public void ErrorMessage_IsLocalized_AndNamesTheOption()
    {
        CommandLineException ex = Fails("--mode", "ModeX");
        Assert.Equal("--mode", ex.Option);
        Assert.Equal("ModeX", ex.Value);
        Assert.Equal("Invalid value for --mode: 'ModeX'", ex.Message);
        Assert.Equal("选项 --mode 的取值无效：'ModeX'", ex.GetMessage(UiLanguage.SimplifiedChinese));
        Assert.Equal("不认识的选项：--Foo", Fails("--Foo").GetMessage(UiLanguage.SimplifiedChinese));
    }

    [Fact]
    public void PeekOptions_FindsLanguageAndUserDataInBadArguments()
    {
        Assert.Equal(("zh-HANT", @"D:\UD"), CommandLineParser.PeekOptions(["--mode", "ModeX", "--LANG=zh-tw", "--user-data", @"D:\UD"]));
        Assert.Equal((null, null), CommandLineParser.PeekOptions(["--lang", "fr", "--user-data"]));
        Assert.Equal(("zh-HANS", null), CommandLineParser.PeekOptions(["--user-data", "--lang", "zh"]));
        Assert.Equal(((string?)null, (string?)null), CommandLineParser.PeekOptions([]));
    }
}
