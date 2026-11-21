using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

    // 系统字体列表在首次展开 FontBox 时才懒加载；记下加载时的界面语言以便切语言后同步显示名与排序
    private bool _fontListLoaded;
    private string? _fontListLanguage;

    public MainWindow()
    {
        InitializeComponent();

        ApplyCurrentFont();
        PopulateLanguageBox();
        PopulateThemeBox();
        PopulateFontBox(loadAll: false);
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

    private void ApplyCurrentFont() =>
        FontService.Apply(this, App.Services.Config.FontFamily, Loc.Instance.Language);

    private void PopulateFontBox(bool loadAll)
    {
        _populating = true;
        try
        {
            string lang = Loc.Instance.Language;
            string? configured = App.Services.Config.FontFamily;
            if (loadAll)
            {
                (IReadOnlyList<FontOption> options, FontOption selected) = FontService.GetOptions(lang, configured);
                FontBox.ItemsSource = options;
                FontBox.SelectedItem = selected;
                _fontListLoaded = true;
                _fontListLanguage = lang;
            }
            else
            {
                FontOption placeholder = FontService.CreateSelectedPlaceholder(lang, configured);
                FontBox.ItemsSource = new[] { placeholder };
                FontBox.SelectedIndex = 0;
                _fontListLoaded = false;
                _fontListLanguage = null;
            }
        }
        finally
        {
            _populating = false;
        }
    }

    private void OnFontDropDownOpened(object? sender, EventArgs e)
    {
        // Fluent ComboBox 弹层内 ScrollViewer.CanContentScroll 默认为 False（切换主题重建模板后也会复位），
        // 每次展开先开启以让 VirtualizingStackPanel 生效，再按需懒加载完整系统字体列表
        if (FontBox.Template?.FindName("PART_Popup", FontBox) is Popup { Child: { } child }
            && FindVisualChild<ScrollViewer>(child) is { } scrollViewer)
        {
            scrollViewer.CanContentScroll = true;
        }

        if (!_fontListLoaded || _fontListLanguage != Loc.Instance.Language)
        {
            PopulateFontBox(loadAll: true);
        }
    }

    private void OnFontChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || FontBox.SelectedItem is not FontOption choice)
            return;

        App.Services.Config.FontFamily = choice.ConfigName;
        App.Services.TrySaveConfig(this);
        ApplyCurrentFont();

        // 若此前列表含未安装字体的临时占位项且已改选正常项，从内存缓存重生成列表移除残留占位
        if (_fontListLoaded && !choice.IsMissing
            && FontBox.ItemsSource is IEnumerable<FontOption> current
            && current.Any(o => o.IsMissing))
        {
            PopulateFontBox(loadAll: true);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
            return match;

        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            if (FindVisualChild<T>(VisualTreeHelper.GetChild(root, i)) is { } found)
                return found;
        }

        return null;
    }

    private void OnUiLanguageChanged(object? sender, EventArgs e)
    {
        ApplyCurrentFont();

        // 代码里拼出的文字不走绑定，手动刷新
        PopulateThemeBox();
        PopulateFontBox(loadAll: _fontListLoaded);
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
