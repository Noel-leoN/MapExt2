using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapExTools.Core.Heightmaps;

namespace MapExTools.Views;

/// <summary>解码后的高度图：游戏导入时用的那个通道（灰度，或 RGBA 的 R），从上到下逐行存放。</summary>
internal sealed record DecodedHeightmap(int Width, int Height, ushort[] Samples, HeightmapStats Stats);

/// <summary>
/// 用 WIC 解码高度图（计划 §2 第 2 条：不走 GDI+，它会降成 8 位）。<c>PreservePixelFormat</c> 保留文件里的像素格式，
/// 16 位值原样取出：4.2 动工前用本机 4 张 4096² 的 16 位灰度 PNG（含 2 张带 sRGB／gAMA 块的）与不经 WIC 的手写解码逐像素比对，
/// 完全相同；16 位灰度 PNG、TIFF 编码再解码也无损。
/// </summary>
internal static class HeightmapDecoder
{
    /// <summary>解码一张图；比 4096×4096 大时返回 null（只显示文件头信息）。</summary>
    /// <exception cref="NotSupportedException">浮点等无法按 16 位显示的格式。</exception>
    /// <exception cref="IOException">读文件出错。</exception>
    /// <exception cref="FileFormatException">不是能解码的图片。</exception>
    public static DecodedHeightmap? Decode(string path)
    {
        // 不缓存整图（None）：像素在流打开期间一次复制出来，省掉解码器自己那份 4096² 的缓存
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        BitmapFrame frame = BitmapDecoder.Create(stream,
            BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;
        if ((long)width * height > HeightmapRules.MaxDisplayPixels)
            return null;

        ushort[] samples = new ushort[width * height];
        PixelFormat format = frame.Format;
        if (format == PixelFormats.Gray16)
        {
            frame.CopyPixels(samples, width * 2, 0);
        }
        else if (format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64)
        {
            CopyFirstChannel(frame, samples, channels: 4);
        }
        else if (format == PixelFormats.Rgb48)
        {
            CopyFirstChannel(frame, samples, channels: 3);
        }
        else if (format == PixelFormats.Gray32Float || format == PixelFormats.Rgb128Float
                 || format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float)
        {
            throw new NotSupportedException(format.ToString());
        }
        else
        {
            // 8 位等：转成 Bgra32 取 R，与游戏一样放大到 16 位（b << 8 | b，TextureImporter.Ensure16BitsPerChannel）
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            byte[] row = new byte[width * 4];
            for (int y = 0; y < height; y++)
            {
                converted.CopyPixels(new Int32Rect(0, y, width, 1), row, width * 4, 0);
                for (int x = 0; x < width; x++)
                {
                    byte r = row[x * 4 + 2];
                    samples[y * width + x] = (ushort)((r << 8) | r);
                }
            }
        }

        return new DecodedHeightmap(width, height, samples, HeightmapStats.Compute(samples));
    }

    /// <summary>多通道 16 位：逐行复制，只留第一个通道（R，与 <c>TerrainSystem.ToR16</c> 一致）。</summary>
    private static void CopyFirstChannel(BitmapSource frame, ushort[] samples, int channels)
    {
        int width = frame.PixelWidth;
        ushort[] row = new ushort[width * channels];
        for (int y = 0; y < frame.PixelHeight; y++)
        {
            frame.CopyPixels(new Int32Rect(0, y, width, 1), row, width * channels * 2, 0);
            for (int x = 0; x < width; x++)
                samples[y * width + x] = row[x * channels];
        }
    }
}
