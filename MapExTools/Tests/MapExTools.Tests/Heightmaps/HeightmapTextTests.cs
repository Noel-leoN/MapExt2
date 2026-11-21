using MapExTools.Core.Heightmaps;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;

namespace MapExTools.Tests.Heightmaps;

/// <summary>高度图页的文字（计划 4.2）。</summary>
public sealed class HeightmapTextTests
{
    private const string Hans = UiLanguage.SimplifiedChinese;
    private const string En = UiLanguage.English;

    [Fact]
    public void Kind_AndSize()
    {
        Assert.Equal("4096×4096 · 16 位灰度", HeightmapText.SizeAndKind(new ImageFileHeader(ImageContainer.Png, 4096, 4096, 1, 16, false), Hans));
        Assert.Equal("16 位 RGBA（游戏取 R 通道）", HeightmapText.Kind(new ImageFileHeader(ImageContainer.Tiff, 4096, 4096, 4, 16, false), Hans));
        Assert.Equal("16 位 · 3 通道", HeightmapText.Kind(new ImageFileHeader(ImageContainer.Png, 8192, 8192, 3, 16, false), Hans));
        Assert.Equal("32-bit floating point, 1 channel(s)", HeightmapText.Kind(new ImageFileHeader(ImageContainer.Tiff, 4096, 4096, 1, 32, true), En));
    }

    [Fact]
    public void Issues_BriefAndFull()
    {
        Assert.Equal("可导入", HeightmapText.Issues(HeightmapIssues.None, Hans, brief: true));
        Assert.Equal("可以导入游戏", HeightmapText.Issues(HeightmapIssues.None, Hans, brief: false));
        Assert.Equal("自动重采样", HeightmapText.Issues(HeightmapIssues.ResampledSize, Hans, brief: true));
        Assert.Contains("4096×4096", HeightmapText.Issues(HeightmapIssues.ResampledSize, Hans, brief: false), StringComparison.Ordinal);
        Assert.Equal("尺寸不符 · 格式不符", HeightmapText.Issues(HeightmapIssues.WrongSize | HeightmapIssues.WrongFormat, Hans, brief: true));
        Assert.Contains("；", HeightmapText.Issues(HeightmapIssues.WrongSize | HeightmapIssues.WrongFormat, Hans, brief: false), StringComparison.Ordinal);
        Assert.Contains("; ", HeightmapText.Issues(HeightmapIssues.WrongSize | HeightmapIssues.WrongFormat, En, brief: false), StringComparison.Ordinal);
        Assert.DoesNotContain("；", HeightmapText.Issues(HeightmapIssues.WrongSize | HeightmapIssues.WrongFormat, En, brief: false), StringComparison.Ordinal);
        Assert.Equal("认不出的图片文件", HeightmapText.Issues(null, Hans, brief: true));
        Assert.Equal(PackageTone.Good, HeightmapText.IssuesTone(HeightmapIssues.None));
        Assert.Equal(PackageTone.Accent, HeightmapText.IssuesTone(HeightmapIssues.ResampledSize));
        Assert.Equal(PackageTone.Error, HeightmapText.IssuesTone(HeightmapIssues.WrongSize));
        Assert.Equal(PackageTone.Error, HeightmapText.IssuesTone(HeightmapIssues.ResampledSize | HeightmapIssues.WrongFormat));
        Assert.Equal(PackageTone.Warning, HeightmapText.IssuesTone(null));
    }

    [Fact]
    public void Stats_RawAndElevation()
    {
        IReadOnlyList<string> lines = HeightmapText.Stats(new HeightmapStats(0, 65535, 32767.5, 2, 0, 65535), 4096, Hans);

        Assert.Equal(["原始值 0 – 65535", "海拔 0.0 – 4,096.0 米（平均 2,048.0 米）"], lines);
    }

    [Fact]
    public void Hover_WithAndWithoutSeaLevel()
    {
        Assert.Equal("x 10，y 20 · 原始值 65535 · 海拔 4,096.0 米", HeightmapText.Hover(10, 20, 65535, 4096, null, Hans));
        Assert.Equal("x 10，y 20 · 原始值 65535 · 海拔 4,096.0 米 · 高出海平面 4,056.0 米", HeightmapText.Hover(10, 20, 65535, 4096, 40, Hans));
        Assert.Equal("x 0, y 0 · raw 0 · elevation 0.0 m · 40.0 m below sea level", HeightmapText.Hover(0, 0, 0, 4096, 40, En));
    }

    [Fact]
    public void Legend_BandsReadLowToHigh()
    {
        Assert.Equal("50 – 60 米", HeightmapText.Legend(new LegendEntry(LegendKind.Water, 0, 60, 50), Hans));
        Assert.Equal("海岸线（海平面 40.0 米）", HeightmapText.Legend(new LegendEntry(LegendKind.Coast, 0, 40, 40), Hans));
        Assert.Equal("White: 4,096 m", HeightmapText.Legend(new LegendEntry(LegendKind.White, 0, 4096, 4096), En));
        Assert.Equal("黑：12 米及以下", HeightmapText.Legend(new LegendEntry(LegendKind.BlackAtMost, 0, 12, 12), Hans));
        Assert.Equal("白：640 米及以上", HeightmapText.Legend(new LegendEntry(LegendKind.WhiteAtLeast, 0, 640, 640), Hans));
    }

    [Fact]
    public void Status_CountsImportable()
    {
        var files = new[]
        {
            new HeightmapFileInfo("a", "a", 0, default, new ImageFileHeader(ImageContainer.Png, 4096, 4096, 1, 16, false), null),
            new HeightmapFileInfo("b", "b", 0, default, new ImageFileHeader(ImageContainer.Png, 8192, 8192, 1, 16, false), null),
            new HeightmapFileInfo("c", "c", 0, default, new ImageFileHeader(ImageContainer.Png, 16384, 16384, 1, 16, false), null),
            new HeightmapFileInfo("d", "d", 0, default, null, null),
        };

        Assert.Equal("共 4 张图：可导入 2、不可导入 2", HeightmapText.Status(files, Hans));
    }
}
