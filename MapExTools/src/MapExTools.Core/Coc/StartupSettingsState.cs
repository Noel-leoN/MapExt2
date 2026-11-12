using System.Globalization;
using System.Text.Json;
using MapExtPDX;

namespace MapExTools.Core.Coc;

/// <summary>
/// 一个受管键按游戏解码规则得到的值。<see cref="Value"/>：模式键为模式名字符串，开关键为 bool。
/// </summary>
/// <param name="InFile">文件的 MapExtPDX 块里有没有这个键；没有时 <see cref="Value"/> 为默认值。</param>
/// <param name="Invalid">文件里的值游戏读不懂，会落回默认值（模式键落回 ModeA）。</param>
/// <param name="Raw">文件里的取值原文；不在文件里时为 null。</param>
public sealed record StartupSettingValue(StartupKeyInfo Key, object Value, bool InFile, bool Invalid, string? Raw);

/// <summary>一项待写入的改动；<see cref="WasInvalid"/> 表示原值游戏读不懂，值相同也要改写。</summary>
public sealed record StartupSettingChange(StartupKeyInfo Key, object OldValue, object NewValue, bool WasInvalid);

/// <summary>
/// 读取 <c>MapExtPDX.coc</c> 的结果：原文、MapExtPDX 块的位置与各项，以及 9 个受管键实际生效的值。
/// 没有这个块或没有某个键时，该键取 <see cref="StartupKeys"/> 里的默认值。
/// </summary>
public sealed class StartupSettingsState
{
    private readonly Dictionary<string, StartupSettingValue> _byName;

    private StartupSettingsState(
        bool exists,
        bool hasBom,
        string text,
        CocBlock? block,
        int blockCount,
        IReadOnlyList<CocEntry> entries,
        IReadOnlyList<StartupSettingValue> settings)
    {
        Exists = exists;
        HasBom = hasBom;
        Text = text;
        Block = block;
        BlockCount = blockCount;
        Entries = entries;
        Settings = settings;
        _byName = settings.ToDictionary(s => s.Key.Name, StringComparer.Ordinal);
    }

    /// <summary>文件是否存在。</summary>
    public bool Exists { get; }

    /// <summary>文件是否以 UTF-8 BOM 开头；写回时照原样保留。</summary>
    public bool HasBom { get; }

    /// <summary>去掉 BOM 后的全文。</summary>
    public string Text { get; }

    /// <summary>第一个 MapExtPDX 块；没有时为 null。</summary>
    public CocBlock? Block { get; }

    /// <summary>MapExtPDX 块的个数。多于 1 个时游戏只认第一个，其余忽略并记 ParseError。</summary>
    public int BlockCount { get; }

    /// <summary>第一个 MapExtPDX 块顶层的全部项（含不受管的键），按文件顺序。</summary>
    public IReadOnlyList<CocEntry> Entries { get; }

    /// <summary>9 个受管键的值，顺序同 <see cref="StartupKeys.Keys"/>。</summary>
    public IReadOnlyList<StartupSettingValue> Settings { get; }

    /// <summary>有没有游戏读不懂的值。</summary>
    public bool HasInvalidValues => Settings.Any(s => s.Invalid);

    /// <summary>按键名取值；不是受管键时抛 <see cref="KeyNotFoundException"/>。</summary>
    public StartupSettingValue this[string keyName] => _byName[keyName];

    /// <summary>键名到值的对照，可直接改几项后作为写入目标。</summary>
    public Dictionary<string, object> GetValues() =>
        Settings.ToDictionary(s => s.Key.Name, s => s.Value, StringComparer.Ordinal);

    /// <summary>解析设定文本。</summary>
    /// <exception cref="CocFormatException">格式有误。</exception>
    public static StartupSettingsState Parse(string text, bool exists = true, bool hasBom = false)
    {
        ArgumentNullException.ThrowIfNull(text);

        CocBlock? block = null;
        int blockCount = 0;
        IReadOnlyList<CocEntry> entries = [];
        if (exists)
        {
            foreach (CocBlock candidate in CocParser.ParseBlocks(text))
            {
                if (candidate.Name != StartupKeys.CocBlockName)
                    continue;
                blockCount++;
                block ??= candidate;
            }

            if (block != null)
                entries = CocParser.ParseEntries(text, block);
        }

        var settings = new List<StartupSettingValue>(StartupKeys.Keys.Length);
        foreach (StartupKeyInfo key in StartupKeys.Keys)
        {
            CocEntry? hit = entries.FirstOrDefault(e => e.Key == key.Name);
            if (hit == null)
            {
                settings.Add(new StartupSettingValue(key, key.DefaultValue, InFile: false, Invalid: false, Raw: null));
                continue;
            }

            (object value, bool valid) = Decode(key, hit.Raw);
            settings.Add(new StartupSettingValue(key, value, InFile: true, Invalid: !valid, hit.Raw));
        }

        return new StartupSettingsState(exists, hasBom, text, block, blockCount, entries, settings);
    }

    /// <summary>
    /// 按游戏的解码规则换算取值。模式键：游戏用 <c>Enum.Parse</c>（区分大小写，去掉首尾空白，也接受数字），
    /// 失败时取 <c>default(T)</c> 即 ModeA；数字超出枚举范围时游戏不报错，但 MapExt 按 ModeA 的 CoreValue 处理，
    /// 这里同样记为读不懂、按 ModeA 显示。开关键只认 <c>true</c>／<c>false</c>。
    /// </summary>
    internal static (object Value, bool Valid) Decode(StartupKeyInfo key, string raw)
    {
        if (key.Kind == StartupKeyKind.Mode)
        {
            string? text = raw;
            if (raw.StartsWith('"'))
            {
                try
                {
                    text = JsonSerializer.Deserialize<string>(raw);
                }
                catch (JsonException)
                {
                    text = null;
                }
            }

            text = text?.Trim();
            if (text != null)
            {
                if (Array.IndexOf(StartupKeys.ModeNames, text) >= 0)
                    return (text, true);
                if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                    && number >= 0 && number < StartupKeys.ModeNames.Length)
                {
                    return (StartupKeys.ModeNames[number], true);
                }
            }

            return (StartupKeys.ModeNames[0], false);
        }

        return raw switch
        {
            "true" => (true, true),
            "false" => (false, true),
            _ => (key.DefaultValue, false),
        };
    }

    /// <summary>值写进 .coc 的原文：模式名带引号，开关为 <c>true</c>／<c>false</c>。</summary>
    internal static string Encode(StartupKeyInfo key, object value) =>
        key.Kind == StartupKeyKind.Mode
            ? "\"" + (string)value + "\""
            : (bool)value ? "true" : "false";
}
