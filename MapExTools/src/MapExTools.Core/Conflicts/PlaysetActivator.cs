using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapExTools.Core.Game;
using MapExTools.Core.Paths;

namespace MapExTools.Core.Conflicts;

/// <summary>切换当前激活播放集的结果状态。</summary>
public enum PlaysetActivationStatus
{
    /// <summary>已成功切换激活播放集（在 <see cref="PlaysetActivator.Check"/> 中表示预检通过、可以写入）。</summary>
    Activated,

    /// <summary>目标播放集已经是当前激活项且本地保留状态正常，未写入文件。</summary>
    AlreadyActive,

    /// <summary>游戏正在运行，拒绝写入。</summary>
    GameRunning,

    /// <summary><c>playset_config_lock.json</c> 处于 5 分钟有效锁定期内或被外部程序独占占用。</summary>
    ConfigLocked,

    /// <summary><c>playset_config.json</c> 不存在。</summary>
    ConfigMissing,

    /// <summary><c>playset_config.json</c> 非合法 UTF-8 或 JSON 结构损坏。</summary>
    ConfigMalformed,

    /// <summary><c>playset_config.json</c> 的 <c>configVersion</c> 不是受支持的版本 1。</summary>
    UnsupportedConfigVersion,

    /// <summary>目标播放集在 <c>playset_config.json</c> 中不存在（或仅大小写不同的歧义项）。</summary>
    PlaysetNotFound,

    /// <summary>目标播放集 ID 不符合 PDX SDK 有效 ID 规则。</summary>
    InvalidPlaysetId,

    /// <summary>备份、写盘、原子替换或写后回读校验失败。</summary>
    WriteFailed,
}

/// <summary><see cref="PlaysetActivator"/> 预检或执行激活切换的结构化结果。</summary>
public sealed record PlaysetActivationResult(
    PlaysetActivationStatus Status,
    string? TargetPlaysetId = null,
    string? TargetPlaysetName = null,
    string? LockHolder = null,
    string? LockUpdatedAt = null,
    string? BackupPath = null,
    string? ErrorMessage = null)
{
    public bool IsSuccess => Status is PlaysetActivationStatus.Activated or PlaysetActivationStatus.AlreadyActive;
}

/// <summary>
/// 对齐 PDX SDK 2.5.0 规则的本地激活播放集切换器：
/// <list type="bullet">
/// <item>仅在游戏未运行且 <c>playset_config_lock.json</c> 未被其他程序锁定时允许写入；</item>
/// <item>仅改根级 <c>activePlaysetId</c>（写入目标项 <c>id</c> 原文）与目标项 <c>isOffdisk</c>（<c>true</c> 改 <c>false</c>），严禁改动 <c>modifiedAt</c>、<c>mods[]</c> 或 <c>playset_sync.json</c>；</item>
/// <item>按原文件 BOM 状态与 PDX SDK 紧凑无转义格式输出，写前复制 <c>playset_config.json.bak</c>，写 <c>.tmp</c> 回读校验后再原子替换并二次回读核对。</item>
/// </list>
/// </summary>
public static class PlaysetActivator
{
    public const int SupportedConfigVersion = 1;

    /// <summary>游戏本体在 <c>PdxSdkPlatform.Initialize</c> 中注册的锁持有者名。</summary>
    public const string GameLockHolderName = "Cities: Skylines II";

    /// <summary>PDX SDK <c>ModsLocalConfigurationService.LockTimeout</c>（5 分钟）。</summary>
    public static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(5);

    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions s_compactJsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>对齐 PDX SDK <c>PlaysetAndModExtensions.IsValidId</c>：非空白，且若可解析为整数则必须大于 0。</summary>
    public static bool IsValidSdkId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
        if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
            return numeric > 0;
        return true;
    }

    /// <summary>
    /// 只读预检（护栏 ①–③）：检查游戏进程、写锁、<c>configVersion</c> 与目标项有效性，不写盘。
    /// <para>供 CLI 在写 <c>.coc</c> 前统一预检使用；严禁在 WPF UI 线程 <c>RefreshView()</c> 中同步调用。</para>
    /// </summary>
    public static PlaysetActivationResult Check(
        UserDataPaths paths,
        string? targetPlaysetId,
        Func<bool>? isGameRunning = null,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return ValidateOnly(paths, targetPlaysetId, isGameRunning ?? GameProcess.IsRunning, nowUtc ?? DateTimeOffset.UtcNow).Result;
    }

    /// <summary>在游戏关闭且无有效外部写锁时，原子切换 <c>playset_config.json</c> 的激活播放集。</summary>
    public static PlaysetActivationResult Activate(
        UserDataPaths paths,
        string? targetPlaysetId,
        Func<bool>? isGameRunning = null,
        DateTimeOffset? nowUtc = null) =>
        ActivateCore(paths, targetPlaysetId, isGameRunning ?? GameProcess.IsRunning, nowUtc ?? DateTimeOffset.UtcNow, replaceFile: null);

    internal static PlaysetActivationResult ActivateCore(
        UserDataPaths paths,
        string? targetPlaysetId,
        Func<bool> isGameRunning,
        DateTimeOffset nowUtc,
        Action<string, string>? replaceFile)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(isGameRunning);

        ValidatedPlan validated = ValidateOnly(paths, targetPlaysetId, isGameRunning, nowUtc);
        if (validated.Result.Status != PlaysetActivationStatus.Activated || validated.Root == null || validated.TargetNode == null || validated.ExactTargetId == null)
            return validated.Result;

        string serialized;
        try
        {
            // 护栏 ④：最小写入——只改根级 activePlaysetId（目标项 id 原文）与目标项 isOffdisk（true -> false）
            validated.Root["activePlaysetId"] = validated.ExactTargetId;
            if (validated.TargetNode["isOffdisk"] is JsonValue offdiskVal &&
                offdiskVal.TryGetValue(out bool isOffdisk) &&
                isOffdisk)
            {
                validated.TargetNode["isOffdisk"] = false;
            }

            serialized = validated.Root.ToJsonString(s_compactJsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return new PlaysetActivationResult(
                PlaysetActivationStatus.ConfigMalformed,
                validated.ExactTargetId,
                validated.TargetName,
                ErrorMessage: ex.Message);
        }

        // 落盘前复查游戏进程，堵住读取与写盘之间的启动竞态
        if (isGameRunning())
            return new PlaysetActivationResult(PlaysetActivationStatus.GameRunning, validated.ExactTargetId, validated.TargetName);

        // 护栏 ⑤：先留 .bak，再写 .tmp 并读回校验，最后原子替换并从磁盘二次核对
        string configPath = paths.PlaysetConfigFile;
        string backupPath = configPath + ".bak";
        string tempPath = configPath + ".tmp";
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: validated.HasBom);

        try
        {
            File.Copy(configPath, backupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PlaysetActivationResult(
                PlaysetActivationStatus.WriteFailed,
                validated.ExactTargetId,
                validated.TargetName,
                ErrorMessage: ex.Message);
        }

        try
        {
            File.WriteAllText(tempPath, serialized, encoding);
            VerifyConfigFileOnDisk(tempPath, validated.ExactTargetId, validated.PlaysetCount);

            if (replaceFile != null)
                replaceFile(tempPath, configPath);
            else
                File.Move(tempPath, configPath, overwrite: true);

            VerifyConfigFileOnDisk(configPath, validated.ExactTargetId, validated.PlaysetCount);
            return new PlaysetActivationResult(
                PlaysetActivationStatus.Activated,
                validated.ExactTargetId,
                validated.TargetName,
                BackupPath: backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new PlaysetActivationResult(
                PlaysetActivationStatus.WriteFailed,
                validated.ExactTargetId,
                validated.TargetName,
                BackupPath: backupPath,
                ErrorMessage: ex.Message);
        }
        finally
        {
            TryDeleteQuietly(tempPath);
        }
    }

    private sealed record ValidatedPlan(
        PlaysetActivationResult Result,
        JsonObject? Root = null,
        JsonObject? TargetNode = null,
        string? ExactTargetId = null,
        string? TargetName = null,
        bool HasBom = false,
        int PlaysetCount = 0);

    private static ValidatedPlan ValidateOnly(
        UserDataPaths paths,
        string? targetPlaysetId,
        Func<bool> isGameRunning,
        DateTimeOffset nowUtc)
    {
        // 护栏 ①：游戏进程在运行
        if (isGameRunning())
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.GameRunning, targetPlaysetId));

        string? requested = string.IsNullOrWhiteSpace(targetPlaysetId) ? null : targetPlaysetId.Trim();
        if (requested == null || !IsValidSdkId(requested))
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.InvalidPlaysetId, targetPlaysetId));

        // 护栏 ②：写锁判定（对齐 PDX SDK 2.5.0）
        if (CheckLock(paths.PlaysetConfigLockFile, nowUtc) is { } locked)
            return new ValidatedPlan(locked);

        // 护栏 ③：读取、UTF-8 校验、configVersion == 1 与目标项定位
        string configPath = paths.PlaysetConfigFile;
        if (!File.Exists(configPath))
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.ConfigMissing, requested));

        byte[] rawBytes;
        try
        {
            using Stream fs = PlaysetModScanner.OpenSharedRead(configPath);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            rawBytes = ms.ToArray();
        }
        catch (FileNotFoundException)
        {
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.ConfigMissing, requested));
        }
        catch (DirectoryNotFoundException)
        {
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.ConfigMissing, requested));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.WriteFailed, requested, ErrorMessage: ex.Message));
        }

        bool hasBom = rawBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        int offset = hasBom ? Encoding.UTF8.Preamble.Length : 0;
        string text;
        try
        {
            text = s_strictUtf8.GetString(rawBytes, offset, rawBytes.Length - offset);
        }
        catch (DecoderFallbackException ex)
        {
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.ConfigMalformed, requested, ErrorMessage: ex.Message));
        }

        try
        {
            JsonNode? parsedNode = JsonNode.Parse(text);
            if (parsedNode is not JsonObject root)
            {
                return new ValidatedPlan(new PlaysetActivationResult(
                    PlaysetActivationStatus.ConfigMalformed,
                    requested,
                    ErrorMessage: "playset_config.json root is not a JSON object."));
            }

            if (!TryGetIntegerProperty(root, "configVersion", out int configVersion) || configVersion != SupportedConfigVersion)
            {
                return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.UnsupportedConfigVersion, requested));
            }

            if (root["playsets"] is not JsonArray playsets)
            {
                return new ValidatedPlan(new PlaysetActivationResult(
                    PlaysetActivationStatus.ConfigMalformed,
                    requested,
                    ErrorMessage: "playset_config.json is missing a valid playsets array."));
            }

            // 先按区分大小写精确匹配，找不到再按不区分大小写唯一匹配
            JsonObject? exactMatch = null;
            string? exactMatchId = null;
            var ciMatches = new List<(JsonObject Node, string RawId)>();

            foreach (JsonNode? item in playsets)
            {
                if (item is not JsonObject psObj || !TryGetStringProperty(psObj, "id", out string? rawId) || string.IsNullOrEmpty(rawId))
                    continue;

                if (string.Equals(rawId, requested, StringComparison.Ordinal))
                {
                    exactMatch = psObj;
                    exactMatchId = rawId;
                    break;
                }

                if (string.Equals(rawId, requested, StringComparison.OrdinalIgnoreCase))
                {
                    ciMatches.Add((psObj, rawId));
                }
            }

            JsonObject? targetNode = exactMatch ?? (ciMatches.Count == 1 ? ciMatches[0].Node : null);
            string? exactId = exactMatchId ?? (ciMatches.Count == 1 ? ciMatches[0].RawId : null);

            if (targetNode == null || exactId == null)
                return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.PlaysetNotFound, requested));

            if (!IsValidSdkId(exactId))
                return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.InvalidPlaysetId, exactId));

            string targetName = ExtractPlaysetName(targetNode, exactId);

            // 目标已是激活项（区分大小写一致）且 isOffdisk 不为 true 时不写盘
            bool isAlreadyActive = TryGetStringProperty(root, "activePlaysetId", out string? currentActive) &&
                                   string.Equals(currentActive, exactId, StringComparison.Ordinal);
            bool isTargetOffdisk = targetNode["isOffdisk"] is JsonValue offVal &&
                                   offVal.TryGetValue(out bool offBool) &&
                                   offBool;
            if (isAlreadyActive && !isTargetOffdisk)
            {
                return new ValidatedPlan(new PlaysetActivationResult(
                    PlaysetActivationStatus.AlreadyActive,
                    exactId,
                    targetName));
            }

            return new ValidatedPlan(
                new PlaysetActivationResult(PlaysetActivationStatus.Activated, exactId, targetName),
                root,
                targetNode,
                exactId,
                targetName,
                hasBom,
                playsets.Count);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return new ValidatedPlan(new PlaysetActivationResult(PlaysetActivationStatus.ConfigMalformed, requested, ErrorMessage: ex.Message));
        }
    }

    private static PlaysetActivationResult? CheckLock(string lockPath, DateTimeOffset nowUtc)
    {
        if (!File.Exists(lockPath))
            return null;

        byte[] lockBytes;
        try
        {
            using Stream fs = PlaysetModScanner.OpenSharedRead(lockPath);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            lockBytes = ms.ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 锁文件正被外部程序独占占用，保守按被锁拒绝
            return new PlaysetActivationResult(
                PlaysetActivationStatus.ConfigLocked,
                LockHolder: null,
                LockUpdatedAt: null,
                ErrorMessage: ex.Message);
        }

        try
        {
            ReadOnlyMemory<byte> jsonBytes = lockBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)
                ? lockBytes.AsMemory(Encoding.UTF8.Preamble.Length)
                : lockBytes;
            using JsonDocument doc = JsonDocument.Parse(jsonBytes);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            // 对齐 SDK ModConfigurationMapper.ParseConfigLock：configVersion <= 0 或缺失视为无效锁（未锁）
            if (!root.TryGetProperty("configVersion", out JsonElement verEl) ||
                verEl.ValueKind != JsonValueKind.Number ||
                !verEl.TryGetInt32(out int lockVer) ||
                lockVer <= 0)
            {
                return null;
            }

            string? lockHolder = root.TryGetProperty("lockHolder", out JsonElement holderEl) && holderEl.ValueKind == JsonValueKind.String
                ? holderEl.GetString()
                : null;

            // 护栏 ① 已确认游戏未在运行；持有者为游戏本体（崩溃残留锁）按 SDK "自己的锁" 放行
            if (string.Equals(lockHolder, GameLockHolderName, StringComparison.Ordinal))
                return null;

            string? updatedRaw = root.TryGetProperty("updatedAt", out JsonElement updEl) && updEl.ValueKind == JsonValueKind.String
                ? updEl.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(updatedRaw) ||
                !DateTimeOffset.TryParse(updatedRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset updatedAt))
            {
                return null;
            }

            // 距今不足 5 分钟（含 updatedAt 晚于当前时间的负差值）视为锁定中
            TimeSpan elapsed = nowUtc - updatedAt.ToUniversalTime();
            if (elapsed < LockTimeout)
            {
                return new PlaysetActivationResult(
                    PlaysetActivationStatus.ConfigLocked,
                    LockHolder: string.IsNullOrWhiteSpace(lockHolder) ? null : lockHolder.Trim(),
                    LockUpdatedAt: updatedRaw.Trim());
            }

            return null;
        }
        catch (JsonException)
        {
            // 锁文件损坏无法解析按 SDK 行为视为未锁
            return null;
        }
    }

    private static void VerifyConfigFileOnDisk(string path, string expectedActiveId, int expectedPlaysetCount)
    {
        using Stream fs = PlaysetModScanner.OpenSharedRead(path);
        using JsonDocument doc = JsonDocument.Parse(fs);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Written playset_config.json root is not a JSON object.");

        string? actualActive = root.TryGetProperty("activePlaysetId", out JsonElement activeEl) && activeEl.ValueKind == JsonValueKind.String
            ? activeEl.GetString()
            : null;
        if (!string.Equals(actualActive, expectedActiveId, StringComparison.Ordinal))
            throw new InvalidDataException($"Verification failed: activePlaysetId expected '{expectedActiveId}', got '{actualActive}'.");

        if (!root.TryGetProperty("playsets", out JsonElement playsetsEl) ||
            playsetsEl.ValueKind != JsonValueKind.Array ||
            playsetsEl.GetArrayLength() != expectedPlaysetCount)
        {
            throw new InvalidDataException("Verification failed: playsets array count mismatch.");
        }
    }

    private static string ExtractPlaysetName(JsonObject playsetNode, string fallbackId)
    {
        if (playsetNode["presentation"] is JsonObject pres &&
            TryGetStringProperty(pres, "name", out string? name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        return fallbackId;
    }

    private static bool TryGetStringProperty(JsonObject obj, string propName, out string? value)
    {
        value = null;
        if (!obj.TryGetPropertyValue(propName, out JsonNode? node) || node is not JsonValue jv)
            return false;

        if (jv.TryGetValue(out string? s))
        {
            value = s;
            return true;
        }

        if (jv.TryGetValue(out long n))
        {
            value = n.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static bool TryGetIntegerProperty(JsonObject obj, string propName, out int value)
    {
        value = 0;
        if (!obj.TryGetPropertyValue(propName, out JsonNode? node) || node is not JsonValue jv)
            return false;

        return jv.TryGetValue(out value);
    }

    private static void TryDeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 清理临时文件失败不掩盖主错误
        }
    }
}
