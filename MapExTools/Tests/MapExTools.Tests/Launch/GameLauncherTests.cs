using System.Diagnostics;
using System.Text;
using MapExTools.Core.Launch;

namespace MapExTools.Tests.Launch;

public sealed class GameLauncherTests : IDisposable
{
    private const string GameExe = @"D:\Games\Steam\steamapps\common\Cities Skylines II\Cities2.exe";
    private const string SteamExe = @"D:\Games\Steam\steam.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<ProcessStartInfo> _started = [];
    private bool _gameRunning;
    private bool _steamRunning = true;

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string RunOncePath => Path.Combine(_root, "runOnce.txt");

    private GameLauncher CreateLauncher() => new(RunOncePath, () => _gameRunning, () => _steamRunning, _started.Add);

    private static GameLocateResult SteamGame(bool steamBuild = true, bool withSteam = true) => new(
        new GameLocation(GameExe, GameLocationSource.Steam, steamBuild),
        withSteam ? new SteamInfo(@"D:\Games\Steam", SteamExe) : null,
        null);

    // === 检查 ===

    [Fact]
    public void Check_GameRunning_ComesFirst()
    {
        _gameRunning = true;
        Assert.Equal(LaunchBlock.GameRunning, CreateLauncher().Check(GameLocateResult.None, LaunchChannel.Direct));
    }

    [Theory]
    [InlineData(LaunchChannel.Direct)]
    [InlineData(LaunchChannel.Steam)]
    [InlineData(LaunchChannel.RunOnce)]
    public void Check_NoGame(LaunchChannel channel)
    {
        Assert.Equal(LaunchBlock.GameNotFound, CreateLauncher().Check(GameLocateResult.None, channel));
    }

    [Fact]
    public void Check_DirectSteamBuild_NeedsSteamRunning()
    {
        GameLauncher launcher = CreateLauncher();
        Assert.Equal(LaunchBlock.None, launcher.Check(SteamGame(), LaunchChannel.Direct));

        _steamRunning = false;
        Assert.Equal(LaunchBlock.SteamNotRunning, launcher.Check(SteamGame(), LaunchChannel.Direct));

        // 非 Steam 版直启不需要 Steam
        Assert.Equal(LaunchBlock.None, launcher.Check(SteamGame(steamBuild: false, withSteam: false), LaunchChannel.Direct));
    }

    [Theory]
    [InlineData(LaunchChannel.Steam)]
    [InlineData(LaunchChannel.RunOnce)]
    public void Check_SteamChannels(LaunchChannel channel)
    {
        GameLauncher launcher = CreateLauncher();
        Assert.Equal(LaunchBlock.SteamNotFound, launcher.Check(SteamGame(withSteam: false), channel));
        Assert.Equal(LaunchBlock.NotSteamBuild, launcher.Check(SteamGame(steamBuild: false), channel));

        // 经 Steam 的通道会自己打开 Steam，不要求它已在运行
        _steamRunning = false;
        Assert.Equal(LaunchBlock.None, launcher.Check(SteamGame(), channel));
    }

    // === 启动内容 ===

    [Fact]
    public void Direct_StartsExeInGameDir_WithSteamIds()
    {
        LaunchRequest request = GameLauncher.CreateRequest(SteamGame(), new LaunchOptions { DeveloperMode = true }, LaunchChannel.Direct);

        ProcessStartInfo start = request.StartInfo;
        Assert.Equal(GameExe, start.FileName);
        Assert.Equal(Path.GetDirectoryName(GameExe), start.WorkingDirectory);
        Assert.False(start.UseShellExecute);
        Assert.Equal("--developerMode", start.Arguments);
        Assert.Equal("949230", start.Environment["SteamAppId"]);
        Assert.Equal("949230", start.Environment["SteamGameId"]);
        Assert.Null(request.RunOnceText);
        Assert.Equal("--developerMode", request.Arguments);
    }

    [Fact]
    public void Direct_NonSteamBuild_DoesNotSetSteamIds()
    {
        LaunchRequest request = GameLauncher.CreateRequest(
            SteamGame(steamBuild: false, withSteam: false),
            new LaunchOptions { DeveloperMode = true },
            LaunchChannel.Direct);

        Assert.False(request.StartInfo.Environment.ContainsKey("SteamAppId"));
        Assert.False(request.StartInfo.Environment.ContainsKey("SteamGameId"));
    }

    [Fact]
    public void Steam_UsesApplaunch()
    {
        LaunchRequest request = GameLauncher.CreateRequest(SteamGame(), new LaunchOptions { NoSplash = true }, LaunchChannel.Steam);
        Assert.Equal(SteamExe, request.StartInfo.FileName);
        Assert.False(request.StartInfo.UseShellExecute);
        Assert.Equal("-applaunch 949230 --noSplash", request.StartInfo.Arguments);

        LaunchRequest bare = GameLauncher.CreateRequest(SteamGame(), new LaunchOptions(), LaunchChannel.Steam);
        Assert.Equal("-applaunch 949230", bare.StartInfo.Arguments);
    }

    [Fact]
    public void RunOnce_OpensSteamUrlWithoutArguments()
    {
        var options = new LaunchOptions { DeveloperMode = true, DisableBurstCompilation = true };
        LaunchRequest request = GameLauncher.CreateRequest(SteamGame(), options, LaunchChannel.RunOnce);

        Assert.Equal("steam://rungameid/949230", request.StartInfo.FileName);
        Assert.True(request.StartInfo.UseShellExecute);
        Assert.Equal(string.Empty, request.StartInfo.Arguments);
        Assert.Equal("--developerMode", request.RunOnceText);
    }

    [Fact]
    public void CreateRequest_WithoutGameOrSteam_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GameLauncher.CreateRequest(GameLocateResult.None, new LaunchOptions(), LaunchChannel.Direct));
        Assert.Throws<InvalidOperationException>(() =>
            GameLauncher.CreateRequest(SteamGame(withSteam: false), new LaunchOptions(), LaunchChannel.Steam));
        Assert.Throws<InvalidOperationException>(() =>
            GameLauncher.CreateRequest(SteamGame(withSteam: false), new LaunchOptions(), LaunchChannel.RunOnce));
    }

    // === 启动 ===

    [Fact]
    public void Launch_RunOnce_WritesFileThenOpensUrl()
    {
        LaunchResult result = CreateLauncher().Launch(SteamGame(), new LaunchOptions { NoSplash = true, CustomArguments = "--a\n--b" },
            LaunchChannel.RunOnce);

        Assert.Equal(LaunchBlock.None, result.Block);
        ProcessStartInfo start = Assert.Single(_started);
        Assert.Equal("steam://rungameid/949230", start.FileName);

        byte[] bytes = File.ReadAllBytes(RunOncePath);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "runOnce.txt 不应带 BOM");
        Assert.Equal("--noSplash --a --b", Encoding.UTF8.GetString(bytes));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void Launch_RunOnce_OverwritesOldContent_AndEmptyDeletesIt()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(RunOncePath, "--continuelastsave\r\n");
        GameLauncher launcher = CreateLauncher();

        launcher.Launch(SteamGame(), new LaunchOptions { DeveloperMode = true }, LaunchChannel.RunOnce);
        Assert.Equal("--developerMode", File.ReadAllText(RunOncePath));

        launcher.Launch(SteamGame(), new LaunchOptions(), LaunchChannel.RunOnce);
        Assert.False(File.Exists(RunOncePath));
        Assert.Equal(2, _started.Count);
    }

    [Fact]
    public void Launch_Direct_LeavesRunOnceAlone()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(RunOncePath, "--noSplash");

        LaunchResult result = CreateLauncher().Launch(SteamGame(), new LaunchOptions(), LaunchChannel.Direct);

        Assert.Equal(LaunchChannel.Direct, result.Request?.Channel);
        Assert.Equal(GameExe, Assert.Single(_started).FileName);
        Assert.Equal("--noSplash", File.ReadAllText(RunOncePath));
    }

    [Fact]
    public void Launch_Blocked_DoesNothing()
    {
        _steamRunning = false;
        LaunchResult direct = CreateLauncher().Launch(SteamGame(), new LaunchOptions(), LaunchChannel.Direct);
        Assert.Equal(LaunchBlock.SteamNotRunning, direct.Block);
        Assert.Null(direct.Request);

        LaunchResult runOnce = CreateLauncher().Launch(SteamGame(withSteam: false), new LaunchOptions { NoSplash = true }, LaunchChannel.RunOnce);
        Assert.Equal(LaunchBlock.SteamNotFound, runOnce.Block);

        Assert.Empty(_started);
        Assert.False(File.Exists(RunOncePath));
    }

    [Fact]
    public void Launch_RunOnce_StartThrows_RemovesNewlyCreatedFile()
    {
        var launcher = new GameLauncher(RunOncePath, () => false, () => true, _ => throw new InvalidOperationException("boom"));

        Assert.Throws<InvalidOperationException>(() =>
            launcher.Launch(SteamGame(), new LaunchOptions { NoSplash = true }, LaunchChannel.RunOnce));

        Assert.False(File.Exists(RunOncePath));
    }

    [Fact]
    public void Launch_RunOnce_StartThrows_RestoresPreviousFile()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(RunOncePath, "--previous-flag");
        var launcher = new GameLauncher(RunOncePath, () => false, () => true, _ => throw new InvalidOperationException("boom"));

        Assert.Throws<InvalidOperationException>(() =>
            launcher.Launch(SteamGame(), new LaunchOptions { DeveloperMode = true }, LaunchChannel.RunOnce));

        Assert.Equal("--previous-flag", File.ReadAllText(RunOncePath));
    }
}
