using System.Windows.Media;
using MapExTools.Core.Heightmaps;
using MapExTools.Core.Packages;

namespace MapExTools.Views;

/// <summary>高度图列表的一项；文字按界面语言生成，换语言时整表重建。</summary>
/// <param name="Detail">尺寸与格式；认不出文件头时为原因。</param>
/// <param name="Issues">能否导入（短写法），按 <see cref="Tone"/> 着色。</param>
/// <param name="Meta">大小与修改时间。</param>
public sealed record HeightmapListItem(HeightmapFileInfo File, string Name, string Detail, string Issues, PackageTone Tone, string Meta);

/// <summary>图例的一行：色块与说明。</summary>
public sealed record LegendRow(Brush Swatch, string Text);
