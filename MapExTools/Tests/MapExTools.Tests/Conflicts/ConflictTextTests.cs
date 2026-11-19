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
            new(ConflictFindingKind.SystemEnabledWrite, ConflictSeverity.Hard, "LandValueSystem", "Mod.OnLoad", "WithCustomSystem"),
            new(ConflictFindingKind.ReplacedSystemPatch, ConflictSeverity.Warning, "ServiceCoverageSystem.OnUpdate", "Transpiler", "Patch.Transpiler", [ConflictTargets.KeyResourceBuyer]),
            new(ConflictFindingKind.CrossPhaseSystemSchedule, ConflictSeverity.Hard, "UpdateGroupSystem", "Mod.OnLoad"),
            new(ConflictFindingKind.HarmonyPatchOverlap, ConflictSeverity.Warning, "ServiceCoverageSystem.SetupPathfindMethods", "Prefix (bool)", "Patch.Prefix", [ConflictTargets.KeyResourceBuyer]),
            new(ConflictFindingKind.UnpatchAllNull, ConflictSeverity.Hard, "Mod.OnLoad"),
            new(ConflictFindingKind.UnpatchAllDisposeOnly, ConflictSeverity.Info, "Mod.OnDispose"),
            new(ConflictFindingKind.StaticFieldReflectionWrite, ConflictSeverity.Warning, "kMapSize", "Mutator.Apply"),
            new(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Warning, "RWH", "2.0", null, [ConflictTargets.KeyHouseholdProperty]),
            new(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Hard, "RealisticPathFinding", "0.7.6"),
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
            "0.7.6",
            false,
            ConflictSeverity.Hard,
            [new ConflictFinding(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Hard, "RealisticPathFinding", "0.7.6")],
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
}
