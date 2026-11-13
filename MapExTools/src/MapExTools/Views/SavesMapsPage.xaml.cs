using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MapExTools.Core.Coc;
using MapExTools.Core.Game;
using MapExTools.Core.Packages;
using MapExTools.Services;
using MapExtPDX;

namespace MapExTools.Views;

/// <summary>
/// 存档与地图页（计划 4.1）。左边列出 <c>Saves/</c>、<c>Maps/</c> 下的 .cok，右边显示选中项的预览图、元数据，
/// 以及它与设定模式的关系；"切到该模式"只改 <c>MapExtPDX.coc</c> 的模式，读档仍在游戏主菜单里做（D6）。
///
/// <para>扫描在第一次显示、点"重新扫描"、游戏刚退出时进行，都在后台线程。页面可见时每 1.5 秒看一次 .coc 有没有被外部改过
/// （启动页、游戏退出时写回），改过就重新读取设定、重算关系。</para>
/// </summary>
public partial class SavesMapsPage : UserControl
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan s_filterDelay = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _pollTimer = new() { Interval = s_pollInterval };
    private readonly DispatcherTimer _filterTimer = new() { Interval = s_filterDelay };

    private List<PackageListItem> _items = [];
    private PackageScanResult? _scan;
    private string? _scanError;
    private bool _scanning;

    private StartupSettingsState? _cocState;
    private GateSettings? _settings;
    private Exception? _cocError;
    private CocStamp _cocStamp;
    private bool? _gameRunning;

    private PackageListItem? _shownItem;
    private PreviewLoad? _previewLoad;
    private CancellationTokenSource? _previewCts;

    // 重建列表或填充下拉框时会触发 SelectionChanged，这段时间不当作用户操作
    private bool _populating;

    public SavesMapsPage()
    {
        InitializeComponent();

        _pollTimer.Tick += (_, _) => OnPollTick();
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            RebuildVisible();
        };

        // 切换标签页时本页会卸载、重新载入：扫描结果保留，只在可见时轮询
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private enum RelationFilter
    {
        All,
        Match,
        Convertible,
        Mismatch,
        Other,
        Damaged,
    }

    private sealed record Choice<T>(T Value, string Display);

    /// <summary>.coc 的存在与否、修改时间与长度，用来发现外部改动。</summary>
    private readonly record struct CocStamp(bool Exists, DateTime LastWriteUtc, long Length);

    private static string CurrentLanguage => Loc.Instance.Language;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        PopulateRelationBox();
        ReloadSettings();
        ApplySettings();

        // 切走期间游戏退出了，也要重新扫描
        bool running = GameProcess.IsRunning();
        bool gameExited = _gameRunning == true && !running;
        _gameRunning = running;
        _pollTimer.Start();

        if (_scan == null || gameExited)
            _ = RescanAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _pollTimer.Stop();
        _filterTimer.Stop();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PopulateRelationBox();
        ApplySettings();
    }

    private void OnPollTick()
    {
        if (ReadCocStamp() != _cocStamp)
        {
            ReloadSettings();
            ApplySettings();
        }

        // 游戏刚退出：可能有新存档
        bool running = GameProcess.IsRunning();
        if (_gameRunning == true && !running)
            _ = RescanAsync();
        _gameRunning = running;
    }

    // === 设定 ===

    private CocStamp ReadCocStamp()
    {
        var info = new FileInfo(App.Services.Paths.CocFile);
        return info.Exists ? new CocStamp(true, info.LastWriteTimeUtc, info.Length) : default;
    }

    /// <summary>重新读 .coc。格式有误或读不了时设定为 null，关系一律显示为未知。</summary>
    private void ReloadSettings()
    {
        _cocStamp = ReadCocStamp();
        try
        {
            _cocState = new CocSettingsManager(App.Services.Paths.CocFile).Read();
            _settings = GateSettings.FromCoc(_cocState);
            _cocError = null;
        }
        catch (Exception ex) when (ex is CocFormatException or IOException or UnauthorizedAccessException)
        {
            _cocState = null;
            _settings = null;
            _cocError = ex;
        }
    }

    /// <summary>按当前设定与语言刷新全部文字与关系。</summary>
    private void ApplySettings()
    {
        foreach (PackageListItem item in _items)
            item.Update(_settings, CurrentLanguage);

        UpdateHeader();
        UpdateToggleTexts();
        RebuildVisible();
    }

    private void UpdateHeader()
    {
        if (_settings == null)
        {
            string message = _cocError switch
            {
                CocFormatException format => format.GetMessage(CurrentLanguage),
                { } other => other.Message,
                null => string.Empty,
            };
            HeaderText.Text = Loc.Instance.Format("Saves.CocError", message);
            HeaderNote.Visibility = Visibility.Collapsed;
            return;
        }

        HeaderText.Text = PackageText.Header(_settings, CurrentLanguage);
        var notes = new List<string>();
        if (_cocState is { Exists: false })
            notes.Add(Loc.Instance["Saves.CocDefault"]);
        if (_settings.ValidationDisabled)
            notes.Add(Loc.Instance["Saves.ValidationOff"]);
        HeaderNote.Text = string.Join(Environment.NewLine, notes);
        HeaderNote.Visibility = notes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // === 扫描 ===

    private async Task RescanAsync()
    {
        if (_scanning)
            return;

        _scanning = true;
        RescanButton.IsEnabled = false;
        UpdateStatus();
        try
        {
            var paths = App.Services.Paths;
            _scan = await Task.Run(() => PackageScanner.Scan(paths));
            _scanError = null;
            _items = _scan.Packages
                .Select(p => new PackageListItem(p))
                .OrderByDescending(i => i.Package.LastWriteTime)
                .ThenBy(i => i.Package.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _scanError = ex.Message;
        }
        finally
        {
            _scanning = false;
            RescanButton.IsEnabled = true;
        }

        ApplySettings();
    }

    private async void OnRescanClick(object sender, RoutedEventArgs e)
    {
        ReloadSettings();
        await RescanAsync();
    }

    private void UpdateToggleTexts()
    {
        SavesToggle.Content = Loc.Instance.Format("Saves.Tab.Saves", _items.Count(i => i.InSavesTab));
        MapsToggle.Content = Loc.Instance.Format("Saves.Tab.Maps", _items.Count(i => !i.InSavesTab));
    }

    private void UpdateStatus()
    {
        if (_scanning)
        {
            StatusText.Text = Loc.Instance["Saves.Scanning"];
        }
        else if (_scanError != null)
        {
            StatusText.Text = Loc.Instance.Format("Saves.ScanFailed", _scanError);
        }
        else if (_scan != null)
        {
            IReadOnlyList<CokPackageInfo> all = _scan.Packages;
            StatusText.Text = Loc.Instance.Format("Saves.Status",
                all.Count,
                all.Count(p => p.Kind == PackageKind.Save),
                all.Count(p => p.Kind == PackageKind.Map),
                all.Count(p => p.Problem != PackageProblem.None),
                all.Count(p => p.DuplicatePaths.Count > 0),
                _scan.Elapsed.TotalSeconds);
        }
    }

    // === 列表与筛选 ===

    private void PopulateRelationBox()
    {
        _populating = true;
        int index = Math.Max(RelationBox.SelectedIndex, 0);
        RelationBox.ItemsSource = new[]
        {
            new Choice<RelationFilter>(RelationFilter.All, Loc.Instance["Saves.Filter.All"]),
            new Choice<RelationFilter>(RelationFilter.Match, Loc.Instance["Saves.Filter.Match"]),
            new Choice<RelationFilter>(RelationFilter.Convertible, Loc.Instance["Saves.Filter.Convertible"]),
            new Choice<RelationFilter>(RelationFilter.Mismatch, Loc.Instance["Saves.Filter.Mismatch"]),
            new Choice<RelationFilter>(RelationFilter.Other, Loc.Instance["Saves.Filter.Other"]),
            new Choice<RelationFilter>(RelationFilter.Damaged, Loc.Instance["Saves.Filter.Damaged"]),
        };
        RelationBox.SelectedIndex = index;
        _populating = false;
    }

    /// <summary>按存档／地图、关系与关键词重建列表，尽量保留选中项。</summary>
    private void RebuildVisible()
    {
        string? keepPath = _shownItem?.Package.Path;
        bool saves = SavesToggle.IsChecked == true;
        RelationFilter filter = (RelationBox.SelectedItem as Choice<RelationFilter>)?.Value ?? RelationFilter.All;
        string keyword = KeywordBox.Text.Trim();

        List<PackageListItem> visible = _items.Where(i => i.InSavesTab == saves && Matches(i.Relation, filter) && i.Matches(keyword)).ToList();
        PackageListItem? keep = keepPath == null
            ? null
            : visible.FirstOrDefault(i => string.Equals(i.Package.Path, keepPath, StringComparison.OrdinalIgnoreCase));

        _populating = true;
        PackageList.ItemsSource = visible;
        PackageList.SelectedItem = keep;
        _populating = false;

        if (keep != null)
            PackageList.ScrollIntoView(keep);
        ShowDetails(keep);
        UpdateStatus();
    }

    private static bool Matches(PackageRelation? relation, RelationFilter filter) => filter switch
    {
        RelationFilter.Match => relation == PackageRelation.Match,
        RelationFilter.Convertible => relation is PackageRelation.Convertible or PackageRelation.ConvertibleWhenEnabled,
        RelationFilter.Mismatch => relation is PackageRelation.Mismatch or PackageRelation.Unchecked,
        RelationFilter.Other => relation is PackageRelation.Stretched or PackageRelation.Unknown,
        RelationFilter.Damaged => relation == null,
        _ => true,
    };

    private void OnKindChecked(object sender, RoutedEventArgs e)
    {
        // XAML 加载时 IsChecked="True" 就会触发，那时其他控件还没建好
        if (IsLoaded)
            RebuildVisible();
    }

    private void OnRelationFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_populating && IsLoaded)
            RebuildVisible();
    }

    private void OnKeywordChanged(object sender, TextChangedEventArgs e)
    {
        // 停止输入 0.3 秒后再筛
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_populating)
            ShowDetails(PackageList.SelectedItem as PackageListItem);
    }

    // === 详情 ===

    /// <summary>显示选中项；同一项只刷新文字，不重新读预览图。</summary>
    private void ShowDetails(PackageListItem? item)
    {
        bool sameItem = ReferenceEquals(item, _shownItem);
        _shownItem = item;
        NoSelectionText.Visibility = item == null ? Visibility.Visible : Visibility.Collapsed;
        DetailsScroll.Visibility = item == null ? Visibility.Collapsed : Visibility.Visible;
        if (item == null)
        {
            CancelPreview();
            PreviewImage.Source = null;
            _previewLoad = null;
            return;
        }

        DetailTitle.Text = item.Title;
        DetailLines.ItemsSource = PackageText.Details(item.Package, _settings, CurrentLanguage, App.Services.Paths.Root);
        UpdateSwitchButton(item);

        if (sameItem)
            RenderPreviewCaption(item);
        else
            LoadPreview(item);
    }

    private async void LoadPreview(PackageListItem item)
    {
        CancelPreview();
        PreviewImage.Source = null;
        _previewLoad = null;
        RenderPreviewCaption(item);
        if (item.Package.PreviewEntry == null)
            return;

        var cts = new CancellationTokenSource();
        _previewCts = cts;
        try
        {
            PreviewLoad load = await PreviewBitmaps.LoadAsync(item.Package, maxWidth: null, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(item, _shownItem))
                return;

            _previewLoad = load;
            PreviewImage.Source = load.Bitmap;
            RenderPreviewCaption(item);
        }
        catch (OperationCanceledException)
        {
            // 已换了选中项
        }
    }

    private void CancelPreview()
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
    }

    private void RenderPreviewCaption(PackageListItem item)
    {
        PreviewCaption.DataContext = PackageText.Preview(item.Package, _previewLoad?.Result, _previewLoad?.Error, CurrentLanguage);
        bool loading = item.Package.PreviewEntry != null && _previewLoad == null;
        PreviewPlaceholder.Text = PreviewImage.Source != null
            ? string.Empty
            : Loc.Instance[loading ? "Saves.Preview.Loading" : "Saves.NoPreview"];
    }

    private void UpdateSwitchButton(PackageListItem item)
    {
        StartupModeInfo? target = SaveGateRules.GetTargetMode(item.Package);
        if (target == null)
        {
            SwitchButton.Content = Loc.Instance["Saves.SwitchNone"];
            SwitchButton.IsEnabled = false;
        }
        else if (_settings != null && target.Name == _settings.ModeName)
        {
            SwitchButton.Content = Loc.Instance["Saves.SwitchCurrent"];
            SwitchButton.IsEnabled = false;
        }
        else
        {
            SwitchButton.Content = Loc.Instance.Format("Saves.SwitchTo", PackageText.ModeLabel(target, CurrentLanguage));
            SwitchButton.IsEnabled = _cocState != null;
        }
    }

    // === 两个按钮 ===

    /// <summary>"切到该模式"：确认后只改 .coc 里的模式，游戏运行中拒绝写入（游戏退出时会写回设定）。</summary>
    private void OnSwitchClick(object sender, RoutedEventArgs e)
    {
        if (_shownItem == null || _cocState == null || _settings == null || SaveGateRules.GetTargetMode(_shownItem.Package) is not { } target)
            return;

        string from = PackageText.CoreValueLabel(_settings.CurrentCoreValue, CurrentLanguage);
        string to = PackageText.ModeLabel(target, CurrentLanguage);
        Window? owner = Window.GetWindow(this);
        if (MessageBox.Show(owner!, Loc.Instance.Format("Saves.SwitchConfirm", from, to), Loc.Instance["App.Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var manager = new CocSettingsManager(App.Services.Paths.CocFile);
            CocSaveResult result = manager.Save(_cocState, new Dictionary<string, object> { [StartupKeys.ModeKey] = target.Name });
            if (result.Status == CocSaveStatus.GameRunning)
                ShowMessage(Loc.Instance["Saves.SwitchGameRunning"], MessageBoxImage.Warning);
            else
                ShowMessage(Loc.Instance.Format("Saves.SwitchDone", to), MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is CocFormatException or CocSaveException or IOException or UnauthorizedAccessException)
        {
            string message = ex switch
            {
                CocFormatException format => format.GetMessage(CurrentLanguage),
                CocSaveException save => save.GetMessage(CurrentLanguage),
                _ => ex.Message,
            };
            ShowMessage(Loc.Instance.Format("Saves.SwitchFailed", message), MessageBoxImage.Warning);
        }

        ReloadSettings();
        ApplySettings();
    }

    private void OnShowInExplorerClick(object sender, RoutedEventArgs e)
    {
        if (_shownItem == null)
            return;

        try
        {
            Process.Start("explorer.exe", "/select,\"" + _shownItem.Package.Path + "\"")?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowMessage(ex.Message, MessageBoxImage.Warning);
        }
    }

    private void ShowMessage(string text, MessageBoxImage image) =>
        MessageBox.Show(Window.GetWindow(this)!, text, Loc.Instance["App.Title"], MessageBoxButton.OK, image);
}
