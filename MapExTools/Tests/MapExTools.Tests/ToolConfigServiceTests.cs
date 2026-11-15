using System.Text;
using MapExTools.Core.Config;
using MapExTools.Core.Launch;
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
        Assert.False(result.Config.LaunchOptionsExpanded);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AndCreatesDirectory()
    {
        ToolConfigService service = CreateService();
        service.Save(new ToolConfig
        {
            Language = "zh-HANT",
            Theme = ThemePreference.Dark,
            GameExePath = @"D:\Games\Cities2.exe",
            LaunchOptionsExpanded = true,
        });

        ToolConfigLoadResult result = service.Load();
        Assert.Equal(ToolConfigLoadStatus.Loaded, result.Status);
        Assert.Equal("zh-HANT", result.Config.Language);
        Assert.Equal(ThemePreference.Dark, result.Config.Theme);
        Assert.Equal(@"D:\Games\Cities2.exe", result.Config.GameExePath);
        Assert.True(result.Config.LaunchOptionsExpanded);

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
    public void LaunchOptions_RoundTrip()
    {
        ToolConfigService service = CreateService();
        var config = new ToolConfig();
        config.Launch.Channel = LaunchChannel.RunOnce;
        config.Launch.DeveloperMode = true;
        config.Launch.LogLevel = "DEBUG";
        config.Launch.CustomArguments = "--x \"a b\"";
        service.Save(config);

        string json = File.ReadAllText(service.ConfigPath);
        Assert.Contains("\"launch\": {", json);
        Assert.Contains("\"channel\": \"RunOnce\"", json);

        LaunchOptions loaded = service.Load().Config.Launch;
        Assert.Equal(LaunchChannel.RunOnce, loaded.Channel);
        Assert.True(loaded.DeveloperMode);
        Assert.False(loaded.NoSplash);
        Assert.Equal("DEBUG", loaded.LogLevel);
        Assert.Equal("--x \"a b\"", loaded.CustomArguments);
    }

    [Theory]
    [InlineData("""{ "language": "en-US" }""")]
    [InlineData("""{ "launch": null }""")]
    public void Load_MissingOrNullLaunch_UsesDefaults(string json)
    {
        ToolConfigService service = CreateService();
        Directory.CreateDirectory(Path.GetDirectoryName(service.ConfigPath)!);
        File.WriteAllText(service.ConfigPath, json, Encoding.UTF8);

        ToolConfigLoadResult result = service.Load();
        Assert.Equal(ToolConfigLoadStatus.Loaded, result.Status);
        Assert.Equal(LaunchChannel.Direct, result.Config.Launch.Channel);
    }

    [Fact]
    public void Save_PreservesUnknownLaunchFields()
    {
        ToolConfigService service = CreateService();
        Directory.CreateDirectory(Path.GetDirectoryName(service.ConfigPath)!);
        File.WriteAllText(service.ConfigPath, """{ "launch": { "channel": "Steam", "futureOption": 3 } }""", Encoding.UTF8);

        ToolConfigLoadResult loaded = service.Load();
        Assert.Equal(LaunchChannel.Steam, loaded.Config.Launch.Channel);
        loaded.Config.Launch.NoSplash = true;
        service.Save(loaded.Config);

        string json = File.ReadAllText(service.ConfigPath);
        Assert.Contains("\"futureOption\": 3", json);
        Assert.Contains("\"noSplash\": true", json);
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
