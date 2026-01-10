using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using Unity.Collections.LowLevel.Unsafe; // 需要引用 Unity.Collections

namespace MapExtPDX.MapExt.ReBurstSystem
{

    public static class GenericJobReplacePatch
    {
        // --- 日志封装 ---
        private static readonly string ModName = Mod.ModName;
        private static readonly string patchTypeName = nameof(GenericJobReplacePatch);

        public static void Info(string message) => Mod.Info($"[{ModName}.{patchTypeName}] {message}");
        public static void Warn(string message) => Mod.Warn($"[{ModName}.{patchTypeName}] ⚠️ {message}");
        public static void Error(string message) => Mod.Error($"[{ModName}.{patchTypeName}] ❌ {message}");
        public static void Error(Exception e, string message) => Mod.Error(e, $"[{ModName}.{patchTypeName}] ❌ {message}");

        // --- 上下文管理 ---
        private class MethodPatchContext
        {
            public Dictionary<Type, Type> JobReplacements { get; } = new Dictionary<Type, Type>();
            public Dictionary<FieldInfo, FieldInfo> FieldReplacements { get; } = new Dictionary<FieldInfo, FieldInfo>();
        }

        private static Dictionary<MethodBase, MethodPatchContext> activePatchContexts = new Dictionary<MethodBase, MethodPatchContext>();
        private static readonly object _contextLock = new object();

        /// <summary>
        /// 注册替换规则
        /// </summary>
        public static void AddReplacementToContext(MethodBase method, Type originalJobType, Type replacementJobType)
        {
            if (method == null || originalJobType == null || replacementJobType == null) return;

            lock (_contextLock)
            {
                if (!activePatchContexts.TryGetValue(method, out MethodPatchContext context))
                {
                    context = new MethodPatchContext();
                    activePatchContexts[method] = context;
                }
                context.JobReplacements[originalJobType] = replacementJobType;

#if DEBUG
            Info($"➕ 注册映射 | 方法: {method.Name}\n    └─ Job: {originalJobType.Name} -> {replacementJobType.Name}");
#endif

                // 注册字段映射
                foreach (var oldField in AccessTools.GetDeclaredFields(originalJobType))
                {
                    var newField = AccessTools.Field(replacementJobType, oldField.Name);
                    if (newField != null)
                    {
                        context.FieldReplacements[oldField] = newField;
                    }
                    else
                    {
                        Warn($"字段缺失警告 | 在新Job {replacementJobType.Name} 中未找到字段: {oldField.Name}，可能导致 Patch 失败。");
                    }
                }
            }
        }

        /// <summary>
        /// Harmony Transpiler 核心逻辑
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase originalMethod)
        {
            MethodPatchContext context = null;
            lock (_contextLock)
            {
                if (!activePatchContexts.TryGetValue(originalMethod, out context))
                {
                    // 模糊匹配回退策略
                    var fallbackEntry = activePatchContexts.FirstOrDefault(kvp =>
                        kvp.Key.Name == originalMethod.Name &&
                        kvp.Key.DeclaringType == originalMethod.DeclaringType);
                    if (fallbackEntry.Key != null) context = fallbackEntry.Value;
                }
            }

            // 无需 Patch，原样返回
            if (context == null || context.JobReplacements.Count == 0)
            {
                foreach (var i in instructions) yield return i;
                yield break;
            }

#if DEBUG
        Info($"==============================================================");
        Info($"🔧 开始处理方法: {originalMethod.DeclaringType?.Name}.{originalMethod.Name}");
#endif

            var list = instructions.ToList();
            Dictionary<int, LocalBuilder> localRedirects = new Dictionary<int, LocalBuilder>();
            int replacementCount = 0;

            // ==========================================================================================
            // PHASE 1: 预扫描 (Pre-Scan)
            // ==========================================================================================
#if DEBUG
        Info($"  [阶段 1] 预扫描局部变量..."); 
#endif
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].opcode == OpCodes.Initobj &&
                    list[i].operand is Type initType &&
                    context.JobReplacements.ContainsKey(initType))
                {
                    int locIndex = FindPreviousLocalIndex(list, i);
                    if (locIndex >= 0 && !localRedirects.ContainsKey(locIndex))
                    {
                        Type newType = context.JobReplacements[initType];
                        localRedirects[locIndex] = il.DeclareLocal(newType);
#if DEBUG
                    Info($"    🔎 发现待替换变量 [{locIndex}] | {initType.Name} -> {newType.Name} (OpCode: {list[i].opcode})"); 
#endif
                    }
                    else if (locIndex == -1)
                    {
                        Warn($"    ⚠️ 无法定位 Initobj 的变量源，索引: {i}，类型: {initType.Name}");
                    }
                }
            }

            // ==========================================================================================
            // PHASE 2: 指令重写 (Execution)
            // ==========================================================================================
#if DEBUG
        Info($"  [阶段 2] 执行指令替换..."); 
#endif
            for (int i = 0; i < list.Count; i++)
            {
                var instruction = list[i];
                bool isPatched = false;

                // 1. 拦截 Initobj
                if (instruction.opcode == OpCodes.Initobj &&
                    instruction.operand is Type initType &&
                    context.JobReplacements.TryGetValue(initType, out Type newInitType))
                {
                    var newInst = new CodeInstruction(OpCodes.Initobj, newInitType);
                    newInst.labels = instruction.labels;
                    yield return newInst;
                    replacementCount++;
#if DEBUG
                Info($"    ✅ [Initobj] {initType.Name} -> {newInitType.Name}"); 
#endif
                    continue;
                }

                // 2. 拦截变量操作 (Load/Store/Addr)
                int currentLocIndex = GetLocalIndex(instruction);
                if (currentLocIndex >= 0 && localRedirects.TryGetValue(currentLocIndex, out LocalBuilder newLocalBuilder))
                {
                    var newInst = new CodeInstruction(instruction.opcode, newLocalBuilder);
                    newInst.opcode = ConvertToLongOpCode(instruction.opcode);
                    newInst.labels = instruction.labels;
                    newInst.blocks = instruction.blocks;
                    yield return newInst;
                    // 变量重定向过于频繁，Debug通常不打印每一条，避免刷屏
                    continue;
                }

                // 3. 字段写入 (Stfld) - 类型欺骗
                if (!isPatched && instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo oldFieldInfo)
                {
                    if (context.FieldReplacements.TryGetValue(oldFieldInfo, out FieldInfo newFieldInfo))
                    {
                        if (oldFieldInfo.FieldType != newFieldInfo.FieldType)
                        {
                            // 类型不一致，进行二进制欺骗
#if DEBUG
                        Info($"    🧬 [Stfld] 内存重解释: {oldFieldInfo.Name} ({oldFieldInfo.FieldType.Name}) -> {newFieldInfo.Name} ({newFieldInfo.FieldType.Name})"); 
#endif
                            LocalBuilder tempOldVal = il.DeclareLocal(oldFieldInfo.FieldType);
                            var stlocInst = new CodeInstruction(OpCodes.Stloc, tempOldVal);
                            stlocInst.labels = instruction.labels;
                            yield return stlocInst;
                            yield return new CodeInstruction(OpCodes.Ldloca, tempOldVal);
                            yield return new CodeInstruction(OpCodes.Ldobj, newFieldInfo.FieldType);
                            yield return new CodeInstruction(OpCodes.Stfld, newFieldInfo);
                        }
                        else
                        {
                            // 类型一致
                            var newInst = new CodeInstruction(OpCodes.Stfld, newFieldInfo);
                            newInst.labels = instruction.labels;
                            yield return newInst;
                        }
                        isPatched = true;
                        continue;
                    }
                }

                // 4. 字段读取 (Ldfld)
                if (!isPatched && (instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Ldflda) &&
                    instruction.operand is FieldInfo fieldOp)
                {
                    if (context.FieldReplacements.TryGetValue(fieldOp, out FieldInfo newField))
                    {
                        var newInst = new CodeInstruction(instruction.opcode, newField);
                        newInst.labels = instruction.labels;
                        yield return newInst;
                        isPatched = true;
                        continue;
                    }
                }

                // 5. 泛型与类型引用替换
                if (!isPatched)
                {
                    CodeInstruction finalInst = instruction;
                    if (instruction.operand is Type opType && context.JobReplacements.TryGetValue(opType, out Type repType))
                    {
                        finalInst = new CodeInstruction(instruction.opcode, repType);
#if DEBUG
                    Info($"    🔄 [TypeRef] {opType.Name} -> {repType.Name}"); 
#endif
                    }
                    else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                             instruction.operand is MethodInfo method && method.IsGenericMethod)
                    {
                        var args = method.GetGenericArguments();
                        var newArgs = args.Select(t => context.JobReplacements.ContainsKey(t) ? context.JobReplacements[t] : t).ToArray();

                        if (!args.SequenceEqual(newArgs))
                        {
                            try
                            {
                                var newM = method.GetGenericMethodDefinition().MakeGenericMethod(newArgs);
                                finalInst = new CodeInstruction(instruction.opcode, newM);
#if DEBUG
                            Info($"    🔗 [GenericCall] 更新泛型参数: {method.Name}"); 
#endif
                            }
                            catch (Exception e)
                            {
                                Error($"    ❌ 构造泛型方法失败: {method.Name}. 错误: {e.Message}");
                            }
                        }
                    }

                    if (finalInst != instruction)
                    {
                        finalInst.labels = instruction.labels;
                        finalInst.blocks = instruction.blocks;
                        yield return finalInst;
                        isPatched = true;
                        continue;
                    }
                }

                yield return instruction;
            }

#if DEBUG
        Info($"✨ 方法处理完成 | 发生 {replacementCount} 处关键替换");
        Info($"==============================================================");
#endif
        }

        // --- 辅助函数 (保持不变或微调) ---
        private static int FindPreviousLocalIndex(List<CodeInstruction> list, int currentIndex)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                var op = list[i].opcode;
                if (op == OpCodes.Nop) continue;
                if (op == OpCodes.Ldloca || op == OpCodes.Ldloca_S) return GetLocalIndex(list[i]);
                if (IsLdLoc(op)) return GetLocalIndex(list[i]);
                return -1;
            }
            return -1;
        }

        private static bool IsLdLoc(OpCode op) =>
            op == OpCodes.Ldloc || op == OpCodes.Ldloc_S ||
            op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
            op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3;

        private static int GetLocalIndex(CodeInstruction i)
        {
            if (i.operand is LocalBuilder lb) return lb.LocalIndex;
            if (i.operand is int index) return index;
            if (i.operand is byte bIndex) return bIndex;
            if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
            if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
            if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
            if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;
            if (i.opcode == OpCodes.Ldloca_S || i.opcode == OpCodes.Ldloc_S || i.opcode == OpCodes.Stloc_S) return Convert.ToInt32(i.operand);
            return -1;
        }

        private static OpCode ConvertToLongOpCode(OpCode op)
        {
            if (op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S) return OpCodes.Ldloc;
            if (op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S) return OpCodes.Stloc;
            if (op == OpCodes.Ldloca_S) return OpCodes.Ldloca;
            return op;
        }
    }

    // 字段对齐预处理系统
    public class JobFieldValidator
    {
        
        public class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            // 存储自动识别到的类型映射 (原类型 -> 替换类型)
            public Dictionary<Type, Type> ImplicitTypeMapping { get; } = new Dictionary<Type, Type>();
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();

            public void Error(string msg) { IsValid = false; Errors.Add($"❌ {msg}"); }
            public void Warning(string msg) => Warnings.Add($"⚠️ {msg}");

            public string GetReport()
            {
                var sb = new StringBuilder();
                if (Errors.Count > 0)
                {
                    sb.AppendLine("--- 错误 ---");
                    foreach (var e in Errors) sb.AppendLine(e);
                }
                if (Warnings.Count > 0)
                {
                    sb.AppendLine("--- 警告 ---");
                    foreach (var w in Warnings) sb.AppendLine(w);
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// 验证替换Job与原始Job的字段对齐
        /// </summary>
        public static ValidationResult ValidateJobReplacement(Type original, Type replacement)
        {
            var result = new ValidationResult();

            if (original == null || replacement == null) { result.Error("类型为空"); return result; }
            if (!original.IsValueType || !replacement.IsValueType) result.Error("Job 必须是值类型 (struct)");

            // 1. 记录 Job 本身的映射
            result.ImplicitTypeMapping[original] = replacement;

            // 2. 字段逐一检查
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var fields1 = original.GetFields(flags).OrderBy(f => f.MetadataToken).ToArray();
            var fields2 = replacement.GetFields(flags).OrderBy(f => f.MetadataToken).ToArray();

            if (fields1.Length != fields2.Length)
            {
                result.Error($"字段数量不匹配: 原版 {fields1.Length} vs 替换版 {fields2.Length}");
                return result;
            }

            for (int i = 0; i < fields1.Length; i++)
            {
                var f1 = fields1[i];
                var f2 = fields2[i];

                // 检查名称
                if (f1.Name != f2.Name)
                    result.Warning($"索引 {i} 处的字段名称不匹配: 原版 '{f1.Name}' vs 替换版 '{f2.Name}'");

                // --- 核心修复：处理 Private Struct 克隆 ---
                if (f1.FieldType != f2.FieldType)
                {
                    // 尝试验证是否为兼容的克隆体
                    if (AreTypesStructurallyCompatible(f1.FieldType, f2.FieldType))
                    {
                        // 注册映射关系，供后续 Patch 使用
                        if (!result.ImplicitTypeMapping.ContainsKey(f1.FieldType))
                        {
                            result.ImplicitTypeMapping[f1.FieldType] = f2.FieldType;
                            result.Warning($"检测到类型替换 (克隆结构体): {f1.FieldType.Name} -> {f2.FieldType.Name}。已自动建立映射。");
                        }
                    }
                    else
                    {
                        result.Error($"字段 '{f1.Name}' 类型严重不匹配且无法兼容: {f1.FieldType} vs {f2.FieldType}");
                    }
                }

                // 检查 Attribute (Burst 安全性关键)
                CheckAttribute<NativeDisableUnsafePtrRestrictionAttribute>(f1, f2, result);
                CheckAttribute<Unity.Collections.ReadOnlyAttribute>(f1, f2, result);
                CheckAttribute<Unity.Collections.WriteOnlyAttribute>(f1, f2, result);
            }
            return result;
        }

        /// <summary>
        /// 检查两个类型在内存布局上是否兼容 (Size check)
        /// </summary>
        private static bool AreTypesStructurallyCompatible(Type t1, Type t2)
        {
            // 必须都是值类型
            if (!t1.IsValueType || !t2.IsValueType) return false;

            try
            {
                int size1 = UnsafeUtility.SizeOf(t1); // Unity Unsafe 库比 Marshal 更准确处理泛型 struct
                int size2 = UnsafeUtility.SizeOf(t2);
                return size1 == size2;
            }
            catch
            {
                // 如果 Unsafe 失败，回退到 Marshal (可能不准确但比 crash 好)
                try { return Marshal.SizeOf(t1) == Marshal.SizeOf(t2); } catch { return false; }
            }
        }

        private static void CheckAttribute<T>(FieldInfo f1, FieldInfo f2, ValidationResult result) where T : Attribute
        {
            bool has1 = f1.GetCustomAttribute<T>() != null;
            bool has2 = f2.GetCustomAttribute<T>() != null;
            if (has1 != has2)
            {
                result.Warning($"特性不匹配 '{f1.Name}': [{typeof(T).Name}] 原版{(has1 ? "有" : "无")} vs 替换版{(has2 ? "有" : "无")}");
            }
        }
    }

}
