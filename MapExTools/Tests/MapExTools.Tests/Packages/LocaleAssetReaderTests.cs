using MapExTools.Core.Packages;

namespace MapExTools.Tests.Packages;

/// <summary>.loc 二进制格式（<c>LocaleAsset.Save</c>）。</summary>
public sealed class LocaleAssetReaderTests
{
    [Fact]
    public void TryRead_ReadsLocaleIdAndEntries()
    {
        byte[] data = CokTestData.Locale("zh-HANS", new Dictionary<string, string>
        {
            ["Maps.MAP_TITLE[AFLAT57km]"] = "平原 57 公里",
            ["Maps.MAP_DESCRIPTION[AFLAT57km]"] = "说明",
        });

        LocaleAssetData? locale = LocaleAssetReader.TryRead(data);

        Assert.NotNull(locale);
        Assert.Equal("zh-HANS", locale.LocaleId);
        Assert.Equal("平原 57 公里", locale.Entries["Maps.MAP_TITLE[AFLAT57km]"]);
        Assert.Equal(2, locale.Entries.Count);
    }

    [Fact]
    public void TryRead_Truncated_ReturnsNull()
    {
        byte[] data = CokTestData.Locale("en-US", new Dictionary<string, string> { ["Maps.MAP_TITLE[A]"] = "Alpha" });

        Assert.Null(LocaleAssetReader.TryRead(data[..^8]));
        Assert.Null(LocaleAssetReader.TryRead([]));
    }

    [Fact]
    public void TryRead_NegativeCount_ReturnsNull()
    {
        byte[] data = CokTestData.Locale("en-US", new Dictionary<string, string>());
        // 条数字段在末尾索引计数（4 字节）之前
        data[^8] = 0xFF;
        data[^7] = 0xFF;
        data[^6] = 0xFF;
        data[^5] = 0xFF;

        Assert.Null(LocaleAssetReader.TryRead(data));
    }
}
