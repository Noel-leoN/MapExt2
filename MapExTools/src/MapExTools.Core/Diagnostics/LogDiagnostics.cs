using System.Text;
using MapExTools.Core.Paths;

namespace MapExTools.Core.Diagnostics;

/// <summary>一个日志文件的读取情况。</summary>
/// <param name="ReadError">文件存在但读不了时的错误信息。</param>
public sealed record LogFileStatus(string Path, bool Exists, long Length, DateTime? LastWriteTime, string? ReadError)
{
    public bool IsReadable => Exists && ReadError == null;
}

/// <summary>
/// 日志诊断的会话摘要：<c>MapExtPDX.log</c>、<c>SceneFlow.log</c>、<c>Player.log</c> 与 <c>Player-prev.log</c>。
/// 文件不存在或读不了时，对应的摘要为 null。
/// </summary>
/// <param name="GameRunning">生成摘要时游戏是否在运行；决定 Player.log 的结束状态能不能下结论。</param>
public sealed record DiagnosticsReport(
    DateTime GeneratedAt,
    bool GameRunning,
    LogFileStatus MapExtLogFile,
    MapExtLogSummary? MapExt,
    LogFileStatus SceneFlowLogFile,
    SceneFlowSummary? SceneFlow,
    LogFileStatus PlayerLogFile,
    PlayerLogSummary? Player,
    LogFileStatus PlayerPrevLogFile,
    PlayerLogSummary? PlayerPrev)
{
    /// <summary>上次运行的游戏版本：优先取 Player.log，其次 SceneFlow.log。</summary>
    public string? LastRunGameVersion => Player?.GameVersion ?? SceneFlow?.GameVersion;

    /// <summary>Player.log 对应那次运行的结束状态；文件不存在时为 null。</summary>
    public PlayerLogEndState? PlayerEndState => Player?.GetEndState(GameRunning);

    /// <summary>Player-prev.log 对应的那次运行早已结束。</summary>
    public PlayerLogEndState? PlayerPrevEndState => PlayerPrev?.GetEndState(sessionInProgress: false);
}

/// <summary>读取日志并生成会话摘要（计划 3.1）。</summary>
public static class LogDiagnostics
{
    /// <summary>
    /// 按行读取日志。用 <c>FileShare.ReadWrite | FileShare.Delete</c> 打开，游戏运行中、日志还在写入时也能读；
    /// 按 UTF-8 解码（有 BOM 时按 BOM），无效字节换成替换字符。
    /// </summary>
    public static IReadOnlyList<string> ReadLines(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
            lines.Add(line);
        return lines;
    }

    /// <summary>读取四个日志并生成摘要。</summary>
    public static DiagnosticsReport Collect(UserDataPaths paths, bool gameRunning)
    {
        ArgumentNullException.ThrowIfNull(paths);

        (LogFileStatus mapExtFile, MapExtLogSummary? mapExt) = Summarize(paths.MapExtLog, MapExtLogSummary.Parse);
        (LogFileStatus sceneFlowFile, SceneFlowSummary? sceneFlow) = Summarize(paths.SceneFlowLog, SceneFlowSummary.Parse);
        (LogFileStatus playerFile, PlayerLogSummary? player) = Summarize(paths.PlayerLog, PlayerLogSummary.Parse);
        (LogFileStatus playerPrevFile, PlayerLogSummary? playerPrev) = Summarize(paths.PlayerPrevLog, PlayerLogSummary.Parse);

        return new DiagnosticsReport(
            DateTime.Now, gameRunning,
            mapExtFile, mapExt,
            sceneFlowFile, sceneFlow,
            playerFile, player,
            playerPrevFile, playerPrev);
    }

    private static (LogFileStatus File, T? Summary) Summarize<T>(string path, Func<IReadOnlyList<string>, T> parse)
        where T : class
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            return (new LogFileStatus(path, false, 0, null, null), null);

        try
        {
            T summary = parse(ReadLines(path));
            info.Refresh();
            return (new LogFileStatus(path, true, info.Length, info.LastWriteTime, null), summary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (new LogFileStatus(path, true, info.Length, info.LastWriteTime, ex.Message), null);
        }
    }
}
