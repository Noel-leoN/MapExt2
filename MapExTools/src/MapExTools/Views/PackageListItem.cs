using System.ComponentModel;
using System.Globalization;
using System.Windows.Media;
using MapExTools.Core.Localization;
using MapExTools.Core.Packages;

namespace MapExTools.Views;

/// <summary>
/// 存档与地图列表的一项。文字随界面语言与设定模式更新（<see cref="Update"/>）；缩略图在列表第一次取用时
/// （即这一项滚进可见范围时）才在后台读取，列表是虚拟化的，看不到的项不读。
/// </summary>
public sealed class PackageListItem : INotifyPropertyChanged
{
    /// <summary>缩略图的最大宽度（像素）：680 宽的预览图缩小 4 倍为 170，150% 缩放下仍清晰。</summary>
    private const int ThumbnailMaxWidth = 200;

    private ImageSource? _thumbnail;
    private bool _thumbnailRequested;
    private bool _thumbnailDone;
    private string _language = UiLanguage.English;

    private readonly string _dateText;

    public PackageListItem(CokPackageInfo package)
    {
        Package = package;
        Title = package.Name;
        _dateText = package.LastWriteTime == default ? string.Empty : package.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        InfoText = _dateText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CokPackageInfo Package { get; }

    /// <summary>属于"存档"一栏：内容是存档，或读不出内容但放在 Saves 目录下。</summary>
    public bool InSavesTab => Package.Kind == PackageKind.Save || (Package.Kind == PackageKind.Unknown && Package.Folder == PackageFolder.Saves);

    public string Title { get; private set; }

    public string ModeText { get; private set; } = string.Empty;

    public string RelationText { get; private set; } = string.Empty;

    /// <summary>与设定模式的关系；包读不出来时为 null。</summary>
    public PackageRelation? Relation { get; private set; }

    public PackageTone Tone { get; private set; }

    /// <summary>第三行：修改时间；标题与文件名不同时（地图标题取自 .loc，几张图可能同名）前面加上文件名。</summary>
    public string InfoText { get; private set; }

    /// <summary>缩略图位置上的文字：读取中，或没有可显示的预览图；有缩略图时为空。</summary>
    public string PlaceholderText =>
        _thumbnail != null ? string.Empty
        : Strings.Get(_language, _thumbnailDone || Package.PreviewEntry == null ? "Saves.NoPreview" : "Saves.Preview.Loading");

    /// <summary>缩略图；第一次取用时开始后台读取，读完前为 null。</summary>
    public ImageSource? Thumbnail
    {
        get
        {
            if (!_thumbnailRequested && Package.PreviewEntry != null)
            {
                _thumbnailRequested = true;
                _ = LoadThumbnailAsync();
            }

            return _thumbnail;
        }
    }

    /// <summary>按界面语言与设定模式重算文字。<paramref name="settings"/> 为 null 表示设定模式读不出来。</summary>
    public void Update(GateSettings? settings, string language)
    {
        _language = language;
        Relation = SaveGateRules.Evaluate(Package, settings);
        Title = PackageText.Title(Package, language);
        InfoText = Title == Package.Name ? _dateText : Package.Name + " · " + _dateText;
        ModeText = PackageText.Mode(Package, language, brief: true);
        RelationText = Package.Problem == PackageProblem.None
            ? PackageText.RelationLabel(Relation, language)
            : PackageText.Problem(Package, language);
        Tone = PackageText.RelationTone(Relation);

        // 空字符串表示全部属性都变了
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>关键词是否出现在标题、文件名、城市名或地图名里（不分大小写）。</summary>
    public bool Matches(string keyword)
    {
        if (keyword.Length == 0)
            return true;

        return Contains(Title) || Contains(Package.Name) || Contains(Package.Save?.CityName) || Contains(Package.Save?.MapName)
               || Contains(Package.Map?.DisplayName);

        bool Contains(string? text) => text != null && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadThumbnailAsync()
    {
        try
        {
            PreviewLoad load = await PreviewBitmaps.LoadAsync(Package, ThumbnailMaxWidth, CancellationToken.None);
            _thumbnail = load.Bitmap;
        }
        finally
        {
            // 读不出来也要结束"读取中"，改显示"无预览图"
            _thumbnailDone = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderText)));
        }
    }
}
