using MapExTools.Core.Coc;
using MapExTools.Core.Packages;
using MapExtPDX;

namespace MapExTools.Tests.Packages;

/// <summary>存档标签、地图面积推断与闸门关系，照 MapExt 的 <c>SaveEntryGate</c>（计划 4.1、5.1）。</summary>
public sealed class SaveGateRulesTests
{
    private const double TileArea = 14336.0 * 14336.0;

    // === 存档标签（ValidateSave 的四种情况）===

    public static TheoryData<string?[]?, SaveTagKind, int> SaveTagCases() => new()
    {
        { null, SaveTagKind.Vanilla, -1 },
        { [], SaveTagKind.Vanilla, -1 },
        { ["SomeOtherMod", "Anarchy"], SaveTagKind.Vanilla, -1 },
        // Mod 标识前缀不分大小写
        { ["mapext2"], SaveTagKind.Legacy, 4 },
        { [null, "MapExt2"], SaveTagKind.Legacy, 4 },
        // 标签前缀区分大小写
        { ["MapExt2", "mapext_corevalue=8"], SaveTagKind.Legacy, 4 },
        { ["MapExt2", "MapEXT_CoreValue=8"], SaveTagKind.Tagged, 8 },
        // 标签本身就以 MapExt 开头
        { ["MapEXT_CoreValue=2"], SaveTagKind.Tagged, 2 },
        // 取第一个标签；int.TryParse 允许首尾空白
        { ["MapEXT_CoreValue=2", "MapEXT_CoreValue=4"], SaveTagKind.Tagged, 2 },
        { ["MapExt2", "MapEXT_CoreValue= 4 "], SaveTagKind.Tagged, 4 },
        // 已废弃的 CV=16 照样取出，判定时一律不符
        { ["MapExt2", "MapEXT_CoreValue=16"], SaveTagKind.Tagged, 16 },
        { ["MapExt2", "MapEXT_CoreValue=abc"], SaveTagKind.Unparsable, 0 },
        { ["MapExt2", "MapEXT_CoreValue="], SaveTagKind.Unparsable, 0 },
    };

    [Theory]
    [MemberData(nameof(SaveTagCases))]
    public void ClassifySave(string?[]? mods, SaveTagKind kind, int coreValue)
    {
        Assert.Equal(new SaveModeTag(kind, coreValue), SaveGateRules.ClassifySave(mods));
    }

    // === 地图面积推断（InferMapCoreValue）===

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(4.0, 2)]
    [InlineData(9.0, 3)]
    [InlineData(16.0, 4)]
    [InlineData(64.0, 8)]
    [InlineData(16.0 * 1.049, 4)] // 容差内
    [InlineData(16.0 * 0.951, 4)]
    public void InferMap_Trusted(double ratio, int coreValue)
    {
        MapModeInference inference = SaveGateRules.InferMap((float)(ratio * TileArea));

        Assert.True(inference.Trusted);
        Assert.Equal(coreValue, inference.CoreValue);
        Assert.Equal(ratio, inference.Ratio, 1e-5);
    }

    [Theory]
    [InlineData(256.0)] // CV=16 已不是现行模式（Phase 0.3：6 张图比值约 256）
    [InlineData(8.0)] // 黄州28km：开方 2.83 取 3，与 9 差太多
    [InlineData(16.0 * 1.06)] // 超出 5% 容差
    [InlineData(0.0)]
    [InlineData(-4.0)]
    public void InferMap_Untrusted(double ratio)
    {
        Assert.False(SaveGateRules.InferMap((float)(ratio * TileArea)).Trusted);
    }

    [Fact]
    public void InferMap_NoArea_IsUntrustedWithNaNRatio()
    {
        MapModeInference inference = SaveGateRules.InferMap(null);

        Assert.False(inference.Trusted);
        Assert.True(double.IsNaN(inference.Ratio));
        Assert.False(SaveGateRules.InferMap(float.NaN).Trusted);
        Assert.False(SaveGateRules.InferMap(float.PositiveInfinity).Trusted);
    }

    // === 读存档（EvaluateSave）===

    [Theory]
    // 原版存档：None 直接读；57／28 km 可转换，没开转换时先要开启；其余不符
    [InlineData(SaveTagKind.Vanilla, -1, 1, false, false, PackageRelation.Match)]
    [InlineData(SaveTagKind.Vanilla, -1, 4, true, false, PackageRelation.Convertible)]
    [InlineData(SaveTagKind.Vanilla, -1, 4, false, false, PackageRelation.ConvertibleWhenEnabled)]
    [InlineData(SaveTagKind.Vanilla, -1, 2, true, false, PackageRelation.Convertible)]
    [InlineData(SaveTagKind.Vanilla, -1, 8, true, false, PackageRelation.Mismatch)]
    [InlineData(SaveTagKind.Vanilla, -1, 3, true, false, PackageRelation.Mismatch)]
    // 旧版存档只在 57 km 下直接读，不提供转换
    [InlineData(SaveTagKind.Legacy, 4, 4, false, false, PackageRelation.Match)]
    [InlineData(SaveTagKind.Legacy, 4, 2, true, false, PackageRelation.Mismatch)]
    // 带标签
    [InlineData(SaveTagKind.Tagged, 4, 4, false, false, PackageRelation.Match)]
    [InlineData(SaveTagKind.Tagged, 1, 1, false, false, PackageRelation.Match)]
    [InlineData(SaveTagKind.Tagged, 1, 4, true, false, PackageRelation.Convertible)]
    [InlineData(SaveTagKind.Tagged, 1, 2, false, false, PackageRelation.ConvertibleWhenEnabled)]
    [InlineData(SaveTagKind.Tagged, 1, 8, true, false, PackageRelation.Mismatch)]
    // 28→57 只在 ModeA 下
    [InlineData(SaveTagKind.Tagged, 2, 4, true, false, PackageRelation.Convertible)]
    [InlineData(SaveTagKind.Tagged, 2, 4, false, false, PackageRelation.ConvertibleWhenEnabled)]
    [InlineData(SaveTagKind.Tagged, 2, 8, true, false, PackageRelation.Mismatch)]
    [InlineData(SaveTagKind.Tagged, 4, 2, true, false, PackageRelation.Mismatch)]
    [InlineData(SaveTagKind.Tagged, 8, 4, true, false, PackageRelation.Mismatch)]
    [InlineData(SaveTagKind.Tagged, 16, 8, true, false, PackageRelation.Mismatch)]
    [InlineData(SaveTagKind.Unparsable, 0, 4, true, false, PackageRelation.Mismatch)]
    // 禁止读档校验：整段跳过，不拦也不转换
    [InlineData(SaveTagKind.Tagged, 8, 4, false, true, PackageRelation.Unchecked)]
    [InlineData(SaveTagKind.Vanilla, -1, 4, true, true, PackageRelation.Unchecked)]
    [InlineData(SaveTagKind.Tagged, 4, 4, false, true, PackageRelation.Match)]
    [InlineData(SaveTagKind.Vanilla, -1, 1, false, true, PackageRelation.Match)]
    public void EvaluateSave(SaveTagKind kind, int savedCv, int currentCv, bool conversion, bool validationDisabled, PackageRelation expected)
    {
        var settings = new GateSettings("Mode", currentCv, conversion, validationDisabled);

        Assert.Equal(expected, SaveGateRules.EvaluateSave(new SaveModeTag(kind, savedCv), settings));
    }

    // === 开新城（EvaluateNewGame）===

    [Theory]
    [InlineData(16.0, 4, false, false, PackageRelation.Match)]
    [InlineData(1.0, 1, false, false, PackageRelation.Match)]
    // 原版地图在扩展模式：能转换就转换，否则拉伸；不受禁止读档校验影响
    [InlineData(1.0, 4, true, false, PackageRelation.Convertible)]
    [InlineData(1.0, 4, false, false, PackageRelation.Stretched)]
    [InlineData(1.0, 8, true, false, PackageRelation.Stretched)]
    [InlineData(1.0, 8, true, true, PackageRelation.Stretched)]
    // 其余不符被拦下；禁止读档校验时放行
    [InlineData(64.0, 4, true, false, PackageRelation.Mismatch)]
    [InlineData(64.0, 4, false, true, PackageRelation.Unchecked)]
    [InlineData(16.0, 1, false, false, PackageRelation.Mismatch)]
    [InlineData(4.0, 4, true, false, PackageRelation.Mismatch)] // 地图没有 28→57
    // 面积不可信：放行，载入后按地形判
    [InlineData(256.0, 4, false, false, PackageRelation.Unknown)]
    [InlineData(256.0, 4, false, true, PackageRelation.Unknown)]
    public void EvaluateMap(double ratio, int currentCv, bool conversion, bool validationDisabled, PackageRelation expected)
    {
        var settings = new GateSettings("Mode", currentCv, conversion, validationDisabled);

        Assert.Equal(expected, SaveGateRules.EvaluateMap(SaveGateRules.InferMap((float)(ratio * TileArea)), settings));
    }

    // === 切到该模式 ===

    [Theory]
    [InlineData(SaveTagKind.Vanilla, -1, "None")]
    [InlineData(SaveTagKind.Legacy, 4, "ModeA")]
    [InlineData(SaveTagKind.Tagged, 2, "ModeB")]
    [InlineData(SaveTagKind.Tagged, 3, "ModeD")]
    [InlineData(SaveTagKind.Tagged, 16, null)]
    [InlineData(SaveTagKind.Unparsable, 0, null)]
    public void GetTargetMode_Save(SaveTagKind kind, int coreValue, string? expected)
    {
        Assert.Equal(expected, SaveGateRules.GetTargetMode(new SaveModeTag(kind, coreValue))?.Name);
    }

    [Fact]
    public void GetTargetMode_Map_OnlyWhenTrusted()
    {
        Assert.Equal("ModeC", SaveGateRules.GetTargetMode(SaveGateRules.InferMap((float)(64 * TileArea)))?.Name);
        Assert.Null(SaveGateRules.GetTargetMode(SaveGateRules.InferMap((float)(256 * TileArea))));
    }

    // === 设定 ===

    [Fact]
    public void GateSettings_FromCoc_ReadsModeConversionAndValidation()
    {
        StartupSettingsState state = StartupSettingsState.Parse(
            "MapExtPDX\r\n{\n    \"PatchModeChoice\": \"ModeB\",\n    \"EnableVanillaConversion\": true,\n    \"DisableLoadGameValidation\": true\n}\r\n");

        Assert.Equal(new GateSettings("ModeB", 2, true, true), GateSettings.FromCoc(state));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("\"true\"")] // 字符串不是布尔值，游戏取默认值
    [InlineData("1")]
    public void GateSettings_FromCoc_ConversionOnlyForLiteralTrue(string raw)
    {
        StartupSettingsState state = StartupSettingsState.Parse(
            "MapExtPDX\r\n{\n    \"EnableVanillaConversion\": " + raw + "\n}\r\n");

        Assert.False(GateSettings.FromCoc(state).ConversionEnabled);
    }

    [Fact]
    public void GateSettings_FromCoc_MissingFileUsesDefaults()
    {
        GateSettings settings = GateSettings.FromCoc(StartupSettingsState.Parse(string.Empty, exists: false));

        Assert.Equal(new GateSettings("ModeA", 4, false, false), settings);
    }

    [Fact]
    public void ValidationAndConversionKeys_AreManagedBoolKeys()
    {
        StartupKeyInfo? validation = StartupKeys.FindKey(SaveGateRules.ValidationKey);
        StartupKeyInfo? conversion = StartupKeys.FindKey(SaveGateRules.ConversionKey);
        StartupKeyInfo? backdrop = StartupKeys.FindKey(SaveGateRules.BackdropKey);

        Assert.NotNull(validation);
        Assert.Equal(StartupKeyKind.Bool, validation.Kind);
        Assert.Contains(validation, StartupKeys.Keys);

        Assert.NotNull(conversion);
        Assert.Equal(StartupKeyKind.Bool, conversion.Kind);
        Assert.Equal(false, conversion.DefaultValue);
        Assert.DoesNotContain(conversion, StartupKeys.Keys);

        Assert.NotNull(backdrop);
        Assert.Equal(StartupKeyKind.Bool, backdrop.Kind);
        Assert.Equal(false, backdrop.DefaultValue);
    }
}
