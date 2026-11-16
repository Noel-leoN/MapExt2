using System.Globalization;
using System.Text.Json;
using MapExtPDX;

namespace MapExTools.Core.Coc;

/// <summary>
/// 一个受管键按游戏解码规则得到的值。<see cref="Value"/>：Mode/Enum 为枚举名字符串，Bool 为 bool，Int 为 int，Float 为 float。
/// </summary>
/// <param name="InFile">文件的 MapExtPDX 块里有没有这个键；没有时 <see cref="Value"/> 为默认值。</param>
/// <param name="Invalid">文件里的值游戏读不懂、命中已挂起枚举或超出有效范围；界面与保存时按修正后的安全值处理。</param>
/// <param name="Raw">文件里的取值原文；不在文件里时为 null。</param>
public sealed record StartupSettingValue(StartupKeyInfo Key, object Value, bool InFile, bool Invalid, string? Raw);

/// <summary>一项待写入的改动；<see cref="WasInvalid"/> 表示原值无效或越界，值相同也要改写。</summary>
public sealed record StartupSettingChange(StartupKeyInfo Key, object OldValue, object NewValue, bool WasInvalid);

/// <summary>
/// 读取 <c>MapExtPDX.coc</c> 的结果：原文、MapExtPDX 块的位置与各项，以及受管键实际生效的值。
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
        IReadOnlyList<StartupSettingValue> settings,
        IReadOnlyList<StartupSettingValue> allSettings)
    {
        Exists = exists;
        HasBom = hasBom;
        Text = text;
        Block = block;
        BlockCount = blockCount;
        Entries = entries;
        Settings = settings;
        AllSettings = allSettings;
        _byName = allSettings.ToDictionary(s => s.Key.Name, StringComparer.Ordinal);
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

    /// <summary>启动期 9 个核心受管键的值，顺序同 <see cref="StartupKeys.Keys"/>（供命令行与固定样本使用）。</summary>
    public IReadOnlyList<StartupSettingValue> Settings { get; }

    /// <summary>全部 65 个受管键的值，顺序同 <see cref="StartupKeys.AllKeys"/>（供图形界面全量设置中心使用）。</summary>
    public IReadOnlyList<StartupSettingValue> AllSettings { get; }

    /// <summary>9 个启动期键里有没有无效值。</summary>
    public bool HasInvalidValues => Settings.Any(s => s.Invalid);

    /// <summary>全部 65 个受管键里有没有无效值或越界值。</summary>
    public bool HasAnyInvalidValues => AllSettings.Any(s => s.Invalid);

    /// <summary>按键名取值（支持全部 65 个受管键）；不是受管键时抛 <see cref="KeyNotFoundException"/>。</summary>
    public StartupSettingValue this[string keyName] => _byName[keyName];

    /// <summary>9 个启动期键名到值的对照（供命令行与固定样本使用）。</summary>
    public Dictionary<string, object> GetValues() =>
        Settings.ToDictionary(s => s.Key.Name, s => s.Value, StringComparer.Ordinal);

    /// <summary>全部 65 个受管键名到值的对照（供图形界面工作副本初始化使用）。</summary>
    public Dictionary<string, object> GetAllValues() =>
        AllSettings.ToDictionary(s => s.Key.Name, s => s.Value, StringComparer.Ordinal);

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

        var entryByName = new Dictionary<string, CocEntry>(entries.Count, StringComparer.Ordinal);
        foreach (CocEntry entry in entries)
            entryByName[entry.Key] = entry;

        var allSettings = new List<StartupSettingValue>(StartupKeys.AllKeys.Length);
        foreach (StartupKeyInfo key in StartupKeys.AllKeys)
        {
            if (!entryByName.TryGetValue(key.Name, out CocEntry? hit))
            {
                allSettings.Add(new StartupSettingValue(key, key.DefaultValue, InFile: false, Invalid: false, Raw: null));
                continue;
            }

            (object value, bool valid) = Decode(key, hit.Raw);
            allSettings.Add(new StartupSettingValue(key, value, InFile: true, Invalid: !valid, hit.Raw));
        }

        // 前 9 项与 StartupKeys.Keys 一一对应
        var startupSettings = allSettings.GetRange(0, StartupKeys.Keys.Length);
        return new StartupSettingsState(exists, hasBom, text, block, blockCount, entries, startupSettings, allSettings);
    }

    /// <summary>
    /// 按游戏的解码规则换算取值并校验范围。
    /// <list type="bullet">
    /// <item>Mode / Enum：游戏用 <c>Enum.Parse</c>（区分大小写，去掉首尾空白，也接受数字序号），失败时取 <c>default(T)</c>；
    /// 命中已挂起枚举值（如 <c>Adaptive_EventDriven</c>）时同样落回默认值并标为需修正。</item>
    /// <item>Bool：只认 <c>true</c>／<c>false</c>。</item>
    /// <item>Int / Float：不带引号的有限十进制数；无法解析时落回默认值并标无效，超出滑块 <c>[Min, Max]</c>（含 MaxCost &lt;= 0）时夹紧至边界并标无效。</item>
    /// </list>
    /// </summary>
    internal static (object Value, bool Valid) Decode(StartupKeyInfo key, string raw)
    {
        switch (key.Kind)
        {
            case StartupKeyKind.Mode:
            case StartupKeyKind.Enum:
                return DecodeEnum(key, raw);

            case StartupKeyKind.Bool:
                return raw switch
                {
                    "true" => (true, true),
                    "false" => (false, true),
                    _ => (key.DefaultValue, false),
                };

            case StartupKeyKind.Int:
            {
                if (raw.StartsWith('"')
                    || !int.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int iv))
                {
                    return (key.DefaultValue, false);
                }

                int min = (int)Math.Round(key.Min);
                int max = (int)Math.Round(key.Max);
                if (iv < min || iv > max)
                    return (Math.Clamp(iv, min, max), false);
                return (iv, true);
            }

            case StartupKeyKind.Float:
            {
                if (raw.StartsWith('"')
                    || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float fv)
                    || !float.IsFinite(fv))
                {
                    return (key.DefaultValue, false);
                }

                if (fv == 0f)
                    fv = 0f; // 归一化 -0f

                float min = (float)key.Min;
                float max = (float)key.Max;
                if (fv < min - 1e-6f || fv > max + 1e-6f)
                    return (Math.Clamp(fv, min, max), false);
                return (fv, true);
            }

            default:
                return (key.DefaultValue, false);
        }
    }

    private static (object Value, bool Valid) DecodeEnum(StartupKeyInfo key, string raw)
    {
        string[] allNames = key.EnumNames ?? StartupKeys.ModeNames;
        string[] selectable = key.SelectableEnumNames ?? allNames;

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
        string? resolved = null;
        if (text != null)
        {
            if (Array.IndexOf(allNames, text) >= 0)
            {
                resolved = text;
            }
            else if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                     && number >= 0 && number < allNames.Length)
            {
                resolved = allNames[number];
            }
        }

        if (resolved != null && Array.IndexOf(selectable, resolved) >= 0)
            return (resolved, true);

        return ((string)key.DefaultValue, false);
    }

    /// <summary>值写进 .coc 的原文：枚举名带引号，开关为 <c>true</c>／<c>false</c>，整数与浮点为不带科学计数法的十进制字面量。</summary>
    internal static string Encode(StartupKeyInfo key, object value) =>
        key.Kind switch
        {
            StartupKeyKind.Mode or StartupKeyKind.Enum => "\"" + (string)value + "\"",
            StartupKeyKind.Bool => (bool)value ? "true" : "false",
            StartupKeyKind.Int => ((int)value).ToString(CultureInfo.InvariantCulture),
            StartupKeyKind.Float => FormatFloat((float)value),
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

    private static string FormatFloat(float value)
    {
        if (value == 0f)
            return "0";
        decimal exact = (decimal)value;
        return exact.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
