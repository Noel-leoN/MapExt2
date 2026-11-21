using System.Globalization;
using System.Text;
using MapExTools.Core.Game;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Core.Coc;

/// <summary><see cref="CocSettingsManager.Save"/> 的结果。</summary>
public enum CocSaveStatus
{
    /// <summary>已写入。</summary>
    Saved,

    /// <summary>目标值与文件一致，没有写入。</summary>
    NoChanges,

    /// <summary>游戏正在运行，拒绝写入：游戏退出时会把内存中的设定写回，覆盖这次的改动。</summary>
    GameRunning,
}

/// <param name="Changes">按 <see cref="StartupKeys.AllKeys"/> 顺序列出的改动；游戏运行中时为本该写入的改动。</param>
/// <param name="BackupPath">写入前原文件的备份；新建文件或没有写入时为 null。</param>
public sealed record CocSaveResult(CocSaveStatus Status, IReadOnlyList<StartupSettingChange> Changes, string? BackupPath);

/// <summary>写入失败的原因。</summary>
public enum CocSaveFailure
{
    /// <summary>文件在读取之后被改过（手动编辑、游戏退出时写回等），须重新读取后再保存。</summary>
    FileChanged,

    /// <summary>写入结果校验失败：写盘前的预演或写盘后读回的值与目标不符。</summary>
    VerifyFailed,
}

/// <summary>写入 .coc 失败。界面用 <see cref="GetMessage"/> 取当前语言的文字。</summary>
public sealed class CocSaveException : IOException
{
    public CocSaveException(CocSaveFailure failure, string? detail = null)
        : base(BuildMessage(UiLanguage.English, failure, detail))
    {
        Failure = failure;
        Detail = detail;
    }

    public CocSaveFailure Failure { get; }

    /// <summary>校验失败的键名或块名。</summary>
    public string? Detail { get; }

    public string GetMessage(string language) => BuildMessage(language, Failure, Detail);

    private static string BuildMessage(string language, CocSaveFailure failure, string? detail) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, "Coc.Err." + failure), detail ?? string.Empty);
}

/// <summary>
/// 读写 <c>{UserData}/ModsSettings/MapExtPDX/MapExtPDX.coc</c> 里 MapExtPDX 块的受管键，其余字符逐字保留。
/// 移植并扩展自开发版 <c>Set-MapExtStartup.ps1</c> 的解析与改写。
///
/// <para>写入流程：游戏运行中拒绝写入 → 只写有改动的键 → 文件自读取后被改过则拒绝 → 在内存里改写并按同一规则
/// 重新解析核对 → 原文件复制为 <c>.bak</c> → 写临时文件后整体替换 → 从磁盘读回再核对一次。</para>
///
/// <para>备份与临时文件不能用 <c>.coc</c> 扩展名：用户数据目录下每个 .coc 的每个块都会被游戏当作设定载入。</para>
/// </summary>
public sealed class CocSettingsManager
{
    // 读取时遇到无效字节就报错，不把它换成替换字符后写回，以免改坏文件里的其他内容
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Func<bool> _isGameRunning;

    /// <param name="cocPath">设定文件路径。</param>
    /// <param name="isGameRunning">检测游戏是否在运行，默认 <see cref="GameProcess.IsRunning"/>；测试时可替换。</param>
    public CocSettingsManager(string cocPath, Func<bool>? isGameRunning = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(cocPath);
        CocPath = Path.GetFullPath(cocPath);
        _isGameRunning = isGameRunning ?? GameProcess.IsRunning;
    }

    public string CocPath { get; }

    /// <summary>写入前原文件的备份 <c>MapExtPDX.coc.bak</c>。</summary>
    public string BackupPath => CocPath + ".bak";

    private string TempPath => CocPath + ".tmp";

    /// <summary>9 个核心启动键的默认值，供命令行 <c>--reset</c> 与固定样本使用。</summary>
    public static Dictionary<string, object> GetDefaultValues() =>
        StartupKeys.Keys.ToDictionary(k => k.Name, k => k.DefaultValue, StringComparer.Ordinal);

    /// <summary>全部 65 个受管键的默认值，供图形界面"全部恢复默认"使用。</summary>
    public static Dictionary<string, object> GetAllDefaultValues() =>
        StartupKeys.AllKeys.ToDictionary(k => k.Name, k => k.DefaultValue, StringComparer.Ordinal);

    /// <summary>某个一级分类下所有受管键的默认值。</summary>
    public static Dictionary<string, object> GetCategoryDefaultValues(OptionCategory category) =>
        StartupKeys.AllKeys.Where(k => k.Category == category)
            .ToDictionary(k => k.Name, k => k.DefaultValue, StringComparer.Ordinal);

    /// <summary>某个分组卡片下所有受管键的默认值。</summary>
    public static Dictionary<string, object> GetGroupDefaultValues(OptionGroup group) =>
        StartupKeys.AllKeys.Where(k => k.Group == group)
            .ToDictionary(k => k.Name, k => k.DefaultValue, StringComparer.Ordinal);

    /// <summary>读取并解析设定文件；文件不存在时各键取默认值。</summary>
    /// <exception cref="CocFormatException">格式有误或不是 UTF-8 文本。</exception>
    /// <exception cref="IOException">文件正被占用等读取错误。</exception>
    public StartupSettingsState Read()
    {
        if (!File.Exists(CocPath))
            return StartupSettingsState.Parse(string.Empty, exists: false);

        byte[] bytes = File.ReadAllBytes(CocPath);
        bool hasBom = bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        int offset = hasBom ? Encoding.UTF8.Preamble.Length : 0;
        string text;
        try
        {
            text = s_strictUtf8.GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            throw new CocFormatException(CocErrorKind.InvalidEncoding, 0, 0);
        }

        return StartupSettingsState.Parse(text, exists: true, hasBom);
    }

    /// <summary>
    /// 列出需要写入的项：目标值与当前值不同，或当前值无效／越界。只比较 <paramref name="target"/> 里给出的键。
    /// </summary>
    /// <exception cref="ArgumentException">目标里有不认识的键，或取值类型、范围、枚举名不对。</exception>
    public static IReadOnlyList<StartupSettingChange> GetChanges(StartupSettingsState state, IReadOnlyDictionary<string, object> target)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateTarget(target);

        var changes = new List<StartupSettingChange>();
        foreach (StartupSettingValue current in state.AllSettings)
        {
            if (!target.TryGetValue(current.Key.Name, out object? value))
                continue;
            if (current.Invalid || !StartupKeys.ValueEquals(current.Key, current.Value, value))
                changes.Add(new StartupSettingChange(current.Key, current.Value, value, current.Invalid));
        }

        return changes;
    }

    /// <summary>
    /// 把 <paramref name="target"/> 相对 <paramref name="baseline"/> 的改动写入文件。
    /// <paramref name="baseline"/> 是界面或命令行上次读到的状态，只写相对它有改动的键。
    /// </summary>
    /// <exception cref="CocFormatException">文件格式有误。</exception>
    /// <exception cref="CocSaveException">文件在读取后被改过，或写入结果校验失败。</exception>
    /// <exception cref="IOException">读写错误。</exception>
    /// <exception cref="UnauthorizedAccessException">没有写入权限。</exception>
    public CocSaveResult Save(StartupSettingsState baseline, IReadOnlyDictionary<string, object> target)
    {
        IReadOnlyList<StartupSettingChange> changes = GetChanges(baseline, target);
        if (_isGameRunning())
            return new CocSaveResult(CocSaveStatus.GameRunning, changes, null);

        // 读取之后文件被改过就不写：即便本次目标值恰好等于旧基准，磁盘上也可能已被外部改成其他值
        StartupSettingsState current = Read();
        if (current.Exists != baseline.Exists || current.HasBom != baseline.HasBom
            || !string.Equals(current.Text, baseline.Text, StringComparison.Ordinal))
        {
            throw new CocSaveException(CocSaveFailure.FileChanged);
        }

        if (changes.Count == 0)
            return new CocSaveResult(CocSaveStatus.NoChanges, changes, null);

        Dictionary<string, object> updates = changes.ToDictionary(c => c.Key.Name, c => c.NewValue, StringComparer.Ordinal);
        string text = BuildText(current, updates);

        // 写盘前按同一规则重新解析一遍，写盘后再从磁盘读回核对
        Verify(StartupSettingsState.Parse(text, exists: true, current.HasBom), updates);
        string? backup = WriteFile(text, current.HasBom);
        Verify(Read(), updates);

        return new CocSaveResult(CocSaveStatus.Saved, changes, backup);
    }

    /// <summary>
    /// 生成写入后的全文：已有的键原地替换值，缺的键追加到对象末尾，其余字符不动。
    /// 没有 MapExtPDX 块时按游戏写出的格式新建：块名行与结尾用 CRLF，对象内部用 LF，缩进 4 个空格。
    /// </summary>
    public static string BuildText(StartupSettingsState state, IReadOnlyDictionary<string, object> updates)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateTarget(updates);

        // 按 StartupKeys.AllKeys 的顺序处理，追加的键顺序固定（前 9 项与 StartupKeys.Keys 相同）
        var ordered = new List<(StartupKeyInfo Key, string Raw)>();
        foreach (StartupKeyInfo key in StartupKeys.AllKeys)
        {
            if (updates.TryGetValue(key.Name, out object? value))
                ordered.Add((key, StartupSettingsState.Encode(key, value)));
        }

        if (state.Block == null)
        {
            string blockText = StartupKeys.CocBlockName + "\r\n{\n"
                + string.Join(",\n", ordered.Select(u => $"    \"{u.Key.Name}\": {u.Raw}"))
                + "\n}\r\n";
            if (CocParser.IsBlank(state.Text))
                return blockText;

            string prefix = state.Text.EndsWith('\n') ? state.Text : state.Text + "\r\n";
            return prefix + blockText;
        }

        string text = state.Text;
        CocBlock block = state.Block;
        string newline = text.AsSpan(block.Open, block.Close - block.Open + 1).Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var edits = new List<(int Start, int End, string Text)>();
        var missing = new List<string>();
        foreach ((StartupKeyInfo key, string raw) in ordered)
        {
            bool found = false;
            foreach (CocEntry entry in state.Entries)
            {
                if (entry.Key != key.Name)
                    continue;
                edits.Add((entry.Start, entry.End, raw));
                found = true;
            }

            if (!found)
                missing.Add($"\"{key.Name}\": {raw}");
        }

        if (missing.Count > 0)
        {
            if (state.Entries.Count > 0)
            {
                CocEntry last = state.Entries[^1];
                string indent = GetEntryIndent(text, state.Entries[0].KeyStart);
                string insert = string.Concat(missing.Select(m => "," + newline + indent + m));
                edits.Add((last.End, last.End, insert));
            }
            else
            {
                string body = newline + string.Join("," + newline, missing.Select(m => "    " + m)) + newline;
                edits.Add((block.Open + 1, block.Close, body));
            }
        }

        // 从后往前改，前面的位置不受影响
        var builder = new StringBuilder(text);
        foreach ((int start, int end, string replacement) in edits.OrderByDescending(e => e.Start))
        {
            builder.Remove(start, end - start);
            builder.Insert(start, replacement);
        }

        return builder.ToString();
    }

    /// <summary>取键名所在行的行首空白作为缩进；键名与 <c>{</c> 同行时退回 4 个空格。</summary>
    private static string GetEntryIndent(string text, int keyStart)
    {
        int lineStart = text.LastIndexOf('\n', Math.Max(keyStart - 1, 0)) + 1;
        string lead = text[lineStart..keyStart];
        return lead.All(c => c is ' ' or '\t') ? lead : "    ";
    }

    private static void ValidateTarget(IReadOnlyDictionary<string, object> target)
    {
        ArgumentNullException.ThrowIfNull(target);
        foreach ((string name, object value) in target)
        {
            StartupKeyInfo key = StartupKeys.FindKey(name)
                                 ?? throw new ArgumentException($"不是受管的设定键：{name}", nameof(target));
            bool ok = key.Kind switch
            {
                StartupKeyKind.Mode => value is string mode && Array.IndexOf(StartupKeys.ModeNames, mode) >= 0,
                StartupKeyKind.Enum => value is string enumVal
                                       && key.SelectableEnumNames != null
                                       && Array.IndexOf(key.SelectableEnumNames, enumVal) >= 0,
                StartupKeyKind.Bool => value is bool,
                StartupKeyKind.Int => value is int iv
                                      && iv >= (int)Math.Round(key.Min)
                                      && iv <= (int)Math.Round(key.Max),
                StartupKeyKind.Float => value is float fv
                                        && float.IsFinite(fv)
                                        && fv >= (float)key.Min - 1e-6f
                                        && fv <= (float)key.Max + 1e-6f,
                _ => false,
            };
            if (!ok)
                throw new ArgumentException($"{name} 的取值无效：{value}", nameof(target));
        }
    }

    private static void Verify(StartupSettingsState written, IReadOnlyDictionary<string, object> updates)
    {
        if (written.Block == null)
            throw new CocSaveException(CocSaveFailure.VerifyFailed, StartupKeys.CocBlockName);

        foreach ((string name, object value) in updates)
        {
            StartupSettingValue actual = written[name];
            if (!actual.InFile || actual.Invalid || !StartupKeys.ValueEquals(actual.Key, actual.Value, value))
                throw new CocSaveException(CocSaveFailure.VerifyFailed, name);
        }
    }

    /// <summary>先把原文件复制为 .bak，再写临时文件并整体替换。返回备份路径；新建文件时为 null。</summary>
    private string? WriteFile(string text, bool hasBom)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CocPath)!);

        string? backup = null;
        if (File.Exists(CocPath))
        {
            File.Copy(CocPath, BackupPath, overwrite: true);
            backup = BackupPath;
        }

        try
        {
            File.WriteAllText(TempPath, text, new UTF8Encoding(hasBom));
            File.Move(TempPath, CocPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(TempPath))
                File.Delete(TempPath);
        }

        return backup;
    }
}
