using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using MapExTools.Core.CommandLine;
using MapExTools.Core.Config;
using MapExTools.Core.Game;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;
using MapExTools.Services;

namespace MapExTools;

public partial class App : Application
{
    private const int MaxHandledUiExceptions = 3;
    private bool _handlingUnhandled;
    private int _unhandledCount;

    // 主窗口显示之前出错时，吞掉异常后不会再有窗口可关，见 OnDispatcherUnhandledException
    private bool _mainWindowShown;

    /// <summary>全局服务，在 <see cref="OnStartup"/> 里建立（命令行模式不建立）。</summary>
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 命令行（计划 2.2）：带设定、--launch、--show、--help 时在命令行里完成，不开窗口；
        // 只带 --lang、--user-data、--game-process 时照常打开界面
        CommandLineOptions options;
        try
        {
            options = CommandLineParser.Parse(e.Args);
        }
        catch (CommandLineException ex)
        {
            string lang = GuessLanguage(e.Args);
            CliConsole.Write(ex.GetMessage(lang) + Environment.NewLine + Strings.Get(lang, "Cli.HelpHint"),
                dialogWithoutConsole: true, isError: true);
            ExitWith(ExitCodes.Error);
            return;
        }

        if (options.GameProcess != null)
            GameProcess.ProcessName = options.GameProcess;

        UserDataPaths paths;
        try
        {
            paths = options.UserData is { } root ? new UserDataPaths(root) : UserDataPaths.CreateDefault();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            string lang = options.Language ?? GuessLanguage(e.Args);
            CliConsole.Write(string.Format(CultureInfo.InvariantCulture, Strings.Get(lang, "Cli.Error"), ex.Message),
                dialogWithoutConsole: true, isError: true);
            ExitWith(ExitCodes.Error);
            return;
        }

        var configService = new ToolConfigService(paths.ToolConfigFile);
        ToolConfigLoadResult load = configService.Load();
        string language = options.Language
                          ?? UiLanguage.Normalize(load.Config.Language)
                          ?? UiLanguage.FromCulture(CultureInfo.CurrentUICulture);

        if (options.IsCommandMode)
        {
            ExitWith(RunCommandLine(options, paths, load, language));
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        Services = new AppServices(paths, configService, load.Config, configReadOnly: load.Status == ToolConfigLoadStatus.ReadFailed);

        // --lang 只管本次运行，不写配置
        Loc.Instance.SetLanguage(language);
        ThemeService.Apply(load.Config.Theme);

        // 在游戏的 Mods 下运行时要求移出（计划 §2 第 9 条），不进主窗口
        InstallLocationService.Initialize(paths);
        if (!InstallLocationService.CheckGameModsAtStartup())
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        _mainWindowShown = true;

        if (load.Status == ToolConfigLoadStatus.Corrupt)
        {
            MessageBox.Show(window,
                Loc.Instance.Format("Config.Corrupt", load.Error ?? "", load.BackupPath ?? "-"),
                Loc.Instance["Config.CorruptTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (load.Status == ToolConfigLoadStatus.ReadFailed)
        {
            MessageBox.Show(window,
                Loc.Instance.Format("Config.ReadFailed", load.Error ?? ""),
                Loc.Instance["Config.ReadFailedTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (e.Exception is OutOfMemoryException || _handlingUnhandled || _unhandledCount >= MaxHandledUiExceptions)
            return;

        _handlingUnhandled = true;
        _unhandledCount++;
        try
        {
            string message = Loc.Instance.Format("App.UnhandledError", e.Exception.GetType().Name, e.Exception.Message);
            string title = Loc.Instance["App.UnhandledErrorTitle"];
            if (MainWindow is { IsLoaded: true } owner)
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

            e.Handled = true;

            // 启动途中（主窗口还没显示）出错：异常吞掉后不会再建窗口，OnMainWindowClose 永远等不到，
            // 进程会无窗口留在后台，所以说明后直接退出
            if (!_mainWindowShown)
                ExitWith(ExitCodes.Error);
        }
        catch
        {
            // 弹窗自身失败时交回系统默认处理，避免递归死循环
        }
        finally
        {
            _handlingUnhandled = false;
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }

    /// <summary>命令行模式：逻辑在 Core 的 <see cref="CommandLineRunner"/>，这里接上真实环境与控制台。</summary>
    private static int RunCommandLine(CommandLineOptions options, UserDataPaths paths, ToolConfigLoadResult load, string language)
    {
        // 正常结果写 stdout，错误与警告写 stderr（配置读不了也算警告）
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        if (load.Status == ToolConfigLoadStatus.Corrupt)
        {
            error.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.Get(language, "Cli.ConfigCorrupt"), load.Error ?? string.Empty));
        }
        else if (load.Status == ToolConfigLoadStatus.ReadFailed)
        {
            error.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.Get(language, "Cli.ConfigReadFailed"), load.Error ?? string.Empty));
        }

        var context = new CommandLineContext
        {
            Paths = paths,
            Config = load.Config,
            IsGameRunning = GameProcess.IsRunning,
            LocateGame = () => AppServices.LocateGame(paths, load.Config),
            Launcher = AppServices.CreateLauncher(paths),
        };
        int code = new CommandLineRunner(context, output, language, error).Run(options);

        CliConsole.Write(output.ToString(), error.ToString(),
            dialogWithoutConsole: code != ExitCodes.Success || options.Show || options.Help
                                  || options.CheckConflicts || options.ListPlaysets || options.ListMods,
            isError: code != ExitCodes.Success);
        return code;
    }

    /// <summary>
    /// 参数有误时报错用的语言：参数里能认出的 <c>--lang</c>，其次配置里保存的界面语言（只读，不像正式读取那样
    /// 处理损坏的文件），再次系统语言。
    /// </summary>
    private static string GuessLanguage(string[] args)
    {
        (string? language, string? userData) = CommandLineParser.PeekOptions(args);
        if (language != null)
            return language;

        try
        {
            UserDataPaths paths = userData != null ? new UserDataPaths(userData) : UserDataPaths.CreateDefault();
            if (File.Exists(paths.ToolConfigFile))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(paths.ToolConfigFile));
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("language", out JsonElement saved)
                    && UiLanguage.Normalize(saved.ValueKind == JsonValueKind.String ? saved.GetString() : null) is { } known)
                {
                    return known;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // 读不了就按系统语言
        }

        return UiLanguage.FromCulture(CultureInfo.CurrentUICulture);
    }

    /// <summary>结束进程并带上退出码：没有窗口，直接结束消息循环。</summary>
    private void ExitWith(int code)
    {
        Environment.ExitCode = code;
        Shutdown(code);
    }
}
