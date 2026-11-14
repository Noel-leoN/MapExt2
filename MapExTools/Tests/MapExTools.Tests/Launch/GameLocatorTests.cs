using MapExTools.Core.Launch;

namespace MapExTools.Tests.Launch;

/// <summary>在临时目录里按 Steam 的布局搭出 Steam、库、游戏清单与游戏目录。</summary>
public sealed class GameLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public GameLocatorTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string SteamDir => Path.Combine(_root, "Steam");

    private string SkyveFile => Path.Combine(_root, "UserData", "ModsSettings", "Skyve", "FolderSettings.json");

    private void CreateSteam(params string[] libraries)
    {
        Touch(Path.Combine(SteamDir, "steam.exe"));
        if (libraries.Length == 0)
            return;

        var body = new System.Text.StringBuilder("\"libraryfolders\"\n{\n");
        for (int i = 0; i < libraries.Length; i++)
            body.Append($"\t\"{i}\"\n\t{{\n\t\t\"path\"\t\t\"{libraries[i].Replace(@"\", @"\\")}\"\n\t}}\n");
        body.Append("}\n");
        Write(Path.Combine(SteamDir, "steamapps", "libraryfolders.vdf"), body.ToString());
    }

    /// <summary>在某个库里装上游戏：清单、exe，<paramref name="steamBuild"/> 时再放 steam_api64.dll。</summary>
    private string InstallGame(string library, string installDir = "Cities Skylines II", bool steamBuild = true)
    {
        Write(Path.Combine(library, "steamapps", "appmanifest_949230.acf"),
            $"\"AppState\"\n{{\n\t\"appid\"\t\t\"949230\"\n\t\"installdir\"\t\t\"{installDir}\"\n}}\n");
        string gameDir = Path.Combine(library, "steamapps", "common", installDir);
        return CreateGameDir(gameDir, steamBuild);
    }

    private static string CreateGameDir(string gameDir, bool steamBuild)
    {
        string exe = Path.Combine(gameDir, "Cities2.exe");
        Touch(exe);
        if (steamBuild)
            Touch(Path.Combine(gameDir, "Cities2_Data", "Plugins", "x86_64", "steam_api64.dll"));
        return exe;
    }

    private GameLocateResult Locate(string? manual = null, params string[] steamCandidates) =>
        GameLocator.Locate(new GameLocatorInput(manual, steamCandidates, SkyveFile));

    private static void Touch(string path) => Write(path, string.Empty);

    private static void Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void FindsGameInSecondLibrary()
    {
        string library = Path.Combine(_root, "Lib2");
        CreateSteam(SteamDir, library);
        string exe = InstallGame(library);

        GameLocateResult result = Locate(null, SteamDir);

        Assert.Equal(exe, result.Game?.ExePath);
        Assert.Equal(GameLocationSource.Steam, result.Game?.Source);
        Assert.True(result.Game?.IsSteamBuild);
        Assert.Equal(Path.GetDirectoryName(exe), result.Game?.GameDir);
        Assert.Equal(Path.Combine(SteamDir, "steam.exe"), result.Steam?.Exe);
        Assert.Equal(SteamDir, result.Steam?.Dir);
        Assert.True(result.SteamChannelsAvailable);
        Assert.Null(result.MissingManualPath);
    }

    [Fact]
    public void WithoutLibraryFile_ChecksSteamDirItself()
    {
        CreateSteam();
        string exe = InstallGame(SteamDir);

        Assert.Equal(exe, Locate(null, SteamDir).Game?.ExePath);
    }

    [Fact]
    public void MalformedLibraryFile_StillChecksSteamDir()
    {
        CreateSteam();
        Write(Path.Combine(SteamDir, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" {");
        string exe = InstallGame(SteamDir);

        Assert.Equal(exe, Locate(null, SteamDir).Game?.ExePath);
    }

    [Fact]
    public void RegistryStylePath_IsNormalizedToActualCase()
    {
        // 注册表里的 SteamPath 是全小写加正斜杠
        CreateSteam();
        string exe = InstallGame(SteamDir);
        string registryStyle = SteamDir.ToLowerInvariant().Replace('\\', '/');

        GameLocateResult result = Locate(null, registryStyle);

        Assert.Equal(SteamDir, result.Steam?.Dir);
        Assert.Equal(exe, result.Game?.ExePath);
    }

    [Fact]
    public void SkipsCandidateWithoutSteamExe()
    {
        CreateSteam();
        string exe = InstallGame(SteamDir);

        GameLocateResult result = Locate(null, Path.Combine(_root, "NoSteamHere"), "", SteamDir);

        Assert.Equal(SteamDir, result.Steam?.Dir);
        Assert.Equal(exe, result.Game?.ExePath);
    }

    [Fact]
    public void ManifestPointingToMissingExe_TriesNextLibrary()
    {
        string library = Path.Combine(_root, "Lib2");
        CreateSteam(SteamDir, library);
        Write(Path.Combine(SteamDir, "steamapps", "appmanifest_949230.acf"), "\"AppState\" { \"installdir\" \"Gone\" }");
        string exe = InstallGame(library);

        Assert.Equal(exe, Locate(null, SteamDir).Game?.ExePath);
    }

    [Fact]
    public void ManualPath_TakesPriority()
    {
        CreateSteam();
        InstallGame(SteamDir);
        string manual = CreateGameDir(Path.Combine(_root, "Elsewhere"), steamBuild: false);

        GameLocateResult result = Locate(manual, SteamDir);

        Assert.Equal(manual, result.Game?.ExePath);
        Assert.Equal(GameLocationSource.Manual, result.Game?.Source);
        Assert.False(result.Game?.IsSteamBuild);
        Assert.NotNull(result.Steam);
        Assert.False(result.SteamChannelsAvailable);
    }

    [Theory]
    [InlineData("Missing/Cities2.exe")]
    [InlineData("Other/Game.exe")]
    public void ManualPath_MissingOrWrongName_FallsBackAndIsReported(string relative)
    {
        CreateSteam();
        string exe = InstallGame(SteamDir);
        string manual = Path.Combine(_root, relative);
        if (relative.StartsWith("Other", StringComparison.Ordinal))
            Touch(manual);

        GameLocateResult result = Locate(manual, SteamDir);

        Assert.Equal(exe, result.Game?.ExePath);
        Assert.Equal(manual, result.MissingManualPath);
    }

    [Fact]
    public void WithoutSteam_UsesSkyveRecord()
    {
        string gameDir = Path.Combine(_root, "XboxGames", "Cities Skylines II", "Content");
        string exe = CreateGameDir(gameDir, steamBuild: false);
        Write(SkyveFile, $$"""{"GamePath":"{{gameDir.Replace(@"\", @"\\")}}","UserIdType":"xbox_xsts"}""");

        GameLocateResult result = Locate(null, Path.Combine(_root, "NoSteam"));

        Assert.Equal(exe, result.Game?.ExePath);
        Assert.Equal(GameLocationSource.Skyve, result.Game?.Source);
        Assert.False(result.Game?.IsSteamBuild);
        Assert.Null(result.Steam);
        Assert.False(result.SteamChannelsAvailable);
    }

    [Fact]
    public void SkyveRecordWithMissingGame_IsIgnored()
    {
        Write(SkyveFile, """{"GamePath":"Z:\\Nowhere"}""");
        Assert.Null(Locate().Game);
    }

    [Fact]
    public void NothingFound()
    {
        GameLocateResult result = Locate(null, Path.Combine(_root, "NoSteam"));
        Assert.Null(result.Game);
        Assert.Null(result.Steam);
        Assert.False(result.SteamChannelsAvailable);
    }

    [Fact]
    public void IsGameExe_ChecksNameAndExistence()
    {
        string exe = CreateGameDir(Path.Combine(_root, "G"), steamBuild: false);
        Assert.True(GameLocator.IsGameExe(exe));
        Assert.True(GameLocator.IsGameExe(exe.ToUpperInvariant()));
        Assert.False(GameLocator.IsGameExe(Path.Combine(_root, "G", "Missing", "Cities2.exe")));
        Touch(Path.Combine(_root, "G", "Other.exe"));
        Assert.False(GameLocator.IsGameExe(Path.Combine(_root, "G", "Other.exe")));
    }

    [Fact]
    public void GetActualCase_RestoresCasing_AndKeepsMissingPartsAsGiven()
    {
        string dir = Path.Combine(_root, "MixedCase", "Sub Dir");
        Directory.CreateDirectory(dir);

        Assert.Equal(dir, GameLocator.GetActualCase(dir.ToLowerInvariant()));

        string missing = Path.Combine(dir.ToLowerInvariant(), "nothing-here");
        Assert.Equal(Path.GetFullPath(missing), GameLocator.GetActualCase(missing));
    }
}
