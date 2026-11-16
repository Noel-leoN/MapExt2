using System.Globalization;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExtPDX;

namespace MapExTools.Core.Coc;

/// <summary>启动与模组设置页的文字格式化：各键、分类、分组、枚举与模式的显示名及改动清单，按界面语言生成。</summary>
public static class StartupSettingsText
{
    /// <summary>经济修复总开关的键名；它关闭时 6 个分组都不注册（<see cref="StartupKeyInfo.EcoGroup"/>）。</summary>
    public const string EconomyMasterKey = "isEnableEconomyFix";

    /// <summary>放在"经济系统"一栏的键：总开关与受它约束的分组。</summary>
    public static bool IsEconomyKey(StartupKeyInfo key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.EcoGroup || key.Name == EconomyMasterKey;
    }

    /// <summary>设定项的显示名，取自 Mod 设置页（去掉字符画前缀）。</summary>
    public static string KeyLabel(StartupKeyInfo key, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Strings.Get(language, "Startup.Key." + key.Name);
    }

    /// <summary>设定项的精炼说明与建议值（悬停提示）。</summary>
    public static string KeyTip(StartupKeyInfo key, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Strings.Get(language, "Startup.Tip." + key.Name);
    }

    /// <summary>一级分类标题。</summary>
    public static string CategoryLabel(OptionCategory category, string language) =>
        Strings.Get(language, "Options.Cat." + category);

    /// <summary>分组卡片标题。</summary>
    public static string GroupLabel(OptionGroup group, string language) =>
        Strings.Get(language, "Options.Group." + group);

    /// <summary>分组卡片右上角的生效时机徽章文字。</summary>
    public static string GroupTiming(OptionGroup group, string language)
    {
        string key = group switch
        {
            OptionGroup.ModeAndConvert or OptionGroup.EcoToggles => "Options.Timing.Restart",
            OptionGroup.EntitiesTraffic => "Options.Timing.LoadOrRestart",
            _ => "Options.Timing.Live",
        };
        return Strings.Get(language, key);
    }

    /// <summary>该分组是否属于需要重启才完整生效的分组（用于徽章着色强调）。</summary>
    public static bool IsRestartGroup(OptionGroup group) =>
        group is OptionGroup.ModeAndConvert or OptionGroup.EcoToggles;

    /// <summary>模式单选项的文字，与 Mod 设置页的下拉项一致，如 <c>ModeA 57km (4x4)</c>。</summary>
    public static string ModeChoice(string modeName, string language) => Strings.Get(language, "Startup.Mode." + modeName);

    /// <summary>枚举下拉项的显示文字。</summary>
    public static string EnumChoice(StartupKeyInfo key, string memberName, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.Kind == StartupKeyKind.Mode
            ? ModeChoice(memberName, language)
            : Strings.Get(language, $"Options.Enum.{key.Name}.{memberName}");
    }

    /// <summary>数值滑块框里显示的纯数字文本（已乘 <see cref="StartupKeyInfo.DisplayScale"/>）。</summary>
    public static string FormatInputNumber(StartupKeyInfo key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Kind == StartupKeyKind.Int)
            return ((int)value).ToString(CultureInfo.InvariantCulture);

        double scaled = Math.Round((float)value * key.DisplayScale, 4);
        return scaled.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>取值的简短写法：模式写 <c>ModeA 57km</c>，枚举写对应档位，开关写开／关，数值带单位。</summary>
    public static string Value(StartupKeyInfo key, object value, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        switch (key.Kind)
        {
            case StartupKeyKind.Mode:
            {
                string name = (string)value;
                int coreValue = StartupKeys.GetCoreValue(name);
                return coreValue > 0 ? PackageText.CoreValueLabel(coreValue, language) : name;
            }
            case StartupKeyKind.Enum:
                return EnumChoice(key, (string)value, language);
            case StartupKeyKind.Bool:
                return Strings.Get(language, (bool)value ? "Startup.On" : "Startup.Off");
            case StartupKeyKind.Int:
            {
                string num = ((int)value).ToString(CultureInfo.InvariantCulture);
                return AppendUnit(num, key.Unit);
            }
            case StartupKeyKind.Float:
            {
                float fv = (float)value;
                if (Math.Abs(key.DisplayScale - 1d) > 1e-9d)
                    return ((decimal)fv).ToString("0.######", CultureInfo.InvariantCulture);
                string num = ((decimal)fv).ToString("0.######", CultureInfo.InvariantCulture);
                return AppendUnit(num, key.Unit);
            }
            default:
                return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>一项改动，如 <c>地图尺寸模式 ModeA 57km→ModeB 28km</c>；原值读不懂或越界时写"修正无效值"。</summary>
    public static string Change(StartupSettingChange change, string language)
    {
        ArgumentNullException.ThrowIfNull(change);

        string label = KeyLabel(change.Key, language);
        string to = Value(change.Key, change.NewValue, language);
        return change.WasInvalid
            ? Format(language, "Startup.ChangeFixInvalid", label, to)
            : Format(language, "Startup.Change", label, Value(change.Key, change.OldValue, language), to);
    }

    /// <summary>完整改动清单，按 <see cref="StartupKeys.AllKeys"/> 的顺序。</summary>
    public static string ChangeList(IEnumerable<StartupSettingChange> changes, string language)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return string.Join(Strings.Get(language, "Startup.ListSeparator"), changes.Select(c => Change(c, language)));
    }

    /// <summary>适合界面状态栏展示的紧凑改动摘要：超过 <paramref name="maxItems"/> 项时截断并附总项数。</summary>
    public static string ChangeListCompact(IReadOnlyList<StartupSettingChange> changes, string language, int maxItems = 5)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count <= maxItems)
            return ChangeList(changes, language);

        string head = string.Join(Strings.Get(language, "Startup.ListSeparator"), changes.Take(maxItems).Select(c => Change(c, language)));
        return Format(language, "Options.ChangesMore", head, changes.Count);
    }

    /// <summary>无效或越界那几项的显示名；没有时为 null。<paramref name="allKeys"/> 为 true 时检查全部 65 个键。</summary>
    public static string? InvalidList(StartupSettingsState state, string language, bool allKeys = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        IReadOnlyList<StartupSettingValue> items = allKeys ? state.AllSettings : state.Settings;
        var labels = items.Where(s => s.Invalid).Select(s => KeyLabel(s.Key, language)).ToList();
        return labels.Count == 0 ? null : string.Join(Strings.Get(language, "Startup.ListSeparator"), labels);
    }

    private static string AppendUnit(string number, string unit) =>
        unit switch
        {
            "" => number,
            "%" or "×" => number + unit,
            _ => number + " " + unit,
        };

    private static string Format(string language, string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
