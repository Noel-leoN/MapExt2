using System.Text.Json;

namespace MapExTools.Core.Diagnostics;

/// <summary>
/// <c>Command line:</c> 里的一个参数。游戏写日志前会打码（<c>GameManager.MaskArguments</c>）：带 <c>=</c> 的参数
/// 去掉前导横线、值换成等长星号；几个敏感键之后的那一项整个换成星号。
/// </summary>
public sealed record CommandLineArgument(string Text)
{
    /// <summary>值被游戏打码，只能看到键名。</summary>
    public bool IsMasked
    {
        get
        {
            // 带 = 的参数一律打码（空值打码后仍为空）；不带 = 的只有全是星号时才是被打码的敏感值
            int eq = Text.IndexOf('=', StringComparison.Ordinal);
            if (eq >= 0)
                return !Text.AsSpan(eq + 1).ContainsAnyExcept('*');
            return Text.Length > 0 && !Text.AsSpan().ContainsAnyExcept('*');
        }
    }

    /// <summary>键名：带 <c>=</c> 的参数取等号之前（已去掉前导横线），其余为参数原文。</summary>
    public string Key
    {
        get
        {
            int eq = Text.IndexOf('=', StringComparison.Ordinal);
            return eq >= 0 ? Text[..eq] : Text;
        }
    }
}

/// <summary>
/// <c>{UserData}/Logs/SceneFlow.log</c> 的会话摘要（游戏 <c>SceneFlow</c> 日志器，每个参数一行，不进 Player.log）。
/// </summary>
/// <param name="ExecutablePath">命令行的第一项，即游戏程序路径。</param>
/// <param name="Arguments">其余各项；经 <c>runOnce.txt</c> 追加的参数也在其中。</param>
/// <param name="Configuration">
/// <c>Configuration:</c> 一行的 JSON 顶层各项（字符串取值本身，其余取 JSON 原文）。带值参数的值只能在这里看到，
/// 但 <c>logsEffectiveness</c> 不在其中。解析失败时为 null。
/// </param>
/// <param name="GameVersion">启动信息块里的 <c>Game version:</c>，含平台。</param>
/// <param name="ModsRegistrationTimeout">
/// "Mods registration did not start within 60s, continuing"：读档时订阅的 Mod 可能还没加载（1.6.4f 起）。
/// </param>
/// <param name="GameManagerDestroyed">记有 <c>GameManager destroyed</c>，即游戏正常走完退出流程。</param>
public sealed record SceneFlowSummary(
    string? CommandLineTimestamp,
    string? ExecutablePath,
    IReadOnlyList<CommandLineArgument> Arguments,
    IReadOnlyDictionary<string, string>? Configuration,
    string? GameVersion,
    LogEntry? ModsRegistrationTimeout,
    bool GameManagerDestroyed)
{
    /// <summary>由 Paradox 启动器启动：启动器会把自己的路径（<c>Launcher\dowser.exe</c>）作为参数传给游戏。</summary>
    public bool ViaParadoxLauncher =>
        Arguments.Any(a => a.Text.EndsWith("dowser.exe", StringComparison.OrdinalIgnoreCase));

    /// <summary>解析日志全文（按行）。同一项出现多次时取最后一次。</summary>
    public static SceneFlowSummary Parse(IReadOnlyList<string> lines)
    {
        string? timestamp = null;
        string? executable = null;
        IReadOnlyList<CommandLineArgument> arguments = [];
        IReadOnlyDictionary<string, string>? configuration = null;
        string? version = null;
        LogEntry? timeout = null;
        bool destroyed = false;

        foreach (LogEntry entry in ColossalLog.Parse(lines))
        {
            string message = entry.Message;
            if (message.StartsWith("Command line: ", StringComparison.Ordinal))
            {
                timestamp = entry.Timestamp;
                executable = message["Command line: ".Length..];
                arguments = entry.Continuation.Where(l => l.Length > 0).Select(l => new CommandLineArgument(l)).ToArray();
            }
            else if (message.StartsWith("Configuration: ", StringComparison.Ordinal))
            {
                configuration = ParseConfiguration(entry.FullText["Configuration: ".Length..]);
            }
            else if (entry.IsWarning && message.StartsWith("Mods registration did not start within", StringComparison.Ordinal))
            {
                timeout = entry;
            }
            else if (message.StartsWith("GameManager destroyed", StringComparison.Ordinal))
            {
                destroyed = true;
            }

            foreach (string line in entry.Continuation)
            {
                if (line.StartsWith("Game version: ", StringComparison.Ordinal))
                    version = line["Game version: ".Length..].Trim();
            }
        }

        return new SceneFlowSummary(timestamp, executable, arguments, configuration, version, timeout, destroyed);
    }

    private static IReadOnlyDictionary<string, string>? ParseConfiguration(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                values[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText();
            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
