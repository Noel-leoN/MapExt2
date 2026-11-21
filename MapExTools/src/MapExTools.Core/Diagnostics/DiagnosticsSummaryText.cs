using System.Globalization;
using System.Text;
using MapExTools.Core.Localization;

namespace MapExTools.Core.Diagnostics;

/// <summary>摘要行的醒目程度，决定界面配色与 Markdown 前缀。</summary>
public enum SummarySeverity
{
    Normal,

    /// <summary>值得留意，但不是问题（如游戏还在运行、经启动器启动、没有记录）。</summary>
    Notice,

    Warning,

    Error,
}

public sealed record SummaryLine(string Text, SummarySeverity Severity = SummarySeverity.Normal);

public sealed record SummarySection(string Title, IReadOnlyList<SummaryLine> Lines);

/// <summary>
/// 会话摘要的文字（计划 3.2）：界面的摘要栏与"复制诊断摘要"共用同一份内容，按界面语言生成。
/// 判断依据都来自 <see cref="DiagnosticsReport"/>，这里只负责措辞。
/// </summary>
public static class DiagnosticsSummaryText
{
    /// <summary>每份 Player 日志最多列出的问题条数。</summary>
    public const int MaxProblemsShown = 3;

    private const int MaxLineLength = 240;

    /// <summary>生成三段摘要：上次运行、MapExt、启动。</summary>
    /// <param name="crashLogPath">游戏目录下 <c>logs/crash.log</c> 的路径；游戏路径未知时为 null，改用泛指的说法。</param>
    public static IReadOnlyList<SummarySection> Build(DiagnosticsReport report, string language, string? crashLogPath)
    {
        ArgumentNullException.ThrowIfNull(report);

        string T(string key) => Strings.Get(language, key);
        string F(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(key), args);
        string crashLog = crashLogPath ?? T("Logs.CrashLogDefault");

        // === 上次运行 ===
        var lastRun = new List<SummaryLine>
        {
            report.LastRunGameVersion is { } version
                ? new SummaryLine(F("Logs.GameVersion", version))
                : new SummaryLine(T("Logs.GameVersionUnknown"), SummarySeverity.Notice),
        };

        if (report.Player is { } player)
        {
            PlayerLogEndState state = player.GetEndState(report.GameRunning);
            lastRun.Add(new SummaryLine(F("Logs.End", StateText(state)), SeverityOf(state, SummarySeverity.Error)));
            if (state is PlayerLogEndState.Crashed or PlayerLogEndState.NoExitRecord)
                lastRun.Add(new SummaryLine(F("Logs.CrashHint", crashLog), SummarySeverity.Error));

            if (player.ProblemCount > 0)
            {
                lastRun.Add(new SummaryLine(F("Logs.Problems", player.ProblemCount), SummarySeverity.Warning));
                foreach (PlayerLogProblem problem in player.RecentProblems.TakeLast(MaxProblemsShown))
                    lastRun.Add(new SummaryLine(Shorten(problem.FirstFrame == null ? problem.Text : problem.Text + " — " + problem.FirstFrame)));
            }
        }
        else if (report.PlayerLogFile.ReadError is { } playerError)
        {
            lastRun.Add(new SummaryLine(F("Logs.StatusError", "Player.log", playerError), SummarySeverity.Warning));
        }

        if (report.PlayerPrev is { } prev)
        {
            PlayerLogEndState state = prev.GetEndState(sessionInProgress: false);
            lastRun.Add(new SummaryLine(F("Logs.PrevEnd", StateText(state)), SeverityOf(state, SummarySeverity.Warning)));
        }

        // === MapExt ===
        var mapExtLines = new List<SummaryLine>();
        string mapExtTitle = "MapExt";
        if (report.MapExt is { } m)
        {
            if (m.ModVersion != null)
                mapExtTitle = F("Logs.Section.MapExtVersion", m.ModVersion);

            mapExtLines.Add(m.AppliedConfig is { } applied
                ? new SummaryLine(F("Logs.Applied", applied.Raw))
                : new SummaryLine(T("Logs.AppliedMissing"), SummarySeverity.Warning));

            mapExtLines.Add(m.BurstEnabled switch
            {
                true => new SummaryLine(T("Logs.Burst.On")),
                false => new SummaryLine(T("Logs.Burst.Off"), SummarySeverity.Warning),
                null => new SummaryLine(T("Logs.Burst.Unknown"), SummarySeverity.Notice),
            });

            if (m.Conflicts.Detected.Count > 0)
            {
                string sep = language == UiLanguage.English ? "; " : "；";
                mapExtLines.Add(new SummaryLine(F("Logs.Conflicts.Found", string.Join(sep, m.Conflicts.Detected)), SummarySeverity.Warning));
            }
            else if (m.Conflicts.NoneDetected)
                mapExtLines.Add(new SummaryLine(T("Logs.Conflicts.None")));
            else
                mapExtLines.Add(new SummaryLine(T("Logs.Conflicts.NotScanned"), SummarySeverity.Notice));

            IReadOnlyList<string> autoDisabled = m.AppliedConfig?.AutoDisabled ?? [];
            if (autoDisabled.Count > 0)
                mapExtLines.Add(new SummaryLine(F("Logs.Conflicts.AutoDisabled", string.Join(", ", autoDisabled)), SummarySeverity.Warning));

            if (m.LastGateDecision is { } gate)
            {
                string text = gate.FollowUps.Count == 0 ? gate.Text : gate.Text + " → " + string.Join(" → ", gate.FollowUps);
                mapExtLines.Add(new SummaryLine(Shorten(F("Logs.Gate", text))));
            }
            else
            {
                mapExtLines.Add(new SummaryLine(T("Logs.GateNone")));
            }

            SummarySeverity counts = m.ErrorCount > 0 ? SummarySeverity.Error
                : m.WarningCount > 0 ? SummarySeverity.Notice
                : SummarySeverity.Normal;
            mapExtLines.Add(new SummaryLine(F("Logs.Counts", m.WarningCount, m.ErrorCount), counts));
        }
        else if (report.MapExtLogFile.ReadError is { } mapExtError)
        {
            mapExtLines.Add(new SummaryLine(F("Logs.StatusError", "MapExtPDX.log", mapExtError), SummarySeverity.Warning));
        }
        else
        {
            mapExtLines.Add(new SummaryLine(T("Logs.MapExtMissing"), SummarySeverity.Warning));
        }

        // === 启动 ===
        var launch = new List<SummaryLine>();
        if (report.SceneFlow is { } s)
        {
            string[] arguments = s.Arguments.Select(DisplayArgument).ToArray();
            launch.Add(arguments.Length > 0
                ? new SummaryLine(F("Logs.Arguments", string.Join(" ", arguments)))
                : new SummaryLine(T("Logs.ArgumentsNone")));
            if (s.ViaParadoxLauncher)
                launch.Add(new SummaryLine(T("Logs.Launcher"), SummarySeverity.Notice));
            if (s.ModsRegistrationTimeout != null)
                launch.Add(new SummaryLine(T("Logs.ModsTimeout"), SummarySeverity.Warning));
        }
        else if (report.SceneFlowLogFile.ReadError is { } sceneFlowError)
        {
            launch.Add(new SummaryLine(F("Logs.StatusError", "SceneFlow.log", sceneFlowError), SummarySeverity.Warning));
        }
        else
        {
            launch.Add(new SummaryLine(T("Logs.SceneFlowMissing"), SummarySeverity.Notice));
        }

        return
        [
            new SummarySection(T("Logs.Section.LastRun"), lastRun),
            new SummarySection(mapExtTitle, mapExtLines),
            new SummarySection(T("Logs.Section.Launch"), launch),
        ];

        string StateText(PlayerLogEndState state) => T("Logs.State." + state);
    }

    /// <summary>
    /// 生成可粘贴到问题反馈里的 Markdown。用户目录换成 <c>%USERPROFILE%</c>，免得把 Windows 用户名带出去。
    /// </summary>
    public static string ToMarkdown(DiagnosticsReport report, IReadOnlyList<SummarySection> sections, string language, string? userProfile)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(sections);

        // 先换掉用户目录再转义：用户名里的下划线转义后就匹配不上了
        string Clean(string text) => Escape(HideUserProfile(text, userProfile));

        var sb = new StringBuilder();
        sb.Append("## ").AppendLine(Strings.Get(language, "Logs.Title"));
        sb.AppendLine();
        sb.AppendLine(Clean(string.Format(CultureInfo.InvariantCulture, Strings.Get(language, "Logs.Generated"),
            report.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))));

        foreach (SummarySection section in sections)
        {
            sb.AppendLine();
            sb.Append("### ").AppendLine(Clean(section.Title));
            sb.AppendLine();
            foreach (SummaryLine line in section.Lines)
            {
                string prefix = line.Severity switch
                {
                    SummarySeverity.Error => "❌ ",
                    SummarySeverity.Warning => "⚠️ ",
                    _ => string.Empty,
                };
                sb.Append("- ").Append(prefix).AppendLine(Clean(line.Text));
            }
        }

        return sb.ToString();
    }

    /// <summary>参数的显示形式：被打码的写成 <c>键=***</c>，启动器路径只留文件名。</summary>
    private static string DisplayArgument(CommandLineArgument argument)
    {
        if (argument.IsMasked)
            return argument.Text.Contains('=') ? argument.Key + "=***" : "***";
        if (argument.Text.EndsWith("dowser.exe", StringComparison.OrdinalIgnoreCase))
            return Path.GetFileName(argument.Text);
        return argument.Text;
    }

    private static SummarySeverity SeverityOf(PlayerLogEndState state, SummarySeverity bad) => state switch
    {
        PlayerLogEndState.Crashed or PlayerLogEndState.NoExitRecord => bad,
        PlayerLogEndState.InProgress => SummarySeverity.Notice,
        _ => SummarySeverity.Normal,
    };

    private static string Shorten(string text) =>
        text.Length <= MaxLineLength ? text : text[..MaxLineLength] + "…";

    /// <summary>转义会改变 Markdown 排版的字符（反斜杠、星号、下划线、反引号、尖括号、方括号、竖线）。</summary>
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is '\\' or '*' or '_' or '`' or '<' or '>' or '[' or ']' or '|')
                sb.Append('\\');
            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string HideUserProfile(string text, string? userProfile)
    {
        if (string.IsNullOrWhiteSpace(userProfile))
            return text;

        string trimmed = userProfile.TrimEnd('\\', '/');
        string forward = trimmed.Replace('\\', '/');
        string escapedBackslash = trimmed.Replace("\\", "\\\\", StringComparison.Ordinal);

        // 先替换更长的双反斜杠形式，再替换普通与正斜杠形式，最后处理 URL 编码形式（如 file:/// URI 中含空格/中文用户名）
        text = text.Replace(escapedBackslash, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        text = text.Replace(trimmed, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        text = text.Replace(forward, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);

        string[] segments = forward.Split('/', StringSplitOptions.None);
        for (int i = 0; i < segments.Length; i++)
        {
            // 保留 Windows 盘符段（如 "C:"）不转义冒号，其余路径段按 URI 百分号编码
            if (i == 0 && segments[i].Length == 2 && segments[i][1] == ':')
                continue;
            segments[i] = Uri.EscapeDataString(segments[i]);
        }

        string urlForward = string.Join('/', segments);
        if (!string.Equals(urlForward, forward, StringComparison.OrdinalIgnoreCase))
        {
            text = text.Replace(urlForward, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(urlForward.Replace('/', '\\'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }
}
