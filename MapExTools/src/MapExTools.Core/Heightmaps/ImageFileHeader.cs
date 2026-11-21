using System.Buffers.Binary;

namespace MapExTools.Core.Heightmaps;

/// <summary>图片的容器格式，按文件头的签名判定，不看扩展名。</summary>
public enum ImageContainer
{
    Png,
    Tiff,
    Jpeg,
}

/// <summary>
/// 图片文件头：尺寸、通道数与每通道位数。游戏取的也是这几项（<c>ImageAsset.Load</c> 的 <c>FileGetInfo</c>），
/// 所以格式判断看文件里存的，不看解码器输出的像素格式：例如 16 位"灰度＋透明"的 PNG 会被 WIC 解成 RGBA64，
/// 在游戏里却是双通道。
/// </summary>
/// <param name="Channels">通道数：PNG 按颜色类型（调色板记 1），TIFF 取 SamplesPerPixel，JPEG 取分量数。</param>
/// <param name="BitsPerChannel">每通道位数：PNG 取位深，TIFF 取 BitsPerSample，JPEG 取精度。</param>
/// <param name="IsFloat">TIFF 的 SampleFormat 为浮点（3）。</param>
/// <param name="IsSigned">TIFF 的 SampleFormat 为有符号整数（2）。</param>
public sealed record ImageFileHeader(
    ImageContainer Container,
    int Width,
    int Height,
    int Channels,
    int BitsPerChannel,
    bool IsFloat,
    bool IsSigned = false)
{
    private static readonly byte[] s_pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public HeightmapPixelKind Kind => HeightmapRules.Classify(Channels, BitsPerChannel, IsFloat, IsSigned);

    public HeightmapIssues Issues => HeightmapRules.Validate(Width, Height, Kind);

    /// <summary>读文件头。打开方式与游戏日志读取相同（不独占），读完即关。</summary>
    /// <exception cref="IOException">读文件出错。</exception>
    /// <exception cref="UnauthorizedAccessException">没有读权限。</exception>
    public static ImageFileHeader? TryRead(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return TryRead(stream);
    }

    /// <summary>从可定位的流读文件头；认不出格式或文件头损坏时返回 null。</summary>
    public static ImageFileHeader? TryRead(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            Span<byte> head = stackalloc byte[8];
            stream.Position = 0;
            int read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            if (read >= 8 && head.SequenceEqual(s_pngSignature))
                return ReadPng(stream);
            if (read >= 4 && (head[..4].SequenceEqual("II*\0"u8) || head[..4].SequenceEqual("MM\0*"u8)))
                return ReadTiff(stream, littleEndian: head[0] == (byte)'I');
            if (read >= 2 && head[0] == 0xFF && head[1] == 0xD8)
                return ReadJpeg(stream);
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
    }

    /// <summary>PNG：签名后的第一个块必须是 IHDR（宽、高、位深、颜色类型，大端）。</summary>
    private static ImageFileHeader? ReadPng(Stream stream)
    {
        Span<byte> chunk = stackalloc byte[8 + 13];
        stream.ReadExactly(chunk);
        if (BinaryPrimitives.ReadInt32BigEndian(chunk) != 13 || !chunk.Slice(4, 4).SequenceEqual("IHDR"u8))
            return null;

        int width = BinaryPrimitives.ReadInt32BigEndian(chunk[8..]);
        int height = BinaryPrimitives.ReadInt32BigEndian(chunk[12..]);
        int depth = chunk[16];
        byte colorType = chunk[17];
        // PNG 规范：调色板图位深只能是 1/2/4/8，出现 16 位属非法文件头
        if (colorType == 3 && depth is not (1 or 2 or 4 or 8))
            return null;

        int channels = colorType switch
        {
            0 => 1, // 灰度
            2 => 3, // RGB
            3 => 1, // 调色板（位深不超过 8）
            4 => 2, // 灰度＋透明
            6 => 4, // RGBA
            _ => 0,
        };
        return channels == 0 || width <= 0 || height <= 0 ? null : new ImageFileHeader(ImageContainer.Png, width, height, channels, depth, false);
    }

    /// <summary>TIFF：读第一个 IFD 的宽、高、BitsPerSample、SamplesPerPixel、SampleFormat；BigTIFF 不支持。</summary>
    private static ImageFileHeader? ReadTiff(Stream stream, bool littleEndian)
    {
        var reader = new EndianReader(stream, littleEndian);
        stream.Position = 4;
        long ifd = reader.UInt32();
        if (ifd < 8 || ifd >= stream.Length)
            return null;

        stream.Position = ifd;
        int count = reader.UInt16();
        int width = 0, height = 0, channels = 1, bits = 1;
        bool isFloat = false, isSigned = false;
        for (int i = 0; i < count; i++)
        {
            long entry = ifd + 2 + i * 12L;
            stream.Position = entry;
            int tag = reader.UInt16();
            int type = reader.UInt16();
            long valueCount = reader.UInt32();
            switch (tag)
            {
                case 256:
                    width = (int)reader.Value(type);
                    break;
                case 257:
                    height = (int)reader.Value(type);
                    break;
                case 258:
                {
                    // BitsPerSample 必须为 SHORT(3)，且多通道各通道位深须一致
                    if (type != 3 || valueCount is <= 0 or > 16)
                        return null;
                    if (valueCount * 2 > 4)
                    {
                        long offset = reader.UInt32();
                        if (offset < 8 || offset + valueCount * 2 > stream.Length)
                            return null;
                        stream.Position = offset;
                    }

                    int firstBits = reader.UInt16();
                    for (int ch = 1; ch < valueCount; ch++)
                    {
                        if (reader.UInt16() != firstBits)
                            return null;
                    }

                    bits = firstBits;
                    break;
                }
                case 277:
                    channels = (int)reader.Value(type);
                    break;
                case 339:
                {
                    // SampleFormat (SHORT=3)：1=无符号整型，2=有符号整型，3=浮点
                    if (type != 3 || valueCount is <= 0 or > 16)
                        return null;
                    if (valueCount * 2 > 4)
                    {
                        long offset = reader.UInt32();
                        if (offset < 8 || offset + valueCount * 2 > stream.Length)
                            return null;
                        stream.Position = offset;
                    }

                    for (int ch = 0; ch < valueCount; ch++)
                    {
                        int sf = reader.UInt16();
                        if (sf == 3)
                            isFloat = true;
                        else if (sf == 2)
                            isSigned = true;
                    }

                    break;
                }
            }
        }

        return width <= 0 || height <= 0 || channels <= 0
            ? null
            : new ImageFileHeader(ImageContainer.Tiff, width, height, channels, bits, isFloat, isSigned);
    }

    /// <summary>JPEG：找第一个 SOF 段（精度、高、宽、分量数）。</summary>
    private static ImageFileHeader? ReadJpeg(Stream stream)
    {
        stream.Position = 2;
        Span<byte> buffer = stackalloc byte[8];
        while (true)
        {
            int prefix = stream.ReadByte();
            if (prefix < 0)
                return null;
            if (prefix != 0xFF)
                return null;

            int marker;
            do
            {
                marker = stream.ReadByte(); // 跳过填充的 0xFF
            }
            while (marker == 0xFF);

            if (marker < 0 || marker is 0xD9 or 0xDA)
                return null; // 到了 EOI 或扫描数据还没有 SOF
            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
                continue; // 没有长度的标记

            stream.ReadExactly(buffer[..2]);
            int length = BinaryPrimitives.ReadUInt16BigEndian(buffer);
            if (length < 2)
                return null;

            bool isSof = marker is (>= 0xC0 and <= 0xCF) and not (0xC4 or 0xC8 or 0xCC);
            if (isSof)
            {
                // SOF 段长度至少 8 字节（2 长度 + 1 精度 + 2 高 + 2 宽 + 1 分量数）
                if (length < 8)
                    return null;
                stream.ReadExactly(buffer[..6]);
                int precision = buffer[0];
                int height = BinaryPrimitives.ReadUInt16BigEndian(buffer[1..]);
                int width = BinaryPrimitives.ReadUInt16BigEndian(buffer[3..]);
                int components = buffer[5];
                return width <= 0 || height <= 0 ? null : new ImageFileHeader(ImageContainer.Jpeg, width, height, components, precision, false);
            }

            stream.Position += length - 2;
        }
    }

    /// <summary>按 TIFF 的字节序读整数。</summary>
    private sealed class EndianReader(Stream stream, bool littleEndian)
    {
        private readonly byte[] _buffer = new byte[4];

        public int UInt16()
        {
            stream.ReadExactly(_buffer, 0, 2);
            return littleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(_buffer) : BinaryPrimitives.ReadUInt16BigEndian(_buffer);
        }

        public long UInt32()
        {
            stream.ReadExactly(_buffer, 0, 4);
            return littleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(_buffer) : BinaryPrimitives.ReadUInt32BigEndian(_buffer);
        }

        /// <summary>条目里的单个值：类型 3 为 SHORT，4 为 LONG。</summary>
        public long Value(int type) => type == 3 ? UInt16() : UInt32();
    }
}
