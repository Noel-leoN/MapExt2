using System.Text;
using MapExTools.Core.Launch;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Launch;

public sealed class RunOnceFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string FilePath => new UserDataPaths(_root).RunOnceFile;

    [Fact]
    public void Path_IsUserDataRoot()
    {
        Assert.Equal(Path.Combine(_root, "runOnce.txt"), FilePath);
        Assert.Equal(Path.Combine(_root, "ModsSettings", "Skyve", "FolderSettings.json"), new UserDataPaths(_root).SkyveFolderSettings);
    }

    [Fact]
    public void ReadPending_MissingOrBlank_IsNull()
    {
        Assert.Null(RunOnceFile.ReadPending(FilePath));

        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, " \r\n\t");
        Assert.Null(RunOnceFile.ReadPending(FilePath));
    }

    [Fact]
    public void ReadPending_CollapsesWhitespace()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(FilePath, "--noSplash  --continuelastsave\r\n");
        Assert.Equal("--noSplash --continuelastsave", RunOnceFile.ReadPending(FilePath));
    }

    [Fact]
    public void Write_CreatesFolder_NoBom_NoNewline_Overwrites()
    {
        RunOnceFile.Write(FilePath, "--a --b");
        RunOnceFile.Write(FilePath, "--c");

        byte[] bytes = File.ReadAllBytes(FilePath);
        Assert.Equal("--c", Encoding.UTF8.GetString(bytes));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public void Write_Empty_DeletesFile()
    {
        RunOnceFile.Write(FilePath, "--a");
        RunOnceFile.Write(FilePath, string.Empty);
        Assert.False(File.Exists(FilePath));

        // 不存在时删除也不报错
        RunOnceFile.Delete(FilePath);
    }
}
