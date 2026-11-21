using MapExTools.Core.Heightmaps;

namespace MapExTools.Tests.Heightmaps;

/// <summary>统计、灰度与分层查找表、图例、海岸线（计划 4.2）。</summary>
public sealed class HeightmapRendererTests
{
    [Fact]
    public void Stats_MinMaxMean()
    {
        HeightmapStats stats = HeightmapStats.Compute([10, 20, 30, 40]);

        Assert.Equal(new HeightmapStats(10, 40, 25, 4, 10, 40), stats);
        Assert.Equal(40 / 65535.0 * 4096, stats.MaxElevation(4096), 9);
        Assert.Throws<ArgumentException>(() => HeightmapStats.Compute([]));
    }

    [Fact]
    public void Stats_StretchRangeClipsHalfAPercentEachEnd()
    {
        // 0–999 各一个：第 5 个（累计 0.5%）是 4，第 995 个（累计 99.5%）是 994
        ushort[] samples = Enumerable.Range(0, 1000).Select(i => (ushort)i).ToArray();

        HeightmapStats stats = HeightmapStats.Compute(samples);

        Assert.Equal((0, 999), ((int)stats.Min, (int)stats.Max));
        Assert.Equal((4, 994), ((int)stats.StretchLow, (int)stats.StretchHigh));
    }

    [Fact]
    public void Stats_Accumulator_MatchesCompute()
    {
        var random = new Random(20261121);
        ushort[] samples = Enumerable.Range(0, 5000).Select(_ => (ushort)random.Next(0, 65536)).ToArray();
        var accumulator = new HeightmapStats.Accumulator();
        foreach (ushort value in samples)
            accumulator.Add(value);

        Assert.Equal(HeightmapStats.Compute(samples), accumulator.Build());
        Assert.Throws<InvalidOperationException>(() => new HeightmapStats.Accumulator().Build());
    }

    [Fact]
    public void Stats_MostlyFlat_FallsBackToMinMax()
    {
        ushort[] samples = [.. Enumerable.Repeat((ushort)100, 1000), 60000, 60000];

        HeightmapStats stats = HeightmapStats.Compute(samples);

        Assert.Equal((100, 60000), ((int)stats.StretchLow, (int)stats.StretchHigh));
    }

    [Fact]
    public void GrayLut_Raw_MapsFullRange()
    {
        byte[] lut = HeightmapRenderer.BuildGrayLut(HeightmapView.Raw, new HeightmapStats(100, 200, 150, 2, 100, 200));

        Assert.Equal(0, lut[0]);
        Assert.Equal(128, lut[32768]);
        Assert.Equal(255, lut[65535]);
    }

    [Fact]
    public void GrayLut_Stretched_MapsTheStretchRange()
    {
        // 最低 0、最高 5000，拉伸区间 1000–2000：区间外裁成纯黑、纯白
        byte[] lut = HeightmapRenderer.BuildGrayLut(HeightmapView.Stretched, new HeightmapStats(0, 5000, 1500, 2, 1000, 2000));

        Assert.Equal(0, lut[0]);
        Assert.Equal(0, lut[1000]);
        Assert.Equal(128, lut[1500]);
        Assert.Equal(255, lut[2000]);
        Assert.Equal(255, lut[5000]);
    }

    [Fact]
    public void GrayLut_Stretched_FlatImageIsMidGray()
    {
        byte[] lut = HeightmapRenderer.BuildGrayLut(HeightmapView.Stretched, new HeightmapStats(500, 500, 500, 1, 500, 500));

        Assert.Equal(128, lut[500]);
    }

    [Fact]
    public void ToColorLut_IsOpaqueGray()
    {
        uint[] lut = HeightmapRenderer.ToColorLut([0, 128, 255]);

        Assert.Equal([0xFF000000u, 0xFF808080u, 0xFFFFFFFFu], lut);
    }

    [Fact]
    public void Render_GrayWithoutCoast_LooksUpEveryPixel()
    {
        // 多于一行、宽度不整除并行分块，核对每个像素都查过表
        ushort[] samples = Enumerable.Range(0, 300 * 700).Select(i => (ushort)(i * 7 % 65536)).ToArray();
        uint[] lut = HeightmapRenderer.ToColorLut(HeightmapRenderer.BuildGrayLut(HeightmapView.Raw, HeightmapStats.Compute(samples)));
        uint[] destination = new uint[samples.Length + 5]; // 借来的缓冲可能比需要的大

        HeightmapRenderer.Render(samples, 700, 300, lut, 0, destination);

        Assert.Equal(samples.Select(s => lut[s]), destination.Take(samples.Length));
    }

    // 高度比例 6553.5 时原始值 ×0.1 即为米数，便于核对
    private const double Scale = 6553.5;

    [Fact]
    public void Layered_LandAndWaterBandsAroundSeaLevel()
    {
        // 最低 0 m、最高 120 m，海平面 60 m：水下 60–0 m、陆地 60–120 m 各分 6 级（每级 10 m）
        var stats = new HeightmapStats(0, 1200, 600, 2, 0, 1200);
        LayeredScale scale = HeightmapRenderer.CreateLayeredScale(stats, Scale, 60);
        uint[] lut = HeightmapRenderer.BuildLayeredLut(scale, Scale);
        int threshold = scale.SeaThreshold;

        Assert.True(scale.HasLand && scale.HasWater);
        Assert.InRange(threshold, 599, 601); // 60 m 附近，以 SeaLevelThreshold 的换算为准
        Assert.Equal(HeightmapRenderer.LandBands[0], lut[threshold]); // 海平面：陆地最低一级
        Assert.Equal(HeightmapRenderer.WaterBands[0], lut[threshold - 1]); // 刚到水下：最浅一级
        // 其余取各级中间的点，避开浮点换算的边界
        Assert.Equal(HeightmapRenderer.LandBands[0], lut[650]); // 65 m
        Assert.Equal(HeightmapRenderer.LandBands[1], lut[750]); // 75 m
        Assert.Equal(HeightmapRenderer.LandBands[5], lut[1200]); // 最高点
        Assert.Equal(HeightmapRenderer.WaterBands[0], lut[550]); // 水下 5 m
        Assert.Equal(HeightmapRenderer.WaterBands[1], lut[450]); // 水下 15 m
        Assert.Equal(HeightmapRenderer.WaterBands[5], lut[0]); // 最低点
    }

    [Fact]
    public void Layered_NoSea_SpreadsLandOverTheImageRange()
    {
        var stats = new HeightmapStats(1000, 1600, 1300, 2, 1000, 1600);
        LayeredScale scale = HeightmapRenderer.CreateLayeredScale(stats, Scale, 0);
        uint[] lut = HeightmapRenderer.BuildLayeredLut(scale, Scale);

        Assert.False(scale.HasWater);
        Assert.Equal(100, scale.LandFrom, 9);
        Assert.Equal(HeightmapRenderer.LandBands[0], lut[1000]);
        Assert.Equal(HeightmapRenderer.LandBands[3], lut[1350]);
        Assert.Equal(HeightmapRenderer.LandBands[5], lut[1600]);
    }

    [Fact]
    public void Legend_Layered_HighestLandFirstThenCoastThenDeeperWater()
    {
        var stats = new HeightmapStats(0, 1200, 600, 2, 0, 1200);
        LayeredScale scale = HeightmapRenderer.CreateLayeredScale(stats, Scale, 60);

        IReadOnlyList<LegendEntry> legend = HeightmapRenderer.BuildLegend(HeightmapView.Layered, stats, Scale, scale);

        Assert.Equal(13, legend.Count);
        Assert.Equal(new LegendEntry(LegendKind.Land, HeightmapRenderer.LandBands[5], 110, 120), Round(legend[0]));
        Assert.Equal(new LegendEntry(LegendKind.Land, HeightmapRenderer.LandBands[0], 60, 70), Round(legend[5]));
        Assert.Equal(new LegendEntry(LegendKind.Coast, HeightmapRenderer.CoastColor, 60, 60), legend[6]);
        Assert.Equal(new LegendEntry(LegendKind.Water, HeightmapRenderer.WaterBands[0], 60, 50), Round(legend[7]));
        Assert.Equal(new LegendEntry(LegendKind.Water, HeightmapRenderer.WaterBands[5], 10, 0), Round(legend[12]));

        static LegendEntry Round(LegendEntry e) => e with { From = Math.Round(e.From, 6), To = Math.Round(e.To, 6) };
    }

    [Fact]
    public void Legend_Gray_BlackAndWhiteEnds()
    {
        var stats = new HeightmapStats(0, 1200, 600, 2, 0, 1200);

        IReadOnlyList<LegendEntry> raw = HeightmapRenderer.BuildLegend(HeightmapView.Raw, stats, Scale, null);
        IReadOnlyList<LegendEntry> stretched = HeightmapRenderer.BuildLegend(HeightmapView.Stretched, stats with { StretchLow = 100, StretchHigh = 1100 }, Scale, null);

        Assert.Equal([Scale, 0], raw.Select(e => e.From));
        Assert.Equal([LegendKind.White, LegendKind.Black], raw.Select(e => e.Kind));
        Assert.Equal([110, 10], stretched.Select(e => Math.Round(e.From, 6)));
        Assert.Equal([LegendKind.WhiteAtLeast, LegendKind.BlackAtMost], stretched.Select(e => e.Kind));
    }

    [Fact]
    public void RenderLayered_DrawsCoastOnLandNextToWater()
    {
        // 5×3：左两列是水（原始值 0），右三列是陆地（1000）；海平面门槛 500
        ushort[] samples =
        [
            0, 0, 1000, 1000, 1000,
            0, 0, 1000, 1000, 1000,
            0, 0, 1000, 1000, 1000,
        ];
        var stats = HeightmapStats.Compute(samples);
        LayeredScale scale = HeightmapRenderer.CreateLayeredScale(stats, Scale, 50);
        uint[] lut = HeightmapRenderer.BuildLayeredLut(scale, Scale);
        uint[] destination = new uint[samples.Length];

        HeightmapRenderer.Render(samples, 5, 3, lut, scale.SeaThreshold, destination);

        for (int y = 0; y < 3; y++)
        {
            Assert.Equal(lut[0], destination[y * 5]);
            Assert.Equal(lut[0], destination[y * 5 + 1]);
            Assert.Equal(HeightmapRenderer.CoastColor, destination[y * 5 + 2]);
            Assert.Equal(lut[1000], destination[y * 5 + 3]);
        }
    }

    [Fact]
    public void RenderLayered_NoCoastWithoutWater()
    {
        ushort[] samples = [1000, 2000, 3000, 4000];
        LayeredScale scale = HeightmapRenderer.CreateLayeredScale(HeightmapStats.Compute(samples), Scale, 0);
        uint[] lut = HeightmapRenderer.BuildLayeredLut(scale, Scale);
        uint[] destination = new uint[4];

        HeightmapRenderer.Render(samples, 2, 2, lut, scale.SeaThreshold, destination);

        Assert.DoesNotContain(HeightmapRenderer.CoastColor, destination);
    }

    [Fact]
    public void Bands_AreTheDocumentedSteps()
    {
        Assert.Equal(HeightmapRenderer.BandCount, HeightmapRenderer.WaterBands.Count);
        Assert.Equal(HeightmapRenderer.BandCount, HeightmapRenderer.LandBands.Count);
        Assert.All(HeightmapRenderer.WaterBands.Concat(HeightmapRenderer.LandBands), c => Assert.Equal(0xFF000000u, c & 0xFF000000u));
    }
}
