using System.ComponentModel;
using System.Globalization;
using MapExTools.Core.Coc;
using MapExTools.Core.Config;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Diagnostics;
using MapExTools.Core.Launch;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.CommandLine;

/// <summary>命令行的退出码，沿用开发版 <c>Set-MapExtStartup.ps1</c>。</summary>
public static class ExitCodes
{
    public const int Success = 0;

    /// <summary>出错，含设定文件格式有误、参数有误、启动失败。</summary>
    public const int Error = 1;

    /// <summary>游戏正在运行，未做任何改动。</summary>
    public const int GameRunning = 2;
}

/// <summary>命令行执行所需的环境：界面程序用真实的注册表与进程，单元测试换成假的。</summary>
public sealed class CommandLineContext
{
    public required UserDataPaths Paths { get; init; }

    /// <summary>工具配置，<c>--launch</c> 用其中的启动方式与参数。</summary>
    public required ToolConfig Config { get; init; }

    public required Func<bool> IsGameRunning { get; init; }

    public required Func<GameLocateResult> LocateGame { get; init; }

    public required GameLauncher Launcher { get; init; }
}

/// <summary>
/// 执行命令行（计划 2.2）：按选项改 <c>MapExtPDX.coc</c>、显示状态与冲突报告、启动游戏。流程沿用开发版：
/// 要写入或启动时先查游戏是否在运行，在运行就什么都不做、返回 2；写入只改有改动的键（2.1 的
/// <see cref="CocSettingsManager"/>）；写入后按需输出状态与冲突报告，最后才启动（审计修复 D5）。
/// 正常结果写 output，错误与警告写 error。逻辑都在这里，界面程序只负责接上控制台。
/// </summary>
public sealed class CommandLineRunner
{
    private readonly CommandLineContext _context;
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly string _language;

    /// <param name="output">正常结果：已写入、状态、冲突报告、已启动。</param>
    /// <param name="error">错误与警告（游戏在运行、设定文件有误、没能启动等）；为 null 时与 <paramref name="output"/> 合用一个。</param>
    public CommandLineRunner(CommandLineContext context, TextWriter output, string language, TextWriter? error = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        _context = context;
        _output = output;
        _error = error ?? output;
        _language = UiLanguage.Normalize(language) ?? UiLanguage.English;
    }

    public int Run(CommandLineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Help)
        {
            _output.WriteLine(CommandLineText.Usage(_language));
            return ExitCodes.Success;
        }

        // 目标值：--reset 先铺默认值，单项选项覆盖其上
        var target = new Dictionary<string, object>(StringComparer.Ordinal);
        if (options.Reset)
        {
            foreach ((string key, object value) in CocSettingsManager.GetDefaultValues())
                target[key] = value;
        }

        foreach ((string key, object value) in options.Settings)
            target[key] = value;

        if ((target.Count > 0 || options.Launch) && _context.IsGameRunning())
        {
            ErrorLine("Cli.GameRunning");
            return ExitCodes.GameRunning;
        }

        var manager = new CocSettingsManager(_context.Paths.CocFile, _context.IsGameRunning);
        StartupSettingsState state;
        try
        {
            state = manager.Read();
            if (target.Count > 0)
            {
                CocSaveResult result = manager.Save(state, target);
                switch (result.Status)
                {
                    case CocSaveStatus.GameRunning:
                        ErrorLine("Cli.GameRunning");
                        return ExitCodes.GameRunning;
                    case CocSaveStatus.NoChanges:
                        Line("Startup.NoChanges");
                        break;
                    default:
                        Line("Cli.Saved", StartupSettingsText.ChangeList(result.Changes, _language));
                        state = manager.Read();
                        break;
                }
            }
        }
        catch (CocFormatException ex)
        {
            ErrorLine("Cli.FileError", ex.GetMessage(_language));
            if (options.Launch)
                ErrorLine("Cli.LaunchRefusedCoc");
            return ExitCodes.Error;
        }
        catch (CocSaveException ex)
        {
            ErrorLine("Cli.Error", ex.GetMessage(_language));
            return ExitCodes.Error;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLine("Cli.Error", ex.Message);
            return ExitCodes.Error;
        }

        // 按 D5 约定：同时带 --check-conflicts／--show 与 --launch 时先输出诊断再启动。
        // 诊断输出失败不拦启动（与调整顺序前一样照常启动），退出码仍记为出错
        bool diagnosticsFailed = false;
        try
        {
            if (options.Show)
                WriteStatus(state);
            if (options.CheckConflicts)
                WriteConflicts(state);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorLine("Cli.Error", ex.Message);
            diagnosticsFailed = true;
        }

        int code = options.Launch ? Launch() : ExitCodes.Success;
        return diagnosticsFailed ? ExitCodes.Error : code;
    }

    // === 启动 ===

    private int Launch()
    {
        GameLocateResult location = _context.LocateGame();
        LaunchOptions options = _context.Config.Launch;
        LaunchChannel channel = options.Channel;

        if (location.MissingManualPath != null)
            ErrorLine("Launch.ManualMissing", location.MissingManualPath);

        // 直启与 -applaunch 时游戏同样会读 runOnce.txt；命令行不询问，照常启动并说明
        if (channel != LaunchChannel.RunOnce && RunOnceFile.ReadPending(_context.Launcher.RunOncePath) is { } pending)
            ErrorLine("Cli.RunOnceKept", pending);

        // 直启 Steam 版要 Steam 在后台运行；没运行时按通道顺序改走 -applaunch（它会先打开 Steam）
        if (_context.Launcher.Check(location, channel) == LaunchBlock.SteamNotRunning && location.SteamChannelsAvailable)
        {
            ErrorLine("Cli.SteamFallback");
            channel = LaunchChannel.Steam;
        }

        try
        {
            LaunchResult result = _context.Launcher.Launch(location, options, channel);
            if (result.Block != LaunchBlock.None)
            {
                ErrorLine("Cli.NotLaunched", LaunchText.Block(result.Block, _language));
                return result.Block == LaunchBlock.GameRunning ? ExitCodes.GameRunning : ExitCodes.Error;
            }

            Line("Cli.Launched", LaunchText.ChannelShort(channel, _language),
                LaunchText.Command(channel, result.Request!.Arguments, _language));
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorLine("Launch.Failed", ex.Message);
            return ExitCodes.Error;
        }
    }

    // === 状态 ===

    /// <summary>当前设定、最近一次启动生效的配置、runOnce.txt、游戏位置与启动方式、游戏是否在运行。</summary>
    private void WriteStatus(StartupSettingsState state)
    {
        Line("Startup.File", _context.Paths.CocFile);
        foreach (StartupSettingValue setting in state.Settings)
        {
            string indent = setting.Key.EcoGroup ? "    " : "  ";
            string suffix = setting.InFile ? string.Empty : Strings.Get(_language, "Cli.Default");
            _output.WriteLine(indent + Format("Cli.Setting",
                StartupSettingsText.KeyLabel(setting.Key, _language),
                StartupSettingsText.Value(setting.Key, setting.Value, _language),
                suffix));
        }

        if (!state.Exists)
            Line("Startup.NoFile");
        else if (state.Block == null)
            Line("Startup.NoBlock");
        if (state.BlockCount > 1)
            Line("Startup.ExtraBlocks", state.BlockCount);
        if (StartupSettingsText.InvalidList(state, _language) is { } invalid)
            Line("Startup.Invalid", invalid);

        if (ReadApplied() is { } applied)
            Line("Startup.Applied", applied.Timestamp, applied.Raw);
        if (RunOnceFile.ReadPending(_context.Launcher.RunOncePath) is { } pending)
            Line("Launch.Status.RunOnce", pending);

        GameLocateResult location = _context.LocateGame();
        if (location.MissingManualPath != null)
            Line("Launch.ManualMissing", location.MissingManualPath);
        _output.WriteLine(LaunchText.Location(location, _language));

        LaunchOptions options = _context.Config.Launch;
        Line("Cli.LaunchMethod", LaunchText.ChannelName(options.Channel, _language));
        Line("Launch.Preview", LaunchText.Preview(options.Channel, options, _language));
        Line(_context.IsGameRunning() ? "Status.GameRunning" : "Status.GameNotRunning");
    }

    private void WriteConflicts(StartupSettingsState state)
    {
        GameLocateResult location = _context.LocateGame();
        ScannedModWorkspace ws = ConflictAnalyzer.ScanWorkspace(_context.Paths, location.Game?.GameDir);
        ConflictEvalSettings evalSettings = ConflictEvalSettings.FromDictionary(state.GetAllValues());
        ConflictReport report = ConflictAnalyzer.Evaluate(ws, evalSettings);

        // 与冲突预检页一致：摘要句以评估模式开头，另一类模式结果不同时补一行，再写扫描概况
        _output.WriteLine(ConflictText.StatusBanner(report, _language).Value);
        if (ConflictText.CrossModeSummary(report, _language) is { } crossMode)
            _output.WriteLine(crossMode);
        _output.WriteLine(ConflictText.HeaderSummary(report, _language));

        foreach (ModConflictItem item in report.Items)
        {
            _output.WriteLine($"[{ConflictText.SeverityBadge(item.Severity, _language)}] {item.DisplayName} " +
                              $"{ConflictText.VersionLabel(item, _language)} · {ConflictText.SourceLabel(item, _language)}");
            foreach (ConflictFinding finding in item.Findings)
            {
                _output.WriteLine("  - " + ConflictText.DescribeFinding(finding, evalSettings, _language));
            }
        }
    }

    private AppliedConfigInfo? ReadApplied()
    {
        string log = _context.Paths.MapExtLog;
        try
        {
            return File.Exists(log) ? MapExtLogSummary.Parse(LogDiagnostics.ReadLines(log)).AppliedConfig : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // === 输出 ===

    private void Line(string key, params object[] args) =>
        _output.WriteLine(args.Length == 0 ? Strings.Get(_language, key) : Format(key, args));

    /// <summary>错误与警告：写到错误输出（界面程序接到 stderr）。</summary>
    private void ErrorLine(string key, params object[] args) =>
        _error.WriteLine(args.Length == 0 ? Strings.Get(_language, key) : Format(key, args));

    private string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(_language, key), args);
}
