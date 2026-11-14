using MapExTools.Core.Install;

namespace MapExTools.Tests.Install;

/// <summary>按 exe 所在目录归类（计划 §2 第 9 条）。</summary>
public sealed class InstallLocationTests
{
    private static readonly InstallFolders s_folders = new(
        PinnedDir: @"C:\Users\Player\AppData\Local\Programs\MapExTools",
        GameModsDir: @"C:\Users\Player\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods",
        TempDir: @"C:\Users\Player\AppData\Local\Temp\",
        DownloadsDir: @"D:\Downloads");

    [Theory]
    [InlineData(@"D:\Tools\MapExTools\MapExTools.exe", InstallLocationKind.Normal)]
    [InlineData(@"C:\Users\Player\AppData\Local\Programs\MapExTools\MapExTools.exe", InstallLocationKind.Pinned)]
    [InlineData(@"C:\Users\Player\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\MapExTools.exe", InstallLocationKind.GameMods)]
    [InlineData(@"C:\Users\Player\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\Tools\x\MapExTools.exe", InstallLocationKind.GameMods)]
    [InlineData(@"C:\Users\Player\AppData\Local\Temp\Temp1_MapExTools.zip\MapExTools.exe", InstallLocationKind.Temp)]
    [InlineData(@"C:\Users\Player\AppData\Local\Temp\7zO1A2B3C4D\MapExTools.exe", InstallLocationKind.Temp)]
    [InlineData(@"D:\Downloads\MapExTools-v1.0.0-win-x64-portable\MapExTools.exe", InstallLocationKind.Downloads)]
    [InlineData(@"d:\downloads\MAPEXTOOLS.EXE", InstallLocationKind.Downloads)]
    public void Classify_ByFolder(string exePath, InstallLocationKind expected)
    {
        InstallLocation location = InstallLocation.Classify(exePath, s_folders);

        Assert.Equal(expected, location.Kind);
        Assert.Equal(expected is InstallLocationKind.Temp or InstallLocationKind.Downloads or InstallLocationKind.GameMods,
            location.OffersPinning);
    }

    [Fact]
    public void Classify_SimilarFolderNames_AreNotNested()
    {
        Assert.Equal(InstallLocationKind.Normal,
            InstallLocation.Classify(@"D:\Downloads2\MapExTools.exe", s_folders).Kind);
        Assert.Equal(InstallLocationKind.Normal,
            InstallLocation.Classify(@"C:\Users\Player\AppData\Local\Programs\MapExTools2\MapExTools.exe", s_folders).Kind);
        Assert.Equal(InstallLocationKind.Normal,
            InstallLocation.Classify(@"C:\Users\Player\AppData\LocalLow\Colossal Order\Cities Skylines II\ModsData\MapExt2\MapExTools.exe", s_folders).Kind);
    }

    [Fact]
    public void Classify_ModsInsideTemp_ModsWins()
    {
        // 用户目录被测试参数指到临时目录下时，仍按 Mods 处理
        var folders = s_folders with { GameModsDir = @"C:\Users\Player\AppData\Local\Temp\ud\Mods" };

        Assert.Equal(InstallLocationKind.GameMods,
            InstallLocation.Classify(@"C:\Users\Player\AppData\Local\Temp\ud\Mods\MapExTools.exe", folders).Kind);
    }

    [Fact]
    public void Classify_WithoutDownloadsFolder()
    {
        var folders = s_folders with { DownloadsDir = null };

        Assert.Equal(InstallLocationKind.Normal, InstallLocation.Classify(@"D:\Downloads\MapExTools.exe", folders).Kind);
    }

    [Theory]
    [InlineData(@"C:\a\b.exe", @"C:\a", true)]
    [InlineData(@"C:\a\b.exe", @"C:\a\", true)]
    [InlineData(@"C:\a\b.exe", @"C:\", true)]
    [InlineData(@"C:\ab\c.exe", @"C:\a", false)]
    [InlineData(@"C:\a", @"C:\a", false)]
    [InlineData(@"C:\a\b.exe", null, false)]
    [InlineData(@"C:\a\b.exe", "", false)]
    public void IsUnder(string path, string? dir, bool expected)
    {
        Assert.Equal(expected, InstallLocation.IsUnder(path, dir));
    }
}
