using System.Buffers.Binary;

namespace MapExTools.Tests.Heightmaps;

/// <summary>测试用的最小图片文件头：只写解析要用的部分（不算 CRC、没有像素），不入库任何真实图片。</summary>
internal static class HeightmapTestData
{
    /// <summary>PNG 签名加 IHDR 块。</summary>
    public static byte[] Png(int width, int height, int bitDepth, int colorType, int ihdrLength = 13)
    {
        byte[] data = new byte[8 + 8 + 13 + 4];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(data, 0);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(8), ihdrLength);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(20), height);
        data[24] = (byte)bitDepth;
        data[25] = (byte)colorType;
        return data;
    }

    /// <summary>
    /// 只有一个 IFD 的 TIFF：宽（SHORT）、高（LONG）、BitsPerSample（每通道一个值，多于两个时放在偏移处）、
    /// SamplesPerPixel，按需加 SampleFormat。
    /// </summary>
    public static byte[] Tiff(bool littleEndian, int width, int height, int bits, int channels, int? sampleFormat = null)
    {
        var entries = new List<(int Tag, int Type, int Count, byte[] Value)>
        {
            (256, 3, 1, Short(width)),
            (257, 4, 1, Long(height)),
            (258, 3, channels, []),
            (277, 3, 1, Short(channels)),
        };
        if (sampleFormat is int format)
            entries.Add((339, 3, 1, Short(format)));

        int ifdOffset = 8;
        int ifdSize = 2 + entries.Count * 12 + 4;
        int extraOffset = ifdOffset + ifdSize;
        using var stream = new MemoryStream();
        stream.Write(littleEndian ? "II*\0"u8 : "MM\0*"u8);
        stream.Write(Long(ifdOffset));
        stream.Write(Short(entries.Count));
        foreach ((int tag, int type, int count, byte[] value) in entries)
        {
            stream.Write(Short(tag));
            stream.Write(Short(type));
            stream.Write(Long(count));
            byte[] field = new byte[4];
            if (tag == 258)
            {
                // BitsPerSample：两个以内放进字段，否则写偏移
                byte[] values = Enumerable.Range(0, count).SelectMany(_ => Short(bits)).ToArray();
                if (values.Length <= 4)
                    values.CopyTo(field, 0);
                else
                    Long(extraOffset).CopyTo(field, 0);
            }
            else
            {
                value.CopyTo(field, 0);
            }

            stream.Write(field);
        }

        stream.Write(Long(0)); // 没有下一个 IFD
        if (channels > 2)
        {
            foreach (int _ in Enumerable.Range(0, channels))
                stream.Write(Short(bits));
        }

        return stream.ToArray();

        byte[] Short(int value)
        {
            byte[] b = new byte[2];
            if (littleEndian)
                BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)value);
            else
                BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)value);
            return b;
        }

        byte[] Long(int value)
        {
            byte[] b = new byte[4];
            if (littleEndian)
                BinaryPrimitives.WriteUInt32LittleEndian(b, (uint)value);
            else
                BinaryPrimitives.WriteUInt32BigEndian(b, (uint)value);
            return b;
        }
    }

    /// <summary>JPEG：SOI、一个 APP0 段、带填充字节的 SOF0（精度、高、宽、分量数）。</summary>
    public static byte[] Jpeg(int width, int height, int components, int precision = 8)
    {
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8]);
        stream.Write([0xFF, 0xE0, 0x00, 0x10]);
        stream.Write(new byte[14]);
        stream.Write([0xFF, 0xFF, 0xC0]); // 前面多一个填充的 0xFF
        int length = 8 + components * 3;
        stream.Write([(byte)(length >> 8), (byte)length, (byte)precision, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, (byte)components]);
        stream.Write(new byte[components * 3]);
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }
}
