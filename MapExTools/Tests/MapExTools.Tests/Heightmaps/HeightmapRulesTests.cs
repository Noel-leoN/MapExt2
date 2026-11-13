using MapExTools.Core.Heightmaps;

namespace MapExTools.Tests.Heightmaps;

/// <summary>高度图规则：格式归类、能否导入、高度比例与海拔换算、海平面门槛（计划 4.2）。</summary>
public sealed class HeightmapRulesTests
{
    [Theory]
    [InlineData(1, 16, false, HeightmapPixelKind.Gray16)]
    [InlineData(4, 16, false, HeightmapPixelKind.Rgba16)]
    [InlineData(2, 16, false, HeightmapPixelKind.Other16)] // 灰度＋透明
    [InlineData(3, 16, false, HeightmapPixelKind.Other16)] // RGB
    [InlineData(1, 8, false, HeightmapPixelKind.EightBit)]
    [InlineData(3, 8, false, HeightmapPixelKind.EightBit)] // JPG
    [InlineData(1, 1, false, HeightmapPixelKind.EightBit)]
    [InlineData(1, 32, true, HeightmapPixelKind.Unsupported)]
    [InlineData(1, 16, true, HeightmapPixelKind.Unsupported)] // 半精度浮点
    [InlineData(1, 32, false, HeightmapPixelKind.Unsupported)]
    [InlineData(5, 16, false, HeightmapPixelKind.Unsupported)]
    public void Classify(int channels, int bits, bool isFloat, HeightmapPixelKind expected)
    {
        Assert.Equal(expected, HeightmapRules.Classify(channels, bits, isFloat));
    }

    [Theory]
    [InlineData(4096, 4096, HeightmapPixelKind.Gray16, HeightmapIssues.None)]
    [InlineData(4096, 4096, HeightmapPixelKind.Rgba16, HeightmapIssues.None)]
    [InlineData(8192, 8192, HeightmapPixelKind.Gray16, HeightmapIssues.WrongSize)]
    [InlineData(4096, 2048, HeightmapPixelKind.Gray16, HeightmapIssues.WrongSize)]
    [InlineData(4096, 4096, HeightmapPixelKind.Other16, HeightmapIssues.WrongFormat)]
    [InlineData(4096, 4096, HeightmapPixelKind.EightBit, HeightmapIssues.WrongFormat)]
    [InlineData(8192, 8192, HeightmapPixelKind.Other16, HeightmapIssues.WrongSize | HeightmapIssues.WrongFormat)]
    public void Validate(int width, int height, HeightmapPixelKind kind, HeightmapIssues expected)
    {
        Assert.Equal(expected, HeightmapRules.Validate(width, height, kind));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(4096, true)]
    [InlineData(10000, true)]
    [InlineData(199.9, false)]
    [InlineData(10000.1, false)]
    [InlineData(double.NaN, false)]
    public void IsValidHeightScale(double scale, bool expected)
    {
        Assert.Equal(expected, HeightmapRules.IsValidHeightScale(scale));
    }

    [Fact]
    public void ToElevation_IsLinearOverTheFullRange()
    {
        Assert.Equal(0, HeightmapRules.ToElevation(0, 4096));
        Assert.Equal(4096, HeightmapRules.ToElevation(65535, 4096), 9);
        Assert.Equal(2048, HeightmapRules.ToElevation(65535, 2048), 9);
        Assert.Equal(1384.5, HeightmapRules.ToElevation(22152, 4096), 1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(4096.01, 65536)]
    public void SeaLevelThreshold_Extremes(double seaLevel, int expected)
    {
        Assert.Equal(expected, HeightmapRules.SeaLevelThreshold(seaLevel, 4096));
    }

    [Theory]
    [InlineData(40, 4096)]
    [InlineData(0.0625, 4096)] // 恰好在原始值 1 附近
    [InlineData(123.456, 2000)]
    [InlineData(4096, 4096)]
    [InlineData(9999.99, 10000)]
    public void SeaLevelThreshold_IsTheFirstRawAtOrAboveSeaLevel(double seaLevel, double scale)
    {
        int threshold = HeightmapRules.SeaLevelThreshold(seaLevel, scale);

        Assert.InRange(threshold, 1, 65535);
        Assert.True(HeightmapRules.ToElevation(threshold, scale) >= seaLevel);
        Assert.True(HeightmapRules.ToElevation(threshold - 1, scale) < seaLevel);
    }
}
