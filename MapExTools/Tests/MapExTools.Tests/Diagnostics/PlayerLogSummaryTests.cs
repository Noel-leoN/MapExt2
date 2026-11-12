using MapExTools.Core.Diagnostics;

namespace MapExTools.Tests.Diagnostics;

/// <summary>Player.log：游戏版本、未捕获异常与转写进来的错误、结束状态。</summary>
public sealed class PlayerLogSummaryTests
{
    [Fact]
    public void CrashSample_VersionProblemsAndCrashMarker()
    {
        PlayerLogSummary s = PlayerLogSummary.Parse(LogSamples.ReadLines("Player-crash.txt"));

        Assert.Equal("1.6.4f1 (1000.0001) [6400.20000]", s.GameVersion);
        Assert.Equal(2, s.ProblemCount);

        PlayerLogProblem exception = s.RecentProblems[0];
        Assert.Equal(4, exception.LineNumber);
        Assert.Equal(PlayerLogProblemKind.Exception, exception.Kind);
        Assert.Equal("NullReferenceException", exception.Source);
        Assert.Equal("at Game.Sample.SampleSystem.OnUpdate () [0x00000] in <00000000000000000000000000000000>:0", exception.FirstFrame);

        PlayerLogProblem logged = s.RecentProblems[1];
        Assert.Equal(PlayerLogProblemKind.LoggedError, logged.Kind);
        Assert.Equal("SceneFlow", logged.Source);
        Assert.StartsWith("[SceneFlow] [CRITICAL]  System update error", logged.Text, StringComparison.Ordinal);

        Assert.True(s.HasCrashMarker);
        Assert.False(s.HasExitRecord);
        Assert.Equal(PlayerLogEndState.Crashed, s.GetEndState(sessionInProgress: false));
    }

    [Fact]
    public void ExitRecord_MeansExitedNormally()
    {
        PlayerLogSummary s = PlayerLogSummary.Parse(LogSamples.Lines("""
            Game version: 1.6.3f1 (1064.3599) [6354.19879]
            Memory Statistics:
              Peak Allocated memory 14.0 KB
            """));

        Assert.Equal(PlayerLogEndState.ExitedNormally, s.GetEndState(sessionInProgress: false));
        Assert.Equal(0, s.ProblemCount);
    }

    [Theory]
    [InlineData(true, PlayerLogEndState.InProgress)]
    [InlineData(false, PlayerLogEndState.NoExitRecord)]
    public void NoExitRecord_DependsOnWhetherTheSessionIsRunning(bool inProgress, PlayerLogEndState expected)
    {
        PlayerLogSummary s = PlayerLogSummary.Parse(["Game version: 1.6.4f1 (1000.0001) [6400.20000]", "Loading..."]);
        Assert.Equal(expected, s.GetEndState(inProgress));
    }

    [Theory]
    [InlineData("System.InvalidOperationException: Collection was modified", "System.InvalidOperationException")]
    [InlineData("ArgumentException: Invalid path", "ArgumentException")]
    [InlineData("Unity.Entities.EntityQueryException", "Unity.Entities.EntityQueryException")]
    public void ExceptionLines_AreRecognized(string line, string type)
    {
        PlayerLogSummary s = PlayerLogSummary.Parse([line]);
        PlayerLogProblem problem = Assert.Single(s.RecentProblems);
        Assert.Equal(type, problem.Source);
        Assert.Null(problem.FirstFrame);
    }

    [Theory]
    [InlineData("  at Some.Method () [0x00000] in <hash>:0")] // 堆栈行缩进
    [InlineData("An Exception: happened")]                    // 有空格，不是类型名
    [InlineData("[SampleMod] [WARN]  only a warning")]
    [InlineData("[SampleMod] [INFO]  Exception: not an error")]
    public void OtherLines_AreNotProblems(string line)
    {
        Assert.Equal(0, PlayerLogSummary.Parse([line]).ProblemCount);
    }

    [Fact]
    public void RecentProblems_KeepsOnlyTheLastOnes()
    {
        string[] lines = Enumerable.Range(1, PlayerLogSummary.MaxRecentProblems + 3)
            .Select(i => $"[Logger{i}] [ERROR]  failure {i}")
            .ToArray();

        PlayerLogSummary s = PlayerLogSummary.Parse(lines);

        Assert.Equal(PlayerLogSummary.MaxRecentProblems + 3, s.ProblemCount);
        Assert.Equal(PlayerLogSummary.MaxRecentProblems, s.RecentProblems.Count);
        Assert.Equal($"Logger{PlayerLogSummary.MaxRecentProblems + 3}", s.RecentProblems[^1].Source);
    }
}
