using MapExTools.Core.Diagnostics;

namespace MapExTools.Tests.Diagnostics;

/// <summary>按游戏日志器的格式切记录：时间戳、可选的上下文、级别与多行消息。</summary>
public sealed class ColossalLogTests
{
    [Fact]
    public void Parse_HeadersContextLevelsAndContinuation()
    {
        string[] lines = LogSamples.Lines("""
            Unrelated first line
            [2026-11-09 23:49:30,061] [INFO]  Command line: D:\Games\Cities2.exe
            --developerMode
            [2026-11-09 23:49:58,852] {Some Climate (copy)} [WARN]  Duplicate prefab ID: X
            [2026-11-09 23:50:01,000] [ERROR]  Failed
            System.Exception: boom
              at A.B ()
            [2026-11-09 23:50:02,000] [CRITICAL]  Worse
            """);

        IReadOnlyList<LogEntry> entries = ColossalLog.Parse(lines);

        Assert.Equal(4, entries.Count);

        LogEntry command = entries[0];
        Assert.Equal(2, command.LineNumber);
        Assert.Equal("2026-11-09 23:49:30,061", command.Timestamp);
        Assert.Null(command.Context);
        Assert.Equal("INFO", command.Level);
        Assert.Equal(@"Command line: D:\Games\Cities2.exe", command.Message);
        Assert.Equal(["--developerMode"], command.Continuation);

        Assert.Equal("Some Climate (copy)", entries[1].Context);
        Assert.True(entries[1].IsWarning);
        Assert.False(entries[1].IsError);

        Assert.True(entries[2].IsError);
        Assert.Equal("Failed\nSystem.Exception: boom\n  at A.B ()", entries[2].FullText);
        Assert.True(entries[3].IsError);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsNoEntries()
    {
        Assert.Empty(ColossalLog.Parse([]));
        Assert.Empty(ColossalLog.Parse(["no header", ""]));
    }
}
