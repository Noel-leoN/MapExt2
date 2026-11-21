using System.ComponentModel;
using System.Diagnostics;
using MapExTools.Core.CommandLine;
using MapExTools.Core.Config;
using MapExTools.Core.Launch;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.CommandLine;

/// <summary>命令行执行：临时用户目录，游戏进程、Steam、定位与启动都换成假的。</summary>
public sealed class CommandLineRunnerTests : IDisposable
{
    private const string Lang = UiLanguage.SimplifiedChinese;
    private const string GameExe = @"D:\Games\Steam\steamapps\common\Cities Skylines II\Cities2.exe";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<ProcessStartInfo> _started = [];
    private readonly ToolConfig _config = new();
    private bool _gameRunning;
    private bool _steamRunning = true;
    private Exception? _startFailure;
    private int _locateFailures;
    private GameLocateResult _location = new(
        new GameLocation(GameExe, GameLocationSource.Steam, true),
        new SteamInfo(@"D:\Games\Steam", @"D:\Games\Steam\steam.exe"),
        null);

    public CommandLineRunnerTests()
    {
        Paths = new UserDataPaths(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private UserDataPaths Paths { get; }

    private CommandLineContext CreateContext()
    {
        var launcher = new GameLauncher(Paths.RunOnceFile, () => _gameRunning, () => _steamRunning, start =>
        {
            if (_startFailure != null)
                throw _startFailure;
            _started.Add(start);
        });
        return new CommandLineContext
        {
            Paths = Paths,
            Config = _config,
            IsGameRunning = () => _gameRunning,
            LocateGame = () => _locateFailures-- > 0 ? throw new InvalidOperationException("定位失败（测试）") : _location,
            Launcher = launcher,
        };
    }

    /// <summary>正常结果与错误合用一个输出（按时间先后核对文字）。</summary>
    private (int Code, string Output) Run(params string[] args)
    {
        var output = new StringWriter();
        int code = new CommandLineRunner(CreateContext(), output, Lang).Run(CommandLineParser.Parse(args));
        return (code, output.ToString());
    }

    /// <summary>正常结果与错误分开（界面程序分别接到 stdout 与 stderr）。</summary>
    private (int Code, string Output, string Error) RunSplit(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = new CommandLineRunner(CreateContext(), output, Lang, error).Run(CommandLineParser.Parse(args));
        return (code, output.ToString(), error.ToString());
    }

    private static string Text(string key) => Strings.Get(Lang, key);

    private void WriteCoc(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.CocFile)!);
        File.WriteAllText(Paths.CocFile, text);
    }

    private string ReadCoc() => File.ReadAllText(Paths.CocFile);

    // === 帮助与显示 ===

    [Fact]
    public void Help_PrintsUsage_WithEveryOption()
    {
        (int code, string output) = Run("--help", "--mode", "ModeB");

        Assert.Equal(ExitCodes.Success, code);
        foreach (SettingOption option in CommandLineParser.SettingOptions)
            Assert.Contains(option.Name, output);
        Assert.Contains("--game-process", output);
        Assert.False(File.Exists(Paths.CocFile), "--help 不应写入");
    }

    [Fact]
    public void Show_WithoutFile_ListsDefaults()
    {
        (int code, string output) = Run("--show");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("  地图尺寸模式：ModeA 57km（默认）", output);
        Assert.Contains("    RCI需求调节系统组：开（默认）", output);
        Assert.Contains(Text("Startup.NoFile"), output);
        Assert.Contains(@"游戏：D:\Games\Steam\steamapps\common\Cities Skylines II\Cities2.exe（经 Steam 找到）", output);
        Assert.Contains("启动方式：直接启动 Cities2.exe（推荐）", output);
        Assert.Contains("命令行：Cities2.exe", output);
        Assert.Contains(Text("Status.GameNotRunning"), output);
    }

    [Fact]
    public void Show_ListsRunOnceAndLaunchOptions()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Paths.RunOnceFile, "--continuelastsave\r\n");
        _config.Launch.Channel = LaunchChannel.Steam;
        _config.Launch.DeveloperMode = true;
        _location = GameLocateResult.None;

        (int code, string output) = Run("--show");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("runOnce.txt 里有下次启动的一次性参数：--continuelastsave", output);
        Assert.Contains("命令行：steam.exe -applaunch 949230 --developerMode", output);
        Assert.Contains(Text("Launch.GameNotFound"), output);
    }

    [Fact]
    public void Show_WhileGameRunning_IsAllowed()
    {
        _gameRunning = true;
        (int code, string output) = Run("--show");
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(Text("Status.GameRunning"), output);
    }

    // === 写入设定 ===

    [Fact]
    public void SetMode_CreatesFile()
    {
        (int code, string output) = Run("--mode", "modeb");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("\"PatchModeChoice\": \"ModeB\"", ReadCoc());
        Assert.Contains("已写入：地图尺寸模式 ModeA 57km→ModeB 28km", output);
    }

    [Fact]
    public void SameValueAgain_WritesNothing()
    {
        Run("--mode", "ModeB");
        DateTime before = File.GetLastWriteTimeUtc(Paths.CocFile);

        (int code, string output) = Run("--mode", "ModeB");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(Text("Startup.NoChanges"), output);
        Assert.Equal(before, File.GetLastWriteTimeUtc(Paths.CocFile));
    }

    [Fact]
    public void Reset_ThenOverride()
    {
        WriteCoc("MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeB\",\n    \"EnableDemandEcoSystem\": false\n}\r\n");

        (int code, _) = Run("--reset", "--mode", "ModeD");

        Assert.Equal(ExitCodes.Success, code);
        string text = ReadCoc();
        Assert.Contains("\"PatchModeChoice\": \"ModeD\"", text);
        Assert.Contains("\"EnableDemandEcoSystem\": true", text);
    }

    [Fact]
    public void GameRunning_RefusesWrites_Exit2()
    {
        const string original = "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n}\r\n";
        WriteCoc(original);
        _gameRunning = true;

        (int code, string output) = Run("--mode", "ModeB", "--launch");

        Assert.Equal(ExitCodes.GameRunning, code);
        Assert.Contains(Text("Cli.GameRunning"), output);
        Assert.Equal(original, ReadCoc());
        Assert.Empty(_started);
    }

    [Fact]
    public void MalformedFile_Exit1_NothingWrittenOrLaunched()
    {
        const string broken = "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n    \"EnableDemandEcoSystem\": true\n}\r\n";
        WriteCoc(broken);

        (int code, string output) = Run("--demand", "off", "--launch");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("设定文件格式有误：第 4 行第 5 列：两项之间缺少逗号", output);
        Assert.Contains(Text("Cli.LaunchRefusedCoc"), output);
        Assert.Equal(broken, ReadCoc());
        Assert.Empty(_started);
    }

    // === 启动 ===

    [Fact]
    public void Launch_Direct_UsesSavedOptions()
    {
        _config.Launch.NoSplash = true;

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Success, code);
        ProcessStartInfo start = Assert.Single(_started);
        Assert.Equal(GameExe, start.FileName);
        Assert.Equal("--noSplash", start.Arguments);
        Assert.Contains("已启动（直启）：Cities2.exe --noSplash", output);
    }

    [Fact]
    public void Launch_AfterSaving()
    {
        (int code, string output) = Run("--mode", "ModeC", "--launch");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("\"PatchModeChoice\": \"ModeC\"", ReadCoc());
        Assert.True(output.IndexOf("已写入", StringComparison.Ordinal) < output.IndexOf("已启动", StringComparison.Ordinal));
        Assert.Single(_started);
    }

    [Fact]
    public void CheckConflicts_EmptyUserData_Exits0AndPrintsBanner()
    {
        (int code, string output) = Run("--check-conflicts");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("ModeA", output, StringComparison.Ordinal);
        Assert.Empty(_started);
    }

    [Fact]
    public void CheckConflicts_WithLaunch_OutputsConflictsBeforeLaunching()
    {
        (int code, string output) = Run("--check-conflicts", "--launch");

        Assert.Equal(ExitCodes.Success, code);
        int conflictPos = output.IndexOf("ModeA", StringComparison.Ordinal);
        int launchPos = output.IndexOf("已启动", StringComparison.Ordinal);
        Assert.True(conflictPos >= 0 && launchPos > conflictPos, $"预期先输出冲突再启动，实际输出：\n{output}");
        Assert.Single(_started);
    }

    [Fact]
    public void Launch_SteamNotRunning_FallsBackToApplaunch()
    {
        _steamRunning = false;

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(Text("Cli.SteamFallback"), output);
        ProcessStartInfo start = Assert.Single(_started);
        Assert.Equal(@"D:\Games\Steam\steam.exe", start.FileName);
        Assert.Equal("-applaunch 949230", start.Arguments);
        Assert.Contains("已启动（经 Steam）：steam.exe -applaunch 949230", output);
    }

    [Fact]
    public void Launch_SteamNotRunning_NoSteamFound_Exit1()
    {
        _steamRunning = false;
        _location = new GameLocateResult(new GameLocation(GameExe, GameLocationSource.Manual, true), null, null);

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("没有启动游戏：" + Text("Launch.Block.SteamNotRunning"), output);
        Assert.Empty(_started);
    }

    [Fact]
    public void Launch_GameNotFound_Exit1()
    {
        _location = new GameLocateResult(null, null, @"D:\Old\Cities2.exe");

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains(@"手动指定的路径已不存在（D:\Old\Cities2.exe）", output);
        Assert.Contains("没有启动游戏：" + Text("Launch.Block.GameNotFound"), output);
    }

    [Fact]
    public void Launch_MentionsLeftoverRunOnce()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Paths.RunOnceFile, "--startEditor");

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("runOnce.txt 里还有参数，这次启动时游戏也会读进去：--startEditor", output);
        Assert.Equal("--startEditor", File.ReadAllText(Paths.RunOnceFile));
    }

    [Fact]
    public void Launch_RunOnceChannel_WritesFile()
    {
        _config.Launch.Channel = LaunchChannel.RunOnce;
        _config.Launch.DeveloperMode = true;

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal("--developerMode", File.ReadAllText(Paths.RunOnceFile));
        Assert.Equal("steam://rungameid/949230", Assert.Single(_started).FileName);
        Assert.Contains("已启动（经 runOnce.txt）：runOnce.txt：--developerMode；再打开 steam://rungameid/949230", output);
    }

    [Fact]
    public void Launch_StartFails_Exit1()
    {
        _startFailure = new Win32Exception(2, "找不到文件");

        (int code, string output) = Run("--launch");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("无法启动游戏：找不到文件", output);
    }

    [Fact]
    public void ShowFails_WithLaunch_StillLaunches_Exit1()
    {
        // --show 里定位游戏失败；启动时再定位就正常：与调整顺序前一样照常启动，退出码记为出错
        _locateFailures = 1;

        (int code, string output, string error) = RunSplit("--show", "--launch");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Single(_started);
        Assert.Contains("定位失败（测试）", error);
        Assert.Contains("已启动", output);
    }

    // === 输出分流 ===

    [Fact]
    public void SplitStreams_SavedToOutput_LaunchProblemsToError()
    {
        _location = new GameLocateResult(null, null, @"D:\Old\Cities2.exe");

        (int code, string output, string error) = RunSplit("--mode", "ModeB", "--launch");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("已写入", output);
        Assert.DoesNotContain("没有启动游戏", output);
        Assert.Contains(@"手动指定的路径已不存在（D:\Old\Cities2.exe）", error);
        Assert.Contains("没有启动游戏：" + Text("Launch.Block.GameNotFound"), error);
        Assert.DoesNotContain("已写入", error);
    }

    [Fact]
    public void SplitStreams_GameRunning_OnlyError()
    {
        _gameRunning = true;

        (int code, string output, string error) = RunSplit("--mode", "ModeB");

        Assert.Equal(ExitCodes.GameRunning, code);
        Assert.Equal(string.Empty, output);
        Assert.Contains(Text("Cli.GameRunning"), error);
    }

    [Fact]
    public void SplitStreams_ShowAndConflictReportGoToOutput()
    {
        (int code, string output, string error) = RunSplit("--show", "--check-conflicts");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(Text("Status.GameNotRunning"), output);
        Assert.Contains("ModeA", output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error);
    }
}
