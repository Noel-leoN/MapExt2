using System.Globalization;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Core.CommandLine;

/// <summary>命令行参数错误的种类。</summary>
public enum CommandLineErrorKind
{
    /// <summary>不认识的选项或多出来的参数。</summary>
    Unknown,

    /// <summary>选项缺少取值。</summary>
    MissingValue,

    /// <summary>取值无效。</summary>
    InvalidValue,

    /// <summary>开关类选项带了取值（如 <c>--show=1</c>）。</summary>
    UnexpectedValue,

    /// <summary>同一选项出现多次。</summary>
    Duplicate,
}

/// <summary>命令行参数有误。界面与命令行用 <see cref="GetMessage"/> 取当前语言的文字。</summary>
public sealed class CommandLineException : Exception
{
    public CommandLineException(CommandLineErrorKind kind, string option, string? value = null)
        : base(BuildMessage(UiLanguage.English, kind, option, value))
    {
        Kind = kind;
        Option = option;
        Value = value;
    }

    public CommandLineErrorKind Kind { get; }

    public string Option { get; }

    public string? Value { get; }

    public string GetMessage(string language) => BuildMessage(language, Kind, Option, Value);

    private static string BuildMessage(string language, CommandLineErrorKind kind, string option, string? value) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, "Cli.Err." + kind), option, value ?? string.Empty);
}

/// <summary>一个 MapExt 启动设定对应的命令行选项，如 <c>--demand</c> 对应 <c>EnableDemandEcoSystem</c>。</summary>
public sealed record SettingOption(string Name, StartupKeyInfo Key);

/// <summary>解析后的命令行。</summary>
public sealed class CommandLineOptions
{
    public bool Help { get; init; }

    public bool Show { get; init; }

    public bool Reset { get; init; }

    public bool Launch { get; init; }

    public bool CheckConflicts { get; init; }

    /// <summary><c>--lang</c>，已换成规范的语言代码。</summary>
    public string? Language { get; init; }

    /// <summary>测试用 <c>--user-data</c>：游戏用户数据目录。</summary>
    public string? UserData { get; init; }

    /// <summary>测试用 <c>--game-process</c>：检测游戏是否在运行所用的进程名。</summary>
    public string? GameProcess { get; init; }

    /// <summary>要写入的设定：键名 → 取值（模式为模式名，开关为 bool）。</summary>
    public IReadOnlyDictionary<string, object> Settings { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// 是否在命令行里完成、不开窗口。只带 <c>--lang</c>、<c>--user-data</c>、<c>--game-process</c> 时照常打开界面。
    /// </summary>
    public bool IsCommandMode => Help || Show || Reset || Launch || CheckConflicts || Settings.Count > 0;
}

/// <summary>
/// 解析命令行（计划 2.2）。选项名不分大小写，取值可以写在下一个参数里，也可以写成 <c>--mode=ModeB</c>。
/// 设定类选项对应 <c>MapExtPDX.coc</c> 的 9 个受管键，取值规则沿用开发版 <c>Set-MapExtStartup.ps1</c>：
/// 模式为 <c>ModeA</c>／<c>ModeB</c>／<c>ModeC</c>／<c>ModeD</c>／<c>None</c>，开关为 <c>on</c>／<c>off</c>。
/// </summary>
public static class CommandLineParser
{
    // 设定选项名与 .coc 键名的对照。StartupKeys 增删键时这里要同步，单元测试会检查覆盖全部键
    private static readonly (string Option, string Key)[] s_settingNames =
    [
        ("--mode", StartupKeys.ModeKey),
        ("--economy-fix", "isEnableEconomyFix"),
        ("--demand", "EnableDemandEcoSystem"),
        ("--job-search", "EnableJobSearchEcoSystem"),
        ("--household-property", "EnableHouseholdPropertyEcoSystem"),
        ("--resource-buyer", "EnableResourceBuyerEcoSystem"),
        ("--resident-ai", "EnableResidentAIEcoSystem"),
        ("--downstream-ai", "EnableDownstreamAIEcoSystem"),
        ("--disable-load-game-validation", "DisableLoadGameValidation"),
    ];

    /// <summary>设定类选项，顺序同 <see cref="StartupKeys.Keys"/>。</summary>
    public static IReadOnlyList<SettingOption> SettingOptions { get; } = s_settingNames
        .Select(n => new SettingOption(n.Option, StartupKeys.FindKey(n.Key)
                                                 ?? throw new InvalidOperationException($"StartupKeys 里没有 {n.Key}")))
        .OrderBy(o => Array.IndexOf(StartupKeys.Keys, o.Key))
        .ToList();

    /// <exception cref="CommandLineException">参数有误。</exception>
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        bool help = false, show = false, reset = false, launch = false, checkConflicts = false;
        string? language = null, userData = null, gameProcess = null;
        var settings = new Dictionary<string, object>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (arg is "-h" or "-?" or "/?")
            {
                help = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length == 2)
                throw new CommandLineException(CommandLineErrorKind.Unknown, arg);

            int eq = arg.IndexOf('=');
            string written = eq < 0 ? arg : arg[..eq];
            string? inline = eq < 0 ? null : arg[(eq + 1)..];
            string name = written.ToLowerInvariant();

            SettingOption? setting = SettingOptions.FirstOrDefault(o => o.Name == name);
            bool isFlag = name is "--help" or "--show" or "--reset" or "--launch" or "--check-conflicts";
            if (setting == null && !isFlag && name is not ("--lang" or "--user-data" or "--game-process"))
                throw new CommandLineException(CommandLineErrorKind.Unknown, written);
            if (!seen.Add(name))
                throw new CommandLineException(CommandLineErrorKind.Duplicate, written);

            if (isFlag)
            {
                if (inline != null)
                    throw new CommandLineException(CommandLineErrorKind.UnexpectedValue, written);
                switch (name)
                {
                    case "--help":
                        help = true;
                        break;
                    case "--show":
                        show = true;
                        break;
                    case "--reset":
                        reset = true;
                        break;
                    case "--check-conflicts":
                        checkConflicts = true;
                        break;
                    default:
                        launch = true;
                        break;
                }

                continue;
            }

            string value = inline ?? TakeNext(args, ref i, written);
            switch (name)
            {
                case "--lang":
                    language = NormalizeLanguage(value) ?? throw Invalid(written, value);
                    break;
                case "--user-data":
                    userData = string.IsNullOrWhiteSpace(value) ? throw Invalid(written, value) : value;
                    break;
                case "--game-process":
                    gameProcess = NormalizeProcessName(value) ?? throw Invalid(written, value);
                    break;
                default:
                    settings[setting!.Key.Name] = ParseSettingValue(setting, written, value);
                    break;
            }
        }

        return new CommandLineOptions
        {
            Help = help,
            Show = show,
            Reset = reset,
            Launch = launch,
            CheckConflicts = checkConflicts,
            Language = language,
            UserData = userData,
            GameProcess = gameProcess,
            Settings = settings,
        };
    }

    /// <summary>
    /// 参数解析失败时，从原始参数里找出能认的 <c>--lang</c> 与 <c>--user-data</c>，好用玩家要的语言报错。
    /// 都没有时为 null。
    /// </summary>
    public static (string? Language, string? UserData) PeekOptions(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? language = null, userData = null;
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            int eq = arg.IndexOf('=');
            string name = (eq < 0 ? arg : arg[..eq]).ToLowerInvariant();
            if (name is not ("--lang" or "--user-data"))
                continue;

            string? value;
            if (eq >= 0)
            {
                value = arg[(eq + 1)..];
            }
            else if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            else
            {
                value = null;
            }

            if (name == "--lang")
                language ??= NormalizeLanguage(value);
            else if (!string.IsNullOrWhiteSpace(value))
                userData ??= value;
        }

        return (language, userData);
    }

    /// <summary>
    /// <c>--lang</c> 的取值：<c>en-US</c>、<c>zh-HANS</c>、<c>zh-HANT</c>（不分大小写），也认 <c>en</c>、<c>zh</c>、
    /// <c>zh-CN</c>、<c>zh-TW</c> 等常见写法；其余返回 null。
    /// </summary>
    public static string? NormalizeLanguage(string? value)
    {
        if (UiLanguage.Normalize(value) is { } known)
            return known;

        return value?.Trim().ToLowerInvariant() switch
        {
            "en" => UiLanguage.English,
            "zh" or "zh-cn" or "zh-sg" or "zh-hans" => UiLanguage.SimplifiedChinese,
            "zh-tw" or "zh-hk" or "zh-mo" or "zh-hant" => UiLanguage.TraditionalChinese,
            _ => null,
        };
    }

    private static object ParseSettingValue(SettingOption option, string written, string value)
    {
        string text = value.Trim();
        if (option.Key.Kind == StartupKeyKind.Mode)
        {
            // 模式名不分大小写，换成游戏认的写法（游戏解析枚举名区分大小写）
            return StartupKeys.ModeNames.FirstOrDefault(m => string.Equals(m, text, StringComparison.OrdinalIgnoreCase))
                   ?? throw Invalid(written, value);
        }

        return text.ToLowerInvariant() switch
        {
            "on" => true,
            "off" => false,
            _ => throw Invalid(written, value),
        };
    }

    /// <summary>进程名去掉 <c>.exe</c>；空的或带路径时返回 null。</summary>
    private static string? NormalizeProcessName(string value)
    {
        string name = value.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name.Length == 0 || name.IndexOfAny(['\\', '/', ':']) >= 0 ? null : name;
    }

    /// <summary>取下一个参数作为取值；没有，或下一个是另一个选项时报缺少取值。</summary>
    private static string TakeNext(IReadOnlyList<string> args, ref int i, string option)
    {
        if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new CommandLineException(CommandLineErrorKind.MissingValue, option);
        return args[++i];
    }

    private static CommandLineException Invalid(string option, string value) =>
        new(CommandLineErrorKind.InvalidValue, option, value);
}
