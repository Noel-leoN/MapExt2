using System.Text;
using MapExTools.Core.Diagnostics;

namespace MapExTools.Tests.Diagnostics;

/// <summary>增量读取正在写入的日志：续写、半行、跨次的多字节字符、重写与删除。</summary>
public sealed class LogTailTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public LogTailTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string LogPath => Path.Combine(_dir, "Sample.log");

    private void Append(string text) => Append(Encoding.UTF8.GetBytes(text));

    private void Append(byte[] bytes)
    {
        using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        stream.Write(bytes);
    }

    [Fact]
    public void MissingFile_ThenCreated()
    {
        var tail = new LogTail(LogPath);

        LogTailResult missing = tail.Poll();
        Assert.False(missing.Exists);
        Assert.False(missing.Reset);
        Assert.Empty(missing.Lines);

        Append("第一行\n");
        LogTailResult created = tail.Poll();
        Assert.True(created.Exists);
        Assert.False(created.Reset);
        Assert.Equal(["第一行"], created.Lines);
    }

    [Fact]
    public void Appends_ReturnOnlyNewCompleteLines()
    {
        Append("a\r\nb\n");
        var tail = new LogTail(LogPath);
        Assert.Equal(["a", "b"], tail.Poll().Lines);

        // 半行先留着，等换行写出来再交出
        Append("c");
        Assert.Empty(tail.Poll().Lines);
        Append("d\r\ne\n");
        LogTailResult result = tail.Poll();
        Assert.False(result.Reset);
        Assert.Equal(["cd", "e"], result.Lines);

        Assert.Empty(tail.Poll().Lines);
    }

    [Fact]
    public void MultiByteCharacterSplitAcrossPolls()
    {
        byte[] text = Encoding.UTF8.GetBytes("中文\n");
        var tail = new LogTail(LogPath);

        Append(text[..2]); // "中" 的前两个字节
        Assert.Empty(tail.Poll().Lines);
        Append(text[2..]);
        Assert.Equal(["中文"], tail.Poll().Lines);
    }

    [Fact]
    public void Bom_IsSkipped()
    {
        Append([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("first\n")]);
        Assert.Equal(["first"], new LogTail(LogPath).Poll().Lines);
    }

    [Fact]
    public void FileRewrittenShorter_Resets()
    {
        Append("[2026-11-12 20:00:00,000] [INFO]  old 1\n[2026-11-12 20:00:01,000] [INFO]  old 2\n");
        var tail = new LogTail(LogPath);
        tail.Poll();

        // 游戏启动时重写日志
        File.WriteAllText(LogPath, "[2026-11-12 21:00:00,000] [INFO]  new\n");
        LogTailResult result = tail.Poll();

        Assert.True(result.Reset);
        Assert.Equal(["[2026-11-12 21:00:00,000] [INFO]  new"], result.Lines);
    }

    [Fact]
    public void FileRewrittenLongerWithSameHeader_Resets()
    {
        // Player.log 每次运行的开头几行都一样：新文件比上次读到的位置还长时，要靠读到位置之前的字节分辨
        const string header = "Mono path[0] = 'D:/Games/Cities2_Data/Managed'\nInitialize engine version: 2022.3.71f1\n";
        Append(header + "run 1 line\n");
        var tail = new LogTail(LogPath);
        tail.Poll();

        File.WriteAllText(LogPath, header + "run 2 has a longer line here\nmore\n");
        LogTailResult result = tail.Poll();

        Assert.True(result.Reset);
        Assert.Equal(4, result.Lines.Count);
        Assert.Equal("run 2 has a longer line here", result.Lines[2]);
    }

    [Fact]
    public void FileDeleted_ResetsAndReportsMissing()
    {
        Append("line\n");
        var tail = new LogTail(LogPath);
        tail.Poll();

        File.Delete(LogPath);
        LogTailResult deleted = tail.Poll();
        Assert.False(deleted.Exists);
        Assert.True(deleted.Reset);

        Append("again\n");
        LogTailResult recreated = tail.Poll();
        Assert.True(recreated.Exists);
        Assert.Equal(["again"], recreated.Lines);
    }

    [Fact]
    public void ReadsWhileAnotherWriterHoldsTheFile()
    {
        using var writer = new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write(Encoding.UTF8.GetBytes("live 1\n"));
        writer.Flush();

        var tail = new LogTail(LogPath);
        Assert.Equal(["live 1"], tail.Poll().Lines);

        writer.Write(Encoding.UTF8.GetBytes("live 2\n"));
        writer.Flush();
        Assert.Equal(["live 2"], tail.Poll().Lines);
    }

    [Fact]
    public void InitialLargeFile_SkipsHeadAndAlignsToNextLine()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++)
            sb.Append($"old-line-{i:0000}-padding-padding-padding\n");
        sb.Append("tail-keep-1\n");
        sb.Append("tail-keep-2\n");
        Append(sb.ToString());

        // 使用小窗口（128 字节）验证首读仅取末尾并丢弃窗口边界处的半截行
        var tail = new LogTail(LogPath, maxInitialBytes: 128);
        LogTailResult first = tail.Poll();

        Assert.True(first.Exists);
        Assert.False(first.Reset);
        Assert.DoesNotContain("old-line-0000-padding-padding-padding", first.Lines);
        Assert.Equal("tail-keep-1", first.Lines[^2]);
        Assert.Equal("tail-keep-2", first.Lines[^1]);

        Append("tail-keep-3\n");
        LogTailResult next = tail.Poll();
        Assert.False(next.Reset);
        Assert.Equal(["tail-keep-3"], next.Lines);
    }
}
