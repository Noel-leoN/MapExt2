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
            new(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Info, "WaterFeatures", "1.4.4"),
            new(ConflictFindingKind.KnownAuditRule, ConflictSeverity.Warning, "RoadBuilderDualInstall", "0.8.0"),
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
}
