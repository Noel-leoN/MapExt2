using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>单个托管程序集的零反编译静态字节码检查结果。</summary>
public sealed record AssemblyInspectionResult(
    string AssemblyName,
    Version AssemblyVersion,
    bool ImplementsIMod,
    IReadOnlyList<string> CustomSystemTypes,
    IReadOnlyList<EnabledWriteEvidence> EnabledWrites,
    IReadOnlyList<HarmonyOverlapEvidence> HarmonyOverlaps,
    IReadOnlyList<UnpatchAllNullEvidence> UnpatchAllNullCalls,
    IReadOnlyList<StaticFieldReflectionWriteEvidence> ReflectionFieldWrites,
    IReadOnlyList<PatchedReplacedSystemEvidence>? ReplacedSystemPatches = null,
    IReadOnlyList<CrossPhaseScheduleEvidence>? CrossPhaseSchedules = null);

/// <summary>
/// 基于 <see cref="PEReader"/> 与 <see cref="MetadataReader"/> 的三级漏斗静态字节码检测器（无需反编译 C# 源码）。
/// </summary>
public static class ModAssemblyInspector
{
    private static readonly EcmaTypeNameProvider s_typeProvider = new();
    private static readonly CustomAttrTypeProvider s_attrProvider = new();

    /// <summary>从磁盘只读读取程序集字节并执行三级漏斗检查。</summary>
    public static AssemblyInspectionResult? InspectFile(string dllPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(dllPath);
        byte[] bytes;
        using (Stream fs = PlaysetModScanner.OpenSharedRead(dllPath))
        {
            bytes = new byte[fs.Length];
            fs.ReadExactly(bytes);
        }

        return InspectBytes(bytes);
    }

    /// <summary>从内存字节流执行三级漏斗检查（供后台扫描与 <c>PersistedAssemblyBuilder</c> 内存单元测试共用）。</summary>
    public static AssemblyInspectionResult? InspectBytes(byte[] peBytes)
    {
        ArgumentNullException.ThrowIfNull(peBytes);

        using var ms = new MemoryStream(peBytes, writable: false);
        using var peReader = new PEReader(ms, PEStreamOptions.LeaveOpen);

        // === 第 1 级：PE 与托管元数据过滤 ===
        if (!peReader.HasMetadata)
            return null;

        MetadataReader md = peReader.GetMetadataReader();
        if (!md.IsAssembly)
            return null;

        AssemblyDefinition asmDef = md.GetAssemblyDefinition();
        string asmName = md.GetString(asmDef.Name);
        Version asmVersion = asmDef.Version;

        // === 第 2 级：ECMA-335 表头 O(1) 粗筛（不解码方法体） ===
        bool refsGameOrEngine = false;
        bool refsHarmony = false;
        foreach (AssemblyReferenceHandle arh in md.AssemblyReferences)
        {
            string refName = md.GetString(md.GetAssemblyReference(arh).Name);
            if (string.Equals(refName, "Game", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(refName, "Unity.Entities", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(refName, "Colossal.Core", StringComparison.OrdinalIgnoreCase))
            {
                refsGameOrEngine = true;
            }
            else if (refName.Contains("Harmony", StringComparison.OrdinalIgnoreCase))
            {
                refsHarmony = true;
            }
        }

        bool implementsIMod = PlaysetModScanner.CheckImplementsIMod(md);
        List<string> customSystems = CollectCustomSystemTypes(md);

        if (!refsGameOrEngine && !refsHarmony)
        {
            return new AssemblyInspectionResult(asmName, asmVersion, implementsIMod, customSystems, [], [], [], []);
        }

        bool hitsSensitiveTypeRef = false;
        bool hitsHarmonyTypeRef = false;
        bool hitsFieldInfoRef = false;
        IReadOnlyCollection<string> sensitiveSet = ConflictTargets.SensitiveTypeNames;

        foreach (TypeReferenceHandle trh in md.TypeReferences)
        {
            string? fullName = PlaysetModScanner.GetEntityFullTypeName(md, trh);
            if (fullName == null)
                continue;

            if (sensitiveSet.Contains(fullName))
                hitsSensitiveTypeRef = true;
            else if (fullName.StartsWith("HarmonyLib.", StringComparison.Ordinal))
                hitsHarmonyTypeRef = true;
            else if (fullName is "System.Reflection.FieldInfo" or "System.Reflection.MemberInfo")
                hitsFieldInfoRef = true;
        }

        if (!hitsSensitiveTypeRef && !hitsHarmonyTypeRef && !hitsFieldInfoRef)
        {
            return new AssemblyInspectionResult(asmName, asmVersion, implementsIMod, customSystems, [], [], [], []);
        }

        // === 第 3 级：细粒度行为定性（仅针对存在交集的代码 Mod） ===
        Dictionary<EntityHandle, string> protectedFields =
            IlTokenScanner.CollectProtectedSystemFields(md, peReader, s_typeProvider);

        var enabledWrites = new List<EnabledWriteEvidence>();
        var crossPhaseSchedules = new List<CrossPhaseScheduleEvidence>();
        var harmonyOverlaps = new List<HarmonyOverlapEvidence>();
        var replacedSystemPatches = new List<PatchedReplacedSystemEvidence>();
        var rawUnpatchCalls = new List<(MethodDefinitionHandle Handle, string Caller)>();
        var reflectionWrites = new List<StaticFieldReflectionWriteEvidence>();
        var internalEdges = new HashSet<(MethodDefinitionHandle Caller, MethodDefinitionHandle Callee)>();
        var assemblySensitiveTypeLiterals = new HashSet<string>(StringComparer.Ordinal);
        var pendingDynamicHooks = new List<(string Display, DetectedPatchKind Kind, List<string> RefTypes, List<string> StrLiterals)>();

        var disposeRoots = new HashSet<MethodDefinitionHandle>();
        var lifecycleRoots = new HashSet<MethodDefinitionHandle>();
        var publicMethods = new HashSet<MethodDefinitionHandle>();

        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition td = md.GetTypeDefinition(th);
            string typeFullName = PlaysetModScanner.GetEntityFullTypeName(md, th) ?? "<Module>";
            bool typeIsIMod = TypeImplementsInterface(md, td, "Game.Modding.IMod");
            bool typeIsSystem = TypeDerivesFromSystem(md, td);

            // 解析类级（含外层声明类）的 [HarmonyPatch] 特性
            bool hasClassHarmonyPatch = CollectHarmonyPatchSpecs(md, td.GetCustomAttributes(), out List<PatchTargetSpec> classSpecs);
            TypeDefinitionHandle parentHandle = td.GetDeclaringType();
            while (!parentHandle.IsNil)
            {
                TypeDefinition parentTd = md.GetTypeDefinition(parentHandle);
                if (CollectHarmonyPatchSpecs(md, parentTd.GetCustomAttributes(), out List<PatchTargetSpec> parentSpecs))
                {
                    hasClassHarmonyPatch = true;
                    classSpecs.InsertRange(0, parentSpecs);
                }

                parentHandle = parentTd.GetDeclaringType();
            }

            // 用于动态 TargetMethod() 关联类上的补丁种类
            var classPatchKinds = new List<(string MethodDisplay, DetectedPatchKind Kind)>();
            var dynamicTargetCandidates = new List<(string MethodDisplay, List<string> Types, List<string> Strings)>();

            foreach (MethodDefinitionHandle mh in td.GetMethods())
            {
                MethodDefinition mdef = md.GetMethodDefinition(mh);
                string methodName = md.GetString(mdef.Name);
                string callerDisplay = $"{typeFullName}.{methodName}";

                MethodAttributes access = mdef.Attributes & MethodAttributes.MemberAccessMask;
                if (access is MethodAttributes.Public or MethodAttributes.FamORAssem or MethodAttributes.Family)
                {
                    publicMethods.Add(mh);
                }

                // 分类生命周期根方法
                if (typeIsIMod && (methodName == "OnDispose" || methodName.EndsWith(".OnDispose", StringComparison.Ordinal)))
                {
                    disposeRoots.Add(mh);
                }
                else if ((typeIsIMod && (methodName == "OnLoad" || methodName.EndsWith(".OnLoad", StringComparison.Ordinal))) ||
                         methodName is ".ctor" or ".cctor" ||
                         (typeIsSystem && methodName is "OnCreate" or "OnUpdate" or "OnStartRunning" or
                             "OnGamePreload" or "OnGameLoaded" or "OnGameLoadingComplete"))
                {
                    lifecycleRoots.Add(mh);
                }

                // 解析方法上的 Harmony 特性与约定方法名
                bool hasMethodHarmonyPatch = CollectHarmonyPatchSpecs(md, mdef.GetCustomAttributes(), out List<PatchTargetSpec> methodSpecs);
                DetectedPatchKind? patchKind = DetectMethodPatchKind(md, mdef, methodName, hasClassHarmonyPatch || hasMethodHarmonyPatch);
                bool isTargetMethodHook = IsHarmonyTargetMethodHook(md, mdef, methodName, hasClassHarmonyPatch || hasMethodHarmonyPatch);

                if (patchKind.HasValue)
                {
                    classPatchKinds.Add((callerDisplay, patchKind.Value));
                    foreach ((string targetType, string targetMethod, string? genArg) in MergePatchSpecs(classSpecs, methodSpecs))
                    {
                        if (ConflictTargets.FindPatchedMethod(targetType, targetMethod, genArg) != null)
                        {
                            harmonyOverlaps.Add(new HarmonyOverlapEvidence(
                                callerDisplay,
                                targetType,
                                targetMethod,
                                genArg,
                                patchKind.Value));
                        }
                        else if (IsReplacedSystemLifecycleMethod(targetMethod) &&
                                 ConflictTargets.FindProtectedSystem(targetType) != null)
                        {
                            replacedSystemPatches.Add(new PatchedReplacedSystemEvidence(
                                callerDisplay,
                                targetType,
                                targetMethod,
                                patchKind.Value));
                        }
                    }
                }

                // 扫描方法体 IL
                if (mdef.RelativeVirtualAddress == 0)
                    continue;

                MethodBodyBlock body;
                try
                {
                    body = peReader.GetMethodBody(mdef.RelativeVirtualAddress);
                }
                catch (BadImageFormatException)
                {
                    continue;
                }

                IlTokenScanner.ScanMethodBody(
                    md,
                    mh,
                    mdef,
                    callerDisplay,
                    body,
                    s_typeProvider,
                    protectedFields,
                    enabledWrites,
                    crossPhaseSchedules,
                    rawUnpatchCalls,
                    reflectionWrites,
                    internalEdges,
                    assemblySensitiveTypeLiterals,
                    out List<string>? refTypes,
                    out List<string>? strLiterals,
                    out bool callsImperativePatch);

                if ((isTargetMethodHook || callsImperativePatch) && (refTypes != null || strLiterals != null))
                {
                    dynamicTargetCandidates.Add((callerDisplay, refTypes ?? [], strLiterals ?? []));
                }
            }

            // 暂存本类的 TargetMethod() / 命令式 Harmony.Patch 候选，待全程序集字符串字面量收齐后统一匹配（支持跨类常量字符串，如 ServiceCoverageFix）
            foreach ((string hookCaller, List<string> refTypes, List<string> strLiterals) in dynamicTargetCandidates)
            {
                DetectedPatchKind inferredKind = classPatchKinds.Count > 0
                    ? classPatchKinds[0].Kind
                    : DetectedPatchKind.DynamicOrImperative;
                string display = classPatchKinds.Count > 0 ? classPatchKinds[0].MethodDisplay : hookCaller;
                pendingDynamicHooks.Add((display, inferredKind, refTypes, strLiterals));
            }
        }

        // === 全程序集扫描完毕后统一匹配动态 TargetMethod() 与命令式补丁目标 ===
        foreach ((string display, DetectedPatchKind inferredKind, List<string> refTypes, List<string> strLiterals) in pendingDynamicHooks)
        {
            MatchDynamicHarmonyTargets(
                display,
                inferredKind,
                refTypes,
                assemblySensitiveTypeLiterals,
                strLiterals,
                harmonyOverlaps,
                replacedSystemPatches);
        }

        // === 分类无参 UnpatchAll(null) 的上下文 ===
        List<UnpatchAllNullEvidence> classifiedUnpatch = ClassifyUnpatchAllCalls(
            rawUnpatchCalls,
            internalEdges,
            disposeRoots,
            lifecycleRoots,
            publicMethods);

        return new AssemblyInspectionResult(
            asmName,
            asmVersion,
            implementsIMod,
            customSystems,
            Deduplicate(enabledWrites),
            Deduplicate(harmonyOverlaps),
            Deduplicate(classifiedUnpatch),
            Deduplicate(reflectionWrites),
            Deduplicate(replacedSystemPatches),
            Deduplicate(crossPhaseSchedules));
    }

    private sealed record PatchTargetSpec(
        string? TargetType,
        string? GenericArg,
        string? MethodName,
        int MethodTypeCode);

    private static List<string> CollectCustomSystemTypes(MetadataReader md)
    {
        var systems = new List<string>();
        foreach (TypeDefinitionHandle th in md.TypeDefinitions)
        {
            TypeDefinition td = md.GetTypeDefinition(th);
            if ((td.Attributes & TypeAttributes.Abstract) != 0)
                continue;
            if (TypeDerivesFromSystem(md, td))
            {
                string? name = PlaysetModScanner.GetEntityFullTypeName(md, th);
                if (name != null)
                    systems.Add(name);
            }
        }

        return systems;
    }

    private static bool TypeImplementsInterface(MetadataReader md, TypeDefinition td, string interfaceFullName)
    {
        foreach (InterfaceImplementationHandle ih in td.GetInterfaceImplementations())
        {
            InterfaceImplementation impl = md.GetInterfaceImplementation(ih);
            if (PlaysetModScanner.GetEntityFullTypeName(md, impl.Interface) == interfaceFullName)
                return true;
        }

        return false;
    }

    private static bool TypeDerivesFromSystem(MetadataReader md, TypeDefinition td)
    {
        EntityHandle cur = td.BaseType;
        int guard = 0;
        while (!cur.IsNil && guard++ < 8)
        {
            string? baseName = cur.Kind == HandleKind.TypeSpecification
                ? TryDecodeTypeSpec(md, (TypeSpecificationHandle)cur)
                : PlaysetModScanner.GetEntityFullTypeName(md, cur);

            if (baseName == null)
                break;

            if (baseName is "Game.GameSystemBase" or "Unity.Entities.ComponentSystemBase" or "Unity.Entities.SystemBase" ||
                baseName.StartsWith("Game.Simulation.CellMapSystem`1", StringComparison.Ordinal) ||
                baseName.StartsWith("Game.UI.UISystemBase", StringComparison.Ordinal) ||
                baseName.StartsWith("Game.Tools.ToolBaseSystem", StringComparison.Ordinal))
            {
                return true;
            }

            if (cur.Kind == HandleKind.TypeDefinition)
            {
                cur = md.GetTypeDefinition((TypeDefinitionHandle)cur).BaseType;
            }
            else
            {
                break;
            }
        }

        return false;
    }

    private static string? TryDecodeTypeSpec(MetadataReader md, TypeSpecificationHandle handle)
    {
        try
        {
            return md.GetTypeSpecification(handle).DecodeSignature(s_typeProvider, GenericContext.Empty);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static bool CollectHarmonyPatchSpecs(
        MetadataReader md,
        CustomAttributeHandleCollection attrs,
        out List<PatchTargetSpec> specs)
    {
        specs = [];
        foreach (CustomAttributeHandle ah in attrs)
        {
            CustomAttribute attr = md.GetCustomAttribute(ah);
            string? attrType = GetAttributeTypeName(md, attr);
            if (attrType != "HarmonyLib.HarmonyPatch")
                continue;

            try
            {
                CustomAttributeValue<string?> decoded = attr.DecodeValue(s_attrProvider);
                string? targetType = null;
                string? genericArg = null;
                string? methodName = null;
                int methodTypeCode = 0;

                foreach (CustomAttributeTypedArgument<string?> arg in decoded.FixedArguments)
                {
                    if (arg.Type == "System.Type" && arg.Value is string rawType)
                    {
                        (targetType, genericArg) = ParseSerializedTypeString(rawType);
                    }
                    else if (arg.Type == "System.String" && arg.Value is string s)
                    {
                        methodName = s;
                    }
                    else if (arg.Type == "HarmonyLib.MethodType" && arg.Value is int mt)
                    {
                        methodTypeCode = mt;
                    }
                }

                foreach (CustomAttributeNamedArgument<string?> named in decoded.NamedArguments)
                {
                    if (named.Name == "declaringType" && named.Value is string rawType)
                        (targetType, genericArg) = ParseSerializedTypeString(rawType);
                    else if (named.Name == "methodName" && named.Value is string s)
                        methodName = s;
                    else if (named.Name == "methodType" && named.Value is int mt)
                        methodTypeCode = mt;
                }

                specs.Add(new PatchTargetSpec(targetType, genericArg, methodName, methodTypeCode));
            }
            catch (BadImageFormatException)
            {
                // 跳过畸形特性 Blob
            }
        }

        return specs.Count > 0;
    }

    private static IEnumerable<(string TargetType, string TargetMethod, string? GenericArg)> MergePatchSpecs(
        List<PatchTargetSpec> classSpecs,
        List<PatchTargetSpec> methodSpecs)
    {
        string? classType = null;
        string? classGenArg = null;
        string? classMethod = null;
        int classMethodType = 0;

        foreach (PatchTargetSpec spec in classSpecs)
        {
            if (spec.TargetType != null)
            {
                classType = spec.TargetType;
                classGenArg = spec.GenericArg;
            }

            if (spec.MethodName != null)
                classMethod = spec.MethodName;
            if (spec.MethodTypeCode != 0)
                classMethodType = spec.MethodTypeCode;
        }

        // 若方法上贴有多条自带独立方法名的 [HarmonyPatch]，逐条与类级默认值合并产出，避免后写覆盖前写
        int distinctMethodSpecs = methodSpecs.Count(s => !string.IsNullOrEmpty(s.MethodName));
        if (distinctMethodSpecs > 1)
        {
            string? runningType = classType;
            string? runningGenArg = classGenArg;
            foreach (PatchTargetSpec mSpec in methodSpecs)
            {
                if (mSpec.TargetType != null)
                {
                    runningType = mSpec.TargetType;
                    runningGenArg = mSpec.GenericArg;
                }

                if (string.IsNullOrEmpty(mSpec.MethodName) || runningType == null)
                    continue;

                int mt = mSpec.MethodTypeCode != 0 ? mSpec.MethodTypeCode : classMethodType;
                string? resolved = ApplyHarmonyMethodType(mSpec.MethodName, mt);
                if (!string.IsNullOrEmpty(resolved))
                    yield return (runningType, resolved, runningGenArg);
            }

            yield break;
        }

        string? mergedType = classType;
        string? mergedGenArg = classGenArg;
        string? mergedMethod = classMethod;
        int mergedMethodType = classMethodType;

        foreach (PatchTargetSpec spec in methodSpecs)
        {
            if (spec.TargetType != null)
            {
                mergedType = spec.TargetType;
                mergedGenArg = spec.GenericArg;
            }

            if (spec.MethodName != null)
                mergedMethod = spec.MethodName;
            if (spec.MethodTypeCode != 0)
                mergedMethodType = spec.MethodTypeCode;
        }

        if (mergedType == null)
            yield break;

        string? finalMethod = ApplyHarmonyMethodType(mergedMethod, mergedMethodType);
        if (!string.IsNullOrEmpty(finalMethod))
        {
            yield return (mergedType, finalMethod, mergedGenArg);
        }
    }

    private static string? ApplyHarmonyMethodType(string? methodName, int methodTypeCode) => methodTypeCode switch
    {
        // HarmonyLib.MethodType: Normal=0, Getter=1, Setter=2, Constructor=3, StaticConstructor=4
        1 => !string.IsNullOrEmpty(methodName) ? "get_" + methodName : null,
        2 => !string.IsNullOrEmpty(methodName) ? "set_" + methodName : null,
        3 => ".ctor",
        4 => ".cctor",
        _ => methodName,
    };

    private static DetectedPatchKind? DetectMethodPatchKind(
        MetadataReader md,
        MethodDefinition mdef,
        string methodName,
        bool inHarmonyPatchContext)
    {
        bool isPrefix = false, isPostfix = false, isTranspiler = false, isFinalizer = false;

        foreach (CustomAttributeHandle ah in mdef.GetCustomAttributes())
        {
            string? attrType = GetAttributeTypeName(md, md.GetCustomAttribute(ah));
            switch (attrType)
            {
                case "HarmonyLib.HarmonyPrefix": isPrefix = true; break;
                case "HarmonyLib.HarmonyPostfix": isPostfix = true; break;
                case "HarmonyLib.HarmonyTranspiler": isTranspiler = true; break;
                case "HarmonyLib.HarmonyFinalizer": isFinalizer = true; break;
            }
        }

        if (!isPrefix && !isPostfix && !isTranspiler && !isFinalizer && inHarmonyPatchContext)
        {
            switch (methodName)
            {
                case "Prefix": isPrefix = true; break;
                case "Postfix": isPostfix = true; break;
                case "Transpiler": isTranspiler = true; break;
                case "Finalizer": isFinalizer = true; break;
            }
        }

        if (isTranspiler)
            return DetectedPatchKind.Transpiler;
        if (isPrefix)
        {
            bool returnsBool = false;
            try
            {
                MethodSignature<string?> sig = mdef.DecodeSignature(s_typeProvider, GenericContext.Empty);
                returnsBool = sig.ReturnType == "System.Boolean";
            }
            catch (BadImageFormatException)
            {
                returnsBool = true;
            }

            return returnsBool ? DetectedPatchKind.PrefixBlockingCapable : DetectedPatchKind.PrefixVoid;
        }

        if (isPostfix)
            return DetectedPatchKind.Postfix;
        if (isFinalizer)
            return DetectedPatchKind.Finalizer;
        return null;
    }

    private static bool IsHarmonyTargetMethodHook(
        MetadataReader md,
        MethodDefinition mdef,
        string methodName,
        bool inHarmonyPatchContext)
    {
        foreach (CustomAttributeHandle ah in mdef.GetCustomAttributes())
        {
            string? attrType = GetAttributeTypeName(md, md.GetCustomAttribute(ah));
            if (attrType is "HarmonyLib.HarmonyTargetMethod" or "HarmonyLib.HarmonyTargetMethods")
                return true;
        }

        return inHarmonyPatchContext && methodName is "TargetMethod" or "TargetMethods";
    }

    private static bool IsReplacedSystemLifecycleMethod(string methodName) =>
        methodName is "OnUpdate" or "OnCreate" or "OnStartRunning" or "OnStopRunning";

    private static void MatchDynamicHarmonyTargets(
        string callerDisplay,
        DetectedPatchKind kind,
        List<string> refTypes,
        HashSet<string> assemblySensitiveTypeLiterals,
        List<string> strLiterals,
        List<HarmonyOverlapEvidence> overlapSink,
        List<PatchedReplacedSystemEvidence> replacedSystemSink)
    {
        var expandedTypes = new List<(string BaseType, string? GenArg)>(refTypes.Count + assemblySensitiveTypeLiterals.Count);
        foreach (string rt in refTypes)
        {
            int lt = rt.IndexOf('<');
            if (lt > 0 && rt.EndsWith('>'))
            {
                expandedTypes.Add((rt[..lt], rt[(lt + 1)..^1]));
            }
            else
            {
                expandedTypes.Add((rt, null));
            }
        }

        foreach (string typeLit in assemblySensitiveTypeLiterals)
        {
            expandedTypes.Add((typeLit, null));
        }

        foreach ((string baseType, string? genArg) in expandedTypes)
        {
            foreach (string rawStr in strLiterals)
            {
                foreach (string candidateMethod in ExpandCandidateMethodNames(rawStr))
                {
                    if (ConflictTargets.FindPatchedMethod(baseType, candidateMethod, genArg) != null)
                    {
                        overlapSink.Add(new HarmonyOverlapEvidence(callerDisplay, baseType, candidateMethod, genArg, kind));
                    }
                    else if (genArg == null &&
                             IsReplacedSystemLifecycleMethod(candidateMethod) &&
                             ConflictTargets.FindProtectedSystem(baseType) != null)
                    {
                        replacedSystemSink.Add(new PatchedReplacedSystemEvidence(callerDisplay, baseType, candidateMethod, kind));
                    }
                }
            }
        }
    }

    private static IEnumerable<string> ExpandCandidateMethodNames(string raw)
    {
        yield return raw;
        if (!raw.StartsWith("get_", StringComparison.Ordinal) && !raw.StartsWith("set_", StringComparison.Ordinal))
        {
            yield return "get_" + raw;
            yield return "set_" + raw;
        }
    }

    private static List<UnpatchAllNullEvidence> ClassifyUnpatchAllCalls(
        List<(MethodDefinitionHandle Handle, string Caller)> rawCalls,
        HashSet<(MethodDefinitionHandle Caller, MethodDefinitionHandle Callee)> edges,
        HashSet<MethodDefinitionHandle> disposeRoots,
        HashSet<MethodDefinitionHandle> lifecycleRoots,
        HashSet<MethodDefinitionHandle> publicMethods)
    {
        if (rawCalls.Count == 0)
            return [];

        var forward = new Dictionary<MethodDefinitionHandle, List<MethodDefinitionHandle>>();
        var backward = new Dictionary<MethodDefinitionHandle, List<MethodDefinitionHandle>>();
        foreach ((MethodDefinitionHandle caller, MethodDefinitionHandle callee) in edges)
        {
            if (caller == callee)
                continue;
            if (!forward.TryGetValue(caller, out List<MethodDefinitionHandle>? fList))
            {
                fList = [];
                forward[caller] = fList;
            }

            fList.Add(callee);

            if (!backward.TryGetValue(callee, out List<MethodDefinitionHandle>? bList))
            {
                bList = [];
                backward[callee] = bList;
            }

            bList.Add(caller);
        }

        HashSet<MethodDefinitionHandle> reachableFromLifecycle = BfsReachable(lifecycleRoots, forward);
        HashSet<MethodDefinitionHandle> reachableFromDispose = BfsReachable(disposeRoots, forward);

        bool IsStrictlyDisposeOnly(MethodDefinitionHandle target, HashSet<MethodDefinitionHandle> visiting)
        {
            if (disposeRoots.Contains(target))
                return true;
            if (reachableFromLifecycle.Contains(target) || !reachableFromDispose.Contains(target))
                return false;
            if (publicMethods.Contains(target))
                return false;
            if (!backward.TryGetValue(target, out List<MethodDefinitionHandle>? callers) || callers.Count == 0)
                return false;
            if (!visiting.Add(target))
                return true;

            foreach (MethodDefinitionHandle c in callers)
            {
                if (!IsStrictlyDisposeOnly(c, visiting))
                    return false;
            }

            return true;
        }

        var results = new List<UnpatchAllNullEvidence>(rawCalls.Count);
        foreach ((MethodDefinitionHandle handle, string callerName) in rawCalls)
        {
            UnpatchAllContext ctx;
            if (reachableFromLifecycle.Contains(handle))
                ctx = UnpatchAllContext.InitializationOrLifecycle;
            else if (IsStrictlyDisposeOnly(handle, []))
                ctx = UnpatchAllContext.DisposeOnly;
            else
                ctx = UnpatchAllContext.UnknownCallback;

            results.Add(new UnpatchAllNullEvidence(callerName, ctx));
        }

        return results;
    }

    private static HashSet<MethodDefinitionHandle> BfsReachable(
        HashSet<MethodDefinitionHandle> roots,
        Dictionary<MethodDefinitionHandle, List<MethodDefinitionHandle>> graph)
    {
        var visited = new HashSet<MethodDefinitionHandle>(roots);
        var queue = new Queue<MethodDefinitionHandle>(roots);
        while (queue.Count > 0)
        {
            MethodDefinitionHandle cur = queue.Dequeue();
            if (!graph.TryGetValue(cur, out List<MethodDefinitionHandle>? nextList))
                continue;
            foreach (MethodDefinitionHandle next in nextList)
            {
                if (visited.Add(next))
                    queue.Enqueue(next);
            }
        }

        return visited;
    }

    private static string? GetAttributeTypeName(MetadataReader md, CustomAttribute attr) => attr.Constructor.Kind switch
    {
        HandleKind.MemberReference => PlaysetModScanner.GetEntityFullTypeName(md, md.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent),
        HandleKind.MethodDefinition => PlaysetModScanner.GetEntityFullTypeName(md, md.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).GetDeclaringType()),
        _ => null,
    };

    /// <summary>
    /// 解析特性序列化类型字符串（如 <c>"Game.Simulation.WaterSystem, Game, Version=..."</c> 或
    /// <c>"Game.Simulation.CellMapSystem`1[[Game.Simulation.LandValueCell, Game]]"</c>）。
    /// </summary>
    internal static (string TypeName, string? GenericArg) ParseSerializedTypeString(string raw)
    {
        string trimmed = raw.Trim();
        string? genArg = null;

        int dblBracket = trimmed.IndexOf("[[", StringComparison.Ordinal);
        if (dblBracket > 0)
        {
            string basePart = trimmed[..dblBracket];
            int close = trimmed.IndexOf(']', dblBracket + 2);
            if (close > dblBracket + 2)
            {
                string inner = trimmed[(dblBracket + 2)..close];
                genArg = StripAssemblyQualifier(inner);
            }

            return (StripAssemblyQualifier(basePart), genArg);
        }

        int angle = trimmed.IndexOf('<');
        if (angle > 0 && trimmed.EndsWith('>'))
        {
            return (StripAssemblyQualifier(trimmed[..angle]), StripAssemblyQualifier(trimmed[(angle + 1)..^1]));
        }

        return (StripAssemblyQualifier(trimmed), null);
    }

    private static string StripAssemblyQualifier(string typeStr)
    {
        int comma = typeStr.IndexOf(',');
        string core = (comma >= 0 ? typeStr[..comma] : typeStr).Trim();
        return core.Replace('/', '+');
    }

    private static IReadOnlyList<T> Deduplicate<T>(List<T> source) =>
        source.Count <= 1 ? source : source.Distinct().ToList();

    private sealed class CustomAttrTypeProvider : ICustomAttributeTypeProvider<string?>
    {
        public string? GetPrimitiveType(PrimitiveTypeCode typeCode) => s_typeProvider.GetPrimitiveType(typeCode);
        public string? GetSystemType() => "System.Type";
        public string? GetSZArrayType(string? elementType) => elementType != null ? elementType + "[]" : null;

        public string? GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            PlaysetModScanner.GetEntityFullTypeName(reader, handle);

        public string? GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            PlaysetModScanner.GetEntityFullTypeName(reader, handle);

        public string? GetTypeFromSerializedName(string name) => name;

        public PrimitiveTypeCode GetUnderlyingEnumType(string? type) => PrimitiveTypeCode.Int32;

        public bool IsSystemType(string? type) => type == "System.Type";
    }
}
