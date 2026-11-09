using System.Text;
using MapExTools.Core.Config;
using MapExTools.Core.Paths;

namespace MapExTools.Tests;

public sealed class ToolConfigServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private ToolConfigService CreateService() => new(new UserDataPaths(_root).ToolConfigFile);

    [Fact]
    public void Paths_ConfigLivesUnderModsDataMapExt2()
    {
        var paths = new UserDataPaths(_root);
        Assert.Equal(Path.Combine(_root, "ModsData", "MapExt2", "MapExTools.json"), paths.ToolConfigFile);
        Assert.Equal(Path.Combine(_root, "ModsSettings", "MapExtPDX", "MapExtPDX.coc"), paths.CocFile);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        ToolConfigLoadResult result = CreateService().Load();
        Assert.Equal(ToolConfigLoadStatus.Missing, result.Status);
        Assert.Null(result.Config.Language);
        Assert.Equal(ThemePreference.System, result.Config.Theme);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AndCreatesDirectory()
    {
        ToolConfigService service = CreateService();
        service.Save(new ToolConfig { Language = "zh-HANT", Theme = ThemePreference.Dark, GameExePath = @"D:\Games\Cities2.exe" });

        ToolConfigLoadResult result = service.Load();
        Assert.Equal(ToolConfigLoadStatus.Loaded, result.Status);
        Assert.Equal("zh-HANT", result.Config.Language);
        Assert.Equal(ThemePreference.Dark, result.Config.Theme);
        Assert.Equal(@"D:\Games\Cities2.exe", result.Config.GameExePath);

        string json = File.ReadAllText(service.ConfigPath);
        Assert.Contains("\"theme\": \"Dark\"", json);

        // BOM 要看原始字节：ReadAllText 会去掉 BOM，而按区域比较字符串时 U+FEFF 会被忽略，在任何位置都算"找到"
        byte[] bytes = File.ReadAllBytes(service.ConfigPath);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "MapExTools.json 不应带 BOM");
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(service.ConfigPath)!, "*.tmp"));
    }

    [Fact]
    public void Save_PreservesUnknownFieldsFromNewerVersion()
    {
        ToolConfigService service = CreateService();
        Directory.CreateDirectory(Path.GetDirectoryName(service.ConfigPath)!);
        File.WriteAllText(service.ConfigPath, """{ "schemaVersion": 2, "language": "en-US", "futureFeature": { "x": 1 } }""", Encoding.UTF8);

        ToolConfigLoadResult loaded = service.Load();
        loaded.Config.Theme = ThemePreference.Light;
        service.Save(loaded.Config);

        string json = File.ReadAllText(service.ConfigPath);
        Assert.Contains("futureFeature", json);
        Assert.Contains("\"theme\": \"Light\"", json);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndKeepsCopy()
    {
        ToolConfigService service = CreateService();
        Directory.CreateDirectory(Path.GetDirectoryName(service.ConfigPath)!);
        File.WriteAllText(service.ConfigPath, "{ not json", Encoding.UTF8);

        ToolConfigLoadResult result = service.Load();
        Assert.Equal(ToolConfigLoadStatus.Corrupt, result.Status);
        Assert.Equal(service.CorruptBackupPath, result.BackupPath);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Equal("{ not json", File.ReadAllText(service.CorruptBackupPath));
        Assert.Equal(ThemePreference.System, result.Config.Theme);
    }

    [Fact]
    public void Load_UnknownEnumValue_IsTreatedAsCorrupt()
    {
        ToolConfigService service = CreateService();
        Directory.CreateDirectory(Path.GetDirectoryName(service.ConfigPath)!);
        File.WriteAllText(service.ConfigPath, """{ "theme": "Purple" }""", Encoding.UTF8);

        Assert.Equal(ToolConfigLoadStatus.Corrupt, service.Load().Status);
    }
}
