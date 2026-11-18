// Copyright (c) 2024 Noel2(Noel-leoN)
// Licensed under the MIT License.
// See LICENSE in the project root for full license information.
// When using this part of the code, please clearly credit [Project Name] and the author.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using HarmonyLib;
using MapExtPDX.MapExt.MapSizePatchSet;
using Unity.Entities;

namespace MapExtPDX.MapExt.Core
{
    /// <summary>严重 CellMap 冲突的结构化条目种类（供三语弹窗按语言渲染，避免日志中文混入英文对话框）。</summary>
    public enum CriticalIssueKind
    {
        ReEnabledOnLoad,
        VanillaStillActive,
        ReplacementDisabled,
        HarmonyPatchesRemoved,
        MapSizeStaticFieldTampered,
    }

    /// <summary>单条严重 CellMap 冲突的结构化描述。</summary>
    public sealed class CriticalIssueItem
    {
        public CriticalIssueKind Kind;
        public string TargetName;
        public int Count;
        public string Sample;
        public string LogDetail;
    }

    /// <summary>
    /// 游戏内运行期冲突精准归因底座（轨道 B）：
    /// <list type="bullet">
    /// <item>基于 <c>ModManager.TryGetExecutableAsset</c> + <c>GetMeta()</c> 将任意第三方 <see cref="Assembly"/>（含随包附属业务 DLL）回溯至主入口 Mod 的商店显示名与平台 ID；</item>
    /// <item>在 <c>Mod.OnLoad</c> 末尾录制本会话实际挂载的 Dual Harmony 补丁快照并执行单向子集自检（<c>实际已挂 ⊆ ConflictTargets</c>）；</item>
    /// <item>按需对已加载程序集执行内存 IL 接收者标签追踪（不触碰已释放底层流的 <c>ExecutableAsset.definition</c>）、<c>Harmony.GetPatchInfo</c> 存活／侵入核查与 <c>UnpatchAll(null)</c> 状态分层锁定，以及 <c>CellMapSystem&lt;T&gt;.kMapSize == 14336</c> 回读校验。</item>
    /// </list>
    /// </summary>
    public static class ModAttributionHelper
    {
        private const string Tag = nameof(ModAttributionHelper);

        #region Opcode Table & Slot Tags

        private struct OpSpec
        {
            public OpCode Code;
            public int OperandBytes;
        }

        private enum SlotKind : byte
        {
            Other = 0,
            Self = 1,
            ProtectedSystem = 2,
            NullLiteral = 3,
        }

        private struct SlotTag
        {
            public SlotKind Kind;
            public Type SystemType;

            public static SlotTag Other => new SlotTag { Kind = SlotKind.Other };
            public static SlotTag Self => new SlotTag { Kind = SlotKind.Self };
            public static SlotTag Null => new SlotTag { Kind = SlotKind.NullLiteral };
            public static SlotTag Protected(Type systemType) => new SlotTag { Kind = SlotKind.ProtectedSystem, SystemType = systemType };
        }

        private sealed class CapturedPatchRecord
        {
            public MethodBase TargetMethod;
            public bool HadModesPatch;
            public bool HadGlobalPatch;
        }

        private static readonly OpSpec[] s_OneByteOps = new OpSpec[256];
        private static readonly OpSpec[] s_TwoByteOps = new OpSpec[256];
        private static readonly List<CapturedPatchRecord> s_SessionPatches = new List<CapturedPatchRecord>();

        // 本会话层 1 内存 IL 归因索引缓存（游戏运行期间已加载程序集不变，全会话至多扫描一次）
        private static bool s_Layer1IndexBuilt;
        private static readonly Dictionary<Type, List<string>> s_CachedMutatorCallers = new Dictionary<Type, List<string>>();
        private static readonly Dictionary<Type, List<string>> s_CachedMutatorModLabels = new Dictionary<Type, List<string>>();

        private static readonly Type[] s_MonitoredCellMapSystems =
        {
            typeof(AirPollutionSystem),
            typeof(AvailabilityInfoToGridSystem),
            typeof(GroundPollutionSystem),
            typeof(GroundWaterSystem),
            typeof(LandValueSystem),
            typeof(NaturalResourceSystem),
            typeof(NoisePollutionSystem),
            typeof(PopulationToGridSystem),
            typeof(SoilWaterSystem),
            typeof(TerrainAttractivenessSystem),
            typeof(TrafficAmbienceSystem),
            typeof(ZoneAmbienceSystem),
            typeof(TelecomCoverageSystem),
            typeof(WindSystem),
        };

        static ModAttributionHelper()
        {
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(OpCode)) continue;

                OpCode op = (OpCode)field.GetValue(null);
                ushort val = (ushort)op.Value;
                int len = GetOperandLength(op.OperandType);
                if (val < 0x100)
                    s_OneByteOps[val] = new OpSpec { Code = op, OperandBytes = len };
                else if ((val & 0xFF00) == 0xFE00)
                    s_TwoByteOps[val & 0xFF] = new OpSpec { Code = op, OperandBytes = len };
            }
        }

        #endregion

        #region B.1 Identity Resolution

        /// <summary>将任意已加载 <see cref="Assembly"/> 解析为可读的商店显示名、版本与来源标签（支持附属引用 DLL 向主 Mod 回溯）。</summary>
        public static string FormatAssemblyIdentity(Assembly assembly)
        {
            if (assembly == null) return "Unknown";

            try
            {
                ModManager mm = GameManager.instance?.modManager;
                if (mm != null && mm.TryGetExecutableAsset(assembly, out ExecutableAsset asset) && asset != null)
                {
                    ExecutableAsset resolved = ResolveOwningModAsset(mm, asset);
                    return FormatExecutableAsset(resolved);
                }
            }
            catch
            {
                // 降级使用程序集名
            }

            AssemblyName an = assembly.GetName();
            return $"{an.Name} v{an.Version}";
        }

        /// <summary>格式化 <see cref="ExecutableAsset"/> 为 <c>"{displayName} v{version} (Local|PDX:{id})"</c>。</summary>
        public static string FormatExecutableAsset(ExecutableAsset asset)
        {
            if (asset == null) return "Unknown";

            try
            {
                SourceMeta meta = asset.GetMeta();
                string display = !string.IsNullOrWhiteSpace(meta.displayName) ? meta.displayName : asset.name;
                string source = asset.isLocal
                    ? "Local"
                    : (!string.IsNullOrWhiteSpace(meta.platformID) ? $"PDX:{meta.platformID}" : "PDX");
                return $"{display} v{asset.version} ({source})";
            }
            catch
            {
                return $"{asset.name} v{asset.version}";
            }
        }

        /// <summary>若 <paramref name="asset"/> 为随包附属类库（<c>!isMod</c>），沿引用关系或同包路径回溯主 <c>isMod</c> 资产。</summary>
        public static ExecutableAsset ResolveOwningModAsset(ModManager modManager, ExecutableAsset asset)
        {
            if (asset == null || asset.isMod || modManager == null) return asset;

            try
            {
                // 1. 优先沿 ExecutableAsset.references 反向匹配引用了它的主 Mod
                foreach (ModManager.ModInfo modInfo in modManager)
                {
                    ExecutableAsset candidate = modInfo.asset;
                    if (candidate == null || !candidate.isMod || candidate.references == null) continue;

                    foreach (ExecutableAsset refAsset in candidate.references.Values)
                    {
                        if (ReferenceEquals(refAsset, asset))
                            return candidate;
                    }
                }

                // 2. 其次按相同部署子目录（subPath）回溯
                string targetSubPath = asset.GetMeta().subPath;
                if (!string.IsNullOrEmpty(targetSubPath))
                {
                    foreach (ModManager.ModInfo modInfo in modManager)
                    {
                        ExecutableAsset candidate = modInfo.asset;
                        if (candidate == null || !candidate.isMod) continue;

                        string candSubPath = candidate.GetMeta().subPath;
                        if (string.Equals(candSubPath, targetSubPath, StringComparison.OrdinalIgnoreCase))
                            return candidate;
                    }
                }
            }
            catch
            {
                // 回溯异常时退回原资产本身
            }

            return asset;
        }

        /// <summary>判断是否为本工程自身或自动休眠的配套模组程序集。</summary>
        public static bool IsSelfOrCompanionAssembly(Assembly assembly)
        {
            if (assembly == null) return true;
            if (assembly == typeof(Mod).Assembly) return true;

            string asmName = assembly.GetName().Name;
            return ConflictTargets.IsSelfOrCompanion(asmName, null);
        }

        #endregion

        #region Session Patch Snapshot & Subset Self-Check

        /// <summary>
        /// 在 <c>Mod.OnLoadCore</c> 全部补丁应用完成后调用：
        /// 录制本会话实际已挂的 <c>MapExtPDX_modes</c>／<c>MapExtPDX_global</c> 目标方法快照，
        /// 并执行单向子集自检（<c>实际已挂 ⊆ ConflictTargets 声明全集</c>）。
        /// </summary>
        public static void CaptureAndVerifySessionPatches()
        {
            s_SessionPatches.Clear();

            try
            {
                var unregistered = new List<string>();

                foreach (MethodBase original in Harmony.GetAllPatchedMethods())
                {
                    if (original == null) continue;
                    HarmonyLib.Patches info = Harmony.GetPatchInfo(original);
                    if (info == null) continue;

                    bool hasModes = HasOwner(info, Mod.HarmonyIdModes);
                    bool hasGlobal = HasOwner(info, Mod.HarmonyIdGlobal);
                    if (!hasModes && !hasGlobal) continue;

                    s_SessionPatches.Add(new CapturedPatchRecord
                    {
                        TargetMethod = original,
                        HadModesPatch = hasModes,
                        HadGlobalPatch = hasGlobal,
                    });

                    if (!IsDeclaredInConflictTargets(original, info))
                    {
                        unregistered.Add($"{ original.DeclaringType?.FullName}.{original.Name}");
                    }
                }

                if (unregistered.Count == 0)
                {
                    ModLog.Ok(Tag, $"ConflictTargets 单向子集自检通过（本会话实际挂载 {s_SessionPatches.Count} 个目标方法）");
                }
                else
                {
                    ModLog.Warn(Tag, $"发现 {unregistered.Count} 个实际挂载目标未登记至 ConflictTargets: {string.Join(", ", unregistered)}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"采集 Harmony 补丁快照或执行子集自检异常，已跳过: {ex.Message}");
            }
        }

        private static bool IsDeclaredInConflictTargets(MethodBase original, HarmonyLib.Patches info)
        {
            Type declType = original.DeclaringType;
            if (declType == null) return true;

            // CellMapSystemPatchManager 按 ldsfld 自动扫描的方法：核验补丁来源与允许命名空间／基类
            bool allFromCellMapFieldScanner = AllOwnPatchesFromType(info, typeof(CellMapSystemPatchManager));
            if (allFromCellMapFieldScanner)
            {
                if (declType.IsGenericType && declType.GetGenericTypeDefinition() == typeof(CellMapSystem<>))
                    return true;

                string ns = declType.Namespace ?? string.Empty;
                foreach (string prefix in ConflictTargets.CellMapFieldPatchNamespaces)
                {
                    if (ns.StartsWith(prefix, StringComparison.Ordinal))
                        return true;
                }
            }

            string typeName;
            string genArgName = null;
            if (declType.IsGenericType)
            {
                Type genDef = declType.GetGenericTypeDefinition();
                typeName = genDef.FullName ?? genDef.Name;
                Type[] args = declType.GetGenericArguments();
                if (args.Length == 1)
                    genArgName = args[0].FullName;
            }
            else
            {
                typeName = declType.FullName ?? declType.Name;
            }

            return ConflictTargets.FindPatchedMethod(typeName, original.Name, genArgName) != null;
        }

        private static bool AllOwnPatchesFromType(HarmonyLib.Patches info, Type patchDeclaringType)
        {
            bool anyOwn = false;
            foreach (Patch p in EnumerateAllPatches(info))
            {
                if (p.owner != Mod.HarmonyIdModes && p.owner != Mod.HarmonyIdGlobal) continue;
                anyOwn = true;
                if (p.PatchMethod?.DeclaringType != patchDeclaringType)
                    return false;
            }

            return anyOwn;
        }

        private static bool HasOwner(HarmonyLib.Patches info, string owner)
        {
            foreach (Patch p in EnumerateAllPatches(info))
            {
                if (p.owner == owner) return true;
            }

            return false;
        }

        private static IEnumerable<Patch> EnumerateAllPatches(HarmonyLib.Patches info)
        {
            if (info.Prefixes != null)
                foreach (Patch p in info.Prefixes) yield return p;
            if (info.Postfixes != null)
                foreach (Patch p in info.Postfixes) yield return p;
            if (info.Transpilers != null)
                foreach (Patch p in info.Transpilers) yield return p;
            if (info.Finalizers != null)
                foreach (Patch p in info.Finalizers) yield return p;
        }

        #endregion

        #region Layer 1: On-Demand Memory IL Receiver Tracking & Dual-Run Detection

        /// <summary>
        /// 层 1：针对指定的受保护系统类型集合，按需构建并复用本会话内存 IL 归因索引，
        /// 使用栈顶接收者标签追踪找出对其调用 <c>set_Enabled</c> 的具体 Mod 与方法（严格排除 <c>ldarg.0</c> 关自己，跳过 <c>0Harmony</c> 等非游戏引用库）。
        /// </summary>
        public static Dictionary<Type, List<string>> FindEnabledMutators(
            ICollection<Type> targetSystems,
            World world,
            out Dictionary<Type, List<string>> dualRunReplacements)
        {
            var mutators = new Dictionary<Type, List<string>>();
            dualRunReplacements = new Dictionary<Type, List<string>>();
            if (targetSystems == null || targetSystems.Count == 0) return mutators;

            try
            {
                EnsureLayer1IndexBuilt();

                // 按第三方主 Mod 资产收集当前 World 中处于 Enabled == true 的模拟系统（排除纯 UI / 工具系统以减少静态双跑误判）
                var activeSimSystemsByMod = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                ModManager mm = GameManager.instance?.modManager;
                if (world != null && world.IsCreated)
                {
                    foreach (ComponentSystemBase sys in world.Systems)
                    {
                        if (sys == null || !sys.Enabled) continue;
                        Type st = sys.GetType();
                        if (typeof(UISystemBase).IsAssignableFrom(st) || typeof(ToolBaseSystem).IsAssignableFrom(st))
                            continue;

                        Assembly asm = st.Assembly;
                        if (asm == typeof(GameSystemBase).Assembly ||
                            asm == typeof(ComponentSystemBase).Assembly ||
                            IsSelfOrCompanionAssembly(asm))
                        {
                            continue;
                        }

                        if (mm != null && mm.TryGetExecutableAsset(asm, out ExecutableAsset sysAsset) && sysAsset != null)
                        {
                            string modLabel = FormatAssemblyIdentity(asm);
                            if (!activeSimSystemsByMod.TryGetValue(modLabel, out List<string> sysNames))
                            {
                                sysNames = new List<string>();
                                activeSimSystemsByMod[modLabel] = sysNames;
                            }

                            if (!sysNames.Contains(st.Name))
                                sysNames.Add(st.Name);
                        }
                    }
                }

                foreach (Type target in targetSystems)
                {
                    if (target == null) continue;
                    if (s_CachedMutatorCallers.TryGetValue(target, out List<string> callers) && callers.Count > 0)
                    {
                        mutators[target] = new List<string>(callers);
                    }

                    if (s_CachedMutatorModLabels.TryGetValue(target, out List<string> modLabels))
                    {
                        foreach (string modLabel in modLabels)
                        {
                            if (activeSimSystemsByMod.TryGetValue(modLabel, out List<string> customSysList) &&
                                customSysList.Count > 0)
                            {
                                string sampleSys = string.Join(", ", customSysList.Take(3));
                                AddAttribution(dualRunReplacements, target, $"{modLabel} ({sampleSys})");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"层 1 内存 IL 归因查询异常，已降级: {ex.Message}");
            }

            return mutators;
        }

        private static void EnsureLayer1IndexBuilt()
        {
            if (s_Layer1IndexBuilt) return;
            s_Layer1IndexBuilt = true;

            var sw = Stopwatch.StartNew();
            var allProtectedTypes = BuildSessionProtectedTypeSet();
            if (allProtectedTypes.Count == 0) return;

            int scannedAssemblies = 0;
            int skippedAssemblies = 0;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;

            foreach (Assembly asm in GetLoadedThirdPartyAssemblies())
            {
                if (!ReferencesGameOrEntities(asm))
                {
                    skippedAssemblies++;
                    continue;
                }

                scannedAssemblies++;
                string modIdentity = FormatAssemblyIdentity(asm);
                var hitTypesInThisAsm = new HashSet<Type>();

                foreach (Type type in SafeGetTypes(asm))
                {
                    Dictionary<FieldInfo, Type> fieldSystemMap = CollectProtectedFieldsInType(type, allProtectedTypes);

                    foreach (MethodInfo method in SafeGetMethods(type, flags))
                    {
                        ScanMethodForEnabledWrites(method, allProtectedTypes, fieldSystemMap, modIdentity, s_CachedMutatorCallers, hitTypesInThisAsm);
                    }

                    foreach (ConstructorInfo ctor in SafeGetConstructors(type, flags))
                    {
                        ScanMethodForEnabledWrites(ctor, allProtectedTypes, fieldSystemMap, modIdentity, s_CachedMutatorCallers, hitTypesInThisAsm);
                    }
                }

                foreach (Type hitSys in hitTypesInThisAsm)
                {
                    AddAttribution(s_CachedMutatorModLabels, hitSys, modIdentity);
                }
            }

            sw.Stop();
            ModLog.Scan(Tag,
                $"层 1 归因索引构建完成：扫描 {scannedAssemblies} 个候选程序集（跳过 {skippedAssemblies} 个非游戏引用库），耗时 {sw.ElapsedMilliseconds} ms");
        }

        private static HashSet<Type> BuildSessionProtectedTypeSet()
        {
            var set = new HashSet<Type>();
            Assembly gameAsm = typeof(GameSystemBase).Assembly;
            foreach (ProtectedSystemTarget item in ConflictTargets.ProtectedSystems)
            {
                if (!item.CheckStaticEnabledWrite) continue;
                Type resolved = gameAsm.GetType(item.FullTypeName, throwOnError: false);
                if (resolved != null)
                    set.Add(resolved);
            }

            ReplacementRegistrar registrar = SystemReplacer.Registrar;
            if (registrar != null)
            {
                foreach (ReplacementRegistrar.Declaration decl in registrar.Declarations)
                {
                    foreach (Type v in decl.Vanilla)
                    {
                        if (v != null) set.Add(v);
                    }

                    if (decl.ModType != null)
                        set.Add(decl.ModType);
                }
            }

            return set;
        }

        private static bool ReferencesGameOrEntities(Assembly asm)
        {
            try
            {
                foreach (AssemblyName refName in asm.GetReferencedAssemblies())
                {
                    string n = refName.Name;
                    if (string.Equals(n, "Game", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n, "Unity.Entities", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static Dictionary<FieldInfo, Type> CollectProtectedFieldsInType(Type declaringType, HashSet<Type> targetSet)
        {
            var map = new Dictionary<FieldInfo, Type>();
            try
            {
                foreach (FieldInfo f in declaringType.GetFields(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (targetSet.Contains(f.FieldType))
                        map[f] = f.FieldType;
                }
            }
            catch
            {
                // 忽略个别类型字段解析异常
            }

            return map;
        }

        private static void ScanMethodForEnabledWrites(
            MethodBase method,
            HashSet<Type> targetSet,
            Dictionary<FieldInfo, Type> fieldMap,
            string modIdentity,
            Dictionary<Type, List<string>> mutators,
            HashSet<Type> hitTypesInThisAsm)
        {
            MethodBody body;
            byte[] il;
            try
            {
                if (method.IsAbstract || method.ContainsGenericParameters) return;
                body = method.GetMethodBody();
                il = body?.GetILAsByteArray();
                if (il == null || il.Length == 0) return;
            }
            catch
            {
                return;
            }

            // 快速预筛：方法体内必须包含 call (0x28) 或 callvirt (0x6F)
            bool hasCall = false;
            for (int i = 0; i < il.Length; i++)
            {
                if (il[i] == 0x28 || il[i] == 0x6F)
                {
                    hasCall = true;
                    break;
                }
            }

            if (!hasCall) return;

            Module module = method.Module;
            Type[] genericTypeArgs = null;
            Type[] genericMethodArgs = null;
            try
            {
                if (method.DeclaringType != null && method.DeclaringType.IsGenericType)
                    genericTypeArgs = method.DeclaringType.GetGenericArguments();
                if (method.IsGenericMethod)
                    genericMethodArgs = method.GetGenericArguments();
            }
            catch
            {
                return;
            }

            bool isInstance = !method.IsStatic;
            ParameterInfo[] parameters;
            try
            {
                parameters = method.GetParameters();
            }
            catch
            {
                return;
            }

            int argSlots = (isInstance ? 1 : 0) + parameters.Length;
            var argTags = new SlotTag[Math.Max(argSlots, 4)];
            if (isInstance) argTags[0] = SlotTag.Self;
            for (int i = 0; i < parameters.Length; i++)
            {
                Type pt = parameters[i].ParameterType;
                if (targetSet.Contains(pt))
                    argTags[(isInstance ? 1 : 0) + i] = SlotTag.Protected(pt);
            }

            IList<LocalVariableInfo> locals = body.LocalVariables;
            var localTags = new SlotTag[Math.Max(locals.Count, 8)];
            for (int i = 0; i < locals.Count; i++)
            {
                Type lt = locals[i].LocalType;
                if (lt != null && targetSet.Contains(lt))
                    localTags[i] = SlotTag.Protected(lt);
            }

            var stack = new List<SlotTag>(8);
            Dictionary<int, List<SlotTag>> branchStacks = null;

            int pos = 0;
            while (pos < il.Length)
            {
                if (branchStacks != null && branchStacks.TryGetValue(pos, out List<SlotTag> incoming))
                    MergeStack(stack, incoming);

                byte opByte = il[pos++];
                OpSpec spec;
                if (opByte == 0xFE)
                {
                    if (pos >= il.Length) break;
                    spec = s_TwoByteOps[il[pos++]];
                }
                else
                {
                    spec = s_OneByteOps[opByte];
                }

                OpCode op = spec.Code;
                int operandStart = pos;

                if (op.OperandType == OperandType.InlineSwitch)
                {
                    if (pos + 4 > il.Length) break;
                    uint count = BitConverter.ToUInt32(il, pos);
                    pos += 4;
                    long jumpBytes = (long)count * 4;
                    if (pos + jumpBytes > il.Length) break;
                    PopSafe(stack);
                    for (uint i = 0; i < count; i++)
                    {
                        int delta = BitConverter.ToInt32(il, pos + (int)(i * 4));
                        RecordBranch(ref branchStacks, pos + (int)jumpBytes + delta, stack);
                    }

                    pos += (int)jumpBytes;
                    continue;
                }

                if (pos + spec.OperandBytes > il.Length) break;
                pos += spec.OperandBytes;

                short opVal = op.Value;

                if (opVal == OpCodes.Ldnull.Value)
                {
                    stack.Add(SlotTag.Null);
                    continue;
                }

                if (TryGetLdarg(opVal, il, operandStart, out int argIdx))
                {
                    stack.Add(argIdx >= 0 && argIdx < argTags.Length ? argTags[argIdx] : SlotTag.Other);
                    continue;
                }

                if (TryGetLdloc(opVal, il, operandStart, out int ldlocIdx))
                {
                    stack.Add(ldlocIdx >= 0 && ldlocIdx < localTags.Length ? localTags[ldlocIdx] : SlotTag.Other);
                    continue;
                }

                if (TryGetStloc(opVal, il, operandStart, out int stlocIdx))
                {
                    SlotTag top = PopSafe(stack);
                    if (stlocIdx >= localTags.Length)
                        Array.Resize(ref localTags, Math.Max(stlocIdx + 1, localTags.Length * 2));
                    if (top.Kind == SlotKind.ProtectedSystem)
                        localTags[stlocIdx] = top;
                    else if (top.Kind == SlotKind.Self && localTags[stlocIdx].Kind != SlotKind.ProtectedSystem)
                        localTags[stlocIdx] = SlotTag.Self;
                    continue;
                }

                if (opVal == OpCodes.Dup.Value)
                {
                    stack.Add(stack.Count > 0 ? stack[stack.Count - 1] : SlotTag.Other);
                    continue;
                }

                if (opVal == OpCodes.Pop.Value)
                {
                    PopSafe(stack);
                    continue;
                }

                if (opVal == OpCodes.Ldtoken.Value || opVal == OpCodes.Castclass.Value || opVal == OpCodes.Isinst.Value)
                {
                    if (opVal != OpCodes.Ldtoken.Value) PopSafe(stack);
                    Type resolvedType = SafeResolveType(module, BitConverter.ToInt32(il, operandStart), genericTypeArgs, genericMethodArgs);
                    stack.Add(resolvedType != null && targetSet.Contains(resolvedType) ? SlotTag.Protected(resolvedType) : SlotTag.Other);
                    continue;
                }

                if (opVal == OpCodes.Ldfld.Value || opVal == OpCodes.Ldsfld.Value)
                {
                    if (opVal == OpCodes.Ldfld.Value) PopSafe(stack);
                    FieldInfo field = SafeResolveField(module, BitConverter.ToInt32(il, operandStart), genericTypeArgs, genericMethodArgs);
                    Type sysType = null;
                    if (field != null)
                    {
                        if (!fieldMap.TryGetValue(field, out sysType) && targetSet.Contains(field.FieldType))
                            sysType = field.FieldType;
                    }

                    stack.Add(sysType != null ? SlotTag.Protected(sysType) : SlotTag.Other);
                    continue;
                }

                if (opVal == OpCodes.Call.Value || opVal == OpCodes.Callvirt.Value || opVal == OpCodes.Newobj.Value)
                {
                    MethodBase called = SafeResolveMethod(module, BitConverter.ToInt32(il, operandStart), genericTypeArgs, genericMethodArgs);
                    if (called != null)
                    {
                        ParameterInfo[] cParams;
                        try { cParams = called.GetParameters(); }
                        catch { stack.Clear(); continue; }

                        if (called.Name == "set_Enabled" && cParams.Length == 1 && !called.IsStatic && opVal != OpCodes.Newobj.Value)
                        {
                            PopSafe(stack); // 弹出任意布尔实参
                            SlotTag receiver = PopSafe(stack);
                            if (receiver.Kind == SlotKind.ProtectedSystem && receiver.SystemType != null)
                            {
                                string entry = $"{modIdentity} [{method.DeclaringType?.Name}.{method.Name}]";
                                AddAttribution(mutators, receiver.SystemType, entry);
                                hitTypesInThisAsm.Add(receiver.SystemType);
                            }
                            else if (receiver.Kind != SlotKind.Self && called.DeclaringType != null && targetSet.Contains(called.DeclaringType))
                            {
                                string entry = $"{modIdentity} [{method.DeclaringType?.Name}.{method.Name}]";
                                AddAttribution(mutators, called.DeclaringType, entry);
                                hitTypesInThisAsm.Add(called.DeclaringType);
                            }

                            continue;
                        }

                        if (called.Name == "GetTypeFromHandle" && called.DeclaringType == typeof(Type) && cParams.Length == 1 && called.IsStatic)
                        {
                            SlotTag handleSlot = PopSafe(stack);
                            stack.Add(handleSlot.Kind == SlotKind.ProtectedSystem ? handleSlot : SlotTag.Other);
                            continue;
                        }

                        SlotTag firstArg = SlotTag.Other;
                        for (int p = cParams.Length - 1; p >= 0; p--)
                        {
                            SlotTag popped = PopSafe(stack);
                            if (p == 0) firstArg = popped;
                        }

                        if (!called.IsStatic && opVal != OpCodes.Newobj.Value)
                            PopSafe(stack);

                        if (opVal == OpCodes.Newobj.Value)
                        {
                            Type ct = called.DeclaringType;
                            stack.Add(ct != null && targetSet.Contains(ct) ? SlotTag.Protected(ct) : SlotTag.Other);
                        }
                        else if (called is MethodInfo mi)
                        {
                            Type retSys = null;
                            if (mi.Name == "GetOrCreateSystemManaged" || mi.Name == "GetExistingSystemManaged")
                            {
                                if (mi.IsGenericMethod)
                                {
                                    Type ga = mi.GetGenericArguments()[0];
                                    if (targetSet.Contains(ga)) retSys = ga;
                                }
                                else if (cParams.Length == 1 && firstArg.Kind == SlotKind.ProtectedSystem)
                                {
                                    retSys = firstArg.SystemType;
                                }
                            }
                            else if (mi.ReturnType != typeof(void) && targetSet.Contains(mi.ReturnType))
                            {
                                retSys = mi.ReturnType;
                            }

                            if (mi.ReturnType != typeof(void))
                                stack.Add(retSys != null ? SlotTag.Protected(retSys) : SlotTag.Other);
                        }

                        continue;
                    }
                }

                if (op.OperandType == OperandType.InlineBrTarget || op.OperandType == OperandType.ShortInlineBrTarget)
                {
                    int delta = op.OperandType == OperandType.ShortInlineBrTarget
                        ? (sbyte)il[operandStart]
                        : BitConverter.ToInt32(il, operandStart);
                    ApplyPop(stack, op.StackBehaviourPop);
                    RecordBranch(ref branchStacks, pos + delta, stack);
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

                ApplyPop(stack, op.StackBehaviourPop);
                ApplyPush(stack, op.StackBehaviourPush);
            }
        }

        #endregion

        #region Layer 2: Harmony Snapshot Survival & State-Tiered UnpatchAll(null) Attribution

        /// <summary>
        /// 层 2：核查本会话在 <c>OnLoad</c> 末尾录制的 Harmony 补丁快照是否仍在，并检测同一目标方法上的外部重叠 <c>Transpiler</c>／阻塞型 <c>Prefix</c>。
        /// </summary>
        public static void InspectSessionPatches(
            List<CriticalIssueItem> strippedIssues,
            List<string> overlapWarnings,
            HashSet<string> culpritModIdentities)
        {
            if (s_SessionPatches.Count == 0) return;

            try
            {
                int strippedCount = 0;
                var strippedSamples = new List<string>();

                foreach (CapturedPatchRecord rec in s_SessionPatches)
                {
                    HarmonyLib.Patches info = Harmony.GetPatchInfo(rec.TargetMethod);
                    bool modesAlive = !rec.HadModesPatch || (info != null && HasOwner(info, Mod.HarmonyIdModes));
                    bool globalAlive = !rec.HadGlobalPatch || (info != null && HasOwner(info, Mod.HarmonyIdGlobal));

                    if (!modesAlive || !globalAlive)
                    {
                        strippedCount++;
                        if (strippedSamples.Count < 5)
                            strippedSamples.Add($"{rec.TargetMethod.DeclaringType?.Name}.{rec.TargetMethod.Name}");
                        continue;
                    }

                    if (info == null) continue;

                    // 检查外部重叠 Transpiler 或返回 bool 的 Prefix
                    if (info.Transpilers != null)
                    {
                        foreach (Patch p in info.Transpilers)
                        {
                            if (IsOwnPatch(p) || p.PatchMethod?.DeclaringType == null) continue;
                            Assembly extAsm = p.PatchMethod.DeclaringType.Assembly;
                            if (IsSelfOrCompanionAssembly(extAsm)) continue;

                            string src = FormatAssemblyIdentity(extAsm);
                            string msg = $"{rec.TargetMethod.DeclaringType?.Name}.{rec.TargetMethod.Name} [外部 Transpiler: {src}]";
                            if (!overlapWarnings.Contains(msg))
                                overlapWarnings.Add(msg);
                        }
                    }

                    if (info.Prefixes != null)
                    {
                        foreach (Patch p in info.Prefixes)
                        {
                            if (IsOwnPatch(p) || p.PatchMethod?.DeclaringType == null) continue;
                            if (p.PatchMethod.ReturnType != typeof(bool)) continue;
                            Assembly extAsm = p.PatchMethod.DeclaringType.Assembly;
                            if (IsSelfOrCompanionAssembly(extAsm)) continue;

                            string src = FormatAssemblyIdentity(extAsm);
                            string msg = $"{rec.TargetMethod.DeclaringType?.Name}.{rec.TargetMethod.Name} [外部拦截型 Prefix: {src}]";
                            if (!overlapWarnings.Contains(msg))
                                overlapWarnings.Add(msg);
                        }
                    }
                }

                if (strippedCount > 0)
                {
                    List<string> culprits = FindUnpatchAllNullCulpritsByState();
                    foreach (string c in culprits)
                        culpritModIdentities.Add(c);

                    string sampleStr = string.Join(", ", strippedSamples);
                    string culpritDesc = culprits.Count > 0
                        ? $"定位来源: {string.Join("; ", culprits)}"
                        : "未能定位特定调用源";
                    strippedIssues.Add(new CriticalIssueItem
                    {
                        Kind = CriticalIssueKind.HarmonyPatchesRemoved,
                        Count = strippedCount,
                        Sample = sampleStr,
                        LogDetail = $"Harmony 补丁被移除 ({strippedCount} 个目标方法，如 {sampleStr}；{culpritDesc})",
                    });
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"层 2 Harmony 补丁存活核查异常，已跳过: {ex.Message}");
            }
        }

        /// <summary>
        /// 按运行期 <c>ModInfo.State</c> 分层锁定调用了无参 <c>Harmony.UnpatchAll(null)</c> 的模组：
        /// ① 优先匹配在非 <c>OnDispose</c> 路径含该调用且 <c>state == Loaded</c> 的模组；
        /// ② 若无，则在 <c>OnDispose</c> 路径含该调用的候选集中匹配 <c>state is Disposed or GeneralError</c>（因自身 <c>OnLoad</c> 异常触发卸载清场）的模组。
        /// </summary>
        private static List<string> FindUnpatchAllNullCulpritsByState()
        {
            var tier1LoadedNonDispose = new List<string>();
            var tier2DisposedOrError = new List<string>();

            ModManager mm = GameManager.instance?.modManager;
            if (mm == null) return tier1LoadedNonDispose;

            foreach (ModManager.ModInfo modInfo in mm)
            {
                ExecutableAsset asset = modInfo.asset;
                Assembly asm = asset?.assembly;
                if (asm == null || IsSelfOrCompanionAssembly(asm)) continue;

                ScanAssemblyForUnpatchAllNull(asm, out string nonDisposeCaller, out string disposeCaller);
                if (nonDisposeCaller == null && disposeCaller == null) continue;

                string label = FormatAssemblyIdentity(asm);
                if (nonDisposeCaller != null && modInfo.state == ModManager.ModInfo.State.Loaded)
                {
                    string item = $"{label} [{nonDisposeCaller}]";
                    if (!tier1LoadedNonDispose.Contains(item))
                        tier1LoadedNonDispose.Add(item);
                }
                else if (disposeCaller != null &&
                         (modInfo.state == ModManager.ModInfo.State.Disposed ||
                          modInfo.state == ModManager.ModInfo.State.GeneralError ||
                          modInfo.state == ModManager.ModInfo.State.LoadAssemblyError ||
                          modInfo.state == ModManager.ModInfo.State.LoadAssemblyReferenceError))
                {
                    string item = $"{label} [{disposeCaller}, state={modInfo.state}]";
                    if (!tier2DisposedOrError.Contains(item))
                        tier2DisposedOrError.Add(item);
                }
            }

            return tier1LoadedNonDispose.Count > 0 ? tier1LoadedNonDispose : tier2DisposedOrError;
        }

        private static void ScanAssemblyForUnpatchAllNull(
            Assembly asm,
            out string nonDisposeCaller,
            out string disposeCaller)
        {
            nonDisposeCaller = null;
            disposeCaller = null;

            var callSites = new List<(MethodBase Method, string Display)>();
            var edges = new HashSet<(MethodBase Caller, MethodBase Callee)>();
            var disposeRoots = new HashSet<MethodBase>();
            var lifecycleRoots = new HashSet<MethodBase>();

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;

            foreach (Type type in SafeGetTypes(asm))
            {
                bool isIMod = typeof(IMod).IsAssignableFrom(type);
                foreach (MethodBase m in SafeGetMethods(type, flags).Cast<MethodBase>().Concat(SafeGetConstructors(type, flags)))
                {
                    if (isIMod && (m.Name == "OnDispose" || m.Name.EndsWith(".OnDispose", StringComparison.Ordinal)))
                        disposeRoots.Add(m);
                    else if ((isIMod && (m.Name == "OnLoad" || m.Name.EndsWith(".OnLoad", StringComparison.Ordinal))) ||
                             m.IsConstructor || m.Name == "OnCreate" || m.Name == "OnUpdate")
                        lifecycleRoots.Add(m);

                    ScanMethodForUnpatchAllAndEdges(m, asm, callSites, edges);
                }
            }

            if (callSites.Count == 0) return;

            // 构建简单正向可达集
            var forward = new Dictionary<MethodBase, List<MethodBase>>();
            var backward = new Dictionary<MethodBase, List<MethodBase>>();
            foreach ((MethodBase caller, MethodBase callee) in edges)
            {
                if (!forward.TryGetValue(caller, out List<MethodBase> fl))
                {
                    fl = new List<MethodBase>();
                    forward[caller] = fl;
                }
                fl.Add(callee);

                if (!backward.TryGetValue(callee, out List<MethodBase> bl))
                {
                    bl = new List<MethodBase>();
                    backward[callee] = bl;
                }
                bl.Add(caller);
            }

            HashSet<MethodBase> reachDispose = Bfs(disposeRoots, forward);
            HashSet<MethodBase> reachLifecycle = Bfs(lifecycleRoots, forward);

            foreach ((MethodBase m, string disp) in callSites)
            {
                bool onlyDispose = disposeRoots.Contains(m) ||
                                   (reachDispose.Contains(m) && !reachLifecycle.Contains(m) && !m.IsPublic);
                if (onlyDispose)
                    disposeCaller ??= disp;
                else
                    nonDisposeCaller ??= disp;
            }
        }

        private static void ScanMethodForUnpatchAllAndEdges(
            MethodBase method,
            Assembly owningAsm,
            List<(MethodBase, string)> callSites,
            HashSet<(MethodBase, MethodBase)> edges)
        {
            byte[] il;
            try
            {
                if (method.IsAbstract || method.ContainsGenericParameters) return;
                il = method.GetMethodBody()?.GetILAsByteArray();
                if (il == null || il.Length == 0) return;
            }
            catch
            {
                return;
            }

            Module module = method.Module;
            bool prevWasLdnull = false;
            int pos = 0;

            while (pos < il.Length)
            {
                byte b = il[pos++];
                OpSpec spec;
                if (b == 0xFE)
                {
                    if (pos >= il.Length) break;
                    spec = s_TwoByteOps[il[pos++]];
                }
                else
                {
                    spec = s_OneByteOps[b];
                }

                int operandStart = pos;
                if (spec.Code.OperandType == OperandType.InlineSwitch)
                {
                    if (pos + 4 > il.Length) break;
                    uint c = BitConverter.ToUInt32(il, pos);
                    pos += 4 + (int)(c * 4);
                    prevWasLdnull = false;
                    continue;
                }

                if (pos + spec.OperandBytes > il.Length) break;
                pos += spec.OperandBytes;

                short opVal = spec.Code.Value;
                if (opVal == OpCodes.Ldnull.Value)
                {
                    prevWasLdnull = true;
                    continue;
                }

                if (opVal == OpCodes.Call.Value || opVal == OpCodes.Callvirt.Value)
                {
                    MethodBase target = SafeResolveMethod(module, BitConverter.ToInt32(il, operandStart), null, null);
                    if (target != null)
                    {
                        if (target.DeclaringType?.Assembly == owningAsm)
                            edges.Add((method, target));

                        if (prevWasLdnull &&
                            target.Name == "UnpatchAll" &&
                            target.DeclaringType == typeof(Harmony) &&
                            target.GetParameters().Length == 1)
                        {
                            callSites.Add((method, $"{method.DeclaringType?.Name}.{method.Name}"));
                        }
                    }
                }

                if (opVal != OpCodes.Nop.Value)
                    prevWasLdnull = false;
            }
        }

        private static HashSet<MethodBase> Bfs(HashSet<MethodBase> roots, Dictionary<MethodBase, List<MethodBase>> graph)
        {
            var visited = new HashSet<MethodBase>(roots);
            var list = new List<MethodBase>(roots);
            for (int i = 0; i < list.Count; i++)
            {
                MethodBase cur = list[i];
                if (!graph.TryGetValue(cur, out List<MethodBase> nexts)) continue;
                foreach (MethodBase n in nexts)
                {
                    if (visited.Add(n)) list.Add(n);
                }
            }

            return visited;
        }

        private static bool IsOwnPatch(Patch p) =>
            p.owner == Mod.HarmonyIdModes || p.owner == Mod.HarmonyIdGlobal;

        #endregion

        #region Layer 3: CellMapSystem<T>.kMapSize == 14336 Readback & Reflection Attribution

        /// <summary>
        /// 层 3：回读核验全部 14 个 <c>CellMapSystem&lt;T&gt;.kMapSize == 14336</c>（MapExt 只做 Transpiler 常数替换，绝不改写静态字段本身）；
        /// 一旦不等于 14336，按需扫描同一方法体内同时包含 <c>"kMapSize"</c> 与 <c>FieldInfo.SetValue</c> 的已加载第三方程序集。
        /// </summary>
        public static void CheckCellMapStaticFields(
            List<CriticalIssueItem> criticalIssues,
            HashSet<string> culpritModIdentities)
        {
            try
            {
                var tampered = new List<string>();
                foreach (Type derived in s_MonitoredCellMapSystems)
                {
                    Type baseType = derived.BaseType;
                    while (baseType != null)
                    {
                        if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(CellMapSystem<>))
                        {
                            FieldInfo mapField = baseType.GetField("kMapSize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            if (mapField != null && mapField.GetValue(null) is int actual && actual != PatchManager.OriginalMapSize)
                            {
                                tampered.Add($"{derived.Name}.kMapSize={actual}");
                            }

                            break;
                        }

                        baseType = baseType.BaseType;
                    }
                }

                if (tampered.Count == 0) return;

                List<string> reflectionSuspects = FindSameMethodFieldMutators();
                foreach (string s in reflectionSuspects)
                    culpritModIdentities.Add(s);

                string sampleStr = string.Join(", ", tampered.Take(3));
                string srcDesc = reflectionSuspects.Count > 0
                    ? $"疑似改写源: {string.Join("; ", reflectionSuspects)}"
                    : "未定位到同方法反射源";
                string logDetail = $"CellMapSystem<T>.kMapSize 静态字段被外部改写 ({tampered.Count} 处，如 {sampleStr}；{srcDesc})";
                ModLog.Error(Tag, logDetail);
                criticalIssues.Add(new CriticalIssueItem
                {
                    Kind = CriticalIssueKind.MapSizeStaticFieldTampered,
                    Count = tampered.Count,
                    Sample = sampleStr,
                    LogDetail = logDetail,
                });
            }
            catch (Exception ex)
            {
                ModLog.Warn(Tag, $"核验 CellMapSystem<T>.kMapSize 异常，已跳过: {ex.Message}");
            }
        }

        private static List<string> FindSameMethodFieldMutators()
        {
            var results = new List<string>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static |
                                       BindingFlags.DeclaredOnly;

            foreach (Assembly asm in GetLoadedThirdPartyAssemblies())
            {
                string modLabel = FormatAssemblyIdentity(asm);
                foreach (Type type in SafeGetTypes(asm))
                {
                    foreach (MethodBase m in SafeGetMethods(type, flags).Cast<MethodBase>().Concat(SafeGetConstructors(type, flags)))
                    {
                        if (MethodHasMapSizeStringAndSetValue(m))
                        {
                            string entry = $"{modLabel} [{type.Name}.{m.Name}]";
                            if (!results.Contains(entry))
                                results.Add(entry);
                        }
                    }
                }
            }

            return results;
        }

        private static bool MethodHasMapSizeStringAndSetValue(MethodBase method)
        {
            byte[] il;
            try
            {
                if (method.IsAbstract || method.ContainsGenericParameters) return false;
                il = method.GetMethodBody()?.GetILAsByteArray();
                if (il == null || il.Length == 0) return false;
            }
            catch
            {
                return false;
            }

            // 快速字节过滤：必须同时存在 ldstr (0x72) 与 call/callvirt (0x28/0x6F)
            bool hasLdstr = false, hasCall = false;
            for (int i = 0; i < il.Length; i++)
            {
                if (il[i] == 0x72) hasLdstr = true;
                else if (il[i] == 0x28 || il[i] == 0x6F) hasCall = true;
            }

            if (!hasLdstr || !hasCall) return false;

            Module module = method.Module;
            bool seenMapSizeStr = false;
            bool seenSetValue = false;
            int pos = 0;

            while (pos < il.Length)
            {
                byte b = il[pos++];
                OpSpec spec;
                if (b == 0xFE)
                {
                    if (pos >= il.Length) break;
                    spec = s_TwoByteOps[il[pos++]];
                }
                else
                {
                    spec = s_OneByteOps[b];
                }

                int operandStart = pos;
                if (spec.Code.OperandType == OperandType.InlineSwitch)
                {
                    if (pos + 4 > il.Length) break;
                    uint c = BitConverter.ToUInt32(il, pos);
                    pos += 4 + (int)(c * 4);
                    continue;
                }

                if (pos + spec.OperandBytes > il.Length) break;
                pos += spec.OperandBytes;

                short opVal = spec.Code.Value;
                if (opVal == OpCodes.Ldstr.Value)
                {
                    try
                    {
                        string s = module.ResolveString(BitConverter.ToInt32(il, operandStart));
                        if (s == "kMapSize" || s == "kTextureSize")
                            seenMapSizeStr = true;
                    }
                    catch
                    {
                        // 忽略异常 token
                    }
                }
                else if (opVal == OpCodes.Call.Value || opVal == OpCodes.Callvirt.Value)
                {
                    MethodBase called = SafeResolveMethod(module, BitConverter.ToInt32(il, operandStart), null, null);
                    if (called != null &&
                        (called.Name == "SetValue" || called.Name == "SetValueDirect") &&
                        typeof(FieldInfo).IsAssignableFrom(called.DeclaringType))
                    {
                        seenSetValue = true;
                    }
                }

                if (seenMapSizeStr && seenSetValue)
                    return true;
            }

            return false;
        }

        #endregion

        #region Low-Level Reflection & Stack Helpers

        private static IEnumerable<Assembly> GetLoadedThirdPartyAssemblies()
        {
            ModManager mm = GameManager.instance?.modManager;
            if (mm == null) yield break;

            var seen = new HashSet<Assembly>();
            foreach (ModManager.ModInfo modInfo in mm)
            {
                ExecutableAsset asset = modInfo.asset;
                if (asset == null || !asset.isLoaded || asset.assembly == null) continue;
                if (IsSelfOrCompanionAssembly(asset.assembly)) continue;
                if (seen.Add(asset.assembly))
                    yield return asset.assembly;
            }
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static MethodInfo[] SafeGetMethods(Type type, BindingFlags flags)
        {
            try { return type.GetMethods(flags); }
            catch { return Array.Empty<MethodInfo>(); }
        }

        private static ConstructorInfo[] SafeGetConstructors(Type type, BindingFlags flags)
        {
            try { return type.GetConstructors(flags); }
            catch { return Array.Empty<ConstructorInfo>(); }
        }

        private static MethodBase SafeResolveMethod(Module module, int token, Type[] genTypeArgs, Type[] genMethodArgs)
        {
            try { return module.ResolveMethod(token, genTypeArgs, genMethodArgs); }
            catch { return null; }
        }

        private static FieldInfo SafeResolveField(Module module, int token, Type[] genTypeArgs, Type[] genMethodArgs)
        {
            try { return module.ResolveField(token, genTypeArgs, genMethodArgs); }
            catch { return null; }
        }

        private static Type SafeResolveType(Module module, int token, Type[] genTypeArgs, Type[] genMethodArgs)
        {
            try { return module.ResolveType(token, genTypeArgs, genMethodArgs); }
            catch { return null; }
        }

        private static void AddAttribution(Dictionary<Type, List<string>> dict, Type key, string value)
        {
            if (!dict.TryGetValue(key, out List<string> list))
            {
                list = new List<string>();
                dict[key] = list;
            }

            if (!list.Contains(value))
                list.Add(value);
        }

        private static SlotTag PopSafe(List<SlotTag> stack)
        {
            if (stack.Count == 0) return SlotTag.Other;
            SlotTag top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return top;
        }

        private static void RecordBranch(ref Dictionary<int, List<SlotTag>> map, int targetOffset, List<SlotTag> current)
        {
            if (targetOffset < 0 || current.Count == 0) return;
            if (map == null) map = new Dictionary<int, List<SlotTag>>();
            if (!map.TryGetValue(targetOffset, out List<SlotTag> existing))
            {
                map[targetOffset] = new List<SlotTag>(current);
                return;
            }

            MergeStack(existing, current);
        }

        private static void MergeStack(List<SlotTag> current, List<SlotTag> incoming)
        {
            if (current.Count == 0)
            {
                current.AddRange(incoming);
                return;
            }

            int count = Math.Min(current.Count, incoming.Count);
            for (int i = 0; i < count; i++)
            {
                if (current[i].Kind != SlotKind.ProtectedSystem && incoming[i].Kind == SlotKind.ProtectedSystem)
                    current[i] = incoming[i];
            }
        }

        private static bool TryGetLdarg(short opVal, byte[] il, int operandStart, out int idx)
        {
            if (opVal == OpCodes.Ldarg_0.Value) { idx = 0; return true; }
            if (opVal == OpCodes.Ldarg_1.Value) { idx = 1; return true; }
            if (opVal == OpCodes.Ldarg_2.Value) { idx = 2; return true; }
            if (opVal == OpCodes.Ldarg_3.Value) { idx = 3; return true; }
            if (opVal == OpCodes.Ldarg_S.Value) { idx = il[operandStart]; return true; }
            if (opVal == OpCodes.Ldarg.Value) { idx = BitConverter.ToUInt16(il, operandStart); return true; }
            idx = -1;
            return false;
        }

        private static bool TryGetLdloc(short opVal, byte[] il, int operandStart, out int idx)
        {
            if (opVal == OpCodes.Ldloc_0.Value) { idx = 0; return true; }
            if (opVal == OpCodes.Ldloc_1.Value) { idx = 1; return true; }
            if (opVal == OpCodes.Ldloc_2.Value) { idx = 2; return true; }
            if (opVal == OpCodes.Ldloc_3.Value) { idx = 3; return true; }
            if (opVal == OpCodes.Ldloc_S.Value) { idx = il[operandStart]; return true; }
            if (opVal == OpCodes.Ldloc.Value) { idx = BitConverter.ToUInt16(il, operandStart); return true; }
            idx = -1;
            return false;
        }

        private static bool TryGetStloc(short opVal, byte[] il, int operandStart, out int idx)
        {
            if (opVal == OpCodes.Stloc_0.Value) { idx = 0; return true; }
            if (opVal == OpCodes.Stloc_1.Value) { idx = 1; return true; }
            if (opVal == OpCodes.Stloc_2.Value) { idx = 2; return true; }
            if (opVal == OpCodes.Stloc_3.Value) { idx = 3; return true; }
            if (opVal == OpCodes.Stloc_S.Value) { idx = il[operandStart]; return true; }
            if (opVal == OpCodes.Stloc.Value) { idx = BitConverter.ToUInt16(il, operandStart); return true; }
            idx = -1;
            return false;
        }

        private static void ApplyPop(List<SlotTag> stack, StackBehaviour pop)
        {
            int count;
            switch (pop)
            {
                case StackBehaviour.Pop0:
                    count = 0;
                    break;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    count = 1;
                    break;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    count = 2;
                    break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                case StackBehaviour.Popref_popi_pop1:
                    count = 3;
                    break;
                default:
                    count = 0;
                    break;
            }

            for (int i = 0; i < count; i++)
                PopSafe(stack);
        }

        private static void ApplyPush(List<SlotTag> stack, StackBehaviour push)
        {
            int count;
            switch (push)
            {
                case StackBehaviour.Push0:
                case StackBehaviour.Varpush:
                    count = 0;
                    break;
                case StackBehaviour.Push1_push1:
                    count = 2;
                    break;
                default:
                    count = 1;
                    break;
            }

            for (int i = 0; i < count; i++)
                stack.Add(SlotTag.Other);
        }

        private static int GetOperandLength(OperandType operandType)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                case OperandType.InlineSwitch:
                    return 0;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return 8;
                default:
                    return 0;
            }
        }

        #endregion
    }
}
