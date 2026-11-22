using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Services;

/// <summary>
/// 窗口级共享的后台模组与冲突预检服务（激活态与预览态双轨隔离）：
/// <list type="bullet">
/// <item><see cref="Report"/> 与 <see cref="EvaluateWith"/> 始终锚定游戏当前激活 Playset，专供顶层页签角标与「启动与设定」页拦截使用；</item>
/// <item><see cref="PreviewReport"/>、<see cref="EvaluatePreviewWith"/> 与 <see cref="SelectPlayset"/> 供「模组与冲突预检」页切换浏览任意本地播放集（选中激活项时零开销复用主工作区）；</item>
/// <item>磁盘读取与 <c>MetadataReader</c> 扫描统一放入后台 <see cref="Task.Run"/> 执行，跨播放集切换按文件指纹缓存字节码解析结果，并在磁盘快照变化或手动重扫时自动清空。</item>
/// </list>
/// </summary>
public sealed class ConflictScanService
{
    private readonly UserDataPaths _paths;
    private readonly CocSession _coc;
    private readonly Func<string?> _gameDirProvider;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<AssemblyFileFingerprint, AssemblyInspectionResult?> _inspectionCache = new();

    private DiskStamp _lastStamp;
    private ScannedModWorkspace? _workspace;
    private ScannedModWorkspace? _previewWorkspace;
    private string? _selectedPlaysetId;
    private long _previewRequestId;
    private bool _isScanning;
    private bool _isPreviewScanning;
    private bool _rescanQueued;
    private bool _hasAttemptedOnce;
    private bool _stampChecking;

    public ConflictScanService(UserDataPaths paths, CocSession coc, Func<string?> gameDirProvider)
    {
        _paths = paths;
        _coc = coc;
        _gameDirProvider = gameDirProvider;
        _coc.Changed += OnCocChanged;
    }

    private readonly record struct FileStamp(bool Exists, DateTime LastWriteUtc, long Length);

    private readonly record struct DiskStamp(
        FileStamp PlaysetConfig,
        FileStamp PlaysetSync,
        FileStamp ModsDirectory,
        FileStamp PdxCache,
        long ModsFolderHash,
        string? GameDir);

    /// <summary>扫描状态或评估报告更新时在 UI 线程触发。</summary>
    public event EventHandler? Changed;

    public bool IsScanning
    {
        get { lock (_gate) return _isScanning; }
    }

    /// <summary>「模组与冲突预检」页当前预览目标是否正在扫描中。</summary>
    public bool IsPreviewScanning
    {
        get
        {
            lock (_gate)
            {
                return _selectedPlaysetId == null ? _isScanning : (_isPreviewScanning || _isScanning);
            }
        }
    }

    /// <summary>当前预览页选中的非激活播放集本地 ID；为 <c>null</c> 表示自动跟随当前激活播放集。</summary>
    public string? SelectedPlaysetId
    {
        get { lock (_gate) return _selectedPlaysetId; }
    }

    /// <summary>当前激活播放集的评估报告（供主窗口顶层角标与启动页拦截读取）。</summary>
    public ConflictReport? Report { get; private set; }

    /// <summary>「模组与冲突预检」页所选目标播放集的评估报告（跟随激活项时与 <see cref="Report"/> 相同）。</summary>
    public ConflictReport? PreviewReport { get; private set; }

    public string? LastScanError { get; private set; }

    public string? LastPreviewError { get; private set; }

    /// <summary>主动请求一次全量后台重扫（首次启动窗口或用户点击“重新扫描模组”时调用）。</summary>
    public void TriggerScan()
    {
        _inspectionCache.Clear();
        lock (_gate)
        {
            if (_isScanning)
            {
                _rescanQueued = true;
                return;
            }

            _isScanning = true;
            _rescanQueued = false;
        }

        NotifyChangedOnUiThread();
        _ = RunScanLoopAsync();
    }

    /// <summary>切换「模组与冲突预检」页预览的目标播放集（传 <c>null</c> 或当前激活播放集 ID 时归位为自动跟随激活项）。</summary>
    public void SelectPlayset(string? playsetId)
    {
        string? normalized = string.IsNullOrWhiteSpace(playsetId) ? null : playsetId.Trim();
        ScannedModWorkspace? activeWs;
        long requestId;

        lock (_gate)
        {
            activeWs = _workspace;
            if (normalized != null &&
                activeWs?.Playset.ActivePlaysetId is { } activeId &&
                string.Equals(normalized, activeId, StringComparison.OrdinalIgnoreCase))
            {
                normalized = null;
            }

            if (string.Equals(_selectedPlaysetId, normalized, StringComparison.OrdinalIgnoreCase) &&
                (normalized == null || _previewWorkspace != null || _isPreviewScanning))
            {
                return;
            }

            _selectedPlaysetId = normalized;
            requestId = ++_previewRequestId;

            if (normalized == null)
            {
                _isPreviewScanning = false;
                _previewWorkspace = activeWs;
            }
            else
            {
                _isPreviewScanning = true;
                _previewWorkspace = null;
            }
        }

        if (normalized == null)
        {
            DispatchToUi(() =>
            {
                LastPreviewError = null;
                PreviewReport = Report;
                Changed?.Invoke(this, EventArgs.Empty);
            });
            return;
        }

        string? gameDir = _gameDirProvider();
        DispatchToUi(() =>
        {
            LastPreviewError = null;
            PreviewReport = null;
            Changed?.Invoke(this, EventArgs.Empty);
        });
        _ = RunPreviewScanAsync(normalized, requestId, gameDir);
    }

    /// <summary>由主窗口 1.5 秒定时器调用：仅在后台比对轻量目录/配置文件时间戳，变化时才排入后台扫描。</summary>
    public void Poll()
    {
        bool hasAttempted;
        lock (_gate)
        {
            if (_isScanning || _stampChecking)
                return;

            hasAttempted = _workspace != null || _hasAttemptedOnce;
            if (hasAttempted)
                _stampChecking = true;
        }

        if (!hasAttempted)
        {
            TriggerScan();
            return;
        }

        // 在 UI 线程先读好 GameDir 传入后台，避免与 AppServices.RefreshGameLocation 跨线程读写 GameLocation
        string? gameDir = _gameDirProvider();
        _ = CheckDiskStampAsync(gameDir);
    }

    private async Task CheckDiskStampAsync(string? gameDir)
    {
        try
        {
            DiskStamp current = await Task.Run(() => CaptureDiskStamp(gameDir));
            bool changed;
            lock (_gate)
            {
                _stampChecking = false;
                changed = !_isScanning && current != _lastStamp;
            }

            if (changed)
                DispatchToUi(TriggerScan);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            lock (_gate)
            {
                _stampChecking = false;
            }
        }
    }

    /// <summary>按任意指定的设定快照对当前激活播放集工作区执行内存级即时评估（供启动前按已保存的磁盘设定核对）。</summary>
    public ConflictReport? EvaluateWith(ConflictEvalSettings settings)
    {
        ScannedModWorkspace? ws;
        lock (_gate)
        {
            ws = _workspace;
        }

        return ws == null ? null : ConflictAnalyzer.Evaluate(ws, settings);
    }

    /// <summary>按任意指定的设定快照对「模组与冲突预检」页当前预览工作区执行内存级即时评估。</summary>
    public ConflictReport? EvaluatePreviewWith(ConflictEvalSettings settings)
    {
        ScannedModWorkspace? ws;
        lock (_gate)
        {
            ws = _selectedPlaysetId == null ? _workspace : _previewWorkspace;
        }

        return ws == null ? null : ConflictAnalyzer.Evaluate(ws, settings);
    }

    private void OnCocChanged(object? sender, EventArgs e)
    {
        ScannedModWorkspace? activeWs;
        ScannedModWorkspace? previewWs;
        bool followActive;
        lock (_gate)
        {
            activeWs = _workspace;
            followActive = _selectedPlaysetId == null;
            previewWs = followActive ? activeWs : _previewWorkspace;
        }

        if (activeWs == null && previewWs == null)
            return;

        ConflictEvalSettings evalSettings = ConflictEvalSettings.FromDictionary(_coc.WorkingValues);
        if (activeWs != null)
            Report = ConflictAnalyzer.Evaluate(activeWs, evalSettings);

        PreviewReport = followActive
            ? Report
            : (previewWs != null ? ConflictAnalyzer.Evaluate(previewWs, evalSettings) : null);
        NotifyChangedOnUiThread();
    }

    private async Task RunScanLoopAsync()
    {
        while (true)
        {
            string? gameDir = _gameDirProvider();
            DiskStamp stampBefore = await Task.Run(() => CaptureDiskStamp(gameDir));
            lock (_gate)
            {
                _lastStamp = stampBefore;
            }

            ScannedModWorkspace workspace;
            try
            {
                workspace = await Task.Run(() => ConflictAnalyzer.ScanWorkspace(_paths, gameDir, null, _inspectionCache));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                lock (_gate)
                {
                    _lastStamp = stampBefore;
                    _hasAttemptedOnce = true;
                    _isScanning = false;
                    _rescanQueued = false;
                }

                string errMsg = ex.Message;
                DispatchToUi(() =>
                {
                    LastScanError = errMsg;
                    if (SelectedPlaysetId == null)
                        LastPreviewError = errMsg;
                    Changed?.Invoke(this, EventArgs.Empty);
                });
                return;
            }

            bool runAgain;
            string? previewToRefresh = null;
            long previewReqId = 0;

            lock (_gate)
            {
                _workspace = workspace;
                _lastStamp = stampBefore;
                _hasAttemptedOnce = true;

                // 若此前正在预览某个非激活播放集：检查它是否仍在列表中且仍处于非激活状态；若已删除或已成为新的激活项则自动归位
                if (_selectedPlaysetId != null)
                {
                    PlaysetSummary? stillValidInactive = workspace.Playset.EffectiveAvailablePlaysets
                        .FirstOrDefault(p => !p.IsActive && string.Equals(p.Id, _selectedPlaysetId, StringComparison.OrdinalIgnoreCase));
                    if (stillValidInactive == null)
                    {
                        _selectedPlaysetId = null;
                        _isPreviewScanning = false;
                        _previewWorkspace = workspace;
                        ++_previewRequestId;
                    }
                    else
                    {
                        previewToRefresh = stillValidInactive.Id;
                        _selectedPlaysetId = previewToRefresh;
                        _isPreviewScanning = true;
                        previewReqId = ++_previewRequestId;
                    }
                }
                else
                {
                    _previewWorkspace = workspace;
                }

                if (_rescanQueued)
                {
                    _rescanQueued = false;
                    runAgain = true;
                }
                else
                {
                    _isScanning = false;
                    runAgain = false;
                }
            }

            // 在 UI 线程读取最新 Coc 工作副本完成瞬时定级并广播
            bool followActiveNow = previewToRefresh == null;
            DispatchToUi(() =>
            {
                LastScanError = null;
                ConflictEvalSettings evalSettings = ConflictEvalSettings.FromDictionary(_coc.WorkingValues);
                ConflictReport activeReport = ConflictAnalyzer.Evaluate(workspace, evalSettings);
                Report = activeReport;
                if (followActiveNow)
                {
                    LastPreviewError = null;
                    PreviewReport = activeReport;
                }

                Changed?.Invoke(this, EventArgs.Empty);
            });

            if (!runAgain)
            {
                if (previewToRefresh != null)
                    _ = RunPreviewScanAsync(previewToRefresh, previewReqId, gameDir);
                break;
            }
        }
    }

    private async Task RunPreviewScanAsync(string targetPlaysetId, long requestId, string? gameDir)
    {
        ScannedModWorkspace previewWs;
        try
        {
            previewWs = await Task.Run(() => ConflictAnalyzer.ScanWorkspace(_paths, gameDir, targetPlaysetId, _inspectionCache));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            bool stillCurrent;
            lock (_gate)
            {
                stillCurrent = requestId == _previewRequestId &&
                               string.Equals(_selectedPlaysetId, targetPlaysetId, StringComparison.OrdinalIgnoreCase);
                if (stillCurrent)
                    _isPreviewScanning = false;
            }

            if (stillCurrent)
            {
                string err = ex.Message;
                DispatchToUi(() =>
                {
                    LastPreviewError = err;
                    Changed?.Invoke(this, EventArgs.Empty);
                });
            }

            return;
        }

        bool apply;
        lock (_gate)
        {
            apply = requestId == _previewRequestId &&
                    string.Equals(_selectedPlaysetId, targetPlaysetId, StringComparison.OrdinalIgnoreCase);
            if (apply)
            {
                _previewWorkspace = previewWs;
                _isPreviewScanning = false;
            }
        }

        if (!apply)
            return;

        DispatchToUi(() =>
        {
            LastPreviewError = null;
            ConflictEvalSettings evalSettings = ConflictEvalSettings.FromDictionary(_coc.WorkingValues);
            PreviewReport = ConflictAnalyzer.Evaluate(previewWs, evalSettings);
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private DiskStamp CaptureDiskStamp(string? gameDir)
    {
        return new DiskStamp(
            ReadFileStamp(_paths.PlaysetConfigFile),
            ReadFileStamp(_paths.PlaysetSyncFile),
            ReadFileStamp(_paths.ModsDirectoryFile),
            ReadFileStamp(_paths.PdxModsCacheFile),
            ComputeModsFolderStamp(_paths.ModsDir),
            gameDir);
    }

    private static FileStamp ReadFileStamp(string path)
    {
        var fi = new FileInfo(path);
        return fi.Exists ? new FileStamp(true, fi.LastWriteTimeUtc, fi.Length) : default;
    }

    private static long ComputeModsFolderStamp(string modsDir)
    {
        if (!Directory.Exists(modsDir))
            return 0;

        try
        {
            var hc = new HashCode();
            hc.Add(Directory.GetLastWriteTimeUtc(modsDir).Ticks);
            foreach (string dllPath in UserDataFiles.Enumerate(modsDir, [".dll"]))
            {
                var fi = new FileInfo(dllPath);
                hc.Add(dllPath, StringComparer.OrdinalIgnoreCase);
                hc.Add(fi.LastWriteTimeUtc.Ticks);
                hc.Add(fi.Length);
            }

            return hc.ToHashCode();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return -1;
        }
    }

    private void NotifyChangedOnUiThread() => DispatchToUi(() => Changed?.Invoke(this, EventArgs.Empty));

    private static void DispatchToUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _ = dispatcher.BeginInvoke(action);
        }
    }
}
