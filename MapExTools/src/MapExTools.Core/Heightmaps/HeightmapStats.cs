namespace MapExTools.Core.Heightmaps;

/// <summary>
/// 高度图原始值（16 位，游戏导入时用的那个通道）的统计。
/// <para><see cref="StretchLow"/>、<see cref="StretchHigh"/> 是自动拉伸用的 0.5% 与 99.5% 分位：高度图常有少数山峰或深谷，
/// 按最低到最高值拉伸会把大部分地形压在很窄的灰度里（本机一张图平均 179 m、最高 1385 m，拉伸后几乎全黑）。</para>
/// </summary>
public sealed record HeightmapStats(ushort Min, ushort Max, double Mean, long Count, ushort StretchLow, ushort StretchHigh)
{
    /// <summary>自动拉伸两端各裁掉的比例。</summary>
    public const double StretchClip = 0.005;

    /// <summary>统计全部像素。</summary>
    /// <exception cref="ArgumentException">没有像素。</exception>
    public static HeightmapStats Compute(ReadOnlySpan<ushort> samples)
    {
        if (samples.IsEmpty)
            throw new ArgumentException("没有像素", nameof(samples));

        int[] histogram = new int[HeightmapRules.MaxRaw + 1];
        long sum = 0;
        foreach (ushort value in samples)
        {
            histogram[value]++;
            sum += value;
        }

        return FromHistogram(histogram, sum, samples.Length);
    }

    private static HeightmapStats FromHistogram(int[] histogram, long sum, long count)
    {
        int min = Array.FindIndex(histogram, c => c > 0);
        int max = Array.FindLastIndex(histogram, c => c > 0);
        long lowCount = (long)Math.Ceiling(count * StretchClip);
        long highCount = (long)Math.Ceiling(count * (1 - StretchClip));
        int low = Percentile(histogram, lowCount);
        int high = Percentile(histogram, highCount);
        if (high <= low)
        {
            // 绝大部分像素同值（如平地）：分位数拉不开，退回最低到最高
            low = min;
            high = max;
        }

        return new HeightmapStats((ushort)min, (ushort)max, (double)sum / count, count, (ushort)low, (ushort)high);
    }

    /// <summary>
    /// 逐个累计像素的统计。超过预览尺寸的高度图解码时只保留降采样后的像素，统计要在降采样的同时按源图全部像素累计，
    /// 否则盒式平均会抹平最高、最低点与分位数。结果与对同一批像素调用 <see cref="Compute"/> 相同。
    /// </summary>
    public sealed class Accumulator
    {
        private readonly int[] _histogram = new int[HeightmapRules.MaxRaw + 1];
        private long _sum;
        private long _count;

        public void Add(ushort value)
        {
            _histogram[value]++;
            _sum += value;
            _count++;
        }

        /// <exception cref="InvalidOperationException">还没有累计任何像素。</exception>
        public HeightmapStats Build() =>
            _count == 0 ? throw new InvalidOperationException("没有像素") : FromHistogram(_histogram, _sum, _count);
    }

    public double MinElevation(double heightScale) => HeightmapRules.ToElevation(Min, heightScale);

    public double MaxElevation(double heightScale) => HeightmapRules.ToElevation(Max, heightScale);

    public double MeanElevation(double heightScale) => Mean / HeightmapRules.MaxRaw * heightScale;

    /// <summary>累计像素数达到 <paramref name="count"/> 的最小原始值。</summary>
    private static int Percentile(int[] histogram, long count)
    {
        long cumulative = 0;
        for (int value = 0; value < histogram.Length; value++)
        {
            cumulative += histogram[value];
            if (cumulative >= count)
                return value;
        }

        return histogram.Length - 1;
    }
}
