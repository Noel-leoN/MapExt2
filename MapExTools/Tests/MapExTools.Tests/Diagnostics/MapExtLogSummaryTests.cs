using MapExTools.Core.Diagnostics;

namespace MapExTools.Tests.Diagnostics;

/// <summary>MapExtPDX.log 的会话摘要。匹配的措辞来自 Mod 源码，改 Mod 日志时要同步这里与样本。</summary>
public sealed class MapExtLogSummaryTests
{
    private static MapExtLogSummary ParseSample() => MapExtLogSummary.Parse(LogSamples.ReadLines("MapExtPDX-v4.12.txt"));

    private static string Line(string level, string tag, string text) =>
        $"[2026-11-12 20:00:00,000] [{level}]  [MapExt|{tag}] 🔹 {text}";

    [Fact]
    public void Sample_VersionTimesAndCounts()
    {
        MapExtLogSummary s = ParseSample();

        Assert.Equal("4.12.0", s.ModVersion);
        Assert.Equal("2026-11-12 20:01:02,100", s.FirstTimestamp);
        Assert.Equal("2026-11-12 20:30:00,000", s.LastTimestamp);
        Assert.Equal(3, s.WarningCount);
        Assert.Equal(1, s.ErrorCount);
        Assert.Equal(4, s.RecentProblems.Count);

        LogEntry error = s.RecentProblems[^1];
        Assert.Equal("ERROR", error.Level);
        Assert.Contains("System.InvalidOperationException: Sample failure", error.Continuation);
    }

    [Fact]
    public void Sample_AppliedConfigAndBurst()
    {
        MapExtLogSummary s = ParseSample();

        AppliedConfigInfo? config = s.AppliedConfig;
        Assert.NotNull(config);
        Assert.Equal("2026-11-12 20:01:09,903", config.Timestamp);
        Assert.Equal("ModeA", config.Mode);
        Assert.Equal(4, config.CoreValue);
        Assert.Equal("False", config.Values["ResBuyer"]);
        Assert.Equal(["ResidentAI"], config.AutoDisabled);
        Assert.Equal(10, config.Values.Count);
        Assert.True(s.BurstEnabled);
    }

    [Fact]
    public void Sample_Conflicts()
    {
        ConflictInfo c = ParseSample().Conflicts;

        Assert.True(c.Scanned);
        Assert.False(c.NoneDetected);
        Assert.Equal(["检测到冲突 Mod: SampleModA (ResidentAI/ResourceBuyer 系统替换)"], c.Detected);
        Assert.Equal(["Auto-disabled ResidentAI group (conflict: SampleModA)"], c.AutoDisabled);
    }

    [Fact]
    public void Sample_LastGateDecisionIsTheLatestSaveLoad()
    {
        GateDecisionInfo? d = ParseSample().LastGateDecision;

        Assert.NotNull(d);
        Assert.Equal("MenuLoad", d.Entry);
        Assert.Equal(GateSubject.Save, d.Subject);
        Assert.Equal("Sample City", d.Name);
        Assert.Equal("User/fedcba9876543210fedcba9876543210", d.RawId);
        Assert.Equal("fedcba9876543210fedcba9876543210", d.AssetId);
        Assert.Equal("CV=4", d.SaveDescription);
        Assert.Equal("Success", d.Result);
        Assert.Equal(4, d.CurrentCoreValue);
        Assert.Empty(d.FollowUps);
    }

    [Fact]
    public void MapDecision_ParsesInferenceAndFollowUps()
    {
        MapExtLogSummary s = MapExtLogSummary.Parse(
        [
            Line("INFO", "SaveEntryGate", "MenuNewGame 地图 'Sample Map 28km'（ID User/0123456789abcdef0123456789abcdef）：面积 8.2208E+008，比值 4，推断 CV=2（可信），当前 CV=4"),
            Line("INFO", "SaveEntryGate", "MenuNewGame 地图 'Sample Map 28km'：为 CV=2 制作，与当前模式不符，拦下"),
        ]);

        GateDecisionInfo? d = s.LastGateDecision;
        Assert.NotNull(d);
        Assert.Equal(GateSubject.Map, d.Subject);
        Assert.Equal("Sample Map 28km", d.Name);
        Assert.Equal("0123456789abcdef0123456789abcdef", d.AssetId);
        Assert.Equal(2, d.InferredCoreValue);
        Assert.True(d.InferenceTrusted);
        Assert.Equal(4, d.CurrentCoreValue);
        Assert.Equal(["MenuNewGame 地图 'Sample Map 28km'：为 CV=2 制作，与当前模式不符，拦下"], d.FollowUps);
    }

    [Fact]
    public void MapDecision_UntrustedInference()
    {
        GateDecisionInfo? d = MapExtLogSummary.Parse(
        [
            Line("INFO", "SaveEntryGate", "Convergence 地图 'Odd Map'（ID User/abc）：面积 5.2613E+010，比值 256，推断 CV=16（不可信，放行，载入后按地形实际尺寸判），当前 CV=4"),
        ]).LastGateDecision;

        Assert.NotNull(d);
        Assert.Equal("Convergence", d.Entry);
        Assert.Equal(16, d.InferredCoreValue);
        Assert.False(d.InferenceTrusted);
    }

    [Fact]
    public void SaveDecision_VanillaSaveWithConversionFollowUp()
    {
        GateDecisionInfo? d = MapExtLogSummary.Parse(
        [
            Line("INFO", "SaveEntryGate", "MenuContinue 'Old City'（ID User/1111）：存档 无 MapExt 标记（原版），当前 CV=4，结果 ModNotUsed"),
            Line("INFO", "SaveEntryGate", "玩家确认转换 'Old City' → ModeA"),
            Line("INFO", "SaveEntryGate", "MenuContinue 'Old City'：转换请求绑定的存档，放行"),
        ]).LastGateDecision;

        // 确认转换后重新触发的那一次放行，成为最近一次判定
        Assert.NotNull(d);
        Assert.Equal(GateSubject.ConversionRequest, d.Subject);
        Assert.Equal("Old City", d.Name);
        Assert.Null(d.AssetId);
    }

    [Fact]
    public void SaveDecision_ModNotUsedIsParsed()
    {
        GateDecisionInfo? d = MapExtLogSummary.Parse(
        [
            Line("INFO", "SaveEntryGate", "MenuLoad 'Old City'（ID User/1111）：存档 无 MapExt 标记（原版），当前 CV=4，结果 ModNotUsed"),
        ]).LastGateDecision;

        Assert.NotNull(d);
        Assert.Equal("无 MapExt 标记（原版）", d.SaveDescription);
        Assert.Equal("ModNotUsed", d.Result);
        Assert.Equal("1111", d.AssetId);
    }

    [Fact]
    public void BurstDisabled_IsFalse()
    {
        MapExtLogSummary s = MapExtLogSummary.Parse(
        [
            "[2026-11-12 20:00:00,000] [WARN]  [MapExt|Mod] ⚠️ 本会话 Burst: 已停用（--burst-disable-compilation 或 UNITY_BURST_DISABLE_COMPILATION），Job 改走 Mono，运行会明显变慢",
        ]);

        Assert.False(s.BurstEnabled);
        Assert.Equal(1, s.WarningCount);
    }

    [Fact]
    public void OlderModVersion_HasNoBurstLineOrGateDecision()
    {
        MapExtLogSummary s = MapExtLogSummary.Parse(
        [
            Line("INFO", "Mod", "OnLoad, version:4.11.0"),
            "[2026-11-12 20:00:01,000] [INFO]  [MapExt|ModConflictDetector] ✅ 未检测到已知冲突 Mod",
            Line("INFO", "Mod", "本会话生效配置: Mode=ModeB, CV=2, EcoFix=False, Demand=True, Job=True, Household=True, ResBuyer=False, ResidentAI=False, DownAI=False, AutoDisabled=[]"),
        ]);

        Assert.Equal("4.11.0", s.ModVersion);
        Assert.Null(s.BurstEnabled);
        Assert.Null(s.LastGateDecision);
        Assert.True(s.Conflicts.NoneDetected);
        Assert.Equal(2, s.AppliedConfig?.CoreValue);
        Assert.Empty(s.AppliedConfig!.AutoDisabled);
    }

    [Fact]
    public void EmptyLog_HasNothing()
    {
        MapExtLogSummary s = MapExtLogSummary.Parse([]);

        Assert.Null(s.ModVersion);
        Assert.Null(s.AppliedConfig);
        Assert.Null(s.FirstTimestamp);
        Assert.False(s.Conflicts.Scanned);
        Assert.Equal(0, s.WarningCount);
    }

    [Fact]
    public void RecentProblems_KeepsOnlyTheLastOnes()
    {
        string[] lines = Enumerable.Range(1, MapExtLogSummary.MaxRecentProblems + 5)
            .Select(i => $"[2026-11-12 20:00:{i:00},000] [WARN]  [MapExt|Sample] ⚠️ 第 {i} 条")
            .ToArray();

        MapExtLogSummary s = MapExtLogSummary.Parse(lines);

        Assert.Equal(MapExtLogSummary.MaxRecentProblems + 5, s.WarningCount);
        Assert.Equal(MapExtLogSummary.MaxRecentProblems, s.RecentProblems.Count);
        Assert.EndsWith($"第 {MapExtLogSummary.MaxRecentProblems + 5} 条", s.RecentProblems[^1].Message, StringComparison.Ordinal);
    }
}
