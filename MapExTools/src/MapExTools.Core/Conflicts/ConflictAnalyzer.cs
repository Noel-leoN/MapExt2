using System.Diagnostics;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>
/// 静态冲突扫描与二维动态评估协调器：
/// 将磁盘字节码读取（<see cref="ScanWorkspace"/>）与 <c>(PatchMode, System)</c> 二维设定定级（<see cref="Evaluate"/>）解耦，
/// 修改 <c>.coc</c> 模式或经济分组时可在内存中瞬时重算。
/// </summary>
public static class ConflictAnalyzer
{
    private static readonly string[] s_findTargetsEconomyKeys =
    [
        ConflictTargets.KeyHouseholdProperty,
        ConflictTargets.KeyResourceBuyer,
        ConflictTargets.KeyDownstreamAI,
    ];

    /// <summary>只读扫描指定或激活 Playset 与本地 <c>Mods/</c> 并完成胜出程序集的字节码特征提取。</summary>
    public static ScannedModWorkspace ScanWorkspace(
        UserDataPaths paths,
        string? gameDir = null,
        string? targetPlaysetId = null,
        IDictionary<AssemblyFileFingerprint, AssemblyInspectionResult?>? inspectionCache = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var sw = Stopwatch.StartNew();
        PlaysetScanResult playset = PlaysetModScanner.Scan(paths, gameDir, targetPlaysetId);

        var byPkg = new Dictionary<string, IReadOnlyList<AssemblyInspectionResult>>(StringComparer.Ordinal);
        var unreadableExtra = new List<UnreadableAssemblyEntry>(playset.UnreadableAssemblies);
        int enabledCodeModCount = 0;

        foreach (EnabledModPackage pkg in playset.Packages)
        {
            if (pkg.WinningAssemblies.Count == 0)
                continue;

            bool pkgHasIMod = false;
            var list = new List<AssemblyInspectionResult>(pkg.WinningAssemblies.Count);

            foreach (WinningAssemblyEntry asm in pkg.WinningAssemblies)
            {
                // 跳过本工程自身及自动休眠配套模组的字节码扫描（防止把自身系统替换误判为外部冲突）
                if (ConflictTargets.IsSelfOrCompanion(asm.AssemblyName, pkg.PdxModId))
                {
                    pkgHasIMod = true;
                    continue;
                }

                try
                {
                    AssemblyInspectionResult? res;
                    if (inspectionCache != null && TryCreateFingerprint(asm.FilePath, out AssemblyFileFingerprint fp))
                    {
                        if (!inspectionCache.TryGetValue(fp, out res))
                        {
                            res = ModAssemblyInspector.InspectFile(asm.FilePath);
                            inspectionCache[fp] = res;
                        }
                    }
                    else
                    {
                        res = ModAssemblyInspector.InspectFile(asm.FilePath);
                    }

                    if (res != null)
                    {
                        list.Add(res);
                        if (res.ImplementsIMod)
                            pkgHasIMod = true;
                    }
                }
                catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OverflowException)
                {
                    unreadableExtra.Add(new UnreadableAssemblyEntry(asm.FilePath, pkg.PackageKey, pkg.DisplayName, ex.Message));
                }
            }

            if (pkgHasIMod || pkg.WinningAssemblies.Count > 0)
                enabledCodeModCount++;

            if (list.Count > 0)
                byPkg[pkg.PackageKey] = list;
        }

        sw.Stop();
        PlaysetScanResult finalPlayset = unreadableExtra.Count == playset.UnreadableAssemblies.Count
            ? playset
            : RebuildUnreadableState(playset, unreadableExtra);

        return new ScannedModWorkspace(
            finalPlayset,
            byPkg,
            enabledCodeModCount,
            sw.ElapsedMilliseconds,
            DateTime.UtcNow);
    }

    private static bool TryCreateFingerprint(string filePath, out AssemblyFileFingerprint fingerprint)
    {
        try
        {
            string full = Path.GetFullPath(filePath);
            var fi = new FileInfo(full);
            if (fi.Exists)
            {
                fingerprint = new AssemblyFileFingerprint(full, fi.LastWriteTimeUtc.Ticks, fi.Length);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // 回退无缓存直接读取
        }

        fingerprint = default;
        return false;
    }

    private static PlaysetScanResult RebuildUnreadableState(PlaysetScanResult playset, List<UnreadableAssemblyEntry> unreadableExtra)
    {
        if (playset.AllModItems == null || playset.AllModItems.Count == 0)
            return playset with { UnreadableAssemblies = unreadableExtra };

        var badKeys = new HashSet<string>(unreadableExtra.Select(u => u.PackageKey), StringComparer.Ordinal);
        List<PlaysetModListItem> updated = playset.AllModItems
            .Select(m => m.LoadState == ModLoadState.AssetOrNonCode && badKeys.Contains(m.PackageKey)
                ? m with { LoadState = ModLoadState.UnreadableAssembly }
                : m)
            .ToList();
        return playset with { UnreadableAssemblies = unreadableExtra, AllModItems = updated };
    }

    /// <summary>结合当前 <c>MapExtPDX.coc</c> 设定快照输出排序后的冲突与备注卡片列表。</summary>
    public static ConflictReport Evaluate(ScannedModWorkspace workspace, ConflictEvalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(settings);

        var items = new List<ModConflictItem>();
        PlaysetScanResult playset = workspace.Playset;

        // 按包键索引损坏 DLL（避免不同源的同名模组串归）
        Dictionary<string, List<UnreadableAssemblyEntry>> unreadableByPkgKey = playset.UnreadableAssemblies
            .GroupBy(u => u.PackageKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var emittedDualInstallRules = new HashSet<string>(StringComparer.Ordinal);

        foreach (EnabledModPackage pkg in playset.Packages)
        {
            // 被更高优副本完全遮蔽的重复包：仅当触发双装规则（如 RoadBuilder 本地+订阅双开）时由胜出方统一报告
            if (pkg.IsShadowedDuplicate)
                continue;

            // 排除 MapExtPDX 自身；若同时启用了独立版 EconomyEX，给出一条自动休眠知情说明
            bool isMapExtSelf = pkg.PdxModId == ConflictTargets.MapExtPdxModId ||
                                pkg.WinningAssemblies.Any(a =>
                                    string.Equals(a.AssemblyName, "MapExt2", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(a.AssemblyName, "MapExtPDX", StringComparison.OrdinalIgnoreCase));
            if (isMapExtSelf)
                continue;

            bool isEconomyExCompanion = pkg.PdxModId == ConflictTargets.EconomyExPdxModId ||
                                        pkg.WinningAssemblies.Any(a =>
                                            string.Equals(a.AssemblyName, "EconomyEX", StringComparison.OrdinalIgnoreCase));
            if (isEconomyExCompanion)
            {
                var companionFinding = new ConflictFinding(
                    ConflictFindingKind.CompanionAutoHibernates,
                    ConflictSeverity.Info,
                    pkg.DisplayName);
                items.Add(new ModConflictItem(
                    pkg.PackageKey,
                    pkg.DisplayName,
                    pkg.Source,
                    pkg.PdxModId,
                    pkg.DisplayVersion,
                    null,
                    false,
                    ConflictSeverity.Info,
                    [companionFinding],
                    []));
                continue;
            }

            var findings = new List<ConflictFinding>();
            KnownModConflictRule? matchedRule = MatchPackageRule(pkg);

            // 1. 云端待下载订阅项（仍允许按 PdxModId 匹配已知规则提前提示）
            if (pkg.IsPendingCloudDownload)
            {
                findings.Add(new ConflictFinding(
                    ConflictFindingKind.PendingCloudDownload,
                    ConflictSeverity.Info,
                    pkg.PdxModId ?? pkg.DisplayName));
            }

            // 2. 缺前置依赖不可加载项
            if (pkg.MissingDependencies.Count > 0)
            {
                findings.Add(new ConflictFinding(
                    ConflictFindingKind.MissingDependencies,
                    ConflictSeverity.Info,
                    string.Join(", ", pkg.MissingDependencies)));
            }

            // 3. 损坏 / 非法元数据 DLL
            if (unreadableByPkgKey.TryGetValue(pkg.PackageKey, out List<UnreadableAssemblyEntry>? badList))
            {
                foreach (UnreadableAssemblyEntry bad in badList)
                {
                    findings.Add(new ConflictFinding(
                        ConflictFindingKind.UnreadableAssembly,
                        ConflictSeverity.Info,
                        Path.GetFileName(bad.FilePath),
                        bad.ErrorMessage));
                }
            }

            // 4. 字节码静态特征评估（InspectionsByPackageKey 只包含胜出且可加载的程序集；包内另有辅助 DLL 缺可选依赖时不再跳过主程序集）
            if (workspace.InspectionsByPackageKey.TryGetValue(pkg.PackageKey, out IReadOnlyList<AssemblyInspectionResult>? inspections))
            {
                EvaluateBytecodeInspections(inspections, settings, findings);
            }

            // 5. 补充兼容性规则评估（仅覆盖单点字节码未涵盖的交互项：RWH 的游戏内按名自动停用、双装与待下载兜底）
            bool packageWillNotLoad = pkg.HasManagedAssemblies && pkg.WinningAssemblies.Count == 0 && !pkg.IsPendingCloudDownload;
            if (matchedRule != null && !packageWillNotLoad)
            {
                bool dualSatisfied = !matchedRule.RequireDualLocalAndPdxInstall ||
                                     matchedRule.AssemblyNames.Any(playset.DualLocalAndPdxAssemblyNames.Contains);

                if (dualSatisfied && (!matchedRule.RequireDualLocalAndPdxInstall || emittedDualInstallRules.Add(matchedRule.RuleId)))
                {
                    ConflictSeverity ruleSev = ConflictTargets.EvaluateKnownRule(
                        matchedRule,
                        settings,
                        out string[] ruleKeys);
                    if (ruleSev != ConflictSeverity.None)
                    {
                        (ConflictScope ruleScope, IReadOnlyList<string>? ruleGroups) = ScopeOf(matchedRule, settings);
                        findings.Add(new ConflictFinding(
                            ConflictFindingKind.KnownAuditRule,
                            ruleSev,
                            matchedRule.RuleId,
                            matchedRule.AuditedVersion,
                            null,
                            ruleKeys.Length > 0 ? ruleKeys : null,
                            ruleScope,
                            ruleGroups));
                    }
                }
            }

            if (findings.Count == 0)
                continue;

            ConflictSeverity maxSeverity = findings.Max(f => f.Severity);
            if (maxSeverity == ConflictSeverity.None)
                continue;

            // 计算一键关闭经济分组按钮键集合：
            // 仅当最高等级为 Warning，且每一条 Severity >= Warning 的发现均具备可一键关闭的纯经济分组键时才开放！
            IReadOnlyList<string> oneClickKeys = ComputeSafeOneClickKeys(findings, maxSeverity);

            // 仅当真正产出了已知审计规则发现时才标注审计基准版本，避免单靠字节码或仅缺依赖/损坏 DLL 误带核对版本
            bool hasKnownRuleFinding = findings.Any(f => f.Kind == ConflictFindingKind.KnownAuditRule);
            string? auditedVer = hasKnownRuleFinding ? matchedRule?.AuditedVersion : null;
            bool newerThanAudited = auditedVer != null &&
                                    ConflictTargets.IsVersionNewerThanAudited(pkg.DisplayVersion, auditedVer);

            items.Add(new ModConflictItem(
                pkg.PackageKey,
                pkg.DisplayName,
                pkg.Source,
                pkg.PdxModId,
                pkg.DisplayVersion,
                auditedVer,
                newerThanAudited,
                maxSeverity,
                findings,
                oneClickKeys));
        }

        items.Sort((a, b) =>
        {
            int sev = b.Severity.CompareTo(a.Severity);
            return sev != 0 ? sev : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        int hardCount = items.Count(i => i.Severity == ConflictSeverity.Hard);
        int warnCount = items.Count(i => i.Severity == ConflictSeverity.Warning);
        int mitigatedCount = items.Count(i => i.Severity == ConflictSeverity.Mitigated);
        int infoCount = items.Count(i => i.Severity == ConflictSeverity.Info);

        return new ConflictReport(
            workspace,
            settings,
            items,
            hardCount,
            warnCount,
            mitigatedCount,
            infoCount);
    }

    /// <summary>
    /// 同一扫描结果按已保存设定评估比按工作设定评估更严重、且至少为条件冲突的模组（按包键对应，工作设定下没有条目视为无冲突）；
    /// 顺序沿用 <paramref name="saved"/> 的排序。冲突预检页按工作设定评估，用它点名磁盘上的设定仍冲突的模组。
    /// </summary>
    public static IReadOnlyList<ModConflictItem> WorseUnderSavedSettings(ConflictReport working, ConflictReport saved)
    {
        ArgumentNullException.ThrowIfNull(working);
        ArgumentNullException.ThrowIfNull(saved);

        Dictionary<string, ConflictSeverity> workingSeverity = working.Items
            .GroupBy(i => i.PackageKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(i => i.Severity), StringComparer.Ordinal);

        return saved.Items
            .Where(i => i.Severity >= ConflictSeverity.Warning &&
                        i.Severity > workingSeverity.GetValueOrDefault(i.PackageKey, ConflictSeverity.None))
            .ToList();
    }

    private static KnownModConflictRule? MatchPackageRule(EnabledModPackage pkg)
    {
        if (ConflictTargets.MatchKnownRule(pkg.PdxModId, null) is { } byId)
            return byId;

        foreach (WinningAssemblyEntry asm in pkg.WinningAssemblies)
        {
            if (ConflictTargets.MatchKnownRule(null, asm.AssemblyName) is { } byAsm)
                return byAsm;
        }

        return ConflictTargets.MatchKnownRule(null, pkg.DisplayName);
    }

    private static void EvaluateBytecodeInspections(
        IReadOnlyList<AssemblyInspectionResult> inspections,
        ConflictEvalSettings settings,
        List<ConflictFinding> findings)
    {
        var hasCustomSystems = inspections.Any(i => i.CustomSystemTypes.Count > 0);
        var seenSystemTargets = new HashSet<string>(StringComparer.Ordinal);
        var seenPatchTargets = new HashSet<string>(StringComparer.Ordinal);
        var seenReplacedSysPatches = new HashSet<string>(StringComparer.Ordinal);
        var seenCrossPhaseTargets = new HashSet<string>(StringComparer.Ordinal);
        var waterSimSpeedWriters = new List<string>();

        foreach (AssemblyInspectionResult ins in inspections)
        {
            // 4a. 对 MapExt 替换或修改的原版系统调用 set_Enabled（先定级剔除对宿主无害的常量 Enabled = true，再按目标系统去重归并）
            foreach (EnabledWriteEvidence ew in ins.EnabledWrites)
            {
                ProtectedSystemTarget? target = ConflictTargets.FindProtectedSystem(ew.TargetSystemTypeName);
                if (target == null)
                    continue;

                SystemConflictEvaluation eval = ConflictTargets.EvaluateSystemConflict(target, settings, ew.ConstantEnabledValue);
                if (eval.Severity == ConflictSeverity.None)
                    continue;

                if (!seenSystemTargets.Add(ew.TargetSystemTypeName))
                    continue;

                (ConflictScope scope, IReadOnlyList<string>? scopeGroups) = ScopeOf(target, settings);
                findings.Add(new ConflictFinding(
                    ConflictFindingKind.SystemEnabledWrite,
                    eval.Severity,
                    ShortenType(ew.TargetSystemTypeName),
                    ShortenMember(ew.CallerTypeAndMethod),
                    hasCustomSystems ? "WithCustomSystem" : null,
                    eval.MitigatingToggleKey != null ? [eval.MitigatingToggleKey] : null,
                    scope,
                    scopeGroups,
                    target.Role));
            }

            // 4a-2. 跨阶段将原版 UpdateGroupSystem 注册至非 Modification5 阶段（引发 ModificationBarrier5 异常）
            if (ins.CrossPhaseSchedules != null)
            {
                foreach (CrossPhaseScheduleEvidence cp in ins.CrossPhaseSchedules)
                {
                    if (!seenCrossPhaseTargets.Add(cp.TargetSystemTypeName))
                        continue;

                    findings.Add(new ConflictFinding(
                        ConflictFindingKind.CrossPhaseSystemSchedule,
                        ConflictSeverity.Hard,
                        ShortenType(cp.TargetSystemTypeName),
                        ShortenMember(cp.CallerTypeAndMethod),
                        Scope: ConflictScope.AllModes));
                }
            }

            // 4b. Harmony 补丁重叠
            foreach (HarmonyOverlapEvidence ho in ins.HarmonyOverlaps)
            {
                string displayTarget = ho.GenericArgumentTypeName != null
                    ? $"{ShortenType(ho.TargetTypeName)}<{ShortenType(ho.GenericArgumentTypeName)}>.{ho.TargetMethodName}"
                    : $"{ShortenType(ho.TargetTypeName)}.{ho.TargetMethodName}";

                if (!seenPatchTargets.Add($"{displayTarget}:{ho.Kind}"))
                    continue;

                PatchedMethodTarget? target = ConflictTargets.FindPatchedMethod(
                    ho.TargetTypeName,
                    ho.TargetMethodName,
                    ho.GenericArgumentTypeName);
                if (target == null)
                    continue;

                bool blockingOrTranspiler = ho.Kind is DetectedPatchKind.Transpiler
                    or DetectedPatchKind.PrefixBlockingCapable
                    or DetectedPatchKind.DynamicOrImperative;

                SystemConflictEvaluation eval = ConflictTargets.EvaluatePatchOverlap(target, settings, blockingOrTranspiler);
                if (eval.Severity == ConflictSeverity.None)
                    continue;

                (ConflictScope patchScope, IReadOnlyList<string>? patchGroups) = ScopeOf(target, settings);
                IReadOnlyList<string>? patchMitigatingKeys = MitigatingKeysOf(target, settings, eval);
                findings.Add(new ConflictFinding(
                    ConflictFindingKind.HarmonyPatchOverlap,
                    eval.Severity,
                    displayTarget,
                    FormatPatchKindLabel(ho.Kind),
                    ShortenMember(ho.PatchTypeAndMethod),
                    patchMitigatingKeys,
                    patchScope,
                    patchGroups));
            }

            // 4b-2. 对已被 MapExt 替换停用的原版系统生命周期方法（如 ServiceCoverageSystem.OnUpdate）挂载补丁
            if (ins.ReplacedSystemPatches != null)
            {
                foreach (PatchedReplacedSystemEvidence rp in ins.ReplacedSystemPatches)
                {
                    string displayTarget = $"{ShortenType(rp.TargetTypeName)}.{rp.TargetMethodName}";
                    if (!seenReplacedSysPatches.Add($"{displayTarget}:{rp.Kind}"))
                        continue;

                    ProtectedSystemTarget? target = ConflictTargets.FindProtectedSystem(rp.TargetTypeName);
                    if (target == null)
                        continue;

                    SystemConflictEvaluation eval = ConflictTargets.EvaluateReplacedSystemPatch(target, settings);
                    if (eval.Severity == ConflictSeverity.None)
                        continue;

                    (ConflictScope replacedScope, IReadOnlyList<string>? replacedGroups) = ScopeOf(target, settings);
                    findings.Add(new ConflictFinding(
                        ConflictFindingKind.ReplacedSystemPatch,
                        eval.Severity,
                        displayTarget,
                        FormatPatchKindLabel(rp.Kind),
                        ShortenMember(rp.PatchTypeAndMethod),
                        eval.MitigatingToggleKey != null ? [eval.MitigatingToggleKey] : null,
                        replacedScope,
                        replacedGroups));
                }
            }

            // 4c. 无参 Harmony.UnpatchAll(null) 三档上下文
            foreach (UnpatchAllNullEvidence ua in ins.UnpatchAllNullCalls)
            {
                switch (ua.Context)
                {
                    case UnpatchAllContext.InitializationOrLifecycle:
                        findings.Add(new ConflictFinding(
                            ConflictFindingKind.UnpatchAllNull,
                            ConflictSeverity.Hard,
                            ShortenMember(ua.CallerTypeAndMethod),
                            Scope: ConflictScope.AllModes));
                        break;
                    case UnpatchAllContext.UnknownCallback:
                        findings.Add(new ConflictFinding(
                            ConflictFindingKind.UnpatchAllNull,
                            ConflictSeverity.Warning,
                            ShortenMember(ua.CallerTypeAndMethod),
                            Scope: ConflictScope.AllModes));
                        break;
                    case UnpatchAllContext.DisposeOnly:
                        findings.Add(new ConflictFinding(
                            ConflictFindingKind.UnpatchAllDisposeOnly,
                            ConflictSeverity.Info,
                            ShortenMember(ua.CallerTypeAndMethod),
                            Scope: ConflictScope.AllModes));
                        break;
                }
            }

            // 4d. 同方法体内 "kMapSize" / "kTextureSize" + FieldInfo.SetValue（只有扩展地图模式改写这两个尺寸）
            foreach (StaticFieldReflectionWriteEvidence rw in ins.ReflectionFieldWrites)
            {
                ConflictSeverity sev = settings.IsExpandedMode ? ConflictSeverity.Warning : ConflictSeverity.Info;
                findings.Add(new ConflictFinding(
                    ConflictFindingKind.StaticFieldReflectionWrite,
                    sev,
                    rw.FieldNameLiteral,
                    ShortenMember(rw.CallerTypeAndMethod),
                    Scope: ConflictScope.ExpandedMapModes));
            }

            // 4e. 写原版 WaterSystem.WaterSimSpeed：同包内跨程序集按方法去重，循环后汇成一条
            if (ins.WaterSimSpeedWrites != null)
            {
                foreach (WaterSimSpeedWriteEvidence ws in ins.WaterSimSpeedWrites)
                {
                    string caller = ShortenMember(ws.CallerTypeAndMethod);
                    if (!waterSimSpeedWriters.Contains(caller, StringComparer.Ordinal))
                        waterSimSpeedWriters.Add(caller);
                }
            }
        }

        // MapExt 在各模式、各水模拟档位都会写 WaterSimSpeed，相互覆盖只影响水模拟推进节奏：定级与设定无关，一律备注
        if (waterSimSpeedWriters.Count > 0)
        {
            findings.Add(new ConflictFinding(
                ConflictFindingKind.WaterSimSpeedWrite,
                ConflictSeverity.Info,
                string.Join(", ", waterSimSpeedWriters),
                Scope: ConflictScope.AllModes));
        }
    }

    /// <summary>
    /// 目标原版系统的适用范围，按 MapExt 在哪种地图模式、哪些分组下替换或修改它：
    /// CellOnly 双重归属系统在扩展地图模式下必替、与分组无关，原版尺寸模式下只随找房与租金系统组替换。
    /// </summary>
    private static (ConflictScope Scope, IReadOnlyList<string>? GroupKeys) ScopeOf(
        ProtectedSystemTarget target,
        ConflictEvalSettings settings) => target.Role switch
    {
        ProtectedSystemRole.AlwaysReplacedCellMap => (ConflictScope.AllModes, null),
        ProtectedSystemRole.ExpandedOnlyCellMap
            or ProtectedSystemRole.ExpandedOnlyCellMapCoreHost
            or ProtectedSystemRole.ExpandedOnlyCellMapAuxHost => (ConflictScope.ExpandedMapModes, null),
        ProtectedSystemRole.CellOnlyDualRole when settings.IsExpandedMode => (ConflictScope.ExpandedMapModes, null),
        _ when target.MitigatingToggleKey != null => (ConflictScope.EconomyGroups, [target.MitigatingToggleKey]),
        _ => (ConflictScope.AllModes, null),
    };

    /// <summary>MapExt 补丁目标方法的适用范围，与 <see cref="ConflictTargets.EvaluatePatchOverlap"/> 的挂载条件一致。</summary>
    private static (ConflictScope Scope, IReadOnlyList<string>? GroupKeys) ScopeOf(
        PatchedMethodTarget target,
        ConflictEvalSettings settings) => target.Scope switch
    {
        PatchTargetScope.AllModes => (ConflictScope.AllModes, null),
        PatchTargetScope.ExpandedModesOnly => (ConflictScope.ExpandedMapModes, null),
        PatchTargetScope.EconomyDemand => (ConflictScope.EconomyGroups, [ConflictTargets.KeyDemand]),
        PatchTargetScope.EconomyHouseholdProperty => (ConflictScope.EconomyGroups, [ConflictTargets.KeyHouseholdProperty]),
        PatchTargetScope.EconomyResourceBuyer => (ConflictScope.EconomyGroups, [ConflictTargets.KeyResourceBuyer]),
        PatchTargetScope.FindTargets when settings.IsExpandedMode => (ConflictScope.ExpandedMapModes, null),
        PatchTargetScope.FindTargets => (ConflictScope.EconomyGroups, s_findTargetsEconomyKeys),
        _ => (ConflictScope.Unspecified, null),
    };

    /// <summary>
    /// 补丁重叠发现的可一键关闭分组键：<c>EvaluatePatchOverlap</c> 的返回结构只含单键，
    /// 原版尺寸模式下的 <c>PathfindSetupSystem.FindTargets</c> 由三个经济分组以或关系控制挂载，
    /// 此处补齐当前实际生效的分组键，使单条说明与整卡一键关闭按钮保持完整。
    /// </summary>
    private static IReadOnlyList<string>? MitigatingKeysOf(
        PatchedMethodTarget target,
        ConflictEvalSettings settings,
        SystemConflictEvaluation eval)
    {
        if (eval.Severity != ConflictSeverity.Warning)
            return null;

        if (eval.MitigatingToggleKey != null)
            return [eval.MitigatingToggleKey];

        if (!settings.IsExpandedMode && target.Scope == PatchTargetScope.FindTargets)
        {
            string[] activeKeys = s_findTargetsEconomyKeys.Where(settings.IsToggleEffective).ToArray();
            return activeKeys.Length > 0 ? activeKeys : null;
        }

        return null;
    }

    /// <summary>补充规则的适用范围：只在扩展地图模式定级的归扩展；可按分组规避的归经济分组；其余（如双装）归所有模式。</summary>
    private static (ConflictScope Scope, IReadOnlyList<string>? GroupKeys) ScopeOf(
        KnownModConflictRule rule,
        ConflictEvalSettings settings)
    {
        if (rule.VanillaModeSeverity == ConflictSeverity.None && rule.ExpandedModeSeverity != ConflictSeverity.None)
            return (ConflictScope.ExpandedMapModes, null);

        bool byGroups = rule.MitigatingToggleKeys.Length > 0 &&
                        (!settings.IsExpandedMode || rule.CanMitigateInExpandedModes);
        return byGroups ? (ConflictScope.EconomyGroups, rule.MitigatingToggleKeys) : (ConflictScope.AllModes, null);
    }

    private static IReadOnlyList<string> ComputeSafeOneClickKeys(List<ConflictFinding> findings, ConflictSeverity maxSeverity)
    {
        if (maxSeverity != ConflictSeverity.Warning)
            return [];

        var keys = new List<string>();
        foreach (ConflictFinding f in findings)
        {
            if (f.Severity < ConflictSeverity.Warning)
                continue;

            if (f.MitigatingToggleKeys == null || f.MitigatingToggleKeys.Count == 0)
            {
                // 存在至少一条无法单靠关闭纯经济分组消解的警告项，禁止给出一键消解按钮以免误导玩家
                return [];
            }

            foreach (string k in f.MitigatingToggleKeys)
            {
                if (!keys.Contains(k, StringComparer.Ordinal))
                    keys.Add(k);
            }
        }

        return keys;
    }

    internal static string ShortenType(string fullTypeName)
    {
        string cleaned = fullTypeName;
        int tick = cleaned.IndexOf('`');
        if (tick > 0)
            cleaned = cleaned[..tick];

        int lastDot = cleaned.LastIndexOf('.');
        return lastDot >= 0 ? cleaned[(lastDot + 1)..] : cleaned;
    }

    private static string ShortenMember(string callerTypeAndMethod)
    {
        int lastDot = callerTypeAndMethod.LastIndexOf('.');
        if (lastDot <= 0)
            return callerTypeAndMethod;

        string typePart = callerTypeAndMethod[..lastDot];
        string methodPart = callerTypeAndMethod[(lastDot + 1)..];
        return $"{ShortenType(typePart)}.{methodPart}";
    }

    private static string FormatPatchKindLabel(DetectedPatchKind kind) => kind switch
    {
        DetectedPatchKind.Transpiler => "Transpiler",
        DetectedPatchKind.PrefixBlockingCapable => "Prefix (bool)",
        DetectedPatchKind.PrefixVoid => "Prefix (void)",
        DetectedPatchKind.Postfix => "Postfix",
        DetectedPatchKind.Finalizer => "Finalizer",
        _ => "Harmony.Patch",
    };
}
