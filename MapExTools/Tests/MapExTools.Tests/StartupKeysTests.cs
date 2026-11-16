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
        Assert.Equal(StartupKeyKind.Enum, StartupKeys.FindKey("WaterSimQuality")?.Kind);
        Assert.Equal(StartupKeyKind.Int, StartupKeys.FindKey("RentMultiplierResidential")?.Kind);
        Assert.Equal(StartupKeyKind.Float, StartupKeys.FindKey("ShoppingMaxCost")?.Kind);
        Assert.Null(StartupKeys.FindKey("patchmodechoice"));
        Assert.Null(StartupKeys.FindKey("TerrainCullThrottle"));
        Assert.Null(StartupKeys.FindKey("NoDogsPurge"));
    }

    [Fact]
    public void AllKeys_Has65ValidUniqueDefinitions_AndPreservesLeadingNine()
    {
        Assert.Equal(65, StartupKeys.AllKeys.Length);
        Assert.Equal(StartupKeys.Keys, StartupKeys.AllKeys.Take(StartupKeys.Keys.Length));
        Assert.Equal(StartupKeys.AllKeys.Length, StartupKeys.AllKeys.Select(k => k.Name).Distinct(StringComparer.Ordinal).Count());

        foreach (StartupKeyInfo key in StartupKeys.AllKeys)
        {
            switch (key.Kind)
            {
                case StartupKeyKind.Mode:
                case StartupKeyKind.Enum:
                    Assert.NotNull(key.EnumNames);
                    Assert.NotNull(key.SelectableEnumNames);
                    Assert.Contains((string)key.DefaultValue, key.SelectableEnumNames!);
                    Assert.All(key.SelectableEnumNames!, m => Assert.Contains(m, key.EnumNames!));
                    break;
                case StartupKeyKind.Bool:
                    Assert.IsType<bool>(key.DefaultValue);
                    break;
                case StartupKeyKind.Int:
                {
                    int dv = Assert.IsType<int>(key.DefaultValue);
                    Assert.True(key.Step > 0d, key.Name);
                    Assert.InRange(dv, (int)Math.Round(key.Min), (int)Math.Round(key.Max));
                    break;
                }
                case StartupKeyKind.Float:
                {
                    float dv = Assert.IsType<float>(key.DefaultValue);
                    Assert.True(key.Step > 0d, key.Name);
                    Assert.InRange(dv, (float)key.Min - 1e-6f, (float)key.Max + 1e-6f);
                    if (key.Name.EndsWith("MaxCost", StringComparison.Ordinal))
                        Assert.True(key.Min >= 1000d, $"{key.Name} 下限严禁为 0");
                    break;
                }
            }
        }
    }

    [Fact]
    public void AllKeys_MatchPublicPropertiesInModSettingsSource()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "MapExtPDX", "MapExt", "Setting", "ModSettings.cs")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);

        string source = File.ReadAllText(Path.Combine(dir!, "MapExtPDX", "MapExt", "Setting", "ModSettings.cs"));
        foreach (StartupKeyInfo key in StartupKeys.AllKeys)
        {
            Assert.Matches($@"\bpublic\s+\S+\s+{System.Text.RegularExpressions.Regex.Escape(key.Name)}\b", source);
        }
    }
}
