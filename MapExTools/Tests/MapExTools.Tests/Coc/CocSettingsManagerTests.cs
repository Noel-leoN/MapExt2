using System.Text;
using MapExTools.Core.Coc;
using MapExTools.Core.Localization;
using MapExtPDX;

namespace MapExTools.Tests.Coc;

/// <summary>受管键的取值换算与写入流程：游戏运行中、读取后被改过、编码错误、参数校验。</summary>
public sealed class CocSettingsManagerTests : IDisposable
{
    private const string GameFormatInput = "game-format.input.coc";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string CocPath => Path.Combine(_root, "ModsSettings", "MapExtPDX", "MapExtPDX.coc");

    private CocSettingsManager CreateManager(bool gameRunning = false) => new(CocPath, () => gameRunning);

    private void WriteCoc(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CocPath)!);
        File.WriteAllBytes(CocPath, bytes);
    }

    private void WriteCoc(string text) => WriteCoc(new UTF8Encoding(false).GetBytes(text));

    private static string Block(string body) => "MapExtPDX\r\n{\n" + body + "\n}\r\n";

    // === 读取与取值换算 ===

    [Fact]
    public void Read_MissingFile_UsesDefaults()
    {
        StartupSettingsState state = CreateManager().Read();

        Assert.False(state.Exists);
        Assert.Null(state.Block);
        Assert.Equal(StartupKeys.Keys.Length, state.Settings.Count);
        Assert.All(state.Settings, s =>
        {
            Assert.False(s.InFile);
            Assert.False(s.Invalid);
            Assert.Equal(s.Key.DefaultValue, s.Value);
        });
    }

    [Fact]
    public void Read_GameFormat_DecodesValuesInFileAndDefaultsOtherwise()
    {
        WriteCoc(CocSamples.ReadBytes(GameFormatInput));
        StartupSettingsState state = CreateManager().Read();

        Assert.True(state.Exists);
        Assert.False(state.HasBom);
        Assert.Equal(1, state.BlockCount);
        Assert.Equal("ModeA", state[StartupKeys.ModeKey].Value);
        Assert.Equal(false, state["EnableHouseholdPropertyEcoSystem"].Value);
        Assert.Equal("false", state["EnableHouseholdPropertyEcoSystem"].Raw);
        Assert.True(state["isEnableEconomyFix"].InFile);
        Assert.False(state["EnableDemandEcoSystem"].InFile);
        Assert.Equal(true, state["EnableDemandEcoSystem"].Value);
        Assert.False(state.HasInvalidValues);
        Assert.Equal(10, state.Entries.Count);
    }

    [Theory]
    [InlineData("\"ModeC\"", "ModeC", true)]
    [InlineData("2", "ModeC", true)]               // 游戏的 Enum.Parse 也接受数字，按枚举顺序
    [InlineData("\"2\"", "ModeC", true)]
    [InlineData("\" ModeD \"", "ModeD", true)]     // Enum.Parse 去掉首尾空白
    [InlineData("\"Mode\\u0042\"", "ModeB", true)] // JSON 转义先还原
    [InlineData("\"modec\"", "ModeA", false)]      // 区分大小写，读不懂时游戏取 default(T) 即 ModeA
    [InlineData("7", "ModeA", false)]              // 超出枚举范围：MapExt 按 ModeA 的 CoreValue 处理
    [InlineData("-1", "ModeA", false)]
    [InlineData("true", "ModeA", false)]
    [InlineData("null", "ModeA", false)]
    public void Read_ModeValue_DecodedLikeGame(string raw, string expected, bool valid)
    {
        StartupSettingValue mode = StartupSettingsState.Parse(Block($"    \"PatchModeChoice\": {raw}"))[StartupKeys.ModeKey];

        Assert.True(mode.InFile);
        Assert.Equal(expected, mode.Value);
        Assert.Equal(!valid, mode.Invalid);
        Assert.Equal(raw, mode.Raw);
    }

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("false", false, true)]
    [InlineData("1", false, false)]
    [InlineData("\"true\"", false, false)]
    [InlineData("null", false, false)]
    public void Read_BoolValue_OnlyTrueOrFalse(string raw, bool expected, bool valid)
    {
        // EnableResidentAIEcoSystem 默认为 false，读不懂时落回默认值
        StartupSettingValue value = StartupSettingsState.Parse(Block($"    \"EnableResidentAIEcoSystem\": {raw}"))["EnableResidentAIEcoSystem"];

        Assert.Equal(expected, value.Value);
        Assert.Equal(!valid, value.Invalid);
    }

    [Fact]
    public void Read_DuplicateBlocks_UsesFirstLikeGame()
    {
        StartupSettingsState state = StartupSettingsState.Parse(
            Block("    \"PatchModeChoice\": \"ModeB\"") + Block("    \"PatchModeChoice\": \"ModeC\""));

        Assert.Equal(2, state.BlockCount);
        Assert.Equal("ModeB", state[StartupKeys.ModeKey].Value);
    }

    [Fact]
    public void Read_Bom_IsDetectedAndStripped()
    {
        WriteCoc(CocSamples.ReadBytes("bom.input.coc"));
        StartupSettingsState state = CreateManager().Read();

        Assert.True(state.HasBom);
        Assert.StartsWith("MapExtPDX", state.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_InvalidUtf8_ThrowsWithoutPosition()
    {
        // GBK 编码的"你"不是有效的 UTF-8
        byte[] bytes = [.. Encoding.UTF8.GetBytes("MapExtPDX\r\n{\n    \"S\": \""), 0xC4, 0xE3, .. Encoding.UTF8.GetBytes("\"\n}\r\n")];
        WriteCoc(bytes);

        CocFormatException ex = Assert.Throws<CocFormatException>(() => CreateManager().Read());
        Assert.Equal(CocErrorKind.InvalidEncoding, ex.Kind);
        Assert.Equal(0, ex.Line);
        Assert.Equal("设定文件不是有效的 UTF-8 文本。", ex.GetMessage(UiLanguage.SimplifiedChinese));
        Assert.Equal(bytes, File.ReadAllBytes(CocPath));
    }

    // === 改动列表 ===

    [Fact]
    public void GetChanges_IncludesInvalidValuesAndFollowsKeyOrder()
    {
        StartupSettingsState state = StartupSettingsState.Parse(
            Block("    \"PatchModeChoice\": \"ModeA\",\n    \"isEnableEconomyFix\": 1"));
        var target = new Dictionary<string, object>
        {
            ["EnableDemandEcoSystem"] = false,
            ["PatchModeChoice"] = "ModeA",
            ["isEnableEconomyFix"] = true,
        };

        IReadOnlyList<StartupSettingChange> changes = CocSettingsManager.GetChanges(state, target);

        Assert.Equal(["isEnableEconomyFix", "EnableDemandEcoSystem"], changes.Select(c => c.Key.Name));
        Assert.True(changes[0].WasInvalid);
        Assert.Equal(true, changes[0].NewValue);
        Assert.False(changes[1].WasInvalid);
        Assert.Equal(true, changes[1].OldValue);
    }

    public static TheoryData<string, object> BadTargets() => new()
    {
        { "patchmodechoice", "ModeA" }, // 键名区分大小写
        { "TerrainCullThrottle", true }, // 已隐藏弃用，不是受管的键
        { "NoDogsPurge", true }, // 一次性游戏内操作，不是受管的键
        { "PatchModeChoice", "modeC" },
        { "PatchModeChoice", 2 },
        { "isEnableEconomyFix", "true" },
        { "WaterSimQuality", "Adaptive_EventDriven" }, // 已硬挂起的枚举档位不允许写入
        { "ShoppingMaxCost", 0f }, // 严禁写入 0（静默无限寻路陷阱）
        { "RentMultiplierCommercial", 250 }, // 超出 [0, 200]
        { "ShoppingTrafficReduction", float.NaN },
    };

    [Theory]
    [MemberData(nameof(BadTargets))]
    public void GetChanges_RejectsUnknownKeysAndBadValues(string key, object value)
    {
        Assert.Throws<ArgumentException>(() =>
            CocSettingsManager.GetChanges(StartupSettingsState.Parse(string.Empty, exists: false), new Dictionary<string, object> { [key] = value }));
    }

    [Fact]
    public void GetDefaultValues_MatchesStartupKeys()
    {
        Dictionary<string, object> defaults = CocSettingsManager.GetDefaultValues();
        Assert.Equal(StartupKeys.Keys.Select(k => k.Name), defaults.Keys);
        Assert.All(StartupKeys.Keys, k => Assert.Equal(k.DefaultValue, defaults[k.Name]));
    }

    // === 写入 ===

    [Fact]
    public void Save_WhileGameRunning_DoesNotWrite()
    {
        byte[] input = CocSamples.ReadBytes(GameFormatInput);
        WriteCoc(input);
        CocSettingsManager manager = CreateManager(gameRunning: true);

        CocSaveResult result = manager.Save(manager.Read(), new Dictionary<string, object> { ["PatchModeChoice"] = "ModeB" });

        Assert.Equal(CocSaveStatus.GameRunning, result.Status);
        Assert.Single(result.Changes);
        Assert.Equal(input, File.ReadAllBytes(CocPath));
        Assert.False(File.Exists(manager.BackupPath));
    }

    [Fact]
    public void Save_FileChangedAfterRead_Throws_AndKeepsOtherEdit()
    {
        WriteCoc(CocSamples.ReadBytes(GameFormatInput));
        CocSettingsManager manager = CreateManager();
        StartupSettingsState state = manager.Read();

        // 读取之后，游戏退出时写回或用户手动编辑改了文件
        string edited = Block("    \"PatchModeChoice\": \"ModeC\"");
        WriteCoc(edited);

        CocSaveException ex = Assert.Throws<CocSaveException>(() =>
            manager.Save(state, new Dictionary<string, object> { ["EnableDemandEcoSystem"] = false }));
        Assert.Equal(CocSaveFailure.FileChanged, ex.Failure);
        Assert.Equal("设定文件读取后被其他程序改过，请重新读取后再保存。", ex.GetMessage(UiLanguage.SimplifiedChinese));
        Assert.Equal(edited, File.ReadAllText(CocPath));
        Assert.False(File.Exists(manager.BackupPath));
    }

    [Fact]
    public void Save_NoChangesRelativeToBaseline_ButFileChangedOnDisk_ThrowsFileChanged()
    {
        WriteCoc(CocSamples.ReadBytes(GameFormatInput));
        CocSettingsManager manager = CreateManager();
        StartupSettingsState baseline = manager.Read();

        // 外部已将磁盘模式改为 ModeC；此时提交目标恰好等于旧基准 ModeA，仍须检出外部改动而不静默返 NoChanges
        string external = Block("    \"PatchModeChoice\": \"ModeC\"");
        WriteCoc(external);

        CocSaveException ex = Assert.Throws<CocSaveException>(() =>
            manager.Save(baseline, new Dictionary<string, object> { [StartupKeys.ModeKey] = "ModeA" }));
        Assert.Equal(CocSaveFailure.FileChanged, ex.Failure);
        Assert.Equal(external, File.ReadAllText(CocPath));
    }

    [Fact]
    public void Save_FileDeletedAfterRead_Throws()
    {
        WriteCoc(CocSamples.ReadBytes(GameFormatInput));
        CocSettingsManager manager = CreateManager();
        StartupSettingsState state = manager.Read();
        File.Delete(CocPath);

        CocSaveException ex = Assert.Throws<CocSaveException>(() =>
            manager.Save(state, new Dictionary<string, object> { ["PatchModeChoice"] = "ModeB" }));
        Assert.Equal(CocSaveFailure.FileChanged, ex.Failure);
        Assert.False(File.Exists(CocPath));
    }

    [Fact]
    public void Save_FullValueMapWithOneChange_WritesOnlyThatKey()
    {
        byte[] input = CocSamples.ReadBytes(GameFormatInput);
        WriteCoc(input);
        CocSettingsManager manager = CreateManager();
        StartupSettingsState state = manager.Read();

        // 界面会把 9 个键全部交上来，只有改过的键写进文件，文件里没有的键也不补
        Dictionary<string, object> target = state.GetValues();
        target["PatchModeChoice"] = "ModeB";
        CocSaveResult result = manager.Save(state, target);

        Assert.Equal(CocSaveStatus.Saved, result.Status);
        StartupSettingChange change = Assert.Single(result.Changes);
        Assert.Equal(StartupKeys.ModeKey, change.Key.Name);
        string expected = Encoding.UTF8.GetString(input).Replace("\"PatchModeChoice\": \"ModeA\"", "\"PatchModeChoice\": \"ModeB\"", StringComparison.Ordinal);
        Assert.Equal(expected, File.ReadAllText(CocPath));
    }

    [Fact]
    public void Save_Twice_SecondIsNoChanges()
    {
        WriteCoc(CocSamples.ReadBytes(GameFormatInput));
        CocSettingsManager manager = CreateManager();
        var target = new Dictionary<string, object> { ["PatchModeChoice"] = "ModeD", ["EnableResidentAIEcoSystem"] = true };

        Assert.Equal(CocSaveStatus.Saved, manager.Save(manager.Read(), target).Status);
        byte[] afterFirst = File.ReadAllBytes(CocPath);

        Assert.Equal(CocSaveStatus.NoChanges, manager.Save(manager.Read(), target).Status);
        Assert.Equal(afterFirst, File.ReadAllBytes(CocPath));
    }

    [Fact]
    public void Save_LeftoverTempFile_IsReplacedAndRemoved()
    {
        WriteCoc(CocSamples.ReadBytes(GameFormatInput));
        File.WriteAllText(CocPath + ".tmp", "上次中断留下的临时文件");
        CocSettingsManager manager = CreateManager();

        manager.Save(manager.Read(), new Dictionary<string, object> { ["PatchModeChoice"] = "ModeB" });

        Assert.False(File.Exists(CocPath + ".tmp"));
        Assert.Equal("ModeB", manager.Read()[StartupKeys.ModeKey].Value);
    }

    [Fact]
    public void Save_BadTarget_ThrowsBeforeTouchingFile()
    {
        byte[] input = CocSamples.ReadBytes(GameFormatInput);
        WriteCoc(input);
        CocSettingsManager manager = CreateManager();

        Assert.Throws<ArgumentException>(() =>
            manager.Save(manager.Read(), new Dictionary<string, object> { ["PatchModeChoice"] = "ModeX" }));
        Assert.Equal(input, File.ReadAllBytes(CocPath));
        Assert.False(File.Exists(manager.BackupPath));
    }

    [Fact]
    public void SaveException_VerifyFailed_MessageNamesTheKey()
    {
        var ex = new CocSaveException(CocSaveFailure.VerifyFailed, "PatchModeChoice");
        Assert.Equal("写入结果校验失败：PatchModeChoice", ex.GetMessage(UiLanguage.SimplifiedChinese));
        Assert.Equal("Verification after writing failed: PatchModeChoice", ex.Message);
    }

    [Fact]
    public void ReadAndSave_ExtendedTypes_EnumIntFloatAndClamping()
    {
        StartupSettingsState state = StartupSettingsState.Parse(Block(
            "    \"WaterSimQuality\": \"Adaptive_EventDriven\",\n" +
            "    \"SnowSimFreeze\": 1,\n" +
            "    \"ShoppingMaxCost\": 0,\n" +
            "    \"RentMultiplierCommercial\": 250,\n" +
            "    \"ShoppingTrafficReduction\": 0.0002,\n" +
            "    \"HouseholdResourceDemandMultiplier\": 5.5"));

        // 挂起枚举落回 Vanilla 并标无效；数字序号 1 解码为 Auto
        Assert.Equal("Vanilla_EveryFrame", state["WaterSimQuality"].Value);
        Assert.True(state["WaterSimQuality"].Invalid);
        Assert.Equal("Auto", state["SnowSimFreeze"].Value);
        Assert.False(state["SnowSimFreeze"].Invalid);

        // MaxCost=0 夹紧到 1000f 并标无效；250% 夹紧到 200% 并标无效
        Assert.Equal(1000f, state["ShoppingMaxCost"].Value);
        Assert.True(state["ShoppingMaxCost"].Invalid);
        Assert.Equal(200, state["RentMultiplierCommercial"].Value);
        Assert.True(state["RentMultiplierCommercial"].Invalid);

        Assert.Equal(0.0002f, (float)state["ShoppingTrafficReduction"].Value, 1e-6f);
        Assert.Equal(5.5f, (float)state["HouseholdResourceDemandMultiplier"].Value, 1e-6f);

        // 全量保存后越界与挂起项被修正为合法字面量，二次保存为 NoChanges
        CocSettingsManager manager = CreateManager();
        WriteCoc(state.Text);
        StartupSettingsState disk = manager.Read();
        Dictionary<string, object> all = disk.GetAllValues();
        all["ShoppingTrafficReduction"] = 0.0001f;
        all["RentMultiplierCommercial"] = 85;

        CocSaveResult saved = manager.Save(disk, all);
        Assert.Equal(CocSaveStatus.Saved, saved.Status);

        StartupSettingsState reloaded = manager.Read();
        Assert.False(reloaded.HasAnyInvalidValues);
        Assert.Equal("Vanilla_EveryFrame", reloaded["WaterSimQuality"].Value);
        Assert.Equal(1000f, reloaded["ShoppingMaxCost"].Value);
        Assert.Equal(85, reloaded["RentMultiplierCommercial"].Value);
        Assert.Equal("0.0001", reloaded["ShoppingTrafficReduction"].Raw);
        Assert.Equal(CocSaveStatus.NoChanges, manager.Save(reloaded, reloaded.GetAllValues()).Status);
    }
}
