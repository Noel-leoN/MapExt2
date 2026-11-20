using System.Globalization;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Launch;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Tests.Conflicts;

public sealed class ConflictTextTests
{
    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.TraditionalChinese)]
    public void RenderAllFindingKinds_ProducesNonEmptyNeutralText(string lang)
    {
        var settings = new ConflictEvalSettings("ModeA", true, true, true, true, true, false, false);
        var findings = new ConflictFinding[]
        {
            // set_Enabled：角色未知（手工构造）走不区分角色的说法，其余按各角色与适用范围
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Hard, "LandValueSystem", "Mod.OnLoad", "WithCustomSystem"),
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Hard, "LandValueSystem", "Mod.OnLoad", "WithCustomSystem",
                Scope: ConflictScope.AllModes, SystemRole: ProtectedSystemRole.AlwaysReplacedCellMap),
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Hard, "AirPollutionSystem", "Mod.OnLoad", null,
                Scope: ConflictScope.ExpandedMapModes, SystemRole: ProtectedSystemRole.ExpandedOnlyCellMap),
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Hard, "RentAdjustSystem", "NightSystem.OnUpdate", null,
                Scope: ConflictScope.ExpandedMapModes, SystemRole: ProtectedSystemRole.CellOnlyDualRole),
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Warning, "AttractionSystem", "Mod.OnLoad", null,
                Scope: ConflictScope.ExpandedMapModes, SystemRole: ProtectedSystemRole.ExpandedOnlyCellMapCoreHost),
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Warning, "LeisureSystem", "Mod.OnLoad", null,
                [ConflictTargets.KeyDownstreamAI], ConflictScope.EconomyGroups, [ConflictTargets.KeyDownstreamAI],
                ProtectedSystemRole.SharedEconomyReplaced),
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Mitigated, "ResidentialDemandSystem", "Mod.OnLoad", null,
                null, ConflictScope.EconomyGroups, [ConflictTargets.KeyDemand], ProtectedSystemRole.SharedDemandHost),
            new(ConflictFindingKind.HarmonyPatchOverlap, ConflictSeverity.Warning, "PathfindSetupSystem.FindTargets", "Prefix (bool)", "Patch.Prefix",
                null, ConflictScope.EconomyGroups,
                [ConflictTargets.KeyHouseholdProperty, ConflictTargets.KeyResourceBuyer, ConflictTargets.KeyDownstreamAI]),
            new(ConflictFindingKind.ReplacedSystemPatch, ConflictSeverity.Warning, "ServiceCoverageSystem.OnUpdate", "Transpiler", "Patch.Transpiler", [ConflictTargets.KeyResourceBuyer]),
            new(ConflictFindingKind.CrossPhaseSystemSchedule, ConflictSeverity.Hard, "UpdateGroupSystem", "Mod.OnLoad"),
            new(ConflictFindingKind.HarmonyPatchOverlap, ConflictSeverity.Warning, "ServiceCoverageSystem.SetupPathfindMethods", "Prefix (bool)", "Patch.Prefix", [ConflictTargets.KeyResourceBuyer]),
            new(ConflictFindingKind.UnpatchAllNull, ConflictSeverity.Hard, "Mod.OnLoad"),
            new(ConflictFindingKind.UnpatchAllDisposeOnly, ConflictSeverity.Info, "Mod.OnDispose"),
            new(ConflictFindingKind.StaticFieldReflectionWrite, ConflictSeverity.Warning, "kMapSize", "Mutator.Apply"),
            new(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Warning, "RWH", "2.0", null, [ConflictTargets.KeyHouseholdProperty]),
            new(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Warning, "RoadBuilderDualInstall", "0.8.0"),
            new(ConflictFindingKind.WaterSimSpeedWrite, ConflictSeverity.Info, "CustomWaterToolSystem.OnUpdate, TidesAndWavesSystem.OnUpdate"),
            new(ConflictFindingKind.PendingCloudDownload, ConflictSeverity.Info, "86944"),
            new(ConflictFindingKind.MissingDependencies, ConflictSeverity.Info, "ExtraLib"),
            new(ConflictFindingKind.UnreadableAssembly, ConflictSeverity.Info, "Broken.dll", "Bad PE header"),
            new(ConflictFindingKind.CompanionAutoHibernates, ConflictSeverity.Info, "EconomyEX"),
        };

        foreach (ConflictFinding f in findings)
        {
            string desc = ConflictText.DescribeFinding(f, settings, lang);
            Assert.False(string.IsNullOrWhiteSpace(desc));
            Assert.DoesNotContain("Conflicts.", desc, StringComparison.Ordinal);
            Assert.DoesNotContain("Startup.Key.", desc, StringComparison.Ordinal);
            Assert.DoesNotContain("{", desc, StringComparison.Ordinal);

            // 不再用玩家难懂的“受保护原版系统”
            Assert.DoesNotContain("受保护", desc, StringComparison.Ordinal);
            Assert.DoesNotContain("受保護", desc, StringComparison.Ordinal);
            Assert.DoesNotContain("protected", desc, StringComparison.OrdinalIgnoreCase);

            // 写 WaterSimSpeed 的发现原样列出全部调用方法
            if (f.Kind == ConflictFindingKind.WaterSimSpeedWrite)
                Assert.Contains(f.Arg0, desc, StringComparison.Ordinal);
        }

        var item = new ModConflictItem(
            "pdx_mods:77171",
            "Realistic Trips",
            ModInstallSource.PdxMods,
            "77171",
            "2.9.5",
            "2.6",
            IsNewerThanAudited: true,
            ConflictSeverity.Warning,
            findings,
            [ConflictTargets.KeyHouseholdProperty]);

        string verText = ConflictText.VersionLabel(item, lang);
        Assert.Contains("v2.9.5", verText, StringComparison.Ordinal);
        Assert.Contains("v2.6", verText, StringComparison.Ordinal);

        string btnLabel = ConflictText.OneClickButtonLabel(item.OneClickDisableKeys, lang);
        Assert.False(string.IsNullOrWhiteSpace(btnLabel));
    }

    // 每条补充规则都有文案，删掉的规则不留文案键
    [Fact]
    public void RuleTexts_MatchKnownRulesOneToOne()
    {
        const string prefix = "Conflicts.Rule.";
        string[] textIds = Strings.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .Select(k => k[prefix.Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] ruleIds = ConflictTargets.KnownModRules
            .Select(r => r.RuleId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ruleIds, textIds);
    }

    [Fact]
    public void LaunchIntegration_DetectsDisableCodeModdingAcrossCheckboxesCustomArgsAndRunOnce()
    {
        var emptyPlayset = new PlaysetScanResult(
            "p1",
            "TestPlayset",
            true,
            null,
            false,
            [],
            [],
            [],
            new HashSet<string>());
        var workspace = new ScannedModWorkspace(emptyPlayset, new Dictionary<string, IReadOnlyList<AssemblyInspectionResult>>(), 3, 12, DateTime.UtcNow);
        var settings = new ConflictEvalSettings("ModeA", true, true, true, true, false, false, false);
        var hardItem = new ModConflictItem(
            "pdx_mods:121226",
            "Realistic PathFinding",
            ModInstallSource.PdxMods,
            "121226",
            "0.7.6",
            null,
            false,
            ConflictSeverity.Hard,
            [new ConflictFinding(ConflictFindingKind.CrossPhaseSystemSchedule, ConflictSeverity.Hard, "UpdateGroupSystem", "Mod.OnLoad")],
            []);
        var report = new ConflictReport(workspace, settings, [hardItem], HardConflictCount: 1, WarningCount: 0, MitigatedCount: 0, InfoCount: 0);

        // 1. 常规未禁用代码 Mod：给出 Error 级启动提示
        var normalOpts = new LaunchOptions { Channel = LaunchChannel.Direct };
        Assert.False(ConflictText.IsCodeModdingDisabledForLaunch(normalOpts, runOncePending: null));
        DetailLine? errLine = ConflictText.BuildLaunchPageHint(report, normalOpts, null, UiLanguage.SimplifiedChinese);
        Assert.NotNull(errLine);
        Assert.Equal(PackageTone.Error, errLine.Tone);
        Assert.Contains("Realistic PathFinding", errLine.Value, StringComparison.Ordinal);

        // 2. 勾选 DisableCodeModding
        var checkboxOpts = new LaunchOptions { DisableCodeModding = true };
        Assert.True(ConflictText.IsCodeModdingDisabledForLaunch(checkboxOpts, null));
        DetailLine? mutedLine = ConflictText.BuildLaunchPageHint(report, checkboxOpts, null, UiLanguage.SimplifiedChinese);
        Assert.NotNull(mutedLine);
        Assert.Equal(PackageTone.Muted, mutedLine.Tone);

        // 3. CustomArguments 传入 -disableCodeModding
        var customOpts = new LaunchOptions { CustomArguments = "--noSplash -disableCodeModding" };
        Assert.True(ConflictText.IsCodeModdingDisabledForLaunch(customOpts, null));

        // 4. 直启通道下 runOnce.txt 待消费含 --disableModding（但若选 RunOnce 通道因会被覆盖则不看旧文件）
        Assert.True(ConflictText.IsCodeModdingDisabledForLaunch(normalOpts, "--disableModding"));
        var runOnceChannelOpts = new LaunchOptions { Channel = LaunchChannel.RunOnce };
        Assert.False(ConflictText.IsCodeModdingDisabledForLaunch(runOnceChannelOpts, "--disableModding"));
    }

    private static ModConflictItem Item(string packageKey, string name, ConflictSeverity severity) =>
        new(packageKey, name, ModInstallSource.PdxMods, null, "1.0", null, false, severity, [], []);

    private static ConflictReport Report(string mode, params ModConflictItem[] items)
    {
        var playset = new PlaysetScanResult("p1", "TestPlayset", true, null, false, [], [], [], new HashSet<string>());
        var workspace = new ScannedModWorkspace(playset, new Dictionary<string, IReadOnlyList<AssemblyInspectionResult>>(), items.Length, 1, DateTime.UtcNow);
        return new ConflictReport(
            workspace,
            new ConflictEvalSettings(mode, true, true, true, true, false, false, false),
            items,
            HardConflictCount: items.Count(i => i.Severity == ConflictSeverity.Hard),
            WarningCount: items.Count(i => i.Severity == ConflictSeverity.Warning),
            MitigatedCount: items.Count(i => i.Severity == ConflictSeverity.Mitigated),
            InfoCount: items.Count(i => i.Severity == ConflictSeverity.Info));
    }

    // 工作副本里有未保存的模式改动（None），磁盘上仍是 ModeA
    private static readonly ConflictReport s_working = Report(
        "None",
        Item("pdx_mods:1", "ModA", ConflictSeverity.Mitigated),
        Item("pdx_mods:2", "ModB", ConflictSeverity.Warning),
        Item("pdx_mods:4", "ModD", ConflictSeverity.Hard));

    private static readonly ConflictReport s_saved = Report(
        "ModeA",
        Item("pdx_mods:1", "ModA", ConflictSeverity.Hard),
        Item("pdx_mods:2", "ModB", ConflictSeverity.Warning),
        Item("pdx_mods:3", "ModC", ConflictSeverity.Warning),
        Item("pdx_mods:5", "ModE", ConflictSeverity.Info));

    [Fact]
    public void WorseUnderSavedSettings_ListsOnlyModsThatConflictMoreOnDisk()
    {
        // ModA 工作设定下已规避、磁盘上硬冲突；ModC 工作设定下没有条目；ModB 两边相同；ModD 工作设定更严重；ModE 只是备注
        string[] expected = ["ModA", "ModC"];
        Assert.Equal(expected, ConflictAnalyzer.WorseUnderSavedSettings(s_working, s_saved).Select(i => i.DisplayName));
    }

    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.TraditionalChinese)]
    public void UnsavedBasisNote_NamesModsStillConflictingOnDisk(string lang)
    {
        DetailLine plain = ConflictText.UnsavedBasisNote(s_working, saved: null, lang);
        Assert.Equal(Strings.Get(lang, "Conflicts.UnsavedBasisNote"), plain.Value);
        Assert.Equal(PackageTone.Warning, plain.Tone);

        // 已保存设定下没有更严重的模组：仍是通用提示
        Assert.Equal(plain, ConflictText.UnsavedBasisNote(s_working, s_working, lang));

        DetailLine worse = ConflictText.UnsavedBasisNote(s_working, s_saved, lang);
        Assert.Equal(PackageTone.Error, worse.Tone);
        Assert.Contains("ModA", worse.Value, StringComparison.Ordinal);
        Assert.Contains("ModC", worse.Value, StringComparison.Ordinal);
        Assert.Contains(ConflictText.SeverityBadge(ConflictSeverity.Hard, lang), worse.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("ModB", worse.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("{", worse.Value, StringComparison.Ordinal);

        // 只有条件冲突时用警告色
        ConflictReport savedWarnOnly = Report("ModeA", Item("pdx_mods:3", "ModC", ConflictSeverity.Warning));
        Assert.Equal(PackageTone.Warning, ConflictText.UnsavedBasisNote(s_working, savedWarnOnly, lang).Tone);
    }

    private static string Fmt(string lang, string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(lang, key), args);

    // CellOnly（如 RentAdjustSystem）：扩展地图模式下标注扩展地图模式、说明关分组也避不开，并指出原版尺寸模式下只随找房与租金系统组出现；
    // 原版尺寸模式下标注该经济分组，并给出可关闭该分组规避
    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.TraditionalChinese)]
    public void EnabledWrite_CellOnly_ExplainsExpandedVersusVanillaSize(string lang)
    {
        string group = Strings.Get(lang, "Startup.Key." + ConflictTargets.KeyHouseholdProperty);

        var modeA = new ConflictEvalSettings("ModeA", true, true, true, true, false, false, false);
        var hard = new ConflictFinding(
            ConflictFindingKind.SystemEnabledWrite,
            ConflictSeverity.Hard,
            "RentAdjustSystem",
            "CompanyDisableNightNotificationSystem.OnUpdate",
            "WithCustomSystem",
            Scope: ConflictScope.ExpandedMapModes,
            SystemRole: ProtectedSystemRole.CellOnlyDualRole);
        string hardText = ConflictText.DescribeFinding(hard, modeA, lang);
        Assert.StartsWith(Strings.Get(lang, "Conflicts.Scope.Expanded"), hardText, StringComparison.Ordinal);
        Assert.Contains(Fmt(lang, "Conflicts.Find.EnabledWrite.Replaced", hard.Arg1!, hard.Arg0), hardText, StringComparison.Ordinal);
        Assert.Contains(Fmt(lang, "Conflicts.Effect.CellOnlyExpanded", group), hardText, StringComparison.Ordinal);
        Assert.Contains(Strings.Get(lang, "Conflicts.Find.EnabledWrite.CustomSystem"), hardText, StringComparison.Ordinal);

        var none = new ConflictEvalSettings("None", true, true, true, true, false, false, false);
        ConflictFinding warn = hard with
        {
            Severity = ConflictSeverity.Warning,
            MitigatingToggleKeys = [ConflictTargets.KeyHouseholdProperty],
            Scope = ConflictScope.EconomyGroups,
            ScopeGroupKeys = [ConflictTargets.KeyHouseholdProperty],
        };
        string warnText = ConflictText.DescribeFinding(warn, none, lang);
        Assert.StartsWith(Fmt(lang, "Conflicts.Scope.EconomyGroups", group), warnText, StringComparison.Ordinal);
        Assert.Contains(Fmt(lang, "Conflicts.Effect.CellOnlyVanilla", group), warnText, StringComparison.Ordinal);
        Assert.Contains(Fmt(lang, "Conflicts.Note.CanDisableGroup", group), warnText, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.Get(lang, "Conflicts.Scope.Expanded"), warnText, StringComparison.Ordinal);
    }

    // 摘要句以评估模式开头并附模式类别；扫描概况不再含模式；适用范围说明按模式类别给出
    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.TraditionalChinese)]
    public void StatusBanner_LeadsWithEvaluatedModeAndClass(string lang)
    {
        ConflictReport modeA = Report("ModeA", Item("pdx_mods:4", "ModD", ConflictSeverity.Hard));
        string modeALabel = ConflictText.EvalModeLabel(modeA.Settings, lang);
        Assert.Equal(Fmt(lang, "Conflicts.ModeLabel", "ModeA 57km", Strings.Get(lang, "Conflicts.ModeClass.Expanded")), modeALabel);
        Assert.Contains(modeALabel, ConflictText.StatusBanner(modeA, lang).Value, StringComparison.Ordinal);
        Assert.Equal(Strings.Get(lang, "Conflicts.ModeScope.Expanded"), ConflictText.ModeScopeNote(modeA.Settings, lang));
        Assert.DoesNotContain("ModeA", ConflictText.HeaderSummary(modeA, lang), StringComparison.Ordinal);

        ConflictReport none = Report("None", Item("pdx_mods:2", "ModB", ConflictSeverity.Warning));
        string noneLabel = ConflictText.EvalModeLabel(none.Settings, lang);
        Assert.Equal(Fmt(lang, "Conflicts.ModeLabel", "None 14km", Strings.Get(lang, "Conflicts.ModeClass.Vanilla")), noneLabel);
        Assert.Contains(noneLabel, ConflictText.StatusBanner(none, lang).Value, StringComparison.Ordinal);
        Assert.Equal(Strings.Get(lang, "Conflicts.ModeScope.Vanilla"), ConflictText.ModeScopeNote(none.Settings, lang));
    }

    // 启动确认框：以评估模式开头，逐个模组写硬冲突的后果；CellOnly 写重复运行、不越界，不套用 CellMap 越界的说法
    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.SimplifiedChinese)]
    [InlineData(UiLanguage.TraditionalChinese)]
    public void LaunchHardConfirm_StatesModeAndPerModConsequence(string lang)
    {
        var cellOnly = new ConflictFinding(
            ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Hard, "RentAdjustSystem", "Night.OnUpdate",
            Scope: ConflictScope.ExpandedMapModes, SystemRole: ProtectedSystemRole.CellOnlyDualRole);
        var crossPhase = new ConflictFinding(
            ConflictFindingKind.CrossPhaseSystemSchedule, ConflictSeverity.Hard, "UpdateGroupSystem", "Mod.OnLoad",
            Scope: ConflictScope.AllModes);
        var trips = new ModConflictItem(
            "pdx_mods:77171", "Realistic Trips", ModInstallSource.PdxMods, "77171", "2.9.5", null, false,
            ConflictSeverity.Hard, [cellOnly], []);
        var rpf = new ModConflictItem(
            "pdx_mods:121226", "Realistic PathFinding", ModInstallSource.PdxMods, "121226", "0.7.6", null, false,
            ConflictSeverity.Hard, [crossPhase], []);
        ConflictReport report = Report("ModeA", trips, rpf);

        string msg = ConflictText.BuildLaunchHardConfirmMessage(report, lang);
        Assert.Contains(ConflictText.EvalModeLabel(report.Settings, lang), msg, StringComparison.Ordinal);
        Assert.Contains(
            Fmt(lang, "Conflicts.Launch.ConfirmHardItem", "Realistic Trips", ConflictText.SourceLabel(trips, lang),
                Fmt(lang, "Conflicts.Impact.CellOnly", "RentAdjustSystem")),
            msg,
            StringComparison.Ordinal);
        Assert.Contains(Fmt(lang, "Conflicts.Impact.CrossPhase", "UpdateGroupSystem"), msg, StringComparison.Ordinal);
        Assert.DoesNotContain(Fmt(lang, "Conflicts.Impact.CellMap", "RentAdjustSystem"), msg, StringComparison.Ordinal);
        Assert.DoesNotContain("{", msg, StringComparison.Ordinal);
    }
}
