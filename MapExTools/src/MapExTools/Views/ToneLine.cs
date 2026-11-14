using MapExTools.Core.Packages;

namespace MapExTools.Views;

/// <summary>一行按醒目程度着色的文字，配合 App.xaml 的 <c>ToneText</c> 样式（绑定 <see cref="Tone"/>）。</summary>
public sealed record ToneLine(string Text, PackageTone Tone = PackageTone.Normal);
