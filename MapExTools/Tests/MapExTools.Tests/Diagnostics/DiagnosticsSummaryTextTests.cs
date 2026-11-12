using MapExTools.Core.Diagnostics;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Diagnostics;

/// <summary>会话摘要的措辞与醒目程度，以及复制出去的 Markdown。</summary>
public sealed class DiagnosticsSummaryTextTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private UserDataPaths Paths => new(_root);

    private DiagnosticsReport SampleReport()
    {
        Directory.CreateDirectory(Paths.LogsDir);
        File.Copy(LogSamples.PathOf("MapExtPDX-v4.12.txt"), Paths.MapExtLog);
        File.Copy(LogSamples.PathOf("SceneFlow.txt"), Paths.SceneFlowLog);
        File.Copy(LogSamples.PathOf("Player-crash.txt"), Paths.PlayerLog);
        return LogDiagnostics.Collect(Paths, gameRunning: false);
    }

    private static SummaryLine Find(IReadOnlyList<SummarySection> sections, string startsWith) =>
        sections.SelectMany(s => s.Lines).Single(l => l.Text.StartsWith(startsWith, StringComparison.Ordinal));

    [Fact]
    public void Build_SampleReport_SimplifiedChinese()
    {
        IReadOnlyList<SummarySection> sections = DiagnosticsSummaryText.Build(SampleReport(), UiLanguage.SimplifiedChinese, crashLogPath: null);

        Assert.Equal(["上次运行", "MapExt 4.12.0", "启动"], sections.Select(s => s.Title));

        Assert.Equal(SummarySeverity.Normal, Find(sections, "游戏版本：1.6.4f1 (1000.0001) [6400.20000]").Severity);
        Assert.Equal(SummarySeverity.Error, Find(sections, "Player.log：闪退（有 Unity 崩溃标记）").Severity);
        Assert.Equal("如果游戏是自己关掉的，请查看 游戏目录下的 logs/crash.log", Find(sections, "如果游戏是自己关掉的").Text);
        Assert.Equal(SummarySeverity.Warning, Find(sections, "Player.log 有 2 条异常或错误记录").Severity);
        Assert.Contains(sections[0].Lines, l => l.Text.StartsWith("NullReferenceException:", StringComparison.Ordinal) && l.Text.Contains(" — at Game.Sample", StringComparison.Ordinal));

        Assert.StartsWith("本会话生效配置：Mode=ModeA, CV=4", Find(sections, "本会话生效配置").Text, StringComparison.Ordinal);
        Assert.Equal(SummarySeverity.Normal, Find(sections, "Burst：已启用").Severity);
        Assert.Equal(SummarySeverity.Warning, Find(sections, "已知冲突 Mod：检测到冲突 Mod: SampleModA").Severity);
        Assert.Equal("本会话自动停用：ResidentAI", Find(sections, "本会话自动停用").Text);
        Assert.Contains("MenuLoad 'Sample City'", Find(sections, "最近一次读档判定").Text, StringComparison.Ordinal);
        Assert.Equal(SummarySeverity.Error, Find(sections, "警告 3 条，错误 1 条").Severity);

        Assert.Equal("启动参数：--developerMode --burst-disable-compilation logsEffectiveness=*** startGame=***", Find(sections, "启动参数").Text);
        Assert.Equal(SummarySeverity.Warning, Find(sections, "这次读档时订阅的 Mod 可能还没加载").Severity);
    }

    [Fact]
    public void Build_UsesKnownCrashLogPathAndOtherLanguages()
    {
        DiagnosticsReport report = SampleReport();

        IReadOnlyList<SummarySection> english = DiagnosticsSummaryText.Build(report, UiLanguage.English, @"D:\Games\CS2\logs\crash.log");
        Assert.Equal(@"If the game closed by itself, check D:\Games\CS2\logs\crash.log", Find(english, "If the game closed").Text);
        Assert.Equal("Last run", english[0].Title);

        IReadOnlyList<SummarySection> traditional = DiagnosticsSummaryText.Build(report, UiLanguage.TraditionalChinese, crashLogPath: null);
        Assert.Equal("上次執行", traditional[0].Title);
        Assert.Equal(SummarySeverity.Normal, Find(traditional, "Burst：已啟用").Severity);
    }

    [Fact]
    public void Build_NoLogs()
    {
        IReadOnlyList<SummarySection> sections = DiagnosticsSummaryText.Build(
            LogDiagnostics.Collect(Paths, gameRunning: false), UiLanguage.SimplifiedChinese, crashLogPath: null);

        Assert.Equal(SummarySeverity.Notice, Find(sections, "游戏版本：未知").Severity);
        Assert.Equal("MapExt", sections[1].Title);
        Assert.Equal(SummarySeverity.Warning, Find(sections, "没有 MapExtPDX.log").Severity);
        Assert.Equal(SummarySeverity.Notice, Find(sections, "没有 SceneFlow.log").Severity);
    }

    [Fact]
    public void Build_GameRunning_PlayerLogInProgressIsANotice()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Paths.PlayerLog, "Game version: 1.6.4f1 (1000.0001) [6400.20000]\n");

        IReadOnlyList<SummarySection> sections = DiagnosticsSummaryText.Build(
            LogDiagnostics.Collect(Paths, gameRunning: true), UiLanguage.SimplifiedChinese, crashLogPath: null);

        Assert.Equal(SummarySeverity.Notice, Find(sections, "Player.log：游戏还在运行").Severity);
        Assert.DoesNotContain(sections[0].Lines, l => l.Text.StartsWith("如果游戏是自己关掉的", StringComparison.Ordinal));
    }

    [Fact]
    public void ToMarkdown_HeadingsPrefixesEscapingAndUserProfile()
    {
        DiagnosticsReport report = SampleReport();
        const string profile = @"C:\Users\Some_User";
        IReadOnlyList<SummarySection> sections = DiagnosticsSummaryText.Build(report, UiLanguage.SimplifiedChinese, profile + @"\Games\CS2\logs\crash.log");

        string markdown = DiagnosticsSummaryText.ToMarkdown(report, sections, UiLanguage.SimplifiedChinese, profile);

        Assert.StartsWith("## MapExTools 诊断摘要", markdown, StringComparison.Ordinal);
        Assert.Contains("### 上次运行", markdown, StringComparison.Ordinal);
        Assert.Contains("- ❌ Player.log：闪退（有 Unity 崩溃标记）", markdown, StringComparison.Ordinal);
        Assert.Contains("- ⚠️ 已知冲突 Mod：", markdown, StringComparison.Ordinal);
        Assert.Contains(@"logsEffectiveness=\*\*\*", markdown, StringComparison.Ordinal);
        Assert.Contains(@"\<00000000000000000000000000000000\>", markdown, StringComparison.Ordinal);

        // 用户名里有下划线也能换掉，且不留原名
        Assert.Contains(@"%USERPROFILE%\Games\CS2\logs\crash.log", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Some", markdown, StringComparison.Ordinal);
    }
}
