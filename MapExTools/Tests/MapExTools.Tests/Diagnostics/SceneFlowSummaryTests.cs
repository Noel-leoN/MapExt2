using MapExTools.Core.Diagnostics;

namespace MapExTools.Tests.Diagnostics;

/// <summary>SceneFlow.log：命令行（带值参数被打码）、Configuration、版本、Mod 未加载就读档的 60 秒告警。</summary>
public sealed class SceneFlowSummaryTests
{
    [Fact]
    public void Sample_CommandLineWithMaskedArguments()
    {
        SceneFlowSummary s = SceneFlowSummary.Parse(LogSamples.ReadLines("SceneFlow.txt"));

        Assert.Equal("2026-11-12 20:00:50,000", s.CommandLineTimestamp);
        Assert.Equal(@"D:\Games\Steam\steamapps\common\Cities Skylines II\Cities2.exe", s.ExecutablePath);
        Assert.Equal(["--developerMode", "--burst-disable-compilation", "logsEffectiveness", "startGame"], s.Arguments.Select(a => a.Key));
        Assert.Equal([false, false, true, true], s.Arguments.Select(a => a.IsMasked));
        Assert.False(s.ViaParadoxLauncher);
    }

    [Fact]
    public void Sample_ConfigurationVersionAndTimeout()
    {
        SceneFlowSummary s = SceneFlowSummary.Parse(LogSamples.ReadLines("SceneFlow.txt"));

        Assert.NotNull(s.Configuration);
        Assert.Equal("0123456789abcdef0123456789abcdef", s.Configuration["startGame"]);
        Assert.Equal("true", s.Configuration["developerMode"]);
        Assert.Equal("null", s.Configuration["profilerTarget"]);
        Assert.Equal("1.6.4f1 (1000.0001) [6400.20000] Windows Steamworks", s.GameVersion);

        Assert.NotNull(s.ModsRegistrationTimeout);
        Assert.Equal("2026-11-12 20:02:59,000", s.ModsRegistrationTimeout.Timestamp);
        Assert.False(s.GameManagerDestroyed);
    }

    [Fact]
    public void LaunchedByParadoxLauncher_AndShutDownNormally()
    {
        SceneFlowSummary s = SceneFlowSummary.Parse(LogSamples.Lines("""
            [2026-11-09 23:49:30,061] [INFO]  Command line: D:\Games\Steam\steamapps\common\Cities Skylines II\Cities2.exe
            D:\Games\Steam\steamapps\common\Cities Skylines II\Launcher\dowser.exe
            --developerMode
            [2026-11-10 00:26:44,369] [INFO]  GameManager destroyed (5755.788ms)
            """));

        Assert.True(s.ViaParadoxLauncher);
        Assert.True(s.GameManagerDestroyed);
        Assert.Null(s.ModsRegistrationTimeout);
        Assert.Null(s.Configuration);
    }

    [Theory]
    [InlineData("startGame=****", true, "startGame")]
    [InlineData("logsEffectiveness=", true, "logsEffectiveness")] // 空值打码后仍为空
    [InlineData("******", true, "******")]                         // 敏感键之后的那一项整个打码
    [InlineData("--developerMode", false, "--developerMode")]
    [InlineData("-popupwindow", false, "-popupwindow")]
    public void CommandLineArgument_MaskDetection(string text, bool masked, string key)
    {
        var argument = new CommandLineArgument(text);
        Assert.Equal(masked, argument.IsMasked);
        Assert.Equal(key, argument.Key);
    }

    [Fact]
    public void BrokenConfigurationJson_IsNull()
    {
        SceneFlowSummary s = SceneFlowSummary.Parse(["[2026-11-12 20:00:55,010] [INFO]  Configuration: {not json"]);
        Assert.Null(s.Configuration);
    }

    [Fact]
    public void TimeoutMustBeAWarning()
    {
        // 同样的字样出现在 INFO 里不算告警
        SceneFlowSummary s = SceneFlowSummary.Parse(["[2026-11-12 20:00:55,010] [INFO]  Mods registration did not start within 60s, continuing"]);
        Assert.Null(s.ModsRegistrationTimeout);
    }
}
