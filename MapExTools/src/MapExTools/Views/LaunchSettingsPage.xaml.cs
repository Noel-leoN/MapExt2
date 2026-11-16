using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MapExTools.Core.Coc;
using MapExTools.Core.Install;
using MapExTools.Core.Packages;
using MapExTools.Services;
using MapExtPDX;

namespace MapExTools.Views;

/// <summary>
/// 启动与设定页。最上面是启动游戏（见 <c>LaunchSettingsPage.Launch.cs</c>）；中间是高频核心启动设定速选（地图尺寸模式、
/// 存档转换与安全校验、经济系统开关共 11 项），与第 2 页签「模组设置」共用窗口级 <see cref="CocSession"/>；
/// 最下面是 MapExTools 的位置与桌面快捷方式。
/// </summary>
public partial class LaunchSettingsPage : UserControl
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(1.5);

    private readonly DispatcherTimer _pollTimer = new() { Interval = s_pollInterval };
    private readonly Dictionary<string, RadioButton> _modeButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checkBoxes = new(StringComparer.Ordinal);

    private bool _populating;

    public LaunchSettingsPage()
    {
        InitializeComponent();
        BuildOptions();
        UpdateOptionTexts();

        _pollTimer.Tick += (_, _) => RefreshLaunchPoll(Coc.GameRunning);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        SyncFromSession();
        InitLaunch();
    }

    public bool HasUnsavedChanges => Coc.HasUnsavedChanges;

    private static CocSession Coc => App.Services.Coc;

    private static string CurrentLanguage => Loc.Instance.Language;

    // === 生命周期 ===

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        Coc.Changed += OnSessionChanged;
        UpdateOptionTexts();
        SyncFromSession();
        _pollTimer.Start();
        RefreshLaunchOnLoad();
        RefreshInstall();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        Coc.Changed -= OnSessionChanged;
        _pollTimer.Stop();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateOptionTexts();
        UpdateState();
        PopulateLogLevels();
        UpdateLaunchState();
        RefreshInstall();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        SyncFromSession();
        UpdateLaunchState();
    }

    // === 选项 ===

    /// <summary>按 <see cref="OptionCategory.Core"/> 生成 5 个模式单选与 10 个核心开关。</summary>
    private void BuildOptions()
    {
        foreach (string mode in StartupKeys.ModeNames)
        {
            var radio = new RadioButton { GroupName = "LaunchPatchMode", Tag = mode, Margin = new Thickness(0, 1, 0, 1) };
            radio.Checked += OnModeChecked;
            _modeButtons[mode] = radio;
            ModePanel.Children.Add(radio);
        }

        // 中栏顺序：地图扩展转换 → 停用背景地图 → 禁止读档校验
        string[] middleOrder = [SaveGateRules.ConversionKey, SaveGateRules.BackdropKey, SaveGateRules.ValidationKey];
        foreach (string name in middleOrder)
        {
            StartupKeyInfo key = StartupKeys.FindKey(name)!;
            AddCoreCheckBox(key, OtherPanel);
        }

        foreach (StartupKeyInfo key in StartupKeys.AllKeys.Where(StartupSettingsText.IsEconomyKey))
        {
            AddCoreCheckBox(key, EconomyPanel);
        }
    }

    private void AddCoreCheckBox(StartupKeyInfo key, Panel host)
    {
        var box = new CheckBox
        {
            Tag = key.Name,
            Margin = new Thickness(key.EcoGroup ? 24 : 0, 1, 0, 1),
        };
        box.Checked += OnCheckChanged;
        box.Unchecked += OnCheckChanged;
        _checkBoxes[key.Name] = box;
        host.Children.Add(box);
    }

    private void UpdateOptionTexts()
    {
        string lang = CurrentLanguage;
        foreach ((string mode, RadioButton radio) in _modeButtons)
            radio.Content = StartupSettingsText.ModeChoice(mode, lang);
        foreach ((string name, CheckBox box) in _checkBoxes)
        {
            StartupKeyInfo key = StartupKeys.FindKey(name)!;
            box.Content = StartupSettingsText.KeyLabel(key, lang);
            box.ToolTip = StartupSettingsText.KeyTip(key, lang);
        }
    }

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (_populating || sender is not RadioButton { IsChecked: true, Tag: string mode })
            return;

        Coc.SetValue(StartupKeys.ModeKey, mode);
    }

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (_populating || sender is not CheckBox { Tag: string name } box)
            return;

        Coc.SetValue(name, box.IsChecked == true);
    }

    private void SyncFromSession()
    {
        _populating = true;
        try
        {
            string mode = Coc.GetString(StartupKeys.ModeKey);
            foreach ((string name, RadioButton radio) in _modeButtons)
                radio.IsChecked = name == mode;
            foreach ((string name, CheckBox box) in _checkBoxes)
                box.IsChecked = Coc.GetBool(name);
        }
        finally
        {
            _populating = false;
        }

        UpdateState();
    }

    // === 界面状态 ===

    private void UpdateState()
    {
        string lang = CurrentLanguage;
        bool usable = Coc.IsUsable;
        bool running = Coc.GameRunning;
        bool economyOn = Coc.GetBool(StartupSettingsText.EconomyMasterKey);

        foreach (RadioButton radio in _modeButtons.Values)
            radio.IsEnabled = usable;
        foreach ((string name, CheckBox box) in _checkBoxes)
            box.IsEnabled = Coc.IsKeyEnabled(StartupKeys.FindKey(name)!);

        EconomyOffText.Visibility = usable && !economyOn ? Visibility.Visible : Visibility.Collapsed;
        ValidationWarning.Visibility = Coc.GetBool(SaveGateRules.ValidationKey)
            ? Visibility.Visible
            : Visibility.Collapsed;

        IReadOnlyList<StartupSettingChange> pending = usable ? Coc.GetPending() : [];
        if (!usable)
        {
            PendingText.Visibility = Visibility.Collapsed;
            PendingText.ToolTip = null;
        }
        else
        {
            PendingText.Visibility = Visibility.Visible;
            string compact = pending.Count > 0 ? StartupSettingsText.ChangeListCompact(pending, lang) : string.Empty;
            PendingText.DataContext = pending.Count > 0
                ? new ToneLine(Loc.Instance.Format("Startup.Pending", compact), PackageTone.Warning)
                : new ToneLine(Loc.Instance["Startup.PendingNone"], PackageTone.Muted);
            PendingText.ToolTip = pending.Count > 0
                ? Loc.Instance.Format("Startup.Pending", StartupSettingsText.ChangeList(pending, lang))
                : null;
        }

        SaveButton.IsEnabled = usable && !running && pending.Count > 0;
        DefaultsButton.IsEnabled = usable;
        StatusLines.ItemsSource = Coc.BuildStatusLines(lang);

        string cocPath = Coc.CocPath;
        bool exists = File.Exists(cocPath);
        FileText.Text = Loc.Instance.Format("Startup.File", cocPath);
        FileText.ToolTip = FileText.Text;
        EditLink.IsEnabled = exists;
        ShowFileLink.IsEnabled = exists || Directory.Exists(Path.GetDirectoryName(cocPath));

        if (Coc.Applied is { } applied)
        {
            AppliedText.Text = Loc.Instance.Format("Startup.Applied", applied.Timestamp, applied.Raw);
            AppliedText.ToolTip = AppliedText.Text;
            AppliedText.Visibility = Visibility.Visible;
        }
        else
        {
            AppliedText.Visibility = Visibility.Collapsed;
            AppliedText.ToolTip = null;
        }
    }

    // === 按钮与链接 ===

    private void OnSaveClick(object sender, RoutedEventArgs e) => Coc.Save(Window.GetWindow(this));

    private void OnReloadClick(object sender, RoutedEventArgs e) => Coc.ReloadManual();

    private void OnDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (Coc.IsUsable)
            Coc.ResetCoreStartupDefaults();
    }

    private void OnOpenAllOptionsClick(object sender, RoutedEventArgs e)
    {
        (Window.GetWindow(this) as MainWindow)?.OpenModSettings();
    }

    private void OnEditClick(object sender, RoutedEventArgs e) => Coc.OpenInNotepad(Window.GetWindow(this));

    private void OnShowFileClick(object sender, RoutedEventArgs e) => Coc.ShowInExplorer(Window.GetWindow(this));

    // === MapExTools 的位置与快捷方式 ===

    private void RefreshInstall()
    {
        string lang = CurrentLanguage;
        InstallLocation location = InstallLocationService.Current;
        string desktop = ToolShortcut.DesktopDir;

        ShortcutStatus status = desktop.Length > 0
            ? ToolShortcut.Inspect(desktop, location.ExePath)
            : new ShortcutStatus(ShortcutState.Foreign, ToolShortcut.FileName, null);

        LocationText.Text = InstallText.Location(location, lang);
        ShortcutText.Text = desktop.Length > 0 ? InstallText.Shortcut(status, lang) : string.Empty;
        ShortcutText.Visibility = desktop.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        InstallSummaryText.DataContext = new ToneLine(
            InstallText.Summary(location, desktop.Length > 0 ? status : null, lang),
            location.OffersPinning ? PackageTone.Warning : PackageTone.Muted);

        bool temporary = location.OffersPinning;
        bool devBuild = InstallLocationService.IsDevelopmentBuild;
        PinButton.Visibility = temporary ? Visibility.Visible : Visibility.Collapsed;
        PinButton.IsEnabled = !devBuild;
        PinButton.ToolTip = Loc.Instance.Format("Install.PinTip", ToolPinning.DefaultPinnedDir);

        CreateShortcutButton.Visibility = !temporary && status.State is ShortcutState.Missing or ShortcutState.PointsElsewhere
            ? Visibility.Visible
            : Visibility.Collapsed;
        CreateShortcutButton.Content = Loc.Instance[status.State == ShortcutState.PointsElsewhere
            ? "Install.RetargetShortcut"
            : "Install.CreateShortcut"];
        DeleteShortcutButton.Visibility = status.State is ShortcutState.PointsHere or ShortcutState.PointsElsewhere
            ? Visibility.Visible
            : Visibility.Collapsed;

        InstallNote.Text = Loc.Instance["Install.PinDevBuild"];
        InstallNote.Visibility = temporary && devBuild ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        (Window.GetWindow(this) as MainWindow)?.PinAndRestart();
    }

    private void OnCreateShortcutClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ToolShortcut.Create(ToolShortcut.DesktopDir, InstallLocationService.Current.ExePath, Loc.Instance["Install.ShortcutDescription"]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
        {
            ShowMessage(Loc.Instance.Format("Install.ShortcutFailed", ex.Message), MessageBoxImage.Warning);
        }

        RefreshInstall();
    }

    private void OnDeleteShortcutClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ToolShortcut.Delete(ToolShortcut.DesktopDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowMessage(Loc.Instance.Format("Install.ShortcutFailed", ex.Message), MessageBoxImage.Warning);
        }

        RefreshInstall();
    }

    private static string Localized(string language, string key) => Core.Localization.Strings.Get(language, key);

    private void ShowMessage(string text, MessageBoxImage image) =>
        MessageBox.Show(Window.GetWindow(this)!, text, Loc.Instance["App.Title"], MessageBoxButton.OK, image);
}
