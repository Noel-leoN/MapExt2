using System.Globalization;
using MapExTools.Core.Localization;

namespace MapExTools.Core.Launch;

/// <summary>启动游戏一栏与命令行共用的文字（计划 2.2），按界面语言生成。</summary>
public static class LaunchText
{
    /// <summary>通道的完整名称，如"直接启动 Cities2.exe（推荐）"。</summary>
    public static string ChannelName(LaunchChannel channel, string language) =>
        Strings.Get(language, "Launch.Channel." + channel);

    /// <summary>通道的简称，用在"已启动（直启，…）"里。</summary>
    public static string ChannelShort(LaunchChannel channel, string language) =>
        Strings.Get(language, "Launch.ChannelShort." + channel);

    /// <summary>不能启动的原因。</summary>
    public static string Block(LaunchBlock block, string language) =>
        Strings.Get(language, "Launch.Block." + block);

    /// <summary>游戏位置一行：路径与来源；没找到时提示手动指定。</summary>
    public static string Location(GameLocateResult result, string language)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Game == null
            ? Strings.Get(language, "Launch.GameNotFound")
            : Format(language, "Launch.Game", result.Game.ExePath, Strings.Get(language, "Launch.Source." + result.Game.Source));
    }

    /// <summary>
    /// 实际执行的内容：直启为 <c>Cities2.exe 参数</c>，<c>-applaunch</c> 为 <c>steam.exe -applaunch 949230 参数</c>，
    /// runOnce 为写进文件的参数与打开的链接。
    /// </summary>
    /// <param name="arguments">直启与 <c>-applaunch</c> 为参数串，runOnce 为 runOnce.txt 的内容。</param>
    public static string Command(LaunchChannel channel, string arguments, string language)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return channel switch
        {
            LaunchChannel.Direct => Join(GameLocator.ExeName, arguments),
            LaunchChannel.Steam => Join("steam.exe -applaunch " + GameLocator.SteamAppId, arguments),
            _ => arguments.Length > 0
                ? Format(language, "Launch.PreviewRunOnce", arguments, GameLauncher.SteamRunUrl)
                : Format(language, "Launch.PreviewRunOnceEmpty", GameLauncher.SteamRunUrl),
        };
    }

    /// <summary>按当前参数预览某条通道要执行的内容。</summary>
    public static string Preview(LaunchChannel channel, LaunchOptions options, string language)
    {
        ArgumentNullException.ThrowIfNull(options);

        string arguments = channel == LaunchChannel.RunOnce
            ? LaunchArgumentBuilder.BuildRunOnceText(options)
            : LaunchArgumentBuilder.BuildCommandLine(options);
        return Command(channel, arguments, language);
    }

    /// <summary>启动配置折叠栏的标题摘要：通道简称（或未定位游戏）加生效参数项数。</summary>
    public static string OptionsSummary(GameLocateResult result, LaunchOptions options, string language)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        string head = result.Game == null
            ? Strings.Get(language, "Launch.Summary.NoGame")
            : ChannelShort(options.Channel, language);
        int count = LaunchArgumentBuilder.ActiveOptionCount(options);
        string tail = count switch
        {
            0 => Strings.Get(language, "Launch.Summary.NoOptions"),
            1 => Strings.Get(language, "Launch.Summary.OneOption"),
            _ => Format(language, "Launch.Summary.Options", count),
        };
        return Format(language, "Launch.Summary.Join", head, tail);
    }

    private static string Join(string program, string arguments) =>
        arguments.Length > 0 ? program + " " + arguments : program;

    private static string Format(string language, string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Strings.Get(language, key), args);
}
