using System.Globalization;
using System.Text;
using MapExTools.Core.Launch;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExtPDX;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>
/// 冲突预检页、模组列表与启动页联动的三语文案拼装（遵循中性可复核事实规范，不贬损或替第三方打包票）。
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

    public static string SourceLabel(ModConflictItem item, string language) =>
        SourceLabel(item.Source, item.PdxModId, language);

    public static string SourceLabel(ModInstallSource source, string? pdxModId, string language) => source switch
    {
        ModInstallSource.UserMods => Strings.Get(language, "Conflicts.Source.UserMods"),
        ModInstallSource.PdxMods when !string.IsNullOrEmpty(pdxModId) =>
            Format(language, "Conflicts.Source.PdxId", pdxModId),
        ModInstallSource.PdxLocal => Strings.Get(language, "Conflicts.Source.PdxLocal"),
        _ => Strings.Get(language, "Conflicts.Source.PdxMods"),
    };

    public static string LoadStateLabel(ModLoadState state, string language) =>
        Strings.Get(language, "Conflicts.Mods.State." + state);

    public static PackageTone LoadStateTone(ModLoadState state) => state switch
    {
        ModLoadState.LoadedCodeMod => PackageTone.Good,
        ModLoadState.AssetOrNonCode => PackageTone.Accent,
        ModLoadState.MissingDependencies or ModLoadState.UnreadableAssembly => PackageTone.Warning,
        _ => PackageTone.Muted,
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

    public static string ModItemVersionLabel(PlaysetModListItem item, string language)
    {
        ArgumentNullException.ThrowIfNull(item);

        string baseVer;
        if (!string.IsNullOrWhiteSpace(item.DisplayVersion))
        {
            baseVer = "v" + item.DisplayVersion.TrimStart('v', 'V');
        }
        else if (item.PlatformVersion.HasValue)
        {
            baseVer = "#" + item.PlatformVersion.Value.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            baseVer = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.LockedPlatformVersion))
        {
            baseVer += Format(language, "Conflicts.Mods.LockedVer", item.LockedPlatformVersion);
        }

        return baseVer.TrimStart();
    }

    /// <summary>单行模组的次要说明文字（作者 · 分类标签 · 包体积 · 胜出 DLL 或缺失依赖 · 适配游戏版本）。</summary>
    public static string ModItemMetadataLine(PlaysetModListItem item, string language)
    {
        ArgumentNullException.ThrowIfNull(item);

        var parts = new List<string>(5);
        if (!string.IsNullOrWhiteSpace(item.Author))
            parts.Add(Format(language, "Conflicts.Mods.Meta.Author", item.Author));

        if (item.Tags.Count > 0)
            parts.Add(string.Join(", ", item.Tags));

        if (item.SizeBytes is > 0 and var bytes)
            parts.Add(FormatFileSize(bytes));

        if (item.WinningAssemblyNames.Count > 0)
            parts.Add(Format(language, "Conflicts.Mods.Meta.Dll", string.Join(", ", item.WinningAssemblyNames)));
        else if (item.MissingDependencies.Count > 0)
            parts.Add(Format(language, "Conflicts.Mods.Meta.Missing", string.Join(", ", item.MissingDependencies)));

        if (!string.IsNullOrWhiteSpace(item.RequiredGameVersion))
            parts.Add(Format(language, "Conflicts.Mods.Meta.GameVer", item.RequiredGameVersion));

        return string.Join(" · ", parts);
    }

    public static string FormatFileSize(ulong bytes)
    {
        if (bytes < 1024UL)
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} B");
        if (bytes < 1024UL * 1024UL)
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:0.0} KB");
        if (bytes < 1024UL * 1024UL * 1024UL)
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):0.0} MB");
        return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0 * 1024.0):0.00} GB");
    }

    /// <summary>生成播放集下拉框单行显示名（同名播放集自动附加 <c>[#后端ID]</c> 或短本地 ID）。</summary>
    public static string FormatPlaysetChoice(PlaysetSummary summary, bool hasDuplicateName, string language)
    {
        ArgumentNullException.ThrowIfNull(summary);

        string displayName = hasDuplicateName
            ? $"{summary.Name} [{DisambiguationTag(summary)}]"
            : summary.Name;

        if (summary.IsActive)
            return Format(language, "Conflicts.Playset.ItemActive", displayName, summary.EnabledModCount, summary.TotalModCount);
        if (summary.IsOffdisk)
            return Format(language, "Conflicts.Playset.ItemOffdisk", displayName, summary.EnabledModCount, summary.TotalModCount);
        return Format(language, "Conflicts.Playset.ItemInactive", displayName, summary.EnabledModCount, summary.TotalModCount);
    }

    public static string DisambiguationTag(PlaysetSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (!string.IsNullOrWhiteSpace(summary.BackendPlaysetId))
            return "#" + summary.BackendPlaysetId;

        return summary.Id.Length > 8 ? summary.Id[..8] : summary.Id;
    }

    /// <summary>扫描概况：目标/激活播放集、启用的代码模组数与扫描耗时（评估模式放在 <see cref="StatusBanner"/> 句首）。</summary>
    public static string HeaderSummary(ConflictReport report, string language)
    {
        ArgumentNullException.ThrowIfNull(report);

        PlaysetScanResult playset = report.Workspace.Playset;
        string name = playset.EffectivePlaysetName ?? Strings.Get(language, "Conflicts.NoActivePlayset");
        string key = playset.IsTargetActivePlayset ? "Conflicts.HeaderSummary" : "Conflicts.HeaderSummary.Preview";

        return Format(
            language,
            key,
            name,
            report.Workspace.EnabledCodeModCount,
            report.Workspace.ScanElapsedMs);
    }

    /// <summary>当所选目标为非激活播放集时给出的只读预览说明。</summary>
    public static string? PreviewInactiveNote(PlaysetScanResult playset, string language)
    {
        ArgumentNullException.ThrowIfNull(playset);
        if (playset.IsTargetActivePlayset)
            return null;

        string name = playset.EffectivePlaysetName ?? playset.EffectivePlaysetId ?? "?";
        return Format(language, "Conflicts.PreviewInactiveNote", name);
    }

    /// <summary>当所选播放集开启了 <c>isOffdisk</c>（不保留本地文件）时的提示。</summary>
    public static string? OffdiskNote(PlaysetScanResult playset, string language)
    {
        ArgumentNullException.ThrowIfNull(playset);
        return playset.IsTargetOffdisk ? Strings.Get(language, "Conflicts.OffdiskNote") : null;
    }

    /// <summary>当目标环境存在已启用模组（或正在预览非激活播放集）但未启用 MapExt 时的知情说明。</summary>
    public static string? MapExtMissingNote(PlaysetScanResult playset, string language)
    {
        ArgumentNullException.ThrowIfNull(playset);
        if (playset.IsMapExtEnabledInTarget)
            return null;

        if (playset.IsTargetActivePlayset && playset.Packages.Count == 0)
            return null;

        return Strings.Get(language, "Conflicts.MapExtMissingNote");
    }

    /// <summary>导出目标播放集与本地 <c>Mods/</c> 的模组清单为 Markdown 表格。</summary>
    public static string FormatModListMarkdown(
        PlaysetScanResult playset,
        ConflictReport? report,
        int enabledCodeModCount,
        string language)
    {
        ArgumentNullException.ThrowIfNull(playset);

        IReadOnlyList<PlaysetModListItem> items = playset.EffectiveAllModItems;
        string titleName = playset.EffectivePlaysetName ?? Strings.Get(language, "Conflicts.NoActivePlayset");
        int enabledTotal = items.Count(m => m.IsEnabled);

        var sb = new StringBuilder();
        sb.AppendLine(Format(language, "Conflicts.Mods.Md.Title", EscapeMarkdownCell(titleName)));
        sb.AppendLine();
        sb.AppendLine(Format(language, "Conflicts.Mods.Md.Summary", items.Count, enabledTotal, enabledCodeModCount));
        sb.AppendLine();
        sb.AppendLine(Strings.Get(language, "Conflicts.Mods.Md.Columns"));
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");

        string yes = Strings.Get(language, "Conflicts.Mods.Md.Yes");
        string no = Strings.Get(language, "Conflicts.Mods.Md.No");
        string localOrder = Strings.Get(language, "Conflicts.Mods.OrderLocal");

        foreach (PlaysetModListItem m in items)
        {
            string orderText = m.LoadOrder.HasValue
                ? m.LoadOrder.Value.ToString(CultureInfo.InvariantCulture)
                : localOrder;
            string enabledText = m.IsEnabled ? yes : no;
            string ver = ModItemVersionLabel(m, language);
            string source = SourceLabel(m.Source, m.PdxModId, language);
            string state = LoadStateLabel(m.LoadState, language);

            string conflict = "-";
            if (report != null && m.IsEnabled && m.LoadState != ModLoadState.Disabled)
            {
                ConflictSeverity sev = report.GetPackageSeverity(m.PackageKey);
                if (sev != ConflictSeverity.None)
                    conflict = SeverityBadge(sev, language);
                else if (m.LoadState == ModLoadState.LoadedCodeMod)
                    conflict = SeverityBadge(ConflictSeverity.None, language);
            }

            string author = string.IsNullOrWhiteSpace(m.Author) ? "-" : m.Author;
            sb.Append("| ").Append(EscapeMarkdownCell(orderText))
              .Append(" | ").Append(EscapeMarkdownCell(enabledText))
              .Append(" | ").Append(EscapeMarkdownCell(m.DisplayName))
              .Append(" | ").Append(EscapeMarkdownCell(string.IsNullOrEmpty(ver) ? "-" : ver))
              .Append(" | ").Append(EscapeMarkdownCell(source))
              .Append(" | ").Append(EscapeMarkdownCell(state))
              .Append(" | ").Append(EscapeMarkdownCell(conflict))
              .Append(" | ").Append(EscapeMarkdownCell(author))
              .AppendLine(" |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string EscapeMarkdownCell(string text) =>
        text.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    /// <summary>评估所用模式的标签并附模式类别，如“ModeA 57km（扩展地图）”“None 14km（原版尺寸）”。</summary>
    public static string EvalModeLabel(ConflictEvalSettings settings, string language)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string modeClass = Strings.Get(
            language,
            settings.IsExpandedMode ? "Conflicts.ModeClass.Expanded" : "Conflicts.ModeClass.Vanilla");
        return Format(language, "Conflicts.ModeLabel", ModeNameWithSize(settings.ModeName), modeClass);
    }

    /// <summary>冲突摘要句，以评估模式开头：同一批模组换一类模式评估，结果可能不同。</summary>
    public static DetailLine StatusBanner(ConflictReport report, string language)
    {
        ArgumentNullException.ThrowIfNull(report);

        string mode = EvalModeLabel(report.Settings, language);

        if (report.HardConflictCount > 0)
        {
            return new DetailLine(
                string.Empty,
                Format(language, "Conflicts.Summary.HasHard", report.HardConflictCount, report.WarningCount, mode),
                PackageTone.Error);
        }

        if (report.WarningCount > 0)
        {
            return new DetailLine(
                string.Empty,
                Format(language, "Conflicts.Summary.HasWarning", report.WarningCount, report.MitigatedCount, mode),
                PackageTone.Warning);
        }

        return new DetailLine(
            string.Empty,
            Format(language, "Conflicts.Summary.Clean", report.Workspace.EnabledCodeModCount, report.MitigatedCount, mode),
            PackageTone.Good);
    }

    /// <summary>冲突预检页顶部说明：结果只适用于评估所用的模式类别与当前分组设定。</summary>
    public static string ModeScopeNote(ConflictEvalSettings settings, string language)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return Strings.Get(
            language,
            settings.IsExpandedMode ? "Conflicts.ModeScope.Expanded" : "Conflicts.ModeScope.Vanilla");
    }

    /// <summary>
    /// 换另一类模式（扩展地图与原版尺寸互换）、沿用同一组分组设定再评估一次；条件冲突及以上的结果与当前不同时给出一行摘要，相同时返回 null。
    /// 只陈述另一类模式下的结果，不作建议：已有的扩展存档不能改用原版尺寸读取。
    /// </summary>
    public static string? CrossModeSummary(ConflictReport report, string language)
    {
        ArgumentNullException.ThrowIfNull(report);

        ConflictEvalSettings current = report.Settings;
        string altMode = current.IsExpandedMode
            ? VanillaModeName
            : StartupKeys.Modes.FirstOrDefault(m => m.Name != VanillaModeName)?.Name ?? "ModeA";
        var alt = new ConflictEvalSettings(
            altMode,
            current.EconomyFix,
            current.Demand,
            current.JobSearch,
            current.HouseholdProperty,
            current.ResourceBuyer,
            current.ResidentAI,
            current.DownstreamAI);
        ConflictReport altReport = ConflictAnalyzer.Evaluate(report.Workspace, alt);

        if (ActiveConflictKeys(report).SetEquals(ActiveConflictKeys(altReport)))
            return null;

        string counts = altReport.HardConflictCount > 0
            ? Format(language, "Conflicts.CrossMode.Hard", altReport.HardConflictCount, altReport.WarningCount)
            : altReport.WarningCount > 0
                ? Format(language, "Conflicts.CrossMode.WarningOnly", altReport.WarningCount)
                : Strings.Get(language, "Conflicts.CrossMode.Clean");

        return current.IsExpandedMode
            ? Format(language, "Conflicts.CrossMode.Vanilla", ModeNameWithSize(altMode), counts)
            : Format(language, "Conflicts.CrossMode.Expanded", counts);
    }

    /// <summary>
    /// 有未保存改动时冲突预检页的评估依据提示。页面按工作设定评估；若有模组按已保存的设定（游戏实际读取的）冲突更重，
    /// 在提示中点名，避免一键关组后页面显示“已规避”、磁盘上的设定却仍冲突时无从察觉。
    /// 预览非激活播放集时改用全局 <c>MapExtPDX.coc</c> 说明文案，避免误指游戏即将加载该播放集。
    /// </summary>
    /// <param name="saved">同一扫描结果按已保存设定评估的报告；读不到已保存设定时传 null，只给通用提示。</param>
    public static DetailLine UnsavedBasisNote(ConflictReport working, ConflictReport? saved, string language)
    {
        ArgumentNullException.ThrowIfNull(working);

        bool isActiveTarget = working.Workspace.Playset.IsTargetActivePlayset;
        string plainKey = isActiveTarget ? "Conflicts.UnsavedBasisNote" : "Conflicts.UnsavedBasisNote.Preview";
        string worseKey = isActiveTarget ? "Conflicts.UnsavedBasisWorse" : "Conflicts.UnsavedBasisWorse.Preview";

        IReadOnlyList<ModConflictItem> worse = saved == null ? [] : ConflictAnalyzer.WorseUnderSavedSettings(working, saved);
        if (worse.Count == 0)
            return new DetailLine(string.Empty, Strings.Get(language, plainKey), PackageTone.Warning);

        string names = string.Join(
            Strings.Get(language, "Startup.ListSeparator"),
            worse.Select(i => Format(language, "Conflicts.SavedSeverityItem", i.DisplayName, SeverityBadge(i.Severity, language))));
        PackageTone tone = worse.Any(i => i.Severity == ConflictSeverity.Hard) ? PackageTone.Error : PackageTone.Warning;
        return new DetailLine(string.Empty, Format(language, worseKey, names), tone);
    }

    /// <summary>单条发现的文字：句首标注适用范围，句末附定级说明（已规避、可关闭某分组规避）。</summary>
    public static string DescribeFinding(ConflictFinding finding, ConflictEvalSettings settings, string language)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(settings);

        string text = finding.Kind switch
        {
            ConflictFindingKind.SystemEnabledWrite => DescribeEnabledWrite(finding, settings, language),

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

        string gap = Gap(language);
        if (finding.Severity == ConflictSeverity.Mitigated)
        {
            text += gap + Strings.Get(language, "Conflicts.Note.MitigatedBySettings");
        }
        else if (finding.Severity == ConflictSeverity.Warning &&
                 finding.MitigatingToggleKeys is { Count: > 0 } keys)
        {
            string toggleNames = JoinToggleNames(keys, language);
            text += gap + Format(language, "Conflicts.Note.CanDisableGroup", toggleNames);
        }

        return ScopeTag(finding, language) is { } tag ? tag + gap + text : text;
    }

    /// <summary>适用范围标签；诊断类发现（待下载、缺前置等）不标注。</summary>
    private static string? ScopeTag(ConflictFinding finding, string language) => finding.Scope switch
    {
        ConflictScope.ExpandedMapModes => Strings.Get(language, "Conflicts.Scope.Expanded"),
        ConflictScope.EconomyGroups when finding.ScopeGroupKeys is { Count: > 0 } keys =>
            Format(language, "Conflicts.Scope.EconomyGroups", JoinToggleNames(keys, language)),
        ConflictScope.AllModes => Strings.Get(language, "Conflicts.Scope.AllModes"),
        _ => null,
    };

    /// <summary>
    /// <c>set_Enabled</c> 发现：按目标系统的角色称为“MapExt 替换的”或“MapExt 修改的”原版系统，再写 MapExt 何时介入及后果。
    /// 角色未知（如手工构造的发现）时用不区分角色的说法。
    /// </summary>
    private static string DescribeEnabledWrite(ConflictFinding finding, ConflictEvalSettings settings, string language)
    {
        string baseKey = finding.SystemRole switch
        {
            null => "Conflicts.Find.EnabledWrite",
            ProtectedSystemRole.ExpandedOnlyCellMapCoreHost
                or ProtectedSystemRole.ExpandedOnlyCellMapAuxHost
                or ProtectedSystemRole.SharedDemandHost => "Conflicts.Find.EnabledWrite.Modified",
            _ => "Conflicts.Find.EnabledWrite.Replaced",
        };

        var parts = new List<string>(3) { Format(language, baseKey, finding.Arg1 ?? "?", finding.Arg0) };
        if (EnabledWriteEffect(finding, settings, language) is { } effect)
            parts.Add(effect);
        if (finding.Arg2 == "WithCustomSystem")
            parts.Add(Strings.Get(language, "Conflicts.Find.EnabledWrite.CustomSystem"));

        return string.Join(Gap(language), parts);
    }

    /// <summary>
    /// 按角色与评估模式写后果。CellOnly 双重归属系统的说法依据冲突检测审计 §8.7（N1）：原版重新运行时取格有边界检查或钳制，
    /// 结果是取值错位与重复结算、不会越界；扩展地图模式下关闭分组也无法避免，原版尺寸模式下只随找房与租金系统组出现。
    /// </summary>
    private static string? EnabledWriteEffect(ConflictFinding finding, ConflictEvalSettings settings, string language)
    {
        string householdGroup = Strings.Get(language, "Startup.Key." + ConflictTargets.KeyHouseholdProperty);
        return finding.SystemRole switch
        {
            ProtectedSystemRole.AlwaysReplacedCellMap or ProtectedSystemRole.ExpandedOnlyCellMap =>
                Strings.Get(language, "Conflicts.Effect.CellMapReplaced"),
            ProtectedSystemRole.CellOnlyDualRole => settings.IsExpandedMode
                ? Format(language, "Conflicts.Effect.CellOnlyExpanded", householdGroup)
                : Format(language, "Conflicts.Effect.CellOnlyVanilla", householdGroup),
            ProtectedSystemRole.SharedEconomyReplaced when finding.ScopeGroupKeys is { Count: > 0 } keys =>
                Format(language, "Conflicts.Effect.EconomyReplaced", JoinToggleNames(keys, language)),
            ProtectedSystemRole.ExpandedOnlyCellMapCoreHost or ProtectedSystemRole.ExpandedOnlyCellMapAuxHost =>
                Strings.Get(language, "Conflicts.Effect.ExpandedHost"),
            ProtectedSystemRole.SharedDemandHost when finding.ScopeGroupKeys is { Count: > 0 } keys =>
                Format(language, "Conflicts.Effect.DemandHost", JoinToggleNames(keys, language)),
            _ => null,
        };
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

    /// <summary>格式化 <see cref="PlaysetActivationResult"/> 的面向用户说明文案（GUI 与 CLI 共用）。</summary>
    public static string FormatActivationResult(PlaysetActivationResult result, bool hasPendingMods, string language)
    {
        ArgumentNullException.ThrowIfNull(result);

        string displayName = !string.IsNullOrWhiteSpace(result.TargetPlaysetName)
            ? result.TargetPlaysetName
            : (result.TargetPlaysetId ?? "?");

        return result.Status switch
        {
            PlaysetActivationStatus.Activated => Format(
                language,
                hasPendingMods ? "Conflicts.Activate.SuccessWithPending" : "Conflicts.Activate.Success",
                displayName),
            PlaysetActivationStatus.AlreadyActive => Format(language, "Conflicts.Activate.AlreadyActive", displayName),
            PlaysetActivationStatus.GameRunning => Strings.Get(language, "Conflicts.Activate.Err.GameRunning"),
            PlaysetActivationStatus.ConfigLocked when !string.IsNullOrWhiteSpace(result.LockUpdatedAt) => Format(
                language,
                "Conflicts.Activate.Err.Locked",
                !string.IsNullOrWhiteSpace(result.LockHolder)
                    ? result.LockHolder
                    : Strings.Get(language, "Conflicts.Activate.UnknownLockHolder"),
                result.LockUpdatedAt),
            PlaysetActivationStatus.ConfigLocked => Strings.Get(language, "Conflicts.Activate.Err.LockedFileInUse"),
            PlaysetActivationStatus.ConfigMissing => Strings.Get(language, "Conflicts.Activate.Err.ConfigMissing"),
            PlaysetActivationStatus.ConfigMalformed => Format(
                language,
                "Conflicts.Activate.Err.ConfigMalformed",
                result.ErrorMessage ?? "?"),
            PlaysetActivationStatus.UnsupportedConfigVersion => Strings.Get(language, "Conflicts.Activate.Err.UnsupportedVersion"),
            PlaysetActivationStatus.PlaysetNotFound => Format(language, "Conflicts.Activate.Err.NotFound", displayName),
            PlaysetActivationStatus.InvalidPlaysetId => Format(language, "Conflicts.Activate.Err.InvalidId", result.TargetPlaysetId ?? string.Empty),
            _ => Format(language, "Conflicts.Activate.Err.WriteFailed", result.ErrorMessage ?? "?"),
        };
    }

    /// <summary>激活后重扫失败且没有可用报告时的启动页提示（告知本次启动前不会做冲突确认）。</summary>
    public static DetailLine BuildLaunchScanErrorHint(string scanError, string language) =>
        new(string.Empty, Format(language, "Conflicts.Launch.ScanError", scanError), PackageTone.Warning);

    /// <summary>激活播放集后新一轮扫描完成前的启动页提示。</summary>
    public static DetailLine BuildLaunchRescanningHint(string language) =>
        new(string.Empty, Strings.Get(language, "Conflicts.Launch.RescanningActive"), PackageTone.Accent);

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

    /// <summary>点击启动时的硬冲突二次确认弹窗文本：以评估模式开头，逐个模组写出硬冲突的后果。</summary>
    public static string BuildLaunchHardConfirmMessage(ConflictReport report, string language)
    {
        ArgumentNullException.ThrowIfNull(report);

        string list = string.Join(
            Environment.NewLine,
            report.Items
                .Where(i => i.Severity == ConflictSeverity.Hard)
                .Select(i => Format(
                    language,
                    "Conflicts.Launch.ConfirmHardItem",
                    i.DisplayName,
                    SourceLabel(i, language),
                    HardImpacts(i, language))));

        return Format(
            language,
            "Conflicts.Launch.ConfirmHard",
            EvalModeLabel(report.Settings, language),
            report.HardConflictCount,
            list);
    }

    /// <summary>
    /// 一个模组各项硬冲突的后果，按种类归并：CellMap 替换系统被开关可能越界；CellOnly 系统是重复运行、不越界（审计 §8.7）；
    /// 跨阶段注册触发 ECB 报错；清除全部补丁使 MapExt 的修改失效。其余种类请玩家看冲突预检页。
    /// </summary>
    private static string HardImpacts(ModConflictItem item, string language)
    {
        var cellMapSystems = new List<string>();
        var cellOnlySystems = new List<string>();
        var crossPhaseSystems = new List<string>();
        bool removesAllPatches = false;
        bool other = false;

        foreach (ConflictFinding f in item.Findings.Where(f => f.Severity == ConflictSeverity.Hard))
        {
            switch (f.Kind)
            {
                case ConflictFindingKind.SystemEnabledWrite
                    when f.SystemRole is ProtectedSystemRole.AlwaysReplacedCellMap or ProtectedSystemRole.ExpandedOnlyCellMap:
                    AddOnce(cellMapSystems, f.Arg0);
                    break;
                case ConflictFindingKind.SystemEnabledWrite when f.SystemRole == ProtectedSystemRole.CellOnlyDualRole:
                    AddOnce(cellOnlySystems, f.Arg0);
                    break;
                case ConflictFindingKind.CrossPhaseSystemSchedule:
                    AddOnce(crossPhaseSystems, f.Arg0);
                    break;
                case ConflictFindingKind.UnpatchAllNull:
                    removesAllPatches = true;
                    break;
                default:
                    other = true;
                    break;
            }
        }

        var phrases = new List<string>();
        if (cellMapSystems.Count > 0)
            phrases.Add(Format(language, "Conflicts.Impact.CellMap", string.Join(", ", cellMapSystems)));
        if (cellOnlySystems.Count > 0)
            phrases.Add(Format(language, "Conflicts.Impact.CellOnly", string.Join(", ", cellOnlySystems)));
        if (crossPhaseSystems.Count > 0)
            phrases.Add(Format(language, "Conflicts.Impact.CrossPhase", string.Join(", ", crossPhaseSystems)));
        if (removesAllPatches)
            phrases.Add(Strings.Get(language, "Conflicts.Impact.UnpatchAll"));
        if (other || phrases.Count == 0)
            phrases.Add(Strings.Get(language, "Conflicts.Impact.SeeConflictTab"));

        return string.Join(Strings.Get(language, "Startup.ListSeparator"), phrases);

        static void AddOnce(List<string> list, string value)
        {
            if (!list.Contains(value, StringComparer.Ordinal))
                list.Add(value);
        }
    }

    /// <summary>原版尺寸模式在 <c>.coc</c> 中的模式名（评估时以此区分扩展地图模式与原版尺寸）。</summary>
    private const string VanillaModeName = "None";

    /// <summary>模式名加地图边长，如“ModeA 57km”；不是现行模式时原样返回模式名。</summary>
    private static string ModeNameWithSize(string modeName)
    {
        StartupModeInfo? mode = StartupKeys.Modes.FirstOrDefault(m => m.Name == modeName);
        return mode != null
            ? string.Create(CultureInfo.InvariantCulture, $"{mode.Name} {mode.MapSize / 1000}km")
            : modeName;
    }

    /// <summary>条件冲突及以上的模组与等级，用于比较两类模式的评估结果是否相同。</summary>
    private static HashSet<(string PackageKey, ConflictSeverity Severity)> ActiveConflictKeys(ConflictReport report) =>
        report.Items
            .Where(i => i.Severity >= ConflictSeverity.Warning)
            .Select(i => (i.PackageKey, i.Severity))
            .ToHashSet();

    /// <summary>相邻两段文字之间的间隔：中文句读自带停顿，不加空格；英文加一个空格。</summary>
    private static string Gap(string language) =>
        language is UiLanguage.SimplifiedChinese or UiLanguage.TraditionalChinese ? string.Empty : " ";

    private static string Format(string language, string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
