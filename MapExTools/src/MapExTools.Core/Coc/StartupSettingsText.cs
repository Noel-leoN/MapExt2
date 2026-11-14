using System.Globalization;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExtPDX;

namespace MapExTools.Core.Coc;

/// <summary>启动与设定页的设定文字（计划 2.3）：各键与模式的显示名、改动清单，按界面语言生成。</summary>
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

    /// <summary>设定项的显示名，取自 Mod 设置页（去掉树形前缀）。</summary>
    public static string KeyLabel(StartupKeyInfo key, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Strings.Get(language, "Startup.Key." + key.Name);
    }

    /// <summary>模式单选项的文字，与 Mod 设置页的下拉项一致，如 <c>ModeA 57km (4x4)</c>。</summary>
    public static string ModeChoice(string modeName, string language) => Strings.Get(language, "Startup.Mode." + modeName);

    /// <summary>取值的简短写法：模式写 <c>ModeA 57km</c>，开关写开／关。</summary>
    public static string Value(StartupKeyInfo key, object value, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        if (key.Kind == StartupKeyKind.Mode)
        {
            string name = (string)value;
            int coreValue = StartupKeys.GetCoreValue(name);
            return coreValue > 0 ? PackageText.CoreValueLabel(coreValue, language) : name;
        }

        return Strings.Get(language, (bool)value ? "Startup.On" : "Startup.Off");
    }

    /// <summary>一项改动，如 <c>地图尺寸模式 ModeA 57km→ModeB 28km</c>；原值读不懂时写"修正无效值"。</summary>
    public static string Change(StartupSettingChange change, string language)
    {
        ArgumentNullException.ThrowIfNull(change);

        string label = KeyLabel(change.Key, language);
        string to = Value(change.Key, change.NewValue, language);
        return change.WasInvalid
            ? Format(language, "Startup.ChangeFixInvalid", label, to)
            : Format(language, "Startup.Change", label, Value(change.Key, change.OldValue, language), to);
    }

    /// <summary>改动清单，按 <see cref="StartupKeys.Keys"/> 的顺序。</summary>
    public static string ChangeList(IEnumerable<StartupSettingChange> changes, string language)
    {
        ArgumentNullException.ThrowIfNull(changes);
        return string.Join(Strings.Get(language, "Startup.ListSeparator"), changes.Select(c => Change(c, language)));
    }

    /// <summary>游戏读不懂的那几项的显示名；没有时为 null。</summary>
    public static string? InvalidList(StartupSettingsState state, string language)
    {
        ArgumentNullException.ThrowIfNull(state);

        var labels = state.Settings.Where(s => s.Invalid).Select(s => KeyLabel(s.Key, language)).ToList();
        return labels.Count == 0 ? null : string.Join(Strings.Get(language, "Startup.ListSeparator"), labels);
    }

    private static string Format(string language, string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
