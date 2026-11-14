using System.Text.RegularExpressions;
using MapExTools.Core.CommandLine;
using MapExTools.Core.Localization;

namespace MapExTools.Tests.CommandLine;

public partial class CommandLineTextTests
{
    // 选项行：两个空格缩进、语法、至少两个空格、说明
    [GeneratedRegex(@"^  (?<syntax>--\S.*?)  +(?<text>\S.*)$")]
    private static partial Regex OptionRow();

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (string language in UiLanguage.All)
            data.Add(language);
        return data;
    }

    [Theory]
    [InlineData("--mode", 6)]
    [InlineData("<目录>", 6)]
    [InlineData("<名稱>", 6)]
    [InlineData("（默认）", 8)]
    public void DisplayWidth_CountsWideCharactersTwice(string text, int expected)
    {
        Assert.Equal(expected, CommandLineText.DisplayWidth(text));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Usage_DescriptionsLineUp(string language)
    {
        string[] lines = CommandLineText.Usage(language).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var columns = new HashSet<int>();
        int rows = 0;
        foreach (string line in lines)
        {
            Match match = OptionRow().Match(line);
            if (!match.Success)
                continue;
            rows++;
            columns.Add(CommandLineText.DisplayWidth(line[..match.Groups["text"].Index]));
        }

        // 9 个设定、--reset、--launch、--show、--lang、--help、--user-data、--game-process
        Assert.Equal(CommandLineParser.SettingOptions.Count + 7, rows);
        Assert.Single(columns);
    }
}
