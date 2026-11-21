using MapExTools.Core.Packages;

namespace MapExTools.Tests.Packages;

/// <summary>.Texture 文件头、ZSTD 解压、上下翻转与 RGBA→BGRA（计划 4.1、5.1）。</summary>
public sealed class TextureAssetDecoderTests
{
    [Theory]
    [InlineData(5, 14)]
    [InlineData(4, 14)]
    [InlineData(3, 11)]
    public void ReadHeader_ByVersion(int version, int headerLength)
    {
        byte[] data = CokTestData.Texture(version, 680, 383, 8, CokTestData.RgbaPattern(680, 383));

        TextureHeader header = TextureAssetDecoder.ReadHeader(data);

        Assert.Equal(new TextureHeader(version, 680, 383, 1, 1, 8, null, 2, headerLength), header);
    }

    [Fact]
    public void ReadHeader_Version2_ReadsFormatName()
    {
        byte[] data = CokTestData.Texture(2, 4, 2, 4, CokTestData.RgbaPattern(4, 2));

        TextureHeader header = TextureAssetDecoder.ReadHeader(data);

        Assert.Equal(4, header.Format);
        Assert.Equal("R8G8B8A8_SRGB", header.FormatName);
        Assert.Equal(2 + 2 + 2 + 1 + 1 + "R8G8B8A8_SRGB".Length + 3, header.Length);
    }

    [Fact]
    public void ReadHeader_Truncated_Throws()
    {
        Assert.Throws<InvalidDataException>(() => TextureAssetDecoder.ReadHeader([5, 0, 1]));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(5, false)]
    [InlineData(4, false)]
    [InlineData(3, false)]
    public void Decode_FlipsRowsAndSwapsToBgra(int version, bool compress)
    {
        byte[] data = CokTestData.Texture(version, 4, 3, 8, CokTestData.RgbaPattern(4, 3), compress);

        TextureDecodeResult result = TextureAssetDecoder.Decode(data);

        Assert.Equal(TextureDecodeStatus.Decoded, result.Status);
        Assert.Equal(compress, result.Compressed);
        DecodedImage image = result.Image!;
        Assert.Equal((4, 3), (image.Width, image.Height));
        // 输出第 0 行是存储的最后一行（y=2）；像素 (x=1) 存的是 R=1、G=2、B=3
        Assert.Equal(new byte[] { 3, 2, 1, 255 }, image.Pixels[4..8]);
        // 输出最后一行是存储的第 0 行
        int last = 2 * image.Stride;
        Assert.Equal(new byte[] { 3, 0, 3, 255 }, image.Pixels[(last + 12)..(last + 16)]);
    }

    [Fact]
    public void Decode_BgraFormat_KeepsChannelOrder()
    {
        byte[] data = CokTestData.Texture(5, 2, 1, 59, [10, 20, 30, 40, 50, 60, 70, 80], compress: true);

        DecodedImage image = TextureAssetDecoder.Decode(data).Image!;

        Assert.Equal(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 }, image.Pixels);
    }

    [Fact]
    public void Decode_CompressedMipChain_UsesTopLevel()
    {
        // 4×2 的两层：4×2 与 2×1，整条链一起压缩
        byte[] top = CokTestData.RgbaPattern(4, 2);
        byte[] chain = [.. top, .. new byte[2 * 1 * 4]];
        byte[] data = CokTestData.Texture(5, 4, 2, 8, chain, compress: true, mipCount: 2);

        TextureDecodeResult result = TextureAssetDecoder.Decode(data);

        Assert.Equal(TextureDecodeStatus.Decoded, result.Status);
        Assert.Equal(4 * 2 * 4, result.Image!.Pixels.Length);
    }

    [Fact]
    public void Decode_CompressedSizeMismatch_IsCorrupt()
    {
        // 头里写 2 层，实际只压缩了第一层
        byte[] data = CokTestData.Texture(5, 4, 2, 8, CokTestData.RgbaPattern(4, 2), compress: true, mipCount: 2);

        Assert.Equal(TextureDecodeStatus.Corrupt, TextureAssetDecoder.Decode(data).Status);
    }

    [Fact]
    public void Decode_OnePixel_IsPlaceholder()
    {
        byte[] data = CokTestData.Texture(5, 1, 1, 8, [1, 2, 3, 4], compress: true);

        TextureDecodeResult result = TextureAssetDecoder.Decode(data);

        Assert.Equal(TextureDecodeStatus.Placeholder, result.Status);
        Assert.Null(result.Image);
    }

    [Fact]
    public void Decode_BlockCompressedFormat_IsUnsupported()
    {
        // GraphicsFormat 109 = RGBA_BC7_UNorm：4×4 块 16 字节
        byte[] data = CokTestData.Texture(5, 4, 4, 109, new byte[16], compress: true);

        TextureDecodeResult result = TextureAssetDecoder.Decode(data);

        Assert.Equal(TextureDecodeStatus.UnsupportedFormat, result.Status);
        Assert.Equal(109, result.Header!.Format);
    }

    [Fact]
    public void Decode_TextureArray_IsUnsupportedLayout()
    {
        byte[] data = CokTestData.Texture(5, 2, 2, 8, new byte[2 * 2 * 4 * 3], depth: 3, dimension: 5);

        Assert.Equal(TextureDecodeStatus.UnsupportedLayout, TextureAssetDecoder.Decode(data).Status);
    }

    [Fact]
    public void Decode_ExceedsMaxDataBytes_IsTooLarge()
    {
        byte[] data = CokTestData.Texture(5, 16384, 16384, 8, []);

        TextureDecodeResult result = TextureAssetDecoder.Decode(data);

        Assert.Equal(TextureDecodeStatus.TooLarge, result.Status);
        Assert.Equal(16384, result.Header!.Width);
        Assert.Equal(16384, result.Header.Height);
        Assert.Null(result.Image);
    }

    [Fact]
    public void Decode_TruncatedPixels_IsCorrupt()
    {
        byte[] data = CokTestData.Texture(5, 4, 2, 8, CokTestData.RgbaPattern(4, 2));

        Assert.Equal(TextureDecodeStatus.Corrupt, TextureAssetDecoder.Decode(data[..^1]).Status);
    }

    [Fact]
    public void Decode_CompressedLengthBeyondData_IsCorrupt()
    {
        byte[] data = CokTestData.Texture(5, 4, 2, 8, CokTestData.RgbaPattern(4, 2), compress: true);
        data[14] = 0xFF; // 长度字段改成远超文件的值
        data[15] = 0xFF;

        Assert.Equal(TextureDecodeStatus.Corrupt, TextureAssetDecoder.Decode(data).Status);
    }

    [Fact]
    public void Decode_GarbageZstd_IsCorrupt()
    {
        byte[] data = CokTestData.Texture(5, 4, 2, 8, CokTestData.RgbaPattern(4, 2), compress: true);
        for (int i = 18; i < data.Length; i++)
            data[i] = 0x5A;

        Assert.Equal(TextureDecodeStatus.Corrupt, TextureAssetDecoder.Decode(data).Status);
    }

    [Fact]
    public void Downscale_AveragesBlocks()
    {
        // 4×2 → 2×1（倍数 2）：每 2×2 块取平均
        byte[] pixels =
        [
            0, 0, 0, 0, 4, 4, 4, 4, 100, 0, 0, 0, 100, 0, 0, 0,
            8, 8, 8, 8, 12, 12, 12, 12, 100, 0, 0, 0, 101, 0, 0, 0,
        ];

        DecodedImage small = TextureAssetDecoder.Downscale(new DecodedImage(4, 2, pixels), 2);

        Assert.Equal((2, 1), (small.Width, small.Height));
        Assert.Equal(new byte[] { 6, 6, 6, 6, 100, 0, 0, 0 }, small.Pixels);
    }

    [Fact]
    public void Downscale_SmallEnough_ReturnsSameImage()
    {
        var image = new DecodedImage(4, 2, new byte[32]);

        Assert.Same(image, TextureAssetDecoder.Downscale(image, 4));
    }

    [Fact]
    public void Downscale_PreviewSize_FitsThumbnailWidth()
    {
        var image = new DecodedImage(680, 383, new byte[680 * 383 * 4]);

        DecodedImage small = TextureAssetDecoder.Downscale(image, 200);

        Assert.Equal((170, 95), (small.Width, small.Height));
    }
}
