using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MapExTools.Core.Heightmaps;
using MapExTools.Core.Packages;
using MapExTools.Services;

namespace MapExTools.Views;

/// <summary>
/// 高度图查看页（计划 4.2）。左边列出 <c>{UserData}/Heightmaps</c> 下的图片（只读文件头，判断能否导入游戏），
/// 右边显示选中的图：三种视图（原始灰度、自动拉伸、分层着色），滚轮缩放、左键拖动，鼠标所在像素显示原始值与海拔。
/// 也可以打开或拖入目录外的文件。解码与渲染都在后台线程，界面线程只接结果。
/// </summary>
public partial class HeightmapViewerPage : UserControl
{
    private const double MinZoom = 0.02;
    private const double MaxZoom = 32;
    private const double WheelStep = 1.25;

    private static readonly TimeSpan s_inputDelay = TimeSpan.FromMilliseconds(400);

    private readonly DispatcherTimer _inputTimer = new() { Interval = s_inputDelay };

    private IReadOnlyList<HeightmapFileInfo> _files = [];
    private bool _scanned;
    private bool _scanning;

    private HeightmapFileInfo? _current;
    private DecodedHeightmap? _image;

    /// <summary>显示用的位图，同尺寸的图之间复用（见 <see cref="Render"/>）。</summary>
    private WriteableBitmap? _bitmap;
    private string? _imageMessageKey;
    private string? _imageMessageArg;
    private CancellationTokenSource? _decodeCts;
    private bool _rendering;
    private bool _renderAgain;
    private bool _fitPending;

    private HeightmapView _view = HeightmapView.Stretched;
    private double _heightScale = HeightmapRules.DefaultHeightScale;
    private double _seaLevel;
    private double _zoom = 1;
    private IReadOnlyList<LegendEntry> _legend = [];

    private Point? _dragStart;
    private double _dragHorizontal;
    private double _dragVertical;

    // 填充下拉框、重建列表时会触发 SelectionChanged，这段时间不当作用户操作
    private bool _populating;

    public HeightmapViewerPage()
    {
        InitializeComponent();
        _inputTimer.Tick += (_, _) =>
        {
            _inputTimer.Stop();
            ApplyInputs();
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private sealed record Choice<T>(T Value, string Display);

    private static string CurrentLanguage => Loc.Instance.Language;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged += OnLanguageChanged;
        PopulateViewBox();
        RefreshTexts();
        if (!_scanned)
            _ = RescanAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.LanguageChanged -= OnLanguageChanged;
        _inputTimer.Stop();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        PopulateViewBox();
        RebuildList();
        RefreshTexts();
    }

    // === 文件列表 ===

    private async Task RescanAsync()
    {
        if (_scanning)
            return;

        _scanning = true;
        RefreshButton.IsEnabled = false;
        StatusText.Text = Loc.Instance["Heightmaps.Scanning"];
        try
        {
            var paths = App.Services.Paths;
            _files = await Task.Run(() => HeightmapFiles.Scan(paths));
            _scanned = true;
        }
        finally
        {
            _scanning = false;
            RefreshButton.IsEnabled = true;
        }

        RebuildList();
        RefreshTexts();
    }

    private void RebuildList()
    {
        string? keep = (FileList.SelectedItem as HeightmapListItem)?.File.Path;
        string language = CurrentLanguage;
        List<HeightmapListItem> items = _files.Select(f => new HeightmapListItem(
                f,
                f.RelativeName,
                f.Header != null ? HeightmapText.SizeAndKind(f.Header, language)
                : f.ReadError != null ? Loc.Instance.Format("Heightmaps.ReadError", f.ReadError)
                : Loc.Instance["Heightmaps.Unrecognized"],
                HeightmapText.Issues(f.Issues, language, brief: true),
                HeightmapText.IssuesTone(f.Issues),
                PackageText.FormatSize(f.Length) + " · " + f.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

        _populating = true;
        FileList.ItemsSource = items;
        FileList.SelectedItem = keep == null ? null : items.FirstOrDefault(i => string.Equals(i.File.Path, keep, StringComparison.OrdinalIgnoreCase));
        _populating = false;
    }

    private void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_populating && FileList.SelectedItem is HeightmapListItem item)
            Show(item.File);
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) => await RescanAsync();

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Loc.Instance["Heightmaps.FileFilter"],
            InitialDirectory = Directory.Exists(App.Services.Paths.HeightmapsDir) ? App.Services.Paths.HeightmapsDir : string.Empty,
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
            OpenExternal(dialog.FileName);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        string dir = App.Services.Paths.HeightmapsDir;
        if (!Directory.Exists(dir))
        {
            ShowWarning(Loc.Instance.Format("Heightmaps.FolderMissing", dir));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && File.Exists(files[0]))
            OpenExternal(files[0]);
    }

    /// <summary>打开目录外的文件（或目录里的文件，但不经列表选择）：列表里有就选中它，否则直接显示。</summary>
    private void OpenExternal(string path)
    {
        HeightmapListItem? listed = (FileList.ItemsSource as IEnumerable<HeightmapListItem>)?
            .FirstOrDefault(i => string.Equals(i.File.Path, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        if (listed != null)
        {
            FileList.SelectedItem = listed;
            FileList.ScrollIntoView(listed);
            return;
        }

        _populating = true;
        FileList.SelectedItem = null;
        _populating = false;
        Show(HeightmapFiles.Describe(path, App.Services.Paths.HeightmapsDir));
    }

    // === 解码与渲染 ===

    /// <summary>显示一个文件：先显示文件头信息，再在后台解码；比 4096×4096 大的不解码。</summary>
    private async void Show(HeightmapFileInfo file)
    {
        _decodeCts?.Cancel();
        _decodeCts?.Dispose();
        var cts = new CancellationTokenSource();
        _decodeCts = cts;

        _current = file;
        _image = null;
        _legend = [];
        HeightImage.Source = null;
        HoverText.Text = string.Empty;
        bool tooLarge = file.Header != null && (long)file.Header.Width * file.Header.Height > HeightmapRules.MaxDisplayPixels;
        SetImageMessage(tooLarge ? "Heightmaps.TooLarge" : "Heightmaps.Decoding");
        RefreshTexts();
        if (tooLarge)
            return;

        try
        {
            DecodedHeightmap? decoded = await Task.Run(() => HeightmapDecoder.Decode(file.Path), cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(file, _current))
                return;

            _image = decoded;
            SetImageMessage(decoded == null ? "Heightmaps.TooLarge" : null);
            _fitPending = true;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // 图片是用户的文件，内容不可信：解不出来就在界面上说明，不让 async void 把异常抛出去
            if (!ReferenceEquals(file, _current))
                return;
            SetImageMessage("Heightmaps.DecodeFailed", ex.Message);
        }

        RefreshTexts();
        Render();
    }

    /// <summary>
    /// 按当前视图、高度比例与海平面重画。同一时间只画一次：画的过程中参数或图又变了，画完再按最新的画一遍。
    ///
    /// <para><b>内存</b>：所有视图都画进同一张 <see cref="WriteableBitmap"/>（同尺寸的图之间也复用）。界面线程锁住位图，
    /// 后台线程逐行直接写进它的后台缓冲，画完在界面线程标脏、解锁（WPF 文档里的后台更新写法），不经中间缓冲。
    /// 每次重画都新建位图时，旧位图的非托管内存要等垃圾回收才释放，换几次视图专用内存就到 430 MB 左右（4.2 实测）。</para>
    /// </summary>
    private async void Render()
    {
        if (_rendering)
        {
            _renderAgain = true;
            return;
        }

        _rendering = true;
        try
        {
            while (_image is { } image)
            {
                _renderAgain = false;
                HeightmapView view = _view;
                double heightScale = _heightScale;
                double seaLevel = _seaLevel;
                if (_bitmap == null || _bitmap.PixelWidth != image.Width || _bitmap.PixelHeight != image.Height)
                    _bitmap = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null);

                WriteableBitmap bitmap = _bitmap;
                IReadOnlyList<LegendEntry> legend;
                bitmap.Lock();
                try
                {
                    IntPtr buffer = bitmap.BackBuffer;
                    int stride = bitmap.BackBufferStride;
                    legend = await Task.Run(() => DrawInto(image, view, heightScale, seaLevel, buffer, stride));
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, image.Width, image.Height));
                }
                finally
                {
                    bitmap.Unlock();
                }

                // 画的过程中换了图或改了参数：按最新的再画一遍
                if (_renderAgain || !ReferenceEquals(image, _image))
                    continue;

                HeightImage.Source = bitmap;
                _legend = legend;
                RefreshLegend();
                UpdateScalingMode();
                if (_fitPending)
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitIfPending);
                break;
            }
        }
        finally
        {
            _rendering = false;
        }
    }

    /// <summary>占位提示（解码中、太大、解码失败）：存文案键，换语言时跟着变。</summary>
    private void SetImageMessage(string? key, string? arg = null)
    {
        _imageMessageKey = key;
        _imageMessageArg = arg;
    }

    /// <summary>
    /// 在后台线程把像素（Bgr32）逐行写进位图的后台缓冲，返回图例。每个线程用一行大小的缓冲中转（4096 宽时 16 KB）。
    /// </summary>
    private static IReadOnlyList<LegendEntry> DrawInto(DecodedHeightmap image, HeightmapView view, double heightScale, double seaLevel,
        IntPtr buffer, int stride)
    {
        uint[] lut;
        LayeredScale? scale = null;
        int coastThreshold = 0;
        if (view == HeightmapView.Layered)
        {
            scale = HeightmapRenderer.CreateLayeredScale(image.Stats, heightScale, seaLevel);
            lut = HeightmapRenderer.BuildLayeredLut(scale, heightScale);
            coastThreshold = scale.SeaThreshold;
        }
        else
        {
            lut = HeightmapRenderer.ToColorLut(HeightmapRenderer.BuildGrayLut(view, image.Stats));
        }

        Parallel.For(0, image.Height, () => new int[image.Width], (y, _, row) =>
        {
            HeightmapRenderer.RenderRow(image.Samples, image.Width, image.Height, y, lut, coastThreshold, MemoryMarshal.Cast<int, uint>(row.AsSpan()));
            Marshal.Copy(row, 0, buffer + y * stride, image.Width);
            return row;
        }, _ => { });
        return HeightmapRenderer.BuildLegend(view, image.Stats, heightScale, scale);
    }

    // === 视图与输入 ===

    private void PopulateViewBox()
    {
        _populating = true;
        var items = new[]
        {
            new Choice<HeightmapView>(HeightmapView.Raw, Loc.Instance["Heightmaps.View.Raw"]),
            new Choice<HeightmapView>(HeightmapView.Stretched, Loc.Instance["Heightmaps.View.Stretched"]),
            new Choice<HeightmapView>(HeightmapView.Layered, Loc.Instance["Heightmaps.View.Layered"]),
        };
        ViewBox.ItemsSource = items;
        ViewBox.SelectedIndex = Array.FindIndex(items, c => c.Value == _view);
        _populating = false;
    }

    private void OnViewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_populating || ViewBox.SelectedItem is not Choice<HeightmapView> choice || choice.Value == _view)
            return;

        _view = choice.Value;
        Render();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e)
    {
        // 停止输入 0.4 秒后再换算
        _inputTimer.Stop();
        _inputTimer.Start();
    }

    /// <summary>读高度比例与海平面；不合法时提示并沿用上一个值。分层视图要重画，灰度视图只改文字。</summary>
    private void ApplyInputs()
    {
        var errors = new List<string>();
        double heightScale = _heightScale;
        if (TryParse(HeightScaleBox.Text, out double scale) && HeightmapRules.IsValidHeightScale(scale))
            heightScale = scale;
        else
            errors.Add(Loc.Instance["Heightmaps.BadHeightScale"]);

        double seaLevel = _seaLevel;
        if (TryParse(SeaLevelBox.Text, out double sea))
            seaLevel = sea;
        else
            errors.Add(Loc.Instance["Heightmaps.BadSeaLevel"]);

        InputError.Text = string.Join(Environment.NewLine, errors);
        InputError.Visibility = errors.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (heightScale == _heightScale && seaLevel == _seaLevel)
            return;

        _heightScale = heightScale;
        _seaLevel = seaLevel;
        RefreshTexts();
        if (_view == HeightmapView.Layered)
        {
            Render();
        }
        else if (_image != null)
        {
            _legend = HeightmapRenderer.BuildLegend(_view, _image.Stats, _heightScale, null);
            RefreshLegend();
        }
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        || double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    // === 文字 ===

    private void RefreshTexts()
    {
        string language = CurrentLanguage;
        FolderText.Text = Loc.Instance.Format("Heightmaps.Folder", App.Services.Paths.HeightmapsDir);
        if (!_scanning)
            StatusText.Text = HeightmapText.Status(_files, language);

        HeightmapFileInfo? file = _current;
        if (file == null)
        {
            TitleText.Text = string.Empty;
            KindText.Text = string.Empty;
            IssuesText.Text = string.Empty;
            StatsText.Text = string.Empty;
            PlaceholderText.Text = Loc.Instance["Heightmaps.NoSelection"];
            return;
        }

        TitleText.Text = file.RelativeName;
        KindText.Text = file.Header != null ? HeightmapText.SizeAndKind(file.Header, language)
            : file.ReadError != null ? Loc.Instance.Format("Heightmaps.ReadError", file.ReadError)
            : string.Empty;
        string issues = HeightmapText.Issues(file.Issues, language, brief: false);
        IssuesText.Text = issues;
        IssuesText.DataContext = new DetailLine(string.Empty, issues, HeightmapText.IssuesTone(file.Issues));
        StatsText.Text = _image == null ? string.Empty : string.Join(" · ", HeightmapText.Stats(_image.Stats, _heightScale, language));
        PlaceholderText.Text = _imageMessageKey == null ? string.Empty
            : _imageMessageArg == null ? Loc.Instance[_imageMessageKey]
            : Loc.Instance.Format(_imageMessageKey, _imageMessageArg);
        RefreshLegend();
    }

    private void RefreshLegend()
    {
        string language = CurrentLanguage;
        LegendList.ItemsSource = _legend.Select(entry => new LegendRow(SwatchBrush(entry.Color), HeightmapText.Legend(entry, language))).ToList();
        LegendPanel.Visibility = _legend.Count > 0 && _image != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static SolidColorBrush SwatchBrush(uint argb)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        brush.Freeze();
        return brush;
    }

    // === 缩放、拖动与读数 ===

    /// <summary>缩放到整张图放得进视口；视口还没排版好时返回 false。</summary>
    private bool FitToViewport()
    {
        if (_image == null || Viewport.ViewportWidth <= 0 || Viewport.ViewportHeight <= 0)
            return false;

        double fit = Math.Min(Viewport.ViewportWidth / _image.Width, Viewport.ViewportHeight / _image.Height);
        SetZoom(fit, null);
        return true;
    }

    /// <summary>新图第一次显示时适应视口；视口还没排版好就等 SizeChanged 再做。</summary>
    private void FitIfPending()
    {
        if (_fitPending && FitToViewport())
            _fitPending = false;
    }

    private void OnFitClick(object sender, RoutedEventArgs e) => FitToViewport();

    /// <summary>一个像素对一个屏幕像素：按显示缩放换算。</summary>
    private void OnActualPixelsClick(object sender, RoutedEventArgs e) =>
        SetZoom(1 / VisualTreeHelper.GetDpi(this).DpiScaleX, null);

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e) => FitIfPending();

    /// <summary>以 <paramref name="anchor"/>（视口坐标，默认视口中心）为不动点缩放。</summary>
    private void SetZoom(double zoom, Point? anchor)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        Point point = anchor ?? new Point(Viewport.ViewportWidth / 2, Viewport.ViewportHeight / 2);
        double contentX = (Viewport.HorizontalOffset + point.X) / _zoom;
        double contentY = (Viewport.VerticalOffset + point.Y) / _zoom;

        _zoom = zoom;
        ImageScale.ScaleX = zoom;
        ImageScale.ScaleY = zoom;
        Viewport.UpdateLayout();
        Viewport.ScrollToHorizontalOffset(contentX * zoom - point.X);
        Viewport.ScrollToVerticalOffset(contentY * zoom - point.Y);
        ZoomText.Text = Loc.Instance.Format("Heightmaps.Zoom", zoom * VisualTreeHelper.GetDpi(this).DpiScaleX * 100);
        UpdateScalingMode();
    }

    /// <summary>放大到能看清像素时用最近邻，缩小时用高质量缩放。</summary>
    private void UpdateScalingMode() =>
        RenderOptions.SetBitmapScalingMode(HeightImage, _zoom * VisualTreeHelper.GetDpi(this).DpiScaleX >= 2
            ? BitmapScalingMode.NearestNeighbor
            : BitmapScalingMode.HighQuality);

    private void OnViewportWheel(object sender, MouseWheelEventArgs e)
    {
        if (_image == null)
            return;

        e.Handled = true;
        SetZoom(e.Delta > 0 ? _zoom * WheelStep : _zoom / WheelStep, e.GetPosition(Viewport));
    }

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(Viewport);
        _dragHorizontal = Viewport.HorizontalOffset;
        _dragVertical = Viewport.VerticalOffset;
        HeightImage.CaptureMouse();
        HeightImage.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnImageMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragStart = null;
        HeightImage.ReleaseMouseCapture();
        HeightImage.Cursor = Cursors.Cross;
    }

    private void OnImageMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is Point start && e.LeftButton == MouseButtonState.Pressed)
        {
            Point now = e.GetPosition(Viewport);
            Viewport.ScrollToHorizontalOffset(_dragHorizontal - (now.X - start.X));
            Viewport.ScrollToVerticalOffset(_dragVertical - (now.Y - start.Y));
        }

        // 图像以 96 DPI 建立，图像自身坐标即像素坐标
        DecodedHeightmap? image = _image;
        Point p = e.GetPosition(HeightImage);
        int x = (int)Math.Floor(p.X);
        int y = (int)Math.Floor(p.Y);
        if (image == null || x < 0 || y < 0 || x >= image.Width || y >= image.Height)
        {
            HoverText.Text = string.Empty;
            return;
        }

        HoverText.Text = HeightmapText.Hover(x, y, image.Samples[y * image.Width + x], _heightScale, _seaLevel, CurrentLanguage);
    }

    private void OnImageMouseLeave(object sender, MouseEventArgs e)
    {
        if (_dragStart == null)
            HoverText.Text = string.Empty;
    }

    private void ShowWarning(string text) =>
        MessageBox.Show(Window.GetWindow(this)!, text, Loc.Instance["App.Title"], MessageBoxButton.OK, MessageBoxImage.Warning);
}
