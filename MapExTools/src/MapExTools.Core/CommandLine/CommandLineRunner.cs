using System.ComponentModel;
using System.Globalization;
using MapExTools.Core.Coc;
using MapExTools.Core.Config;
using MapExTools.Core.Diagnostics;
using MapExTools.Core.Launch;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;

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
/// 执行命令行（计划 2.2）：按选项改 <c>MapExtPDX.coc</c>、启动游戏、显示状态。流程沿用开发版：
/// 要写入或启动时先查游戏是否在运行，在运行就什么都不做、返回 2；写入只改有改动的键（2.1 的
/// <see cref="CocSettingsManager"/>）；最后按需显示状态。逻辑都在这里，界面程序只负责接上控制台。
/// </summary>
public sealed class CommandLineRunner
{
    private readonly CommandLineContext _context;
    private readonly TextWriter _output;
    private readonly string _language;

    public CommandLineRunner(CommandLineContext context, TextWriter output, string language)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(output);

        _context = context;
        _output = output;
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
            Line("Cli.GameRunning");
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
                        Line("Cli.GameRunning");
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
            Line("Cli.FileError", ex.GetMessage(_language));
            if (options.Launch)
                Line("Cli.LaunchRefusedCoc");
            return ExitCodes.Error;
        }
        catch (CocSaveException ex)
        {
            Line("Cli.Error", ex.GetMessage(_language));
            return ExitCodes.Error;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Line("Cli.Error", ex.Message);
            return ExitCodes.Error;
        }

        int code = options.Launch ? Launch() : ExitCodes.Success;
        if (options.Show)
            WriteStatus(state);
        return code;
    }

    // === 启动 ===

    private int Launch()
    {
        GameLocateResult location = _context.LocateGame();
        LaunchOptions options = _context.Config.Launch;
        LaunchChannel channel = options.Channel;

        if (location.MissingManualPath != null)
            Line("Launch.ManualMissing", location.MissingManualPath);

        // 直启与 -applaunch 时游戏同样会读 runOnce.txt；命令行不询问，照常启动并说明
        if (channel != LaunchChannel.RunOnce && RunOnceFile.ReadPending(_context.Launcher.RunOncePath) is { } pending)
            Line("Cli.RunOnceKept", pending);

        // 直启 Steam 版要 Steam 在后台运行；没运行时按通道顺序改走 -applaunch（它会先打开 Steam）
        if (_context.Launcher.Check(location, channel) == LaunchBlock.SteamNotRunning && location.SteamChannelsAvailable)
        {
            Line("Cli.SteamFallback");
            channel = LaunchChannel.Steam;
        }

        try
        {
            LaunchResult result = _context.Launcher.Launch(location, options, channel);
            if (result.Block != LaunchBlock.None)
            {
                Line("Cli.NotLaunched", LaunchText.Block(result.Block, _language));
                return result.Block == LaunchBlock.GameRunning ? ExitCodes.GameRunning : ExitCodes.Error;
            }

            Line("Cli.Launched", LaunchText.ChannelShort(channel, _language),
                LaunchText.Command(channel, result.Request!.Arguments, _language));
            return ExitCodes.Success;
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Line("Launch.Failed", ex.Message);
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

    private string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(_language, key), args);
}
