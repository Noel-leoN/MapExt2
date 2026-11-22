using System.Text;
using System.Text.Json;
using MapExTools.Core.Conflicts;
using MapExTools.Core.Localization;
using MapExTools.Core.Paths;

namespace MapExTools.Tests.Conflicts;

public sealed class PlaysetActivatorTests : IDisposable
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly string _tempRoot;
    private readonly UserDataPaths _paths;

    public PlaysetActivatorTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MapExTools_ActivatorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pathsDir(_tempRoot));
        _paths = new UserDataPaths(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static string _pathsDir(string root) => Path.Combine(root, ".cache", "Mods");

    private void WriteConfig(string json, bool withBom = false)
    {
        Directory.CreateDirectory(_paths.ModsCacheDir);
        File.WriteAllText(_paths.PlaysetConfigFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: withBom));
    }

    private void WriteLock(string json)
    {
        Directory.CreateDirectory(_paths.ModsCacheDir);
        File.WriteAllText(_paths.PlaysetConfigLockFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    [Fact]
    public void Activate_NormalSwitch_UpdatesOnlyActiveIdAndTargetOffdisk_CreatesBak_PreservesFormatAndUnknownFields()
    {
        const string input =
            "{\"configVersion\":1,\"activePlaysetId\":\"ps-Alpha\",\"customRootFlag\":true,\"playsets\":[" +
            "{\"id\":\"ps-Alpha\",\"modifiedAt\":\"2026-10-01T00:00:00Z\",\"isOffdisk\":false,\"presentation\":{\"name\":\"原始方案\"},\"mods\":[{\"source\":\"pdx_mods\",\"sourceId\":\"107939\",\"isEnabled\":true}]}," +
            "{\"id\":\"ps-Beta\",\"modifiedAt\":\"2026-10-02T01:02:03Z\",\"isOffdisk\":true,\"extraNested\":{\"k\":42},\"presentation\":{\"name\":\"中文测试播放集\"},\"mods\":[{\"source\":\"pdx_mods\",\"sourceId\":\"86944\",\"isEnabled\":false}]}," +
            "{\"id\":\"ps-Gamma\",\"modifiedAt\":\"2026-10-03T00:00:00Z\",\"isOffdisk\":true,\"presentation\":{\"name\":\"Other Offdisk\"},\"mods\":[]}" +
            "]}";
        WriteConfig(input, withBom: false);

        // 故意传入不同大小写 "PS-BETA"：必须写回目标项原文 "ps-Beta"
        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "PS-BETA", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, res.Status);
        Assert.True(res.IsSuccess);
        Assert.Equal("ps-Beta", res.TargetPlaysetId);
        Assert.Equal("中文测试播放集", res.TargetPlaysetName);
        Assert.Equal(_paths.PlaysetConfigFile + ".bak", res.BackupPath);
        Assert.True(File.Exists(res.BackupPath!));
        Assert.Equal(input, File.ReadAllText(res.BackupPath!, Encoding.UTF8));

        // 验证无 BOM、紧凑 JSON、中文不转义
        byte[] writtenBytes = File.ReadAllBytes(_paths.PlaysetConfigFile);
        Assert.False(writtenBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "原文件无 BOM 时不应添加 BOM");

        string writtenText = Encoding.UTF8.GetString(writtenBytes);
        Assert.DoesNotContain("\n", writtenText, StringComparison.Ordinal);
        Assert.Contains("中文测试播放集", writtenText, StringComparison.Ordinal);
        Assert.Contains("原始方案", writtenText, StringComparison.Ordinal);

        // 精确结构验证：只改了 activePlaysetId 和 ps-Beta.isOffdisk，其余全部原样保留
        string expected =
            "{\"configVersion\":1,\"activePlaysetId\":\"ps-Beta\",\"customRootFlag\":true,\"playsets\":[" +
            "{\"id\":\"ps-Alpha\",\"modifiedAt\":\"2026-10-01T00:00:00Z\",\"isOffdisk\":false,\"presentation\":{\"name\":\"原始方案\"},\"mods\":[{\"source\":\"pdx_mods\",\"sourceId\":\"107939\",\"isEnabled\":true}]}," +
            "{\"id\":\"ps-Beta\",\"modifiedAt\":\"2026-10-02T01:02:03Z\",\"isOffdisk\":false,\"extraNested\":{\"k\":42},\"presentation\":{\"name\":\"中文测试播放集\"},\"mods\":[{\"source\":\"pdx_mods\",\"sourceId\":\"86944\",\"isEnabled\":false}]}," +
            "{\"id\":\"ps-Gamma\",\"modifiedAt\":\"2026-10-03T00:00:00Z\",\"isOffdisk\":true,\"presentation\":{\"name\":\"Other Offdisk\"},\"mods\":[]}" +
            "]}";
        Assert.Equal(expected, writtenText);
    }

    [Fact]
    public void Activate_PreservesBom_WhenOriginalHasBom()
    {
        const string input =
            "{\"configVersion\":1,\"activePlaysetId\":\"p1\",\"playsets\":[" +
            "{\"id\":\"p1\",\"presentation\":{\"name\":\"One\"},\"mods\":[]}," +
            "{\"id\":\"p2\",\"presentation\":{\"name\":\"Two\"},\"mods\":[]}]}";
        WriteConfig(input, withBom: true);

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, res.Status);
        byte[] writtenBytes = File.ReadAllBytes(_paths.PlaysetConfigFile);
        Assert.True(writtenBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), "原文件带 BOM 时应保留 BOM");
    }

    [Fact]
    public void Activate_AlreadyActiveAndNotOffdisk_DoesNotTouchFileOrCreateBak()
    {
        const string input =
            "{\"configVersion\":1,\"activePlaysetId\":\"p1\",\"playsets\":[" +
            "{\"id\":\"p1\",\"isOffdisk\":false,\"presentation\":{\"name\":\"Active One\"},\"mods\":[]}]}";
        WriteConfig(input);
        DateTime oldWrite = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(_paths.PlaysetConfigFile, oldWrite);

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p1", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.AlreadyActive, res.Status);
        Assert.True(res.IsSuccess);
        Assert.Equal("Active One", res.TargetPlaysetName);
        Assert.False(File.Exists(_paths.PlaysetConfigFile + ".bak"));
        Assert.Equal(oldWrite, File.GetLastWriteTimeUtc(_paths.PlaysetConfigFile));
    }

    [Fact]
    public void Activate_AlreadyActiveButOffdisk_FlipsIsOffdiskToFalse()
    {
        const string input =
            "{\"configVersion\":1,\"activePlaysetId\":\"p1\",\"playsets\":[" +
            "{\"id\":\"p1\",\"isOffdisk\":true,\"presentation\":{\"name\":\"Offdisk Active\"},\"mods\":[]}]}";
        WriteConfig(input);

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p1", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, res.Status);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_paths.PlaysetConfigFile));
        Assert.False(doc.RootElement.GetProperty("playsets")[0].GetProperty("isOffdisk").GetBoolean());
    }

    [Fact]
    public void Activate_GameRunning_InitialAndPreWriteChecksBothBlock()
    {
        const string input =
            "{\"configVersion\":1,\"activePlaysetId\":\"p1\",\"playsets\":[" +
            "{\"id\":\"p1\",\"presentation\":{\"name\":\"One\"},\"mods\":[]}," +
            "{\"id\":\"p2\",\"presentation\":{\"name\":\"Two\"},\"mods\":[]}]}";
        WriteConfig(input);

        // 1. 首次检查游戏在运行
        PlaysetActivationResult first = PlaysetActivator.Activate(_paths, "p2", () => true, s_now);
        Assert.Equal(PlaysetActivationStatus.GameRunning, first.Status);
        Assert.False(File.Exists(_paths.PlaysetConfigFile + ".bak"));
        Assert.Equal(input, File.ReadAllText(_paths.PlaysetConfigFile));

        // 2. 落盘前复查游戏在运行（第 1 次返回 false，第 2 次返回 true）
        int calls = 0;
        PlaysetActivationResult second = PlaysetActivator.Activate(_paths, "p2", () => ++calls >= 2, s_now);
        Assert.Equal(PlaysetActivationStatus.GameRunning, second.Status);
        Assert.Equal(2, calls);
        Assert.False(File.Exists(_paths.PlaysetConfigFile + ".bak"));
        Assert.Equal(input, File.ReadAllText(_paths.PlaysetConfigFile));
    }

    [Fact]
    public void Activate_LockWithinFiveMinutes_BlocksAndReportsHolder()
    {
        WriteSimpleTwoPlaysets();
        WriteLock("""{"configVersion":1,"lockHolder":"ExternalManager","createdAt":"2026-11-22T11:55:00Z","updatedAt":"2026-11-22T11:57:00Z"}""");

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.ConfigLocked, res.Status);
        Assert.Equal("ExternalManager", res.LockHolder);
        Assert.Equal("2026-11-22T11:57:00Z", res.LockUpdatedAt);
    }

    [Fact]
    public void Activate_LockFileWithUtf8Bom_StillBlocksAsConfigLocked()
    {
        WriteSimpleTwoPlaysets();
        File.WriteAllText(
            _paths.PlaysetConfigLockFile,
            """{"configVersion":1,"lockHolder":"BomTool","createdAt":"2026-11-22T11:58:00Z","updatedAt":"2026-11-22T11:59:00Z"}""",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.ConfigLocked, res.Status);
        Assert.Equal("BomTool", res.LockHolder);
    }

    [Fact]
    public void Activate_LockExpired_AllowsActivation()
    {
        WriteSimpleTwoPlaysets();
        // updatedAt 恰好距今 5 分钟（elapsed == LockTimeout），视为已超时放行
        WriteLock("""{"configVersion":1,"lockHolder":"ExternalManager","createdAt":"2026-11-22T11:50:00Z","updatedAt":"2026-11-22T11:55:00Z"}""");

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, res.Status);
    }

    [Fact]
    public void Activate_LockWithFutureUpdatedAt_Blocks()
    {
        WriteSimpleTwoPlaysets();
        WriteLock("""{"configVersion":1,"lockHolder":"ClockAheadTool","createdAt":"2026-11-22T12:00:00Z","updatedAt":"2026-11-22T12:05:00Z"}""");

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.ConfigLocked, res.Status);
        Assert.Equal("ClockAheadTool", res.LockHolder);
    }

    [Fact]
    public void Activate_GameOwnLeftoverLock_AllowsWhenGameIsNotRunning()
    {
        WriteSimpleTwoPlaysets();
        WriteLock("""{"configVersion":1,"lockHolder":"Cities: Skylines II","createdAt":"2026-11-22T11:59:00Z","updatedAt":"2026-11-22T11:59:30Z"}""");

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, res.Status);
    }

    [Theory]
    [InlineData("""{"configVersion":1,"lockHolder":"","updatedAt":"2026-11-22T11:59:00Z"}""")]
    [InlineData("""{"configVersion":1,"updatedAt":"2026-11-22T11:59:00Z"}""")]
    public void Activate_LockWithEmptyOrMissingHolder_BlocksWithFallbackMessage(string lockJson)
    {
        WriteSimpleTwoPlaysets();
        WriteLock(lockJson);

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.ConfigLocked, res.Status);
        Assert.Null(res.LockHolder);
        Assert.Equal("2026-11-22T11:59:00Z", res.LockUpdatedAt);

        string msgZh = ConflictText.FormatActivationResult(res, hasPendingMods: false, UiLanguage.SimplifiedChinese);
        Assert.Contains("未知程序", msgZh, StringComparison.Ordinal);
        Assert.DoesNotContain("被  锁定", msgZh, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"lockHolder":"Other","updatedAt":"2026-11-22T11:59:00Z"}""")]
    [InlineData("""{"configVersion":0,"lockHolder":"Other","updatedAt":"2026-11-22T11:59:00Z"}""")]
    [InlineData("""{"configVersion":1,"lockHolder":"Other","updatedAt":"not-a-timestamp"}""")]
    [InlineData("""not valid json""")]
    public void Activate_InvalidLockMetadata_TreatedAsUnlocked(string lockJson)
    {
        WriteSimpleTwoPlaysets();
        WriteLock(lockJson);

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, res.Status);
    }

    [Fact]
    public void Activate_ExclusivelyLockedLockFile_BlocksAsConfigLocked()
    {
        WriteSimpleTwoPlaysets();
        WriteLock("""{"configVersion":1,"lockHolder":"Other","updatedAt":"2026-11-22T11:59:00Z"}""");

        using var exclusive = new FileStream(_paths.PlaysetConfigLockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.ConfigLocked, res.Status);
        Assert.Null(res.LockUpdatedAt);
        string msgEn = ConflictText.FormatActivationResult(res, hasPendingMods: false, UiLanguage.English);
        Assert.Contains("in use", msgEn, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{"configVersion":2,"activePlaysetId":"p1","playsets":[{"id":"p2","mods":[]}]}""")]
    [InlineData("""{"configVersion":0,"activePlaysetId":"p1","playsets":[{"id":"p2","mods":[]}]}""")]
    [InlineData("""{"activePlaysetId":"p1","playsets":[{"id":"p2","mods":[]}]}""")]
    public void Activate_UnsupportedConfigVersion_RefusesWrite(string configJson)
    {
        WriteConfig(configJson);

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.UnsupportedConfigVersion, res.Status);
        Assert.Equal(configJson, File.ReadAllText(_paths.PlaysetConfigFile));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("-42")]
    public void Activate_InvalidPlaysetId_RefusesWrite(string badId)
    {
        WriteSimpleTwoPlaysets();

        PlaysetActivationResult res = PlaysetActivator.Activate(_paths, badId, () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.InvalidPlaysetId, res.Status);
    }

    [Fact]
    public void Activate_MalformedJsonOrInvalidUtf8_RefusesWrite()
    {
        // 1. 损坏 JSON
        WriteConfig("{ broken json");
        PlaysetActivationResult broken = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);
        Assert.Equal(PlaysetActivationStatus.ConfigMalformed, broken.Status);

        // 2. 非法 UTF-8 字节序列
        File.WriteAllBytes(_paths.PlaysetConfigFile, [0x7B, 0xFF, 0xFE, 0x7D]);
        PlaysetActivationResult badUtf8 = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);
        Assert.Equal(PlaysetActivationStatus.ConfigMalformed, badUtf8.Status);

        // 3. 同一对象重复键名（JsonNode.Parse 抛 ArgumentException）
        WriteConfig("""{"configVersion":1,"configVersion":1,"playsets":[{"id":"p2","mods":[]}]}""");
        PlaysetActivationResult dupKey = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);
        Assert.Equal(PlaysetActivationStatus.ConfigMalformed, dupKey.Status);
    }

    [Fact]
    public void Activate_MissingConfigOrMissingTarget_ReturnsExpectedStatus()
    {
        PlaysetActivationResult missing = PlaysetActivator.Activate(_paths, "p2", () => false, s_now);
        Assert.Equal(PlaysetActivationStatus.ConfigMissing, missing.Status);

        WriteSimpleTwoPlaysets();
        PlaysetActivationResult notFound = PlaysetActivator.Activate(_paths, "p-nonexistent", () => false, s_now);
        Assert.Equal(PlaysetActivationStatus.PlaysetNotFound, notFound.Status);
    }

    [Fact]
    public void Activate_ReplaceFailure_CleansUpTmpAndLeavesOriginalIntact()
    {
        WriteSimpleTwoPlaysets();
        string before = File.ReadAllText(_paths.PlaysetConfigFile);

        PlaysetActivationResult res = PlaysetActivator.ActivateCore(
            _paths,
            "p2",
            () => false,
            s_now,
            replaceFile: (_, _) => throw new IOException("Simulated replace failure"));

        Assert.Equal(PlaysetActivationStatus.WriteFailed, res.Status);
        Assert.Contains("Simulated replace failure", res.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllText(_paths.PlaysetConfigFile));
        Assert.False(File.Exists(_paths.PlaysetConfigFile + ".tmp"), "替换失败后必须清理 .tmp 临时文件");
    }

    [Fact]
    public void Check_DoesNotWriteAnyFiles()
    {
        WriteSimpleTwoPlaysets();
        string before = File.ReadAllText(_paths.PlaysetConfigFile);

        PlaysetActivationResult check = PlaysetActivator.Check(_paths, "p2", () => false, s_now);

        Assert.Equal(PlaysetActivationStatus.Activated, check.Status);
        Assert.Equal(before, File.ReadAllText(_paths.PlaysetConfigFile));
        Assert.False(File.Exists(_paths.PlaysetConfigFile + ".bak"));
        Assert.False(File.Exists(_paths.PlaysetConfigFile + ".tmp"));
    }

    private void WriteSimpleTwoPlaysets()
    {
        const string json =
            "{\"configVersion\":1,\"activePlaysetId\":\"p1\",\"playsets\":[" +
            "{\"id\":\"p1\",\"isOffdisk\":false,\"presentation\":{\"name\":\"Set One\"},\"mods\":[]}," +
            "{\"id\":\"p2\",\"isOffdisk\":false,\"presentation\":{\"name\":\"Set Two\"},\"mods\":[]}]}";
        WriteConfig(json);
    }
}
