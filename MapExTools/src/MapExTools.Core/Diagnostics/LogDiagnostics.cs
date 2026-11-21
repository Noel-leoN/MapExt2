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
    /// <summary>Player.log 超过该大小时只扫开头提取版本并跳至末尾窗口流式解析（默认 64 MB）。</summary>
    internal const long MaxPlayerLogFullReadBytes = 64L * 1024 * 1024;

    private const int PlayerLogHeadScanLines = 2048;

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

    /// <summary>
    /// 流式读取 <c>Player.log</c>：文件不超过阈值时逐行从头读到尾；超过阈值时先读开头前段提取
    /// <c>Game version:</c>，再跳至末尾窗口并跳过半截边界行流式读到末尾，避免大日志占用数百 MB 堆内存。
    /// </summary>
    internal static IEnumerable<string> StreamPlayerLogLines(string path, long maxFullReadBytes = MaxPlayerLogFullReadBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length <= maxFullReadBytes)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            while (reader.ReadLine() is { } line)
                yield return line;
            yield break;
        }

        // 超大文件先在开头前段寻找 Game version: 行
        string? headerVersionLine = null;
        using (var headReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true))
        {
            for (int i = 0; i < PlayerLogHeadScanLines && headReader.ReadLine() is { } headLine; i++)
            {
                if (headLine.StartsWith("Game version: ", StringComparison.Ordinal))
                {
                    headerVersionLine = headLine;
                    break;
                }
            }
        }

        if (headerVersionLine != null)
            yield return headerVersionLine;

        // 跳至末尾窗口并丢弃第一段可能从多字节或行中截断的半截行
        stream.Position = Math.Max(0, stream.Length - maxFullReadBytes);
        SkipToNextNewline(stream);

        using var tailReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        while (tailReader.ReadLine() is { } line)
            yield return line;
    }

    private static void SkipToNextNewline(FileStream stream)
    {
        byte[] buf = new byte[8192];
        long startPos = stream.Position;
        int read;
        while ((read = stream.Read(buf, 0, buf.Length)) > 0)
        {
            int nl = Array.IndexOf(buf, (byte)'\n', 0, read);
            if (nl >= 0)
            {
                stream.Position = startPos + nl + 1;
                return;
            }

            startPos += read;
        }
    }

    /// <summary>读取四个日志并生成摘要。</summary>
    public static DiagnosticsReport Collect(UserDataPaths paths, bool gameRunning)
    {
        ArgumentNullException.ThrowIfNull(paths);

        (LogFileStatus mapExtFile, MapExtLogSummary? mapExt) = Summarize(paths.MapExtLog, MapExtLogSummary.Parse);
        (LogFileStatus sceneFlowFile, SceneFlowSummary? sceneFlow) = Summarize(paths.SceneFlowLog, SceneFlowSummary.Parse);
        (LogFileStatus playerFile, PlayerLogSummary? player) = SummarizeStream(paths.PlayerLog, p => StreamPlayerLogLines(p), PlayerLogSummary.Parse);
        (LogFileStatus playerPrevFile, PlayerLogSummary? playerPrev) = SummarizeStream(paths.PlayerPrevLog, p => StreamPlayerLogLines(p), PlayerLogSummary.Parse);

        return new DiagnosticsReport(
            DateTime.Now, gameRunning,
            mapExtFile, mapExt,
            sceneFlowFile, sceneFlow,
            playerFile, player,
            playerPrevFile, playerPrev);
    }

    internal static (LogFileStatus File, T? Summary) Summarize<T>(string path, Func<IReadOnlyList<string>, T> parse)
        where T : class =>
        SummarizeCore(path, p => parse(ReadLines(p)));

    internal static (LogFileStatus File, T? Summary) SummarizeStream<T>(
        string path,
        Func<string, IEnumerable<string>> lineSource,
        Func<IEnumerable<string>, T> parse)
        where T : class =>
        SummarizeCore(path, p => parse(lineSource(p)));

    private static (LogFileStatus File, T? Summary) SummarizeCore<T>(string path, Func<string, T> readAndParse)
        where T : class
    {
        var info = new FileInfo(path);
        if (!info.Exists)
            return (new LogFileStatus(path, false, 0, null, null), null);

        long cachedLength;
        DateTime? cachedWriteTime;
        try
        {
            cachedLength = info.Length;
            cachedWriteTime = info.LastWriteTime;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (new LogFileStatus(path, false, 0, null, null), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (new LogFileStatus(path, true, 0, null, ex.Message), null);
        }

        try
        {
            T summary = readAndParse(path);
            long finalLength = cachedLength;
            DateTime? finalWriteTime = cachedWriteTime;
            try
            {
                info.Refresh();
                if (info.Exists)
                {
                    finalLength = info.Length;
                    finalWriteTime = info.LastWriteTime;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 读完日志到 Refresh 之间文件若恰被轮转删除，沿用先前缓存的文件信息
            }

            return (new LogFileStatus(path, true, finalLength, finalWriteTime, null), summary);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (new LogFileStatus(path, false, 0, null, null), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (new LogFileStatus(path, true, cachedLength, cachedWriteTime, ex.Message), null);
        }
    }
}
