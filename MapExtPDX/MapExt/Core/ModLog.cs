// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>
    /// 全局统一日志门面。
    /// 所有系统/Patch 统一通过此类输出日志，确保格式一致性。
    /// 格式: [MapExt|Tag] Icon message
    /// </summary>
    public static class ModLog
    {
        #region 图标常量

        // 使用彩色 Emoji 图标，在日志查看器中更醒目
        private const string IconInfo  = "🔹";  // 普通信息
        private const string IconOk    = "✅";  // 成功/完成
        private const string IconWarn  = "⚠️";  // 警告
        private const string IconError = "❌";  // 错误
        private const string IconPatch = "🔧";  // 补丁操作
        private const string IconScan  = "🔍";  // 扫描/搜索
        private const string IconSwap  = "🔄";  // 替换
        private const string IconDebug = "🔸";  // DEBUG 级别

        #endregion

        #region 格式模板

        /// <summary>
        /// 统一格式: [MapExt|tag] icon message
        /// </summary>
        private static string Fmt(string tag, string icon, string msg)
            => $"[MapExt|{tag}] {icon} {msg}";

        #endregion

        #region 标准日志方法

        /// <summary>普通信息</summary>
        public static void Info(string tag, string msg)
            => Mod.Logger.Info(Fmt(tag, IconInfo, msg));

        /// <summary>成功/完成</summary>
        public static void Ok(string tag, string msg)
            => Mod.Logger.Info(Fmt(tag, IconOk, msg));

        /// <summary>补丁操作</summary>
        public static void Patch(string tag, string msg)
            => Mod.Logger.Info(Fmt(tag, IconPatch, msg));

        /// <summary>扫描/搜索操作</summary>
        public static void Scan(string tag, string msg)
            => Mod.Logger.Info(Fmt(tag, IconScan, msg));

        /// <summary>替换操作</summary>
        public static void Swap(string tag, string msg)
            => Mod.Logger.Info(Fmt(tag, IconSwap, msg));

        /// <summary>警告</summary>
        public static void Warn(string tag, string msg)
            => Mod.Logger.Warn(Fmt(tag, IconWarn, msg));

        /// <summary>错误</summary>
        public static void Error(string tag, string msg)
            => Mod.Logger.Error(Fmt(tag, IconError, msg));

        /// <summary>错误（含异常）</summary>
        public static void Error(string tag, Exception e, string msg)
            => Mod.Logger.Error(e, Fmt(tag, IconError, msg));

        /// <summary>DEBUG 级别（仅 Debug 构建输出）</summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Debug(string tag, string msg)
            => Mod.Logger.Debug(Fmt(tag, IconDebug, msg));

        #endregion

        #region 分组报告

        /// <summary>
        /// 输出一个带标题的分组报告块。
        /// 用于 PatchSet3 等需要输出多行汇总的场景。
        /// </summary>
        public static void Report(string tag, string title, Action<ReportBuilder> build)
        {
            var rb = new ReportBuilder();
            build(rb);

            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"┌─── {title} ───");
            foreach (var line in rb.Lines)
                sb.AppendLine($"│ {line}");
            sb.Append($"└─── {rb.Lines.Count} entries ───");

            Mod.Logger.Info(Fmt(tag, IconOk, sb.ToString()));
        }

        /// <summary>
        /// Report 构建器，用于在 lambda 中添加行
        /// </summary>
        public class ReportBuilder
        {
            internal readonly List<string> Lines = new List<string>();

            /// <summary>添加一行自由文本</summary>
            public void Add(string line) => Lines.Add(line);

            /// <summary>添加 key: value 格式的统计行</summary>
            public void Stat(string key, object value) => Lines.Add($"{key}: {value}");

            /// <summary>添加带替换图标的条目</summary>
            public void Item(string detail) => Lines.Add($"  {IconSwap} {detail}");
        }

        #endregion

        #region 工具方法

        // 正则: 匹配 [[完整程序集限定名]] 格式的泛型参数，提取短名称
        // 例: CellMapSystem`1[[Game.Simulation.AirPollution, Game, Version=...]] → CellMapSystem<AirPollution>
        private static readonly Regex GenericArgRegex = new Regex(
            @"`\d+\[\[([^,\]]+)(?:,[^\]]*)\]\]",
            RegexOptions.Compiled);

        /// <summary>
        /// 将 .NET 反射产生的冗长泛型类型名缩短为可读形式。
        /// <para>
        /// 例: "Game.Simulation.CellMapSystem`1[[Game.Simulation.AirPollution, Game, Version=0.0.0.0, ...]]"
        /// → "CellMapSystem&lt;AirPollution&gt;"
        /// </para>
        /// </summary>
        public static string ShortenTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;

            // 1. 替换泛型参数 [[FullyQualified, Assembly, ...]] → <ShortName>
            string result = GenericArgRegex.Replace(fullName, m =>
            {
                string qualifiedName = m.Groups[1].Value; // e.g. "Game.Simulation.AirPollution"
                int lastDot = qualifiedName.LastIndexOf('.');
                string shortName = lastDot >= 0 ? qualifiedName.Substring(lastDot + 1) : qualifiedName;
                return $"<{shortName}>";
            });

            // 2. 去掉命名空间前缀 (保留最后一个 . 后面的部分)
            //    但要避免截断 "::" 分隔的方法名部分
            int colonIdx = result.IndexOf("::", StringComparison.Ordinal);
            if (colonIdx > 0)
            {
                string typePart = result.Substring(0, colonIdx);
                string methodPart = result.Substring(colonIdx); // "::MethodName"
                int lastDot = typePart.LastIndexOf('.');
                if (lastDot >= 0) typePart = typePart.Substring(lastDot + 1);
                result = typePart + methodPart;
            }
            else
            {
                int lastDot = result.LastIndexOf('.');
                if (lastDot >= 0) result = result.Substring(lastDot + 1);
            }

            return result;
        }

        #endregion
    }
}
