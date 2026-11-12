using System.Text;
using MapExTools.Core.Diagnostics;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Diagnostics;

/// <summary>按用户数据目录收集四个日志：文件缺失、正在被写入、结束状态与上次运行版本。</summary>
public sealed class LogDiagnosticsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private UserDataPaths Paths => new(_root);

    private static void CopySample(string sample, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(LogSamples.PathOf(sample), target, overwrite: true);
    }

    [Fact]
    public void Collect_NoLogs_ReportsMissingFiles()
    {
        DiagnosticsReport r = LogDiagnostics.Collect(Paths, gameRunning: false);

        Assert.False(r.MapExtLogFile.Exists);
        Assert.Null(r.MapExt);
        Assert.Null(r.SceneFlow);
        Assert.Null(r.Player);
        Assert.Null(r.PlayerPrev);
        Assert.Null(r.LastRunGameVersion);
        Assert.Null(r.PlayerEndState);
    }

    [Fact]
    public void Collect_AllLogs_SummarizesEach()
    {
        CopySample("MapExtPDX-v4.12.txt", Paths.MapExtLog);
        CopySample("SceneFlow.txt", Paths.SceneFlowLog);
        CopySample("Player-crash.txt", Paths.PlayerLog);
        File.WriteAllText(Paths.PlayerPrevLog, "Game version: 1.6.3f1 (1064.3599) [6354.19879]\nMemory Statistics:\n");

        DiagnosticsReport r = LogDiagnostics.Collect(Paths, gameRunning: false);

        Assert.True(r.MapExtLogFile.IsReadable);
        Assert.True(r.MapExtLogFile.Length > 0);
        Assert.NotNull(r.MapExtLogFile.LastWriteTime);
        Assert.Equal("4.12.0", r.MapExt?.ModVersion);
        Assert.NotNull(r.SceneFlow?.ModsRegistrationTimeout);
        Assert.Equal("1.6.4f1 (1000.0001) [6400.20000]", r.LastRunGameVersion);
        Assert.Equal(PlayerLogEndState.Crashed, r.PlayerEndState);
        Assert.Equal(PlayerLogEndState.ExitedNormally, r.PlayerPrevEndState);
    }

    [Fact]
    public void Collect_VersionFallsBackToSceneFlow()
    {
        CopySample("SceneFlow.txt", Paths.SceneFlowLog);

        DiagnosticsReport r = LogDiagnostics.Collect(Paths, gameRunning: false);

        Assert.Equal("1.6.4f1 (1000.0001) [6400.20000] Windows Steamworks", r.LastRunGameVersion);
    }

    [Fact]
    public void Collect_GameRunning_PlayerLogInProgress()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Paths.PlayerLog, "Game version: 1.6.4f1 (1000.0001) [6400.20000]\n");

        Assert.Equal(PlayerLogEndState.InProgress, LogDiagnostics.Collect(Paths, gameRunning: true).PlayerEndState);
        Assert.Equal(PlayerLogEndState.NoExitRecord, LogDiagnostics.Collect(Paths, gameRunning: false).PlayerEndState);
    }

    [Fact]
    public void ReadLines_WhileAnotherProcessIsWriting()
    {
        CopySample("MapExtPDX-v4.12.txt", Paths.MapExtLog);

        // 游戏运行时日志一直开着写入：以写方式打开并只允许共享读写
        using var writer = new FileStream(Paths.MapExtLog, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        writer.Write(Encoding.UTF8.GetBytes("[2026-11-12 20:31:00,000] [INFO]  [MapExt|Mod] 🔹 写入中\n"));
        writer.Flush();

        IReadOnlyList<string> lines = LogDiagnostics.ReadLines(Paths.MapExtLog);
        Assert.EndsWith("写入中", lines[^1], StringComparison.Ordinal);

        DiagnosticsReport r = LogDiagnostics.Collect(Paths, gameRunning: true);
        Assert.True(r.MapExtLogFile.IsReadable);
        Assert.Equal("2026-11-12 20:31:00,000", r.MapExt?.LastTimestamp);
    }

    [Fact]
    public void ReadLines_DecodesUtf8WithAndWithoutBom()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.MapExtLog)!);
        File.WriteAllText(Paths.MapExtLog, "第一行\r\n第二行\n", new UTF8Encoding(true));

        Assert.Equal(["第一行", "第二行"], LogDiagnostics.ReadLines(Paths.MapExtLog));
    }
}
