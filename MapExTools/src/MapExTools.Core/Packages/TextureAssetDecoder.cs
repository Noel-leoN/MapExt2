using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace MapExTools.Core.Packages;

/// <summary>
/// <c>.Texture</c> 的文件头（<c>TextureAsset.ReadHeader</c>）。各版本的字段：
/// 版本 u16、宽 u16、高 u16；版本 ≥4 有深度 u16；mip 数 u8；版本 ≥3 像素格式为 u8（<c>GraphicsFormat</c> 的值），
/// 更早的版本是格式名字符串；版本 ≥4 有维度 u8；版本 ≥2 有过滤、环绕、各向异性三个 u8。
/// </summary>
/// <param name="Format"><c>GraphicsFormat</c> 的值；旧版本的格式名不认识时为 −1。</param>
/// <param name="FormatName">版本 ≤2 时文件里的格式名；其余为 null。</param>
/// <param name="Dimension"><c>TextureDimension</c> 的值，2 为 Tex2D；版本 ≤3 固定为 2。</param>
/// <param name="Length">文件头的字节数（不含版本 ≥5 的长度字段）。</param>
public sealed record TextureHeader(
    int Version, int Width, int Height, int Depth, int MipCount, int Format, string? FormatName, int Dimension, int Length);

/// <summary>解码后的图像：从上到下逐行存放，每像素 4 字节，顺序 B、G、R、A。</summary>
public sealed record DecodedImage(int Width, int Height, byte[] Pixels)
{
    public int Stride => Width * 4;
}

/// <summary><see cref="TextureAssetDecoder.Decode"/> 的结论。</summary>
public enum TextureDecodeStatus
{
    /// <summary>已解码。</summary>
    Decoded,

    /// <summary>1×1 的占位预览图（2026-05 至 08 由 MapExt 转换生成的存档）。</summary>
    Placeholder,

    /// <summary>像素格式不支持（如 BC 块压缩），只显示文字信息。</summary>
    UnsupportedFormat,

    /// <summary>不是单层二维贴图。</summary>
    UnsupportedLayout,

    /// <summary>像素数据尺寸超出解码上限。</summary>
    TooLarge,

    /// <summary>文件头或像素数据不完整、解压失败。</summary>
    Corrupt,
}

/// <param name="Compressed">像素数据是否经 ZSTD 压缩（版本 ≥5 且长度字段不为 −1）。</param>
/// <param name="Detail">解码失败的原因（英文，供界面附在说明后面）。</param>
public sealed record TextureDecodeResult(
    TextureDecodeStatus Status, TextureHeader? Header, bool Compressed, DecodedImage? Image, string? Detail);

/// <summary>
/// 解码游戏的 <c>.Texture</c> 预览图（计划 4.1、附录 A）。支持 8 位四通道格式：RGBA（<c>GraphicsFormat</c> 4、8）与
/// BGRA（57、59）。版本 ≥5 时文件头后有 int32 长度字段：−1 表示后面是未压缩像素，其他值是 ZSTD 压缩后的长度
/// （<c>TextureAsset.LoadData</c>），用 .NET 内置的 <see cref="ZstandardDecoder"/> 解压。
/// 游戏存的像素从画面底部一行行往上，输出时上下翻转（Phase 0.3 实测）。
/// </summary>
public static class TextureAssetDecoder
{
    /// <summary>像素数据（含全部 mip）的上限。预览图约 1 MB，超过 256 MB 的不解码。</summary>
    public const long MaxDataBytes = 256L * 1024 * 1024;

    private const int Tex2D = 2;

    /// <summary>旧版本（≤2）文件头里的格式名 → <c>GraphicsFormat</c> 的值，只列支持的几种。</summary>
    private static readonly Dictionary<string, int> s_formatNames = new(StringComparer.Ordinal)
    {
        ["R8G8B8A8_SRGB"] = 4,
        ["R8G8B8A8_UNorm"] = 8,
        ["B8G8R8A8_SRGB"] = 57,
        ["B8G8R8A8_UNorm"] = 59,
    };

    /// <summary>读文件头。</summary>
    /// <exception cref="InvalidDataException">数据比文件头还短，或格式名字符串损坏。</exception>
    public static TextureHeader ReadHeader(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data);
        int version = reader.ReadUInt16();
        int width = reader.ReadUInt16();
        int height = reader.ReadUInt16();
        int depth = version >= 4 ? reader.ReadUInt16() : 1;
        int mipCount = reader.ReadByte();

        int format;
        string? formatName = null;
        if (version >= 3)
        {
            format = reader.ReadByte();
        }
        else
        {
            formatName = reader.ReadString();
            format = s_formatNames.TryGetValue(formatName, out int known) ? known : -1;
        }

        int dimension = version >= 4 ? reader.ReadByte() : Tex2D;
        if (version >= 2)
            reader.Skip(3); // 过滤、环绕、各向异性

        return new TextureHeader(version, width, height, depth, mipCount, format, formatName, dimension, reader.Position);
    }

    /// <summary>解码整个 .Texture 文件的第一层 mip。不抛异常，失败原因见返回值。</summary>
    public static TextureDecodeResult Decode(ReadOnlySpan<byte> data)
    {
        TextureHeader header;
        try
        {
            header = ReadHeader(data);
        }
        catch (InvalidDataException ex)
        {
            return new TextureDecodeResult(TextureDecodeStatus.Corrupt, null, false, null, ex.Message);
        }

        int offset = header.Length;
        int compressedLength = -1;
        if (header.Version > 4)
        {
            if (data.Length < offset + 4)
                return Fail(TextureDecodeStatus.Corrupt, header, false, "missing length field");
            compressedLength = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            offset += 4;
        }

        bool compressed = compressedLength != -1;
        if (header.Width == 0 || header.Height == 0)
            return Fail(TextureDecodeStatus.Corrupt, header, compressed, "zero size");
        if (header.Width == 1 && header.Height == 1)
            return new TextureDecodeResult(TextureDecodeStatus.Placeholder, header, compressed, null, null);
        if (!IsSupportedFormat(header.Format))
            return Fail(TextureDecodeStatus.UnsupportedFormat, header, compressed, null);
        if (header.Dimension != Tex2D || header.Depth != 1)
            return Fail(TextureDecodeStatus.UnsupportedLayout, header, compressed, null);

        long levelBytes = (long)header.Width * header.Height * 4;
        long chainBytes = ComputeMipChainBytes(header.Width, header.Height, Math.Max(header.MipCount, 1));
        if (chainBytes > MaxDataBytes)
            return Fail(TextureDecodeStatus.TooLarge, header, compressed, "too large");

        ReadOnlySpan<byte> pixels;
        byte[]? buffer = null;
        if (compressed)
        {
            if (compressedLength < 0 || compressedLength > data.Length - offset)
                return Fail(TextureDecodeStatus.Corrupt, header, true, "bad compressed length");

            // 游戏把整条 mip 链一起压缩：一次解压到整条链的大小，写入的长度必须正好相等
            buffer = new byte[chainBytes];
            if (!ZstandardDecoder.TryDecompress(data.Slice(offset, compressedLength), buffer, out int written) || written != chainBytes)
                return Fail(TextureDecodeStatus.Corrupt, header, true, "ZSTD decompression failed");
            pixels = buffer;
        }
        else
        {
            if (data.Length - offset < levelBytes)
                return Fail(TextureDecodeStatus.Corrupt, header, false, "pixel data truncated");
            pixels = data.Slice(offset, (int)levelBytes);
        }

        bool bgra = header.Format is 57 or 59;
        var image = new DecodedImage(header.Width, header.Height, FlipToBgra(pixels[..(int)levelBytes], header.Width, header.Height, bgra));
        return new TextureDecodeResult(TextureDecodeStatus.Decoded, header, compressed, image, null);
    }

    /// <summary>
    /// 按整数倍缩小，使宽不超过 <paramref name="maxWidth"/>（每块取平均）。用于列表缩略图，原图不变；不需要缩小时返回原图。
    /// </summary>
    public static DecodedImage Downscale(DecodedImage image, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxWidth, 1);

        int factor = (image.Width + maxWidth - 1) / maxWidth;
        if (factor <= 1)
            return image;

        int width = Math.Max(image.Width / factor, 1);
        int height = Math.Max(image.Height / factor, 1);
        int blockW = Math.Min(factor, image.Width);
        int blockH = Math.Min(factor, image.Height);
        int area = blockW * blockH;
        byte[] source = image.Pixels;
        byte[] result = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0, a = 0;
                for (int dy = 0; dy < blockH; dy++)
                {
                    int row = (y * factor + dy) * image.Stride + x * factor * 4;
                    for (int dx = 0; dx < blockW; dx++)
                    {
                        int i = row + dx * 4;
                        b += source[i];
                        g += source[i + 1];
                        r += source[i + 2];
                        a += source[i + 3];
                    }
                }

                int o = (y * width + x) * 4;
                result[o] = (byte)((b + area / 2) / area);
                result[o + 1] = (byte)((g + area / 2) / area);
                result[o + 2] = (byte)((r + area / 2) / area);
                result[o + 3] = (byte)((a + area / 2) / area);
            }
        }

        return new DecodedImage(width, height, result);
    }

    /// <summary>支持的像素格式：8 位四通道的 RGBA（4 sRGB、8 UNorm）与 BGRA（57 sRGB、59 UNorm）。</summary>
    public static bool IsSupportedFormat(int format) => format is 4 or 8 or 57 or 59;

    /// <summary>
    /// 整条 mip 链的字节数（每像素 4 字节），与 <c>TextureUtils.ComputeMipchainSize</c> 一致：每层边长减半，最小为 1。
    /// 层数与游戏一样不超过完整 mip 链的层数（<c>ComputeMipchainLevels</c>）。
    /// </summary>
    internal static long ComputeMipChainBytes(int width, int height, int mipCount)
    {
        int levels = Math.Min(mipCount, BitOperations.Log2((uint)Math.Max(width, height)) + 1);
        long total = 0;
        for (int i = 0; i < levels; i++)
            total += (long)Math.Max(width >> i, 1) * Math.Max(height >> i, 1) * 4;
        return total;
    }

    /// <summary>上下翻转，并把 RGBA 换成 BGRA。</summary>
    private static byte[] FlipToBgra(ReadOnlySpan<byte> source, int width, int height, bool sourceIsBgra)
    {
        int stride = width * 4;
        byte[] result = new byte[source.Length];
        for (int y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> src = source.Slice((height - 1 - y) * stride, stride);
            Span<byte> dst = result.AsSpan(y * stride, stride);
            if (sourceIsBgra)
            {
                src.CopyTo(dst);
                continue;
            }

            for (int i = 0; i < stride; i += 4)
            {
                dst[i] = src[i + 2];
                dst[i + 1] = src[i + 1];
                dst[i + 2] = src[i];
                dst[i + 3] = src[i + 3];
            }
        }

        return result;
    }

    private static TextureDecodeResult Fail(TextureDecodeStatus status, TextureHeader header, bool compressed, string? detail) =>
        new(status, header, compressed, null, detail);

    /// <summary>按小端读文件头；数据不够时抛 <see cref="InvalidDataException"/>。</summary>
    private ref struct SpanReader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;

        public int Position { get; private set; }

        public int ReadByte() => Take(1)[0];

        public int ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

        public void Skip(int count) => Take(count);

        /// <summary><c>BinaryWriter.Write(string)</c> 的格式：7 位变长长度前缀加 UTF-8 字节。</summary>
        public string ReadString()
        {
            int length = 0;
            for (int shift = 0; ; shift += 7)
            {
                if (shift > 28)
                    throw new InvalidDataException("bad string length");
                int b = ReadByte();
                length |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    break;
            }

            if (length < 0)
                throw new InvalidDataException("bad string length");
            return Encoding.UTF8.GetString(Take(length));
        }

        private ReadOnlySpan<byte> Take(int count)
        {
            if (count > _data.Length - Position)
                throw new InvalidDataException("texture header truncated");
            ReadOnlySpan<byte> slice = _data.Slice(Position, count);
            Position += count;
            return slice;
        }
    }
}
