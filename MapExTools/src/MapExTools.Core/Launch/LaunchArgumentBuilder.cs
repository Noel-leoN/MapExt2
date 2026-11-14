namespace MapExTools.Core.Launch;

/// <summary>
/// 按 <see cref="LaunchOptions"/> 拼启动参数（计划 2.2）。参数名与游戏 <c>GameManager.ParseOptions</c> 一致，
/// <c>--burst-disable-compilation</c> 是 Unity Burst 读的进程参数。勾选项的顺序固定，自定义参数附在最后。
/// </summary>
public static class LaunchArgumentBuilder
{
    /// <summary>"默认"：不传 <c>--logsEffectiveness=</c>。</summary>
    public const string DefaultLogLevel = "DEFAULT";

    public const string BurstDisableArgument = "--burst-disable-compilation";

    /// <summary>
    /// 日志级别下拉框的选项。除 <see cref="DefaultLogLevel"/> 外都是 <c>Level.GetLevel</c> 认识的名称；
    /// 它遇到不认识的字符串返回 Disabled，会关掉全部日志，所以只能从这里选，不能自由输入。
    /// </summary>
    public static IReadOnlyList<string> LogLevels { get; } =
    [
        DefaultLogLevel, "ALL", "VERBOSE", "TRACE", "DEBUG", "INFO", "WARN", "ERROR", "CRITICAL", "FATAL", "EMERGENCY", "DISABLED",
    ];

    /// <summary>
    /// 换成列表里的写法（不分大小写，与游戏一致）；空、<see cref="DefaultLogLevel"/> 或不认识时返回 null，即不传参。
    /// </summary>
    public static string? NormalizeLogLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return null;

        string? match = LogLevels.FirstOrDefault(l => string.Equals(l, level.Trim(), StringComparison.OrdinalIgnoreCase));
        return match == null || match == DefaultLogLevel ? null : match;
    }

    /// <summary>勾选项对应的参数，不含自定义参数。</summary>
    /// <param name="includeBurst">走 runOnce.txt 时为 false：Burst 读的是进程命令行，写进文件无效。</param>
    public static IReadOnlyList<string> BuildOptionArguments(LaunchOptions options, bool includeBurst = true)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = new List<string>();
        if (options.DisableUserSection)
            args.Add("--disableUserSection");

        // --disableModding 同时禁用代码 Mod，两个都勾时只传它
        if (options.DisableModding)
            args.Add("--disableModding");
        else if (options.DisableCodeModding)
            args.Add("--disableCodeModding");

        if (options.NoSplash)
            args.Add("--noSplash");
        if (options.DeveloperMode)
            args.Add("--developerMode");
        if (options.UiDeveloperMode)
            args.Add("--uiDeveloperMode");
        if (NormalizeLogLevel(options.LogLevel) is { } level)
            args.Add("--logsEffectiveness=" + level);
        if (options.DuplicateLogToDefault)
            args.Add("--duplicateLogToDefault");
        if (includeBurst && options.DisableBurstCompilation)
            args.Add(BurstDisableArgument);
        return args;
    }

    /// <summary>
    /// 直启与 <c>-applaunch</c> 的参数串：勾选项加上原样附在后面的自定义参数（只把换行、制表符换成空格）。
    /// </summary>
    public static string BuildCommandLine(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parts = new List<string>(BuildOptionArguments(options));
        string custom = NormalizeCustom(options.CustomArguments);
        if (custom.Length > 0)
            parts.Add(custom);
        return string.Join(' ', parts);
    }

    /// <summary>
    /// <c>runOnce.txt</c> 的内容：游戏读出全文后只按空格拆分并去重（<c>GameManager.MergeAdditionalCommandLineArguments</c>），
    /// 换行会粘在参数上让它失效，所以单个空格分隔、不带结尾换行；不含 Burst 参数。没有参数时为空串。
    /// </summary>
    public static string BuildRunOnceText(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IEnumerable<string> custom = NormalizeCustom(options.CustomArguments).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', BuildOptionArguments(options, includeBurst: false).Concat(custom));
    }

    /// <summary>自定义参数里有引号：runOnce.txt 只按空格拆分，引号会原样进参数。</summary>
    public static bool CustomHasQuotes(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.CustomArguments?.Contains('"') == true;
    }

    /// <summary>去掉首尾空白，换行与制表符换成空格。</summary>
    public static string NormalizeCustom(string? custom)
    {
        if (string.IsNullOrWhiteSpace(custom))
            return string.Empty;

        return custom.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
    }
}
