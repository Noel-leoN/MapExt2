using MapExTools.Core.Launch;
using MapExTools.Core.Localization;

namespace MapExTools.Tests.Launch;

public class LaunchTextTests
{
    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (string language in UiLanguage.All)
            data.Add(language);
        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryEnumValue_HasText(string language)
    {
        // 这几类文字按枚举名拼键，漏一条时 Strings.Get 会原样返回键名
        foreach (LaunchChannel channel in Enum.GetValues<LaunchChannel>())
        {
            Assert.DoesNotContain("Launch.", LaunchText.ChannelName(channel, language), StringComparison.Ordinal);
            Assert.DoesNotContain("Launch.", LaunchText.ChannelShort(channel, language), StringComparison.Ordinal);
            Assert.DoesNotContain("Launch.", Strings.Get(language, "Launch.ChannelNote." + channel), StringComparison.Ordinal);
        }

        foreach (LaunchBlock block in Enum.GetValues<LaunchBlock>())
            Assert.DoesNotContain("Launch.", LaunchText.Block(block, language), StringComparison.Ordinal);

        foreach (GameLocationSource source in Enum.GetValues<GameLocationSource>())
            Assert.DoesNotContain("Launch.", Strings.Get(language, "Launch.Source." + source), StringComparison.Ordinal);
    }

    [Fact]
    public void Command_PerChannel()
    {
        Assert.Equal("Cities2.exe", LaunchText.Command(LaunchChannel.Direct, "", UiLanguage.English));
        Assert.Equal("Cities2.exe --noSplash", LaunchText.Command(LaunchChannel.Direct, "--noSplash", UiLanguage.English));
        Assert.Equal("steam.exe -applaunch 949230 --noSplash", LaunchText.Command(LaunchChannel.Steam, "--noSplash", UiLanguage.English));
        Assert.Equal("runOnce.txt：--noSplash；再打开 steam://rungameid/949230",
            LaunchText.Command(LaunchChannel.RunOnce, "--noSplash", UiLanguage.SimplifiedChinese));
        Assert.Equal("不带参数打开 steam://rungameid/949230（删除已有的 runOnce.txt）",
            LaunchText.Command(LaunchChannel.RunOnce, "", UiLanguage.SimplifiedChinese));
    }

    [Fact]
    public void Preview_UsesRunOnceTextForRunOnce()
    {
        var options = new LaunchOptions { DeveloperMode = true, DisableBurstCompilation = true };
        Assert.Equal("Cities2.exe --developerMode --burst-disable-compilation",
            LaunchText.Preview(LaunchChannel.Direct, options, UiLanguage.English));
        Assert.Equal("runOnce.txt: --developerMode; then opens steam://rungameid/949230",
            LaunchText.Preview(LaunchChannel.RunOnce, options, UiLanguage.English));
    }

    [Fact]
    public void Location()
    {
        Assert.Equal(Strings.Get(UiLanguage.SimplifiedChinese, "Launch.GameNotFound"),
            LaunchText.Location(GameLocateResult.None, UiLanguage.SimplifiedChinese));

        var found = new GameLocateResult(new GameLocation(@"D:\G\Cities2.exe", GameLocationSource.Skyve, false), null, null);
        Assert.Equal(@"游戏：D:\G\Cities2.exe（取自 Skyve 的记录）", LaunchText.Location(found, UiLanguage.SimplifiedChinese));
    }
}
