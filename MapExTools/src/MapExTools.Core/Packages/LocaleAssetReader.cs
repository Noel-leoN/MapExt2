using System.Text;

namespace MapExTools.Core.Packages;

/// <summary>一个 <c>.loc</c> 文件：语言 id（如 <c>zh-HANS</c>）与文案键值。</summary>
public sealed record LocaleAssetData(string LocaleId, IReadOnlyDictionary<string, string> Entries);

/// <summary>
/// 读游戏的 <c>.loc</c> 本地化资产（<c>LocaleAsset.Save</c> 用 <c>BinaryWriter</c> 写出）：
/// 版本 u16、系统语言名、语言 id、语言自称，再是 int32 条数与成对的键、值字符串，最后是索引计数（这里不读）。
/// 字符串是 <c>BinaryWriter</c> 的格式：7 位变长长度前缀加 UTF-8 字节。
/// </summary>
public static class LocaleAssetReader
{
    /// <summary>条数的上限，超过就当作文件损坏，免得按坏掉的长度分配内存。</summary>
    private const int MaxEntries = 1_000_000;

    /// <summary>解析 .loc 的内容；格式不对时返回 null。</summary>
    public static LocaleAssetData? TryRead(ReadOnlySpan<byte> data)
    {
        try
        {
            using var stream = new MemoryStream(data.ToArray(), writable: false);
            using var reader = new BinaryReader(stream, new UTF8Encoding(false, throwOnInvalidBytes: false));
            reader.ReadUInt16();
            reader.ReadString(); // 系统语言名，如 ChineseSimplified
            string localeId = reader.ReadString();
            reader.ReadString(); // 语言自称

            int count = reader.ReadInt32();
            if (count < 0 || count > MaxEntries)
                return null;

            var entries = new Dictionary<string, string>(Math.Min(count, 1024), StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                entries[key] = reader.ReadString(); // 与游戏一致：重复的键以后一个为准
            }

            return new LocaleAssetData(localeId, entries);
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException)
        {
            return null;
        }
    }
}
