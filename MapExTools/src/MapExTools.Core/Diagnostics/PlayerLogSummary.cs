using System.Text.RegularExpressions;

namespace MapExTools.Core.Diagnostics;

/// <summary>Player.log 记录的这一次运行是怎么结束的。</summary>
public enum PlayerLogEndState
{
    /// <summary>以 <c>Memory Statistics:</c> 收尾，正常退出。</summary>
    ExitedNormally,

    /// <summary>有 Unity 的 <c>Crash!!!</c> 标记。</summary>
    Crashed,

    /// <summary>游戏还在运行，这一次运行还没结束。</summary>
    InProgress,

    /// <summary>没有正常退出的记录，可能闪退了：去看游戏目录下的 <c>logs/crash.log</c>。</summary>
    NoExitRecord,
}

/// <summary>Player.log 里的问题记录的来源。</summary>
public enum PlayerLogProblemKind
{
    /// <summary>Unity 写出的未捕获异常：行首就是 <c>异常类型: 消息</c>，其后是 <c>at</c> 开头的堆栈。</summary>
    Exception,

    /// <summary>游戏各日志器转写进来的 ERROR 及以上记录：<c>[日志器名] [级别]  消息</c>。</summary>
    LoggedError,
}

/// <param name="LineNumber">首行的行号，从 1 起。</param>
/// <param name="Source">异常类型，或日志器名。</param>
/// <param name="Text">首行原文。</param>
/// <param name="FirstFrame">紧随其后的第一行堆栈（<c>at …</c>）；没有时为 null。</param>
public sealed record PlayerLogProblem(int LineNumber, PlayerLogProblemKind Kind, string Source, string Text, string? FirstFrame);

/// <summary>
/// <c>Player.log</c> 或 <c>Player-prev.log</c> 的摘要。游戏每次启动把上一次的 Player.log 改名为 Player-prev.log。
/// 游戏版本是这个文件对应的那次运行写下的：游戏更新后第一次启动之前，它还是旧版本。
/// </summary>
/// <param name="GameVersion"><c>Game version:</c> 之后的版本号（<c>GameManager</c> 用 <c>Debug.Log</c> 写出）。</param>
/// <param name="ProblemCount">问题记录的总数。</param>
/// <param name="RecentProblems">最后几条问题记录。</param>
/// <param name="HasExitRecord">有正常退出时写出的 <c>Memory Statistics:</c>。</param>
/// <param name="HasCrashMarker">有 Unity 的 <c>Crash!!!</c> 标记。</param>
public sealed partial record PlayerLogSummary(
    string? GameVersion,
    int ProblemCount,
    IReadOnlyList<PlayerLogProblem> RecentProblems,
    bool HasExitRecord,
    bool HasCrashMarker)
{
    /// <summary>摘要里保留的问题记录条数。</summary>
    public const int MaxRecentProblems = 20;

    // 未捕获异常的首行：异常类型（可带命名空间）后接冒号与消息，顶格
    [GeneratedRegex(@"^(?<type>(?:[A-Za-z_][A-Za-z0-9_`]*\.)*[A-Za-z_][A-Za-z0-9_`]*Exception)(?:: .*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ExceptionRegex();

    // 转写进来的日志器记录：[日志器名] [级别]  消息（CustomLogHandler 的格式）
    [GeneratedRegex(@"^\[(?<logger>[^\]]+)\] \[(?:ERROR|CRITICAL|FATAL|EMERGENCY)\]", RegexOptions.CultureInvariant)]
    private static partial Regex LoggedErrorRegex();

    /// <summary>
    /// 结束状态。<paramref name="sessionInProgress"/>：这份日志对应的运行还没结束
    /// （读 Player.log 时游戏正在运行；Player-prev.log 总是传 false）。
    /// </summary>
    public PlayerLogEndState GetEndState(bool sessionInProgress) =>
        HasCrashMarker ? PlayerLogEndState.Crashed
        : HasExitRecord ? PlayerLogEndState.ExitedNormally
        : sessionInProgress ? PlayerLogEndState.InProgress
        : PlayerLogEndState.NoExitRecord;

    /// <summary>单遍流式解析日志（支持直接传入流式行序列，无需把整份日志载入 <see cref="List{T}"/>）。</summary>
    public static PlayerLogSummary Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        const int lookahead = 5;
        string? version = null;
        int count = 0;
        var recent = new Queue<PlayerLogProblem>();
        bool exitRecord = false, crashMarker = false;

        // 窗口先装前 5 行；每轮取出当前行后补进 1 行，判定当前行时窗口里恰好是其后 5 行
        using IEnumerator<string> enumerator = lines.GetEnumerator();
        var window = new Queue<string>(lookahead);
        while (window.Count < lookahead && enumerator.MoveNext())
            window.Enqueue(enumerator.Current);

        int lineNumber = 0;
        while (window.Count > 0)
        {
            string line = window.Dequeue();
            lineNumber++;
            if (enumerator.MoveNext())
                window.Enqueue(enumerator.Current);

            if (line.StartsWith("Game version: ", StringComparison.Ordinal))
            {
                version = line["Game version: ".Length..].Trim();
                continue;
            }

            if (line.StartsWith("Memory Statistics:", StringComparison.Ordinal))
            {
                exitRecord = true;
                continue;
            }

            if (line.Trim() == "Crash!!!")
            {
                crashMarker = true;
                continue;
            }

            PlayerLogProblem? problem = null;
            Match exception = ExceptionRegex().Match(line);
            if (exception.Success)
            {
                problem = new PlayerLogProblem(lineNumber, PlayerLogProblemKind.Exception, exception.Groups["type"].Value, line, FindFirstFrame(window));
            }
            else
            {
                Match logged = LoggedErrorRegex().Match(line);
                if (logged.Success)
                    problem = new PlayerLogProblem(lineNumber, PlayerLogProblemKind.LoggedError, logged.Groups["logger"].Value, line, FindFirstFrame(window));
            }

            if (problem == null)
                continue;

            count++;
            recent.Enqueue(problem);
            if (recent.Count > MaxRecentProblems)
                recent.Dequeue();
        }

        return new PlayerLogSummary(version, count, recent.ToArray(), exitRecord, crashMarker);
    }

    /// <summary>在紧随其后的最多 5 行前瞻窗口里找第一行 <c>at …</c> 堆栈。</summary>
    private static string? FindFirstFrame(Queue<string> lookaheadWindow)
    {
        foreach (string next in lookaheadWindow)
        {
            string trimmed = next.TrimStart();
            if (trimmed.StartsWith("at ", StringComparison.Ordinal))
                return trimmed.TrimEnd();
        }

        return null;
    }
}
