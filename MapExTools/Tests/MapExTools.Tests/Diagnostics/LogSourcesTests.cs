using MapExTools.Core.Diagnostics;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Diagnostics;

/// <summary>可浏览的日志：固定的四个在前，Logs 目录下其他 *.log 按名称排序，知道游戏路径时加 crash.log。</summary>
public sealed class LogSourcesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Enumerate_NoLogsFolder_ListsTheFixedFour()
    {
        IReadOnlyList<LogSource> sources = LogSources.Enumerate(new UserDataPaths(_root), gameExePath: null);

        Assert.Equal(["MapExtPDX.log", "SceneFlow.log", "Player.log", "Player-prev.log"], sources.Select(s => s.DisplayName));
    }

    [Fact]
    public void Enumerate_OtherLogsSortedWithoutDuplicatesOrSubfolders()
    {
        var paths = new UserDataPaths(_root);
        Directory.CreateDirectory(Path.Combine(paths.LogsDir, "Sub"));
        foreach (string name in new[] { "b.log", "A.log", "MapExtPDX.log", "notes.txt", Path.Combine("Sub", "deep.log") })
            File.WriteAllText(Path.Combine(paths.LogsDir, name), string.Empty);

        IReadOnlyList<LogSource> sources = LogSources.Enumerate(paths, gameExePath: null);

        Assert.Equal(["MapExtPDX.log", "SceneFlow.log", "Player.log", "Player-prev.log", "A.log", "b.log"], sources.Select(s => s.DisplayName));
        Assert.Equal(Path.Combine(paths.LogsDir, "A.log"), sources[4].Path);
    }

    [Fact]
    public void Enumerate_WithGamePath_AddsCrashLog()
    {
        string exe = Path.Combine(_root, "Game", "Cities2.exe");
        IReadOnlyList<LogSource> sources = LogSources.Enumerate(new UserDataPaths(_root), exe);

        LogSource crash = sources[^1];
        Assert.Equal(Path.Combine(_root, "Game", "logs", "crash.log"), crash.Path);
        Assert.StartsWith("crash.log", crash.DisplayName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCrashLogPath_UnknownGamePath_IsNull(string? exe)
    {
        Assert.Null(LogSources.GetCrashLogPath(exe));
    }
}
