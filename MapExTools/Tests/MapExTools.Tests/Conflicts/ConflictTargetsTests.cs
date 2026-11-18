using MapExtPDX.MapExt.Core;

namespace MapExTools.Tests.Conflicts;

/// <summary>共用清单里启动期自动停用经济分组的判定；游戏内 ModConflictDetector 调用的是同一个函数。</summary>
public sealed class ConflictTargetsTests
{
    private static readonly string[] s_allEconomyGroups =
    [
        ConflictTargets.GroupDemand,
        ConflictTargets.GroupJobSearch,
        ConflictTargets.GroupHouseholdProperty,
        ConflictTargets.GroupResourceBuyer,
        ConflictTargets.GroupResidentAI,
        ConflictTargets.GroupDownstreamAI,
    ];

    private static List<(string Group, string TargetId)> Plan(string[] detected, params string[] enabledGroups)
    {
        var enabled = new HashSet<string>(enabledGroups, StringComparer.Ordinal);
        return ConflictTargets.PlanRuntimeAutoDisable(detected, enabled.Contains)
            .Select(d => (d.Group, d.Target.TargetId))
            .ToList();
    }

    [Fact]
    public void AllTargetsDetected_FollowTableOrderAndDisableEachGroupOnce()
    {
        List<(string Group, string TargetId)> plan =
            Plan(["RWH", "RealisticParking", "Time2Work", "RealisticPathFinding"], s_allEconomyGroups);

        // 顺序按清单而非命中顺序；DownstreamAI 记在清单中靠前的 Time2Work 名下，RealisticParking 不再重复停用
        (string, string)[] expected =
        [
            (ConflictTargets.GroupResidentAI, "RealisticPathFinding"),
            (ConflictTargets.GroupResourceBuyer, "RealisticPathFinding"),
            (ConflictTargets.GroupDownstreamAI, "Time2Work"),
            (ConflictTargets.GroupHouseholdProperty, "RWH"),
        ];
        Assert.Equal(expected, plan);
    }

    [Fact]
    public void SharedGroup_GoesToLaterTargetWhenEarlierOneIsAbsent()
    {
        List<RuntimeAutoDisableDecision> plan = ConflictTargets.PlanRuntimeAutoDisable(["RealisticParking"], _ => true);

        RuntimeAutoDisableDecision only = Assert.Single(plan);
        Assert.Equal(ConflictTargets.GroupDownstreamAI, only.Group);
        Assert.Equal("RealisticParking", only.Target.TargetId);
        Assert.Equal("PersonalCarAI", only.Target.OverlapNote);
    }

    [Fact]
    public void GroupsOffInSettings_AreLeftAlone()
    {
        List<(string Group, string TargetId)> plan =
            Plan(["RealisticPathFinding", "RWH"], ConflictTargets.GroupResidentAI);

        (string, string)[] expected = [(ConflictTargets.GroupResidentAI, "RealisticPathFinding")];
        Assert.Equal(expected, plan);
    }

    [Fact]
    public void RuleIdsWithoutAutoDisableTarget_AreIgnored()
    {
        Assert.Empty(ConflictTargets.PlanRuntimeAutoDisable(["WaterFeatures", "RoadBuilderDualInstall", "Unknown"], _ => true));
    }

    // 两边只共用 WaterSimSpeed，MapExt 在各档位都会写它：定级与水模拟档位、地图模式都无关
    [Theory]
    [InlineData("Vanilla_EveryFrame")]
    [InlineData("Reduced_NoBackdrop")]
    [InlineData("Minimal_Every4Frames")]
    [InlineData("Paused_NoFlow")]
    public void WaterFeaturesRule_IsNoteUnderEveryWaterQuality(string waterSimQuality)
    {
        KnownModConflictRule rule = Assert.Single(ConflictTargets.KnownModRules, r => r.RuleId == "WaterFeatures");

        foreach (string mode in new[] { "ModeA", "None" })
        {
            var settings = new ConflictEvalSettings(mode, true, true, true, true, true, true, true, waterSimQuality);
            Assert.Equal(ConflictSeverity.Info, ConflictTargets.EvaluateKnownRule(rule, settings, out string[] keys));
            Assert.Empty(keys);
        }
    }

    [Fact]
    public void AutoDisableTargets_UseEconomyGroupsAndAgreeWithKnownRules()
    {
        RuntimeAutoDisableTarget[] targets = ConflictTargets.RuntimeAutoDisableTargets;
        Assert.Equal(targets.Length, targets.Select(t => t.TargetId).Distinct(StringComparer.Ordinal).Count());

        foreach (RuntimeAutoDisableTarget target in targets)
        {
            Assert.NotEmpty(target.AutoDisableGroups);
            Assert.All(target.AutoDisableGroups, g => Assert.Contains(g, s_allEconomyGroups));

            // 同一模组也在补充规则表里时，两边的识别信息必须一致
            KnownModConflictRule? rule = ConflictTargets.KnownModRules.FirstOrDefault(r => r.RuleId == target.TargetId);
            if (rule != null)
            {
                Assert.Equal(rule.PdxModId, target.PdxModId);
                Assert.Equal(rule.AssemblyNames, target.AssemblyNames);
            }
        }
    }
}
