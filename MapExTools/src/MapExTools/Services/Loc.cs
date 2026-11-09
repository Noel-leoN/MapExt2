using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using MapExTools.Core.Localization;

namespace MapExTools.Services;

/// <summary>
/// 界面文案的绑定源，运行时切换语言后所有绑定一起刷新。
/// XAML 用法：<c>{Binding [Tab.Launch], Source={x:Static svc:Loc.Instance}}</c>。
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private Loc()
    {
    }

    public static Loc Instance { get; } = new();

    public string Language { get; private set; } = UiLanguage.English;

    public string this[string key] => Strings.Get(Language, key);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>语言切换后触发，供代码里拼出的文字自行刷新。</summary>
    public event EventHandler? LanguageChanged;

    public string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, this[key], args);

    public void SetLanguage(string language)
    {
        string normalized = UiLanguage.Normalize(language) ?? UiLanguage.English;
        if (normalized == Language)
            return;

        Language = normalized;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
