using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapExTools.Core.Config;

/// <summary>读取配置的结果。</summary>
public enum ToolConfigLoadStatus
{
    /// <summary>文件不存在，使用默认值。</summary>
    Missing,

    /// <summary>读取成功。</summary>
    Loaded,

    /// <summary>文件损坏，已复制一份到 <see cref="ToolConfigLoadResult.BackupPath"/>，使用默认值。</summary>
    Corrupt,
}

public sealed record ToolConfigLoadResult(
    ToolConfig Config,
    ToolConfigLoadStatus Status,
    string? BackupPath = null,
    string? Error = null);

/// <summary>
/// 读写 <c>MapExTools.json</c>。写入先写临时文件再整体替换，写到一半中断也不会留下半截文件。
/// </summary>
public sealed class ToolConfigService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ToolConfigService(string configPath)
    {
        ConfigPath = Path.GetFullPath(configPath);
    }

    public string ConfigPath { get; }

    /// <summary>损坏文件的备份路径（<c>MapExTools.json.bad</c>）。</summary>
    public string CorruptBackupPath => ConfigPath + ".bad";

    public ToolConfigLoadResult Load()
    {
        if (!File.Exists(ConfigPath))
            return new ToolConfigLoadResult(new ToolConfig(), ToolConfigLoadStatus.Missing);

        try
        {
            string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            ToolConfig config = JsonSerializer.Deserialize<ToolConfig>(json, s_jsonOptions)
                                ?? throw new JsonException("配置文件内容为 null");
            return new ToolConfigLoadResult(config, ToolConfigLoadStatus.Loaded);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // 保留原文件的副本：下次保存会覆盖原文件，副本留给用户自行找回
            string? backup = null;
            try
            {
                File.Copy(ConfigPath, CorruptBackupPath, overwrite: true);
                backup = CorruptBackupPath;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return new ToolConfigLoadResult(new ToolConfig(), ToolConfigLoadStatus.Corrupt, backup, ex.Message);
        }
    }

    public void Save(ToolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.SchemaVersion = ToolConfig.CurrentSchemaVersion;

        string dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);

        string temp = Path.Combine(dir, $".{Path.GetFileName(ConfigPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(config, s_jsonOptions), new UTF8Encoding(false));
            File.Move(temp, ConfigPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
