using System.Text.RegularExpressions;

namespace MapExTools.Core.Diagnostics;

/// <summary>
/// 游戏日志器（<c>Colossal.Logging.UnityLogger</c>）写出的一条记录。首行为 <c>[时间] {上下文} [级别]  消息</c>，
/// 上下文可省略；其后不带时间戳的行（多行消息、异常堆栈）归入 <see cref="Continuation"/>。
/// </summary>
/// <param name="LineNumber">首行的行号，从 1 起。</param>
/// <param name="Timestamp">时间戳原文，如 <c>2026-11-09 23:49:56,320</c>。</param>
public sealed record LogEntry(
    int LineNumber,
    string Timestamp,
    string? Context,
    string Level,
    string Message,
    IReadOnlyList<string> Continuation)
{
    public bool IsWarning => Level == "WARN";

    /// <summary>ERROR 及以上（CRITICAL、FATAL、EMERGENCY）。</summary>
    public bool IsError => Level is "ERROR" or "CRITICAL" or "FATAL" or "EMERGENCY";

    /// <summary>首行消息与后续行，按原样以换行连接。</summary>
    public string FullText => Continuation.Count == 0 ? Message : Message + "\n" + string.Join("\n", Continuation);
}

/// <summary>按游戏日志器的格式把日志文本切成记录（<c>UnityLogger.Internal_WriteStream</c>）。</summary>
public static partial class ColossalLog
{
    // 级别之后的空白是对齐用的，不算消息内容
    [GeneratedRegex(@"^\[(?<time>[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2},[0-9]{3})\] (?:\{(?<ctx>[^}]*)\} )?\[(?<level>[A-Z]+)\] *(?<msg>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderRegex();

    /// <summary>切出全部记录；第一条记录之前的行不属于任何记录，忽略。</summary>
    public static IReadOnlyList<LogEntry> Parse(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var entries = new List<LogEntry>();
        Match? header = null;
        int headerLine = 0;
        var continuation = new List<string>();

        for (int i = 0; i < lines.Count; i++)
        {
            Match match = HeaderRegex().Match(lines[i]);
            if (match.Success)
            {
                Flush();
                header = match;
                headerLine = i + 1;
            }
            else if (header != null)
            {
                continuation.Add(lines[i]);
            }
        }

        Flush();
        return entries;

        void Flush()
        {
            if (header == null)
                return;

            Group context = header.Groups["ctx"];
            entries.Add(new LogEntry(
                headerLine,
                header.Groups["time"].Value,
                context.Success ? context.Value : null,
                header.Groups["level"].Value,
                header.Groups["msg"].Value,
                continuation.ToArray()));
            continuation.Clear();
        }
    }
}
