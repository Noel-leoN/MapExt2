using System.Diagnostics;

namespace MapExTools.Core.Launch;

/// <summary>现在不能用某条通道启动的原因。</summary>
public enum LaunchBlock
{
    None,

    /// <summary>游戏已在运行。</summary>
    GameRunning,

    /// <summary>没有可用的 <c>Cities2.exe</c>。</summary>
    GameNotFound,

    /// <summary>经 Steam 的两条通道：没找到 Steam。</summary>
    SteamNotFound,

    /// <summary>经 Steam 的两条通道：游戏不是 Steam 版。</summary>
    NotSteamBuild,

    /// <summary>
    /// 直启 Steam 版：Steam 没有运行。这时 <c>SteamAPI.Init()</c> 失败，游戏记
    /// "SteamAPI.Init() failed. Is Steam running?" 后报错退出（<c>SteamworksPlatform.Initialize</c>）。
    /// </summary>
    SteamNotRunning,
}

/// <summary>一次启动要做的事。</summary>
/// <param name="StartInfo">要启动的进程：直启为游戏，<c>-applaunch</c> 为 <c>steam.exe</c>，runOnce 为 <c>steam://</c> 链接。</param>
/// <param name="RunOnceText">runOnce 通道写进 <c>runOnce.txt</c> 的内容（空串表示删掉该文件）；其余通道为 null。</param>
/// <param name="Arguments">传给游戏的参数，显示用；runOnce 通道为文件内容。</param>
public sealed record LaunchRequest(LaunchChannel Channel, ProcessStartInfo StartInfo, string? RunOnceText, string Arguments);

/// <param name="Block">不能启动的原因；已启动时为 <see cref="LaunchBlock.None"/>。</param>
/// <param name="Request">已执行的启动；没有启动时为 null。</param>
public sealed record LaunchResult(LaunchBlock Block, LaunchRequest? Request);

/// <summary>
/// 按三条通道启动游戏（计划 2.2、§2 第 3 条）。都只进主菜单，不传读档类参数（D6）。
/// <list type="bullet">
/// <item>直启：<c>UseShellExecute = false</c>，工作目录为游戏根目录，注入 <c>SteamAppId</c>、<c>SteamGameId</c>
/// （= 949230）。有这两个环境变量时 <c>SteamAPI.RestartAppIfNecessary</c> 返回 false，不经 Steam 重开、不弹 Paradox 启动器；
/// 0.1 实测缺 <c>steam_appid.txt</c> 时只靠它们也能直启。</item>
/// <item><c>-applaunch</c>：<c>steam.exe -applaunch 949230 &lt;参数&gt;</c>，Steam 不弹确认框（0.1 的 C）。</item>
/// <item>runOnce：参数写进 <c>runOnce.txt</c>，再打开不带参数的 <c>steam://rungameid/949230</c>。</item>
/// </list>
/// </summary>
public sealed class GameLauncher
{
    public const string SteamRunUrl = "steam://rungameid/" + GameLocator.SteamAppId;

    private readonly Func<bool> _isGameRunning;
    private readonly Func<bool> _isSteamRunning;
    private readonly Action<ProcessStartInfo> _startProcess;

    /// <param name="runOncePath"><c>{UserData}/runOnce.txt</c>。</param>
    /// <param name="isGameRunning">游戏是否在运行。</param>
    /// <param name="isSteamRunning">Steam 是否在运行。</param>
    /// <param name="startProcess">启动进程，默认 <see cref="Process.Start(ProcessStartInfo)"/>；测试时替换。</param>
    public GameLauncher(string runOncePath, Func<bool> isGameRunning, Func<bool> isSteamRunning, Action<ProcessStartInfo>? startProcess = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(runOncePath);
        ArgumentNullException.ThrowIfNull(isGameRunning);
        ArgumentNullException.ThrowIfNull(isSteamRunning);

        RunOncePath = Path.GetFullPath(runOncePath);
        _isGameRunning = isGameRunning;
        _isSteamRunning = isSteamRunning;
        _startProcess = startProcess ?? (start => Process.Start(start)?.Dispose());
    }

    public string RunOncePath { get; }

    /// <summary>检查现在能否用这条通道启动，不启动。</summary>
    public LaunchBlock Check(GameLocateResult location, LaunchChannel channel)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (_isGameRunning())
            return LaunchBlock.GameRunning;
        if (location.Game == null)
            return LaunchBlock.GameNotFound;

        if (channel == LaunchChannel.Direct)
            return location.Game.IsSteamBuild && !_isSteamRunning() ? LaunchBlock.SteamNotRunning : LaunchBlock.None;

        if (location.Steam == null)
            return LaunchBlock.SteamNotFound;
        return location.Game.IsSteamBuild ? LaunchBlock.None : LaunchBlock.NotSteamBuild;
    }

    /// <summary>生成启动内容，不检查、不执行。</summary>
    /// <exception cref="InvalidOperationException">没有游戏，或 Steam 通道没有 Steam。</exception>
    public static LaunchRequest CreateRequest(GameLocateResult location, LaunchOptions options, LaunchChannel channel)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(options);

        GameLocation game = location.Game ?? throw new InvalidOperationException("没有可用的 Cities2.exe");
        switch (channel)
        {
            case LaunchChannel.Direct:
            {
                string args = LaunchArgumentBuilder.BuildCommandLine(options);
                var start = new ProcessStartInfo(game.ExePath)
                {
                    UseShellExecute = false,
                    WorkingDirectory = game.GameDir,
                    Arguments = args,
                };
                if (game.IsSteamBuild)
                {
                    start.Environment["SteamAppId"] = GameLocator.SteamAppId;
                    start.Environment["SteamGameId"] = GameLocator.SteamAppId;
                }

                return new LaunchRequest(channel, start, null, args);
            }

            case LaunchChannel.Steam:
            {
                SteamInfo steam = location.Steam ?? throw new InvalidOperationException("没有找到 Steam");
                string args = LaunchArgumentBuilder.BuildCommandLine(options);
                var start = new ProcessStartInfo(steam.Exe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = steam.Dir,
                    Arguments = args.Length > 0 ? $"-applaunch {GameLocator.SteamAppId} {args}" : $"-applaunch {GameLocator.SteamAppId}",
                };
                return new LaunchRequest(channel, start, null, args);
            }

            case LaunchChannel.RunOnce:
            {
                _ = location.Steam ?? throw new InvalidOperationException("没有找到 Steam");
                string text = LaunchArgumentBuilder.BuildRunOnceText(options);
                var start = new ProcessStartInfo(SteamRunUrl) { UseShellExecute = true };
                return new LaunchRequest(channel, start, text, text);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
        }
    }

    /// <summary>检查通过就启动；runOnce 通道先写文件。不能启动时返回原因，不抛出。</summary>
    /// <exception cref="System.ComponentModel.Win32Exception">进程启动失败。</exception>
    /// <exception cref="IOException">写 runOnce.txt 失败。</exception>
    /// <exception cref="UnauthorizedAccessException">没有写 runOnce.txt 的权限。</exception>
    public LaunchResult Launch(GameLocateResult location, LaunchOptions options, LaunchChannel channel)
    {
        LaunchBlock block = Check(location, channel);
        if (block != LaunchBlock.None)
            return new LaunchResult(block, null);

        LaunchRequest request = CreateRequest(location, options, channel);
        bool wroteRunOnce = false;
        bool hadPreviousRunOnce = false;
        byte[]? previousRunOnceBytes = null;
        if (request.RunOnceText != null)
        {
            if (File.Exists(RunOncePath))
            {
                hadPreviousRunOnce = true;
                previousRunOnceBytes = File.ReadAllBytes(RunOncePath);
            }

            RunOnceFile.Write(RunOncePath, request.RunOnceText);
            wroteRunOnce = true;
        }

        try
        {
            _startProcess(request.StartInfo);
        }
        catch
        {
            if (wroteRunOnce)
                TryRollbackRunOnce(RunOncePath, hadPreviousRunOnce, previousRunOnceBytes);
            throw;
        }

        return new LaunchResult(LaunchBlock.None, request);
    }

    private static void TryRollbackRunOnce(string path, bool hadPrevious, byte[]? previousBytes)
    {
        try
        {
            if (hadPrevious && previousBytes != null)
                File.WriteAllBytes(path, previousBytes);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 回滚失败时不掩盖原始启动异常
        }
    }
}
