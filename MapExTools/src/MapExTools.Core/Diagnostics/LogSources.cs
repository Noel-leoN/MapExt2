using MapExTools.Core.Paths;

namespace MapExTools.Core.Diagnostics;

/// <summary>日志浏览器可以切换的一个日志文件。</summary>
/// <param name="DisplayName">下拉框里显示的名称（文件名；crash.log 带上所在目录）。</param>
public sealed record LogSource(string DisplayName, string Path);

/// <summary>列出可浏览的日志（计划 3.2）。</summary>
public static class LogSources
{
    /// <summary>
    /// 依次为 MapExtPDX.log、SceneFlow.log、Player.log、Player-prev.log（不存在也列出），
    /// <c>{UserData}/Logs</c> 下其他 <c>*.log</c>（按名称排序，不进子目录），
    /// 知道游戏路径时再加游戏目录下的 <c>logs/crash.log</c>。
    /// </summary>
    /// <param name="gameExePath"><c>Cities2.exe</c> 的路径；未知时为 null。</param>
    public static IReadOnlyList<LogSource> Enumerate(UserDataPaths paths, string? gameExePath)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var sources = new List<LogSource>
        {
            new("MapExtPDX.log", paths.MapExtLog),
            new("SceneFlow.log", paths.SceneFlowLog),
            new("Player.log", paths.PlayerLog),
            new("Player-prev.log", paths.PlayerPrevLog),
        };

        var listed = new HashSet<string>(sources.Select(s => s.Path), StringComparer.OrdinalIgnoreCase);
        try
        {
            IEnumerable<string> others = Directory.EnumerateFiles(paths.LogsDir, "*.log", SearchOption.TopDirectoryOnly)
                .Select(p => System.IO.Path.GetFullPath(p))
                .Where(p => !listed.Contains(p))
                .OrderBy(p => System.IO.Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);
            foreach (string path in others)
                sources.Add(new LogSource(System.IO.Path.GetFileName(path), path));
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            // Logs 目录不存在或读不了：只列固定的几个
        }

        string? crashLog = GetCrashLogPath(gameExePath);
        if (crashLog != null)
            sources.Add(new LogSource("crash.log (" + System.IO.Path.GetDirectoryName(crashLog) + ")", crashLog));

        return sources;
    }

    /// <summary>游戏目录下的 <c>logs/crash.log</c>：没有正常退出记录的闪退看这里。游戏路径未知时为 null。</summary>
    public static string? GetCrashLogPath(string? gameExePath)
    {
        if (string.IsNullOrWhiteSpace(gameExePath))
            return null;

        string? gameDir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(gameExePath));
        return gameDir == null ? null : System.IO.Path.Combine(gameDir, "logs", "crash.log");
    }
}
