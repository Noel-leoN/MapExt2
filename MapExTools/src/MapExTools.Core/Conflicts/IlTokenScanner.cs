using System.Collections.Immutable;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>无参 <c>Harmony.UnpatchAll(null)</c> 调用点所处的静态调用路径分类。</summary>
public enum UnpatchAllContext
{
    /// <summary>位于 <c>IMod.OnLoad</c>、系统生命周期（<c>OnCreate</c>/<c>OnUpdate</c>）或构造器可达路径（硬冲突）。</summary>
    InitializationOrLifecycle,

    /// <summary>仅位于 <c>IMod.OnDispose</c> 或仅被 <c>OnDispose</c> 递归调用的清理辅助方法（0 拦截，仅作备注）。</summary>
    DisposeOnly,

    /// <summary>位于其余回调方法（如设置按钮、委托），静态无法排除运行期触发可能（警告）。</summary>
    UnknownCallback,
}

/// <summary>检测到的 Harmony 补丁类型。</summary>
public enum DetectedPatchKind
{
    PrefixBlockingCapable,
    PrefixVoid,
    Transpiler,
    Postfix,
    Finalizer,
    DynamicOrImperative,
}

/// <summary>对受保护系统调用 <c>ComponentSystemBase.set_Enabled</c> 的静态证据。</summary>
public sealed record EnabledWriteEvidence(
    string CallerTypeAndMethod,
    string TargetSystemTypeName,
    bool? ConstantEnabledValue = null);

/// <summary>对已被替换的原版系统生命周期方法（如 <c>OnUpdate</c>）挂载 Harmony 补丁的静态证据。</summary>
public sealed record PatchedReplacedSystemEvidence(
    string PatchTypeAndMethod,
    string TargetTypeName,
    string TargetMethodName,
    DetectedPatchKind Kind);

/// <summary>将原版 <c>UpdateGroupSystem</c> 跨阶段注册到非 <c>Modification5</c> 阶段的静态证据。</summary>
public sealed record CrossPhaseScheduleEvidence(
    string CallerTypeAndMethod,
    string TargetSystemTypeName,
    int PhaseCode);

/// <summary>对 MapExt 补丁目标方法挂载 Harmony 补丁的静态证据。</summary>
public sealed record HarmonyOverlapEvidence(
    string PatchTypeAndMethod,
    string TargetTypeName,
    string TargetMethodName,
    string? GenericArgumentTypeName,
    DetectedPatchKind Kind);

/// <summary>调用无参 <c>Harmony.UnpatchAll(null)</c> 的静态证据。</summary>
public sealed record UnpatchAllNullEvidence(
    string CallerTypeAndMethod,
    UnpatchAllContext Context);

/// <summary>同一方法体内同时出现 <c>"kMapSize"</c>/<c>"kTextureSize"</c> 与 <c>FieldInfo.SetValue</c> 的静态证据。</summary>
public sealed record StaticFieldReflectionWriteEvidence(
    string CallerTypeAndMethod,
    string FieldNameLiteral);

/// <summary>方法体内调用原版 <c>WaterSystem.set_WaterSimSpeed</c>（写水模拟速度）的静态证据。</summary>
public sealed record WaterSimSpeedWriteEvidence(
    string CallerTypeAndMethod);

/// <summary>
/// 基于 <see cref="MetadataReader"/> 的单遍 ECMA-335 IL 指令与签名扫描器：
/// 包含轻量求值栈接收者标签追踪（防 <c>this.Enabled = false</c> 误报并支持非常量布尔实参与局部变量中转）、
/// 同方法静态字段反射改写识别、<c>WaterSimSpeed</c> 写入识别及 <c>UnpatchAll(null)</c> 调用图上下文分级。
/// </summary>
internal static class IlTokenScanner
{
    private readonly record struct OpInfo(OpCode Code, int OperandBytes);

    private static readonly OpInfo[] s_oneByteOps = new OpInfo[256];
    private static readonly OpInfo[] s_twoByteOps = new OpInfo[256];

    static IlTokenScanner()
    {
        foreach (System.Reflection.FieldInfo field in typeof(OpCodes).GetFields(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.FieldType != typeof(OpCode))
                continue;

            var op = (OpCode)field.GetValue(null)!;
            ushort val = (ushort)op.Value;
            int bytes = GetOperandByteLength(op.OperandType);
            if (val < 0x100)
                s_oneByteOps[val] = new OpInfo(op, bytes);
            else if ((val & 0xFF00) == 0xFE00)
                s_twoByteOps[val & 0xFF] = new OpInfo(op, bytes);
        }
    }

    private enum TagKind : byte
    {
        Other = 0,
        Self = 1,
        ProtectedSystem = 2,
        NullLiteral = 3,
        IntConstant = 4,
        TypeToken = 5,
        StringLiteral = 6,
    }

    private readonly record struct SlotTag(TagKind Kind, string? SystemType, int IntValue = 0)
    {
        public static readonly SlotTag Other = new(TagKind.Other, null);
        public static readonly SlotTag Self = new(TagKind.Self, null);
        public static readonly SlotTag Null = new(TagKind.NullLiteral, null);
        public static SlotTag Protected(string systemType) => new(TagKind.ProtectedSystem, systemType);
        public static SlotTag IntConst(int value) => new(TagKind.IntConstant, null, value);
        public static SlotTag Type(string typeName) => new(TagKind.TypeToken, typeName);
        public static SlotTag Str(string literal) => new(TagKind.StringLiteral, literal);
    }

    /// <summary>预扫描类型内的字段写入，找出存有受保护系统引用的字段（即使声明类型为基类 <c>ComponentSystemBase</c> 也能追踪）。</summary>
    public static Dictionary<EntityHandle, string> CollectProtectedSystemFields(
        MetadataReader md,
        PEReader peReader,
        EcmaTypeNameProvider typeProvider)
    {
        var map = new Dictionary<EntityHandle, string>();

        // 1. 先按字段声明类型直接匹配
        foreach (FieldDefinitionHandle fh in md.FieldDefinitions)
        {
            FieldDefinition fd = md.GetFieldDefinition(fh);
            string? fieldType = TryDecodeFieldType(fd, typeProvider);
            if (fieldType != null && ConflictTargets.IsProtectedEnabledTarget(fieldType))
            {
                map[fh] = fieldType;
            }
        }

        // 2. 再扫构造器与 OnCreate 中的 GetOrCreateSystemManaged<T>() -> stfld/stsfld 传播
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition td = md.GetTypeDefinition(th);
            foreach (MethodDefinitionHandle mh in td.GetMethods())
            {
                MethodDefinition m = md.GetMethodDefinition(mh);
                if (m.RelativeVirtualAddress == 0)
                    continue;
                string mName = md.GetString(m.Name);
                if (mName is not ("OnCreate" or ".ctor" or ".cctor" or "OnLoad"))
                    continue;

                MethodBodyBlock body;
                try
                {
                    body = peReader.GetMethodBody(m.RelativeVirtualAddress);
                }
                catch (BadImageFormatException)
                {
                    continue;
                }

                ScanFieldAssignmentsInBody(md, body.GetILBytes(), typeProvider, map);
            }
        }

        return map;
    }

    /// <summary>对单个方法体执行单遍扫描：提取受保护系统 <c>set_Enabled</c> 写入、跨阶段注册、<c>UnpatchAll(null)</c>、同方法 <c>kMapSize</c> 反射写、<c>WaterSimSpeed</c> 写入及方法内调用边。</summary>
    public static void ScanMethodBody(
        MetadataReader md,
        MethodDefinitionHandle methodHandle,
        MethodDefinition methodDef,
        string callerDisplayName,
        MethodBodyBlock body,
        EcmaTypeNameProvider typeProvider,
        IReadOnlyDictionary<EntityHandle, string> protectedFields,
        List<EnabledWriteEvidence> enabledWrites,
        List<CrossPhaseScheduleEvidence> crossPhaseSchedules,
        List<(MethodDefinitionHandle Handle, string Caller)> unpatchAllNullCalls,
        List<StaticFieldReflectionWriteEvidence> reflectionWrites,
        List<WaterSimSpeedWriteEvidence> waterSimSpeedWrites,
        HashSet<(MethodDefinitionHandle Caller, MethodDefinitionHandle Callee)> internalEdges,
        HashSet<string> assemblySensitiveTypeLiterals,
        out List<string>? referencedTypesInMethod,
        out List<string>? stringLiteralsInMethod,
        out List<(string TargetType, string MethodName)>? reflectedMethodTargets,
        out bool callsImperativeHarmonyPatch)
    {
        referencedTypesInMethod = null;
        stringLiteralsInMethod = null;
        reflectedMethodTargets = null;
        callsImperativeHarmonyPatch = false;

        byte[]? il = body.GetILBytes();
        if (il == null || il.Length == 0)
            return;

        MethodSignature<string?>? methodSig = TryDecodeMethodSignature(methodDef, typeProvider);
        bool isInstance = methodSig?.Header.IsInstance ?? ((methodDef.Attributes & System.Reflection.MethodAttributes.Static) == 0);
        ImmutableArray<string?> paramTypes = methodSig?.ParameterTypes ?? ImmutableArray<string?>.Empty;
        ImmutableArray<string?> localTypes = TryDecodeLocals(md, body, typeProvider);

        // 初始化形参标签（槽 0 在实例方法中固定为 Self）
        int argSlotCount = (isInstance ? 1 : 0) + paramTypes.Length;
        var argTags = new SlotTag[Math.Max(argSlotCount, 4)];
        if (isInstance)
            argTags[0] = SlotTag.Self;
        for (int i = 0; i < paramTypes.Length; i++)
        {
            string? pt = paramTypes[i];
            int slot = (isInstance ? 1 : 0) + i;
            if (pt != null && ConflictTargets.IsProtectedEnabledTarget(pt))
                argTags[slot] = SlotTag.Protected(pt);
        }

        // 初始化局部变量标签
        var localTags = new SlotTag[Math.Max(localTypes.Length, 8)];
        for (int i = 0; i < localTypes.Length; i++)
        {
            string? lt = localTypes[i];
            if (lt != null && ConflictTargets.IsProtectedEnabledTarget(lt))
                localTags[i] = SlotTag.Protected(lt);
        }

        var stack = new List<SlotTag>(8);
        Dictionary<int, List<SlotTag>>? branchStacks = null;
        string? seenMapSizeLiteral = null;
        bool seenFieldSetValue = false;

        int pos = 0;
        while (pos < il.Length)
        {
            if (branchStacks != null && branchStacks.TryGetValue(pos, out List<SlotTag>? incoming))
            {
                MergeIntoCurrentStack(stack, incoming);
            }

            byte opByte = il[pos++];
            OpInfo info;
            if (opByte == 0xFE)
            {
                if (pos >= il.Length)
                    break;
                info = s_twoByteOps[il[pos++]];
            }
            else
            {
                info = s_oneByteOps[opByte];
            }

            OpCode op = info.Code;
            if (op.Size == 0)
                break;

            int operandStart = pos;

            if (op.OperandType == OperandType.InlineSwitch)
            {
                if (pos + 4 > il.Length)
                    break;
                uint count = BitConverter.ToUInt32(il, pos);
                pos += 4;
                long jumpBytes = (long)count * 4;
                if (pos + jumpBytes > il.Length)
                    break;

                // switch 弹出 1 个整数条件值并向各目标分支传播栈
                PopSafe(stack);
                for (uint i = 0; i < count; i++)
                {
                    int delta = BitConverter.ToInt32(il, pos + (int)(i * 4));
                    int target = pos + (int)jumpBytes + delta;
                    RecordBranchStack(ref branchStacks, target, stack);
                }

                pos += (int)jumpBytes;
                continue;
            }

            if (pos + info.OperandBytes > il.Length)
                break;
            pos += info.OperandBytes;

            // === 按语义处理关键指令 ===
            short opVal = op.Value;

            // 1. ldnull 与整数常量（用于识别布尔常量实参及 SystemUpdatePhase 枚举值）
            if (opVal == OpCodes.Ldnull.Value)
            {
                stack.Add(SlotTag.Null);
                continue;
            }

            if (TryGetLdcI4Value(opVal, il, operandStart, out int constInt))
            {
                stack.Add(SlotTag.IntConst(constInt));
                continue;
            }

            // 2. ldarg 系列
            if (TryGetLdargIndex(opVal, il, operandStart, out int argIdx))
            {
                stack.Add(argIdx >= 0 && argIdx < argTags.Length ? argTags[argIdx] : SlotTag.Other);
                continue;
            }

            // 3. ldloc 系列
            if (TryGetLdlocIndex(opVal, il, operandStart, out int ldlocIdx))
            {
                stack.Add(ldlocIdx >= 0 && ldlocIdx < localTags.Length ? localTags[ldlocIdx] : SlotTag.Other);
                continue;
            }

            // 4. stloc 系列（保留 ProtectedSystem，同时传递 IntConstant / NullLiteral / TypeToken / StringLiteral / Self）
            if (TryGetStlocIndex(opVal, il, operandStart, out int stlocIdx))
            {
                SlotTag val = PopSafe(stack);
                EnsureCapacity(ref localTags, stlocIdx + 1);
                if (val.Kind == TagKind.ProtectedSystem)
                {
                    localTags[stlocIdx] = val;
                }
                else if (localTags[stlocIdx].Kind != TagKind.ProtectedSystem)
                {
                    localTags[stlocIdx] = val;
                }

                continue;
            }

            // 5. dup 与 pop
            if (opVal == OpCodes.Dup.Value)
            {
                stack.Add(stack.Count > 0 ? stack[^1] : SlotTag.Other);
                continue;
            }

            if (opVal == OpCodes.Pop.Value)
            {
                PopSafe(stack);
                continue;
            }

            // 6. ldstr：提取字符串字面量（检查 kMapSize / kTextureSize、受保护完整类型名及动态补丁方法名）
            if (opVal == OpCodes.Ldstr.Value)
            {
                int token = BitConverter.ToInt32(il, operandStart);
                UserStringHandle ush = System.Reflection.Metadata.Ecma335.MetadataTokens.UserStringHandle(token);
                string? pushedLiteral = null;
                if (!ush.IsNil)
                {
                    string str = md.GetUserString(ush);
                    if (str is "kMapSize" or "kTextureSize")
                        seenMapSizeLiteral ??= str;

                    // 支持提取 "Namespace.Type, Assembly" 或 "Namespace.Type" 形式的完整敏感类型字符串
                    string typeCandidate = str;
                    int comma = typeCandidate.IndexOf(',');
                    if (comma > 0)
                        typeCandidate = typeCandidate[..comma].Trim();
                    if (ConflictTargets.IsTargetSystemOrPatchTypeName(typeCandidate))
                        assemblySensitiveTypeLiterals.Add(typeCandidate);

                    stringLiteralsInMethod ??= [];
                    if (str.Length is > 0 and <= 120)
                    {
                        stringLiteralsInMethod.Add(str);
                        pushedLiteral = str;
                    }
                }

                stack.Add(pushedLiteral != null ? SlotTag.Str(pushedLiteral) : SlotTag.Other);
                continue;
            }

            // 7. ldtoken / castclass / isinst：记录引用的类型并透传 typeof(T) 标签
            if (opVal == OpCodes.Ldtoken.Value || opVal == OpCodes.Castclass.Value || opVal == OpCodes.Isinst.Value)
            {
                int token = BitConverter.ToInt32(il, operandStart);
                EntityHandle eh = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);
                string? typeName = ResolveTypeToken(md, eh, typeProvider);
                if (typeName != null)
                {
                    referencedTypesInMethod ??= [];
                    referencedTypesInMethod.Add(typeName);
                }

                if (opVal != OpCodes.Ldtoken.Value)
                    PopSafe(stack);

                if (typeName != null && ConflictTargets.IsProtectedEnabledTarget(typeName))
                    stack.Add(SlotTag.Protected(typeName));
                else if (opVal == OpCodes.Ldtoken.Value && typeName != null)
                    stack.Add(SlotTag.Type(typeName));
                else
                    stack.Add(SlotTag.Other);
                continue;
            }

            // 8. ldfld / ldsfld
            if (opVal == OpCodes.Ldfld.Value || opVal == OpCodes.Ldsfld.Value)
            {
                if (opVal == OpCodes.Ldfld.Value)
                    PopSafe(stack);

                int token = BitConverter.ToInt32(il, operandStart);
                EntityHandle fh = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);
                string? fieldSys = ResolveProtectedFieldType(md, fh, typeProvider, protectedFields);
                stack.Add(fieldSys != null ? SlotTag.Protected(fieldSys) : SlotTag.Other);
                continue;
            }

            // 9. call / callvirt / newobj
            if (opVal == OpCodes.Call.Value || opVal == OpCodes.Callvirt.Value || opVal == OpCodes.Newobj.Value)
            {
                int token = BitConverter.ToInt32(il, operandStart);
                EntityHandle mh = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);

                if (mh.Kind == HandleKind.MethodDefinition)
                {
                    internalEdges.Add((methodHandle, (MethodDefinitionHandle)mh));
                }

                if (TryResolveCalledMethod(md, mh, typeProvider, out CalledMethodInfo callInfo))
                {
                    if (callInfo.DeclaringType != null)
                    {
                        referencedTypesInMethod ??= [];
                        referencedTypesInMethod.Add(callInfo.DeclaringType);
                    }

                    // 9a. 检查 FieldInfo::SetValue / SetValueDirect（同方法 kMapSize 反射写）
                    if (callInfo.MethodName is "SetValue" or "SetValueDirect" &&
                        callInfo.DeclaringType is "System.Reflection.FieldInfo" or "System.Reflection.MemberInfo")
                    {
                        seenFieldSetValue = true;
                    }

                    // 9b. 检查命令式 Harmony.Patch / PatchProcessor.Patch
                    if (callInfo.MethodName == "Patch" &&
                        callInfo.DeclaringType is "HarmonyLib.Harmony" or "HarmonyLib.PatchProcessor")
                    {
                        callsImperativeHarmonyPatch = true;
                    }

                    // 9b-2. 检查写原版 WaterSystem.WaterSimSpeed（只记录调用方法，求值栈仍按 9f 常规路径弹出实参与接收者）
                    if (callInfo.MethodName == ConflictTargets.WaterSimSpeedSetterName &&
                        callInfo.DeclaringType == ConflictTargets.WaterSystemTypeName &&
                        callInfo.IsInstance &&
                        opVal != OpCodes.Newobj.Value)
                    {
                        waterSimSpeedWrites.Add(new WaterSimSpeedWriteEvidence(callerDisplayName));
                    }

                    // 9c. 检查 Harmony.UnpatchAll(string) 且栈顶参数为 ldnull
                    if (callInfo.MethodName == "UnpatchAll" &&
                        callInfo.DeclaringType == "HarmonyLib.Harmony" &&
                        callInfo.ParameterCount == 1 &&
                        opVal != OpCodes.Newobj.Value)
                    {
                        SlotTag idArg = PopSafe(stack);
                        if (callInfo.IsInstance)
                            PopSafe(stack);
                        if (idArg.Kind == TagKind.NullLiteral)
                        {
                            unpatchAllNullCalls.Add((methodHandle, callerDisplayName));
                        }

                        if (callInfo.HasReturnValue)
                            stack.Add(SlotTag.Other);
                        continue;
                    }

                    // 9d. 检查 ComponentSystemBase::set_Enabled(bool) 接收者标签与布尔常量实参
                    if (callInfo.MethodName == "set_Enabled" &&
                        callInfo.ParameterCount == 1 &&
                        callInfo.IsInstance &&
                        opVal != OpCodes.Newobj.Value)
                    {
                        SlotTag boolArg = PopSafe(stack);
                        SlotTag receiver = PopSafe(stack);
                        bool? constBool = boolArg.Kind == TagKind.IntConstant ? (boolArg.IntValue != 0) : null;

                        if (receiver.Kind == TagKind.ProtectedSystem && receiver.SystemType != null)
                        {
                            enabledWrites.Add(new EnabledWriteEvidence(callerDisplayName, receiver.SystemType, constBool));
                        }
                        else if (receiver.Kind != TagKind.Self &&
                                 callInfo.DeclaringType != null &&
                                 ConflictTargets.IsProtectedEnabledTarget(callInfo.DeclaringType))
                        {
                            enabledWrites.Add(new EnabledWriteEvidence(callerDisplayName, callInfo.DeclaringType, constBool));
                        }

                        continue;
                    }

                    // 9e. Type.GetTypeFromHandle(RuntimeTypeHandle)：透传 ldtoken T 标记（ProtectedSystem 与普通 TypeToken）
                    if (callInfo.MethodName == "GetTypeFromHandle" &&
                        callInfo.DeclaringType == "System.Type" &&
                        callInfo.ParameterCount == 1 &&
                        !callInfo.IsInstance)
                    {
                        SlotTag handleTag = PopSafe(stack);
                        stack.Add(handleTag.Kind is TagKind.ProtectedSystem or TagKind.TypeToken ? handleTag : SlotTag.Other);
                        continue;
                    }

                    // 9f. 常规方法参数与 this 弹出 + 反射方法配对追踪 + 跨阶段注册检测 + 系统获取方法压入标签
                    SlotTag firstArgTag = SlotTag.Other;
                    SlotTag secondArgTag = SlotTag.Other;
                    for (int p = callInfo.ParameterCount - 1; p >= 0; p--)
                    {
                        SlotTag popped = PopSafe(stack);
                        if (p == 0)
                            firstArgTag = popped;
                        else if (p == 1)
                            secondArgTag = popped;
                    }

                    SlotTag receiverTag = callInfo.IsInstance && opVal != OpCodes.Newobj.Value
                        ? PopSafe(stack)
                        : SlotTag.Other;

                    // 追踪反射查找目标方法的配对 (类型, 方法名)，用于命令式 harmony.Patch 精确归因（避免笛卡尔积误报）
                    RecordReflectedMethodTarget(callInfo, receiverTag, firstArgTag, secondArgTag, ref reflectedMethodTargets);

                    // 检测将原版 UpdateGroupSystem 注册到非 Modification5 (8) 阶段（如 GameSimulation = 12，引发 ModificationBarrier5 异常）
                    if (callInfo.DeclaringType == ConflictTargets.UpdateSystemTypeName &&
                        callInfo.MethodName is "UpdateAt" or "UpdateBefore" or "UpdateAfter" &&
                        callInfo.ParameterCount == 1 &&
                        callInfo.FirstGenericMethodArg == ConflictTargets.UpdateGroupSystemTypeName &&
                        (firstArgTag.Kind != TagKind.IntConstant || firstArgTag.IntValue != 8))
                    {
                        int phaseCode = firstArgTag.Kind == TagKind.IntConstant ? firstArgTag.IntValue : -1;
                        crossPhaseSchedules.Add(new CrossPhaseScheduleEvidence(
                            callerDisplayName,
                            ConflictTargets.UpdateGroupSystemTypeName,
                            phaseCode));
                    }

                    if (opVal == OpCodes.Newobj.Value)
                    {
                        string? constructed = callInfo.DeclaringType;
                        stack.Add(constructed != null && ConflictTargets.IsProtectedEnabledTarget(constructed)
                            ? SlotTag.Protected(constructed)
                            : SlotTag.Other);
                    }
                    else if (callInfo.MethodName is "GetOrCreateSystemManaged" or "GetExistingSystemManaged")
                    {
                        string? sysType = callInfo.FirstGenericMethodArg;
                        if (sysType == null && callInfo.ParameterCount == 1 && firstArgTag.Kind == TagKind.ProtectedSystem)
                        {
                            sysType = firstArgTag.SystemType;
                        }

                        if (sysType != null)
                        {
                            referencedTypesInMethod ??= [];
                            referencedTypesInMethod.Add(sysType);
                        }

                        stack.Add(sysType != null && ConflictTargets.IsProtectedEnabledTarget(sysType)
                            ? SlotTag.Protected(sysType)
                            : SlotTag.Other);
                    }
                    else if (callInfo.HasReturnValue &&
                             firstArgTag.Kind == TagKind.StringLiteral &&
                             firstArgTag.SystemType is { } resolvedTypeStr &&
                             ((callInfo.DeclaringType == "HarmonyLib.AccessTools" && callInfo.MethodName == "TypeByName") ||
                              (callInfo.DeclaringType == "System.Type" && callInfo.MethodName == "GetType" && !callInfo.IsInstance)))
                    {
                        (string parsedType, _) = ModAssemblyInspector.ParseSerializedTypeString(resolvedTypeStr);
                        stack.Add(ConflictTargets.IsProtectedEnabledTarget(parsedType)
                            ? SlotTag.Protected(parsedType)
                            : SlotTag.Type(parsedType));
                    }
                    else if (callInfo.HasReturnValue)
                    {
                        string? ret = callInfo.ReturnType;
                        stack.Add(ret != null && ConflictTargets.IsProtectedEnabledTarget(ret)
                            ? SlotTag.Protected(ret)
                            : SlotTag.Other);
                    }

                    continue;
                }
            }

            // 10. 分支跳转与无条件终结指令
            if (op.OperandType is OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)
            {
                int delta = op.OperandType == OperandType.ShortInlineBrTarget
                    ? (sbyte)il[operandStart]
                    : BitConverter.ToInt32(il, operandStart);
                int target = pos + delta;

                ApplyDefaultStackPop(stack, op.StackBehaviourPop);
                RecordBranchStack(ref branchStacks, target, stack);

                if (opVal == OpCodes.Br.Value || opVal == OpCodes.Br_S.Value ||
                    opVal == OpCodes.Leave.Value || opVal == OpCodes.Leave_S.Value)
                {
                    stack.Clear();
                }

                continue;
            }

            if (opVal == OpCodes.Ret.Value || opVal == OpCodes.Throw.Value || opVal == OpCodes.Rethrow.Value)
            {
                stack.Clear();
                continue;
            }

            // 11. 其余普通指令：按 ECMA 标准栈行为推进
            ApplyDefaultStackPop(stack, op.StackBehaviourPop);
            ApplyDefaultStackPush(stack, op.StackBehaviourPush);
        }

        if (seenMapSizeLiteral != null && seenFieldSetValue)
        {
            reflectionWrites.Add(new StaticFieldReflectionWriteEvidence(callerDisplayName, seenMapSizeLiteral));
        }
    }

    private readonly record struct CalledMethodInfo(
        string? DeclaringType,
        string MethodName,
        int ParameterCount,
        bool IsInstance,
        bool HasReturnValue,
        string? ReturnType,
        string? FirstGenericMethodArg);

    private static void ScanFieldAssignmentsInBody(
        MetadataReader md,
        byte[]? il,
        EcmaTypeNameProvider typeProvider,
        Dictionary<EntityHandle, string> fieldMap)
    {
        if (il == null || il.Length == 0)
            return;

        string? pendingProtected = null;
        int pos = 0;
        while (pos < il.Length)
        {
            byte b = il[pos++];
            OpInfo info;
            if (b == 0xFE)
            {
                if (pos >= il.Length) break;
                info = s_twoByteOps[il[pos++]];
            }
            else
            {
                info = s_oneByteOps[b];
            }

            if (info.Code.Size == 0)
                break;

            int operandStart = pos;
            if (info.Code.OperandType == OperandType.InlineSwitch)
            {
                if (pos + 4 > il.Length) break;
                uint count = BitConverter.ToUInt32(il, pos);
                pos += 4;
                long jumpBytes = (long)count * 4;
                if (pos + jumpBytes > il.Length) break;
                pos += (int)jumpBytes;
                pendingProtected = null;
                continue;
            }

            if (pos + info.OperandBytes > il.Length)
                break;
            pos += info.OperandBytes;

            short opVal = info.Code.Value;
            if (opVal == OpCodes.Call.Value || opVal == OpCodes.Callvirt.Value)
            {
                EntityHandle mh = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(BitConverter.ToInt32(il, operandStart));
                if (TryResolveCalledMethod(md, mh, typeProvider, out CalledMethodInfo call) &&
                    call.MethodName is "GetOrCreateSystemManaged" or "GetExistingSystemManaged" &&
                    call.FirstGenericMethodArg is { } sys &&
                    ConflictTargets.IsProtectedEnabledTarget(sys))
                {
                    pendingProtected = sys;
                    continue;
                }

                pendingProtected = null;
            }
            else if ((opVal == OpCodes.Stfld.Value || opVal == OpCodes.Stsfld.Value) && pendingProtected != null)
            {
                EntityHandle fh = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(BitConverter.ToInt32(il, operandStart));
                if (fh.Kind == HandleKind.FieldDefinition)
                    fieldMap[fh] = pendingProtected;
                pendingProtected = null;
            }
            else if (opVal != OpCodes.Castclass.Value && opVal != OpCodes.Isinst.Value && opVal != OpCodes.Nop.Value)
            {
                pendingProtected = null;
            }
        }
    }

    private static bool TryResolveCalledMethod(
        MetadataReader md,
        EntityHandle handle,
        EcmaTypeNameProvider typeProvider,
        out CalledMethodInfo info)
    {
        info = default;
        try
        {
            string? firstGenArg = null;
            if (handle.Kind == HandleKind.MethodSpecification)
            {
                MethodSpecification spec = md.GetMethodSpecification((MethodSpecificationHandle)handle);
                ImmutableArray<string?> genArgs = spec.DecodeSignature(typeProvider, GenericContext.Empty);
                if (genArgs.Length > 0)
                    firstGenArg = genArgs[0];
                handle = spec.Method;
            }

            if (handle.Kind == HandleKind.MemberReference)
            {
                MemberReference mr = md.GetMemberReference((MemberReferenceHandle)handle);
                string name = md.GetString(mr.Name);
                string? declType = ResolveTypeToken(md, mr.Parent, typeProvider);
                MethodSignature<string?> sig = mr.DecodeMethodSignature(typeProvider, GenericContext.Empty);
                bool hasRet = sig.ReturnType != "System.Void";
                info = new CalledMethodInfo(
                    declType,
                    name,
                    sig.ParameterTypes.Length,
                    sig.Header.IsInstance,
                    hasRet,
                    sig.ReturnType,
                    firstGenArg);
                return true;
            }

            if (handle.Kind == HandleKind.MethodDefinition)
            {
                MethodDefinition mdef = md.GetMethodDefinition((MethodDefinitionHandle)handle);
                string name = md.GetString(mdef.Name);
                string? declType = PlaysetModScanner.GetEntityFullTypeName(md, mdef.GetDeclaringType());
                MethodSignature<string?> sig = mdef.DecodeSignature(typeProvider, GenericContext.Empty);
                bool hasRet = sig.ReturnType != "System.Void";
                info = new CalledMethodInfo(
                    declType,
                    name,
                    sig.ParameterTypes.Length,
                    sig.Header.IsInstance,
                    hasRet,
                    sig.ReturnType,
                    firstGenArg);
                return true;
            }
        }
        catch (BadImageFormatException)
        {
            return false;
        }

        return false;
    }

    private static string? ResolveProtectedFieldType(
        MetadataReader md,
        EntityHandle fieldHandle,
        EcmaTypeNameProvider typeProvider,
        IReadOnlyDictionary<EntityHandle, string> protectedFields)
    {
        if (protectedFields.TryGetValue(fieldHandle, out string? mapped))
            return mapped;

        try
        {
            if (fieldHandle.Kind == HandleKind.FieldDefinition)
            {
                FieldDefinition fd = md.GetFieldDefinition((FieldDefinitionHandle)fieldHandle);
                string? ft = fd.DecodeSignature(typeProvider, GenericContext.Empty);
                return ft != null && ConflictTargets.IsProtectedEnabledTarget(ft) ? ft : null;
            }

            if (fieldHandle.Kind == HandleKind.MemberReference)
            {
                MemberReference mr = md.GetMemberReference((MemberReferenceHandle)fieldHandle);
                string? ft = mr.DecodeFieldSignature(typeProvider, GenericContext.Empty);
                return ft != null && ConflictTargets.IsProtectedEnabledTarget(ft) ? ft : null;
            }
        }
        catch (BadImageFormatException)
        {
            return null;
        }

        return null;
    }

    private static string? ResolveTypeToken(MetadataReader md, EntityHandle handle, EcmaTypeNameProvider typeProvider)
    {
        if (handle.Kind == HandleKind.TypeSpecification)
        {
            try
            {
                TypeSpecification ts = md.GetTypeSpecification((TypeSpecificationHandle)handle);
                return ts.DecodeSignature(typeProvider, GenericContext.Empty);
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }

        return PlaysetModScanner.GetEntityFullTypeName(md, handle);
    }

    private static string? TryDecodeFieldType(FieldDefinition fd, EcmaTypeNameProvider provider)
    {
        try
        {
            return fd.DecodeSignature(provider, GenericContext.Empty);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static MethodSignature<string?>? TryDecodeMethodSignature(MethodDefinition md, EcmaTypeNameProvider provider)
    {
        try
        {
            return md.DecodeSignature(provider, GenericContext.Empty);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static ImmutableArray<string?> TryDecodeLocals(MetadataReader md, MethodBodyBlock body, EcmaTypeNameProvider provider)
    {
        if (body.LocalSignature.IsNil)
            return ImmutableArray<string?>.Empty;
        try
        {
            StandaloneSignature sig = md.GetStandaloneSignature(body.LocalSignature);
            return sig.DecodeLocalSignature(provider, GenericContext.Empty);
        }
        catch (BadImageFormatException)
        {
            return ImmutableArray<string?>.Empty;
        }
    }

    private static bool TryGetLdcI4Value(short opVal, byte[] il, int operandStart, out int value)
    {
        if (opVal == OpCodes.Ldc_I4_M1.Value) { value = -1; return true; }
        if (opVal == OpCodes.Ldc_I4_0.Value) { value = 0; return true; }
        if (opVal == OpCodes.Ldc_I4_1.Value) { value = 1; return true; }
        if (opVal == OpCodes.Ldc_I4_2.Value) { value = 2; return true; }
        if (opVal == OpCodes.Ldc_I4_3.Value) { value = 3; return true; }
        if (opVal == OpCodes.Ldc_I4_4.Value) { value = 4; return true; }
        if (opVal == OpCodes.Ldc_I4_5.Value) { value = 5; return true; }
        if (opVal == OpCodes.Ldc_I4_6.Value) { value = 6; return true; }
        if (opVal == OpCodes.Ldc_I4_7.Value) { value = 7; return true; }
        if (opVal == OpCodes.Ldc_I4_8.Value) { value = 8; return true; }
        if (opVal == OpCodes.Ldc_I4_S.Value) { value = (sbyte)il[operandStart]; return true; }
        if (opVal == OpCodes.Ldc_I4.Value) { value = BitConverter.ToInt32(il, operandStart); return true; }
        value = 0;
        return false;
    }

    private static bool TryGetLdargIndex(short opVal, byte[] il, int operandStart, out int index)
    {
        if (opVal == OpCodes.Ldarg_0.Value) { index = 0; return true; }
        if (opVal == OpCodes.Ldarg_1.Value) { index = 1; return true; }
        if (opVal == OpCodes.Ldarg_2.Value) { index = 2; return true; }
        if (opVal == OpCodes.Ldarg_3.Value) { index = 3; return true; }
        if (opVal == OpCodes.Ldarg_S.Value) { index = il[operandStart]; return true; }
        if (opVal == OpCodes.Ldarg.Value) { index = BitConverter.ToUInt16(il, operandStart); return true; }
        index = -1;
        return false;
    }

    private static bool TryGetLdlocIndex(short opVal, byte[] il, int operandStart, out int index)
    {
        if (opVal == OpCodes.Ldloc_0.Value) { index = 0; return true; }
        if (opVal == OpCodes.Ldloc_1.Value) { index = 1; return true; }
        if (opVal == OpCodes.Ldloc_2.Value) { index = 2; return true; }
        if (opVal == OpCodes.Ldloc_3.Value) { index = 3; return true; }
        if (opVal == OpCodes.Ldloc_S.Value) { index = il[operandStart]; return true; }
        if (opVal == OpCodes.Ldloc.Value) { index = BitConverter.ToUInt16(il, operandStart); return true; }
        index = -1;
        return false;
    }

    private static bool TryGetStlocIndex(short opVal, byte[] il, int operandStart, out int index)
    {
        if (opVal == OpCodes.Stloc_0.Value) { index = 0; return true; }
        if (opVal == OpCodes.Stloc_1.Value) { index = 1; return true; }
        if (opVal == OpCodes.Stloc_2.Value) { index = 2; return true; }
        if (opVal == OpCodes.Stloc_3.Value) { index = 3; return true; }
        if (opVal == OpCodes.Stloc_S.Value) { index = il[operandStart]; return true; }
        if (opVal == OpCodes.Stloc.Value) { index = BitConverter.ToUInt16(il, operandStart); return true; }
        index = -1;
        return false;
    }

    private static SlotTag PopSafe(List<SlotTag> stack)
    {
        if (stack.Count == 0)
            return SlotTag.Other;
        SlotTag top = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return top;
    }

    private static void EnsureCapacity(ref SlotTag[] arr, int minLen)
    {
        if (arr.Length < minLen)
            Array.Resize(ref arr, Math.Max(minLen, arr.Length * 2));
    }

    private static void RecordBranchStack(ref Dictionary<int, List<SlotTag>>? map, int targetOffset, List<SlotTag> current)
    {
        if (targetOffset < 0 || current.Count == 0)
            return;
        map ??= [];
        if (!map.TryGetValue(targetOffset, out List<SlotTag>? existing))
        {
            map[targetOffset] = [.. current];
            return;
        }

        MergeIntoCurrentStack(existing, current);
    }

    private static void RecordReflectedMethodTarget(
        CalledMethodInfo callInfo,
        SlotTag receiverTag,
        SlotTag firstArgTag,
        SlotTag secondArgTag,
        ref List<(string TargetType, string MethodName)>? sink)
    {
        if (callInfo.DeclaringType == "HarmonyLib.AccessTools")
        {
            if (callInfo.MethodName is "Method" or "DeclaredMethod")
            {
                if (TryGetTypeName(firstArgTag, out string? targetType) &&
                    secondArgTag.Kind == TagKind.StringLiteral &&
                    !string.IsNullOrEmpty(secondArgTag.SystemType))
                {
                    sink ??= [];
                    sink.Add((targetType, secondArgTag.SystemType));
                }
                else if (firstArgTag.Kind == TagKind.StringLiteral &&
                         firstArgTag.SystemType is { } combined)
                {
                    int colon = combined.IndexOf(':');
                    if (colon > 0 && colon < combined.Length - 1)
                    {
                        string tName = ModAssemblyInspector.ParseSerializedTypeString(combined[..colon]).TypeName;
                        string mName = combined[(colon + 1)..].Trim();
                        if (tName.Length > 0 && mName.Length > 0)
                        {
                            sink ??= [];
                            sink.Add((tName, mName));
                        }
                    }
                }
            }
            else if (callInfo.MethodName is "PropertyGetter" or "DeclaredPropertyGetter")
            {
                if (TryGetTypeName(firstArgTag, out string? targetType) &&
                    secondArgTag.Kind == TagKind.StringLiteral &&
                    !string.IsNullOrEmpty(secondArgTag.SystemType))
                {
                    sink ??= [];
                    sink.Add((targetType, "get_" + secondArgTag.SystemType));
                }
            }
            else if (callInfo.MethodName is "PropertySetter" or "DeclaredPropertySetter")
            {
                if (TryGetTypeName(firstArgTag, out string? targetType) &&
                    secondArgTag.Kind == TagKind.StringLiteral &&
                    !string.IsNullOrEmpty(secondArgTag.SystemType))
                {
                    sink ??= [];
                    sink.Add((targetType, "set_" + secondArgTag.SystemType));
                }
            }
            else if (callInfo.MethodName is "Property" or "DeclaredProperty")
            {
                if (TryGetTypeName(firstArgTag, out string? targetType) &&
                    secondArgTag.Kind == TagKind.StringLiteral &&
                    !string.IsNullOrEmpty(secondArgTag.SystemType))
                {
                    sink ??= [];
                    sink.Add((targetType, secondArgTag.SystemType));
                }
            }
            else if (callInfo.MethodName is "Constructor" or "DeclaredConstructor")
            {
                if (TryGetTypeName(firstArgTag, out string? targetType))
                {
                    sink ??= [];
                    sink.Add((targetType, ".ctor"));
                }
            }
        }
        else if (callInfo.DeclaringType == "System.Type" && callInfo.IsInstance &&
                 callInfo.MethodName is "GetMethod" or "GetProperty")
        {
            if (TryGetTypeName(receiverTag, out string? targetType) &&
                firstArgTag.Kind == TagKind.StringLiteral &&
                !string.IsNullOrEmpty(firstArgTag.SystemType))
            {
                sink ??= [];
                sink.Add((targetType, firstArgTag.SystemType));
            }
        }
    }

    private static bool TryGetTypeName(SlotTag tag, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? typeName)
    {
        if (tag.Kind is TagKind.ProtectedSystem or TagKind.TypeToken && !string.IsNullOrEmpty(tag.SystemType))
        {
            typeName = tag.SystemType;
            return true;
        }

        typeName = null;
        return false;
    }

    private static void MergeIntoCurrentStack(List<SlotTag> current, List<SlotTag> incoming)
    {
        if (current.Count == 0)
        {
            current.AddRange(incoming);
            return;
        }

        int count = Math.Min(current.Count, incoming.Count);
        for (int i = 0; i < count; i++)
        {
            current[i] = MergeSlot(current[i], incoming[i]);
        }
    }

    private static SlotTag MergeSlot(SlotTag a, SlotTag b)
    {
        if (a == b)
            return a;

        // ProtectedSystem 与 NullLiteral 保守并集：任一分支可能传入受保护系统或 null 都必须保留，避免条件路径漏报
        if (a.Kind == TagKind.ProtectedSystem)
            return a;
        if (b.Kind == TagKind.ProtectedSystem)
            return b;
        if (a.Kind == TagKind.NullLiteral || b.Kind == TagKind.NullLiteral)
            return SlotTag.Null;

        // IntConstant 等其他标签在两分支不一致时降为 Other（如 Enabled = cond ? false : true 不判为常量）
        return SlotTag.Other;
    }

    private static void ApplyDefaultStackPop(List<SlotTag> stack, StackBehaviour pop)
    {
        int count = pop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or
            StackBehaviour.Popi_popi8 or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or
            StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or
            StackBehaviour.Popref_popi_popr4 or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref or
            StackBehaviour.Popref_popi_pop1 => 3,
            _ => 0,
        };

        for (int i = 0; i < count; i++)
            PopSafe(stack);
    }

    private static void ApplyDefaultStackPush(List<SlotTag> stack, StackBehaviour push)
    {
        int count = push switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Push1_push1 => 2,
            StackBehaviour.Varpush => 0,
            _ => 1,
        };

        for (int i = 0; i < count; i++)
            stack.Add(SlotTag.Other);
    }

    private static int GetOperandByteLength(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
        OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
        OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 0,
        _ => 0,
    };
}

internal readonly record struct GenericContext
{
    public static readonly GenericContext Empty = default;
}

/// <summary>为 <see cref="MetadataReader"/> 解码方法、局部变量与泛型实例签名提供完整类型名称。</summary>
internal sealed class EcmaTypeNameProvider : ISignatureTypeProvider<string?, GenericContext>
{
    public string? GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Void => "System.Void",
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        _ => null,
    };

    public string? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
        PlaysetModScanner.GetEntityFullTypeName(reader, handle);

    public string? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
        PlaysetModScanner.GetEntityFullTypeName(reader, handle);

    public string? GetTypeFromSpecification(MetadataReader reader, GenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string? GetSZArrayType(string? elementType) => elementType != null ? elementType + "[]" : null;
    public string? GetArrayType(string? elementType, ArrayShape shape) => elementType != null ? elementType + "[]" : null;
    public string? GetByReferenceType(string? elementType) => elementType;
    public string? GetPointerType(string? elementType) => elementType != null ? elementType + "*" : null;
    public string? GetPinnedType(string? elementType) => elementType;
    public string? GetModifiedType(string? modifier, string? unmodifiedType, bool isRequired) => unmodifiedType;
    public string? GetFunctionPointerType(MethodSignature<string?> signature) => "System.IntPtr";
    public string? GetGenericMethodParameter(GenericContext genericContext, int index) => $"!!{index}";
    public string? GetGenericTypeParameter(GenericContext genericContext, int index) => $"!{index}";

    public string? GetGenericInstantiation(string? genericType, ImmutableArray<string?> typeArguments)
    {
        if (genericType == null)
            return null;
        if (typeArguments.IsEmpty)
            return genericType;
        return $"{genericType}<{string.Join(",", typeArguments)}>";
    }
}
