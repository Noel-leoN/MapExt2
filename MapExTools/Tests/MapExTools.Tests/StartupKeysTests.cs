using MapExtPDX;

namespace MapExTools.Tests;

/// <summary>共享定义 StartupKeys 的取值核对：改了这里要同步 ModSettings 与 PatchManager。</summary>
public class StartupKeysTests
{
    [Fact]
    public void Keys_AreTheNineManagedKeysWithModDefaults()
    {
        var expected = new (string Name, StartupKeyKind Kind, object Default, bool Eco)[]
        {
            ("PatchModeChoice", StartupKeyKind.Mode, "ModeA", false),
            ("isEnableEconomyFix", StartupKeyKind.Bool, true, false),
            ("EnableDemandEcoSystem", StartupKeyKind.Bool, true, true),
            ("EnableJobSearchEcoSystem", StartupKeyKind.Bool, true, true),
            ("EnableHouseholdPropertyEcoSystem", StartupKeyKind.Bool, true, true),
            ("EnableResourceBuyerEcoSystem", StartupKeyKind.Bool, false, true),
            ("EnableResidentAIEcoSystem", StartupKeyKind.Bool, false, true),
            ("EnableDownstreamAIEcoSystem", StartupKeyKind.Bool, false, true),
            ("DisableLoadGameValidation", StartupKeyKind.Bool, false, false),
        };

        Assert.Equal(expected.Length, StartupKeys.Keys.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            StartupKeyInfo key = StartupKeys.Keys[i];
            Assert.Equal(expected[i].Name, key.Name);
            Assert.Equal(expected[i].Kind, key.Kind);
            Assert.Equal(expected[i].Default, key.DefaultValue);
            Assert.Equal(expected[i].Eco, key.EcoGroup);
        }
    }

    [Fact]
    public void Modes_MatchEnumOrderAndCoreValues()
    {
        Assert.Equal(["ModeA", "ModeB", "ModeC", "ModeD", "None"], StartupKeys.ModeNames);
        Assert.Equal(StartupKeys.ModeNames, StartupKeys.Modes.Select(m => m.Name));
        Assert.Equal([4, 2, 8, 3, 1], StartupKeys.Modes.Select(m => m.CoreValue));
    }

    [Theory]
    [InlineData("ModeA", 4, 57344)]
    [InlineData("ModeB", 2, 28672)]
    [InlineData("ModeC", 8, 114688)]
    [InlineData("ModeD", 3, 43008)]
    [InlineData("None", 1, 14336)]
    public void GetCoreValue_AndMapSize(string mode, int coreValue, int mapSize)
    {
        Assert.Equal(coreValue, StartupKeys.GetCoreValue(mode));
        StartupModeInfo? info = StartupKeys.FindModeByCoreValue(coreValue);
        Assert.NotNull(info);
        Assert.Equal(mode, info.Name);
        Assert.Equal(mapSize, info.MapSize);
    }

    [Theory]
    [InlineData("modea")] // 游戏解析枚举名区分大小写
    [InlineData("ModeE")]
    [InlineData("")]
    public void GetCoreValue_UnknownName_ReturnsZero(string mode)
    {
        Assert.Equal(0, StartupKeys.GetCoreValue(mode));
    }

    [Theory]
    [InlineData(16)] // 已废弃的旧 ModeD
    [InlineData(0)]
    [InlineData(-1)]
    public void IsKnownCoreValue_RejectsRetiredAndSentinelValues(int coreValue)
    {
        Assert.False(StartupKeys.IsKnownCoreValue(coreValue));
    }

    [Fact]
    public void SaveTagConstants_MatchMod()
    {
        Assert.Equal("MapEXT_CoreValue=", StartupKeys.CoreValueTagPrefix);
        Assert.Equal("MapExt", StartupKeys.ModIdentifierPrefix);
        Assert.Equal(4, StartupKeys.LegacySaveCoreValue);
        Assert.Equal(-1, StartupKeys.UntaggedVanillaCoreValue);
        Assert.Equal(14336d * 14336d, StartupKeys.VanillaTileArea);
        Assert.Equal(0.05, StartupKeys.MapAreaRatioTolerance);
    }

    [Fact]
    public void FindKey_ReturnsDefinitionOrNull()
    {
        Assert.Equal(StartupKeyKind.Mode, StartupKeys.FindKey(StartupKeys.ModeKey)?.Kind);
        Assert.Null(StartupKeys.FindKey("patchmodechoice"));
    }
}
