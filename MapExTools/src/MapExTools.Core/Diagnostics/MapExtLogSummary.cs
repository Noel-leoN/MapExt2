using System.Globalization;
using System.Text.RegularExpressions;

namespace MapExTools.Core.Diagnostics;

/// <summary>MapExt 的"本会话生效配置"一行（<c>Mod.cs</c>，<c>AppliedConfig.ToString</c>）。</summary>
/// <param name="Raw">冒号之后的原文，如 <c>Mode=ModeA, CV=4, EcoFix=True, … AutoDisabled=[]</c>。</param>
/// <param name="Values">按 <c>键=值</c> 拆开的各项；<c>AutoDisabled</c> 的值保留方括号。</param>
public sealed record AppliedConfigInfo(string Timestamp, string Raw, IReadOnlyDictionary<string, string> Values)
{
    public string? Mode => Values.GetValueOrDefault("Mode");

    public int? CoreValue =>
        int.TryParse(Values.GetValueOrDefault("CV"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cv) ? cv : null;

    /// <summary>因检测到冲突 Mod 而在本会话自动停用的经济分组。</summary>
    public IReadOnlyList<string> AutoDisabled
    {
        get
        {
            string? list = Values.GetValueOrDefault("AutoDisabled");
            if (list == null || list.Length < 2)
                return [];
            return list[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}

/// <summary><c>ModConflictDetector</c> 的扫描结果。</summary>
/// <param name="Scanned">日志里有扫描记录。</param>
/// <param name="NoneDetected">记有"未检测到已知冲突 Mod"。</param>
/// <param name="Detected">"检测到冲突 Mod"与"检测到潜在冲突 Mod"各行的原文。</param>
/// <param name="AutoDisabled">自动停用经济分组的各行原文。</param>
public sealed record ConflictInfo(bool Scanned, bool NoneDetected, IReadOnlyList<string> Detected, IReadOnlyList<string> AutoDisabled);

/// <summary>读档判定针对的对象。</summary>
public enum GateSubject
{
    /// <summary>读存档（读档、继续、快速读档、汇合点）。</summary>
    Save,

    /// <summary>用地图开新城。</summary>
    Map,

    /// <summary>转换请求绑定的存档或地图，直接放行。</summary>
    ConversionRequest,
}

/// <summary>
/// <c>SaveEntryGate</c> 的一次读档判定：判定行本身与其后同类日志（拦下、放行、弹转换确认、玩家的选择等）。
/// </summary>
/// <param name="Entry">入口：MenuLoad、MenuContinue、MenuNewGame、QuickLoad、Convergence。</param>
/// <param name="RawId">日志里的资产 ID，带数据源前缀，如 <c>User/&lt;guid&gt;</c>。</param>
/// <param name="AssetId">去掉数据源前缀后的 guid，可与包内 <c>.cid</c> 对照。</param>
/// <param name="SaveDescription">存档的模式标记，如 <c>CV=4</c> 或 <c>无 MapExt 标记（原版）</c>。</param>
/// <param name="Result">存档的判定结果：Success、ModNotUsed、CoreValueMismatch、LegacyMismatch。</param>
/// <param name="InferenceTrusted">地图面积推断是否可信。</param>
public sealed record GateDecisionInfo(
    string Timestamp,
    string Entry,
    GateSubject Subject,
    string Name,
    string? RawId,
    string? AssetId,
    string Text,
    string? SaveDescription,
    string? Result,
    int? CurrentCoreValue,
    int? InferredCoreValue,
    bool? InferenceTrusted,
    IReadOnlyList<string> FollowUps);

/// <summary>
/// <c>MapExtPDX.log</c> 的会话摘要。日志每次启动游戏时重写，反映的是最近一次启动。
/// 这里匹配的日志措辞由 Mod 输出，改 Mod 的这几行日志时要同步改这里。
/// </summary>
/// <param name="ModVersion">"OnLoad, version:" 之后的版本号。</param>
/// <param name="BurstEnabled">本会话 Burst 是否启用；null 表示日志里没有这一行（Mod 早于 v4.12.0，或加载中途出错）。</param>
/// <param name="RecentProblems">最后几条 WARN 及以上的记录。</param>
public sealed partial record MapExtLogSummary(
    string? ModVersion,
    string? FirstTimestamp,
    string? LastTimestamp,
    AppliedConfigInfo? AppliedConfig,
    bool? BurstEnabled,
    ConflictInfo Conflicts,
    GateDecisionInfo? LastGateDecision,
    int WarningCount,
    int ErrorCount,
    IReadOnlyList<LogEntry> RecentProblems)
{
    /// <summary>摘要里保留的问题记录条数。</summary>
    public const int MaxRecentProblems = 10;

    private const string ConflictTag = "ModConflictDetector";
    private const string GateTag = "SaveEntryGate";

    /// <summary>解析日志全文（按行）。</summary>
    public static MapExtLogSummary Parse(IReadOnlyList<string> lines)
    {
        IReadOnlyList<LogEntry> entries = ColossalLog.Parse(lines);

        string? version = null;
        AppliedConfigInfo? applied = null;
        bool? burst = null;
        bool scanned = false, noneDetected = false;
        var detected = new List<string>();
        var autoDisabled = new List<string>();
        GateDecisionInfo? decision = null;
        var followUps = new List<string>();
        int warnings = 0, errors = 0;
        var problems = new Queue<LogEntry>();

        foreach (LogEntry entry in entries)
        {
            if (entry.IsWarning || entry.IsError)
            {
                if (entry.IsWarning)
                    warnings++;
                else
                    errors++;
                problems.Enqueue(entry);
                if (problems.Count > MaxRecentProblems)
                    problems.Dequeue();
            }

            Match line = ModLogLineRegex().Match(entry.Message);
            if (!line.Success)
                continue;

            string tag = line.Groups["tag"].Value;
            string text = line.Groups["text"].Value;

            if (text.StartsWith("OnLoad, version:", StringComparison.Ordinal))
            {
                version = text["OnLoad, version:".Length..].Trim();
            }
            else if (text.StartsWith("本会话生效配置: ", StringComparison.Ordinal))
            {
                applied = ParseAppliedConfig(entry.Timestamp, text["本会话生效配置: ".Length..]);
            }
            else if (text.StartsWith("本会话 Burst: ", StringComparison.Ordinal))
            {
                burst = text.StartsWith("本会话 Burst: 已启用", StringComparison.Ordinal) ? true
                    : text.StartsWith("本会话 Burst: 已停用", StringComparison.Ordinal) ? false
                    : null;
            }
            else if (tag == ConflictTag)
            {
                scanned = true;
                if (text == "未检测到已知冲突 Mod")
                    noneDetected = true;
                else if (text.StartsWith("检测到冲突 Mod", StringComparison.Ordinal) || text.StartsWith("检测到潜在冲突 Mod", StringComparison.Ordinal))
                    detected.Add(text);
                else if (text.StartsWith("Auto-disabled ", StringComparison.Ordinal))
                    autoDisabled.Add(text);
            }
            else if (tag == GateTag)
            {
                GateDecisionInfo? next = ParseGateDecision(entry.Timestamp, text);
                if (next != null)
                {
                    decision = next;
                    followUps = [];
                }
                else if (decision != null)
                {
                    followUps.Add(text);
                }
            }
        }

        return new MapExtLogSummary(
            version,
            entries.Count > 0 ? entries[0].Timestamp : null,
            entries.Count > 0 ? entries[^1].Timestamp : null,
            applied,
            burst,
            new ConflictInfo(scanned, noneDetected, detected, autoDisabled),
            decision == null ? null : decision with { FollowUps = followUps },
            warnings,
            errors,
            problems.ToArray());
    }

    // ModLog 的统一格式：[MapExt|类名] 图标 消息（图标是一个 Emoji，可能带变体选择符）
    [GeneratedRegex(@"^\[MapExt\|(?<tag>[^\]]+)\] \S+ ?(?<text>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ModLogLineRegex();

    // AppliedConfig.ToString：键=值，以逗号分隔；AutoDisabled 的值是方括号括起的列表，里面也有逗号
    [GeneratedRegex(@"(?<key>\w+)=(?<value>\[[^\]]*\]|[^,]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigPairRegex();

    // SaveEntryGate.EvaluateSave：{入口} '{存档名}'（ID {ID}）：存档 {标记}，当前 CV={n}，结果 {结果}
    [GeneratedRegex(@"^(?<entry>\w+) '(?<name>.*)'（ID (?<id>.*?)）：存档 (?<saved>.*?)，当前 CV=(?<cv>-?[0-9]+)，结果 (?<result>\w+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SaveDecisionRegex();

    // SaveEntryGate.EvaluateNewGame：{入口} 地图 '{地图名}'（ID {ID}）：面积 …，比值 …，推断 CV={n}（可信|不可信，…），当前 CV={n}
    [GeneratedRegex(@"^(?<entry>\w+) 地图 '(?<name>.*)'（ID (?<id>.*?)）：面积 (?<area>[^，]*)，比值 (?<ratio>[^，]*)，推断 CV=(?<mapcv>-?[0-9]+)（(?<trust>.*)），当前 CV=(?<cv>-?[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex MapDecisionRegex();

    // 转换请求绑定的存档或地图：{入口} '{名}'：转换请求绑定的存档，放行；地图多一个"地图 "
    [GeneratedRegex(@"^(?<entry>\w+) (?:地图 )?'(?<name>.*)'：转换请求绑定的(?:存档|地图)，放行$", RegexOptions.CultureInvariant)]
    private static partial Regex ConversionDecisionRegex();

    private static AppliedConfigInfo ParseAppliedConfig(string timestamp, string raw)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match pair in ConfigPairRegex().Matches(raw))
            values[pair.Groups["key"].Value] = pair.Groups["value"].Value.Trim();
        return new AppliedConfigInfo(timestamp, raw, values);
    }

    private static GateDecisionInfo? ParseGateDecision(string timestamp, string text)
    {
        Match save = SaveDecisionRegex().Match(text);
        if (save.Success)
        {
            string id = save.Groups["id"].Value;
            return new GateDecisionInfo(
                timestamp, save.Groups["entry"].Value, GateSubject.Save, save.Groups["name"].Value, id, StripSourcePrefix(id), text,
                save.Groups["saved"].Value, save.Groups["result"].Value, ParseInt(save.Groups["cv"].Value), null, null, []);
        }

        Match map = MapDecisionRegex().Match(text);
        if (map.Success)
        {
            string id = map.Groups["id"].Value;
            return new GateDecisionInfo(
                timestamp, map.Groups["entry"].Value, GateSubject.Map, map.Groups["name"].Value, id, StripSourcePrefix(id), text,
                null, null, ParseInt(map.Groups["cv"].Value), ParseInt(map.Groups["mapcv"].Value),
                map.Groups["trust"].Value.StartsWith("可信", StringComparison.Ordinal), []);
        }

        Match conversion = ConversionDecisionRegex().Match(text);
        if (conversion.Success)
        {
            return new GateDecisionInfo(
                timestamp, conversion.Groups["entry"].Value, GateSubject.ConversionRequest, conversion.Groups["name"].Value,
                null, null, text, null, null, null, null, null, []);
        }

        return null;
    }

    /// <summary>日志里的 ID 带数据源前缀（如 <c>User/</c>），取最后一个斜杠之后的部分。</summary>
    private static string StripSourcePrefix(string id)
    {
        int slash = id.LastIndexOf('/');
        return slash >= 0 ? id[(slash + 1)..] : id;
    }

    private static int? ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : null;
}
