// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Colossal.PSI.Environment;
using Colossal.Serialization.Entities;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.Tools
{
    /// <summary>
    /// CellMap 校验和导出工具（开发者用）。
    ///
    /// <para><b>用途</b>：同一存档在不同 Mod 版本下加载后，逐图比对 CellMap 数据是否一致。
    /// 重构二（ModeA–D 合并）的旧档兼容与降级保护都以此为验收依据。</para>
    ///
    /// <para><b>读法</b>：反射读取每个系统自己的 <c>CellMapSystem&lt;T&gt;.m_Map</c>，不经 <c>GetMap</c>。
    /// 原版系统的 <c>GetMap</c> 被替换系统的 Harmony 前缀重定向，经它读到的是替换系统的缓冲区，
    /// 原版系统自己那份（仍随存档读写的原稿）就看不到了。</para>
    ///
    /// <para><b>时机</b>：先开启游戏的「加载后暂停」，加载存档后在模拟推进前导出；
    /// 报告里的模拟帧号用来确认两次导出处于同一时点。</para>
    ///
    /// <para><b>输出</b>：写入 MapExtPDX.log，并另存到 {UserData}/ModsData/MapExt2/CellMapChecksum。</para>
    /// </summary>
    public static class CellMapChecksumExporter
    {
        private const string Tag = nameof(CellMapChecksumExporter);

        // === Constants ===

        #region Constants

        /// <summary>另存目录名，位于 {UserData}/ModsData/MapExt2 下。</summary>
        private const string kReportFolder = "CellMapChecksum";

        #endregion

        // === Fields ===

        #region Fields

        /// <summary>泛型哈希方法的定义，按各系统的格子类型构造。</summary>
        private static readonly MethodInfo s_HashMapDefinition =
            typeof(CellMapChecksumExporter).GetMethod(nameof(HashMap), BindingFlags.NonPublic | BindingFlags.Static);

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// 计算当前世界中全部 CellMap 的长度与哈希，写入 MapExtPDX.log。
        /// </summary>
        /// <returns>供设定页显示的单行结果摘要。</returns>
        public static string Export()
        {
            try
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    ModLog.Warn(Tag, "World 尚未就绪，请先加载存档。");
                    return "World not ready — load a save first.";
                }

                var lines = new List<string>();
                foreach (var system in world.Systems)
                {
                    Type cellType = FindCellType(system.GetType());
                    if (cellType == null) continue;

                    lines.Add((string)s_HashMapDefinition.MakeGenericMethod(cellType)
                        .Invoke(null, new object[] { system }));
                }

                // 按格子类型与系统类型排序，两次导出可直接逐行比对
                lines.Sort(StringComparer.Ordinal);

                uint frame = world.GetExistingSystemManaged<SimulationSystem>()?.frameIndex ?? 0;
                var report = new List<string>
                {
                    $"模式: {PatchManager.CurrentMode} (CV={PatchManager.CurrentCoreValue})",
                    $"模拟帧: {frame}",
                    $"CellMap 数量: {lines.Count}",
                };
                report.AddRange(lines);

                ModLog.Report(Tag, "CellMap 校验和", rb =>
                {
                    foreach (string line in report) rb.Add(line);
                });

                string file = WriteReportFile(report, frame);
                return file == null
                    ? $"Hashed {lines.Count} CellMaps at frame {frame} — see MapExtPDX.log (file copy failed)"
                    : $"Hashed {lines.Count} CellMaps at frame {frame} — ModsData/MapExt2/{kReportFolder}/{file}";
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "CellMap 校验和导出失败。");
                return $"Export failed: {ex.Message}";
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>沿继承链找到 <c>CellMapSystem&lt;T&gt;</c> 并返回格子类型 T；不是 CellMap 系统则返回 null。</summary>
        private static Type FindCellType(Type type)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(CellMapSystem<>))
                    return t.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>
        /// 先完成写入依赖，再读取单个系统的 m_Map，
        /// 输出“格子类型 | 系统类型 | 长度 | 元素字节数 | 哈希”一行。
        /// </summary>
        private static unsafe string HashMap<T>(ComponentSystemBase system) where T : struct, ISerializable
        {
            string head = $"{typeof(T).Name} | {system.GetType().FullName}";

            FieldInfo mapField = AccessTools.Field(typeof(CellMapSystem<T>), "m_Map");
            FieldInfo writeDepsField = AccessTools.Field(typeof(CellMapSystem<T>), "m_WriteDependencies");
            if (mapField == null || writeDepsField == null)
                return $"{head} | 字段缺失（m_Map／m_WriteDependencies）";

            ((JobHandle)writeDepsField.GetValue(system)).Complete();

            var map = (NativeArray<T>)mapField.GetValue(system);
            if (!map.IsCreated)
                return $"{head} | 未创建";

            int stride = UnsafeUtility.SizeOf<T>();
            uint2 hash = xxHash3.Hash64(NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(map), (long)map.Length * stride);
            return $"{head} | len={map.Length} | elem={stride}B | {hash.y:X8}{hash.x:X8}";
        }

        /// <summary>
        /// 另存一份到 {UserData}/ModsData/MapExt2/CellMapChecksum。日志在每次启动游戏时被覆盖，
        /// 轮流切换模式、重启采集基线时靠这份文件保留每次的结果。失败只记警告，日志中的报告不受影响。
        /// </summary>
        /// <returns>写出的文件名；失败时返回 null。</returns>
        private static string WriteReportFile(List<string> lines, uint frame)
        {
            try
            {
                string dir = Path.Combine(EnvPath.kUserDataPath, "ModsData", "MapExt2", kReportFolder);
                Directory.CreateDirectory(dir);
                string name = $"checksum-{DateTime.Now:yyyyMMdd-HHmmss}-{PatchManager.CurrentMode}-f{frame}.txt";
                File.WriteAllLines(Path.Combine(dir, name), lines, new UTF8Encoding(false));
                ModLog.Ok(Tag, $"已另存：{Path.Combine(dir, name)}");
                return name;
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"另存文件失败：{ex.Message}");
                return null;
            }
        }

        #endregion
    }
}
