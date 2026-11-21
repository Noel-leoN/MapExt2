using MapExTools.Core.Heightmaps;

namespace MapExTools.Tests.Heightmaps;

/// <summary>PNG、TIFF、JPEG 的文件头：尺寸、通道数、位深，按签名而不是扩展名判定（计划 4.2）。</summary>
public sealed class ImageFileHeaderTests
{
    private static ImageFileHeader? Read(byte[] data) => ImageFileHeader.TryRead(new MemoryStream(data));

    [Theory]
    [InlineData(0, 16, 1, HeightmapPixelKind.Gray16)]
    [InlineData(6, 16, 4, HeightmapPixelKind.Rgba16)]
    [InlineData(4, 16, 2, HeightmapPixelKind.Other16)] // 灰度＋透明：WIC 会解成 RGBA64，游戏里却是双通道
    [InlineData(2, 16, 3, HeightmapPixelKind.Other16)]
    [InlineData(0, 8, 1, HeightmapPixelKind.EightBit)]
    [InlineData(3, 8, 1, HeightmapPixelKind.EightBit)] // 调色板
    [InlineData(2, 8, 3, HeightmapPixelKind.EightBit)]
    public void Png_ColorTypeAndDepth(int colorType, int depth, int channels, HeightmapPixelKind kind)
    {
        ImageFileHeader? header = Read(HeightmapTestData.Png(4096, 4096, depth, colorType));

        Assert.NotNull(header);
        Assert.Equal(new ImageFileHeader(ImageContainer.Png, 4096, 4096, channels, depth, false), header);
        Assert.Equal(kind, header.Kind);
    }

    [Fact]
    public void Png_BadIhdr_IsNull()
    {
        Assert.Null(Read(HeightmapTestData.Png(4096, 4096, 16, 0, ihdrLength: 12)));
        Assert.Null(Read(HeightmapTestData.Png(4096, 4096, 16, 5)));
        Assert.Null(Read(HeightmapTestData.Png(4096, 4096, 16, 3))); // 调色板不支持 16 位
        Assert.Null(Read(HeightmapTestData.Png(4096, 4096, 16, 0)[..20]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tiff_Gray16_BothByteOrders(bool littleEndian)
    {
        ImageFileHeader? header = Read(HeightmapTestData.Tiff(littleEndian, 4096, 4096, 16, 1));

        Assert.Equal(new ImageFileHeader(ImageContainer.Tiff, 4096, 4096, 1, 16, false), header);
        Assert.Equal(HeightmapIssues.None, header!.Issues);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tiff_BitsPerSampleAtOffset(bool littleEndian)
    {
        // 4 个通道时 BitsPerSample 放不进条目，写在偏移处
        ImageFileHeader? header = Read(HeightmapTestData.Tiff(littleEndian, 4096, 4096, 16, 4));

        Assert.Equal(HeightmapPixelKind.Rgba16, header!.Kind);
        Assert.Equal(16, header.BitsPerChannel);
    }

    [Fact]
    public void Tiff_FloatSampleFormat_IsUnsupported()
    {
        ImageFileHeader? header = Read(HeightmapTestData.Tiff(true, 4096, 4096, 32, 1, sampleFormat: 3));

        Assert.True(header!.IsFloat);
        Assert.False(header.IsSigned);
        Assert.Equal(HeightmapPixelKind.Unsupported, header.Kind);
    }

    [Fact]
    public void Tiff_SignedSampleFormat_IsMarkedSignedAndUnsupported()
    {
        ImageFileHeader? header = Read(HeightmapTestData.Tiff(true, 4096, 4096, 16, 1, sampleFormat: 2));

        Assert.NotNull(header);
        Assert.True(header.IsSigned);
        Assert.False(header.IsFloat);
        Assert.Equal(HeightmapPixelKind.Unsupported, header.Kind);
        Assert.Equal(HeightmapIssues.WrongFormat, header.Issues);
    }

    [Fact]
    public void Jpeg_SofShorterThan8_IsNull()
    {
        // SOI + SOF0 marker + length=6（小于最小 8 字节）
        byte[] badSof = [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x06, 8, 0x10, 0x00, 0x10, 0x00, 3];
        Assert.Null(Read(badSof));
    }

    [Fact]
    public void Tiff_BigTiffAndBrokenOffset_AreNull()
    {
        Assert.Null(Read([.. "II+\0"u8, 8, 0, 0, 0]));
        byte[] broken = HeightmapTestData.Tiff(true, 4096, 4096, 16, 1);
        broken[4] = 0xFF; // IFD 偏移指到文件外
        Assert.Null(Read(broken));
    }

    [Fact]
    public void Jpeg_ReadsSofAfterOtherSegmentsAndFillBytes()
    {
        ImageFileHeader? header = Read(HeightmapTestData.Jpeg(4096, 4096, 3));

        Assert.Equal(new ImageFileHeader(ImageContainer.Jpeg, 4096, 4096, 3, 8, false), header);
        Assert.Equal(HeightmapIssues.WrongFormat, header!.Issues);
    }

    [Fact]
    public void UnknownOrEmpty_IsNull()
    {
        Assert.Null(Read([]));
        Assert.Null(Read("GIF89a"u8.ToArray()));
        Assert.Null(Read([0xFF, 0xD8, 0xFF, 0xD9])); // 没有 SOF 的 JPEG
    }

    [Fact]
    public void TryRead_Path_DetectsBySignatureNotExtension()
    {
        string path = Path.Combine(Path.GetTempPath(), "MapExTools.Tests", Guid.NewGuid().ToString("N") + ".jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            File.WriteAllBytes(path, HeightmapTestData.Png(4096, 4096, 16, 0));

            Assert.Equal(ImageContainer.Png, ImageFileHeader.TryRead(path)!.Container);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
