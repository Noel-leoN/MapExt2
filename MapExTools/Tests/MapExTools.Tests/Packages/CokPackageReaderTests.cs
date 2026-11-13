using System.Text;
using MapExTools.Core.Packages;

namespace MapExTools.Tests.Packages;

/// <summary>.cok 包的读取：元数据、guid 对照、预览图条目、损坏的包（计划 4.1、Phase 0.3）。</summary>
public sealed class CokPackageReaderTests : IDisposable
{
    private const string MapGuid = "aaaaaaaabbbbbbbbccccccccdddddddd";
    private const string EnLocaleGuid = "11111111111111111111111111111111";
    private const string HansLocaleGuid = "22222222222222222222222222222222";
    private const double TileArea = 14336.0 * 14336.0;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public CokPackageReaderTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string PathOf(string name) => Path.Combine(_root, name);

    [Fact]
    public void Read_Save_ReadsMetadataGuidAndPreview()
    {
        string path = CokBuilder.Save(["MapExt2", "MapEXT_CoreValue=4"]).Save(PathOf("Oakdale.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.None, info.Problem);
        Assert.Equal(PackageKind.Save, info.Kind);
        Assert.Equal("Oakdale", info.Name);
        Assert.Equal(CokTestData.SaveGuid, info.MetadataGuid);
        Assert.Equal("City.Texture", info.PreviewEntry);
        Assert.Equal(CokTestData.PreviewGuid, info.PreviewGuid);
        SaveMetadata save = info.Save!;
        Assert.Equal("Oakdale", save.CityName);
        Assert.Equal("AFLAT57km", save.MapName);
        Assert.Equal(12345, save.Population);
        Assert.Equal(987654, save.Money);
        Assert.False(save.AutoSave);
        Assert.Equal(new SaveModeTag(SaveTagKind.Tagged, 4), save.Mode);
    }

    [Fact]
    public void ReadEntryBytes_DecodesThePreview()
    {
        string path = CokBuilder.Save(null).Save(PathOf("Vanilla.cok"));
        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        TextureDecodeResult result = TextureAssetDecoder.Decode(CokPackageReader.ReadEntryBytes(path, info.PreviewEntry!));

        Assert.Equal(TextureDecodeStatus.Decoded, result.Status);
        Assert.True(result.Compressed);
        Assert.Equal(new SaveModeTag(SaveTagKind.Vanilla, -1), info.Save!.Mode);
    }

    [Fact]
    public void Read_OldPreviewFieldName_IsAccepted()
    {
        string json = CokTestData.SaveJson(["MapExt2"], previewKey: "previewAsset");
        string path = new CokBuilder()
            .Asset("Old.SaveGameMetadata", CokTestData.Utf8WithBom(json), CokTestData.SaveGuid)
            .Asset("Old.Texture", CokTestData.Texture(4, 4, 2, 8, CokTestData.RgbaPattern(4, 2)), CokTestData.PreviewGuid)
            .Save(PathOf("Old.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal("Old.Texture", info.PreviewEntry);
        Assert.Equal(new SaveModeTag(SaveTagKind.Legacy, 4), info.Save!.Mode);
    }

    [Fact]
    public void Read_PreviewGuidWithoutEntry_LeavesEntryNull()
    {
        string path = new CokBuilder()
            .Asset("City.SaveGameMetadata", CokTestData.Utf8WithBom(CokTestData.SaveJson(null)), CokTestData.SaveGuid)
            .Save(PathOf("NoTexture.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.None, info.Problem);
        Assert.Equal(CokTestData.PreviewGuid, info.PreviewGuid);
        Assert.Null(info.PreviewEntry);
    }

    [Fact]
    public void Read_CidLookupIgnoresCase_AndMissingCidLeavesGuidNull()
    {
        string path = new CokBuilder()
            .Asset("City.SaveGameMetadata", CokTestData.Utf8WithBom(CokTestData.SaveJson(null)), guid: null)
            .Asset("City.Texture", CokTestData.Texture(5, 4, 2, 8, CokTestData.RgbaPattern(4, 2)), guid: null)
            .Raw("CITY.TEXTURE.CID", Encoding.UTF8.GetBytes(CokTestData.PreviewGuid.ToUpperInvariant()))
            .Save(PathOf("Case.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Null(info.MetadataGuid);
        Assert.Equal("City.Texture", info.PreviewEntry);
    }

    [Fact]
    public void Read_Map_ReadsTitlesByLocaleAndInfersMode()
    {
        var titles = new Dictionary<string, string> { ["Maps.MAP_TITLE[Flat57]"] = "Flat Lands", ["Maps.MAP_DESCRIPTION[Flat57]"] = "-" };
        var hansTitles = new Dictionary<string, string> { ["Maps.MAP_TITLE[Flat57]"] = "平原" };
        string area = (16 * TileArea).ToString("E7", System.Globalization.CultureInfo.InvariantCulture);
        string path = new CokBuilder()
            .Asset("Flat57.MapMetadata", CokTestData.Utf8WithBom(CokTestData.MapJson("Flat57", area, [EnLocaleGuid, HansLocaleGuid])), MapGuid)
            .Asset("Flat57.MapData", [0], CokTestData.DataGuid)
            .Asset("Flat57_en-US.loc", CokTestData.Locale("en-US", titles), EnLocaleGuid)
            .Asset("Flat57_zh-HANS.loc", CokTestData.Locale("zh-HANS", hansTitles), HansLocaleGuid)
            .Save(PathOf("Flat57.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Maps);

        Assert.Equal(PackageProblem.None, info.Problem);
        Assert.Equal(PackageKind.Map, info.Kind);
        MapMetadata map = info.Map!;
        Assert.Equal("平原", map.GetTitle("zh-HANS"));
        Assert.Equal("Flat Lands", map.GetTitle("zh-HANT")); // 没有繁体时退回 en-US
        Assert.Equal("Flat Lands", map.GetTitle("en-US"));
        Assert.True(map.Mode.Trusted);
        Assert.Equal(4, map.Mode.CoreValue);
        Assert.Null(info.PreviewEntry);
        Assert.Null(info.PreviewGuid);
    }

    [Fact]
    public void Read_MapWithoutLocales_TitleFallsBackToDisplayName()
    {
        string path = new CokBuilder()
            .Asset("Plain.MapMetadata", CokTestData.Utf8WithBom(CokTestData.MapJson("Plain", "2.0552E+08")), MapGuid)
            .Save(PathOf("Plain.cok"));

        MapMetadata map = CokPackageReader.Read(path, PackageFolder.Maps).Map!;

        Assert.Equal("Plain", map.GetTitle("zh-HANS"));
        Assert.Equal(1, map.Mode.CoreValue);
    }

    [Fact]
    public void Read_EmptyZip_IsEmpty()
    {
        // 只有目录结尾记录的 22 字节空 ZIP（Phase 0.3 的两个自动存档）
        byte[] empty = [0x50, 0x4B, 0x05, 0x06, .. new byte[18]];
        string path = PathOf("28-6月-23-22-55.cok");
        File.WriteAllBytes(path, empty);

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.Empty, info.Problem);
        Assert.Equal(PackageKind.Unknown, info.Kind);
        Assert.Equal(22, info.Length);
    }

    [Fact]
    public void Read_NotAZip_IsCorrupt()
    {
        string path = PathOf("Broken.cok");
        File.WriteAllText(path, "not a zip file");

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.Corrupt, info.Problem);
        Assert.NotNull(info.ProblemDetail);
    }

    [Fact]
    public void Read_DamagedEntryHeader_IsCorruptWithoutThrowing()
    {
        // 中央目录完好、第一个条目（元数据）的本地文件头损坏：打开条目时才出错
        string path = CokBuilder.Save(null).Save(PathOf("BadHeader.cok"));
        byte[] bytes = File.ReadAllBytes(path);
        bytes[0] = 0;
        File.WriteAllBytes(path, bytes);

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.Corrupt, info.Problem);
    }

    [Fact]
    public void Read_MissingFile_IsUnreadable()
    {
        Assert.Equal(PackageProblem.Unreadable, CokPackageReader.Read(PathOf("Missing.cok"), PackageFolder.Saves).Problem);
    }

    [Fact]
    public void Read_NoMetadata_IsNoMetadata()
    {
        string path = new CokBuilder().Asset("City.SaveGameData", [1], CokTestData.DataGuid).Save(PathOf("DataOnly.cok"));

        Assert.Equal(PackageProblem.NoMetadata, CokPackageReader.Read(path, PackageFolder.Saves).Problem);
    }

    [Fact]
    public void Read_CompressedMetadata_IsIgnoredLikeTheGame()
    {
        // 游戏只收不压缩的条目，压缩过的元数据等于没有
        string path = new CokBuilder()
            .Asset("City.SaveGameMetadata", CokTestData.Utf8WithBom(CokTestData.SaveJson(null)), CokTestData.SaveGuid, stored: false)
            .Save(PathOf("Deflated.cok"));

        Assert.Equal(PackageProblem.NoMetadata, CokPackageReader.Read(path, PackageFolder.Saves).Problem);
    }

    [Fact]
    public void Read_InvalidJson_IsBadMetadata()
    {
        string path = new CokBuilder()
            .Asset("City.SaveGameMetadata", CokTestData.Utf8WithBom("{\"cityName\": "), CokTestData.SaveGuid)
            .Save(PathOf("BadJson.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.BadMetadata, info.Problem);
        Assert.Equal(PackageKind.Save, info.Kind);
        Assert.Equal(CokTestData.SaveGuid, info.MetadataGuid);
    }

    [Fact]
    public void Read_NonFiniteNumbers_AreTolerated()
    {
        string json = CokTestData.SaveJson(["MapExt2"], extra: ",\"weird\":-Infinity,\"note\":\"NaN stays in strings\"");
        string path = new CokBuilder()
            .Asset("City.SaveGameMetadata", CokTestData.Utf8WithBom(json), CokTestData.SaveGuid)
            .Save(PathOf("NaN.cok"));

        CokPackageInfo info = CokPackageReader.Read(path, PackageFolder.Saves);

        Assert.Equal(PackageProblem.None, info.Problem);
    }

    [Theory]
    [InlineData("{\"a\":NaN}", "{\"a\":null}")]
    [InlineData("[Infinity,-Infinity,+Infinity]", "[null,null,null]")]
    [InlineData("{\"s\":\"NaN \\\" Infinity\"}", "{\"s\":\"NaN \\\" Infinity\"}")]
    [InlineData("{\"a\":1.5E+08}", "{\"a\":1.5E+08}")]
    public void SanitizeNonFiniteNumbers(string input, string expected)
    {
        Assert.Equal(expected, CokPackageReader.SanitizeNonFiniteNumbers(input));
    }

    [Theory]
    [InlineData("0123456789ABCDEF0123456789abcdef", "0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcde", null)] // 31 位
    [InlineData("0123456789abcdef0123456789abcdef\n", null)] // 游戏不去换行
    [InlineData("0123456789abcdef0123456789abcdeg", null)]
    [InlineData("00000000000000000000000000000000", null)]
    [InlineData(null, null)]
    public void NormalizeGuid(string? input, string? expected)
    {
        Assert.Equal(expected, CokPackageReader.NormalizeGuid(input));
    }
}
