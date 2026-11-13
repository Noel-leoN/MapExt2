using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MapExTools.Core.Packages;

/// <summary>包所在的目录：<c>{UserData}/Saves</c> 或 <c>{UserData}/Maps</c>。</summary>
public enum PackageFolder
{
    Saves,
    Maps,
}

/// <summary>包的种类：按包内元数据判定，不看所在目录（游戏也是按资产类型认的）。</summary>
public enum PackageKind
{
    Save,
    Map,

    /// <summary>读不出元数据，见 <see cref="CokPackageInfo.Problem"/>。</summary>
    Unknown,
}

/// <summary>包读不出来的原因。</summary>
public enum PackageProblem
{
    None,

    /// <summary>打不开：文件被占用、没有权限等。</summary>
    Unreadable,

    /// <summary>不是有效的 ZIP。</summary>
    Corrupt,

    /// <summary>空包：ZIP 里没有任何条目（如 Phase 0.3 发现的 22 字节自动存档）。</summary>
    Empty,

    /// <summary>没有游戏能读的存档或地图元数据（游戏只收不压缩的条目）。</summary>
    NoMetadata,

    /// <summary>元数据不是有效的 JSON 对象。</summary>
    BadMetadata,
}

/// <summary>存档元数据 <c>.SaveGameMetadata</c>（<c>SaveInfo</c>）里工具用到的字段。</summary>
/// <param name="MapName">开城时用的地图名。</param>
/// <param name="PreviewGuid">预览图资产的 guid（小写）；没有预览图时为 null。</param>
public sealed record SaveMetadata(
    string? CityName,
    string? MapName,
    int? Population,
    long? Money,
    bool AutoSave,
    IReadOnlyList<string?>? ModsEnabled,
    string? PreviewGuid)
{
    /// <summary>按 <c>modsEnabled</c> 判定的模式值。</summary>
    public SaveModeTag Mode { get; } = SaveGateRules.ClassifySave(ModsEnabled);
}

/// <summary>地图元数据 <c>.MapMetadata</c>（<c>MapInfo</c>）里工具用到的字段。</summary>
/// <param name="DisplayName">地图名（保存时的文件名），也是标题文案键 <c>Maps.MAP_TITLE[名称]</c> 里的名称。</param>
/// <param name="Titles">包内 <c>.loc</c> 里的地图标题：语言 id → 标题。</param>
/// <param name="Area">分块总面积（m²），按它推断制作模式。</param>
public sealed record MapMetadata(
    string? DisplayName,
    IReadOnlyDictionary<string, string> Titles,
    float? Area,
    string? Theme,
    string? PreviewGuid)
{
    /// <summary>按面积推断的制作模式。</summary>
    public MapModeInference Mode { get; } = SaveGateRules.InferMap(Area);

    /// <summary>按界面语言取标题：该语言 → en-US（游戏保存地图时总会写这一份）→ 任一语言 → 地图名。</summary>
    public string? GetTitle(string language)
    {
        foreach (string candidate in new[] { language, "en-US" })
        {
            foreach ((string localeId, string title) in Titles)
            {
                if (string.Equals(localeId, candidate, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(title))
                    return title;
            }
        }

        string? any = Titles.Values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        return any ?? DisplayName;
    }
}

/// <summary>一个 .cok 包的检视结果。</summary>
/// <param name="Name">文件名去掉扩展名，即游戏里显示的存档名。</param>
/// <param name="ProblemDetail">读取失败时的异常信息；没有时为 null。</param>
/// <param name="MetadataGuid">元数据条目的 guid（取自包内 <c>.cid</c>，小写）；读不到时为 null。</param>
/// <param name="PreviewEntry">预览图条目的名称；没有预览图或找不到对应条目时为 null。</param>
public sealed record CokPackageInfo(
    string Path,
    string Name,
    PackageFolder Folder,
    long Length,
    DateTime LastWriteTime,
    PackageKind Kind,
    PackageProblem Problem,
    string? ProblemDetail,
    string? MetadataGuid,
    SaveMetadata? Save,
    MapMetadata? Map,
    string? PreviewEntry)
{
    /// <summary>元数据 guid 相同的其他包（复制出来的存档）；没有重复时为空。</summary>
    public IReadOnlyList<string> DuplicatePaths { get; init; } = [];

    /// <summary>元数据里引用的预览图 guid。</summary>
    public string? PreviewGuid => Save?.PreviewGuid ?? Map?.PreviewGuid;
}

/// <summary>
/// 只读解析 .cok 包（Phase 0.3 的格式）。包是标准 ZIP，游戏只收不压缩的条目，每个条目旁有 <c>&lt;条目名&gt;.cid</c>，
/// 内容是 32 位十六进制 guid（<c>FileSystemDataSource.PopulateFromPackage</c>，条目名不分大小写）。元数据是带 BOM 的 UTF-8 JSON，
/// 里面引用的资产（预览图、本地化）都记作 guid。
///
/// <para>文件以 <c>FileShare.ReadWrite | FileShare.Delete</c> 只读打开，读完即关，不妨碍游戏保存或删除。</para>
/// </summary>
public static class CokPackageReader
{
    public const string PackageExtension = ".cok";
    public const string SaveMetadataExtension = ".SaveGameMetadata";
    public const string MapMetadataExtension = ".MapMetadata";
    public const string LocaleExtension = ".loc";
    private const string CidExtension = ".cid";

    /// <summary>元数据与 .loc 条目的大小上限，超过就不读（正常的只有几 KB）。</summary>
    private const long MaxSmallEntryBytes = 16L * 1024 * 1024;

    /// <summary>.Texture 条目的大小上限。</summary>
    private const long MaxTextureEntryBytes = TextureAssetDecoder.MaxDataBytes + 1024;

    private static readonly JsonDocumentOptions s_jsonOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>读一个包；格式问题记在返回值里，不抛异常。</summary>
    public static CokPackageInfo Read(string path, PackageFolder folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = System.IO.Path.GetFullPath(path);
        string name = System.IO.Path.GetFileNameWithoutExtension(fullPath);
        var file = new FileInfo(fullPath);
        long length = 0;
        DateTime lastWrite = default;
        try
        {
            length = file.Length;
            lastWrite = file.LastWriteTime;
            using FileStream stream = OpenRead(fullPath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            return ReadArchive(zip, fullPath, name, folder, length, lastWrite);
        }
        catch (Exception ex) when (ex is IOException and not EndOfStreamException or UnauthorizedAccessException)
        {
            return Failed(PackageProblem.Unreadable, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 包是玩家的文件，内容不可信：ZIP 结构损坏时除了 InvalidDataException，还可能抛出参数、越界等异常，
            // 一律记为损坏，不让一个坏包中断扫描
            return Failed(PackageProblem.Corrupt, ex.Message);
        }

        CokPackageInfo Failed(PackageProblem problem, string detail) =>
            new(fullPath, name, folder, length, lastWrite, PackageKind.Unknown, problem, detail, null, null, null, null);
    }

    /// <summary>读包内一个条目的全部字节（预览图按需解码时用）。</summary>
    /// <exception cref="FileNotFoundException">包里没有这个条目。</exception>
    /// <exception cref="InvalidDataException">包损坏，或条目超出大小上限。</exception>
    /// <exception cref="IOException">读文件出错。</exception>
    public static byte[] ReadEntryBytes(string path, string entryName)
    {
        using FileStream stream = OpenRead(path);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                                ?? throw new FileNotFoundException("entry not found: " + entryName, path);
        return ReadAll(entry, MaxTextureEntryBytes);
    }

    /// <summary>
    /// 把元数据 JSON 里不带引号的 <c>NaN</c>、<c>Infinity</c>、<c>-Infinity</c> 换成 <c>null</c>：
    /// 游戏的 JSON 编码器写浮点数直接用 <c>Convert.ToString</c>，遇到这几个值会写出标准 JSON 不认的字面量。字符串里的不动。
    /// </summary>
    public static string SanitizeNonFiniteNumbers(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        StringBuilder? builder = null;
        int copied = 0;
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c == '\\')
                    i++;
                else if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            int length = MatchNonFinite(json, i);
            if (length == 0)
                continue;

            builder ??= new StringBuilder(json.Length);
            builder.Append(json, copied, i - copied).Append("null");
            i += length - 1;
            copied = i + 1;
        }

        return builder == null ? json : builder.Append(json, copied, json.Length - copied).ToString();
    }

    private static int MatchNonFinite(string json, int index)
    {
        foreach (string token in new[] { "-Infinity", "+Infinity", "Infinity", "NaN" })
        {
            if (string.CompareOrdinal(json, index, token, 0, token.Length) != 0)
                continue;

            int end = index + token.Length;
            if (end < json.Length && (char.IsLetterOrDigit(json[end]) || json[end] == '_'))
                return 0;
            return token.Length;
        }

        return 0;
    }

    /// <summary>guid 文本：32 位十六进制、不全为 0（<c>Hash128.TryParse</c>，不去首尾空白）。返回小写；无效时为 null。</summary>
    public static string? NormalizeGuid(string? text)
    {
        if (text is not { Length: 32 } || !text.All(char.IsAsciiHexDigit) || text.All(c => c == '0'))
            return null;
        return text.ToLowerInvariant();
    }

    internal static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static CokPackageInfo ReadArchive(ZipArchive zip, string path, string name, PackageFolder folder, long length, DateTime lastWrite)
    {
        if (zip.Entries.Count == 0)
            return new CokPackageInfo(path, name, folder, length, lastWrite, PackageKind.Unknown, PackageProblem.Empty, null, null, null, null, null);

        // 条目名不分大小写，同名的取第一个；游戏只收不压缩的条目
        var byName = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in zip.Entries)
            byName.TryAdd(entry.FullName, entry);

        var assets = new List<(ZipArchiveEntry Entry, string? Guid)>();
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (entry.FullName.EndsWith(CidExtension, StringComparison.OrdinalIgnoreCase) || !IsReadableByGame(entry)
                || !ReferenceEquals(byName[entry.FullName], entry))
            {
                continue;
            }

            string? guid = byName.TryGetValue(entry.FullName + CidExtension, out ZipArchiveEntry? cid) ? ReadGuid(cid) : null;
            assets.Add((entry, guid));
        }

        (ZipArchiveEntry Entry, string? Guid) meta = assets.FirstOrDefault(a => HasExtension(a.Entry, SaveMetadataExtension));
        PackageKind kind = PackageKind.Save;
        if (meta.Entry == null)
        {
            meta = assets.FirstOrDefault(a => HasExtension(a.Entry, MapMetadataExtension));
            kind = PackageKind.Map;
        }

        if (meta.Entry == null)
            return new CokPackageInfo(path, name, folder, length, lastWrite, PackageKind.Unknown, PackageProblem.NoMetadata, null, null, null, null, null);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(SanitizeNonFiniteNumbers(ReadText(meta.Entry)), s_jsonOptions);
        }
        catch (JsonException ex)
        {
            return new CokPackageInfo(path, name, folder, length, lastWrite, kind, PackageProblem.BadMetadata, ex.Message, meta.Guid, null, null, null);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new CokPackageInfo(path, name, folder, length, lastWrite, kind, PackageProblem.BadMetadata, "not an object", meta.Guid, null, null, null);

            SaveMetadata? save = null;
            MapMetadata? map = null;
            if (kind == PackageKind.Save)
                save = ReadSave(root);
            else
                map = ReadMap(root, assets);

            string? previewGuid = save?.PreviewGuid ?? map?.PreviewGuid;
            string? previewEntry = previewGuid == null ? null : assets.FirstOrDefault(a => a.Guid == previewGuid).Entry?.FullName;
            return new CokPackageInfo(path, name, folder, length, lastWrite, kind, PackageProblem.None, null, meta.Guid, save, map, previewEntry);
        }
    }

    private static SaveMetadata ReadSave(JsonElement root)
    {
        return new SaveMetadata(
            GetString(root, "cityName"),
            GetString(root, "mapName"),
            GetInt(root, "population"),
            GetLong(root, "money"),
            GetBool(root, "autoSave") ?? false,
            GetStringArray(root, "modsEnabled"),
            // 旧版字段名 previewAsset（SaveInfo.preview 的 DecodeAlias）
            NormalizeGuid(GetString(root, "preview") ?? GetString(root, "previewAsset")));
    }

    private static MapMetadata ReadMap(JsonElement root, List<(ZipArchiveEntry Entry, string? Guid)> assets)
    {
        string? displayName = GetString(root, "displayName");
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<string?>? localeGuids = GetStringArray(root, "localeAssets");
        if (displayName != null && localeGuids != null)
        {
            // 标题键与地图编辑器保存时一致：Maps.MAP_TITLE[地图名]（MapPanelSystem.SaveLocalization）
            string titleKey = "Maps.MAP_TITLE[" + displayName + "]";
            foreach (string? raw in localeGuids)
            {
                string? guid = NormalizeGuid(raw);
                ZipArchiveEntry? entry = guid == null ? null : assets.FirstOrDefault(a => a.Guid == guid).Entry;
                if (entry == null || entry.Length > MaxSmallEntryBytes)
                    continue;

                // 标题只是锦上添花：某个 .loc 读不了就跳过，退回地图名
                LocaleAssetData? locale;
                try
                {
                    locale = LocaleAssetReader.TryRead(ReadAll(entry, MaxSmallEntryBytes));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException)
                {
                    continue;
                }

                if (locale != null && locale.Entries.TryGetValue(titleKey, out string? title))
                    titles.TryAdd(locale.LocaleId, title);
            }
        }

        // 预览图优先，其次缩略图
        string? preview = NormalizeGuid(GetString(root, "preview")) ?? NormalizeGuid(GetString(root, "thumbnail"));
        return new MapMetadata(displayName, titles, GetFloat(root, "area"), GetString(root, "theme"), preview);
    }

    private static bool IsReadableByGame(ZipArchiveEntry entry) =>
        entry.CompressionMethod == ZipCompressionMethod.Stored && !entry.IsEncrypted && entry.FullName.Length > 0 && !entry.FullName.EndsWith('/');

    private static bool HasExtension(ZipArchiveEntry entry, string extension) =>
        string.Equals(System.IO.Path.GetExtension(entry.FullName), extension, StringComparison.OrdinalIgnoreCase);

    /// <summary>读 .cid：游戏用 <c>StreamReader.ReadToEnd</c>（按 BOM 识别编码）后整体解析。</summary>
    private static string? ReadGuid(ZipArchiveEntry cid)
    {
        if (cid.Length > 1024)
            return null;
        try
        {
            return NormalizeGuid(ReadText(cid));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return null;
        }
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        byte[] bytes = ReadAll(entry, MaxSmallEntryBytes);
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static byte[] ReadAll(ZipArchiveEntry entry, long maxBytes)
    {
        if (entry.Length > maxBytes)
            throw new InvalidDataException($"entry too large: {entry.FullName} ({entry.Length} bytes)");

        byte[] bytes = new byte[entry.Length];
        using Stream stream = entry.Open();
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static int? GetInt(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;

    private static long? GetLong(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : null;

    private static float? GetFloat(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float number)
            ? number
            : null;

    private static bool? GetBool(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static IReadOnlyList<string?>? GetStringArray(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            return null;
        return value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).ToList();
    }
}
