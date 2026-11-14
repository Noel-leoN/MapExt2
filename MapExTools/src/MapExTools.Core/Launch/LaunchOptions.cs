using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapExTools.Core.Launch;

/// <summary>启动游戏的三条通道，按优先级排列（计划 §2 第 3 条）。</summary>
public enum LaunchChannel
{
    /// <summary>直接启动 <c>Cities2.exe</c>，注入 <c>SteamAppId</c>、<c>SteamGameId</c>。默认。</summary>
    Direct,

    /// <summary><c>steam.exe -applaunch 949230 &lt;参数&gt;</c>。</summary>
    Steam,

    /// <summary>参数写进 <c>{UserData}/runOnce.txt</c>，再打开 <c>steam://rungameid/949230</c>。</summary>
    RunOnce,
}

/// <summary>
/// 启动方式与参数，存在 <c>MapExTools.json</c> 的 <c>launch</c> 里，界面上改了就保存，命令行 <c>--launch</c> 也用它。
/// 不提供读档类参数（<c>--startGame=</c>、<c>--startEditor</c>、<c>--continuelastsave</c>，计划 D6）。
/// </summary>
public sealed class LaunchOptions
{
    public LaunchChannel Channel { get; set; } = LaunchChannel.Direct;

    /// <summary><c>--disableUserSection</c>：隐藏主菜单的用户区。</summary>
    public bool DisableUserSection { get; set; }

    /// <summary><c>--disableCodeModding</c>：禁用代码 Mod（MapExt 也会被禁用）。</summary>
    public bool DisableCodeModding { get; set; }

    /// <summary><c>--disableModding</c>：禁用代码与资产 Mod，同时隐含 <see cref="DisableCodeModding"/>。</summary>
    public bool DisableModding { get; set; }

    /// <summary><c>--noSplash</c>：跳过开场画面。</summary>
    public bool NoSplash { get; set; }

    /// <summary><c>--developerMode</c>。</summary>
    public bool DeveloperMode { get; set; }

    /// <summary><c>--uiDeveloperMode</c>：UI 调试器与内存跟踪。</summary>
    public bool UiDeveloperMode { get; set; }

    /// <summary><c>--duplicateLogToDefault</c>：各日志也复制进 Player.log。</summary>
    public bool DuplicateLogToDefault { get; set; }

    /// <summary>
    /// <c>--logsEffectiveness=</c> 的级别，取自 <see cref="LaunchArgumentBuilder.LogLevels"/>；null 或认不出时不传参
    /// （游戏遇到不认识的级别会关掉全部日志）。
    /// </summary>
    public string? LogLevel { get; set; }

    /// <summary><c>--burst-disable-compilation</c>：只在进程命令行里有效，走 runOnce.txt 时不传。</summary>
    public bool DisableBurstCompilation { get; set; }

    /// <summary>自定义参数，原样附在后面。</summary>
    public string? CustomArguments { get; set; }

    /// <summary>本版本不认识的字段，原样保留。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
