using MapExTools.Core.Launch;

namespace MapExTools.Tests.Launch;

public class LaunchArgumentBuilderTests
{
    private static LaunchOptions AllOn() => new()
    {
        DisableUserSection = true,
        DisableCodeModding = true,
        DisableModding = true,
        NoSplash = true,
        DeveloperMode = true,
        UiDeveloperMode = true,
        DuplicateLogToDefault = true,
        LogLevel = "DEBUG",
        DisableBurstCompilation = true,
    };

    [Fact]
    public void Defaults_PassNothing()
    {
        var options = new LaunchOptions();
        Assert.Equal(LaunchChannel.Direct, options.Channel);
        Assert.Empty(LaunchArgumentBuilder.BuildOptionArguments(options));
        Assert.Equal(string.Empty, LaunchArgumentBuilder.BuildCommandLine(options));
        Assert.Equal(string.Empty, LaunchArgumentBuilder.BuildRunOnceText(options));
    }

    [Fact]
    public void AllOptions_FixedOrder_DisableModdingReplacesCodeModding()
    {
        Assert.Equal(
            "--disableUserSection --disableModding --noSplash --developerMode --uiDeveloperMode "
            + "--logsEffectiveness=DEBUG --duplicateLogToDefault --burst-disable-compilation",
            LaunchArgumentBuilder.BuildCommandLine(AllOn()));
    }

    [Fact]
    public void DisableCodeModdingAlone()
    {
        Assert.Equal(["--disableCodeModding"], LaunchArgumentBuilder.BuildOptionArguments(new LaunchOptions { DisableCodeModding = true }));
    }

    [Fact]
    public void RunOnce_OmitsBurst()
    {
        Assert.Equal(
            "--disableUserSection --disableModding --noSplash --developerMode --uiDeveloperMode "
            + "--logsEffectiveness=DEBUG --duplicateLogToDefault",
            LaunchArgumentBuilder.BuildRunOnceText(AllOn()));
        Assert.DoesNotContain(LaunchArgumentBuilder.BurstDisableArgument,
            LaunchArgumentBuilder.BuildOptionArguments(AllOn(), includeBurst: false));
    }

    [Theory]
    [InlineData("DEBUG", "DEBUG")]
    [InlineData("debug", "DEBUG")]
    [InlineData(" Warn ", "WARN")]
    [InlineData("DISABLED", "DISABLED")]
    [InlineData("DEFAULT", null)]
    [InlineData("default", null)]
    [InlineData("bogus", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeLogLevel(string? input, string? expected)
    {
        Assert.Equal(expected, LaunchArgumentBuilder.NormalizeLogLevel(input));
    }

    [Fact]
    public void UnknownLogLevel_IsNotPassed()
    {
        // 游戏遇到不认识的级别会关掉全部日志，所以手改配置写错时宁可不传
        Assert.Equal(string.Empty, LaunchArgumentBuilder.BuildCommandLine(new LaunchOptions { LogLevel = "VERBOS" }));
    }

    [Fact]
    public void LogLevels_AreDefaultPlusTheGameLevels()
    {
        // Level.GetLevel(string) 认识的 11 个名称
        Assert.Equal(
            ["DEFAULT", "ALL", "VERBOSE", "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "CRITICAL", "FATAL", "EMERGENCY", "DISABLED"],
            LaunchArgumentBuilder.LogLevels);
    }

    [Fact]
    public void CustomArguments_AppendedAsTyped_ForDirectAndSteam()
    {
        var options = new LaunchOptions { DeveloperMode = true, CustomArguments = "  -popupwindow \"--x=a b\"\r\n--y\t--z " };
        Assert.Equal("--developerMode -popupwindow \"--x=a b\" --y --z", LaunchArgumentBuilder.BuildCommandLine(options));
    }

    [Fact]
    public void CustomArguments_RunOnce_SingleSpacesNoTrailingNewline()
    {
        var options = new LaunchOptions { NoSplash = true, CustomArguments = " --a\r\n\r\n--b\t\t--c  " };
        string text = LaunchArgumentBuilder.BuildRunOnceText(options);
        Assert.Equal("--noSplash --a --b --c", text);
        Assert.False(text.EndsWith('\n'));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("--a", false)]
    [InlineData("\"--a b\"", true)]
    public void CustomHasQuotes(string? custom, bool expected)
    {
        Assert.Equal(expected, LaunchArgumentBuilder.CustomHasQuotes(new LaunchOptions { CustomArguments = custom }));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("\t--a\n", "--a")]
    public void NormalizeCustom(string? input, string expected)
    {
        Assert.Equal(expected, LaunchArgumentBuilder.NormalizeCustom(input));
    }
}
