using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MapExTools.Core.Coc;
using MapExTools.Core.Diagnostics;
using MapExTools.Core.Game;
using MapExTools.Core.Install;
using MapExTools.Core.Packages;
using MapExTools.Services;
using MapExtPDX;

namespace MapExTools.Views;

/// <summary>
/// 启动与设定页（计划 2.3）。上半部分改 <c>MapExtPDX.coc</c> 里 MapExt 只在启动时读取的 9 个设定，做法沿用开发版
/// <c>Set-MapExtStartup.ps1</c> 的界面：界面上的改动相对最后一次读取成功的设定计算，保存时只写有改动的键；
/// 游戏运行中不能保存（退出时游戏会用内存里的设定写回）。下半部分是 MapExTools 的位置与桌面快捷方式（§2 第 9 条）。
///
/// <para>页面可见时每 1.5 秒检查一次：游戏刚退出（退出时写回了设定）或文件被外部改过（记事本、存档页的"切到该模式"），
/// 就重新读取，界面上未保存的改动保留。</para>
/// </summary>
public partial class LaunchSettingsPage : UserControl
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(1.5);

    private readonly DispatcherTimer _pollTimer = new() { Interval = s_pollInterval };
    private readonly Dictionary<string, RadioButton> _modeButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checkBoxes = new(StringComparer.Ordinal);

    // 最后一次读取成功的设定：界面上的改动相对它计算。格式有误时保留上一次的，供修好后恢复改动
    private StartupSettingsState? _state;
    private CocFormatException? _formatError;
    private string? _readError;
    private CocStamp _stamp;
    private bool? _gameRunning;

    // 状态行里的补充说明（已保存、已重新读取……），按界面语言生成，切换语言时跟着变
    private Func<string, string>? _note;
    private AppliedConfigInfo? _applied;

    // 程序设置勾选状态时会触发 Checked／Unchecked，这段时间不当作用户操作
    private bool _populating;

    public LaunchSettingsPage()
    {
        InitializeComponent();
        BuildOptions();
        UpdateOptionTexts();

        _pollTimer.Tick += (_, _) => OnPollTick();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // 第一次显示之前先读一遍，未打开本页就关窗时也能判断有没有未保存的改动
        _gameRunning = GameProcess.IsRunning();
        Reload(keepEdits: false);
    }

    /// <summary>.coc 的存在与否、修改时间与长度，用来发现外部改动。</summary>
    private readonly record struct CocStamp(bool Exists, DateTime LastWriteUtc, long Length);

    /// <summary>
    /// 有没有未保存的改动（关窗、固定到本地重新打开前确认）。文件格式出错期间也算：改动相对最后一次读取成功的设定保留着。
    /// </summary>
    public bool HasUnsavedChanges => GetPending().Count > 0;

    private static string CurrentLanguage => Loc.Instance.Language;

    private static string CocPath => App.Services.Paths.CocFile;

    private bool IsUsable => _state != null && _formatError == null;

    // === 生命周期 ===

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        UpdateOptionTexts();
        OnPollTick();
        _pollTimer.Start();
        RefreshInstall();
        _ = LoadAppliedAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _pollTimer.Stop();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateOptionTexts();
        UpdateState();
        UpdateAppliedText();
        RefreshInstall();
    }

    /// <summary>定时检查：游戏刚退出，或文件被外部改过（含暂时读不了之后的重试）。</summary>
    private void OnPollTick()
    {
        bool running = GameProcess.IsRunning();
        if (_gameRunning == true && !running)
        {
            Reload(keepEdits: true);
            _note = lang => Localized(lang, "Startup.GameExited");
            _ = LoadAppliedAsync();
        }
        else if (ReadStamp() != _stamp)
        {
            bool retry = _readError != null;
            Reload(keepEdits: true);
            if (!retry && _readError == null)
                _note = lang => Localized(lang, "Startup.FileChanged");
        }

        _gameRunning = running;
        UpdateState();
    }

    // === 选项 ===

    /// <summary>按 <see cref="StartupKeys"/> 生成单选与勾选框，键的增减随共享定义走。</summary>
    private void BuildOptions()
    {
        foreach (string mode in StartupKeys.ModeNames)
        {
            var radio = new RadioButton { GroupName = "PatchMode", Tag = mode, Margin = new Thickness(0, 1, 0, 1) };
            radio.Checked += OnOptionChanged;
            _modeButtons[mode] = radio;
            ModePanel.Children.Add(radio);
        }

        foreach (StartupKeyInfo key in StartupKeys.Keys)
        {
            if (key.Kind != StartupKeyKind.Bool)
                continue;

            var box = new CheckBox { Tag = key.Name, Margin = new Thickness(key.EcoGroup ? 28 : 0, 1, 0, 1) };
            box.Checked += OnOptionChanged;
            box.Unchecked += OnOptionChanged;
            _checkBoxes[key.Name] = box;
            (StartupSettingsText.IsEconomyKey(key) ? EconomyPanel : OtherPanel).Children.Add(box);
        }
    }

    private void UpdateOptionTexts()
    {
        foreach ((string mode, RadioButton radio) in _modeButtons)
            radio.Content = StartupSettingsText.ModeChoice(mode, CurrentLanguage);
        foreach ((string name, CheckBox box) in _checkBoxes)
            box.Content = StartupSettingsText.KeyLabel(StartupKeys.FindKey(name)!, CurrentLanguage);
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_populating)
            UpdateState();
    }

    private Dictionary<string, object> GetUiValues()
    {
        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        string? mode = _modeButtons.FirstOrDefault(p => p.Value.IsChecked == true).Key;
        values[StartupKeys.ModeKey] = mode ?? StartupKeys.ModeNames[0];
        foreach ((string name, CheckBox box) in _checkBoxes)
            values[name] = box.IsChecked == true;
        return values;
    }

    private void SetUiValues(IReadOnlyDictionary<string, object> values)
    {
        _populating = true;
        try
        {
            string mode = (string)values[StartupKeys.ModeKey];
            foreach ((string name, RadioButton radio) in _modeButtons)
                radio.IsChecked = name == mode;
            foreach ((string name, CheckBox box) in _checkBoxes)
                box.IsChecked = (bool)values[name];
        }
        finally
        {
            _populating = false;
        }

        UpdateState();
    }

    private IReadOnlyList<StartupSettingChange> GetPending() =>
        _state == null ? [] : CocSettingsManager.GetChanges(_state, GetUiValues());

    // === 读取 ===

    private static CocStamp ReadStamp()
    {
        var info = new FileInfo(CocPath);
        return info.Exists ? new CocStamp(true, info.LastWriteTimeUtc, info.Length) : default;
    }

    /// <summary>
    /// 重新读设定文件作为基准。<paramref name="keepEdits"/> 时把界面上未保存的改动套到新读到的值上；改动相对最后一次
    /// 读取成功的设定计算，中间文件格式出错过也不丢（同开发版）。格式有误时记下错误、界面只能看不能改；
    /// 文件正被占用时下一轮再读。
    /// </summary>
    private void Reload(bool keepEdits)
    {
        IReadOnlyList<StartupSettingChange> edits = keepEdits ? GetPending() : [];
        _stamp = ReadStamp();

        StartupSettingsState state;
        try
        {
            state = new CocSettingsManager(CocPath).Read();
        }
        catch (Exception ex) when (ex is CocFormatException or IOException or UnauthorizedAccessException)
        {
            if (ex is CocFormatException format)
            {
                _formatError = format;
                _readError = null;
            }
            else
            {
                // 暂时读不了（被占用等）：下一轮看到时间戳不同就会重试
                _readError = ex.Message;
                _stamp = default;
            }

            // 点"重新读取"是要放弃改动：读不了时也退回最后一次读到的值
            if (!keepEdits && _state != null)
                SetUiValues(_state.GetValues());
            else
                UpdateState();
            return;
        }

        _formatError = null;
        _readError = null;
        _state = state;

        Dictionary<string, object> values = state.GetValues();
        foreach (StartupSettingChange edit in edits)
            values[edit.Key.Name] = edit.NewValue;
        SetUiValues(values);
    }

    /// <summary>读 MapExtPDX.log 里最近一次启动的"本会话生效配置"。日志每次启动游戏时重写。</summary>
    private async Task LoadAppliedAsync()
    {
        string log = App.Services.Paths.MapExtLog;
        try
        {
            _applied = await Task.Run(() =>
                File.Exists(log) ? MapExtLogSummary.Parse(LogDiagnostics.ReadLines(log)).AppliedConfig : null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 只是一行参考信息：读不了或日志内容意外时不显示，不影响改设定
            _applied = null;
        }

        UpdateAppliedText();
    }

    // === 界面状态 ===

    private void UpdateState()
    {
        bool usable = IsUsable;
        bool running = _gameRunning == true;
        bool economyOn = _checkBoxes[StartupSettingsText.EconomyMasterKey].IsChecked == true;

        foreach (RadioButton radio in _modeButtons.Values)
            radio.IsEnabled = usable;
        foreach ((string name, CheckBox box) in _checkBoxes)
            box.IsEnabled = usable && (!StartupKeys.FindKey(name)!.EcoGroup || economyOn);
        EconomyOffText.Visibility = usable && !economyOn ? Visibility.Visible : Visibility.Collapsed;
        ValidationWarning.Visibility = _checkBoxes[SaveGateRules.ValidationKey].IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        IReadOnlyList<StartupSettingChange> pending = usable ? GetPending() : [];
        if (!usable)
        {
            // 格式有误时没有可比的基准，整行隐藏
            PendingText.Visibility = Visibility.Collapsed;
        }
        else
        {
            PendingText.Visibility = Visibility.Visible;
            PendingText.DataContext = pending.Count > 0
                ? new ToneLine(Loc.Instance.Format("Startup.Pending", StartupSettingsText.ChangeList(pending, CurrentLanguage)), PackageTone.Warning)
                : new ToneLine(Loc.Instance["Startup.PendingNone"], PackageTone.Muted);
        }

        SaveButton.IsEnabled = usable && !running && pending.Count > 0;
        DefaultsButton.IsEnabled = usable;
        StatusLines.ItemsSource = BuildStatusLines(running);

        bool exists = File.Exists(CocPath);
        FileText.Text = Loc.Instance.Format("Startup.File", CocPath);
        EditLink.IsEnabled = exists;
        ShowFileLink.IsEnabled = exists || Directory.Exists(Path.GetDirectoryName(CocPath));
    }

    private List<ToneLine> BuildStatusLines(bool running)
    {
        string lang = CurrentLanguage;
        var lines = new List<ToneLine>();
        if (_formatError != null)
            lines.Add(new ToneLine(Loc.Instance.Format("Startup.FileError", _formatError.GetMessage(lang)), PackageTone.Error));
        else if (_readError != null)
            lines.Add(new ToneLine(Loc.Instance.Format("Startup.ReadError", _readError), PackageTone.Warning));

        if (running)
            lines.Add(new ToneLine(Loc.Instance["Startup.Running"], PackageTone.Error));
        else if (lines.Count == 0)
            lines.Add(new ToneLine(_note?.Invoke(lang) ?? Loc.Instance["Startup.Ready"], PackageTone.Good));

        if (_state != null && _formatError == null)
        {
            if (!_state.Exists)
                lines.Add(new ToneLine(Loc.Instance["Startup.NoFile"], PackageTone.Muted));
            else if (_state.Block == null)
                lines.Add(new ToneLine(Loc.Instance["Startup.NoBlock"], PackageTone.Muted));
            if (_state.BlockCount > 1)
                lines.Add(new ToneLine(Loc.Instance.Format("Startup.ExtraBlocks", _state.BlockCount), PackageTone.Warning));
            if (StartupSettingsText.InvalidList(_state, lang) is { } invalid)
                lines.Add(new ToneLine(Loc.Instance.Format("Startup.Invalid", invalid), PackageTone.Warning));
        }

        return lines;
    }

    private void UpdateAppliedText()
    {
        if (_applied == null)
        {
            AppliedText.Visibility = Visibility.Collapsed;
            return;
        }

        AppliedText.Text = Loc.Instance.Format("Startup.Applied", _applied.Timestamp, _applied.Raw);
        AppliedText.Visibility = Visibility.Visible;
    }

    // === 按钮与链接 ===

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_state == null || !IsUsable)
            return;

        IReadOnlyList<StartupSettingChange> pending = GetPending();
        try
        {
            CocSaveResult result = new CocSettingsManager(CocPath).Save(_state, GetUiValues());
            switch (result.Status)
            {
                case CocSaveStatus.GameRunning:
                    _gameRunning = true;
                    break;
                case CocSaveStatus.NoChanges:
                    _note = lang => Localized(lang, "Startup.NoChanges");
                    break;
                default:
                    string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                    Reload(keepEdits: false);
                    _note = lang => string.Format(CultureInfo.InvariantCulture, Localized(lang, "Startup.Saved"),
                        time, StartupSettingsText.ChangeList(pending, lang));
                    break;
            }
        }
        catch (Exception ex) when (ex is CocFormatException or CocSaveException or IOException or UnauthorizedAccessException)
        {
            string message = ex switch
            {
                CocFormatException format => format.GetMessage(CurrentLanguage),
                CocSaveException save => save.GetMessage(CurrentLanguage),
                _ => ex.Message,
            };
            ShowMessage(Loc.Instance.Format("Startup.SaveFailed", message), MessageBoxImage.Warning);

            // 文件被外部改过等：重新读取，界面上的改动保留，可以再保存一次
            Reload(keepEdits: true);
        }

        UpdateState();
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        _note = null;
        Reload(keepEdits: false);
        _ = LoadAppliedAsync();
    }

    private void OnDefaultsClick(object sender, RoutedEventArgs e)
    {
        if (IsUsable)
            SetUiValues(CocSettingsManager.GetDefaultValues());
    }

    /// <summary>用记事本打开 .coc；保存后由定时检查发现改动并重新读取。</summary>
    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(CocPath))
            return;
        if (GameProcess.IsRunning()
            && MessageBox.Show(Window.GetWindow(this)!, Loc.Instance["Startup.ConfirmEditWhileRunning"], Loc.Instance["App.Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
        start.ArgumentList.Add(CocPath);
        StartProcess(start);
    }

    private void OnShowFileClick(object sender, RoutedEventArgs e)
    {
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        if (File.Exists(CocPath))
            start.Arguments = "/select,\"" + CocPath + "\"";
        else
            start.ArgumentList.Add(Path.GetDirectoryName(CocPath)!);
        StartProcess(start);
    }

    // === MapExTools 的位置与快捷方式 ===

    private void RefreshInstall()
    {
        string lang = CurrentLanguage;
        InstallLocation location = InstallLocationService.Current;
        string desktop = ToolShortcut.DesktopDir;

        // 取不到桌面目录（极少见）时不提供快捷方式
        ShortcutStatus status = desktop.Length > 0
            ? ToolShortcut.Inspect(desktop, location.ExePath)
            : new ShortcutStatus(ShortcutState.Foreign, ToolShortcut.FileName, null);

        LocationText.Text = InstallText.Location(location, lang);
        ShortcutText.Text = desktop.Length > 0 ? InstallText.Shortcut(status, lang) : string.Empty;
        ShortcutText.Visibility = desktop.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 在临时位置或下载文件夹时只提供固定到本地：快捷方式指向那里，文件被清理或挪走后就失效了
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

    // === 辅助 ===

    private static string Localized(string language, string key) => Core.Localization.Strings.Get(language, key);

    private void StartProcess(ProcessStartInfo start)
    {
        try
        {
            Process.Start(start)?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowMessage(Loc.Instance.Format("Startup.OpenFailed", ex.Message), MessageBoxImage.Warning);
        }
    }

    private void ShowMessage(string text, MessageBoxImage image) =>
        MessageBox.Show(Window.GetWindow(this)!, text, Loc.Instance["App.Title"], MessageBoxButton.OK, image);
}
