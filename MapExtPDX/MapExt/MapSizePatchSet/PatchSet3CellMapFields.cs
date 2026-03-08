// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

// using UnityEngine; // For Debug.Log

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Game;
using Game.Simulation;
using HarmonyLib;
using MapExtPDX.MapExt.Core;

/// <summary>
/// 此class修补所有直接调用下列字段的托管代码方法：
/// CellMapSystem<T>.kMapSize/cellmapClosedType.kTextureSize
/// </summary>

// 技术方案：v2.2.0改为自动搜索修补对象

namespace MapExtPDX.MapExt.MapSizePatchSet
{
    public static class CellMapSystemPatchManager
    {
        // --- 日志封装 ---
        private static readonly string modName = Mod.ModName;
        private static readonly string patchTypename = nameof(CellMapSystemPatchManager);
        public static void Info(string message) => Mod.Info($" {modName}.{patchTypename}:{message}");
        public static void Warn(string message) => Mod.Warn($" {Mod.ModName}.{patchTypename}:{message}");
        public static void Error(string message) => Mod.Error($" {(Mod.ModName)}.{patchTypename}:{message}");

        public static void Error(Exception e, string message) =>
            Mod.Error(e, $" {Mod.ModName}.{patchTypename}:{message}");

        // 日志输出：方法名 -> 修改记录列表
        private static readonly ConcurrentDictionary<string, List<string>> _patchLog =
            new ConcurrentDictionary<string, List<string>>();

        // 核心映射表: FieldInfo -> int (目标值)
        private static readonly Dictionary<FieldInfo, int> _replacementMap =
            new Dictionary<FieldInfo, int>(new FieldInfoEqualityComparer());

        // 存储在注册阶段显式发现的基类方法 (闭合泛型方法)
        private static readonly ConcurrentBag<MethodBase> _manualMethodsToPatch = new ConcurrentBag<MethodBase>();

        // 缓存基类定义
        private static readonly Type BaseGenericType = typeof(CellMapSystem<>);

        /// <summary>
        /// 读取设置，注册列表，并应用补丁
        /// </summary>
        public static void ApplyPatches(Harmony harmony)
        {
            Info(" 开始执行修补 CellMapSystem<T> kMapSize/kTextureSize...");

            int mapMult = PatchManager.CurrentCoreValue;
            // Texture multiplier uses 1 for ultra large map (114km / mapMult >= 8) to avoid Unity texture limit crash
            int texMult = mapMult >= 8 ? 1 : mapMult;

            // ---在此处配置你的清单---

            // 格式: Register<系统类>(Map倍率, Texture倍率);

            // 继承CellMapSystem<T>基类的共15个系统/14个派生类型
            // kMapSize修改14个，kTextureSize修改11个
            // TelecomPreviewSystem与TelecomCoverageSystem 封闭类型相同，kTextureSize内部硬编码不作修改
            // WindSystem kTextureSize需要额外处理，暂不加入
            Register<AirPollutionSystem>(mapMult, texMult);
            Register<AvailabilityInfoToGridSystem>(mapMult, texMult);
            Register<GroundPollutionSystem>(mapMult, texMult);
            Register<GroundWaterSystem>(mapMult, texMult);
            Register<LandValueSystem>(mapMult, texMult);
            Register<NaturalResourceSystem>(mapMult, texMult);
            Register<NoisePollutionSystem>(mapMult, texMult);
            Register<PopulationToGridSystem>(mapMult, texMult);
            Register<SoilWaterSystem>(mapMult, texMult);
            Register<TerrainAttractivenessSystem>(mapMult, texMult);
            Register<TrafficAmbienceSystem>(mapMult, texMult);
            Register<ZoneAmbienceSystem>(mapMult, texMult);
            // 例外处理
            Register<TelecomCoverageSystem>(mapMult, 1);
            Register<WindSystem>(mapMult, 1);

            Info(" Registered CellMap systems configuration.");

            // --- 执行修补 ---            
            ApplyAllPatches(harmony);
        }

        // --- 限定搜索范围：配置允许扫描的命名空间前缀 ---
        private static readonly string[] AllowedNamespaces =
        {
            "Game.Simulation",
            "Game.Debug",
            "Game.UI.Tooltip",
            "Game.Tools",
            // 根据实际需求增删
            // "Game.Rendering"  // WindTextureSystem暂未修改
        };

        /// <summary>
        /// 注册目标系统
        /// </summary>
        public static void Register<TDerived>(int mapMultiplier, int textureMultiplier)
        {
            Type derivedType = typeof(TDerived);

            // 1. 注册泛型基类的 kMapSize
            Type baseType = derivedType.BaseType;
            while (baseType != null)
            {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == BaseGenericType)
                {
                    FieldInfo mapField = baseType.GetField("kMapSize",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                    if (mapField != null) RegisterField(mapField, mapMultiplier);

                    // --- 手动扫描该闭合基类的所有方法 ---
                    // 程序集的 GetTypes() 扫不到
                    ScanBaseMethods(baseType);

                    break;
                }

                baseType = baseType.BaseType;
            }

            // 2. 注册派生类的 kTextureSize
            FieldInfo texField = derivedType.GetField("kTextureSize",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            if (texField != null) RegisterField(texField, textureMultiplier);
        }

        // 主动扫描基类方法
        private static void ScanBaseMethods(Type closedBaseType)
        {
            // 获取基类定义的所有方法 (GetData, GetCellCenter 等)
            var methods = closedBaseType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.Static |
                                                    BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                if (method.IsAbstract || method.ContainsGenericParameters) continue;

                // 检查该方法是否引用了被注册的字段
                //if (IsClientMethod(method))
                //{
                _manualMethodsToPatch.Add(method);
                //}
            }
        }

        private static void RegisterField(FieldInfo field, int multiplier)
        {
            // 倍率为1则不修改，优化性能
            if (multiplier <= 1) return;

            try
            {
                // 动态读取该字段在内存中的实际值
                // 对于 static 字段，obj 参数传 null
                object rawValue = field.GetValue(null);

                if (rawValue is int originalValue)
                {
                    int newValue = originalValue * multiplier;

                    if (!_replacementMap.ContainsKey(field))
                    {
                        _replacementMap[field] = newValue;
                        // 日志
                        Info(
                            $" Registered {field.DeclaringType.Name}.{field.Name}: {originalValue} -> {newValue} (x{multiplier})");
                    }
                }
            }
            catch (Exception ex)
            {
                Info(
                    $" Failed to read initial value for {field.DeclaringType.Name}.{field.Name}. Skipped. Error: {ex.Message}");
            }
        }

        public static void ApplyAllPatches(Harmony harmony)
        {
            if (_replacementMap.Count == 0) return;

            Stopwatch sw = Stopwatch.StartNew();
            _patchLog.Clear();

            // 统计变量
            int totalTypesScanned = 0;
            int totalMethodsScanned = 0;


            // 使用 Set 去重 (防止手动注册的方法和自动扫描的方法重复，虽然几率很小)
            HashSet<MethodBase> distinctMethodsToPatch = new(new MethodComparer());

            // --- 1. 准备扫描程序集 ---

            // 先加入手动注册的基类方法
            foreach (var m in _manualMethodsToPatch) distinctMethodsToPatch.Add(m);

            // 自动扫描程序集
            Assembly gameAssembly = typeof(GameSystemBase).Assembly;
            Info($" Scanning assembly {gameAssembly.GetName().Name}...");

            // --- 2. 第一层过滤：仅保留白名单内的类型，排除接口、泛型以及带有 BurstCompile 的结构 ---
            var filteredTypes = gameAssembly.GetTypes()
                .Where(t => t.Namespace != null && AllowedNamespaces.Any(ns => t.Namespace.StartsWith(ns)))
                .Where(t => !t.IsGenericTypeDefinition && !t.IsInterface && !t.GetCustomAttributes()
                    .Any(attr => attr.GetType().Name == "BurstCompileAttribute"))
                .ToList();

            totalTypesScanned = filteredTypes.Count;

            // 方法集合
            ConcurrentBag<MethodBase> scannedMethods = new();

            // --- 3. 第二层过滤：并行扫描方法 ---
            Parallel.ForEach(filteredTypes, type =>
            {
                var members = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                              BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>()
                    .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                                 BindingFlags.Static | BindingFlags.DeclaredOnly));

                foreach (var method in members)
                {
                    // 原子累加总方法数
                    Interlocked.Increment(ref totalMethodsScanned);

                    if (method.IsAbstract || method.ContainsGenericParameters ||
                        method.MethodImplementationFlags.HasFlag(MethodImplAttributes.InternalCall))
                        continue;

                    if (IsClientMethod(method))
                    {
                        scannedMethods.Add(method);
                    }
                }
            });

            // 合并
            foreach (var m in scannedMethods) distinctMethodsToPatch.Add(m);

            // 测量扫描时间
            sw.Stop();
            long scanTime = sw.ElapsedMilliseconds;

            // --- 4. 应用补丁 ---
            // (此阶段必须单线程)
            sw.Restart();
            var transpiler = new HarmonyMethod(typeof(CellMapSystemPatchManager), nameof(Transpiler));
            foreach (var method in distinctMethodsToPatch)
            {
                try
                {
                    harmony.Patch(method, transpiler: transpiler);
                }
                catch
                {
                }
            }

            sw.Stop();

            // 5. 输出详细日志
            PrintSummary(totalTypesScanned, totalMethodsScanned, distinctMethodsToPatch.Count, scanTime,
                sw.ElapsedMilliseconds);
        }

        // 日志辅助
        private static void PrintSummary(int types, int methods, int patched, long scanTime, long patchTime)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(" ");
            sb.AppendLine("========== CellMap Fields 修补报告 ==========");
            sb.AppendLine($"[Stats] Namespaces Filtered: {string.Join(", ", AllowedNamespaces)}");
            sb.AppendLine($"[Stats] Scanned: {types} Types, {methods} Methods in {scanTime}ms");
            sb.AppendLine($"[Stats] Patched: {patched} Methods in {patchTime}ms");
            sb.AppendLine("-----------------------------------------------------------------");

            // 排序输出
            foreach (var entry in _patchLog.OrderBy(e => e.Key))
            {
                sb.AppendLine($"[MODIFIED] {entry.Key}");
                foreach (var detail in entry.Value)
                {
                    sb.AppendLine($"    => {detail}");
                }
            }

            sb.AppendLine("=================================================================");

            Info(sb.ToString());
        }

        private static bool IsClientMethod(MethodBase method)
        {
            try
            {
                var body = method.GetMethodBody();
                if (body == null) return false;

                // 1. 极速预筛选：检查是否包含 Ldsfld (OpCode 0x7E)
                byte[] il = body.GetILAsByteArray();
                if (il == null) return false;

                bool hasLdsfld = false;
                for (int i = 0; i < il.Length; i++)
                    if (il[i] == 0x7E)
                    {
                        hasLdsfld = true;
                        break;
                    }

                if (!hasLdsfld) return false;

                // 2. 精确筛选：检查 Operand
                foreach (var instr in PatchProcessor.GetOriginalInstructions(method))
                {
                    if (instr.opcode == OpCodes.Ldsfld && instr.operand is FieldInfo field &&
                        _replacementMap.ContainsKey(field))
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            MethodBase original)
        {
            string typeName = original.DeclaringType?.FullName ?? "Unknown";
            string methodName = original.Name;

            foreach (var instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldsfld && instr.operand is FieldInfo field)
                {
                    if (_replacementMap.TryGetValue(field, out int newValue))
                    {
                        // 记录日志
                        string logEntry = $"{field.DeclaringType.Name}.{field.Name} -> {newValue}";
                        var list = _patchLog.GetOrAdd(typeName + "::" + methodName, new List<string>());
                        if (!list.Contains(logEntry)) list.Add(logEntry);

                        // *** 关键修复 ***
                        // 1. 直接修改 OpCode 为 ldc.i4
                        // 2. 直接修改 Operand 为 int 值
                        // 3. 绝不触碰 Labels 和 Blocks，它们会自动依附在当前指令对象上
                        instr.opcode = OpCodes.Ldc_I4;
                        instr.operand = newValue;

                        // 注意：不再尝试检测并删除后面的 conv.r4
                        // 让 JIT/Burst 编译器去处理 "ldc.i4 -> conv.r4" 这种微小的冗余
                        // 这能保证 100% 的 IL 结构安全性
                    }
                }

                yield return instr;
            }
        }

        private class FieldInfoEqualityComparer : IEqualityComparer<FieldInfo>
        {
            public bool Equals(FieldInfo x, FieldInfo y)
            {
                if (x == null || y == null) return x == y;
                // 必须比较 DeclaringType，因为不同泛型实例的字段 Token 相同
                return x.MetadataToken == y.MetadataToken &&
                       x.Module == y.Module &&
                       x.DeclaringType == y.DeclaringType;
            }

            public int GetHashCode(FieldInfo obj)
            {
                // 组合 Hash，防止碰撞
                return obj.MetadataToken.GetHashCode() ^ (obj.DeclaringType?.GetHashCode() ?? 0);
            }
        }

        // 用于 HashSet 去重的方法比较器
        private class MethodComparer : IEqualityComparer<MethodBase>
        {
            public bool Equals(MethodBase x, MethodBase y)
            {
                if (x == null || y == null) return x == y;
                // 方法元数据Token和模块相同即为同一方法
                return x.MetadataToken == y.MetadataToken &&
                       x.Module == y.Module &&
                       x.DeclaringType == y.DeclaringType;
            }

            public int GetHashCode(MethodBase obj) =>
                obj.MetadataToken.GetHashCode() ^ (obj.DeclaringType?.GetHashCode() ?? 0);
        }
    }
}
