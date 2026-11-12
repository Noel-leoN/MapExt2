using System.Text.RegularExpressions;

namespace MapExTools.Core.Diagnostics;

/// <summary>日志行的级别，用于着色与筛选。</summary>
public enum LogLevelKind
{
    /// <summary>没有级别（Unity 的普通输出）。</summary>
    None,

    /// <summary>TRACE、VERBOSE、DEBUG。</summary>
    Debug,

    Info,

    /// <summary>WARN。</summary>
    Warning,

    /// <summary>ERROR、CRITICAL、FATAL、EMERGENCY，以及未捕获异常。</summary>
    Error,
}

/// <summary>日志浏览器里的一行。</summary>
/// <param name="Number">行号，从 1 起。</param>
public sealed record LogLine(int Number, string Text, LogLevelKind Level);

/// <summary>
/// 逐行判定级别，要按顺序把整份日志喂进来。游戏日志器格式（<c>[时间] [级别]</c>）里不带时间戳的行都是上一条的后续
/// （多行消息、异常堆栈），沿用上一条的级别；Player.log 是 Unity 的输出，只有缩进的堆栈行沿用异常或错误的级别。
/// </summary>
public sealed partial class LogLineClassifier
{
    private LogLevelKind _current = LogLevelKind.None;
    private bool _colossalFormat;

    [GeneratedRegex(@"^\[[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9:,]{12}\] (?:\{[^}]*\} )?\[(?<level>[A-Z]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex ColossalHeaderRegex();

    // Player.log 里转写进来的日志器记录：[日志器名] [级别]  消息
    [GeneratedRegex(@"^\[[^\]]+\] \[(?<level>[A-Z]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex LoggerHeaderRegex();

    [GeneratedRegex(@"^(?:[A-Za-z_][A-Za-z0-9_`]*\.)*[A-Za-z_][A-Za-z0-9_`]*Exception(?:: .*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex ExceptionRegex();

    public LogLevelKind Classify(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Match colossal = ColossalHeaderRegex().Match(line);
        if (colossal.Success)
        {
            _colossalFormat = true;
            return _current = MapLevel(colossal.Groups["level"].Value);
        }

        // 游戏日志器格式里，不带时间戳的行都是上一条记录的后续
        if (_colossalFormat)
            return _current;

        Match logger = LoggerHeaderRegex().Match(line);
        if (logger.Success)
            return _current = MapLevel(logger.Groups["level"].Value);

        if (ExceptionRegex().IsMatch(line))
            return _current = LogLevelKind.Error;

        // Unity 格式：缩进的堆栈行沿用上一条，其余行没有级别
        if (line.Length > 0 && char.IsWhiteSpace(line[0]))
            return _current;

        return _current = LogLevelKind.None;
    }

    private static LogLevelKind MapLevel(string level) => level switch
    {
        "TRACE" or "VERBOSE" or "DEBUG" => LogLevelKind.Debug,
        "INFO" => LogLevelKind.Info,
        "WARN" or "WARNING" => LogLevelKind.Warning,
        "ERROR" or "CRITICAL" or "FATAL" or "EMERGENCY" or "EXCEPTION" => LogLevelKind.Error,
        _ => LogLevelKind.None,
    };
}
