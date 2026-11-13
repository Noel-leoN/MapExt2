using System.Windows.Media;
using System.Windows.Media.Imaging;
using MapExTools.Core.Packages;

namespace MapExTools.Views;

/// <summary>一次预览图读取的结果。</summary>
/// <param name="Result">解码结论（不含像素，免得占着内存）；读文件出错时为 null。</param>
/// <param name="Bitmap">可显示的图；没有解出图时为 null。</param>
/// <param name="Error">读文件出错的信息。</param>
internal sealed record PreviewLoad(TextureDecodeResult? Result, BitmapSource? Bitmap, string? Error);

/// <summary>
/// 读出 .cok 里的预览图并转成 WPF 位图。读文件、解码、缩小都在后台线程做，同时最多两个，
/// 免得滚动列表时一下子打开几十个包。位图冻结后可跨线程使用。
/// </summary>
internal static class PreviewBitmaps
{
    private static readonly SemaphoreSlim s_gate = new(2);

    /// <param name="maxWidth">缩略图的最大宽度（像素）；null 表示原尺寸。</param>
    public static async Task<PreviewLoad> LoadAsync(CokPackageInfo package, int? maxWidth, CancellationToken cancellationToken)
    {
        if (package.PreviewEntry is not { } entry)
            return new PreviewLoad(null, null, null);

        await s_gate.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => Load(package.Path, entry, maxWidth), cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            // 包的内容不可信，读不了就在界面上说明；调用方多是 async void，异常不能往外抛
            return new PreviewLoad(null, null, ex.Message);
        }
        finally
        {
            s_gate.Release();
        }
    }

    private static PreviewLoad Load(string path, string entry, int? maxWidth)
    {
        TextureDecodeResult result = TextureAssetDecoder.Decode(CokPackageReader.ReadEntryBytes(path, entry));
        if (result.Image is not { } image)
            return new PreviewLoad(result, null, null);

        DecodedImage shown = maxWidth is int width ? TextureAssetDecoder.Downscale(image, width) : image;
        return new PreviewLoad(result with { Image = null }, ToBitmap(shown), null);
    }

    /// <summary>转成冻结的位图。预览图是截图，透明通道没有意义，按 Bgr32 忽略。</summary>
    private static BitmapSource ToBitmap(DecodedImage image)
    {
        BitmapSource bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, image.Pixels, image.Stride);
        bitmap.Freeze();
        return bitmap;
    }
}
