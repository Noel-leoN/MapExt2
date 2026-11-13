using System.IO.Compression;
using System.Text;

namespace MapExTools.Tests.Packages;

/// <summary>
/// 按游戏的格式在测试时生成 .cok 包、.Texture 与 .loc（Phase 0.3、<c>ZipPackageWriter</c>、<c>TextureAsset.Save</c>、
/// <c>LocaleAsset.Save</c>），不往仓库里放玩家的存档。
/// </summary>
internal static class CokTestData
{
    public const string SaveGuid = "0123456789abcdef0123456789abcdef";
    public const string PreviewGuid = "fedcba9876543210fedcba9876543210";
    public const string DataGuid = "00112233445566778899aabbccddeeff";

    /// <summary>带 BOM 的 UTF-8，与游戏写元数据一致（<c>StreamWriter(…, Encoding.UTF8)</c>）。</summary>
    public static byte[] Utf8WithBom(string text) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];

    /// <summary>
    /// 按存储顺序（自下而上）生成 RGBA 像素：第 y 行（存储顺序）的每个像素为 (x, y, x+y, 255)，便于核对翻转与换序。
    /// </summary>
    public static byte[] RgbaPattern(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                pixels[i] = (byte)x;
                pixels[i + 1] = (byte)y;
                pixels[i + 2] = (byte)(x + y);
                pixels[i + 3] = 255;
            }
        }

        return pixels;
    }

    /// <summary>
    /// 生成 .Texture。版本 ≥4 的文件头 14 字节；版本 ≥5 再写长度字段：<paramref name="compress"/> 为 true 时为 ZSTD 压缩后的长度，否则为 −1。
    /// </summary>
    public static byte[] Texture(int version, int width, int height, int format, byte[] pixels, bool compress = false,
        int mipCount = 1, int depth = 1, int dimension = 2)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)version);
            writer.Write((ushort)width);
            writer.Write((ushort)height);
            if (version >= 4)
                writer.Write((ushort)depth);
            writer.Write((byte)mipCount);
            if (version >= 3)
                writer.Write((byte)format);
            else
                writer.Write(FormatName(format));
            if (version >= 4)
                writer.Write((byte)dimension);
            if (version >= 2)
            {
                writer.Write((byte)1); // 过滤
                writer.Write((byte)1); // 环绕
                writer.Write((byte)1); // 各向异性
            }

            if (version > 4)
            {
                if (compress)
                {
                    byte[] compressed = Zstd(pixels);
                    writer.Write(compressed.Length);
                    writer.Write(compressed);
                }
                else
                {
                    writer.Write(-1);
                    writer.Write(pixels);
                }
            }
            else
            {
                writer.Write(pixels);
            }
        }

        return stream.ToArray();
    }

    public static byte[] Zstd(byte[] data)
    {
        byte[] buffer = new byte[ZstandardEncoder.GetMaxCompressedLength(data.Length)];
        Assert.True(ZstandardEncoder.TryCompress(data, buffer, out int written));
        return buffer[..written];
    }

    /// <summary>生成 .loc：版本 1、系统语言名、语言 id、语言自称、条目、索引计数（0 条）。</summary>
    public static byte[] Locale(string localeId, IReadOnlyDictionary<string, string> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)1);
            writer.Write("SystemLanguage");
            writer.Write(localeId);
            writer.Write(localeId + " name");
            writer.Write(entries.Count);
            foreach ((string key, string value) in entries)
            {
                writer.Write(key);
                writer.Write(value);
            }

            writer.Write(0);
        }

        return stream.ToArray();
    }

    /// <summary>存档元数据 JSON；<paramref name="modsEnabled"/> 为 null 时写 null。</summary>
    public static string SaveJson(string[]? modsEnabled, string? preview = PreviewGuid, string previewKey = "preview", string extra = "")
    {
        string mods = modsEnabled == null ? "null" : "[" + string.Join(",", modsEnabled.Select(m => "\"" + m + "\"")) + "]";
        string previewValue = preview == null ? "null" : "\"" + preview + "\"";
        return "{\"" + previewKey + "\":" + previewValue + ",\"theme\":\"European\",\"cityName\":\"Oakdale\",\"population\":12345,"
               + "\"money\":987654,\"xp\":10,\"simulationDate\":{\"year\":2031,\"month\":4,\"hour\":8,\"minute\":30},"
               + "\"mapName\":\"AFLAT57km\",\"saveGameData\":\"" + DataGuid + "\",\"modsEnabled\":" + mods + ",\"autoSave\":false" + extra + "}";
    }

    /// <summary>地图元数据 JSON。</summary>
    public static string MapJson(string displayName, string area, string[]? localeGuids = null, string? preview = null)
    {
        string locales = localeGuids == null ? "null" : "[" + string.Join(",", localeGuids.Select(g => "\"" + g + "\"")) + "]";
        string previewValue = preview == null ? "null" : "\"" + preview + "\"";
        return "{\"displayName\":\"" + displayName + "\",\"thumbnail\":null,\"preview\":" + previewValue + ",\"theme\":\"North American\","
               + "\"temperatureRange\":{\"min\":-5.0,\"max\":25.0},\"cloudiness\":NaN,\"area\":" + area + ",\"buildableLand\":1.0E+08,"
               + "\"localeAssets\":" + locales + ",\"isPlayable\":true}";
    }

    /// <summary>guid 写成游戏的 .cid：32 位十六进制，不带换行。</summary>
    public static byte[] Cid(string guid) => Encoding.UTF8.GetBytes(guid);

    private static string FormatName(int format) => format switch
    {
        4 => "R8G8B8A8_SRGB",
        8 => "R8G8B8A8_UNorm",
        57 => "B8G8R8A8_SRGB",
        59 => "B8G8R8A8_UNorm",
        _ => "RGBA_DXT5_UNorm",
    };
}

/// <summary>按游戏的写法拼一个 .cok：每个资产条目不压缩，旁边配 <c>&lt;条目名&gt;.cid</c>。</summary>
internal sealed class CokBuilder
{
    private readonly List<(string Name, byte[] Data, bool Stored)> _entries = [];

    /// <summary>加一个资产条目；<paramref name="guid"/> 不为 null 时同时加 .cid。</summary>
    public CokBuilder Asset(string name, byte[] data, string? guid, bool stored = true)
    {
        _entries.Add((name, data, stored));
        if (guid != null)
            _entries.Add((name + ".cid", CokTestData.Cid(guid), true));
        return this;
    }

    /// <summary>加一个原样的条目（如大小写不同的 .cid）。</summary>
    public CokBuilder Raw(string name, byte[] data, bool stored = true)
    {
        _entries.Add((name, data, stored));
        return this;
    }

    public string Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach ((string name, byte[] data, bool stored) in _entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name, stored ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using Stream entryStream = entry.Open();
                entryStream.Write(data);
            }
        }

        return path;
    }

    /// <summary>一个完整的存档包：元数据、预览图（版本 5、ZSTD，4×2）与存档数据。</summary>
    public static CokBuilder Save(string[]? modsEnabled, string metadataGuid = CokTestData.SaveGuid, bool withPreview = true)
    {
        var builder = new CokBuilder()
            .Asset("City.SaveGameMetadata", CokTestData.Utf8WithBom(CokTestData.SaveJson(modsEnabled, withPreview ? CokTestData.PreviewGuid : null)), metadataGuid)
            .Asset("City.SaveGameData", [1, 2, 3, 4], CokTestData.DataGuid);
        if (withPreview)
            builder.Asset("City.Texture", CokTestData.Texture(5, 4, 2, 8, CokTestData.RgbaPattern(4, 2), compress: true), CokTestData.PreviewGuid);
        return builder;
    }
}
