using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using MapExTools.Core.Config;
using MapExTools.Core.Game;
using MapExTools.Core.Install;
using MapExTools.Core.Localization;
using MapExTools.Services;

namespace MapExTools;

public partial class MainWindow : Window
{
    /// <summary>游戏进程的检查间隔（计划 2.3：每 1.5 秒）。</summary>
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(1.5);

    private readonly DispatcherTimer _pollTimer;
    private bool? _gameRunning;

    // 填充下拉框时会触发 SelectionChanged，这段时间不当作用户操作
    private bool _populating;

    // 已经确认过（固定到本地后从新位置重新打开），关窗时不再询问未保存的改动
    private bool _closeConfirmed;

    // 玩家关掉了横幅：只在本次运行中隐藏，不写配置
    private bool _bannerHidden;

    public MainWindow()
    {
        InitializeComponent();

        PopulateLanguageBox();
        PopulateThemeBox();
        Loc.Instance.LanguageChanged += OnUiLanguageChanged;
        App.Services.Coc.Changed += OnCocSessionChanged;
        App.Services.Conflicts.Changed += OnConflictsChanged;

        _pollTimer = new DispatcherTimer { Interval = s_pollInterval };
        _pollTimer.Tick += (_, _) =>
        {
            App.Services.Coc.Poll();
            App.Services.Conflicts.Poll();
            RefreshGameState();
        };
        _pollTimer.Start();
        RefreshGameState();
        RefreshUserDataText();
        RefreshBanner();
        RefreshOptionsTabHeader();
        RefreshConflictsTabHeader();
        App.Services.Conflicts.TriggerScan();

        Closing += OnClosing;
        Closed += (_, _) =>
        {
            _pollTimer.Stop();
            Loc.Instance.LanguageChanged -= OnUiLanguageChanged;
            App.Services.Coc.Changed -= OnCocSessionChanged;
            App.Services.Conflicts.Changed -= OnConflictsChanged;
        };
    }

    internal void OpenModSettings()
    {
        MainTabs.SelectedItem = OptionsTab;
    }

    /// <summary>
    /// "固定到本地"（横幅与启动页的按钮共用）：确认后复制、建快捷方式、从新位置启动，然后关闭本窗口。
    /// 有未保存的改动时在确认框里一并说明。
    /// </summary>
    internal void PinAndRestart()
    {
        OptionsPage.CommitPendingNumberBoxes();
        Keyboard.ClearFocus();
        if (!InstallLocationService.PinAndStart(this, confirm: true, App.Services.Coc.HasUnsavedChanges))
            return;

        _closeConfirmed = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed)
            return;

        // 关窗前先让聚焦的数字框失焦并提交待定文本，避免未按回车的输入被静默丢弃
        OptionsPage.CommitPendingNumberBoxes();
        Keyboard.ClearFocus();
        if (!App.Services.Coc.HasUnsavedChanges)
            return;

        if (MessageBox.Show(this, Loc.Instance["Startup.ConfirmClose"], Loc.Instance["App.Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            e.Cancel = true;
        }
    }

    // === 运行位置横幅 ===

    private void RefreshBanner()
    {
        InstallLocation location = InstallLocationService.Current;
        string? key = location.Kind switch
        {
            InstallLocationKind.Temp => "Install.Banner.Temp",
            InstallLocationKind.Downloads => "Install.Banner.Downloads",
            _ => null,
        };
        if (key == null || _bannerHidden)
        {
            PinBanner.Visibility = Visibility.Collapsed;
            return;
        }

        BannerText.Text = Loc.Instance[key];
        BannerPinButton.IsEnabled = !InstallLocationService.IsDevelopmentBuild;
        BannerPinButton.ToolTip = InstallLocationService.IsDevelopmentBuild
            ? Loc.Instance["Install.PinDevBuild"]
            : Loc.Instance.Format("Install.PinTip", ToolPinning.DefaultPinnedDir);
        PinBanner.Visibility = Visibility.Visible;
    }

    private void OnPinClick(object sender, RoutedEventArgs e) => PinAndRestart();

    private void OnHideBannerClick(object sender, RoutedEventArgs e)
    {
        _bannerHidden = true;
        PinBanner.Visibility = Visibility.Collapsed;
    }

    private sealed record Choice<T>(T Value, string Display);

    // === 语言与主题 ===

    private void PopulateLanguageBox()
    {
        _populating = true;
        LanguageBox.ItemsSource = UiLanguage.All.Select(l => new Choice<string>(l, UiLanguage.GetNativeName(l))).ToList();
        LanguageBox.SelectedIndex = UiLanguage.All.ToList().IndexOf(Loc.Instance.Language);
        _populating = false;
    }

    private void PopulateThemeBox()
    {
        _populating = true;
        ThemePreference current = App.Services.Config.Theme;
        var items = Enum.GetValues<ThemePreference>()
            .Select(t => new Choice<ThemePreference>(t, Loc.Instance[$"Theme.{t}"]))
            .ToList();
        ThemeBox.ItemsSource = items;
        ThemeBox.SelectedIndex = items.FindIndex(c => c.Value == current);
        _populating = false;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || LanguageBox.SelectedItem is not Choice<string> choice)
            return;

        App.Services.Config.Language = choice.Value;
        App.Services.TrySaveConfig(this);
        Loc.Instance.SetLanguage(choice.Value);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || ThemeBox.SelectedItem is not Choice<ThemePreference> choice)
            return;

        App.Services.Config.Theme = choice.Value;
        App.Services.TrySaveConfig(this);
        ThemeService.Apply(choice.Value);
    }

    private void OnUiLanguageChanged(object? sender, EventArgs e)
    {
        // 代码里拼出的文字不走绑定，手动刷新
        PopulateThemeBox();
        _gameRunning = null;
        RefreshGameState();
        RefreshUserDataText();
        RefreshBanner();
        RefreshOptionsTabHeader();
        RefreshConflictsTabHeader();
    }

    private void OnCocSessionChanged(object? sender, EventArgs e)
    {
        RefreshOptionsTabHeader();
    }

    private void OnConflictsChanged(object? sender, EventArgs e)
    {
        RefreshConflictsTabHeader();
    }

    private void RefreshOptionsTabHeader()
    {
        int pending = App.Services.Coc.IsUsable ? App.Services.Coc.GetPending().Count : 0;
        OptionsTab.Header = pending > 0
            ? Loc.Instance.Format("Tab.OptionsDirty", pending)
            : Loc.Instance["Tab.Options"];
    }

    private void RefreshConflictsTabHeader()
    {
        var report = App.Services.Conflicts.Report;
        int alerts = report != null ? report.HardConflictCount + report.WarningCount : 0;
        ConflictsTab.Header = alerts > 0
            ? Loc.Instance.Format("Tab.ConflictsAlert", alerts)
            : Loc.Instance["Tab.Conflicts"];
    }

    // === 状态栏 ===

    private void RefreshGameState()
    {
        bool running = GameProcess.IsRunning();
        if (running == _gameRunning)
            return;

        _gameRunning = running;
        GameStateText.Text = Loc.Instance[running ? "Status.GameRunning" : "Status.GameNotRunning"];
        GameStateDot.SetResourceReference(Shape.FillProperty, running ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush");
    }

    private void RefreshUserDataText()
    {
        UserDataText.Text = Loc.Instance.Format("Status.UserData", App.Services.Paths.Root);
    }
}
