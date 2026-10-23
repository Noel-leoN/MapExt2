// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System.Collections.Generic;
using System.Diagnostics;
using MapExtPDX.MapExt.Core;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// CellMap Job 诊断计时（临时工具，ModeC 标定计划 Phase 3.2；计划结案前撤下）。
    ///
    /// <para><b>做法</b>：设定页 Debug 分页开启后，受测系统先收敛输入依赖，再逐个调度并立即完成各 Job，
    /// 用 Stopwatch 计时。单线程 Job 测到的就是它的执行时长；并行 Job 测到的是主线程等它全部完成的墙钟时间。
    /// 会阻塞主线程，只在明确开启时走。Job 的输入与随机种子都和常规路径相同，不改变模拟数值。</para>
    ///
    /// <para><b>输出</b>：每个 Job 累计到指定次数后，把平均与最大值写入 MapExtPDX.log。</para>
    /// </summary>
    public static class CellMapJobTimer
    {
        private const string Tag = nameof(CellMapJobTimer);

        // === Fields ===

        #region Fields

        private struct Stats
        {
            public int Count;
            public double Sum;
            public double Max;
        }

        /// <summary>按“系统.Job”累计；只在主线程的 OnUpdate 里调用，不需要加锁。</summary>
        private static readonly Dictionary<string, Stats> s_Stats = new Dictionary<string, Stats>();

        private struct Comparison
        {
            public int Count;
            public int Mismatched;   // 有差异的次数
            public long MaxCells;    // 单次最多的差异格数
            public string Last;      // 最近一次的明细
        }

        /// <summary>新旧写法的对照，按“系统.Job”累计。</summary>
        private static readonly Dictionary<string, Comparison> s_Comparisons = new Dictionary<string, Comparison>();

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>设定页 Debug 分页的“CellMap 计时”是否开启。</summary>
        public static bool Enabled => Mod.Instance?.Settings?.CellMapProfiling == true;

        /// <summary>当前时间戳，与 <see cref="Record"/> 配对使用。</summary>
        public static long Now() => Stopwatch.GetTimestamp();

        /// <summary>
        /// 记录一次耗时；累计 <paramref name="samplesPerReport"/> 次后写一行平均与最大值，然后清零重新累计。
        /// </summary>
        public static void Record(string system, string job, int textureSize, long startTicks, long endTicks,
            int samplesPerReport)
        {
            double ms = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
            string key = system + "." + job;
            s_Stats.TryGetValue(key, out Stats s);
            s.Count++;
            s.Sum += ms;
            if (ms > s.Max) s.Max = ms;

            if (s.Count >= samplesPerReport)
            {
                ModLog.Scan(Tag, $"{key}（{textureSize}²）：平均 {s.Sum / s.Count:F2} ms，最大 {s.Max:F2} ms（{s.Count} 次）");
                s = default;
            }

            s_Stats[key] = s;
        }

        /// <summary>
        /// 记录一次新旧写法的逐格对照（ModeC 标定计划 Phase 4）：<paramref name="cells"/> 是有差异的格数，
        /// <paramref name="detail"/> 是这一次的明细。累计 <paramref name="samplesPerReport"/> 次后写一行汇总。
        /// </summary>
        public static void Compare(string system, string job, long cells, string detail, int samplesPerReport)
        {
            string key = system + "." + job;
            s_Comparisons.TryGetValue(key, out Comparison c);
            c.Count++;
            if (cells != 0) c.Mismatched++;
            if (cells > c.MaxCells) c.MaxCells = cells;
            c.Last = detail;

            if (c.Count >= samplesPerReport)
            {
                ModLog.Scan(Tag, $"{key} 新旧对照：{c.Count} 次中 {c.Mismatched} 次有差异，单次最多 {c.MaxCells} 格；最近一次 {c.Last}");
                c = default;
            }

            s_Comparisons[key] = c;
        }

        #endregion
    }
}
