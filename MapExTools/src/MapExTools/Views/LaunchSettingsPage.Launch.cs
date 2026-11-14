using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MapExTools.Core.Coc;
using MapExTools.Core.Launch;
using MapExTools.Core.Packages;
using MapExTools.Services;
using Microsoft.Win32;

namespace MapExTools.Views;

/// <summary>
/// 启动与设定页的"启动游戏"一栏（计划 2.2）：游戏位置、三条启动通道、启动参数与命令行预览。参数改了就存进
/// MapExTools.json，命令行 <c>--launch</c> 用同一份。启动前处理三件事：MapExt 设定未保存或格式有误、
/// runOnce.txt 有残留（不论哪条通道游戏都会读）、直启时 Steam 没运行（改走 -applaunch 或取消）。
/// </summary>
public partial class LaunchSettingsPage
{
    // 点了启动后、游戏进程出现前按钮显示"正在启动…"；经 Steam 时要等启动器，超过这个时间就恢复
    private static readonly TimeSpan s_launchCooldown = TimeSpan.FromSeconds(20);

    private readonly Dictionary<LaunchChannel, RadioButton> _channelButtons = [];
    private bool _populatingLaunch;
    private DateTime? _launchStartedUtc;
    private bool? _steamRunning;
    private string? _runOncePending;

    // 上次启动的结果，按界面语言生成
    private Func<string, string>? _launchNote;

    private sealed record LevelChoice(string Level, string Display)
    {
        // 读屏软件与 UI 自动化读的是项的 ToString：返回显示文字，不是记录的字段清单
        public override string ToString() => Display;
    }

    private static LaunchOptions Options => App.Services.Config.Launch;

    private static GameLocateResult Location => App.Services.GameLocation;

    // === 初始化与刷新 ===

    private void InitLaunch()
    {
        _channelButtons[LaunchChannel.Direct] = DirectChannelRadio;
        _channelButtons[LaunchChannel.Steam] = SteamChannelRadio;
        _channelButtons[LaunchChannel.RunOnce] = RunOnceChannelRadio;

        PopulateLogLevels();
        LoadLaunchOptions();
        _steamRunning = SteamRegistry.IsSteamRunning();
        _runOncePending = RunOnceFile.ReadPending(App.Services.Paths.RunOnceFile);
    }

    /// <summary>日志级别下拉框："默认"随界面语言变，其余是游戏认识的级别名。</summary>
    private void PopulateLogLevels()
    {
        _populatingLaunch = true;
        try
        {
            LogLevelBox.ItemsSource = LaunchArgumentBuilder.LogLevels
                .Select(l => new LevelChoice(l, l == LaunchArgumentBuilder.DefaultLogLevel ? Loc.Instance["Launch.LogLevel.Default"] : l))
                .ToList();
            string selected = LaunchArgumentBuilder.NormalizeLogLevel(Options.LogLevel) ?? LaunchArgumentBuilder.DefaultLogLevel;
            LogLevelBox.SelectedIndex = LaunchArgumentBuilder.LogLevels.ToList().IndexOf(selected);
        }
        finally
        {
            _populatingLaunch = false;
        }
    }

    /// <summary>配置 → 界面。</summary>
    private void LoadLaunchOptions()
    {
        _populatingLaunch = true;
        try
        {
            LaunchOptions o = Options;
            foreach ((LaunchChannel channel, RadioButton radio) in _channelButtons)
                radio.IsChecked = channel == o.Channel;
            DisableUserSectionBox.IsChecked = o.DisableUserSection;
            DisableCodeModdingBox.IsChecked = o.DisableCodeModding;
            DisableModdingBox.IsChecked = o.DisableModding;
            NoSplashBox.IsChecked = o.NoSplash;
            DeveloperModeBox.IsChecked = o.DeveloperMode;
            UiDeveloperModeBox.IsChecked = o.UiDeveloperMode;
            DuplicateLogBox.IsChecked = o.DuplicateLogToDefault;
            DisableBurstBox.IsChecked = o.DisableBurstCompilation;
            CustomArgumentsBox.Text = o.CustomArguments ?? string.Empty;
        }
        finally
        {
            _populatingLaunch = false;
        }

        PopulateLogLevels();
        UpdateLaunchState();
    }

    /// <summary>页面显示时重新找一次游戏（玩家可能刚装好或挪了位置）。</summary>
    private void RefreshLaunchOnLoad()
    {
        App.Services.RefreshGameLocation();
        _steamRunning = SteamRegistry.IsSteamRunning();
        _runOncePending = RunOnceFile.ReadPending(App.Services.Paths.RunOnceFile);
        PopulateLogLevels();
        UpdateLaunchState();
    }

    /// <summary>每 1.5 秒的检查：Steam 是否在运行、runOnce.txt 的内容；游戏进程出现后结束"正在启动"。</summary>
    private void RefreshLaunchPoll(bool gameRunning)
    {
        _steamRunning = SteamRegistry.IsSteamRunning();
        _runOncePending = RunOnceFile.ReadPending(App.Services.Paths.RunOnceFile);
        if (_launchStartedUtc is { } started && (gameRunning || DateTime.UtcNow - started > s_launchCooldown))
            _launchStartedUtc = null;
        UpdateLaunchState();
    }

    private void UpdateLaunchState()
    {
        string lang = CurrentLanguage;
        GameLocateResult location = Location;
        LaunchOptions o = Options;
        bool running = _gameRunning == true;

        // 游戏位置与说明
        GameLocationText.Text = LaunchText.Location(location, lang);
        string? locationNote = location.MissingManualPath != null
            ? Loc.Instance.Format("Launch.ManualMissing", location.MissingManualPath)
            : location.Game is { IsSteamBuild: false } ? Loc.Instance["Launch.NotSteamBuild"]
            : location.Game != null && location.Steam == null ? Loc.Instance["Launch.SteamNotFound"]
            : null;
        GameLocationNote.Text = locationNote ?? string.Empty;
        GameLocationNote.Visibility = locationNote != null ? Visibility.Visible : Visibility.Collapsed;
        AutoLocateText.Visibility = App.Services.Config.GameExePath != null ? Visibility.Visible : Visibility.Collapsed;

        // 启动方式：找不到 Steam 或不是 Steam 版时，经 Steam 的两条不能选（已选的照样显示，启动时说明原因）
        bool steamChannels = location.Game == null || location.SteamChannelsAvailable;
        SteamChannelRadio.IsEnabled = steamChannels || o.Channel == LaunchChannel.Steam;
        RunOnceChannelRadio.IsEnabled = steamChannels || o.Channel == LaunchChannel.RunOnce;
        ChannelNoteText.Text = Loc.Instance["Launch.ChannelNote." + o.Channel];
        string? channelBlock = o.Channel != LaunchChannel.Direct && location.Game != null && !location.SteamChannelsAvailable
            ? LaunchText.Block(location.Steam == null ? LaunchBlock.SteamNotFound : LaunchBlock.NotSteamBuild, lang)
            : null;
        ChannelBlockText.Text = channelBlock ?? string.Empty;
        ChannelBlockText.Visibility = channelBlock != null ? Visibility.Visible : Visibility.Collapsed;

        // 参数：禁用所有 Mod 已含禁用代码 Mod；走 runOnce.txt 时禁用 Burst 无效
        DisableCodeModdingBox.IsEnabled = !o.DisableModding;
        bool runOnce = o.Channel == LaunchChannel.RunOnce;
        DisableBurstBox.IsEnabled = !runOnce;
        BurstRunOnceText.Visibility = runOnce ? Visibility.Visible : Visibility.Collapsed;
        CustomQuotesText.Visibility = runOnce && LaunchArgumentBuilder.CustomHasQuotes(o) ? Visibility.Visible : Visibility.Collapsed;

        // 命令行预览；直启时提示另设了两个环境变量
        CommandPreviewBox.Text = Loc.Instance.Format("Launch.Preview", LaunchText.Preview(o.Channel, o, lang));
        CommandPreviewBox.ToolTip = o.Channel == LaunchChannel.Direct ? Loc.Instance["Launch.PreviewEnvTip"] : null;

        // 状态
        var lines = new List<ToneLine>();
        if (running)
            lines.Add(new ToneLine(Loc.Instance["Launch.Status.Running"], PackageTone.Warning));
        else if (o.Channel == LaunchChannel.Direct && location.Game is { IsSteamBuild: true } && _steamRunning == false)
            lines.Add(new ToneLine(Loc.Instance["Launch.Status.SteamNotRunning"], PackageTone.Warning));
        if (_launchNote != null)
            lines.Add(new ToneLine(_launchNote(lang), PackageTone.Good));
        LaunchStatusLines.ItemsSource = lines;

        RunOncePendingRun.Text = _runOncePending != null ? Loc.Instance.Format("Launch.Status.RunOnce", _runOncePending) + "  " : string.Empty;
        RunOncePendingText.Visibility = _runOncePending != null ? Visibility.Visible : Visibility.Collapsed;

        bool starting = _launchStartedUtc != null;
        LaunchButton.Content = Loc.Instance[starting ? "Launch.Starting" : "Launch.Button"];
        LaunchButton.IsEnabled = location.Game != null && !running && !starting;
    }

    // === 参数 ===

    /// <summary>界面 → 配置，并保存。</summary>
    private void OnLaunchOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_populatingLaunch)
            return;

        LaunchOptions o = Options;
        o.Channel = _channelButtons.FirstOrDefault(p => p.Value.IsChecked == true).Key;
        o.DisableUserSection = DisableUserSectionBox.IsChecked == true;
        o.DisableCodeModding = DisableCodeModdingBox.IsChecked == true;
        o.DisableModding = DisableModdingBox.IsChecked == true;
        o.NoSplash = NoSplashBox.IsChecked == true;
        o.DeveloperMode = DeveloperModeBox.IsChecked == true;
        o.UiDeveloperMode = UiDeveloperModeBox.IsChecked == true;
        o.DuplicateLogToDefault = DuplicateLogBox.IsChecked == true;
        o.DisableBurstCompilation = DisableBurstBox.IsChecked == true;
        if (LogLevelBox.SelectedItem is LevelChoice level)
            o.LogLevel = level.Level == LaunchArgumentBuilder.DefaultLogLevel ? null : level.Level;

        App.Services.TrySaveConfig(Window.GetWindow(this));
        UpdateLaunchState();
    }

    /// <summary>打字时只更新预览，失焦或启动时再保存，免得每个字都写一次文件。</summary>
    private void OnCustomArgumentsChanged(object sender, TextChangedEventArgs e)
    {
        if (_populatingLaunch)
            return;

        Options.CustomArguments = string.IsNullOrWhiteSpace(CustomArgumentsBox.Text) ? null : CustomArgumentsBox.Text;
        UpdateLaunchState();
    }

    private void OnCustomArgumentsLostFocus(object sender, RoutedEventArgs e) => SaveCustomArguments();

    private void SaveCustomArguments()
    {
        // 配置对象打字时已更新；与上次写盘的值相同也重写一次，开销很小
        App.Services.TrySaveConfig(Window.GetWindow(this));
    }

    // === 游戏位置 ===

    private void OnChooseGameClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Loc.Instance["Launch.ChooseTitle"],
            Filter = "Cities2.exe|Cities2.exe",
            CheckFileExists = true,
        };
        if (Location.Game != null && Directory.Exists(Location.Game.GameDir))
            dialog.InitialDirectory = Location.Game.GameDir;

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;
        if (!GameLocator.IsGameExe(dialog.FileName))
        {
            ShowMessage(Loc.Instance["Launch.ChooseWrongFile"], MessageBoxImage.Warning);
            return;
        }

        App.Services.Config.GameExePath = dialog.FileName;
        App.Services.TrySaveConfig(Window.GetWindow(this));
        App.Services.RefreshGameLocation();
        UpdateLaunchState();
    }

    private void OnAutoLocateClick(object sender, RoutedEventArgs e)
    {
        App.Services.Config.GameExePath = null;
        App.Services.TrySaveConfig(Window.GetWindow(this));
        App.Services.RefreshGameLocation();
        UpdateLaunchState();
    }

    private void OnClearRunOnceClick(object sender, RoutedEventArgs e)
    {
        TryClearRunOnce();
        _runOncePending = RunOnceFile.ReadPending(App.Services.Paths.RunOnceFile);
        UpdateLaunchState();
    }

    private bool TryClearRunOnce()
    {
        try
        {
            RunOnceFile.Delete(App.Services.Paths.RunOnceFile);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowMessage(Loc.Instance.Format("Launch.ClearFailed", ex.Message), MessageBoxImage.Warning);
            return false;
        }
    }

    // === 启动 ===

    private void OnLaunchClick(object sender, RoutedEventArgs e)
    {
        SaveCustomArguments();
        GameLocateResult location = App.Services.RefreshGameLocation();
        LaunchChannel channel = Options.Channel;
        GameLauncher launcher = App.Services.Launcher;

        if (!ConfirmStartupSettingsBeforeLaunch())
        {
            UpdateLaunchState();
            return;
        }

        // 不论哪条通道游戏都会读 runOnce.txt；runOnce 通道自己会整个覆盖，不用问
        if (channel != LaunchChannel.RunOnce && RunOnceFile.ReadPending(launcher.RunOncePath) is { } pending)
        {
            MessageBoxResult answer = Ask(Loc.Instance.Format("Launch.ConfirmRunOnce", pending), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !TryClearRunOnce()))
            {
                UpdateLaunchState();
                return;
            }
        }

        // 直启 Steam 版时 Steam 没开：游戏会报错退出。可以这一次改走 -applaunch（它会先打开 Steam）
        if (launcher.Check(location, channel) == LaunchBlock.SteamNotRunning)
        {
            if (!location.SteamChannelsAvailable)
            {
                ShowMessage(LaunchText.Block(LaunchBlock.SteamNotRunning, CurrentLanguage), MessageBoxImage.Warning);
                return;
            }

            if (Ask(Loc.Instance["Launch.ConfirmSteamFallback"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            channel = LaunchChannel.Steam;
        }

        try
        {
            LaunchResult result = launcher.Launch(location, Options, channel);
            if (result.Block != LaunchBlock.None)
            {
                ShowMessage(LaunchText.Block(result.Block, CurrentLanguage), MessageBoxImage.Warning);
            }
            else
            {
                string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                string arguments = result.Request!.Arguments;
                LaunchChannel used = channel;
                _launchNote = lang => string.Format(CultureInfo.InvariantCulture, Localized(lang, "Launch.Status.Launched"),
                    LaunchText.ChannelShort(used, lang), time, LaunchText.Command(used, arguments, lang));
                _launchStartedUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowMessage(Loc.Instance.Format("Launch.Failed", ex.Message), MessageBoxImage.Warning);
        }

        _runOncePending = RunOnceFile.ReadPending(launcher.RunOncePath);
        UpdateLaunchState();
    }

    /// <summary>
    /// 启动前看 MapExt 设定：文件格式有误时确认是否仍要启动；有未保存的改动时问"保存后启动／直接启动／取消"。
    /// 返回 false 表示不启动。
    /// </summary>
    private bool ConfirmStartupSettingsBeforeLaunch()
    {
        if (_formatError != null)
        {
            return Ask(Loc.Instance.Format("Launch.ConfirmCocError", _formatError.GetMessage(CurrentLanguage)),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        IReadOnlyList<StartupSettingChange> pending = GetPending();
        if (pending.Count == 0)
            return true;

        MessageBoxResult answer = Ask(
            Loc.Instance.Format("Launch.ConfirmUnsaved", StartupSettingsText.ChangeList(pending, CurrentLanguage)),
            MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return answer switch
        {
            MessageBoxResult.Yes => SaveSettings(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private MessageBoxResult Ask(string text, MessageBoxButton buttons, MessageBoxImage image) =>
        MessageBox.Show(Window.GetWindow(this)!, text, Loc.Instance["App.Title"], buttons, image);
}
