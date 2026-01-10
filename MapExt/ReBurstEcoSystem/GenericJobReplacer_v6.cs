using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine; // 用于 Debug.Log

namespace MapExtPDX.MapExt.ReBurstEcoSystem
{

    public static class GenericJobReplacePatch
    {
        // 日志归一化
        private static readonly string ModName = Mod.ModName;
        private static readonly string patchTypeName = nameof(GenericJobReplacePatch);
        public static void Info(string message) => Mod.Info($" {ModName}.{patchTypeName}:{message}");
        public static void Warn(string message) => Mod.Warn($" {ModName}.{patchTypeName}:{message}");
        public static void Error(string message) => Mod.Error($" {ModName}.{patchTypeName}:{message}");
        public static void Error(Exception e, string message) => Mod.Error(e, $" {Mod.ModName}.{patchTypeName}:{message}");

        // 保存补丁上下文的内部类
        private class MethodPatchContext
        {
            // 原始Job类型 -> 替换后的Job类型
            public Dictionary<Type, Type> JobReplacements { get; } = new Dictionary<Type, Type>();
            // 原始字段 -> 替换后的字段
            public Dictionary<FieldInfo, FieldInfo> FieldReplacements { get; } = new Dictionary<FieldInfo, FieldInfo>();
        }

        // 存储每个方法的补丁上下文
        private static Dictionary<MethodBase, MethodPatchContext> activePatchContexts = new Dictionary<MethodBase, MethodPatchContext>();
        // 防止重复处理（可选逻辑）
        private static readonly HashSet<MethodBase> successfullyProcessedMethods = new HashSet<MethodBase>();

        // 定义锁对象
        private static readonly object _contextLock = new object();
        /// <summary>
        /// 注册替换规则。
        /// </summary>
        /// <param name="method">需要Patch的目标方法</param>
        /// <param name="originalJobType">原始Job结构体类型</param>
        /// <param name="replacementJobType">新的Job结构体类型</param>
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
                Info($" 注册替换: {originalJobType.Name} -> {replacementJobType.Name} (方法: {method.Name})");

                // 注册字段映射：通过名称匹配
                foreach (var oldField in AccessTools.GetDeclaredFields(originalJobType))
                {
                    var newField = AccessTools.Field(replacementJobType, oldField.Name);
                    if (newField != null)
                    {
                        context.FieldReplacements[oldField] = newField;
                        // Log详细映射，方便排查字段未对齐的问题
#if DEBUG
                        Info($"[JobPatch]   字段映射: {oldField.Name} ({oldField.FieldType.Name}) -> {newField.Name} ({newField.FieldType.Name})");
#endif
                    }
                    else
                    {
                        Warn($"警告: 在新Job中未找到字段 {oldField.Name}，这可能导致Patch失败。");
                    }
                }
            }
        }

        /// <summary>
        /// Harmony Transpiler 核心逻辑
        /// </summary>
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase originalMethod)
        {
            // 安全检查：如果Mod正在卸载，不做任何修改
            // if (Mod.IsUnloading) { foreach (var i in instructions) yield return i; yield break; } 

            // 获取当前方法的上下文
            MethodPatchContext context = null;

            lock (_contextLock)
            {
                if (!activePatchContexts.TryGetValue(originalMethod, out context))
                {
                    // 尝试模糊匹配（防止某些泛型实例化导致的Key不一致）
                    var fallbackEntry = activePatchContexts.FirstOrDefault(kvp =>
                        kvp.Key.Name == originalMethod.Name &&
                        kvp.Key.DeclaringType == originalMethod.DeclaringType);
                    if (fallbackEntry.Key != null) context = fallbackEntry.Value;
                }
            }

            if (context == null || context.JobReplacements.Count == 0)
            {
                Warn($"[JobPatch] 跳过方法 {originalMethod.Name}: 无上下文。");
                foreach (var i in instructions) yield return i;
                yield break;
            }

#if DEBUG
            Info($"[JobPatch] 开始处理方法: {originalMethod.Name}"); 
#endif

            var list = instructions.ToList();
            // 存储旧变量索引 -> 新变量LocalBuilder的映射
            Dictionary<int, LocalBuilder> localRedirects = new Dictionary<int, LocalBuilder>();

            // ==========================================================================================
            // PHASE 1: 预扫描 (Pre-Scan)
            // 目的：在修改指令流之前，先确定哪些局部变量是“旧Job类型”，并提前创建“新Job类型”的变量。
            // ==========================================================================================
            for (int i = 0; i < list.Count; i++)
            {
                // 特征 1: 拦截 initobj (例如: new Job())
                if (list[i].opcode == OpCodes.Initobj &&
                    list[i].operand is Type initType &&
                    context.JobReplacements.ContainsKey(initType))
                {
                    // 向前回溯找到 ldloca (加载局部变量地址)
                    int locIndex = FindPreviousLocalIndex(list, i);

                    if (locIndex == -1)
                    {
                        // 记录无法安全替换的指令索引，稍后跳过
                        Warn($"[JobPatch] 无法定位 Initobj 的变量源，跳过处理。Index: {i}, Type: {initType.Name}");
                        // 建议维护一个 HashSet<int> skipIndices 来在 Phase 2 中跳过
                        continue;
                    }

                    if (locIndex >= 0 && !localRedirects.ContainsKey(locIndex))
                    {
                        Type newType = context.JobReplacements[initType];
                        // 在IL生成器中声明一个新的局部变量
                        localRedirects[locIndex] = il.DeclareLocal(newType);
#if DEBUG
                        Info($"[JobPatch] PreScan: 将局部变量 [{locIndex}] ({initType.Name}) 重定向至 -> ({newType.Name})"); 
#endif
                    }
                    else if (locIndex == -1)
                    {
                        Warn($"[JobPatch] PreScan警告: 发现 initobj {initType.Name} 但无法回溯到 ldloca，可能操作的是参数(Arg)或复杂栈。位置: {i}");
                    }
                }
                // 预留位置：特征 2 (stfld) 处理被优化掉 initobj 的情况
            }

            // ==========================================================================================
            // PHASE 2: 指令重写 (Execution)
            // ==========================================================================================
            for (int i = 0; i < list.Count; i++)
            {
                var instruction = list[i];
                bool isPatched = false;

                // 1. 拦截 Initobj 并替换类型
                if (instruction.opcode == OpCodes.Initobj &&
                    instruction.operand is Type initType &&
                    context.JobReplacements.TryGetValue(initType, out Type newInitType))
                {
#if DEBUG
                    Info($"[JobPatch] Patch: Initobj {initType.Name} -> {newInitType.Name}"); 
#endif
                    var newInst = new CodeInstruction(OpCodes.Initobj, newInitType);
                    newInst.labels = instruction.labels; // 继承标签（跳转目标）
                    yield return newInst;
                    isPatched = true;
                    continue;
                }

                // 2. 拦截并重定向局部变量操作 (Load/Store/Addr)
                int currentLocIndex = GetLocalIndex(instruction);
                if (currentLocIndex >= 0 && localRedirects.TryGetValue(currentLocIndex, out LocalBuilder newLocalBuilder))
                {
                    // 创建新指令，使用新的 LocalBuilder
                    var newInst = new CodeInstruction(instruction.opcode, newLocalBuilder);
                    // 必须标准化OpCode，因为 LocalBuilder 无法用于 ldloc.0 这种短指令
                    newInst.opcode = ConvertToLongOpCode(instruction.opcode);
                    newInst.labels = instruction.labels;
                    newInst.blocks = instruction.blocks;
                    // Debug.Log($"[JobPatch] Patch: Local var {currentLocIndex} -> redirected.");
                    yield return newInst;
                    isPatched = true;
                    continue;
                }

                // 3. 字段写入 (Stfld) - 核心逻辑：类型欺骗 (Transmutation)
                if (!isPatched && instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo oldFieldInfo)
                {
                    if (context.FieldReplacements.TryGetValue(oldFieldInfo, out FieldInfo newFieldInfo))
                    {
                        // 如果类型不一致 (例如 private struct 问题，或者命名空间不同但布局相同的struct)
                        if (oldFieldInfo.FieldType != newFieldInfo.FieldType)
                        {
#if DEBUG
                            Info($"[JobPatch] Patch: Stfld 类型欺骗 {oldFieldInfo.Name} -> {newFieldInfo.Name}"); 
#endif

                            // 此时栈顶是 [Value(OldType)], 下面是 [ObjectRef(NewJob)]

                            // 步骤 A: 声明一个临时变量用于接收旧类型的值
                            LocalBuilder tempOldVal = il.DeclareLocal(oldFieldInfo.FieldType);

                            // 步骤 B: 将栈顶的旧值保存到临时变量 (Pop [Value])
                            var stlocInst = new CodeInstruction(OpCodes.Stloc, tempOldVal);
                            stlocInst.labels = instruction.labels;
                            yield return stlocInst;

                            // 步骤 C: 加载临时变量的地址 (Push [PtrToTemp])
                            yield return new CodeInstruction(OpCodes.Ldloca, tempOldVal);

                            // 步骤 D: 关键点！强制以新类型读取该内存地址 (Read [PtrToTemp] as NewType)
                            // 这实现了二进制层面的直接拷贝 (Reinterpret Cast)
                            yield return new CodeInstruction(OpCodes.Ldobj, newFieldInfo.FieldType);

                            // 步骤 E: 将转换后的值写入新字段 (Stfld consumes [ObjectRef] [NewValue])
                            yield return new CodeInstruction(OpCodes.Stfld, newFieldInfo);
                        }
                        else
                        {
                            // 类型完全匹配，直接替换 FieldInfo
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
                        // Debug.Log($"[JobPatch] Patch: Ldfld {fieldOp.Name} -> {newField.Name}");
                        var newInst = new CodeInstruction(instruction.opcode, newField);
                        newInst.labels = instruction.labels;
                        yield return newInst;
                        isPatched = true;
                        continue;
                    }
                }

                // 5. 泛型与类型引用替换 (Call/Callvirt/Metadata)
                if (!isPatched)
                {
                    CodeInstruction finalInst = instruction;

                    // 情况 A: 指令操作数直接是 Type (例如 ldtoken, isinst, box)
                    if (instruction.operand is Type opType && context.JobReplacements.TryGetValue(opType, out Type repType))
                    {
                        finalInst = new CodeInstruction(instruction.opcode, repType);
#if DEBUG
                        Info($"[JobPatch] Patch: Operand Type {opType.Name} -> {repType.Name}"); 
#endif
                    }
                    // 情况 B: 调用了泛型方法 (例如 NativeArray<OldJob>.Sort())
                    else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                             instruction.operand is MethodInfo method && method.IsGenericMethod)
                    {
                        var args = method.GetGenericArguments();
                        // 检查泛型参数是否需要替换
                        var newArgs = args.Select(t => context.JobReplacements.ContainsKey(t) ? context.JobReplacements[t] : t).ToArray();

                        if (!args.SequenceEqual(newArgs))
                        {
                            try
                            {
                                // 构造新的泛型方法
                                var newM = method.GetGenericMethodDefinition().MakeGenericMethod(newArgs);
                                finalInst = new CodeInstruction(instruction.opcode, newM);
#if DEBUG
                                Info($"[JobPatch] Patch: Generic Method {method.Name} -> New Generic Arguments"); 
#endif
                            }
                            catch (Exception e)
                            {
                                Error($"[JobPatch] 构造泛型方法失败: {method.Name}. Error: {e}");
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

            lock (successfullyProcessedMethods) successfullyProcessedMethods.Add(originalMethod);
            Info($" 方法 {originalMethod.Name} 处理完成。");
        }

        // --- 辅助函数 ---

        // 回溯寻找最近的 ldloca，跳过 nop，处理 labels 阻断
        private static int FindPreviousLocalIndex(List<CodeInstruction> list, int currentIndex)
        {
            for (int i = currentIndex - 1; i >= 0; i--)
            {
                var op = list[i].opcode;
                if (op == OpCodes.Nop) continue;

                // 找到 ldloca (加载地址)
                if (op == OpCodes.Ldloca || op == OpCodes.Ldloca_S)
                    return GetLocalIndex(list[i]);

                // 有时 struct 可能通过 ldloc 直接加载 (虽然 initobj 通常接地址)
                if (IsLdLoc(op))
                    return GetLocalIndex(list[i]);

                // 如果遇到其他指令，说明不是紧邻的初始化模式
                // 这里其实比较激进，如果 initobj 前面有复杂的参数压栈，这里会返回 -1，导致 patch 失败
                return -1;
            }
            return -1;
        }

        private static bool IsLdLoc(OpCode op)
        {
            return op == OpCodes.Ldloc || op == OpCodes.Ldloc_S ||
                   op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 ||
                   op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3;
        }

        private static int GetLocalIndex(CodeInstruction i)
        {
            if (i.operand is LocalBuilder lb) return lb.LocalIndex;
            if (i.operand is int index) return index;
            if (i.operand is byte bIndex) return bIndex; // 处理短格式操作数
            if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
            if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
            if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
            if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;
            if (i.opcode == OpCodes.Ldloca_S || i.opcode == OpCodes.Ldloc_S || i.opcode == OpCodes.Stloc_S)
                return Convert.ToInt32(i.operand);
            return -1;
        }

        private static OpCode ConvertToLongOpCode(OpCode op)
        {
            // 将短指令转换为长指令，以便接受 LocalBuilder 作为操作数
            if (op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3 || op == OpCodes.Ldloc_S) return OpCodes.Ldloc;
            if (op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3 || op == OpCodes.Stloc_S) return OpCodes.Stloc;
            if (op == OpCodes.Ldloca_S) return OpCodes.Ldloca;
            return op;
        }
    }

}
