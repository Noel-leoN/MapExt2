using MapExTools.Core.Launch;

namespace MapExTools.Tests.Launch;

public class VdfReaderTests
{
    // 现行格式，照本机 2026-11-14 的 libraryfolders.vdf 排版（制表符缩进、LF），值换成假的
    private const string LibraryFolders =
        "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\Games\\\\Steam\"\n\t\t\"label\"\t\t\"\"\n"
        + "\t\t\"contentid\"\t\t\"123\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"228980\"\t\t\"1\"\n\t\t\t\"949230\"\t\t\"2\"\n\t\t}\n\t}\n"
        + "\t\"1\"\n\t{\n\t\t\"path\"\t\t\"E:\\\\SteamLibrary\"\n\t\t\"apps\"\n\t\t{\n\t\t}\n\t}\n}\n";

    private const string Manifest =
        "\"AppState\"\n{\n\t\"appid\"\t\t\"949230\"\n\t\"name\"\t\t\"Cities: Skylines II\"\n\t\"StateFlags\"\t\t\"4\"\n"
        + "\t\"installdir\"\t\t\"Cities Skylines II\"\n\t\"InstalledDepots\"\n\t{\n\t\t\"949231\"\n\t\t{\n\t\t\t\"manifest\"\t\t\"1\"\n\t\t}\n\t}\n}\n";

    [Fact]
    public void ParseLibraryFolders_CurrentFormat_UnescapesPaths()
    {
        Assert.Equal([@"D:\Games\Steam", @"E:\SteamLibrary"], GameLocator.ParseLibraryFolders(LibraryFolders));
    }

    [Fact]
    public void ParseLibraryFolders_OldFormat_SkipsNonNumericKeys()
    {
        const string old = "\"LibraryFolders\"\r\n{\r\n\t\"TimeNextStatsReport\"\t\t\"1700000000\"\r\n"
                           + "\t\"ContentStatsID\"\t\t\"-123\"\r\n\t\"1\"\t\t\"E:\\\\SteamLibrary\"\r\n}\r\n";
        Assert.Equal([@"E:\SteamLibrary"], GameLocator.ParseLibraryFolders(old));
    }

    [Fact]
    public void ParseLibraryFolders_NoRoot_ReturnsEmpty()
    {
        Assert.Empty(GameLocator.ParseLibraryFolders("\"other\" { \"0\" { \"path\" \"C:\\\\x\" } }"));
    }

    [Fact]
    public void ParseInstallDir_ReadsAppState()
    {
        Assert.Equal("Cities Skylines II", GameLocator.ParseInstallDir(Manifest));
    }

    [Theory]
    [InlineData("\"AppState\" { \"appid\" \"949230\" }")]
    [InlineData("\"AppState\" { \"installdir\" ")]
    [InlineData("")]
    public void ParseInstallDir_MissingOrMalformed_ReturnsNull(string text)
    {
        Assert.Null(GameLocator.ParseInstallDir(text));
    }

    [Fact]
    public void Parse_KeyLookupIgnoresCase()
    {
        VdfNode root = VdfReader.Parse(Manifest);
        Assert.Equal("949230", root["APPSTATE"]?["AppID"]?.Value);
        Assert.True(root["AppState"]!.IsObject);
        Assert.Null(root["AppState"]!["missing"]);
    }

    [Fact]
    public void Parse_Escapes()
    {
        VdfNode root = VdfReader.Parse("\"k\" \"a\\\\b\\\"c\\nd\\te\\qf\"");
        Assert.Equal("a\\b\"c\nd\te\\qf", root["k"]?.Value);
    }

    [Fact]
    public void Parse_CommentsConditionalsAndBareTokens()
    {
        const string text = "// 注释\n\"a\" \"1\" [$WIN32]\nb 2 // 行尾注释\n\"c\" { d \"3\" }";
        VdfNode root = VdfReader.Parse(text);
        Assert.Equal(["a", "b", "c"], root.Children.Select(c => c.Key));
        Assert.Equal("1", root["a"]?.Value);
        Assert.Equal("2", root["b"]?.Value);
        Assert.Equal("3", root["c"]?["d"]?.Value);
    }

    [Theory]
    [InlineData("\"a\" { \"b\" \"1\"")]
    [InlineData("\"a\" \"1\" }")]
    [InlineData("\"a\"")]
    [InlineData("\"a\" \"1")]
    [InlineData("{ \"a\" \"1\" }")]
    [InlineData("\"a\" }")]
    public void Parse_Malformed_Throws(string text)
    {
        Assert.Throws<FormatException>(() => VdfReader.Parse(text));
    }

    [Fact]
    public void Parse_TooDeep_Throws()
    {
        string text = string.Concat(Enumerable.Repeat("\"k\" { ", 40)) + string.Concat(Enumerable.Repeat("} ", 40));
        Assert.Throws<FormatException>(() => VdfReader.Parse(text));
    }

    [Theory]
    [InlineData("""{"AppDataPath":"C:/x","GamePath":"D:\\Games\\CS2","GamingPlatform":"Steam"}""", @"D:\Games\CS2")]
    [InlineData("""{ "GamePath": "D:\\Games\\CS2", }""", @"D:\Games\CS2")]
    [InlineData("""{ "GamePath": 1 }""", null)]
    [InlineData("""{ "Other": "x" }""", null)]
    [InlineData("""[ "GamePath" ]""", null)]
    [InlineData("not json", null)]
    public void ParseSkyveGamePath(string json, string? expected)
    {
        Assert.Equal(expected, GameLocator.ParseSkyveGamePath(json));
    }
}
