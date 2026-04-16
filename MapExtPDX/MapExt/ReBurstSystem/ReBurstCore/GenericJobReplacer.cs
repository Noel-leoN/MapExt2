using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using MapExtPDX.MapExt.Core;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace MapExtPDX.MapExt.ReBurstSystem.Core
{
    public static class GenericJobReplacePatch
    {
        private const string Tag = "ReBurst";


        // --- 上下文管理 ---
        private class MethodPatchContext
        {
            public Dictionary<Type, Type> JobReplacements { get; } = new Dictionary<Type, Type>();
            public Dictionary<FieldInfo, FieldInfo> FieldReplacements { get; } = new Dictionary<FieldInfo, FieldInfo>();
        }

        private static Dictionary<MethodBase, MethodPatchContext> activePatchContexts =
            new Dictionary<MethodBase, MethodPatchContext>();

        private static readonly object _contextLock = new object();

        /// <summary>
        /// 注册替换规则
        /// </summary>
        public static void AddReplacementToContext(MethodBase method, Type originalJobType, Type replacementJobType)
        {
            if (method == null || originalJobType == null || replacementJobType == null) return;

            // --- 步骤 0: 预验证 (v7 Feature) ---
            var validation = JobFieldValidator.ValidateJobReplacement(originalJobType, replacementJobType);
            if (!validation.IsValid)
            {
                ModLog.Error(Tag,
                    $"注册失败 | Job 结构不兼容 {originalJobType.Name} -> {replacementJobType.Name}\n{validation.GetReport()}");
                if (validation.Errors.Count > 0) return;
            }
            else if (validation.Warnings.Count > 0)
            {
                ModLog.Warn(Tag, $"注册警告 | {originalJobType.Name} -> {replacementJobType.Name}\n{validation.GetReport()}");
            }
            else if (validation.Infos.Count > 0)
            {
#if DEBUG
                ModLog.Info(Tag, $"注册信息 | {originalJobType.Name} -> {replacementJobType.Name}\n{validation.GetReport()}");
#else
                ModLog.Info(Tag, $"注册映射 | {originalJobType.Name} -> {replacementJobType.Name} ({validation.Infos.Count} implicit type mappings)");
#endif
            }

            lock (_contextLock)
            {
                if (!activePatchContexts.TryGetValue(method, out MethodPatchContext context))
                {
                    context = new MethodPatchContext();
                    activePatchContexts[method] = context;
                }

                context.JobReplacements[originalJobType] = replacementJobType;

                // 合并验证器发现的隐式类型映射 (例如 private struct 克隆体)
                foreach (var kvp in validation.ImplicitTypeMapping)
                {
                    if (!context.JobReplacements.ContainsKey(kvp.Key))
                    {
                        context.JobReplacements[kvp.Key] = kvp.Value;
                    }
                }

                ModLog.Debug(Tag, $"注册映射 | 方法: {method.Name} └─ Job: {originalJobType.Name} -> {replacementJobType.Name}");

                // 注册字段映射
                void RegisterFields(Type oldType, Type newType)
                {
                    foreach (var oldField in AccessTools.GetDeclaredFields(oldType))
                    {
                        var newField = AccessTools.Field(newType, oldField.Name);
                        if (newField != null)
                        {
                            context.FieldReplacements[oldField] = newField;
                        }
                        else
                        {
                            ModLog.Warn(Tag, $"字段缺失 | 在新Job/结构 {newType.Name} 中未找到字段: {oldField.Name}，可能导致 Patch 失败");
                        }
                    }
                }

                RegisterFields(originalJobType, replacementJobType);

                // 同步注册内部结构体的字段映射，处理自定义结构体嵌套的特殊情况
                foreach (var kvp in validation.ImplicitTypeMapping)
                {
                    RegisterFields(kvp.Key, kvp.Value);
                }
            }
        }

        /// <summary>
        /// 清理所有缓存 (建议在 Mod OnDispose 时调用)
        /// </summary>
        public static void ClearCache()
        {
            lock (_contextLock)
            {
                activePatchContexts.Clear();
                ModLog.Info(Tag, "已清理所有 Patch 上下文缓存");
            }
        }

        /// <summary>
        /// Harmony Transpiler 核心逻辑
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il,
            MethodBase originalMethod)
        {
            var result = TranspilerInternal(instructions, il, originalMethod).ToList();
            return result;
        }

        private static IEnumerable<CodeInstruction> TranspilerInternal(IEnumerable<CodeInstruction> instructions,
            ILGenerator il, MethodBase originalMethod)
        {
            // 安全熔断
            if (Mod.IsUnloading)
            {
                foreach (var i in instructions) yield return i;
                yield break;
            }

            MethodPatchContext context;
            lock (_contextLock)
            {
                if (!activePatchContexts.TryGetValue(originalMethod, out context))
                {
                    // 模糊匹配回退策略 (增加参数数量比对以区分重载)
                    var originalParamCount = originalMethod.GetParameters().Length;
                    var fallbackEntry = activePatchContexts.FirstOrDefault(kvp =>
                        kvp.Key.Name == originalMethod.Name &&
                        kvp.Key.DeclaringType == originalMethod.DeclaringType &&
                        kvp.Key.GetParameters().Length == originalParamCount);
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
            ModLog.Patch(Tag, $"开始处理方法: {originalMethod.DeclaringType?.Name}.{originalMethod.Name}");
#endif

            var list = instructions.ToList();
            Dictionary<int, LocalBuilder> localRedirects = new Dictionary<int, LocalBuilder>();
            int replacementCount = 0;

            // ==========================================================================================
            // PHASE 1: 预扫描 (Pre-Scan)
            // ==========================================================================================
#if DEBUG
            ModLog.Debug(Tag, $"  [阶段 1] 预扫描局部变量...");
#endif
            var methodBody = originalMethod.GetMethodBody();
            if (methodBody != null)
            {
                foreach (var localVar in methodBody.LocalVariables)
                {
                    if (localVar.LocalType != null)
                    {
                        if (context.JobReplacements.TryGetValue(localVar.LocalType, out var newType))
                        {
                            localRedirects[localVar.LocalIndex] = il.DeclareLocal(newType);
                            ModLog.Debug(Tag,
                                $"  发现待替换变量 [{localVar.LocalIndex}] | {localVar.LocalType.Name} -> {newType.Name}");
                        }
                    }
                }
            }
            else
            {
                ModLog.Warn(Tag, $"无法获取方法体局部变量信息: {originalMethod.Name}");
            }

            // ==========================================================================================
            // PHASE 2: 指令重写 (Execution)
            // ==========================================================================================
#if DEBUG
            ModLog.Debug(Tag, $"  [阶段 2] 执行指令替换...");
#endif
            for (int i = 0; i < list.Count; i++)
            {
                var instruction = list[i];

                // 1. 拦截 Initobj
                if (instruction.opcode == OpCodes.Initobj &&
                    instruction.operand is Type initType &&
                    context.JobReplacements.TryGetValue(initType, out Type newInitType))
                {
                    var newInst = new CodeInstruction(OpCodes.Initobj, newInitType);
                    newInst.labels = instruction.labels;
                    yield return newInst;
                    replacementCount++; // Initobj
#if DEBUG
                    ModLog.Debug(Tag, $"    [Initobj] {initType.Name} -> {newInitType.Name}");
#endif
                    continue;
                }

                // 2. 拦截变量操作 (Load/Store/Addr)
                int currentLocIndex = GetLocalIndex(instruction);
                if (currentLocIndex >= 0 &&
                    localRedirects.TryGetValue(currentLocIndex, out LocalBuilder newLocalBuilder))
                {
                    var newInst = new CodeInstruction(instruction.opcode, newLocalBuilder);
                    newInst.opcode = ConvertToLongOpCode(instruction.opcode);
                    newInst.labels = instruction.labels;
                    newInst.blocks = instruction.blocks;
                    yield return newInst;
                    replacementCount++; // LocalVar redirect
                    continue;
                }

                // 3. 字段写入 (Stfld) - 类型欺骗
                if (instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo oldFieldInfo)
                {
                    if (context.FieldReplacements.TryGetValue(oldFieldInfo, out FieldInfo newFieldInfo))
                    {
                        if (oldFieldInfo.FieldType != newFieldInfo.FieldType)
                        {
                            // 无论是否全局映射，由于托管类型签名不一致，原栈顶对象类型无法安全满足 stfld 校验
                            // 我们必须插入一次显式的安全强转 (Bitcast)
#if DEBUG
                            ModLog.Debug(Tag,
                                $"    [Stfld] 安全位投影 (Bitcast) {oldFieldInfo.FieldType.Name} -> {newFieldInfo.FieldType.Name}");
#endif
                            MethodInfo bitcastDef = typeof(GenericJobReplacePatch).GetMethod("Bitcast",
                                BindingFlags.Public | BindingFlags.Static);
                            MethodInfo bitcastMethod =
                                bitcastDef.MakeGenericMethod(oldFieldInfo.FieldType, newFieldInfo.FieldType);

                            var callInst = new CodeInstruction(OpCodes.Call, bitcastMethod);
                            callInst.labels = instruction.labels; // 转移跳转标签到 call 上
                            yield return callInst; // 栈: [ObjectRef, NewValue]

                            yield return new CodeInstruction(OpCodes.Stfld, newFieldInfo); // []
                            replacementCount++;
                        }
                        else
                        {
                            // 类型一致，直接替换字段引用
                            var newInst = new CodeInstruction(instruction.opcode, newFieldInfo);
                            newInst.labels = instruction.labels;
                            yield return newInst;
                            replacementCount++; // Stfld (same type)
                        }

                        continue;
                    }
                }

                // 4. 字段读取 (Ldfld, Ldflda)
                if ((instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Ldflda) &&
                    instruction.operand is FieldInfo fieldOp)
                {
                    if (context.FieldReplacements.TryGetValue(fieldOp, out FieldInfo newField))
                    {
                        if (fieldOp.FieldType != newField.FieldType)
                        {
                            if (instruction.opcode == OpCodes.Ldflda)
                            {
                                ModLog.Warn(Tag,
                                    $"[Ldflda] 字段类型不匹配: {fieldOp.Name} ({fieldOp.FieldType.Name}) -> {newField.Name} ({newField.FieldType.Name})。依赖运行时结构体内存大小一致保证安全");
                                var newInst = instruction;
                                newInst.operand = newField;
                                yield return newInst;
                                continue;
                            }
                            else
                            {
                                // 反向位投影 (Ldfld)
#if DEBUG
                                ModLog.Debug(Tag,
                                    $"    [Ldfld] 安全位投影 (Bitcast) {newField.FieldType.Name} -> {fieldOp.FieldType.Name}");
#endif
                                var newInstLdfld2 = new CodeInstruction(instruction.opcode, newField)
                                {
                                    labels = instruction.labels
                                };
                                yield return newInstLdfld2; // [NewValue]

                                MethodInfo bitcastDef = typeof(GenericJobReplacePatch).GetMethod("Bitcast",
                                    BindingFlags.Public | BindingFlags.Static);
                                MethodInfo bitcastMethod =
                                    bitcastDef.MakeGenericMethod(newField.FieldType, fieldOp.FieldType);

                                yield return new CodeInstruction(OpCodes.Call, bitcastMethod); // [OldValue]
                                replacementCount++;
                                continue;
                            }
                        }

                        var newInst2 = instruction;
                        newInst2.operand = newField;
                        yield return newInst2;
                        continue;
                    }
                }

                // 5. 泛型与类型引用替换
                {
                    CodeInstruction finalInst = instruction;
                    if (instruction.operand is Type opType &&
                        context.JobReplacements.TryGetValue(opType, out var repType))
                    {
                        finalInst = new CodeInstruction(instruction.opcode, repType);
                        replacementCount++;
#if DEBUG
                        ModLog.Debug(Tag, $"    [TypeRef] {opType.Name} -> {repType.Name}");
#endif
                    }
                    else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                             instruction.operand is MethodInfo { IsGenericMethod: not false } method)
                    {
                        var args = method.GetGenericArguments();
                        var newArgs = args.Select(t =>
                            !context.JobReplacements.ContainsKey(t) ? t : context.JobReplacements[t]).ToArray();

                        if (!args.SequenceEqual(newArgs))
                        {
                            try
                            {
                                var newM = method.GetGenericMethodDefinition().MakeGenericMethod(newArgs);
                                finalInst = new CodeInstruction(instruction.opcode, newM);
                                replacementCount++;
                                ModLog.Debug(Tag, $"    [GenericCall] 更新泛型参数: {method.Name}");
                            }
                            catch (Exception e)
                            {
                                ModLog.Error(Tag, $"构造泛型方法失败: {method.Name}. 错误: {e.Message}");
                            }
                        }
                    }

                    if (finalInst != instruction)
                    {
                        finalInst.labels = instruction.labels;
                        finalInst.blocks = instruction.blocks;
                        yield return finalInst;
                        continue;
                    }
                }

                yield return instruction;
            }

#if DEBUG
            ModLog.Ok(Tag, $"方法处理完成 | 发生 {replacementCount} 处关键替换");
#endif
        }

        // --- 辅助函数 ---


        private static bool IsLocalVariableOpCode(OpCode op) =>
            op == OpCodes.Ldloc || op == OpCodes.Ldloc_S ||
            op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
            op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 ||
            op == OpCodes.Ldloca || op == OpCodes.Ldloca_S ||
            op == OpCodes.Stloc || op == OpCodes.Stloc_S ||
            op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1 ||
            op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3;

        private static int GetLocalIndex(CodeInstruction i)
        {
            if (!IsLocalVariableOpCode(i.opcode)) return -1;

            if (i.operand is LocalBuilder lb) return lb.LocalIndex;
            if (i.operand is LocalVariableInfo lvi) return lvi.LocalIndex;
            if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
            if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
            if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
            if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;

            if (i.operand != null)
            {
                try
                {
                    return Convert.ToInt32(i.operand);
                }
                catch
                {
                    // Ignore conversion failures, fallback to -1
                }
            }

            return -1;
        }

        private static OpCode ConvertToLongOpCode(OpCode op)
        {
            if (op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 ||
                op == OpCodes.Ldloc_S) return OpCodes.Ldloc;
            if (op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3 ||
                op == OpCodes.Stloc_S) return OpCodes.Stloc;
            if (op == OpCodes.Ldloca_S) return OpCodes.Ldloca;
            return op;
        }

        /// <summary>
        /// 泛型位投影安全强转 (解决 Burst/IL2CPP C++ 严格别名 Strict-Aliasing 导致的随机崩溃内存失效问题)
        /// 此扩展在 IL 阶段替代原始的 `Ldloca` + `Ldobj` 类型欺骗黑魔法。
        /// </summary>
        public static unsafe TTo Bitcast<TFrom, TTo>(TFrom source)
            where TFrom : struct
            where TTo : struct
        {
            // 通过 MemCpy 强制原指针位拷贝，不给 C++ 编译器任何进行类型擦除优化的借口
            // 使用 Min(SizeOf<TFrom>, SizeOf<TTo>) 防止 TTo > TFrom 时的 buffer over-read
            TTo dest = default;
            int copyLen = math.min(UnsafeUtility.SizeOf<TFrom>(), UnsafeUtility.SizeOf<TTo>());
            UnsafeUtility.MemCpy(UnsafeUtility.AddressOf(ref dest), UnsafeUtility.AddressOf(ref source),
                copyLen);
            return dest;
        }
    }

// 字段对齐预处理系统
    public class JobFieldValidator
    {
        public class ValidationResult
        {
            public bool IsValid { get; set; } = true;
            public Dictionary<Type, Type> ImplicitTypeMapping { get; } = new Dictionary<Type, Type>();
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Infos { get; } = new List<string>();

            public void Error(string msg)
            {
                IsValid = false;
                Errors.Add($"❌ {msg}");
            }

            public void Warning(string msg) => Warnings.Add($"⚠️ {msg}");

            public void Info(string msg) => Infos.Add($"ℹ️ {msg}");

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

                if (Infos.Count > 0)
                {
                    sb.AppendLine("--- 信息 ---");
                    foreach (var i in Infos) sb.AppendLine(i);
                }

                return sb.ToString();
            }
        }

        public static ValidationResult ValidateJobReplacement(Type original, Type replacement)
        {
            var result = new ValidationResult();
            ValidateRecursive(original, replacement, result, new HashSet<Type>());
            return result;
        }

        private static void ValidateRecursive(Type original, Type replacement, ValidationResult result,
            HashSet<Type> visited)
        {
            if (original == null || replacement == null)
            {
                result.Error("类型为空");
                return;
            }

            if (!visited.Add(original)) return;

            if (!original.IsValueType || !replacement.IsValueType)
            {
                result.Error($"Job 或其包含结构体必须是值类型 (struct): {original.Name}");
                return;
            }

            result.ImplicitTypeMapping[original] = replacement;

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly;
            var fields1 = original.GetFields(flags).OrderBy(f => f.MetadataToken).ToArray();
            var fields2 = replacement.GetFields(flags).OrderBy(f => f.MetadataToken).ToArray();

            if (fields1.Length != fields2.Length)
            {
                result.Error(
                    $"字段数量不匹配 {original.Name} vs {replacement.Name} (原版 {fields1.Length} vs 替换版 {fields2.Length})");
                return;
            }

            for (int i = 0; i < fields1.Length; i++)
            {
                var f1 = fields1[i];
                var f2 = fields2[i];

                if (f1.Name != f2.Name)
                    result.Warning($"索引 {i} 处的字段名称不匹配 原版 '{f1.Name}' vs 替换版 '{f2.Name}'");

                if (f1.FieldType != f2.FieldType)
                {
                    if (AreTypesStructurallyCompatible(f1.FieldType, f2.FieldType))
                    {
                        if (!result.ImplicitTypeMapping.ContainsKey(f1.FieldType))
                        {
                            result.ImplicitTypeMapping[f1.FieldType] = f2.FieldType;
                            result.Info($"检测到类型替换 (克隆结构体): {f1.FieldType.Name} -> {f2.FieldType.Name}。已自动建立映射。");
                        }

                        ValidateRecursive(f1.FieldType, f2.FieldType, result, visited);
                    }
                    else
                    {
                        result.Error($"字段 '{f1.Name}' 类型严重不匹配且无法兼容: {f1.FieldType} vs {f2.FieldType}");
                    }
                }

                CheckAttribute<NativeDisableUnsafePtrRestrictionAttribute>(f1, f2, result);
                CheckAttribute<Unity.Collections.ReadOnlyAttribute>(f1, f2, result);
                CheckAttribute<Unity.Collections.WriteOnlyAttribute>(f1, f2, result);
            }
        }

        private static bool AreTypesStructurallyCompatible(Type t1, Type t2)
        {
            if (!t1.IsValueType || !t2.IsValueType) return false;
            try
            {
                int size1 = UnsafeUtility.SizeOf(t1);
                int size2 = UnsafeUtility.SizeOf(t2);
                return size1 == size2;
            }
            catch
            {
                try
                {
                    return Marshal.SizeOf(t1) == Marshal.SizeOf(t2);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void CheckAttribute<T>(FieldInfo f1, FieldInfo f2, ValidationResult result)
            where T : Attribute
        {
            bool has1 = f1.GetCustomAttribute<T>() != null;
            bool has2 = f2.GetCustomAttribute<T>() != null;
            if (has1 != has2)
            {
                result.Warning(
                    $"特性不匹配 '{f1.Name}': [{typeof(T).Name}] 原版{(has1 ? "有" : "无")} vs 替换版{(has2 ? "有" : "无")}");
            }
        }
    }
}
