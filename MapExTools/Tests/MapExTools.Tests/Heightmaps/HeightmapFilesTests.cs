using MapExTools.Core.Heightmaps;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Heightmaps;

/// <summary>列出 Heightmaps 目录：子目录也算、扩展名与跳过规则同游戏、读不出文件头不中断（计划 4.2）。</summary>
public sealed class HeightmapFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative, byte[] data)
    {
        string path = Path.Combine(_root, "Heightmaps", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    [Fact]
    public void Scan_ListsImagesLikeTheEditor()
    {
        Write("a.png", HeightmapTestData.Png(4096, 4096, 16, 0));
        Write(Path.Combine("Sub", "b.TIF"), HeightmapTestData.Tiff(true, 8192, 8192, 16, 1));
        Write("c.jpg", HeightmapTestData.Jpeg(4096, 4096, 3));
        Write("d.raw", new byte[32]);
        Write("a.png.cid", "0123456789abcdef0123456789abcdef"u8.ToArray());
        Write("~e.png", HeightmapTestData.Png(4096, 4096, 16, 0));
        Write(Path.Combine(".hidden", "f.png"), HeightmapTestData.Png(4096, 4096, 16, 0));
        Write("broken.png", "not an image"u8.ToArray());

        IReadOnlyList<HeightmapFileInfo> files = HeightmapFiles.Scan(new UserDataPaths(_root));

        Assert.Equal(["a.png", "broken.png", "c.jpg", Path.Combine("Sub", "b.TIF")], files.Select(f => f.RelativeName));
        Assert.Equal(HeightmapIssues.None, files[0].Issues);
        Assert.Null(files[1].Header);
        Assert.Null(files[1].Issues);
        Assert.Equal(HeightmapIssues.WrongFormat, files[2].Issues);
        Assert.Equal(HeightmapIssues.ResampledSize, files[3].Issues);
    }

    [Fact]
    public void Scan_MissingFolder_IsEmpty()
    {
        Assert.Empty(HeightmapFiles.Scan(new UserDataPaths(_root)));
    }

    [Fact]
    public void Describe_FileOutsideTheFolder_UsesFileName()
    {
        Directory.CreateDirectory(_root);
        string path = Path.Combine(_root, "outside.png");
        File.WriteAllBytes(path, HeightmapTestData.Png(2048, 2048, 16, 0));

        HeightmapFileInfo info = HeightmapFiles.Describe(path, Path.Combine(_root, "Heightmaps"));

        Assert.Equal("outside.png", info.RelativeName);
        Assert.Equal(HeightmapIssues.ResampledSize, info.Issues);
    }

    [Fact]
    public void Describe_MissingFile_RecordsTheError()
    {
        HeightmapFileInfo info = HeightmapFiles.Describe(Path.Combine(_root, "missing.png"));

        Assert.NotNull(info.ReadError);
        Assert.Null(info.Header);
    }
}
