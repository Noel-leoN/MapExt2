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
/// 执行命令行（计划 2.2）：按选项改 <c>MapExtPDX.coc</c>、显示状态、播放集、模组列表与冲突报告、启动游戏。流程沿用开发版：
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

    /// <param name="output">正常结果：已写入、状态、播放集、模组列表、冲突报告、已启动。</param>
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

        // 仅当写设定、--show、--check-conflicts 或 --launch 时严格要求 MapExtPDX.coc 合法；
        // 纯 --list-playsets 不读 .coc，纯 --list-mods 在 .coc 损坏时降级跳过冲突定级仍正常输出列表
        bool strictCoc = target.Count > 0 || options.Reset || options.Show || options.CheckConflicts || options.Launch;
        var manager = new CocSettingsManager(_context.Paths.CocFile, _context.IsGameRunning);
        StartupSettingsState? state = null;

        if (strictCoc)
        {
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
        }
        else if (options.ListMods)
        {
            try
            {
                state = manager.Read();
            }
            catch (CocFormatException ex)
            {
                ErrorLine("Cli.Mods.CocSkippedWarning", ex.GetMessage(_language));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorLine("Cli.Mods.CocSkippedWarning", ex.Message);
            }
        }

        // 若显式指定了 --playset，先校验该目标播放集是否存在且无重名歧义；目标无效时不启动游戏直接返回错误码
        string? resolvedTargetPlaysetId = null;
        if (options.Playset != null)
        {
            if (!TryResolveTargetPlayset(options.Playset, out resolvedTargetPlaysetId, out bool isTargetActive))
                return ExitCodes.Error;

            if (options.Launch && !isTargetActive)
                ErrorLine("Cli.Playset.ReadOnlyLaunchNote", options.Playset);
        }

        // 按 D5 约定：同时带诊断选项与 --launch 时先输出诊断再启动；诊断异常不拦启动，退出码仍记为出错
        bool diagnosticsFailed = false;
        try
        {
            if (options.Show)
                WriteStatus(state!);

            if (options.ListPlaysets && !WritePlaysets())
                diagnosticsFailed = true;

            if (options.CheckConflicts || options.ListMods)
            {
                GameLocateResult location = _context.LocateGame();
                ScannedModWorkspace ws = ConflictAnalyzer.ScanWorkspace(_context.Paths, location.Game?.GameDir, resolvedTargetPlaysetId);
                if (!string.IsNullOrEmpty(ws.Playset.PlaysetConfigError))
                {
                    ErrorLine("Conflicts.PlaysetReadError", ws.Playset.PlaysetConfigError);
                    diagnosticsFailed = true;
                }

                ConflictReport? report = null;
                ConflictEvalSettings? evalSettings = null;
                if (state != null)
                {
                    evalSettings = ConflictEvalSettings.FromDictionary(state.GetAllValues());
                    report = ConflictAnalyzer.Evaluate(ws, evalSettings);
                }

                if (options.CheckConflicts && report != null && evalSettings != null)
                    WriteConflicts(report, evalSettings);

                if (options.ListMods)
                    WriteModList(ws, report);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ErrorLine("Cli.Error", ex.Message);
            diagnosticsFailed = true;
        }

        int code = options.Launch ? Launch() : ExitCodes.Success;
        return diagnosticsFailed ? ExitCodes.Error : code;
    }

    private bool TryResolveTargetPlayset(string query, out string? resolvedId, out bool isActive)
    {
        resolvedId = null;
        isActive = false;

        PlaysetCatalogResult catalog = PlaysetModScanner.ListPlaysets(_context.Paths);
        if (!string.IsNullOrEmpty(catalog.ConfigError))
        {
            ErrorLine("Conflicts.PlaysetReadError", catalog.ConfigError);
            return false;
        }

        if (!catalog.ConfigExists || catalog.Playsets.Count == 0)
        {
            ErrorLine("Cli.Playset.NotFound", query, string.Empty);
            return false;
        }

        PlaysetLookupResult lookup = PlaysetModScanner.ResolvePlayset(catalog.Playsets, query);
        switch (lookup.Kind)
        {
            case PlaysetLookupKind.Matched when lookup.Match != null:
                resolvedId = lookup.Match.Id;
                isActive = lookup.Match.IsActive;
                return true;

            case PlaysetLookupKind.Ambiguous:
            {
                string candidates = string.Join(
                    ", ",
                    lookup.Candidates.Select(p => $"{p.Name} (ID: {p.Id})"));
                ErrorLine("Cli.Playset.Ambiguous", query, candidates);
                return false;
            }

            default:
            {
                string available = string.Join(
                    ", ",
                    catalog.Playsets.Select(p => $"{p.Name} ({p.Id})"));
                string hint = Format("Cli.Playset.AvailableHint", available);
                ErrorLine("Cli.Playset.NotFound", query, hint);
                return false;
            }
        }
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

    // === 状态、播放集与模组清单 ===

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

    private bool WritePlaysets()
    {
        PlaysetCatalogResult catalog = PlaysetModScanner.ListPlaysets(_context.Paths);
        if (!string.IsNullOrEmpty(catalog.ConfigError))
        {
            ErrorLine("Conflicts.PlaysetReadError", catalog.ConfigError);
            return false;
        }

        if (!catalog.ConfigExists)
        {
            Line("Cli.Playsets.NoConfig", _context.Paths.PlaysetConfigFile);
            return true;
        }

        if (catalog.Playsets.Count == 0)
        {
            Line("Cli.Playsets.Empty");
            return true;
        }

        Line("Cli.Playsets.Header", catalog.Playsets.Count);
        string activeTag = Strings.Get(_language, "Cli.Playsets.ActiveTag");
        string offdiskTag = Strings.Get(_language, "Cli.Playsets.OffdiskTag");

        foreach (PlaysetSummary p in catalog.Playsets)
        {
            string backend = !string.IsNullOrWhiteSpace(p.BackendPlaysetId)
                ? Format("Cli.Playsets.BackendId", p.BackendPlaysetId)
                : string.Empty;
            Line(
                "Cli.Playsets.Row",
                p.Name,
                p.IsActive ? activeTag : string.Empty,
                p.IsOffdisk ? offdiskTag : string.Empty,
                p.Id,
                backend,
                p.EnabledModCount,
                p.TotalModCount);
        }

        return true;
    }

    private void WriteConflicts(ConflictReport report, ConflictEvalSettings evalSettings)
    {
        // 与模组与冲突预检页一致：摘要句以评估模式开头，另一类模式结果不同时补一行，再写扫描概况与提示
        _output.WriteLine(ConflictText.StatusBanner(report, _language).Value);
        if (ConflictText.CrossModeSummary(report, _language) is { } crossMode)
            _output.WriteLine(crossMode);
        _output.WriteLine(ConflictText.HeaderSummary(report, _language));
        if (ConflictText.OffdiskNote(report.Workspace.Playset, _language) is { } offdiskNote)
            _output.WriteLine(offdiskNote);
        if (ConflictText.MapExtMissingNote(report.Workspace.Playset, _language) is { } missingMapExt)
            _output.WriteLine(missingMapExt);

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

    private void WriteModList(ScannedModWorkspace ws, ConflictReport? report)
    {
        PlaysetScanResult playset = ws.Playset;
        IReadOnlyList<PlaysetModListItem> items = playset.EffectiveAllModItems;
        string name = playset.EffectivePlaysetName ?? Strings.Get(_language, "Conflicts.NoActivePlayset");
        int enabledCount = items.Count(m => m.IsEnabled);

        Line("Cli.Mods.Header", name, items.Count, enabledCount, ws.EnabledCodeModCount);
        if (items.Count == 0)
        {
            Line("Conflicts.Mods.Empty");
            return;
        }

        string localOrder = Strings.Get(_language, "Conflicts.Mods.OrderLocal");
        foreach (PlaysetModListItem m in items)
        {
            string orderTag = m.LoadOrder.HasValue
                ? "#" + m.LoadOrder.Value.ToString(CultureInfo.InvariantCulture)
                : localOrder;
            string ver = ConflictText.ModItemVersionLabel(m, _language);
            string verPart = string.IsNullOrEmpty(ver) ? string.Empty : " " + ver;
            string src = ConflictText.SourceLabel(m.Source, m.PdxModId, _language);
            string loadState = ConflictText.LoadStateLabel(m.LoadState, _language);

            string conflictPart = string.Empty;
            if (report != null && m.IsEnabled)
            {
                ConflictSeverity sev = report.GetPackageSeverity(m.PackageKey);
                if (sev != ConflictSeverity.None)
                    conflictPart = $" · [{ConflictText.SeverityBadge(sev, _language)}]";
            }

            _output.WriteLine($"  [{orderTag}] {m.DisplayName}{verPart} · {src} · {loadState}{conflictPart}");
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
