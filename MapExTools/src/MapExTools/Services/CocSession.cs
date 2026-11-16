using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using MapExTools.Core.Coc;
using MapExTools.Core.Diagnostics;
using MapExTools.Core.Game;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExTools.Core.Paths;
using MapExTools.Views;
using MapExtPDX;

namespace MapExTools.Services;

/// <summary>
/// 窗口级共享的 <c>MapExtPDX.coc</c> 编辑会话：维护磁盘基准状态、全部 65 个受管键的内存工作副本、
/// 外部改动检测以及保存／重读／恢复默认操作，供「启动与设定」「模组设置」「存档与地图」多页签实时同步。
/// </summary>
public sealed class CocSession
{
    private readonly UserDataPaths _paths;
    private readonly Dictionary<string, object> _working = CocSettingsManager.GetAllDefaultValues();

    private CocStamp _stamp;
    private bool? _gameRunning;
    private Func<string, string>? _note;

    public CocSession(UserDataPaths paths)
    {
        _paths = paths;
        _gameRunning = GameProcess.IsRunning();
        Reload(keepEdits: false, notify: false);
        _ = LoadAppliedAsync();
    }

    private readonly record struct CocStamp(bool Exists, DateTime LastWriteUtc, long Length);

    /// <summary>基准状态、工作副本、游戏运行状态或最近一次生效配置变化时触发。</summary>
    public event EventHandler? Changed;

    public string CocPath => _paths.CocFile;

    /// <summary>最后一次读取成功的设定状态；格式出错期间保留上一次的基准以保留未保存改动。</summary>
    public StartupSettingsState? State { get; private set; }

    public CocFormatException? FormatError { get; private set; }

    public string? ReadError { get; private set; }

    public bool GameRunning => _gameRunning == true;

    public bool IsUsable => State != null && FormatError == null;

    public AppliedConfigInfo? Applied { get; private set; }

    public IReadOnlyDictionary<string, object> WorkingValues => _working;

    /// <summary>是否存在尚未保存的改动（格式出错期间也保留相对上次成功基准的改动）。</summary>
    public bool HasUnsavedChanges => GetPending().Count > 0;

    public object GetValue(string keyName) => _working[keyName];

    public bool GetBool(string keyName) => _working.TryGetValue(keyName, out object? v) && v is true;

    public string GetString(string keyName) => _working.TryGetValue(keyName, out object? v) && v is string s ? s : string.Empty;

    public void SetValue(string keyName, object value)
    {
        StartupKeyInfo key = StartupKeys.FindKey(keyName)
                             ?? throw new ArgumentException($"未知设定键：{keyName}", nameof(keyName));
        if (_working.TryGetValue(keyName, out object? current) && StartupKeys.ValueEquals(key, current, value))
            return;

        _working[keyName] = value;
        RaiseChanged();
    }

    public void SetValues(IReadOnlyDictionary<string, object> values)
    {
        bool any = false;
        foreach ((string name, object value) in values)
        {
            StartupKeyInfo? key = StartupKeys.FindKey(name);
            if (key == null)
                continue;
            if (!_working.TryGetValue(name, out object? current) || !StartupKeys.ValueEquals(key, current, value))
            {
                _working[name] = value;
                any = true;
            }
        }

        if (any)
            RaiseChanged();
    }

    /// <summary>恢复单个键为默认值。</summary>
    public void ResetKey(StartupKeyInfo key) => SetValue(key.Name, key.DefaultValue);

    /// <summary>恢复首页 11 个核心与转换键为默认值。</summary>
    public void ResetCoreStartupDefaults()
    {
        SetValues(CocSettingsManager.GetCategoryDefaultValues(OptionCategory.Core));
    }

    /// <summary>恢复某个分组卡片内的键为默认值。</summary>
    public void ResetGroup(OptionGroup group) => SetValues(CocSettingsManager.GetGroupDefaultValues(group));

    /// <summary>恢复全部 65 个受管键为默认值。</summary>
    public void ResetAllDefaults() => SetValues(CocSettingsManager.GetAllDefaultValues());

    /// <summary>该键当前工作值是否等于 MapExt 默认值。</summary>
    public bool IsDefaultValue(StartupKeyInfo key) =>
        _working.TryGetValue(key.Name, out object? current) && StartupKeys.ValueEquals(key, current, key.DefaultValue);

    /// <summary>对应 <c>ModSettings</c> 的 <c>SettingsUIDisableByCondition</c> 联动置灰规则。</summary>
    public bool IsKeyEnabled(StartupKeyInfo key)
    {
        if (!IsUsable)
            return false;
        if (key.EcoGroup && !GetBool(StartupSettingsText.EconomyMasterKey))
            return false;
        if (key.Name == SaveGateRules.ConversionKey && GetBool(SaveGateRules.BackdropKey) && !GetBool(SaveGateRules.ConversionKey))
            return false;
        if (key.Name == SaveGateRules.BackdropKey && GetBool(SaveGateRules.ConversionKey) && !GetBool(SaveGateRules.BackdropKey))
            return false;
        if (key.Name == "TerrainBrushRoadMargin" && !GetBool("TerrainBrushRoadBlock"))
            return false;
        if (key.Name == "EnableRescueDebugLog" && !GetBool("EnableVehicleRescue"))
            return false;
        return true;
    }

    /// <summary>列出相对上次读取成功的基准待保存的全部改动。</summary>
    public IReadOnlyList<StartupSettingChange> GetPending() =>
        State == null ? [] : CocSettingsManager.GetChanges(State, _working);

    /// <summary>由主窗口 1.5 秒定时器调用：检测游戏刚退出或设定文件被外部程序修改。</summary>
    public void Poll()
    {
        bool running = GameProcess.IsRunning();
        bool changed = false;

        if (_gameRunning == true && !running)
        {
            Reload(keepEdits: true, notify: false);
            _note = lang => Strings.Get(lang, "Startup.GameExited");
            _ = LoadAppliedAsync();
            changed = true;
        }
        else if (ReadStamp() != _stamp)
        {
            bool retry = ReadError != null;
            Reload(keepEdits: true, notify: false);
            if (!retry && ReadError == null)
                _note = lang => Strings.Get(lang, "Startup.FileChanged");
            changed = true;
        }

        if (_gameRunning != running)
        {
            _gameRunning = running;
            changed = true;
        }

        if (changed)
            RaiseChanged();
    }

    /// <summary>手动点击"重新读取"：放弃未保存的改动并刷新日志生效配置。</summary>
    public void ReloadManual()
    {
        _note = null;
        Reload(keepEdits: false, notify: true);
        _ = LoadAppliedAsync();
    }

    /// <summary>外部组件（如存档页"切到该模式"）直接写盘后通知同步基准，同时保留其他未保存改动。</summary>
    public void NotifyExternalWrite()
    {
        Reload(keepEdits: true, notify: true);
    }

    /// <summary>保存当前工作副本中的改动。返回 true 表示已成功写入或本来无改动。</summary>
    public bool Save(Window? owner)
    {
        if (State == null || !IsUsable)
            return false;

        IReadOnlyList<StartupSettingChange> pending = GetPending();
        bool saved = false;
        try
        {
            CocSaveResult result = new CocSettingsManager(CocPath).Save(State, _working);
            switch (result.Status)
            {
                case CocSaveStatus.GameRunning:
                    _gameRunning = true;
                    break;
                case CocSaveStatus.NoChanges:
                    _note = lang => Strings.Get(lang, "Startup.NoChanges");
                    saved = true;
                    break;
                default:
                    string time = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                    Reload(keepEdits: false, notify: false);
                    _note = lang => string.Format(CultureInfo.InvariantCulture, Strings.Get(lang, "Startup.Saved"),
                        time, StartupSettingsText.ChangeListCompact(pending, lang));
                    saved = true;
                    break;
            }
        }
        catch (Exception ex) when (ex is CocFormatException or CocSaveException or IOException or UnauthorizedAccessException)
        {
            string lang = Loc.Instance.Language;
            string message = ex switch
            {
                CocFormatException format => format.GetMessage(lang),
                CocSaveException save => save.GetMessage(lang),
                _ => ex.Message,
            };
            ShowMessage(owner, Loc.Instance.Format("Startup.SaveFailed", message), MessageBoxImage.Warning);

            // 文件被外部改过等：重新读取，界面上未保存的改动保留，可再次保存
            Reload(keepEdits: true, notify: false);
        }

        RaiseChanged();
        return saved;
    }

    public List<ToneLine> BuildStatusLines(string lang)
    {
        var lines = new List<ToneLine>();
        if (FormatError != null)
            lines.Add(new ToneLine(Loc.Instance.Format("Startup.FileError", FormatError.GetMessage(lang)), PackageTone.Error));
        else if (ReadError != null)
            lines.Add(new ToneLine(Loc.Instance.Format("Startup.ReadError", ReadError), PackageTone.Warning));

        if (GameRunning)
            lines.Add(new ToneLine(Loc.Instance["Startup.Running"], PackageTone.Error));
        else if (lines.Count == 0)
            lines.Add(new ToneLine(_note?.Invoke(lang) ?? Loc.Instance["Startup.Ready"], PackageTone.Good));

        if (State != null && FormatError == null)
        {
            if (!State.Exists)
                lines.Add(new ToneLine(Loc.Instance["Startup.NoFile"], PackageTone.Muted));
            else if (State.Block == null)
                lines.Add(new ToneLine(Loc.Instance["Startup.NoBlock"], PackageTone.Muted));
            if (State.BlockCount > 1)
                lines.Add(new ToneLine(Loc.Instance.Format("Startup.ExtraBlocks", State.BlockCount), PackageTone.Warning));
            if (StartupSettingsText.InvalidList(State, lang, allKeys: true) is { } invalid)
                lines.Add(new ToneLine(Loc.Instance.Format("Startup.Invalid", invalid), PackageTone.Warning));
        }

        return lines;
    }

    public void OpenInNotepad(Window? owner)
    {
        if (!File.Exists(CocPath))
            return;
        if (GameProcess.IsRunning()
            && MessageBox.Show(owner!, Loc.Instance["Startup.ConfirmEditWhileRunning"], Loc.Instance["App.Title"],
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
        start.ArgumentList.Add(CocPath);
        StartProcess(owner, start);
    }

    public void ShowInExplorer(Window? owner)
    {
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        if (File.Exists(CocPath))
            start.Arguments = "/select,\"" + CocPath + "\"";
        else
            start.ArgumentList.Add(Path.GetDirectoryName(CocPath)!);
        StartProcess(owner, start);
    }

    private CocStamp ReadStamp()
    {
        var info = new FileInfo(CocPath);
        return info.Exists ? new CocStamp(true, info.LastWriteTimeUtc, info.Length) : default;
    }

    private void Reload(bool keepEdits, bool notify)
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
                FormatError = format;
                ReadError = null;
            }
            else
            {
                ReadError = ex.Message;
                _stamp = default;
            }

            if (!keepEdits && State != null)
            {
                foreach ((string k, object v) in State.GetAllValues())
                    _working[k] = v;
            }

            if (notify)
                RaiseChanged();
            return;
        }

        FormatError = null;
        ReadError = null;
        State = state;

        Dictionary<string, object> fresh = state.GetAllValues();
        foreach (StartupSettingChange edit in edits)
            fresh[edit.Key.Name] = edit.NewValue;
        foreach ((string k, object v) in fresh)
            _working[k] = v;

        if (notify)
            RaiseChanged();
    }

    private async Task LoadAppliedAsync()
    {
        string log = _paths.MapExtLog;
        try
        {
            Applied = await Task.Run(() =>
                File.Exists(log) ? MapExtLogSummary.Parse(LogDiagnostics.ReadLines(log)).AppliedConfig : null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Applied = null;
        }

        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static void StartProcess(Window? owner, ProcessStartInfo start)
    {
        try
        {
            Process.Start(start)?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowMessage(owner, Loc.Instance.Format("Startup.OpenFailed", ex.Message), MessageBoxImage.Warning);
        }
    }

    private static void ShowMessage(Window? owner, string text, MessageBoxImage image)
    {
        if (owner != null)
            MessageBox.Show(owner, text, Loc.Instance["App.Title"], MessageBoxButton.OK, image);
        else
            MessageBox.Show(text, Loc.Instance["App.Title"], MessageBoxButton.OK, image);
    }
}
