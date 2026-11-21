using System.IO;
using System.Windows;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Services;

/// <summary>
/// 窗口级共享的后台冲突预检服务：
/// <list type="bullet">
/// <item>磁盘读取与 <c>MetadataReader</c> 扫描统一放入后台 <see cref="Task.Run"/> 执行，严禁阻塞 UI 线程；</item>
/// <item>仅在 <c>playset_config.json</c>、<c>mod_directory.json</c>、<c>pdx_mods_cache.json</c>、<c>Mods/</c> 目录时间戳或游戏路径变更时标记脏重扫，不在 1.5 秒轮询中重复扫盘；</item>
/// <item>当 <see cref="CocSession.Changed"/> 触发（切换模式或开关经济分组）时，基于已缓存的字节码结果在内存中瞬时重算二维冲突报告。</item>
/// </list>
/// </summary>
public sealed class ConflictScanService
{
    private readonly UserDataPaths _paths;
    private readonly CocSession _coc;
    private readonly Func<string?> _gameDirProvider;
    private readonly object _gate = new();

    private DiskStamp _lastStamp;
    private ScannedModWorkspace? _workspace;
    private bool _isScanning;
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

    public ConflictReport? Report { get; private set; }

    public string? LastScanError { get; private set; }

    /// <summary>主动请求一次后台扫描（首次启动窗口或用户点击“重新扫描模组”时调用）。</summary>
    public void TriggerScan()
    {
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

    /// <summary>按任意指定的设定快照对当前已缓存的工作区执行内存级即时评估（例如启动前按已保存的磁盘设定核对）。</summary>
    public ConflictReport? EvaluateWith(ConflictEvalSettings settings)
    {
        ScannedModWorkspace? ws;
        lock (_gate)
        {
            ws = _workspace;
        }

        return ws == null ? null : ConflictAnalyzer.Evaluate(ws, settings);
    }

    private void OnCocChanged(object? sender, EventArgs e)
    {
        ScannedModWorkspace? ws;
        lock (_gate)
        {
            ws = _workspace;
        }

        if (ws == null)
            return;

        ConflictEvalSettings evalSettings = ConflictEvalSettings.FromDictionary(_coc.WorkingValues);
        Report = ConflictAnalyzer.Evaluate(ws, evalSettings);
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
                workspace = await Task.Run(() => ConflictAnalyzer.ScanWorkspace(_paths, gameDir));
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
                    Changed?.Invoke(this, EventArgs.Empty);
                });
                return;
            }

            bool runAgain;
            lock (_gate)
            {
                _workspace = workspace;
                _lastStamp = stampBefore;
                _hasAttemptedOnce = true;
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
            DispatchToUi(() =>
            {
                LastScanError = null;
                ConflictEvalSettings evalSettings = ConflictEvalSettings.FromDictionary(_coc.WorkingValues);
                Report = ConflictAnalyzer.Evaluate(workspace, evalSettings);
                Changed?.Invoke(this, EventArgs.Empty);
            });

            if (!runAgain)
                break;
        }
    }

    private DiskStamp CaptureDiskStamp(string? gameDir)
    {
        return new DiskStamp(
            ReadFileStamp(_paths.PlaysetConfigFile),
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
