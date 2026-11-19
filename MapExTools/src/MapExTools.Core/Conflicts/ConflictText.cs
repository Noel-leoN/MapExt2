using System.Globalization;
using MapExTools.Core.Launch;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExtPDX;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>
/// 冲突预检页与启动页联动的三语文案拼装（遵循中性可复核事实规范，不贬损或替第三方打包票）。
/// </summary>
public static class ConflictText
{
    public static PackageTone ToneOf(ConflictSeverity severity) => severity switch
    {
        ConflictSeverity.Hard => PackageTone.Error,
        ConflictSeverity.Warning => PackageTone.Warning,
        ConflictSeverity.Mitigated => PackageTone.Accent,
        ConflictSeverity.Info => PackageTone.Muted,
        _ => PackageTone.Good,
    };

    public static string SeverityBadge(ConflictSeverity severity, string language) => severity switch
    {
        ConflictSeverity.Hard => Strings.Get(language, "Conflicts.Badge.Hard"),
        ConflictSeverity.Warning => Strings.Get(language, "Conflicts.Badge.Warning"),
        ConflictSeverity.Mitigated => Strings.Get(language, "Conflicts.Badge.Mitigated"),
        ConflictSeverity.Info => Strings.Get(language, "Conflicts.Badge.Info"),
        _ => Strings.Get(language, "Conflicts.Badge.Clean"),
    };

    public static string SourceLabel(ModConflictItem item, string language) => item.Source switch
    {
        ModInstallSource.UserMods => Strings.Get(language, "Conflicts.Source.UserMods"),
        ModInstallSource.PdxMods when !string.IsNullOrEmpty(item.PdxModId) =>
            Format(language, "Conflicts.Source.PdxId", item.PdxModId),
        ModInstallSource.PdxLocal => Strings.Get(language, "Conflicts.Source.PdxLocal"),
        _ => Strings.Get(language, "Conflicts.Source.PdxMods"),
    };

    public static string VersionLabel(ModConflictItem item, string language)
    {
        string installed = !string.IsNullOrWhiteSpace(item.InstalledVersion)
            ? "v" + item.InstalledVersion.TrimStart('v', 'V')
            : Strings.Get(language, "Install.VersionUnknown");

        if (string.IsNullOrWhiteSpace(item.AuditedVersion))
            return installed;

        string audited = "v" + item.AuditedVersion.TrimStart('v', 'V');
        return item.IsNewerThanAudited
            ? Format(language, "Conflicts.Version.NewerThanAudited", installed, audited)
            : Format(language, "Conflicts.Version.WithAudited", installed, audited);
    }

    public static string HeaderSummary(ConflictReport report, string language)
    {
        ArgumentNullException.ThrowIfNull(report);

        string playset = report.Workspace.Playset.ActivePlaysetName
                         ?? Strings.Get(language, "Conflicts.NoActivePlayset");
        StartupModeInfo? modeInfo = StartupKeys.Modes.FirstOrDefault(m => m.Name == report.Settings.ModeName);
        string modeLabel = modeInfo != null ? PackageText.ModeLabel(modeInfo, language) : report.Settings.ModeName;

        return Format(
            language,
            "Conflicts.HeaderSummary",
            playset,
            report.Workspace.EnabledCodeModCount,
            modeLabel,
            report.Workspace.ScanElapsedMs);
    }

    public static DetailLine StatusBanner(ConflictReport report, string language)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.HardConflictCount > 0)
        {
            return new DetailLine(
                string.Empty,
                Format(language, "Conflicts.Summary.HasHard", report.HardConflictCount, report.WarningCount),
                PackageTone.Error);
        }

        if (report.WarningCount > 0)
        {
            return new DetailLine(
                string.Empty,
                Format(language, "Conflicts.Summary.HasWarning", report.WarningCount, report.MitigatedCount),
                PackageTone.Warning);
        }

        return new DetailLine(
            string.Empty,
            Format(language, "Conflicts.Summary.Clean", report.Workspace.EnabledCodeModCount, report.MitigatedCount),
            PackageTone.Good);
    }

    /// <summary>
    /// 有未保存改动时冲突预检页的评估依据提示。页面按工作设定评估；若有模组按已保存的设定（游戏实际读取的）冲突更重，
    /// 在提示中点名，避免一键关组后页面显示“已规避”、磁盘上的设定却仍冲突时无从察觉。
    /// </summary>
    /// <param name="saved">同一扫描结果按已保存设定评估的报告；读不到已保存设定时传 null，只给通用提示。</param>
    public static DetailLine UnsavedBasisNote(ConflictReport working, ConflictReport? saved, string language)
    {
        ArgumentNullException.ThrowIfNull(working);

        IReadOnlyList<ModConflictItem> worse = saved == null ? [] : ConflictAnalyzer.WorseUnderSavedSettings(working, saved);
        if (worse.Count == 0)
            return new DetailLine(string.Empty, Strings.Get(language, "Conflicts.UnsavedBasisNote"), PackageTone.Warning);

        string names = string.Join(
            Strings.Get(language, "Startup.ListSeparator"),
            worse.Select(i => Format(language, "Conflicts.SavedSeverityItem", i.DisplayName, SeverityBadge(i.Severity, language))));
        PackageTone tone = worse.Any(i => i.Severity == ConflictSeverity.Hard) ? PackageTone.Error : PackageTone.Warning;
        return new DetailLine(string.Empty, Format(language, "Conflicts.UnsavedBasisWorse", names), tone);
    }

    public static string DescribeFinding(ConflictFinding finding, ConflictEvalSettings settings, string language)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(settings);

        string text = finding.Kind switch
        {
            ConflictFindingKind.SystemEnabledWrite => finding.Arg2 == "WithCustomSystem"
                ? Format(language, "Conflicts.Find.EnabledWriteReplace", finding.Arg1 ?? "?", finding.Arg0)
                : Format(language, "Conflicts.Find.EnabledWrite", finding.Arg1 ?? "?", finding.Arg0),

            ConflictFindingKind.ReplacedSystemPatch =>
                Format(language, "Conflicts.Find.ReplacedSystemPatch", finding.Arg2 ?? "?", finding.Arg1 ?? "Patch", finding.Arg0),

            ConflictFindingKind.CrossPhaseSystemSchedule =>
                Format(language, "Conflicts.Find.CrossPhaseSchedule", finding.Arg1 ?? "?", finding.Arg0),

            ConflictFindingKind.HarmonyPatchOverlap =>
                Format(language, "Conflicts.Find.HarmonyOverlap", finding.Arg2 ?? "?", finding.Arg1 ?? "Patch", finding.Arg0),

            ConflictFindingKind.UnpatchAllNull => finding.Severity == ConflictSeverity.Hard
                ? Format(language, "Conflicts.Find.UnpatchAllHard", finding.Arg0)
                : Format(language, "Conflicts.Find.UnpatchAllCallback", finding.Arg0),

            ConflictFindingKind.UnpatchAllDisposeOnly =>
                Format(language, "Conflicts.Find.UnpatchAllDispose", finding.Arg0),

            ConflictFindingKind.StaticFieldReflectionWrite =>
                Format(language, "Conflicts.Find.StaticFieldWrite", finding.Arg1 ?? "?", finding.Arg0),

            ConflictFindingKind.WaterSimSpeedWrite =>
                Format(language, "Conflicts.Find.WaterSimSpeedWrite", finding.Arg0),

            ConflictFindingKind.KnownAuditRule =>
                Strings.Get(language, "Conflicts.Rule." + finding.Arg0),

            ConflictFindingKind.PendingCloudDownload =>
                Format(language, "Conflicts.Find.PendingCloud", finding.Arg0),

            ConflictFindingKind.MissingDependencies =>
                Format(language, "Conflicts.Find.MissingDeps", finding.Arg0),

            ConflictFindingKind.UnreadableAssembly =>
                Format(language, "Conflicts.Find.UnreadableDll", finding.Arg0, finding.Arg1 ?? string.Empty),

            ConflictFindingKind.CompanionAutoHibernates =>
                Strings.Get(language, "Conflicts.Find.EconomyExHibernates"),

            _ => finding.Arg0,
        };

        if (finding.Severity == ConflictSeverity.Mitigated)
        {
            text += " " + Strings.Get(language, "Conflicts.Note.MitigatedBySettings");
        }
        else if (finding.Severity == ConflictSeverity.Warning &&
                 finding.MitigatingToggleKeys is { Count: > 0 } keys)
        {
            string toggleNames = JoinToggleNames(keys, language);
            text += " " + Format(language, "Conflicts.Note.CanDisableGroup", toggleNames);
        }

        return text;
    }

    public static string OneClickButtonLabel(IReadOnlyList<string> toggleKeys, string language)
    {
        string names = JoinToggleNames(toggleKeys, language);
        return Format(language, "Conflicts.Action.DisableGroups", names);
    }

    public static string JoinToggleNames(IReadOnlyList<string> toggleKeys, string language)
    {
        string sep = Strings.Get(language, "Startup.ListSeparator");
        return string.Join(sep, toggleKeys.Select(k => Strings.Get(language, "Startup.Key." + k)));
    }

    /// <summary>
    /// 合并判断本次启动配置（复选框、<c>CustomArguments</c> 与待消费 <c>runOnce.txt</c>）是否已传入禁用代码 Mod 参数。
    /// </summary>
    public static bool IsCodeModdingDisabledForLaunch(LaunchOptions options, string? runOncePending)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DisableCodeModding || options.DisableModding)
            return true;

        if (ContainsDisableModdingFlag(options.CustomArguments))
            return true;

        if (options.Channel != LaunchChannel.RunOnce && ContainsDisableModdingFlag(runOncePending))
            return true;

        return false;
    }

    public static bool ContainsDisableModdingFlag(string? rawArguments)
    {
        if (string.IsNullOrWhiteSpace(rawArguments))
            return false;

        string normalized = LaunchArgumentBuilder.NormalizeCustom(rawArguments);
        foreach (string token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string cleaned = token.Trim('"');
            if (string.Equals(cleaned, "-disableCodeModding", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "--disableCodeModding", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "-disableModding", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cleaned, "--disableModding", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>启动页状态行摘要：代码 Mod 被禁用时说明冲突本次不生效；存在硬冲突时给出红色提示。</summary>
    public static DetailLine? BuildLaunchPageHint(
        ConflictReport? report,
        LaunchOptions options,
        string? runOncePending,
        string language)
    {
        if (report == null)
            return null;

        bool modsDisabled = IsCodeModdingDisabledForLaunch(options, runOncePending);
        if (modsDisabled && (report.HardConflictCount > 0 || report.WarningCount > 0))
        {
            return new DetailLine(
                string.Empty,
                Strings.Get(language, "Conflicts.Launch.CodeModsDisabled"),
                PackageTone.Muted);
        }

        if (report.HardConflictCount > 0)
        {
            string names = string.Join(", ", report.Items
                .Where(i => i.Severity == ConflictSeverity.Hard)
                .Select(i => i.DisplayName));
            return new DetailLine(
                string.Empty,
                Format(language, "Conflicts.Launch.HardBanner", report.HardConflictCount, names),
                PackageTone.Error);
        }

        if (report.WarningCount > 0)
        {
            return new DetailLine(
                string.Empty,
                Format(language, "Conflicts.Launch.WarnBanner", report.WarningCount),
                PackageTone.Warning);
        }

        return null;
    }

    /// <summary>点击启动时的硬冲突二次确认弹窗文本。</summary>
    public static string BuildLaunchHardConfirmMessage(ConflictReport report, string language)
    {
        string list = string.Join(
            Environment.NewLine,
            report.Items
                .Where(i => i.Severity == ConflictSeverity.Hard)
                .Select(i => $"• {i.DisplayName} ({SourceLabel(i, language)})"));

        return Format(language, "Conflicts.Launch.ConfirmHard", report.HardConflictCount, list);
    }

    private static string Format(string language, string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
