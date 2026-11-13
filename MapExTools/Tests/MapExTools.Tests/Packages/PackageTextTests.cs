using MapExTools.Core.Localization;
using MapExTools.Core.Packages;
using MapExtPDX;

namespace MapExTools.Tests.Packages;

/// <summary>存档与地图页的文字：模式标签、关系说明、详情行与预览图一行（计划 4.1）。</summary>
public sealed class PackageTextTests
{
    private const string Hans = UiLanguage.SimplifiedChinese;
    private const string En = UiLanguage.English;
    private const double TileArea = 14336.0 * 14336.0;

    private static readonly GateSettings s_modeA = new("ModeA", 4, ConversionEnabled: false, ValidationDisabled: false);

    private static CokPackageInfo SavePackage(string?[]? mods, string? previewEntry = "City.Texture") =>
        new(@"C:\UserData\Saves\id\Oakdale.cok", "Oakdale", PackageFolder.Saves, 3 * 1024 * 1024, new DateTime(2026, 11, 9, 22, 11, 5),
            PackageKind.Save, PackageProblem.None, null, CokTestData.SaveGuid,
            new SaveMetadata("Oakdale", "AFLAT57km", 12345, 987654, AutoSave: true, mods, CokTestData.PreviewGuid), null, previewEntry);

    private static CokPackageInfo MapPackage(double ratio) =>
        new(@"C:\UserData\Maps\id\Flat.cok", "Flat", PackageFolder.Maps, 1024, default, PackageKind.Map, PackageProblem.None, null, null, null,
            new MapMetadata("Flat", new Dictionary<string, string>(), (float)(ratio * TileArea), "European", null), null);

    [Theory]
    [InlineData(4, En, "ModeA 57km")]
    [InlineData(8, En, "ModeC 114km")]
    [InlineData(3, En, "ModeD 43km")]
    [InlineData(1, Hans, "None 14km（原版）")]
    [InlineData(16, Hans, "CV=16（不是现行模式）")]
    public void CoreValueLabel(int coreValue, string language, string expected)
    {
        Assert.Equal(expected, PackageText.CoreValueLabel(coreValue, language));
    }

    [Fact]
    public void Header_ShowsModeAndConversion()
    {
        Assert.Equal("设定的模式：ModeB 28km · 地图扩展转换：开", PackageText.Header(new GateSettings("ModeB", 2, true, false), Hans));
    }

    [Theory]
    [InlineData(null, true, "原版")]
    [InlineData(new[] { "MapExt2" }, true, "ModeA 57km（旧版）")]
    [InlineData(new[] { "MapExt2", "MapEXT_CoreValue=8" }, true, "ModeC 114km")]
    [InlineData(new[] { "MapExt2", "MapEXT_CoreValue=8" }, false, "ModeC 114km（标签 CV=8）")]
    [InlineData(new[] { "MapExt2", "MapEXT_CoreValue=x" }, true, "标签无效")]
    public void SaveMode(string[]? mods, bool brief, string expected)
    {
        Assert.Equal(expected, PackageText.SaveMode(SaveGateRules.ClassifySave(mods), Hans, brief));
    }

    [Fact]
    public void MapMode_TrustedAndUntrusted()
    {
        Assert.Equal("ModeA 57km（按面积推断，比值 16）", PackageText.MapMode(SaveGateRules.InferMap((float)(16 * TileArea)), Hans, brief: false));
        Assert.Equal("未知：面积比值 256 对不上任何现行模式", PackageText.MapMode(SaveGateRules.InferMap((float)(256 * TileArea)), Hans, brief: false));
        Assert.Equal("未知：元数据里没有面积", PackageText.MapMode(SaveGateRules.InferMap(null), Hans, brief: false));
        Assert.Equal("模式未知", PackageText.MapMode(SaveGateRules.InferMap(null), Hans, brief: true));
        Assert.Equal("面积比值 256", PackageText.MapMode(SaveGateRules.InferMap((float)(256 * TileArea)), Hans, brief: true));
        Assert.Equal("ModeB 28km", PackageText.MapMode(SaveGateRules.InferMap((float)(4 * TileArea)), Hans, brief: true));
    }

    [Fact]
    public void RelationHint_StretchedMentionsConversionOnlyWhereItApplies()
    {
        CokPackageInfo vanillaMap = MapPackage(1);
        string extended = PackageText.RelationHint(vanillaMap, PackageRelation.Stretched, s_modeA, Hans);
        string modeC = PackageText.RelationHint(vanillaMap, PackageRelation.Stretched, new GateSettings("ModeC", 8, false, false), Hans);

        Assert.Contains("开启“地图扩展转换”后可改为转换", extended, StringComparison.Ordinal);
        Assert.DoesNotContain("改为转换", modeC, StringComparison.Ordinal);
    }

    [Fact]
    public void RelationLabelAndTone()
    {
        Assert.Equal("开启转换后可转换", PackageText.RelationLabel(PackageRelation.ConvertibleWhenEnabled, Hans));
        Assert.Equal("损坏", PackageText.RelationLabel(null, Hans));
        Assert.Equal(PackageTone.Good, PackageText.RelationTone(PackageRelation.Match));
        Assert.Equal(PackageTone.Error, PackageText.RelationTone(PackageRelation.Mismatch));
        Assert.Equal(PackageTone.Error, PackageText.RelationTone(null));
    }

    [Fact]
    public void Details_Save()
    {
        CokPackageInfo package = SavePackage(null) with { DuplicatePaths = [@"C:\UserData\Saves\id\Oakdale (2).cok"] };

        IReadOnlyList<DetailLine> lines = PackageText.Details(package, s_modeA, Hans, @"C:\UserData");

        Assert.Equal(["城市", "人口", "资金", "地图", "MapExt 模式", "与设定模式", "自动存档", "修改时间", "大小", "文件", "元数据 guid", "guid 相同的包"],
            lines.Select(l => l.Label));
        DetailLine relation = lines.Single(l => l.Label == "与设定模式");
        Assert.StartsWith("开启转换后可转换 · ", relation.Value, StringComparison.Ordinal);
        Assert.Equal(PackageTone.Warning, relation.Tone);
        Assert.Equal("12,345", lines.Single(l => l.Label == "人口").Value);
        Assert.Equal("3.0 MB", lines.Single(l => l.Label == "大小").Value);
        Assert.Equal("2026-11-09 22:11:05", lines.Single(l => l.Label == "修改时间").Value);
        Assert.Equal(Path.Combine("Saves", "id", "Oakdale.cok"), lines.Single(l => l.Label == "文件").Value);
        Assert.Equal(Path.Combine("Saves", "id", "Oakdale (2).cok"), lines.Single(l => l.Label == "guid 相同的包").Value);
    }

    [Fact]
    public void Details_DamagedPackage_ShowsProblem()
    {
        var package = new CokPackageInfo(@"C:\UserData\Saves\id\Empty.cok", "Empty", PackageFolder.Saves, 22, default, PackageKind.Unknown,
            PackageProblem.Empty, null, null, null, null, null);

        IReadOnlyList<DetailLine> lines = PackageText.Details(package, s_modeA, En, @"C:\UserData");

        Assert.Equal(new DetailLine("Problem", "Empty package: it contains nothing", PackageTone.Error), lines[0]);
        Assert.DoesNotContain(lines, l => l.Label == "With the mode in settings");
    }

    [Fact]
    public void Details_MapWithoutSettings_IsUnknown()
    {
        IReadOnlyList<DetailLine> lines = PackageText.Details(MapPackage(16), settings: null, Hans, @"C:\UserData");

        Assert.Equal("未知 · 读不出设定的模式。", lines.Single(l => l.Label == "与设定模式").Value);
        Assert.Equal("3,288.3 km²", lines.Single(l => l.Label == "面积").Value);
    }

    [Fact]
    public void Preview_States()
    {
        CokPackageInfo package = SavePackage(null);
        var header = new TextureHeader(5, 680, 383, 1, 1, 8, null, 2, 14);

        Assert.Equal("读取中…", PackageText.Preview(package, null, null, Hans).Value);
        Assert.Equal("680×383，版本 5，ZSTD 压缩",
            PackageText.Preview(package, new TextureDecodeResult(TextureDecodeStatus.Decoded, header, true, null, null), null, Hans).Value);
        Assert.Equal("680×383，像素格式 109，不支持显示",
            PackageText.Preview(package, new TextureDecodeResult(TextureDecodeStatus.UnsupportedFormat, header with { Format = 109 }, true, null, null), null, Hans).Value);
        Assert.Equal("无法读取：locked", PackageText.Preview(package, null, "locked", Hans).Value);
        Assert.Equal("缺失：包里没有 guid 为 " + CokTestData.PreviewGuid + " 的条目", PackageText.Preview(SavePackage(null, previewEntry: null), null, null, Hans).Value);
    }

    [Theory]
    [InlineData(22, "22 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5 * 1024 * 1024 + 100 * 1024, "5.1 MB")]
    [InlineData(3L * 1024 * 1024 * 1024, "3.0 GB")]
    public void FormatSize(long bytes, string expected)
    {
        Assert.Equal(expected, PackageText.FormatSize(bytes));
    }

    [Fact]
    public void RelativeTo_OutsideRoot_KeepsFullPath()
    {
        Assert.Equal(@"D:\Elsewhere\a.cok", PackageText.RelativeTo(@"C:\UserData", @"D:\Elsewhere\a.cok"));
    }

    [Fact]
    public void ModeLabel_AllModesHaveTheirSize()
    {
        Assert.All(StartupKeys.Modes, m => Assert.Contains(m.MapSize / 1000 + "km", PackageText.ModeLabel(m, En), StringComparison.Ordinal));
    }
}
