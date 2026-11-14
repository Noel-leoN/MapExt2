using MapExTools.Core.Coc;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Tests.Coc;

/// <summary>启动与设定页的设定文字：每个键与模式都有三语显示名，改动清单的写法（计划 2.3）。</summary>
public sealed class StartupSettingsTextTests
{
    public static TheoryData<string> Languages() => new(UiLanguage.All);

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryKeyAndMode_HasLabel(string language)
    {
        foreach (StartupKeyInfo key in StartupKeys.Keys)
            Assert.NotEqual("Startup.Key." + key.Name, StartupSettingsText.KeyLabel(key, language));
        foreach (string mode in StartupKeys.ModeNames)
            Assert.StartsWith(mode + " ", StartupSettingsText.ModeChoice(mode, language), StringComparison.Ordinal);
    }

    [Fact]
    public void EconomyColumn_HasMasterSwitchAndGroups()
    {
        string[] economy = StartupKeys.Keys.Where(StartupSettingsText.IsEconomyKey).Select(k => k.Name).ToArray();

        Assert.Equal(7, economy.Length);
        Assert.Equal(StartupSettingsText.EconomyMasterKey, economy[0]);
        Assert.DoesNotContain(StartupKeys.ModeKey, economy);
        Assert.DoesNotContain("DisableLoadGameValidation", economy);
    }

    [Fact]
    public void ChangeList_ModeAndSwitches()
    {
        StartupSettingsState state = StartupSettingsState.Parse("MapExtPDX\n{\n    \"PatchModeChoice\": \"ModeA\"\n}\n");
        Dictionary<string, object> target = state.GetValues();
        target[StartupKeys.ModeKey] = "ModeB";
        target["EnableResourceBuyerEcoSystem"] = true;

        IReadOnlyList<StartupSettingChange> changes = CocSettingsManager.GetChanges(state, target);

        Assert.Equal("地图尺寸模式 ModeA 57km→ModeB 28km；消费采购与服务覆盖寻路系统组 关→开",
            StartupSettingsText.ChangeList(changes, UiLanguage.SimplifiedChinese));
        Assert.Equal("Map size mode: ModeA 57km → ModeB 28km; Consumer and Service Pathing Systems: off → on",
            StartupSettingsText.ChangeList(changes, UiLanguage.English));
    }

    [Fact]
    public void ChangeList_InvalidValue_IsFixed()
    {
        StartupSettingsState state = StartupSettingsState.Parse("MapExtPDX\n{\n    \"PatchModeChoice\": \"ModeX\",\n    \"isEnableEconomyFix\": 1\n}\n");

        IReadOnlyList<StartupSettingChange> changes = CocSettingsManager.GetChanges(state, state.GetValues());

        Assert.Equal("地图尺寸模式 修正无效值→ModeA 57km；经济系统修复与性能优化（总开关） 修正无效值→开",
            StartupSettingsText.ChangeList(changes, UiLanguage.SimplifiedChinese));
        Assert.Equal("地图尺寸模式；经济系统修复与性能优化（总开关）", StartupSettingsText.InvalidList(state, UiLanguage.SimplifiedChinese));
    }

    [Fact]
    public void Value_NoneMode_IsMarkedVanilla()
    {
        StartupKeyInfo mode = StartupKeys.FindKey(StartupKeys.ModeKey)!;

        Assert.Equal("None 14km（原版）", StartupSettingsText.Value(mode, "None", UiLanguage.SimplifiedChinese));
        Assert.Equal("ModeC 114km", StartupSettingsText.Value(mode, "ModeC", UiLanguage.TraditionalChinese));
    }

    [Fact]
    public void InvalidList_AllValid_IsNull()
    {
        Assert.Null(StartupSettingsText.InvalidList(StartupSettingsState.Parse(string.Empty, exists: false), UiLanguage.English));
    }
}
