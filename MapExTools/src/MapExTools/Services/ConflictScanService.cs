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
/// <item><see cref="PreviewReport"/>、<see cref="EvaluatePreviewWith"/> 与 <see cref="SelectPlayset"/> 供「模组列表与冲突预检」页切换浏览任意本地播放集（选中激活项时零开销复用主工作区）；</item>
/// <item><see cref="ActivatePlaysetAsync"/> 在后台调用 <see cref="PlaysetActivator"/> 切换激活播放集；成功后丢弃旧报告、置 <see cref="IsActiveStale"/> 并触发不清字节码缓存的重扫，重扫完成前启动页禁止启动；</item>
/// <item>磁盘读取与 <c>MetadataReader</c> 扫描统一放入后台 <see cref="Task.Run"/> 执行，跨播放集切换与自动重扫按文件指纹复用字节码解析结果，仅手动“重新扫描模组”时清空缓存。</item>
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
    private long _activationSeq;
    private bool _isScanning;
    private bool _isPreviewScanning;
    private bool _isActivating;
    private bool _isActiveStale;
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

    /// <summary>是否正在执行激活播放集切换的落盘操作。</summary>
    public bool IsActivating
    {
        get { lock (_gate) return _isActivating; }
    }

    /// <summary>激活播放集成功后，在对应的新一轮激活扫描结束前为 <c>true</c>（此时启动页禁用启动按钮）。</summary>
    public bool IsActiveStale
    {
        get { lock (_gate) return _isActiveStale; }
    }

    /// <summary>「模组列表与冲突预检」页当前预览目标是否正在扫描中。</summary>
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

    /// <summary>「模组列表与冲突预检」页所选目标播放集的评估报告（跟随激活项时与 <see cref="Report"/> 相同）。</summary>
    public ConflictReport? PreviewReport { get; private set; }

    public string? LastScanError { get; private set; }

    public string? LastPreviewError { get; private set; }

    /// <summary>
    /// 主动请求一次全量后台重扫。
    /// <paramref name="clearInspectionCache"/> 仅在用户手动点击“重新扫描模组”时传 <c>true</c>；激活切换后与轮询发现磁盘变化时传 <c>false</c> 以复用字节码缓存。
    /// </summary>
    public void TriggerScan(bool clearInspectionCache = true)
    {
        if (clearInspectionCache)
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

    /// <summary>
    /// 在后台线程执行本地播放集激活切换。
    /// 成功后将预览归位到新激活项、立即丢弃旧的激活工作区与报告、置 <see cref="IsActiveStale"/> 并递增激活序号，随后触发不清字节码缓存的重扫。
    /// </summary>
    public async Task<PlaysetActivationResult> ActivatePlaysetAsync(string playsetId)
    {
        lock (_gate)
        {
            if (_isActivating)
            {
                return new PlaysetActivationResult(
                    PlaysetActivationStatus.WriteFailed,
                    playsetId,
                    ErrorMessage: "Playset activation is already in progress.");
            }

            _isActivating = true;
        }

        NotifyChangedOnUiThread();

        PlaysetActivationResult result;
        try
        {
            result = await Task.Run(() => PlaysetActivator.Activate(_paths, playsetId));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            result = new PlaysetActivationResult(
                PlaysetActivationStatus.WriteFailed,
                playsetId,
                ErrorMessage: ex.Message);
        }

        bool needRescan = false;
        bool startLoop = false;
        bool resetPreviewOnly = false;

        lock (_gate)
        {
            _isActivating = false;

            bool diskMatchesMemory = result.Status == PlaysetActivationStatus.AlreadyActive &&
                                     _workspace?.Playset.ActivePlaysetId is { } memActive &&
                                     string.Equals(memActive, result.TargetPlaysetId, StringComparison.Ordinal);

            if (result.Status == PlaysetActivationStatus.Activated ||
                (result.Status == PlaysetActivationStatus.AlreadyActive && !diskMatchesMemory))
            {
                _selectedPlaysetId = null;
                _isPreviewScanning = false;
                ++_previewRequestId;
                _workspace = null;
                _previewWorkspace = null;
                _isActiveStale = true;
                ++_activationSeq;
                needRescan = true;

                if (_isScanning)
                {
                    _rescanQueued = true;
                    startLoop = false;
                }
                else
                {
                    _isScanning = true;
                    _rescanQueued = false;
                    startLoop = true;
                }
            }
            else if (result.Status == PlaysetActivationStatus.AlreadyActive)
            {
                _selectedPlaysetId = null;
                _isPreviewScanning = false;
                ++_previewRequestId;
                _previewWorkspace = _workspace;
                resetPreviewOnly = true;
            }
        }

        if (needRescan)
        {
            DispatchToUi(() =>
            {
                Report = null;
                PreviewReport = null;
                LastScanError = null;
                LastPreviewError = null;
                Changed?.Invoke(this, EventArgs.Empty);
            });

            if (startLoop)
                _ = RunScanLoopAsync();
        }
        else if (resetPreviewOnly)
        {
            DispatchToUi(() =>
            {
                LastPreviewError = null;
                PreviewReport = Report;
                Changed?.Invoke(this, EventArgs.Empty);
            });
        }
        else
        {
            NotifyChangedOnUiThread();
        }

        return result;
    }

    /// <summary>切换「模组列表与冲突预检」页预览的目标播放集（传 <c>null</c> 或当前已激活播放集精确 ID 时归位为自动跟随激活项）。</summary>
    public void SelectPlayset(string? playsetId)
    {
        string? normalized = string.IsNullOrWhiteSpace(playsetId) ? null : playsetId.Trim();
        ScannedModWorkspace? activeWs;
        long requestId;

        lock (_gate)
        {
            activeWs = _workspace;
            if (normalized != null &&
                activeWs?.Playset.EffectiveAvailablePlaysets.Any(p => p.IsActive && string.Equals(p.Id, normalized, StringComparison.Ordinal)) == true)
            {
                normalized = null;
            }

            if (string.Equals(_selectedPlaysetId, normalized, StringComparison.Ordinal) &&
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
            if (_isScanning || _stampChecking || _isActivating)
                return;

            hasAttempted = _workspace != null || _hasAttemptedOnce;
            if (hasAttempted)
                _stampChecking = true;
        }

        if (!hasAttempted)
        {
            TriggerScan(clearInspectionCache: false);
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
                changed = !_isScanning && !_isActivating && current != _lastStamp;
            }

            if (changed)
                DispatchToUi(() => TriggerScan(clearInspectionCache: false));
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
            long scanActivationSeq;
            lock (_gate)
            {
                scanActivationSeq = _activationSeq;
            }

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
                bool runAgainAfterError;
                lock (_gate)
                {
                    _lastStamp = stampBefore;
                    _hasAttemptedOnce = true;
                    if (_rescanQueued || scanActivationSeq < _activationSeq)
                    {
                        // 扫描中途有新的重扫或激活请求：继续下一轮，不提前解锁 IsActiveStale
                        _rescanQueued = false;
                        runAgainAfterError = true;
                    }
                    else
                    {
                        _isScanning = false;
                        runAgainAfterError = false;
                    }
                }

                if (runAgainAfterError)
                    continue;

                string errMsg = ex.Message;
                DispatchToUi(() =>
                {
                    bool stillCurrent;
                    lock (_gate)
                    {
                        stillCurrent = scanActivationSeq >= _activationSeq;
                        if (stillCurrent)
                            _isActiveStale = false;
                    }

                    if (!stillCurrent)
                        return;

                    LastScanError = errMsg;
                    if (SelectedPlaysetId == null)
                        LastPreviewError = errMsg;
                    Changed?.Invoke(this, EventArgs.Empty);
                });
                return;
            }

            bool runAgain;
            bool discardedStale = false;
            string? previewToRefresh = null;
            long previewReqId = 0;

            lock (_gate)
            {
                if (scanActivationSeq < _activationSeq)
                {
                    // 本轮扫描开始于最新激活之前：丢弃旧结果、不提前解锁 IsActiveStale，强制进入下一轮扫描新激活项
                    _rescanQueued = false;
                    runAgain = true;
                    discardedStale = true;
                }
                else
                {
                    _workspace = workspace;
                    _lastStamp = stampBefore;
                    _hasAttemptedOnce = true;

                    // 若此前正在预览某个非激活播放集：检查它是否仍在列表中且仍处于非激活状态；若已删除或已成为新的激活项则自动归位
                    if (_selectedPlaysetId != null)
                    {
                        PlaysetSummary? stillValidInactive = workspace.Playset.EffectiveAvailablePlaysets
                            .FirstOrDefault(p => !p.IsActive && string.Equals(p.Id, _selectedPlaysetId, StringComparison.Ordinal));
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
            }

            if (discardedStale)
                continue;

            // 在 UI 线程读取最新 Coc 工作副本完成瞬时定级、清除 IsActiveStale 并广播
            bool followActiveNow = previewToRefresh == null;
            DispatchToUi(() =>
            {
                bool stillCurrent;
                lock (_gate)
                {
                    stillCurrent = scanActivationSeq >= _activationSeq;
                    if (stillCurrent)
                        _isActiveStale = false;
                }

                if (!stillCurrent)
                    return;

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
                               string.Equals(_selectedPlaysetId, targetPlaysetId, StringComparison.Ordinal);
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
                    string.Equals(_selectedPlaysetId, targetPlaysetId, StringComparison.Ordinal);
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
