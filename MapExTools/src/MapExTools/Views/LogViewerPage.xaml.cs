using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MapExTools.Core.Diagnostics;
using MapExTools.Core.Game;
using MapExTools.Services;

namespace MapExTools.Views;

/// <summary>
/// 日志诊断页（计划 3.1、3.2）。上半部分是会话摘要，每次切到本页、点刷新、游戏刚退出时重新生成；
/// 下半部分浏览一个日志，可见时每 1.5 秒增量读取新内容，按级别着色，可按级别与关键词筛选。
/// 读文件都在后台线程做，界面线程只处理结果。
/// </summary>
public partial class LogViewerPage : UserControl
{
    /// <summary>日志浏览最多保留的行数，超出时丢掉最早的。</summary>
    private const int MaxLines = 200_000;

    /// <summary>一次追加超过这么多行时整表重建，比逐行通知快。</summary>
    private const int RebuildThreshold = 500;

    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan s_filterDelay = TimeSpan.FromMilliseconds(300);

    private readonly DispatcherTimer _pollTimer = new() { Interval = s_pollInterval };
    private readonly DispatcherTimer _filterTimer = new() { Interval = s_filterDelay };
    private readonly List<LogLine> _allLines = [];
    private ObservableCollection<LogLine> _visible = [];

    private LogTail? _tail;
    private LogLineClassifier _classifier = new();
    private int _lineNumber;
    private bool _truncated;
    private bool? _exists;
    private string? _readError;
    private bool _polling;

    private DiagnosticsReport? _report;
    private Task? _collectTask;
    private bool? _gameRunning;

    // 填充下拉框时会触发 SelectionChanged，这段时间不当作用户操作
    private bool _populating;

    public LogViewerPage()
    {
        InitializeComponent();
        LinesList.ItemsSource = _visible;

        _pollTimer.Tick += (_, _) => OnPollTick();
        _filterTimer.Tick += (_, _) =>
        {
            _filterTimer.Stop();
            RebuildVisible();
        };

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyLines,
            (_, e) => e.CanExecute = LinesList.SelectedItems.Count > 0));

        // 切换标签页时本页会卸载、重新载入：只在可见时读文件
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private enum LevelFilter
    {
        All,
        WarningOrWorse,
        ErrorOnly,
    }

    private sealed record Choice<T>(T Value, string Display);

    /// <summary>游戏目录下的 crash.log；游戏路径未知时为 null（路径由启动服务定位，计划 2.2）。</summary>
    private static string? CrashLogPath => LogSources.GetCrashLogPath(App.Services.GameLocation.Game?.ExePath);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        PopulateLevelBox();
        PopulateLogBox();
        _pollTimer.Start();
        _ = RefreshSummaryAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _pollTimer.Stop();
        if (_filterTimer.IsEnabled)
        {
            _filterTimer.Stop();
            RebuildVisible();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PopulateLevelBox();
        RenderSummary();
        UpdateStatus();
    }

    private void OnPollTick()
    {
        PollNow();

        // 游戏刚退出：日志都写完了，重新生成摘要
        bool running = GameProcess.IsRunning();
        if (_gameRunning == true && !running)
            _ = RefreshSummaryAsync();
        _gameRunning = running;
    }

    // === 会话摘要 ===

    private Task RefreshSummaryAsync()
    {
        if (_collectTask is { IsCompleted: false } running)
            return running;

        _collectTask = RunCollectAsync();
        return _collectTask;
    }

    private async Task RunCollectAsync()
    {
        try
        {
            var paths = App.Services.Paths;
            bool running = GameProcess.IsRunning();
            _gameRunning = running;
            _report = await Task.Run(() => LogDiagnostics.Collect(paths, running));
            RenderSummary();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SummaryNote.Text = Loc.Instance.Format("Logs.RefreshFailed", ex.Message);
        }
    }

    private void RenderSummary()
    {
        if (_report == null)
            return;

        // Build 的顺序：上次运行、MapExt、启动
        IReadOnlyList<SummarySection> sections = DiagnosticsSummaryText.Build(_report, Loc.Instance.Language, CrashLogPath);
        LeftSections.ItemsSource = new[] { sections[0], sections[2] };
        RightSections.ItemsSource = new[] { sections[1] };
        SummaryNote.Text = Loc.Instance.Format("Logs.Generated",
            _report.GeneratedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            PopulateLogBox();
            await RefreshSummaryAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            SummaryNote.Text = Loc.Instance.Format("Logs.RefreshFailed", ex.Message);
        }
    }

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await RefreshSummaryAsync();
            if (_report == null)
                return;

            string language = Loc.Instance.Language;
            string markdown = DiagnosticsSummaryText.ToMarkdown(
                _report,
                DiagnosticsSummaryText.Build(_report, language, CrashLogPath),
                language,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            Clipboard.SetText(markdown);
            SummaryNote.Text = Loc.Instance["Logs.Copied"];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ShowWarning(Loc.Instance.Format("Logs.CopyFailed", ex.Message));
        }
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        string dir = App.Services.Paths.LogsDir;
        if (!Directory.Exists(dir))
        {
            ShowWarning(Loc.Instance.Format("Logs.FolderMissing", dir));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void ShowWarning(string text)
    {
        if (Window.GetWindow(this) is { } owner)
            MessageBox.Show(owner, text, Loc.Instance["App.Title"], MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            MessageBox.Show(text, Loc.Instance["App.Title"], MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    // === 日志浏览 ===

    private void PopulateLevelBox()
    {
        _populating = true;
        int index = Math.Max(LevelBox.SelectedIndex, 0);
        LevelBox.ItemsSource = new[]
        {
            new Choice<LevelFilter>(LevelFilter.All, Loc.Instance["Logs.Level.All"]),
            new Choice<LevelFilter>(LevelFilter.WarningOrWorse, Loc.Instance["Logs.Level.Warning"]),
            new Choice<LevelFilter>(LevelFilter.ErrorOnly, Loc.Instance["Logs.Level.Error"]),
        };
        LevelBox.SelectedIndex = index;
        _populating = false;
    }

    /// <summary>重新列出日志文件（新出现的 *.log 也会列上），保留当前选中的那个。</summary>
    private void PopulateLogBox()
    {
        IReadOnlyList<LogSource> sources = LogSources.Enumerate(App.Services.Paths, App.Services.GameLocation.Game?.ExePath);
        string? current = (LogBox.SelectedItem as LogSource)?.Path ?? _tail?.Path;

        _populating = true;
        LogBox.ItemsSource = sources;
        int index = -1;
        for (int i = 0; i < sources.Count && current != null; i++)
        {
            if (string.Equals(sources[i].Path, current, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        LogBox.SelectedIndex = index >= 0 ? index : 0;
        _populating = false;
        OpenSelectedLog();
    }

    private void OnLogChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_populating)
            OpenSelectedLog();
    }

    /// <summary>选中的日志与正在读的不同时，从头读新的那个；相同时只补读新增内容。</summary>
    private void OpenSelectedLog()
    {
        if (LogBox.SelectedItem is not LogSource source)
            return;

        if (_tail == null || !string.Equals(_tail.Path, source.Path, StringComparison.OrdinalIgnoreCase))
        {
            _tail = new LogTail(source.Path);
            _allLines.Clear();
            _classifier = new LogLineClassifier();
            _lineNumber = 0;
            _truncated = false;
            _exists = null;
            _readError = null;
            RebuildVisible();
        }

        PollNow();
    }

    private async void PollNow()
    {
        LogTail? tail = _tail;
        if (tail == null || _polling)
            return;

        _polling = true;
        try
        {
            LogTailResult result = await Task.Run(tail.Poll);
            if (tail == _tail)
            {
                _readError = null;
                AppendLines(result);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (tail == _tail)
            {
                _readError = ex.Message;
                UpdateStatus();
            }
        }
        finally
        {
            _polling = false;
        }

        // 读的过程中切换了日志：马上读新选的那个，不等下一次定时
        if (tail != _tail)
            PollNow();
    }

    private void AppendLines(LogTailResult result)
    {
        _exists = result.Exists;
        if (result.Reset)
        {
            _allLines.Clear();
            _classifier = new LogLineClassifier();
            _lineNumber = 0;
            _truncated = false;
        }

        var added = new List<LogLine>(result.Lines.Count);
        foreach (string text in result.Lines)
            added.Add(new LogLine(++_lineNumber, text, _classifier.Classify(text)));
        _allLines.AddRange(added);

        bool rebuild = result.Reset || added.Count > RebuildThreshold;
        if (_allLines.Count > MaxLines)
        {
            _allLines.RemoveRange(0, _allLines.Count - MaxLines);
            _truncated = true;
            rebuild = true;
        }

        if (rebuild)
        {
            RebuildVisible();
            return;
        }

        Func<LogLine, bool> filter = CreateFilter();
        foreach (LogLine line in added)
        {
            if (filter(line))
                _visible.Add(line);
        }

        if (added.Count > 0)
            ScrollToEndIfFollowing();
        UpdateStatus();
    }

    private void RebuildVisible()
    {
        _visible = new ObservableCollection<LogLine>(_allLines.Where(CreateFilter()));
        LinesList.ItemsSource = _visible;
        ScrollToEndIfFollowing();
        UpdateStatus();
    }

    private Func<LogLine, bool> CreateFilter()
    {
        LevelFilter level = (LevelBox.SelectedItem as Choice<LevelFilter>)?.Value ?? LevelFilter.All;
        string keyword = KeywordBox.Text.Trim();
        return line =>
            level switch
            {
                LevelFilter.WarningOrWorse => line.Level is LogLevelKind.Warning or LogLevelKind.Error,
                LevelFilter.ErrorOnly => line.Level == LogLevelKind.Error,
                _ => true,
            }
            && (keyword.Length == 0 || line.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private void ScrollToEndIfFollowing()
    {
        if (FollowBox.IsChecked != true || _visible.Count == 0)
            return;

        // 刚换了数据源时列表还没排版，等排版完成再滚动，否则停在开头
        LogLine last = _visible[^1];
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (FollowBox.IsChecked == true && _visible.Count > 0 && ReferenceEquals(_visible[^1], last))
                LinesList.ScrollIntoView(last);
        });
    }

    private void UpdateStatus()
    {
        string path = _tail?.Path ?? string.Empty;
        if (_readError != null)
        {
            StatusText.Text = Loc.Instance.Format("Logs.StatusError", path, _readError);
        }
        else if (_exists == false)
        {
            StatusText.Text = Loc.Instance.Format("Logs.StatusMissing", path);
        }
        else if (_exists == true)
        {
            string text = Loc.Instance.Format("Logs.Status", _visible.Count, _allLines.Count, path);
            StatusText.Text = _truncated ? text + Loc.Instance.Format("Logs.Truncated", MaxLines) : text;
        }
        else
        {
            StatusText.Text = string.Empty;
        }
    }

    private void OnLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_populating)
            RebuildVisible();
    }

    private void OnKeywordChanged(object sender, TextChangedEventArgs e)
    {
        // 停止输入 0.3 秒后再筛，免得每敲一个字都重建列表
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void OnFollowClick(object sender, RoutedEventArgs e) => ScrollToEndIfFollowing();

    /// <summary>Ctrl+C：按行号顺序复制选中的行。</summary>
    private void OnCopyLines(object sender, ExecutedRoutedEventArgs e)
    {
        string text = string.Join(Environment.NewLine,
            LinesList.SelectedItems.Cast<LogLine>().OrderBy(l => l.Number).Select(l => l.Text));
        try
        {
            Clipboard.SetText(text);
        }
        catch (ExternalException ex)
        {
            ShowWarning(Loc.Instance.Format("Logs.CopyFailed", ex.Message));
        }
    }
}
