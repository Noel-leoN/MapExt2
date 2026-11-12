using MapExTools.Core.Diagnostics;

namespace MapExTools.Tests.Diagnostics;

/// <summary>逐行判定级别：游戏日志器格式的后续行沿用上一条；Player.log 里只有缩进的堆栈行沿用。</summary>
public sealed class LogLineClassifierTests
{
    private static LogLevelKind[] ClassifyAll(params string[] lines)
    {
        var classifier = new LogLineClassifier();
        return lines.Select(classifier.Classify).ToArray();
    }

    [Fact]
    public void ColossalFormat_ContinuationLinesInheritTheLevel()
    {
        LogLevelKind[] levels = ClassifyAll(
            "[2026-11-12 20:00:00,000] [INFO]  Command line: Cities2.exe",
            "--developerMode",
            "[2026-11-12 20:00:01,000] {Some Context} [WARN]  Duplicate prefab ID",
            "[2026-11-12 20:00:02,000] [ERROR]  [MapExt|Sample] ❌ failed",
            "System.InvalidOperationException: boom",
            "  at A.B ()",
            "",
            "[2026-11-12 20:00:03,000] [VERBOSE]  detail",
            "[2026-11-12 20:00:04,000] [CRITICAL]  worse");

        Assert.Equal(
        [
            LogLevelKind.Info, LogLevelKind.Info,
            LogLevelKind.Warning,
            LogLevelKind.Error, LogLevelKind.Error, LogLevelKind.Error, LogLevelKind.Error,
            LogLevelKind.Debug,
            LogLevelKind.Error,
        ], levels);
    }

    [Fact]
    public void UnityFormat_OnlyIndentedStackLinesInherit()
    {
        LogLevelKind[] levels = ClassifyAll(
            "Mono path[0] = 'D:/Games/Cities2_Data/Managed'",
            "NullReferenceException: Object reference not set to an instance of an object",
            "  at Game.Sample.SampleSystem.OnUpdate () [0x00000] in <hash>:0",
            "(Filename: <hash> Line: 0)",
            "",
            "[SceneFlow] [CRITICAL]  System update error: System.Exception: boom",
            "\tat Game.Sample ()",
            "[SampleMod] [WARN]  only a warning",
            "[SampleMod] [INFO]  info",
            "Memory Statistics:");

        Assert.Equal(
        [
            LogLevelKind.None,
            LogLevelKind.Error, LogLevelKind.Error,
            LogLevelKind.None, LogLevelKind.None,
            LogLevelKind.Error, LogLevelKind.Error,
            LogLevelKind.Warning,
            LogLevelKind.Info,
            LogLevelKind.None,
        ], levels);
    }
}
