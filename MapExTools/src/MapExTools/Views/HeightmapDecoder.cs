using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapExTools.Core.Heightmaps;

namespace MapExTools.Views;

/// <summary>解码后的高度图：游戏导入时用的那个通道（灰度，或 RGBA 的 R），从上到下逐行存放。</summary>
/// <param name="Stats">统计一律按源图全部像素（降采样预览也一样），不按 <paramref name="Samples"/>。</param>
/// <param name="SourceWidth">源图像素宽度（超 4096 盒式降采样预览时与 <paramref name="Width"/> 不同）。</param>
/// <param name="SourceHeight">源图像素高度。</param>
internal sealed record DecodedHeightmap(int Width, int Height, ushort[] Samples, HeightmapStats Stats, int SourceWidth = 0, int SourceHeight = 0);

/// <summary>
/// 用 WIC 解码高度图（计划 §2 第 2 条：不走 GDI+，它会降成 8 位）。<c>PreservePixelFormat</c> 保留文件里的像素格式，
/// 16 位值原样取出：4.2 动工前用本机 4 张 4096² 的 16 位灰度 PNG（含 2 张带 sRGB／gAMA 块的）与不经 WIC 的手写解码逐像素比对，
/// 完全相同；16 位灰度 PNG、TIFF 编码再解码也无损。
/// 超过 4096 且不大于 8192 的高分辨率高度图在解码时逐行盒式降采样至 4096 以内，防止预览内存飙升。
/// </summary>
internal static class HeightmapDecoder
{
    /// <summary>解码一张图；宽/高超过 8192 或总像素超限时返回 null（只显示文件头信息）。</summary>
    /// <exception cref="NotSupportedException">浮点等无法按 16 位显示的格式。</exception>
    /// <exception cref="IOException">读文件出错。</exception>
    /// <exception cref="FileFormatException">不是能解码的图片。</exception>
    public static DecodedHeightmap? Decode(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 不缓存整图（None）：像素在流打开期间一次复制出来，省掉解码器自己那份全尺寸缓存
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        BitmapFrame frame = BitmapDecoder.Create(stream,
            BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
        int srcWidth = frame.PixelWidth;
        int srcHeight = frame.PixelHeight;
        if (srcWidth <= 0 || srcHeight <= 0
            || srcWidth > HeightmapRules.MaxImportSize || srcHeight > HeightmapRules.MaxImportSize
            || (long)srcWidth * srcHeight > HeightmapRules.MaxDisplayPixels)
        {
            return null;
        }

        PixelFormat format = frame.Format;
        if (format == PixelFormats.Gray32Float || format == PixelFormats.Rgb128Float
            || format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float)
        {
            throw new NotSupportedException(format.ToString());
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 不超过 4096×4096 原样 1:1 解码；超过时按最大边等比盒式降采样到 4096 以内（内存上限恒定约 32 MB）
        if (srcWidth <= HeightmapRules.MaxRenderSize && srcHeight <= HeightmapRules.MaxRenderSize)
        {
            ushort[] samples = DecodeFullResolution(frame, format, srcWidth, srcHeight, cancellationToken);
            return new DecodedHeightmap(srcWidth, srcHeight, samples, HeightmapStats.Compute(samples), srcWidth, srcHeight);
        }

        double scale = Math.Min((double)HeightmapRules.MaxRenderSize / srcWidth, (double)HeightmapRules.MaxRenderSize / srcHeight);
        int dstWidth = Math.Max(1, (int)Math.Round(srcWidth * scale));
        int dstHeight = Math.Max(1, (int)Math.Round(srcHeight * scale));
        (ushort[] downsampled, HeightmapStats stats) = DecodeBoxDownsampled(frame, format, srcWidth, srcHeight, dstWidth, dstHeight, cancellationToken);
        return new DecodedHeightmap(dstWidth, dstHeight, downsampled, stats, srcWidth, srcHeight);
    }

    private static ushort[] DecodeFullResolution(BitmapFrame frame, PixelFormat format, int width, int height, CancellationToken cancellationToken)
    {
        ushort[] samples = new ushort[width * height];
        if (format == PixelFormats.Gray16)
        {
            frame.CopyPixels(samples, width * 2, 0);
        }
        else if (format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64)
        {
            CopyFirstChannel(frame, samples, channels: 4, cancellationToken);
        }
        else if (format == PixelFormats.Rgb48)
        {
            CopyFirstChannel(frame, samples, channels: 3, cancellationToken);
        }
        else
        {
            // 8 位等：转成 Bgra32 取 R，与游戏一样放大到 16 位（b << 8 | b，TextureImporter.Ensure16BitsPerChannel）
            var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            byte[] row = new byte[width * 4];
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                converted.CopyPixels(new Int32Rect(0, y, width, 1), row, width * 4, 0);
                for (int x = 0; x < width; x++)
                {
                    byte r = row[x * 4 + 2];
                    samples[y * width + x] = (ushort)((r << 8) | r);
                }
            }
        }

        return samples;
    }

    /// <summary>
    /// 逐行读取源图并按像素盒式平均降采样到 <paramref name="dstWidth"/>×<paramref name="dstHeight"/>，仅占单行缓冲；
    /// 同时按源图每个像素累计统计（各盒子恰好不重不漏地覆盖源图，每个像素只计一次）。
    /// </summary>
    private static (ushort[] Samples, HeightmapStats Stats) DecodeBoxDownsampled(
        BitmapFrame frame,
        PixelFormat format,
        int srcWidth,
        int srcHeight,
        int dstWidth,
        int dstHeight,
        CancellationToken cancellationToken)
    {
        BitmapSource source;
        int mode; // 0 = Gray16, 1 = Rgba64/Prgba64 (4ch), 2 = Rgb48 (3ch), 3 = Bgra32 (8-bit)
        if (format == PixelFormats.Gray16)
        {
            source = frame;
            mode = 0;
        }
        else if (format == PixelFormats.Rgba64 || format == PixelFormats.Prgba64)
        {
            source = frame;
            mode = 1;
        }
        else if (format == PixelFormats.Rgb48)
        {
            source = frame;
            mode = 2;
        }
        else
        {
            source = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            mode = 3;
        }

        ushort[] srcU16 = mode switch
        {
            0 => new ushort[srcWidth],
            1 => new ushort[srcWidth * 4],
            2 => new ushort[srcWidth * 3],
            _ => [],
        };
        byte[] srcBgra = mode == 3 ? new byte[srcWidth * 4] : [];
        ushort[] dst = new ushort[dstWidth * dstHeight];
        long[] acc = new long[dstWidth];
        int[] cnt = new int[dstWidth];
        var stats = new HeightmapStats.Accumulator();

        for (int dy = 0; dy < dstHeight; dy++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int sy0 = (int)((long)dy * srcHeight / dstHeight);
            int sy1 = Math.Max(sy0 + 1, (int)((long)(dy + 1) * srcHeight / dstHeight));
            Array.Clear(acc);
            Array.Clear(cnt);

            for (int sy = sy0; sy < sy1 && sy < srcHeight; sy++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mode == 0)
                    source.CopyPixels(new Int32Rect(0, sy, srcWidth, 1), srcU16, srcWidth * 2, 0);
                else if (mode == 1)
                    source.CopyPixels(new Int32Rect(0, sy, srcWidth, 1), srcU16, srcWidth * 8, 0);
                else if (mode == 2)
                    source.CopyPixels(new Int32Rect(0, sy, srcWidth, 1), srcU16, srcWidth * 6, 0);
                else
                    source.CopyPixels(new Int32Rect(0, sy, srcWidth, 1), srcBgra, srcWidth * 4, 0);

                for (int dx = 0; dx < dstWidth; dx++)
                {
                    int sx0 = (int)((long)dx * srcWidth / dstWidth);
                    int sx1 = Math.Max(sx0 + 1, (int)((long)(dx + 1) * srcWidth / dstWidth));
                    for (int sx = sx0; sx < sx1 && sx < srcWidth; sx++)
                    {
                        ushort v = mode switch
                        {
                            0 => srcU16[sx],
                            1 => srcU16[sx * 4],
                            2 => srcU16[sx * 3],
                            _ => (ushort)((srcBgra[sx * 4 + 2] << 8) | srcBgra[sx * 4 + 2]),
                        };
                        acc[dx] += v;
                        cnt[dx]++;
                        stats.Add(v);
                    }
                }
            }

            int rowBase = dy * dstWidth;
            for (int dx = 0; dx < dstWidth; dx++)
            {
                dst[rowBase + dx] = cnt[dx] > 0 ? (ushort)(acc[dx] / cnt[dx]) : (ushort)0;
            }
        }

        return (dst, stats.Build());
    }

    /// <summary>多通道 16 位：逐行复制，只留第一个通道（R，与 <c>TerrainSystem.ToR16</c> 一致）。</summary>
    private static void CopyFirstChannel(BitmapSource frame, ushort[] samples, int channels, CancellationToken cancellationToken)
    {
        int width = frame.PixelWidth;
        ushort[] row = new ushort[width * channels];
        for (int y = 0; y < frame.PixelHeight; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            frame.CopyPixels(new Int32Rect(0, y, width, 1), row, width * channels * 2, 0);
            for (int x = 0; x < width; x++)
                samples[y * width + x] = row[x * channels];
        }
    }
}
