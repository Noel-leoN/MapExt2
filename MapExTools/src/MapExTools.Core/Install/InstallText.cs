using System.Globalization;
using MapExTools.Core.Localization;

namespace MapExTools.Core.Install;

/// <summary>运行位置与快捷方式的文字（计划 2.3），按界面语言生成。</summary>
public static class InstallText
{
    /// <summary>位置一行：常规目录只写路径，其余注明类别。</summary>
    public static string Location(InstallLocation location, string language)
    {
        ArgumentNullException.ThrowIfNull(location);

        return location.Kind == InstallLocationKind.Normal
            ? Format(language, "Install.Location", location.ExePath)
            : Format(language, "Install.LocationKind", location.ExePath, Strings.Get(language, "Install.Kind." + location.Kind));
    }

    /// <summary>桌面快捷方式的状态。</summary>
    public static string Shortcut(ShortcutStatus status, string language)
    {
        ArgumentNullException.ThrowIfNull(status);

        return status.State switch
        {
            ShortcutState.Missing => Strings.Get(language, "Install.Shortcut.Missing"),
            ShortcutState.PointsHere => Strings.Get(language, "Install.Shortcut.Here"),
            ShortcutState.PointsElsewhere => Format(language, "Install.Shortcut.Elsewhere", status.Target ?? string.Empty),
            _ => Strings.Get(language, "Install.Shortcut.Foreign"),
        };
    }

    /// <summary>位置与快捷方式折叠栏的标题摘要。</summary>
    public static string Summary(InstallLocation location, ShortcutStatus? status, string language)
    {
        ArgumentNullException.ThrowIfNull(location);

        string kindText = location.Kind switch
        {
            InstallLocationKind.Normal => Strings.Get(language, "Install.Summary.Normal"),
            InstallLocationKind.Temp => Strings.Get(language, "Install.Summary.Temp"),
            _ => Strings.Get(language, "Install.Kind." + location.Kind),
        };
        if (status == null)
            return kindText;

        string shortcutText = status.State switch
        {
            ShortcutState.Missing => Strings.Get(language, "Install.Summary.ShortcutMissing"),
            ShortcutState.PointsHere => Strings.Get(language, "Install.Summary.ShortcutHere"),
            ShortcutState.PointsElsewhere => Strings.Get(language, "Install.Summary.ShortcutElsewhere"),
            _ => Strings.Get(language, "Install.Summary.ShortcutForeign"),
        };
        return Format(language, "Install.Summary.Join", kindText, shortcutText);
    }

    /// <summary>版本号写三段（1.0.0）；读不到时写"未知"。</summary>
    public static string Version(Version? version, string language) =>
        version == null ? Strings.Get(language, "Install.VersionUnknown")
        : version.Build >= 0 ? version.ToString(3)
        : version.ToString();

    /// <summary>"固定到本地"的确认文字：目标位置，覆盖或降级说明，以及未保存的设定会被放弃。</summary>
    public static string PinConfirm(PinPlan plan, bool hasUnsavedSettings, string language)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var parts = new List<string> { Format(language, "Install.PinConfirm", Path.GetDirectoryName(plan.TargetPath) ?? plan.TargetPath) };
        if (plan.IsDowngrade)
        {
            parts.Add(Format(language, "Install.PinDowngrade",
                Version(plan.TargetVersion, language), Version(plan.SourceVersion, language)));
        }
        else if (plan.ReplacesExisting)
        {
            parts.Add(Format(language, "Install.PinReplace",
                Version(plan.TargetVersion, language), Version(plan.SourceVersion, language)));
        }

        if (hasUnsavedSettings)
            parts.Add(Strings.Get(language, "Install.PinUnsaved"));
        return string.Join("\n\n", parts);
    }

    private static string Format(string language, string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
