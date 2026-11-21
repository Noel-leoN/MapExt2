using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapExTools.Core.Launch;

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

    /// <summary>读取失败（如被占用或无权限），未改动磁盘文件，本次会话使用默认值且不自动回写。</summary>
    ReadFailed,
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
        Converters = { new StrictStringEnumConverterFactory() },
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
                                ?? throw new JsonException("Configuration JSON is null.");

            // 手改成 "launch": null 时反序列化会给 null，按默认值处理
            config.Launch ??= new LaunchOptions();

            if (!Enum.IsDefined(config.Theme))
                throw new JsonException($"Undefined enum value for {nameof(ThemePreference)}: {config.Theme}.");
            if (!Enum.IsDefined(config.Launch.Channel))
                throw new JsonException($"Undefined enum value for {nameof(LaunchChannel)}: {config.Launch.Channel}.");

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ToolConfigLoadResult(new ToolConfig(), ToolConfigLoadStatus.ReadFailed, null, ex.Message);
        }
    }

    public void Save(ToolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.SchemaVersion = Math.Max(config.SchemaVersion, ToolConfig.CurrentSchemaVersion);

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

    /// <summary>
    /// 严格字符串枚举转换器：只接受已定义的单个枚举成员名称（不区分大小写），
    /// 拒绝整数、数字字符串以及带逗号的按位组合（例如 <c>"Direct, Steam"</c> 在 <c>Enum.TryParse</c> 中会按位或成 <c>Steam</c>）。
    /// </summary>
    private sealed class StrictStringEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
            (JsonConverter)Activator.CreateInstance(typeof(StrictStringEnumConverter<>).MakeGenericType(typeToConvert))!;

        private sealed class StrictStringEnumConverter<TEnum> : JsonConverter<TEnum>
            where TEnum : struct, Enum
        {
            private static readonly Dictionary<string, TEnum> s_byName = BuildLookup();

            private static Dictionary<string, TEnum> BuildLookup()
            {
                var map = new Dictionary<string, TEnum>(StringComparer.OrdinalIgnoreCase);
                foreach (TEnum value in Enum.GetValues<TEnum>())
                {
                    string name = value.ToString();
                    map[name] = value;
                }

                return map;
            }

            public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException($"Enum {typeof(TEnum).Name} must be a string name.");

                string? raw = reader.GetString();
                if (raw == null || !s_byName.TryGetValue(raw, out TEnum value))
                    throw new JsonException($"Unknown enum value '{raw}' for {typeof(TEnum).Name}.");

                return value;
            }

            public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
                writer.WriteStringValue(value.ToString());
        }
    }
}
