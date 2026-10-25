// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Colossal.PSI.Environment;
using Colossal.Serialization.Entities;
using Game.Prefabs;
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
    /// CellMap 逐图层统计导出工具（开发者用）。
    ///
    /// <para><b>用途</b>：ModeC 标定计划 Phase 1.2。比较原版尺寸与扩展模式下同样布局的城市，
    /// 各图层的覆盖面积与峰值；Phase 3 改贴图倍率后复测也用它。</para>
    ///
    /// <para><b>内容</b>：每张启用中的 CellMap，逐个数值字段统计非零格数、非零面积、最小值、最大值与总和；
    /// 三张污染图另附幸福度扣 1 分起与扣满的面积；最后附实际生效的污染、幸福度与吸引力参数。
    /// 被禁用的系统只列名、不统计：扩展模式下原版系统被禁用，
    /// 它的 m_Map 是随存档读写的原稿，不参与模拟。</para>
    ///
    /// <para><b>读法</b>：与 <see cref="CellMapChecksumExporter"/> 相同，反射读 m_Map，读前先完成写入依赖。</para>
    ///
    /// <para><b>格距</b>：地图边长按 14336 × CurrentCoreValue 计算。本程序集不在 PatchSet3 的扫描范围内，
    /// 在这里读 <c>CellMapSystem&lt;T&gt;.kMapSize</c> 得到的仍是原版的 14336。</para>
    ///
    /// <para><b>输出</b>：写入 MapExtPDX.log，并另存到 {UserData}/ModsData/MapExt2/CellMapStats。</para>
    /// </summary>
    public static class CellMapStatsExporter
    {
        private const string Tag = nameof(CellMapStatsExporter);

        // === Constants ===

        #region Constants

        /// <summary>另存目录名，位于 {UserData}/ModsData/MapExt2 下。</summary>
        private const string kReportFolder = "CellMapStats";

        /// <summary>原版地图边长（米）。</summary>
        private const int kVanillaMapSize = 14336;

        #endregion

        // === Fields ===

        #region Fields

        /// <summary>泛型统计方法的定义，按各系统的格子类型构造。</summary>
        private static readonly MethodInfo s_StatsMapDefinition =
            typeof(CellMapStatsExporter).GetMethod(nameof(StatsMap), BindingFlags.NonPublic | BindingFlags.Static);

        #endregion

        // === Public API ===

        #region Public API

        /// <summary>
        /// 统计当前世界中启用的全部 CellMap，连同实际参数一起写入 MapExtPDX.log。
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

                int mapSize = kVanillaMapSize * PatchManager.CurrentCoreValue;
                EntityManager entityManager = world.EntityManager;
                int3 penaltyThresholds = GetPenaltyThresholds(entityManager);
                var blocks = new List<(string Key, List<string> Lines)>();
                var disabled = new List<string>();
                foreach (var system in world.Systems)
                {
                    Type cellType = CellMapChecksumExporter.FindCellType(system.GetType());
                    if (cellType == null) continue;

                    if (!system.Enabled)
                    {
                        disabled.Add(system.GetType().Name);
                        continue;
                    }

                    var lines = (List<string>)s_StatsMapDefinition.MakeGenericMethod(cellType)
                        .Invoke(null, new object[] { system, mapSize, penaltyThresholds });
                    blocks.Add(($"{cellType.Name} | {system.GetType().FullName}", lines));
                }

                // 按格子类型与系统类型排序，各次导出可直接逐段对照
                blocks.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                disabled.Sort(StringComparer.Ordinal);

                uint frame = world.GetExistingSystemManaged<SimulationSystem>()?.frameIndex ?? 0;
                var report = new List<string>
                {
                    $"模式: {PatchManager.CurrentMode} (CV={PatchManager.CurrentCoreValue})",
                    $"模拟帧: {frame}",
                    $"地图边长: {mapSize} m",
                    $"已统计 CellMap: {blocks.Count}",
                };
                if (disabled.Count > 0)
                    report.Add($"未统计（已禁用）: {string.Join(", ", disabled)}");

                foreach (var block in blocks) report.AddRange(block.Lines);

                report.Add("实际参数:");
                AppendSingleton<PollutionParameterData>(entityManager, report);
                AppendSingleton<CitizenHappinessParameterData>(entityManager, report);
                AppendSingleton<AttractivenessParameterData>(entityManager, report);

                ModLog.Report(Tag, "CellMap 统计", rb =>
                {
                    foreach (string line in report) rb.Add(line);
                });

                string file = WriteReportFile(report, frame);
                return file == null
                    ? $"Stats for {blocks.Count} CellMaps at frame {frame} — see MapExtPDX.log (file copy failed)"
                    : $"Stats for {blocks.Count} CellMaps at frame {frame} — ModsData/MapExt2/{kReportFolder}/{file}";
            }
            catch (Exception ex)
            {
                ModLog.Error(Tag, ex, "CellMap 统计导出失败。");
                return $"Export failed: {ex.Message}";
            }
        }

        #endregion

        // === Helpers ===

        #region Helpers

        /// <summary>格子结构里的一个数值字段：路径、相对格子起点的字节偏移与类型。</summary>
        private readonly struct Leaf
        {
            public readonly string Path;
            public readonly int Offset;
            public readonly TypeCode Code;

            public Leaf(string path, int offset, TypeCode code)
            {
                Path = path;
                Offset = offset;
                Code = code;
            }
        }

        /// <summary>
        /// 先完成写入依赖，再统计单个系统的 m_Map。
        /// 首行是“格子类型 | 系统类型 | 边长 | 格距”，其后每个数值字段一行；
        /// 三张污染图另加一行幸福度扣分的面积（阈值见 <see cref="GetPenaltyThresholds"/>）。
        /// </summary>
        private static unsafe List<string> StatsMap<T>(ComponentSystemBase system, int mapSize, int3 penaltyThresholds)
            where T : struct, ISerializable
        {
            var lines = new List<string>();
            string head = $"{typeof(T).Name} | {system.GetType().FullName}";

            FieldInfo mapField = AccessTools.Field(typeof(CellMapSystem<T>), "m_Map");
            FieldInfo writeDepsField = AccessTools.Field(typeof(CellMapSystem<T>), "m_WriteDependencies");
            if (mapField == null || writeDepsField == null)
            {
                lines.Add($"{head} | 字段缺失（m_Map／m_WriteDependencies）");
                return lines;
            }

            ((JobHandle)writeDepsField.GetValue(system)).Complete();

            var map = (NativeArray<T>)mapField.GetValue(system);
            if (!map.IsCreated || map.Length == 0)
            {
                lines.Add($"{head} | 未创建或为空");
                return lines;
            }

            // CellMap 都是正方形贴图，边长取长度的平方根
            int textureSize = (int)math.round(math.sqrt(map.Length));
            double cellSize = (double)mapSize / textureSize;
            double cellAreaKm2 = cellSize * cellSize / 1e6;
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0} | {1}² | 格距 {2:0.##} m", head, textureSize, cellSize));

            var leaves = new List<Leaf>();
            CollectLeaves(typeof(T), 0, "", leaves);

            int stride = UnsafeUtility.SizeOf<T>();
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(map);
            foreach (Leaf leaf in leaves)
            {
                long nonZero = 0;
                double min = double.MaxValue;
                double max = double.MinValue;
                double sum = 0;
                byte* p = basePtr + leaf.Offset;
                for (int i = 0; i < map.Length; i++, p += stride)
                {
                    double v = ReadValue(p, leaf.Code);
                    if (v != 0) nonZero++;
                    if (v < min) min = v;
                    if (v > max) max = v;
                    sum += v;
                }

                lines.Add(string.Format(CultureInfo.InvariantCulture,
                    "  {0}: 非零 {1} 格（{2:0.###} km²），最小 {3:G6}，最大 {4:G6}，总和 {5:G8}",
                    leaf.Path, nonZero, nonZero * cellAreaKm2, min, max, sum));
            }

            // 污染图的幸福度扣分面积：非零面积会随导出时刻相对衰减更新的相位变化（外圈的小值格下一次衰减就清零），
            // 按扣分阈值统计不受这个影响，也直接对应玩家感受到的影响范围
            int2 thresholds = PenaltyThresholds<T>(penaltyThresholds);
            if (thresholds.x > 0)
            {
                foreach (Leaf leaf in leaves)
                {
                    if (leaf.Path != "m_Pollution") continue;

                    long atLeastOne = 0;
                    long saturated = 0;
                    byte* p = basePtr + leaf.Offset;
                    for (int i = 0; i < map.Length; i++, p += stride)
                    {
                        double v = ReadValue(p, leaf.Code);
                        if (v >= thresholds.x) atLeastOne++;
                        if (v >= thresholds.y) saturated++;
                    }

                    lines.Add(string.Format(CultureInfo.InvariantCulture,
                        "  m_Pollution 幸福度扣分：≥ {0}（扣 1 分起）{1} 格（{2:0.###} km²），≥ {3}（扣满）{4} 格（{5:0.###} km²）",
                        thresholds.x, atLeastOne, atLeastOne * cellAreaKm2, thresholds.y, saturated, saturated * cellAreaKm2));
                }
            }

            return lines;
        }

        /// <summary>
        /// 由幸福度参数得到扣分阈值：(除数, 空气与地面扣满的格值, 噪音扣满的格值)。
        /// 扣分 = 格值 ÷ 除数，封顶为上限（CitizenHappinessSystem）；读不到参数时全为 0，不统计扣分面积。
        /// </summary>
        private static int3 GetPenaltyThresholds(EntityManager entityManager)
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
            if (!query.TryGetSingleton(out CitizenHappinessParameterData data) || data.m_PollutionBonusDivisor <= 0)
                return int3.zero;

            int divisor = data.m_PollutionBonusDivisor;
            return new int3(divisor, divisor * data.m_MaxAirAndGroundPollutionBonus, divisor * data.m_MaxNoisePollutionBonus);
        }

        /// <summary>三张污染图各自的 (扣 1 分, 扣满) 阈值；其他图层返回 0。</summary>
        private static int2 PenaltyThresholds<T>(int3 penaltyThresholds)
        {
            if (typeof(T) == typeof(NoisePollution)) return new int2(penaltyThresholds.x, penaltyThresholds.z);
            if (typeof(T) == typeof(AirPollution) || typeof(T) == typeof(GroundPollution))
                return new int2(penaltyThresholds.x, penaltyThresholds.y);
            return int2.zero;
        }

        /// <summary>递归展开结构体字段（含 float2／float4 等向量的分量），按声明顺序收集全部数值字段。</summary>
        private static void CollectLeaves(Type type, int baseOffset, string prefix, List<Leaf> leaves)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                int offset = baseOffset + UnsafeUtility.GetFieldOffset(field);
                string path = prefix.Length == 0 ? field.Name : $"{prefix}.{field.Name}";
                Type fieldType = field.FieldType;

                // 枚举的 TypeCode 即底层整数类型
                TypeCode code = Type.GetTypeCode(fieldType);
                if (IsNumeric(code))
                    leaves.Add(new Leaf(path, offset, code));
                else if (fieldType.IsValueType && !fieldType.IsPrimitive)
                    CollectLeaves(fieldType, offset, path, leaves);
            }
        }

        private static bool IsNumeric(TypeCode code)
        {
            switch (code)
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                    return true;
                default:
                    return false;
            }
        }

        private static unsafe double ReadValue(byte* p, TypeCode code)
        {
            switch (code)
            {
                case TypeCode.Boolean: return *p != 0 ? 1 : 0;
                case TypeCode.Byte: return *p;
                case TypeCode.SByte: return *(sbyte*)p;
                case TypeCode.Int16: return *(short*)p;
                case TypeCode.UInt16: return *(ushort*)p;
                case TypeCode.Int32: return *(int*)p;
                case TypeCode.UInt32: return *(uint*)p;
                case TypeCode.Int64: return *(long*)p;
                case TypeCode.UInt64: return *(ulong*)p;
                case TypeCode.Single: return *(float*)p;
                case TypeCode.Double: return *(double*)p;
                default: return 0;
            }
        }

        /// <summary>读取参数单例，把数值字段写成一行（跳过 Entity 引用）；找不到唯一实例时写明数量。</summary>
        private static void AppendSingleton<T>(EntityManager entityManager, List<string> report)
            where T : unmanaged, IComponentData
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (!query.TryGetSingleton(out T data))
            {
                report.Add($"  {typeof(T).Name}: 未找到唯一实例（{query.CalculateEntityCount()} 个）");
                return;
            }

            object boxed = data;
            var parts = new List<string>();
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(Entity)) continue;
                parts.Add(string.Format(CultureInfo.InvariantCulture, "{0}={1}", field.Name, field.GetValue(boxed)));
            }

            report.Add($"  {typeof(T).Name}: {string.Join(", ", parts)}");
        }

        /// <summary>
        /// 另存一份到 {UserData}/ModsData/MapExt2/CellMapStats。日志在每次启动游戏时被覆盖，
        /// 轮流切换模式、重启采集对照数据时靠这份文件保留每次的结果。失败只记警告，日志中的报告不受影响。
        /// </summary>
        /// <returns>写出的文件名；失败时返回 null。</returns>
        private static string WriteReportFile(List<string> lines, uint frame)
        {
            try
            {
                string dir = Path.Combine(EnvPath.kUserDataPath, "ModsData", "MapExt2", kReportFolder);
                Directory.CreateDirectory(dir);
                string name = $"stats-{DateTime.Now:yyyyMMdd-HHmmss}-{PatchManager.CurrentMode}-f{frame}.txt";
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
