// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Diagnostics;
using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.SceneFlow;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Collections;
using Unity.Jobs;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// CellMap 存读档计时（临时诊断工具，v4.12.0 整合计划 Phase 0.4；计划结案前撤下）。
    ///
    /// <para><b>用途</b>：决定 4.6"存读档 Job 走 Burst"做不做。任一模式下，一次存档或读档里
    /// 各 CellMap 替换系统合计 ≥ 1 s 才做，否则只把测量值写进计划。</para>
    ///
    /// <para><b>做法</b>：设定页 Debug 分页开启后，<c>CellMapReplacementSystem</c> 的 Serialize／Deserialize
    /// 先完成依赖（引擎包在外层的区段头 Job、模拟中的写入，读档时还有解压与实体创建），再计"调度＋完成"，
    /// 测到的就是本系统存读档 Job 自身的执行时长。引擎原本让各系统的 Job 并行，这里逐个同步执行，
    /// 所以合计值是这些 Job 的总工作量，不是玩家多等的时间。Job 的输入与常规路径相同，存档内容不变；
    /// 存读档时会阻塞主线程，只在开关打开时走。</para>
    ///
    /// <para><b>输出</b>（MapExtPDX.log）：每个系统一行耗时与字节数。字节数是本系统区段在存档流里的
    /// 未压缩字节数，取写入缓冲区的长度差或读取位置差。存档、读档结束时各一行合计，附整次存档
    /// （onGameSaveLoad 开始到结束）或整次读档（onGamePreload 到 onGameLoadingComplete）的总耗时。</para>
    /// </summary>
    public static class CellMapSerializeTimer
    {
        private const string Tag = nameof(CellMapSerializeTimer);

        // === Constants ===

        #region Constants

        private const string kSave = "存档";
        private const string kLoad = "读档";

        #endregion

        // === Fields ===

        #region Fields

        /// <summary>本系统区段的写入缓冲区（引擎给每个系统一个），存档 Job 前后的长度差就是写出的字节数。</summary>
        private static readonly FieldInfo s_WriterBuffer = AccessTools.Field(typeof(EntityWriterData), "m_Buffer");

        /// <summary>读取位置，读档 Job 前后的差就是读入的字节数。</summary>
        private static readonly FieldInfo s_ReaderPosition = AccessTools.Field(typeof(EntityReaderData), "m_Position");

        /// <summary>是否已订阅 GameManager 的存读档事件；只订阅一次。</summary>
        private static bool s_Listening;

        /// <summary>进行中的会话（<see cref="kSave"/> 或 <see cref="kLoad"/>）；null 表示没有。只在主线程读写。</summary>
        private static string s_Session;

        private static long s_SessionStart;
        private static int s_Count;
        private static double s_SumMs;
        private static long s_SumBytes;

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>设定页 Debug 分页的"CellMap 存读档计时"是否开启。</summary>
        public static bool Enabled => Mod.Instance?.Settings?.CellMapSerializeProfiling == true;

        /// <summary>
        /// 开关打开时由设定调用：订阅 GameManager 的存读档事件，用来记整次存档、读档的总耗时。
        /// 只订阅一次；开关再关上时事件照收，但 <see cref="Enabled"/> 为假，不再记录。
        /// </summary>
        public static void EnsureListening()
        {
            if (s_Listening) return;
            GameManager gm = GameManager.instance;
            if (gm == null)
            {
                ModLog.Warn(Tag, "GameManager 尚未就绪，整次存读档的总耗时不会记录；各系统的计时不受影响。");
                return;
            }

            gm.onGameSaveLoad += OnGameSaveLoad;
            gm.onGamePreload += OnGamePreload;
            gm.onGameLoadingComplete += OnGameLoadingComplete;
            s_Listening = true;
            ModLog.Info(Tag, "已开启：CellMap 存读档 Job 改为同步执行并逐个计时，结果写入本日志。");
            if (s_WriterBuffer == null || s_ReaderPosition == null)
                ModLog.Warn(Tag, "取不到 EntityWriterData.m_Buffer 或 EntityReaderData.m_Position，字节数不记。");
        }

        /// <summary>完成依赖后同步执行存档 Job 并计时，返回已完成的句柄。</summary>
        public static JobHandle RunSerialize<TJob>(TJob job, JobHandle dependsOn, EntityWriterData writerData,
            string system, int textureSize) where TJob : struct, IJob
        {
            dependsOn.Complete();
            long before = WrittenBytes(writerData);
            long start = Stopwatch.GetTimestamp();
            JobHandle handle = job.Schedule(dependsOn);
            handle.Complete();
            long end = Stopwatch.GetTimestamp();
            Record(kSave, system, textureSize, start, end, Delta(before, WrittenBytes(writerData)));
            return handle;
        }

        /// <summary>完成依赖后同步执行读档 Job 并计时，返回已完成的句柄。</summary>
        public static JobHandle RunDeserialize<TJob>(TJob job, JobHandle dependsOn, EntityReaderData readerData,
            string system, int textureSize) where TJob : struct, IJob
        {
            dependsOn.Complete();
            long before = ReadPosition(readerData);
            long start = Stopwatch.GetTimestamp();
            JobHandle handle = job.Schedule(dependsOn);
            handle.Complete();
            long end = Stopwatch.GetTimestamp();
            Record(kLoad, system, textureSize, start, end, Delta(before, ReadPosition(readerData)));
            return handle;
        }

        #endregion

        // === Helpers ===

        #region Helpers

        private static void OnGameSaveLoad(string saveName, string previewUri, bool start, bool success)
        {
            if (!Enabled) return;
            if (start) BeginSession(kSave);
            else EndSession(kSave, success ? "成功" : "失败");
        }

        private static void OnGamePreload(Purpose purpose, GameMode mode)
        {
            if (Enabled) BeginSession(kLoad);
        }

        private static void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            if (Enabled) EndSession(kLoad, purpose.ToString());
        }

        private static void BeginSession(string kind)
        {
            if (s_Session != null)
                ModLog.Warn(Tag, $"上一次{s_Session}没有收到结束事件，丢弃它的合计（已记 {s_Count} 个系统）。");

            s_Session = kind;
            s_SessionStart = Stopwatch.GetTimestamp();
            s_Count = 0;
            s_SumMs = 0;
            s_SumBytes = 0;
        }

        private static void EndSession(string kind, string detail)
        {
            if (s_Session != kind) return;
            double total = ToMs(s_SessionStart, Stopwatch.GetTimestamp());

            // 回主菜单（Cleanup）也走读档流程，但一张 CellMap 都不读，这种不记
            if (s_Count > 0 || kind == kSave)
            {
                string sums = FormattableString.Invariant($"{s_Count} 个 CellMap 系统合计 {s_SumMs:F1} ms、{FormatBytes(s_SumBytes)}");
                ModLog.Scan(Tag, FormattableString.Invariant($"{kind}结束（{detail}，{PatchManager.CurrentMode}）：{sums}；整次{kind} {total:F0} ms"));
            }

            s_Session = null;
        }

        private static void Record(string kind, string system, int textureSize, long start, long end, long bytes)
        {
            double ms = ToMs(start, end);
            if (s_Session == kind)
            {
                s_Count++;
                s_SumMs += ms;
                if (bytes > 0) s_SumBytes += bytes;
            }

            ModLog.Scan(Tag, FormattableString.Invariant($"{kind} {system}（{textureSize}²）：{ms:F2} ms，{FormatBytes(bytes)}"));
        }

        private static long WrittenBytes(EntityWriterData writerData) =>
            s_WriterBuffer == null ? -1 : ((NativeList<byte>)s_WriterBuffer.GetValue(writerData)).Length;

        private static long ReadPosition(EntityReaderData readerData) =>
            s_ReaderPosition == null ? -1 : ((NativeReference<int>)s_ReaderPosition.GetValue(readerData)).Value;

        private static long Delta(long before, long after) => before < 0 || after < 0 ? -1 : after - before;

        private static double ToMs(long start, long end) => (end - start) * 1000.0 / Stopwatch.Frequency;

        private static string FormatBytes(long bytes) =>
            bytes < 0 ? "字节数未知" : FormattableString.Invariant($"{bytes / 1048576.0:F2} MiB（{bytes:N0} B）");

        #endregion
    }
}
