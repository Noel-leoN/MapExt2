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

    [Fact]
    public void ListPlaysets_AndListMods_SupportInactivePlaysetAndDecoupleBrokenCoc()
    {
        Directory.CreateDirectory(Paths.ModsCacheDir);
        File.WriteAllText(Paths.PlaysetSyncFile, """
            {
              "configVersion": 1,
              "playsets": [
                { "id": "ps-active", "backends": [ { "id": "pdx_mods", "playsetId": "111" } ] },
                { "id": "ps-other", "backends": [ { "id": "pdx_mods", "playsetId": "222" } ] }
              ]
            }
            """);
        File.WriteAllText(Paths.PdxModsCacheFile, """
            {
              "items": [
                { "source": "pdx_mods", "id": "86944", "version": "3", "userModVersion": "1.3.0", "displayName": "Inactive Builder Mod" }
              ]
            }
            """);
        File.WriteAllText(Paths.PlaysetConfigFile, """
            {
              "activePlaysetId": "ps-active",
              "playsets": [
                {
                  "id": "ps-active",
                  "presentation": { "name": "Active City" },
                  "mods": []
                },
                {
                  "id": "ps-other",
                  "isOffdisk": true,
                  "presentation": { "name": "Inactive Sandbox" },
                  "mods": [
                    { "source": "pdx_mods", "sourceId": "86944", "isEnabled": true },
                    { "source": "pdx_mods", "sourceId": "77777", "isEnabled": false }
                  ]
                },
                {
                  "id": "ps-dup-1",
                  "presentation": { "name": "SameName" },
                  "mods": []
                },
                {
                  "id": "ps-dup-2",
                  "presentation": { "name": "SameName" },
                  "mods": []
                }
              ]
            }
            """);

        // 故意写坏 MapExtPDX.coc：验证 --list-playsets 完全不碰 .coc，--list-mods 仅向 stderr 提示并正常列出模组
        WriteCoc("MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n    \"EnableDemandEcoSystem\": true\n}\r\n");

        (int lpCode, string lpOut, string lpErr) = RunSplit("--list-playsets");
        Assert.Equal(ExitCodes.Success, lpCode);
        Assert.Equal(string.Empty, lpErr);
        Assert.Contains("Active City", lpOut, StringComparison.Ordinal);
        Assert.Contains("Inactive Sandbox", lpOut, StringComparison.Ordinal);
        Assert.Contains("222", lpOut, StringComparison.Ordinal);

        // 按商店数字 ID "222" 读取非激活播放集的模组列表
        (int lmCode, string lmOut, string lmErr) = RunSplit("--list-mods", "--playset", "222");
        Assert.Equal(ExitCodes.Success, lmCode);
        Assert.Contains("Inactive Sandbox", lmOut, StringComparison.Ordinal);
        Assert.Contains("Inactive Builder Mod", lmOut, StringComparison.Ordinal);
        Assert.Contains("77777", lmOut, StringComparison.Ordinal);
        Assert.NotEmpty(lmErr); // .coc 损坏的温和提示写往 stderr

        // 重名歧义报错返回 1
        (int ambCode, _, string ambErr) = RunSplit("--list-mods", "--playset", "SameName");
        Assert.Equal(ExitCodes.Error, ambCode);
        Assert.Contains("ps-dup-1", ambErr, StringComparison.Ordinal);

        // 恢复合法 .coc：验证不存在的播放集配合 --launch 时直接拦截不启动，而有效非激活播放集配合 --launch 会向 stderr 输出只读提示
        WriteCoc("MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n}\r\n");

        (int nfCode, _, string nfErr) = RunSplit("--list-mods", "--playset", "GhostPlayset", "--launch");
        Assert.Equal(ExitCodes.Error, nfCode);
        Assert.Contains("GhostPlayset", nfErr, StringComparison.Ordinal);
        Assert.Empty(_started);

        (int roLaunchCode, string roOut, string roErr) = RunSplit("--check-conflicts", "--playset", "Inactive Sandbox", "--launch");
        Assert.Equal(ExitCodes.Success, roLaunchCode);
        Assert.Contains("Inactive Sandbox", roOut, StringComparison.Ordinal);
        Assert.Contains("--playset", roErr, StringComparison.Ordinal);
        Assert.Single(_started);
    }

    // === Phase 4：--activate-playset ===

    [Fact]
    public void ActivatePlayset_PrecheckFailure_LeavesCocAndConfigUntouched()
    {
        Directory.CreateDirectory(Paths.ModsCacheDir);
        const string cocBefore = "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n}\r\n";
        WriteCoc(cocBefore);
        const string cfgBefore =
            "{\"configVersion\":1,\"activePlaysetId\":\"ps-a\",\"playsets\":[" +
            "{\"id\":\"ps-a\",\"isOffdisk\":false,\"presentation\":{\"name\":\"Alpha\"},\"mods\":[]}," +
            "{\"id\":\"ps-b\",\"isOffdisk\":false,\"presentation\":{\"name\":\"Beta\"},\"mods\":[]}]}";
        File.WriteAllText(Paths.PlaysetConfigFile, cfgBefore);

        // 1. 存在外部有效写锁时预检失败：不改 .coc、不改 playset_config.json、不启动游戏
        string recentUtc = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        File.WriteAllText(Paths.PlaysetConfigLockFile,
            $$"""{"configVersion":1,"lockHolder":"OtherTool","updatedAt":"{{recentUtc}}"}""");

        (int lockCode, _, string lockErr) = RunSplit("--mode", "ModeB", "--activate-playset", "Beta", "--launch");
        Assert.Equal(ExitCodes.Error, lockCode);
        Assert.Contains("OtherTool", lockErr, StringComparison.Ordinal);
        Assert.Equal(cocBefore, ReadCoc());
        Assert.Equal(cfgBefore, File.ReadAllText(Paths.PlaysetConfigFile));
        Assert.Empty(_started);

        // 2. 移除写锁，但同时传入无效的 --playset：预检阶段合并拦下，同样不改 .coc 也不切换激活项
        File.Delete(Paths.PlaysetConfigLockFile);
        (int badInspectCode, _, string badInspectErr) = RunSplit("--mode", "ModeB", "--activate-playset", "Beta", "--list-mods", "--playset", "NoSuchSet");
        Assert.Equal(ExitCodes.Error, badInspectCode);
        Assert.Contains("NoSuchSet", badInspectErr, StringComparison.Ordinal);
        Assert.Equal(cocBefore, ReadCoc());
        Assert.Equal(cfgBefore, File.ReadAllText(Paths.PlaysetConfigFile));
    }

    [Fact]
    public void ActivatePlayset_PureMode_SucceedsEvenWhenCocIsMalformed_AndAlreadyActiveExits0()
    {
        Directory.CreateDirectory(Paths.ModsCacheDir);
        const string brokenCoc = "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n    \"EnableDemandEcoSystem\": true\n}\r\n";
        WriteCoc(brokenCoc);
        File.WriteAllText(Paths.PlaysetConfigFile,
            "{\"configVersion\":1,\"activePlaysetId\":\"ps-a\",\"playsets\":[" +
            "{\"id\":\"ps-a\",\"isOffdisk\":false,\"presentation\":{\"name\":\"Alpha\"},\"mods\":[]}," +
            "{\"id\":\"ps-b\",\"isOffdisk\":true,\"presentation\":{\"name\":\"Beta\"},\"mods\":[]}]}");

        // 纯 --activate-playset 不读 .coc：成功切换，且因目标未启用 MapEx 向 stderr 输出警告，退出码仍为 0
        (int code, string stdout, string stderr) = RunSplit("--activate-playset", "Beta");
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Beta", stdout, StringComparison.Ordinal);
        Assert.Contains("未启用 MapEx", stderr, StringComparison.Ordinal);
        Assert.Contains("\"activePlaysetId\":\"ps-b\"", File.ReadAllText(Paths.PlaysetConfigFile), StringComparison.Ordinal);
        Assert.True(File.Exists(Paths.PlaysetConfigFile + ".bak"));

        // 再次激活已是当前激活项的 Beta：AlreadyActive，退出码 0
        (int alreadyCode, string alreadyOut, _) = RunSplit("--activate-playset", "Beta");
        Assert.Equal(ExitCodes.Success, alreadyCode);
        Assert.Contains("已经是当前激活播放集", alreadyOut, StringComparison.Ordinal);
    }

    [Fact]
    public void ActivatePlayset_GameRunning_Exits2WithoutWriting()
    {
        Directory.CreateDirectory(Paths.ModsCacheDir);
        const string cfgBefore =
            "{\"configVersion\":1,\"activePlaysetId\":\"ps-a\",\"playsets\":[" +
            "{\"id\":\"ps-a\",\"presentation\":{\"name\":\"Alpha\"},\"mods\":[]}," +
            "{\"id\":\"ps-b\",\"presentation\":{\"name\":\"Beta\"},\"mods\":[]}]}";
        File.WriteAllText(Paths.PlaysetConfigFile, cfgBefore);
        _gameRunning = true;

        (int code, string stdout, string stderr) = RunSplit("--activate-playset", "Beta");
        Assert.Equal(ExitCodes.GameRunning, code);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains(Text("Cli.GameRunning"), stderr);
        Assert.Equal(cfgBefore, File.ReadAllText(Paths.PlaysetConfigFile));
    }

    [Fact]
    public void ActivatePlayset_PendingMapExWarning_AndDiagnosticsReadNewActivePlayset()
    {
        Directory.CreateDirectory(Paths.ModsCacheDir);
        WriteCoc("MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n}\r\n");
        File.WriteAllText(Paths.PdxModsCacheFile, """
            {
              "items": [
                { "source": "pdx_mods", "id": "107939", "version": "1", "displayName": "MapExt2" },
                { "source": "pdx_mods", "id": "99999", "version": "1", "displayName": "Alpha Only Mod" }
              ]
            }
            """);
        File.WriteAllText(Paths.PlaysetConfigFile,
            "{\"configVersion\":1,\"activePlaysetId\":\"ps-a\",\"playsets\":[" +
            "{\"id\":\"ps-a\",\"presentation\":{\"name\":\"Alpha\"},\"mods\":[{\"source\":\"pdx_mods\",\"sourceId\":\"99999\",\"isEnabled\":true}]}," +
            "{\"id\":\"ps-b\",\"presentation\":{\"name\":\"BetaPending\"},\"mods\":[{\"source\":\"pdx_mods\",\"sourceId\":\"107939\",\"isEnabled\":true}]}]}");

        // 激活 BetaPending 并附带 --list-mods（不传 --playset）：
        // 复用诊断扫描，向 stderr 发出 MapEx 待下载警告，并列出新激活项 BetaPending 的模组列表，退出码 0
        (int code, string stdout, string stderr) = RunSplit("--activate-playset", "BetaPending", "--list-mods");
        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("BetaPending", stdout, StringComparison.Ordinal);
        Assert.Contains("MapExt2", stdout, StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha Only Mod", stdout, StringComparison.Ordinal);
        Assert.Contains("尚未下载到本地", stderr, StringComparison.Ordinal);

        // 同时附带 --show、--list-playsets、--list-mods 时，“已将…设为当前激活播放集”始终排在所有查询输出之前
        (int comboCode, string comboOut, _) = RunSplit("--activate-playset", "Alpha", "--show", "--list-playsets", "--list-mods");
        Assert.Equal(ExitCodes.Success, comboCode);
        int posActivated = comboOut.IndexOf("已将「Alpha」设为当前激活播放集", StringComparison.Ordinal);
        int posShow = comboOut.IndexOf("地图尺寸模式", StringComparison.Ordinal);
        int posListPlaysets = comboOut.IndexOf("本地播放集", StringComparison.Ordinal);
        int posListMods = comboOut.IndexOf("模组列表 — Alpha", StringComparison.Ordinal);
        Assert.True(posActivated >= 0 && posActivated < posShow && posShow < posListPlaysets && posListPlaysets < posListMods,
            $"预期输出顺序为 激活结果 -> --show -> --list-playsets -> --list-mods，实际输出：\n{comboOut}");
    }

    [Fact]
    public void ActivatePlayset_WithUnsupportedVersionAndLaunch_RefusesLaunchAndExits1()
    {
        Directory.CreateDirectory(Paths.ModsCacheDir);
        WriteCoc("MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n}\r\n");
        File.WriteAllText(Paths.PlaysetConfigFile,
            "{\"configVersion\":2,\"activePlaysetId\":\"ps-a\",\"playsets\":[" +
            "{\"id\":\"ps-a\",\"presentation\":{\"name\":\"Alpha\"},\"mods\":[]}," +
            "{\"id\":\"ps-b\",\"presentation\":{\"name\":\"Beta\"},\"mods\":[]}]}");

        (int code, _, string stderr) = RunSplit("--activate-playset", "Beta", "--launch");
        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("configVersion", stderr, StringComparison.Ordinal);
        Assert.Empty(_started);
    }

    [Fact]
    public void ActivatePlayset_MissingConfig_EmitsDedicatedMissingMessageAndExits1()
    {
        (int code, string stdout, string stderr) = RunSplit("--activate-playset", "Beta");
        Assert.Equal(ExitCodes.Error, code);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains(Text("Conflicts.Activate.Err.ConfigMissing"), stderr, StringComparison.Ordinal);
    }
}
