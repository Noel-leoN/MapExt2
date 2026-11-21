using MapExTools.Core.Coc;
using MapExTools.Core.Localization;

namespace MapExTools.Tests.Coc;

/// <summary>切块与严格 JSON 检查：手动编辑常见错误报出种类与行列（行列从 1 起）。</summary>
public sealed class CocParserTests
{
    public static TheoryData<string, CocErrorKind, int, int, string?> FormatErrors() => new()
    {
        // 开发版测试第 10 节的 7 种手动编辑错误，行列与开发版一致
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n    \"isEnableEconomyFix\": true\n}\r\n", CocErrorKind.CommaMissing, 4, 5, null },
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\",\n}\r\n", CocErrorKind.TrailingComma, 3, 31, null },
        { "MapExtPDX\r\n{\n    \"isEnableEconomyFix\": tru\n}\r\n", CocErrorKind.BadLiteral, 3, 27, "tru" },
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": ModeC\n}\r\n", CocErrorKind.BadLiteral, 3, 24, "ModeC" },
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": ,\n    \"A\": 1\n}\r\n", CocErrorKind.ValueMissing, 3, 24, "PatchModeChoice" },
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\n}\r\n", CocErrorKind.StringUnclosed, 3, 24, null },
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\" \"ModeA\"\n}\r\n", CocErrorKind.ColonMissing, 3, 23, "PatchModeChoice" },

        // 开发版测试第 9 节：括号不配对，报在开括号处
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n", CocErrorKind.BracketUnclosed, 2, 1, null },

        // 块名之后直到文件末尾都没有 {
        { "MapExtPDX\r\n", CocErrorKind.NoOpenBrace, 1, 1, "MapExtPDX" },

        // 键名不是字符串
        { "MapExtPDX\r\n{\n    PatchModeChoice: \"ModeA\"\n}\r\n", CocErrorKind.KeyExpected, 3, 5, null },

        // 键名重复：游戏解码用 Dictionary.Add，会读不进整个块；报在第二次出现处
        { "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\",\n    \"PatchModeChoice\": \"ModeB\"\n}\r\n", CocErrorKind.DuplicateKey, 4, 5, "PatchModeChoice" },

        // 全角空格、不换行空格不是游戏认可的空白
        { "MapExtPDX\r\n{\n    \"A\": 1,\u3000\"B\": 2\n}\r\n", CocErrorKind.KeyExpected, 3, 12, null },
        { "MapExtPDX\r\n{\n    \"A\": 1\u00A0\n}\r\n", CocErrorKind.BadLiteral, 3, 10, "1\u00A0" },

        // 5.4 严格括号配对与字符串控制字符/转义换行校验
        { "MapExtPDX\r\n{\n    \"List\": [1, 2}\n}\r\n", CocErrorKind.BracketUnclosed, 3, 13, null },
        { "MapExtPDX\r\n{\n    \"S\": \"a\\\nb\"\n}\r\n", CocErrorKind.StringUnclosed, 3, 10, null },
        { "MapExtPDX\r\n{\n    \"S\": \"a\rb\"\n}\r\n", CocErrorKind.StringUnclosed, 3, 10, null },
        { "MapExtPDX\r\n{\n    \"S\": \"a\\", CocErrorKind.StringUnclosed, 3, 10, null },
    };

    [Theory]
    [MemberData(nameof(FormatErrors))]
    public void Parse_FormatError_ReportsKindAndPosition(string text, CocErrorKind kind, int line, int column, string? detail)
    {
        CocFormatException ex = Assert.Throws<CocFormatException>(() => StartupSettingsState.Parse(text));
        Assert.Equal(kind, ex.Kind);
        Assert.Equal(line, ex.Line);
        Assert.Equal(column, ex.Column);
        Assert.Equal(detail, ex.Detail);
    }

    [Theory]
    [InlineData(UiLanguage.SimplifiedChinese, "第 4 行第 5 列：两项之间缺少逗号")]
    [InlineData(UiLanguage.TraditionalChinese, "第 4 行第 5 欄：兩項之間缺少逗號")]
    [InlineData(UiLanguage.English, "line 4, column 5: missing comma between two entries")]
    public void GetMessage_FormatsPositionInEachLanguage(string language, string expected)
    {
        CocFormatException ex = Assert.Throws<CocFormatException>(() => StartupSettingsState.Parse(
            "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeA\"\n    \"isEnableEconomyFix\": true\n}\r\n"));
        Assert.Equal(expected, ex.GetMessage(language));
    }

    [Fact]
    public void GetMessage_IncludesDetail_AndExceptionMessageIsEnglish()
    {
        CocFormatException ex = Assert.Throws<CocFormatException>(() => StartupSettingsState.Parse(
            "MapExtPDX\r\n{\n    \"isEnableEconomyFix\": tru\n}\r\n"));
        Assert.Equal("第 3 行第 27 列：无法识别的取值 'tru'", ex.GetMessage(UiLanguage.SimplifiedChinese));
        Assert.Equal("line 3, column 27: unrecognized value 'tru'", ex.Message);

        CocFormatException brace = Assert.Throws<CocFormatException>(() => StartupSettingsState.Parse("MapExtPDX\r\n"));
        Assert.Equal("第 1 行第 1 列：块 'MapExtPDX' 之后缺少 {", brace.GetMessage(UiLanguage.SimplifiedChinese));
    }

    [Fact]
    public void ParseBlocks_FollowsGameRules()
    {
        string text = "OtherMod\r\n{\n    \"a\": \"x { y } \\\" z\"\n}\r\nMapExtPDX  {\"PatchModeChoice\": \"ModeA\"}\r\n";
        IReadOnlyList<CocBlock> blocks = CocParser.ParseBlocks(text);

        Assert.Equal(["OtherMod", "MapExtPDX"], blocks.Select(b => b.Name));
        Assert.All(blocks, b =>
        {
            Assert.Equal('{', text[b.Open]);
            Assert.Equal('}', text[b.Close]);
        });
        Assert.Empty(CocParser.ParseBlocks("\r\n \t\r\n"));
    }

    [Fact]
    public void ParseEntries_ReturnsTopLevelItemsInFileOrder()
    {
        string text = "MapExtPDX\r\n{\n    \"List\": [1, {\"x\": \"}\"}],\n    \"S\": \"a,b\",\n    \"N\": -1.5e3,\n    \"PatchModeChoice\": \"ModeA\"\n}\r\n";
        CocBlock block = Assert.Single(CocParser.ParseBlocks(text));
        IReadOnlyList<CocEntry> entries = CocParser.ParseEntries(text, block);

        Assert.Equal(["List", "S", "N", "PatchModeChoice"], entries.Select(e => e.Key));
        Assert.Equal(["[1, {\"x\": \"}\"}]", "\"a,b\"", "-1.5e3", "\"ModeA\""], entries.Select(e => e.Raw));
        Assert.All(entries, e => Assert.Equal(e.Raw, text[e.Start..e.End]));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("-Infinity")]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("1E+10")]
    public void ParseEntries_AcceptsJsonAndGameLiterals(string literal)
    {
        StartupSettingsState state = StartupSettingsState.Parse($"MapExtPDX\r\n{{\n    \"X\": {literal}\n}}\r\n");
        Assert.Equal(literal, Assert.Single(state.Entries).Raw);
    }

    [Theory]
    [InlineData("01")]
    [InlineData("True")]
    [InlineData("1.")]
    [InlineData("nan")]
    public void ParseEntries_RejectsOtherLiterals(string literal)
    {
        CocFormatException ex = Assert.Throws<CocFormatException>(() =>
            StartupSettingsState.Parse($"MapExtPDX\r\n{{\n    \"X\": {literal}\n}}\r\n"));
        Assert.Equal(CocErrorKind.BadLiteral, ex.Kind);
        Assert.Equal(literal, ex.Detail);
    }
}
