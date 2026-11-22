using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using MapExTools.Core.Paths;
using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>
/// 只读还原指定或当前激活播放集（<c>pdx_mods</c> 与 <c>local</c>）及 <c>{UserData}/Mods/</c> 的模组集合，
/// 并严格按 <c>ExecutableAsset.GetModAssets</c> 与 <c>GetUniqueVersionAsset</c> 规则执行内置库跳过、前置依赖过滤与唯一版本胜出裁决。
///
/// <para><b>安全红线</b>：全程仅以 <c>FileShare.ReadWrite | FileShare.Delete</c> 只读读取配置与 DLL，绝不写入任何 PDX 缓存文件；
/// 严禁使用 <c>AppContext.BaseDirectory</c>（便携单文件自解压模式下指向临时目录）。</para>
/// </summary>
public static class PlaysetModScanner
{
    private static readonly string[] s_burstNativeSuffixes =
    [
        "_win_x86_64.dll",
        "_mac_x86_64.dll",
        "_mac_arm64.dll",
        "_linux_x86_64.dll",
    ];

    /// <summary>仅只读读取 <c>playset_config.json</c> 与可选 <c>playset_sync.json</c> 中的全部本地播放集列表，不扫描任何模组目录或 DLL。</summary>
    public static PlaysetCatalogResult ListPlaysets(UserDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        ParsedPlaysetConfig parsed = ParsePlaysetConfig(paths, targetPlaysetId: null);
        return new PlaysetCatalogResult(
            parsed.ConfigExists,
            parsed.ConfigError,
            parsed.ActivePlaysetId,
            parsed.ActivePlaysetName,
            parsed.Playsets);
    }

    /// <summary>
    /// 按本地播放集 ID（<c>LocalId</c>）→ Paradox 商店数字 ID（<c>BackendPlaysetId</c>）→ 显示名称（忽略大小写）依次匹配目标播放集。
    /// </summary>
    public static PlaysetLookupResult ResolvePlayset(IReadOnlyList<PlaysetSummary> playsets, string? query)
    {
        ArgumentNullException.ThrowIfNull(playsets);

        string? trimmed = TrimOrNull(query);
        if (trimmed == null || playsets.Count == 0)
            return new PlaysetLookupResult(PlaysetLookupKind.NotFound, null, []);

        // 1. 本地 ID 精确匹配
        PlaysetSummary? byLocalId = playsets.FirstOrDefault(p => string.Equals(p.Id, trimmed, StringComparison.OrdinalIgnoreCase));
        if (byLocalId != null)
            return new PlaysetLookupResult(PlaysetLookupKind.Matched, byLocalId, [byLocalId]);

        // 2. 商店后端数字 ID 精确匹配
        List<PlaysetSummary> byBackendId = playsets
            .Where(p => !string.IsNullOrEmpty(p.BackendPlaysetId) &&
                        string.Equals(p.BackendPlaysetId, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byBackendId.Count == 1)
            return new PlaysetLookupResult(PlaysetLookupKind.Matched, byBackendId[0], byBackendId);
        if (byBackendId.Count > 1)
            return new PlaysetLookupResult(PlaysetLookupKind.Ambiguous, null, byBackendId);

        // 3. 显示名称忽略大小写匹配
        List<PlaysetSummary> byName = playsets
            .Where(p => string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return byName.Count switch
        {
            1 => new PlaysetLookupResult(PlaysetLookupKind.Matched, byName[0], byName),
            > 1 => new PlaysetLookupResult(PlaysetLookupKind.Ambiguous, null, byName),
            _ => new PlaysetLookupResult(PlaysetLookupKind.NotFound, null, []),
        };
    }

    /// <summary>扫描当前用户数据目录下的启用模组、胜出程序集及目标播放集的完整模组列表。</summary>
    /// <param name="paths">用户数据目录路径集。</param>
    /// <param name="gameDir">已定位的游戏安装根目录（用于定位 <c>Cities2_Data/Managed</c>；未定位时传 null 触发 Fail-Open）。</param>
    /// <param name="targetPlaysetId">可选的目标播放集本地 ID（或商店数字 ID）；传 null 时扫描当前激活播放集。</param>
    public static PlaysetScanResult Scan(UserDataPaths paths, string? gameDir = null, string? targetPlaysetId = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap = ReadPdxModsCache(paths.PdxModsCacheFile);
        (string[] pdxRoots, string[] localRoots) = ResolveStorageRoots(paths);
        ParsedPlaysetConfig parsed = ParsePlaysetConfig(paths, targetPlaysetId);

        var packages = new List<EnabledModPackage>();
        var rawPlaysetRows = new List<RawPlaysetRow>();

        // === 1. 解析目标 Playset 中的启用项与禁用项（seenEnabledMods 独立去重，防禁用项吃掉后续同 ID 启用项） ===
        if (parsed.TargetMods.Count > 0)
        {
            var seenEnabledMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (RawModSpec spec in parsed.TargetMods)
            {
                bool isPdxMods = string.Equals(spec.Source, "pdx_mods", StringComparison.OrdinalIgnoreCase);
                bool isLocalSource = string.Equals(spec.Source, "local", StringComparison.OrdinalIgnoreCase);
                if (!isPdxMods && !isLocalSource)
                    continue;

                if (spec.IsEnabled)
                {
                    string dedupKey = $"{spec.Source}:{spec.SourceId}";
                    if (seenEnabledMods.Add(dedupKey))
                    {
                        EnabledModPackage pkg = isPdxMods
                            ? ResolvePdxSubscribedPackage(pdxRoots, spec.SourceId, spec.PreferredVersion, cacheMap)
                            : ResolvePdxLocalPackage(localRoots, spec.SourceId, spec.PreferredVersion, cacheMap);
                        packages.Add(pkg);
                        rawPlaysetRows.Add(new RawPlaysetRow(spec.LoadOrder, spec.PreferredVersion, pkg, null, IsDuplicateEnabledEntry: false));
                    }
                    else
                    {
                        // 同一播放集内重复出现的同 ID 启用条目：不重复加入冲突扫描包，列表里如实记为被遮蔽重复项
                        PlaysetModListItem dupItem = ResolveLightweightModItem(
                            spec.LoadOrder,
                            isEnabled: true,
                            ModLoadState.ShadowedDuplicate,
                            isPdxMods ? ModInstallSource.PdxMods : ModInstallSource.PdxLocal,
                            isPdxMods ? pdxRoots : localRoots,
                            spec.Source,
                            spec.SourceId,
                            spec.PreferredVersion,
                            cacheMap);
                        rawPlaysetRows.Add(new RawPlaysetRow(spec.LoadOrder, spec.PreferredVersion, null, dupItem, IsDuplicateEnabledEntry: true));
                    }
                }
                else
                {
                    // 禁用条目严格零 PE 扫描：仅合并商店/内联元数据与轻量版本目录存在性
                    PlaysetModListItem disabledItem = ResolveLightweightModItem(
                        spec.LoadOrder,
                        isEnabled: false,
                        ModLoadState.Disabled,
                        isPdxMods ? ModInstallSource.PdxMods : ModInstallSource.PdxLocal,
                        isPdxMods ? pdxRoots : localRoots,
                        spec.Source,
                        spec.SourceId,
                        spec.PreferredVersion,
                        cacheMap);
                    rawPlaysetRows.Add(new RawPlaysetRow(spec.LoadOrder, spec.PreferredVersion, null, disabledItem, IsDuplicateEnabledEntry: false));
                }
            }
        }

        // === 2. 扫描本地 {UserData}/Mods/ 目录（跳过 . 与 ~ 前缀，复用 UserDataFiles） ===
        var localPackageDlls = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var userModsPackages = new List<EnabledModPackage>();
        IReadOnlyList<string> localModDlls = UserDataFiles.Enumerate(paths.ModsDir, [".dll"]);
        if (localModDlls.Count > 0)
        {
            string normalizedModsRoot = Path.GetFullPath(paths.ModsDir);
            var folderGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var rootLooseDlls = new List<string>();

            foreach (string dllPath in localModDlls)
            {
                string rel = Path.GetRelativePath(normalizedModsRoot, dllPath);
                int sep = rel.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
                if (sep > 0)
                {
                    string folderName = rel[..sep];
                    if (!folderGroups.TryGetValue(folderName, out List<string>? list))
                    {
                        list = [];
                        folderGroups[folderName] = list;
                    }

                    list.Add(dllPath);
                }
                else
                {
                    // 根目录散落 DLL 各自独立成包，不再与同名子目录合并
                    rootLooseDlls.Add(dllPath);
                }
            }

            foreach ((string folderName, List<string> dlls) in folderGroups.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                string folderPath = Path.Combine(normalizedModsRoot, folderName);
                ResolvedMetadata folderMeta = LookupInlineMetadataOnly(folderPath);
                string pkgKey = $"usermods:{folderName}";
                var pkg = new EnabledModPackage
                {
                    PackageKey = pkgKey,
                    Source = ModInstallSource.UserMods,
                    DisplayName = !string.IsNullOrWhiteSpace(folderMeta.DisplayName) ? folderMeta.DisplayName : folderName,
                    DisplayVersion = folderMeta.UserModVersion,
                    Author = folderMeta.Author,
                    RequiredGameVersion = folderMeta.RequiredGameVersion,
                    DirectoryPath = folderPath,
                };
                packages.Add(pkg);
                userModsPackages.Add(pkg);
                localPackageDlls[pkgKey] = dlls;
            }

            foreach (string dllPath in rootLooseDlls.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(dllPath);
                string stem = Path.GetFileNameWithoutExtension(fileName);
                string pkgKey = $"usermods-file:{fileName}";
                var pkg = new EnabledModPackage
                {
                    PackageKey = pkgKey,
                    Source = ModInstallSource.UserMods,
                    DisplayName = stem,
                    DirectoryPath = normalizedModsRoot,
                };
                packages.Add(pkg);
                userModsPackages.Add(pkg);
                localPackageDlls[pkgKey] = [dllPath];
            }
        }

        // === 3. 收集托管程序集候选并执行内置库过滤、前置依赖判定与唯一胜出裁决 ===
        HashSet<string>? builtInManagedNames = TryReadManagedAssemblyNames(gameDir);
        bool managedAvailable = builtInManagedNames != null;

        var candidates = new List<CandidateAssembly>();
        var unreadable = new List<UnreadableAssemblyEntry>();

        foreach (EnabledModPackage pkg in packages)
        {
            if (pkg.IsPendingCloudDownload || string.IsNullOrEmpty(pkg.DirectoryPath))
                continue;

            IReadOnlyList<string> dllFiles = localPackageDlls.TryGetValue(pkg.PackageKey, out List<string>? preCollected)
                ? preCollected
                : UserDataFiles.Enumerate(pkg.DirectoryPath, [".dll"]);

            foreach (string dllPath in dllFiles)
            {
                string fileName = Path.GetFileName(dllPath);
                if (IsBurstNativeLibrary(fileName))
                    continue;

                string bareFileName = Path.GetFileNameWithoutExtension(fileName);
                // 对齐 ExecutableAsset.GetModAssets：存在 Managed/ 时直接跳过与引擎内置库同名的随包 DLL
                if (builtInManagedNames != null && builtInManagedNames.Contains(bareFileName))
                    continue;

                if (!TryReadManagedHeader(dllPath, pkg.PackageKey, pkg.DisplayName, unreadable, out ManagedHeader? header) || header == null)
                    continue;

                if (builtInManagedNames != null && builtInManagedNames.Contains(header.AssemblyName))
                    continue;

                pkg.HasManagedAssemblies = true;
                candidates.Add(new CandidateAssembly(
                    dllPath,
                    header.AssemblyName,
                    header.AssemblyVersion,
                    pkg,
                    header.ReferencedNames,
                    header.ImplementsIMod));

                // 若本地包或无商店缓存包尚未获得更佳显示名/版本号，用首个实现 IMod 的主程序集补全
                if (pkg.DisplayVersion == null && header.ImplementsIMod)
                {
                    pkg.DisplayVersion = FormatAssemblyVersion(header.AssemblyVersion);
                }
            }

            if (pkg.DisplayVersion == null)
            {
                CandidateAssembly? first = candidates.FirstOrDefault(c => ReferenceEquals(c.Package, pkg));
                if (first != null)
                    pkg.DisplayVersion = FormatAssemblyVersion(first.AssemblyVersion);
            }
        }

        // 记录同时在本地 Mods/ 与 PDX 订阅源出现的程序集名（供双装冲突规则检查）
        var localAsmNames = new HashSet<string>(
            candidates.Where(c => c.Package.Source == ModInstallSource.UserMods).Select(c => c.AssemblyName),
            StringComparer.OrdinalIgnoreCase);
        var dualInstalled = new HashSet<string>(
            candidates.Where(c => c.Package.Source != ModInstallSource.UserMods && localAsmNames.Contains(c.AssemblyName))
                      .Select(c => c.AssemblyName),
            StringComparer.OrdinalIgnoreCase);

        // === 4. 计算 canBeLoaded（仅当 Managed/ 目录可用时启用，否则 Fail-Open 全置 true） ===
        EvaluateLoadability(candidates, builtInManagedNames);

        // === 5. 按 ExecutableAsset.GetUniqueVersionAsset 先滤除不可加载副本，再按 isLocal → Version → Path 选唯一胜出者 ===
        var winners = new List<WinningAssemblyEntry>();
        foreach (IGrouping<string, CandidateAssembly> group in candidates.GroupBy(c => c.AssemblyName, StringComparer.OrdinalIgnoreCase))
        {
            List<CandidateAssembly> loadable = group.Where(c => c.CanBeLoaded).ToList();
            if (loadable.Count == 0)
            {
                // 同名组全部缺前置不可加载：记录缺失依赖，不进入胜出列表
                foreach (CandidateAssembly failed in group)
                {
                    foreach (string miss in failed.MissingRefs)
                    {
                        if (!failed.Package.MissingDependencies.Contains(miss, StringComparer.OrdinalIgnoreCase))
                            failed.Package.MissingDependencies.Add(miss);
                    }
                }

                continue;
            }

            CandidateAssembly winner = loadable
                .OrderByDescending(c => c.Package.Source == ModInstallSource.UserMods)
                .ThenByDescending(c => c.AssemblyVersion)
                .ThenBy(c => c.FilePath, StringComparer.OrdinalIgnoreCase)
                .First();

            var entry = new WinningAssemblyEntry(
                winner.FilePath,
                winner.AssemblyName,
                winner.AssemblyVersion,
                winner.Package,
                winner.ReferencedNames);

            winner.Package.WinningAssemblies.Add(entry);
            winners.Add(entry);
        }

        foreach (EnabledModPackage pkg in packages)
        {
            if (pkg.HasManagedAssemblies && pkg.WinningAssemblies.Count == 0 && pkg.MissingDependencies.Count == 0)
            {
                pkg.IsShadowedDuplicate = true;
            }
        }

        winners.Sort((a, b) => string.Compare(a.AssemblyName, b.AssemblyName, StringComparison.OrdinalIgnoreCase));

        // === 6. 汇总完整模组列表（目标 Playset 条目保序 + 本地 Mods/ 目录）与 MapExt 启用标记 ===
        var unreadablePkgKeys = new HashSet<string>(unreadable.Select(u => u.PackageKey), StringComparer.Ordinal);
        var allModItems = new List<PlaysetModListItem>(rawPlaysetRows.Count + userModsPackages.Count);

        foreach (RawPlaysetRow row in rawPlaysetRows)
        {
            if (row.PrebuiltItem != null)
            {
                allModItems.Add(row.PrebuiltItem);
                continue;
            }

            if (row.EnabledPackage != null)
            {
                allModItems.Add(BuildListItemFromEnabledPackage(row.EnabledPackage, row.LoadOrder, row.LockedVersion, unreadablePkgKeys));
            }
        }

        foreach (EnabledModPackage localPkg in userModsPackages)
        {
            allModItems.Add(BuildListItemFromEnabledPackage(localPkg, loadOrder: null, lockedVersion: null, unreadablePkgKeys));
        }

        bool isMapExtEnabled = packages.Any(p => p.PdxModId == ConflictTargets.MapExtPdxModId && p.MissingDependencies.Count == 0) ||
                               winners.Any(a =>
                                   string.Equals(a.AssemblyName, "MapExt2", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(a.AssemblyName, "MapExtPDX", StringComparison.OrdinalIgnoreCase));

        return new PlaysetScanResult(
            parsed.ActivePlaysetId,
            parsed.ActivePlaysetName,
            parsed.ConfigExists,
            parsed.ConfigError,
            managedAvailable,
            packages,
            winners,
            unreadable,
            dualInstalled,
            parsed.TargetPlaysetId,
            parsed.TargetPlaysetName,
            parsed.IsTargetActive,
            parsed.IsTargetOffdisk,
            parsed.TargetFound,
            isMapExtEnabled,
            parsed.Playsets,
            allModItems);
    }

    private sealed record RawModSpec(int LoadOrder, string Source, string SourceId, string? PreferredVersion, bool IsEnabled);

    private sealed record RawPlaysetRow(
        int LoadOrder,
        string? LockedVersion,
        EnabledModPackage? EnabledPackage,
        PlaysetModListItem? PrebuiltItem,
        bool IsDuplicateEnabledEntry);

    private sealed record ParsedPlaysetConfig(
        bool ConfigExists,
        string? ConfigError,
        string? ActivePlaysetId,
        string? ActivePlaysetName,
        string? TargetPlaysetId,
        string? TargetPlaysetName,
        bool IsTargetActive,
        bool IsTargetOffdisk,
        bool TargetFound,
        IReadOnlyList<PlaysetSummary> Playsets,
        IReadOnlyList<RawModSpec> TargetMods);

    private sealed record CacheModMeta(
        string Id,
        int PlatformVersion,
        string? DisplayName,
        string? UserModVersion,
        string? Author,
        string? RequiredGameVersion,
        ulong? SizeBytes,
        IReadOnlyList<string> Tags);

    private sealed record ResolvedMetadata(
        string? DisplayName,
        string? UserModVersion,
        string? Author,
        string? RequiredGameVersion,
        ulong? SizeBytes,
        IReadOnlyList<string> Tags);

    private sealed record ManagedHeader(
        string AssemblyName,
        Version AssemblyVersion,
        IReadOnlyList<string> ReferencedNames,
        bool ImplementsIMod);

    private sealed class CandidateAssembly(
        string filePath,
        string assemblyName,
        Version assemblyVersion,
        EnabledModPackage package,
        IReadOnlyList<string> referencedNames,
        bool implementsIMod)
    {
        public string FilePath { get; } = filePath;
        public string AssemblyName { get; } = assemblyName;
        public Version AssemblyVersion { get; } = assemblyVersion;
        public EnabledModPackage Package { get; } = package;
        public IReadOnlyList<string> ReferencedNames { get; } = referencedNames;
        public bool ImplementsIMod { get; } = implementsIMod;
        public bool CanBeLoaded { get; set; } = true;
        public List<string> MissingRefs { get; } = [];
    }

    private static ParsedPlaysetConfig ParsePlaysetConfig(UserDataPaths paths, string? targetPlaysetId)
    {
        bool configExists = File.Exists(paths.PlaysetConfigFile);
        string? requested = TrimOrNull(targetPlaysetId);
        if (!configExists)
        {
            return new ParsedPlaysetConfig(
                ConfigExists: false,
                ConfigError: null,
                ActivePlaysetId: null,
                ActivePlaysetName: null,
                TargetPlaysetId: requested,
                TargetPlaysetName: requested,
                IsTargetActive: requested == null,
                IsTargetOffdisk: false,
                TargetFound: requested == null,
                Playsets: [],
                TargetMods: []);
        }

        Dictionary<string, string> backendIds = ReadPlaysetSyncBackendIds(paths.PlaysetSyncFile);
        string? configError = null;
        string? activeId = null;
        string? activeName = null;
        var summaries = new List<PlaysetSummary>();
        var modsByPlaysetId = new Dictionary<string, List<RawModSpec>>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using Stream stream = OpenSharedRead(paths.PlaysetConfigFile);
            using JsonDocument doc = JsonDocument.Parse(stream);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                configError = "playset_config.json root is not a JSON object.";
            }
            else
            {
                activeId = TrimOrNull(GetOptionalString(root, "activePlaysetId"));
                if (root.TryGetProperty("playsets", out JsonElement playsetsEl) &&
                    playsetsEl.ValueKind == JsonValueKind.Array)
                {
                    var seenPlaysets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (JsonElement playset in playsetsEl.EnumerateArray())
                    {
                        if (playset.ValueKind != JsonValueKind.Object)
                            continue;

                        string? id = TrimOrNull(GetOptionalString(playset, "id"));
                        if (id == null || !seenPlaysets.Add(id))
                            continue;

                        string? name = null;
                        if (playset.TryGetProperty("presentation", out JsonElement pres) &&
                            pres.ValueKind == JsonValueKind.Object)
                        {
                            name = TrimOrNull(GetOptionalString(pres, "name"));
                        }

                        string resolvedName = name ?? id;
                        bool isActive = activeId != null && string.Equals(id, activeId, StringComparison.OrdinalIgnoreCase);
                        // 对齐 PDX SDK ModsLocalConfigurationService：激活播放集强制视为在磁盘保留模组（isOffdisk = false）
                        bool isOffdisk = !isActive && GetOptionalBool(playset, "isOffdisk");
                        bool isImmutable = GetOptionalBool(playset, "isImmutable");
                        backendIds.TryGetValue(id, out string? backendId);

                        var modSpecs = new List<RawModSpec>();
                        int enabledCount = 0;
                        if (playset.TryGetProperty("mods", out JsonElement modsEl) &&
                            modsEl.ValueKind == JsonValueKind.Array)
                        {
                            int loadOrder = 0;
                            foreach (JsonElement modEl in modsEl.EnumerateArray())
                            {
                                if (modEl.ValueKind != JsonValueKind.Object)
                                    continue;

                                string? source = TrimOrNull(GetOptionalString(modEl, "source"));
                                string? sourceId = TrimOrNull(GetOptionalString(modEl, "sourceId"));
                                if (source == null || sourceId == null)
                                    continue;

                                bool isValidSource = string.Equals(source, "pdx_mods", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(source, "local", StringComparison.OrdinalIgnoreCase);
                                if (!isValidSource)
                                    continue;

                                loadOrder++;
                                bool isEnabled = GetOptionalBool(modEl, "isEnabled");
                                if (isEnabled)
                                    enabledCount++;

                                string? preferredVersion = TrimOrNull(
                                    GetOptionalString(modEl, "sourceVersion") ?? GetOptionalString(modEl, "preferredVersion"));
                                modSpecs.Add(new RawModSpec(loadOrder, source, sourceId, preferredVersion, isEnabled));
                            }
                        }

                        if (isActive)
                            activeName = resolvedName;

                        summaries.Add(new PlaysetSummary(
                            id,
                            backendId,
                            resolvedName,
                            isActive,
                            isOffdisk,
                            isImmutable,
                            enabledCount,
                            modSpecs.Count));
                        modsByPlaysetId[id] = modSpecs;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            configError = ex.Message;
        }

        // 将激活播放集置顶，其余维持原文件顺序
        List<PlaysetSummary> orderedPlaysets = summaries
            .OrderByDescending(p => p.IsActive)
            .ToList();

        if (requested == null)
        {
            IReadOnlyList<RawModSpec> activeMods = activeId != null && modsByPlaysetId.TryGetValue(activeId, out List<RawModSpec>? found)
                ? found
                : [];
            return new ParsedPlaysetConfig(
                ConfigExists: true,
                ConfigError: configError,
                ActivePlaysetId: activeId,
                ActivePlaysetName: activeName,
                TargetPlaysetId: activeId,
                TargetPlaysetName: activeName,
                IsTargetActive: true,
                IsTargetOffdisk: false,
                TargetFound: activeId == null || activeName != null,
                Playsets: orderedPlaysets,
                TargetMods: activeMods);
        }

        PlaysetSummary? matched = orderedPlaysets.FirstOrDefault(p => string.Equals(p.Id, requested, StringComparison.OrdinalIgnoreCase))
                                  ?? orderedPlaysets.FirstOrDefault(p =>
                                      !string.IsNullOrEmpty(p.BackendPlaysetId) &&
                                      string.Equals(p.BackendPlaysetId, requested, StringComparison.OrdinalIgnoreCase));
        if (matched == null)
        {
            return new ParsedPlaysetConfig(
                ConfigExists: true,
                ConfigError: configError,
                ActivePlaysetId: activeId,
                ActivePlaysetName: activeName,
                TargetPlaysetId: requested,
                TargetPlaysetName: requested,
                IsTargetActive: false,
                IsTargetOffdisk: false,
                TargetFound: false,
                Playsets: orderedPlaysets,
                TargetMods: []);
        }

        IReadOnlyList<RawModSpec> targetMods = modsByPlaysetId.TryGetValue(matched.Id, out List<RawModSpec>? targetList)
            ? targetList
            : [];
        return new ParsedPlaysetConfig(
            ConfigExists: true,
            ConfigError: configError,
            ActivePlaysetId: activeId,
            ActivePlaysetName: activeName,
            TargetPlaysetId: matched.Id,
            TargetPlaysetName: matched.Name,
            IsTargetActive: matched.IsActive,
            IsTargetOffdisk: matched.IsOffdisk,
            TargetFound: true,
            Playsets: orderedPlaysets,
            TargetMods: targetMods);
    }

    private static Dictionary<string, string> ReadPlaysetSyncBackendIds(string syncFile)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(syncFile))
            return result;

        try
        {
            using Stream stream = OpenSharedRead(syncFile);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("playsets", out JsonElement playsetsEl) ||
                playsetsEl.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (JsonElement item in playsetsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                string? localId = TrimOrNull(GetOptionalString(item, "id"));
                if (localId == null ||
                    !item.TryGetProperty("backends", out JsonElement backendsEl) ||
                    backendsEl.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement backend in backendsEl.EnumerateArray())
                {
                    if (backend.ValueKind != JsonValueKind.Object)
                        continue;

                    string? backendSource = TrimOrNull(GetOptionalString(backend, "id"));
                    if (!string.Equals(backendSource, "pdx_mods", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? playsetId = TrimOrNull(GetOptionalString(backend, "playsetId"));
                    if (playsetId != null)
                    {
                        result[localId] = playsetId;
                        break;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // playset_sync.json 为可选增强信息，异常静默忽略
        }

        return result;
    }

    private static PlaysetModListItem BuildListItemFromEnabledPackage(
        EnabledModPackage pkg,
        int? loadOrder,
        string? lockedVersion,
        HashSet<string> unreadablePkgKeys)
    {
        ModLoadState state;
        if (pkg.IsPendingCloudDownload)
            state = ModLoadState.PendingCloudDownload;
        else if (pkg.WinningAssemblies.Count > 0)
            state = ModLoadState.LoadedCodeMod;
        else if (pkg.MissingDependencies.Count > 0)
            state = ModLoadState.MissingDependencies;
        else if (pkg.IsShadowedDuplicate)
            state = ModLoadState.ShadowedDuplicate;
        else if (unreadablePkgKeys.Contains(pkg.PackageKey))
            state = ModLoadState.UnreadableAssembly;
        else
            state = ModLoadState.AssetOrNonCode;

        string[] winningNames = pkg.WinningAssemblies.Count > 0
            ? pkg.WinningAssemblies.Select(w => w.AssemblyName).ToArray()
            : [];
        string[] missingDeps = pkg.MissingDependencies.Count > 0
            ? pkg.MissingDependencies.ToArray()
            : [];

        return new PlaysetModListItem(
            pkg.PackageKey,
            loadOrder,
            IsEnabled: true,
            pkg.Source,
            pkg.PdxModId,
            pkg.DisplayName,
            pkg.Author,
            pkg.DisplayVersion,
            pkg.PlatformVersion,
            lockedVersion,
            pkg.RequiredGameVersion,
            pkg.SizeBytes,
            pkg.Tags,
            winningNames,
            missingDeps,
            state,
            pkg.DirectoryPath);
    }

    private static PlaysetModListItem ResolveLightweightModItem(
        int loadOrder,
        bool isEnabled,
        ModLoadState state,
        ModInstallSource installSource,
        string[] roots,
        string sourceKey,
        string sourceId,
        string? preferredVersion,
        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap)
    {
        (string? folder, int? resolvedVer) = FindVersionedDirectory(roots, sourceId, preferredVersion);
        if (folder == null && installSource == ModInstallSource.PdxLocal)
        {
            foreach (string root in roots)
            {
                string direct = Path.Combine(root, sourceId);
                if (Directory.Exists(direct))
                {
                    folder = Path.GetFullPath(direct);
                    break;
                }
            }
        }

        ResolvedMetadata meta = LookupMetadata(cacheMap, sourceKey, sourceId, resolvedVer, folder);
        string fallbackName = installSource == ModInstallSource.PdxMods ? $"PDX:{sourceId}" : sourceId;
        string pkgKey = installSource == ModInstallSource.PdxMods ? $"pdx_mods:{sourceId}" : $"local:{sourceId}";

        return new PlaysetModListItem(
            pkgKey,
            loadOrder,
            isEnabled,
            installSource,
            installSource == ModInstallSource.PdxMods ? sourceId : null,
            !string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.DisplayName : fallbackName,
            meta.Author,
            meta.UserModVersion,
            resolvedVer,
            preferredVersion,
            meta.RequiredGameVersion,
            meta.SizeBytes,
            meta.Tags,
            [],
            [],
            state,
            folder);
    }

    private static (string[] PdxRoots, string[] LocalRoots) ResolveStorageRoots(UserDataPaths paths)
    {
        string[]? pdxRoots = null;
        string[]? localRoots = null;

        if (File.Exists(paths.ModsDirectoryFile))
        {
            try
            {
                using Stream stream = OpenSharedRead(paths.ModsDirectoryFile);
                using JsonDocument doc = JsonDocument.Parse(stream);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("modsSources", out JsonElement sources) &&
                    sources.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement src in sources.EnumerateArray())
                    {
                        if (src.ValueKind != JsonValueKind.Object)
                            continue;
                        string? name = GetOptionalString(src, "source");
                        string[] roots = ReadStringArray(src, "rootPaths");
                        if (roots.Length == 0)
                            continue;

                        if (string.Equals(name, "pdx_mods", StringComparison.OrdinalIgnoreCase))
                            pdxRoots = roots;
                        else if (string.Equals(name, "local", StringComparison.OrdinalIgnoreCase))
                            localRoots = roots;
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // 配置文件损坏或占用时回退默认目录
            }
        }

        if (pdxRoots == null || pdxRoots.Length == 0)
        {
            string defaultPdx = Path.Combine(paths.ModsCacheDir, "pdx_mods");
            string legacySubscribed = Path.Combine(paths.ModsCacheDir, "mods_subscribed");
            pdxRoots = Directory.Exists(defaultPdx) || !Directory.Exists(legacySubscribed)
                ? [defaultPdx]
                : [legacySubscribed];
        }

        if (localRoots == null || localRoots.Length == 0)
        {
            localRoots = [Path.Combine(paths.ModsCacheDir, "local")];
        }

        return (pdxRoots, localRoots);
    }

    private static EnabledModPackage ResolvePdxSubscribedPackage(
        string[] pdxRoots,
        string sourceId,
        string? preferredVersion,
        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap)
    {
        (string? folder, int? resolvedVer) = FindVersionedDirectory(pdxRoots, sourceId, preferredVersion);
        ResolvedMetadata meta = LookupMetadata(cacheMap, "pdx_mods", sourceId, resolvedVer, folder);

        return new EnabledModPackage
        {
            PackageKey = $"pdx_mods:{sourceId}",
            Source = ModInstallSource.PdxMods,
            PdxModId = sourceId,
            DisplayName = !string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.DisplayName : $"PDX:{sourceId}",
            DisplayVersion = meta.UserModVersion,
            PlatformVersion = resolvedVer,
            DirectoryPath = folder,
            IsPendingCloudDownload = folder == null,
            Author = meta.Author,
            RequiredGameVersion = meta.RequiredGameVersion,
            SizeBytes = meta.SizeBytes,
            Tags = meta.Tags,
        };
    }

    private static EnabledModPackage ResolvePdxLocalPackage(
        string[] localRoots,
        string sourceId,
        string? preferredVersion,
        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap)
    {
        (string? folder, int? resolvedVer) = FindVersionedDirectory(localRoots, sourceId, preferredVersion);
        if (folder == null)
        {
            foreach (string root in localRoots)
            {
                string direct = Path.Combine(root, sourceId);
                if (Directory.Exists(direct))
                {
                    folder = Path.GetFullPath(direct);
                    break;
                }
            }
        }

        ResolvedMetadata meta = LookupMetadata(cacheMap, "local", sourceId, resolvedVer, folder);

        return new EnabledModPackage
        {
            PackageKey = $"local:{sourceId}",
            Source = ModInstallSource.PdxLocal,
            DisplayName = !string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.DisplayName : sourceId,
            DisplayVersion = meta.UserModVersion,
            PlatformVersion = resolvedVer,
            DirectoryPath = folder,
            IsPendingCloudDownload = folder == null,
            Author = meta.Author,
            RequiredGameVersion = meta.RequiredGameVersion,
            SizeBytes = meta.SizeBytes,
            Tags = meta.Tags,
        };
    }

    /// <summary>
    /// 对齐 <c>ModsCache.FindModVersion</c> 与 <c>ModsAndPlaysetsCache.UpdateModsCache</c>：
    /// 指定版本非空时只查找 <c>{sourceId}_{preferredVersion}</c>，目录缺失则返回 <c>(null, 版本)</c> 按待下载处理，不回退其他版本；
    /// 指定版本为空时枚举 <c>{sourceId}_*</c> 并按后缀整数取最大版本。
    /// </summary>
    private static (string? DirectoryPath, int? PlatformVersion) FindVersionedDirectory(
        string[] roots,
        string sourceId,
        string? preferredVersion)
    {
        if (!string.IsNullOrWhiteSpace(preferredVersion))
        {
            string trimmed = preferredVersion.Trim();
            int? parsed = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v
                : null;
            string targetName = $"{sourceId}_{trimmed}";
            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                string candidate = Path.Combine(root, targetName);
                if (Directory.Exists(candidate))
                {
                    return (Path.GetFullPath(candidate), parsed);
                }
            }

            // PDX SDK 指定版本不存在时不回退到其他版本目录，按云端待下载处理
            return (null, parsed);
        }

        string prefix = sourceId + "_";
        string? bestPath = null;
        int bestVersion = int.MinValue;

        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            try
            {
                foreach (string subDir in Directory.EnumerateDirectories(root, prefix + "*"))
                {
                    string dirName = Path.GetFileName(subDir);
                    if (!dirName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string suffix = dirName[prefix.Length..];
                    if (int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int ver) && ver > bestVersion)
                    {
                        bestVersion = ver;
                        bestPath = Path.GetFullPath(subDir);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 忽略不可读根目录
            }
        }

        return bestPath != null ? (bestPath, bestVersion) : (null, null);
    }

    private static ResolvedMetadata LookupMetadata(
        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap,
        string source,
        string id,
        int? platformVersion,
        string? folderPath)
    {
        string? displayName = null;
        string? userVersion = null;
        string? author = null;
        string? requiredGameVersion = null;
        ulong? sizeBytes = null;
        IReadOnlyList<string> tags = [];

        if (cacheMap.TryGetValue((source.ToLowerInvariant(), id), out List<CacheModMeta>? entries) && entries.Count > 0)
        {
            CacheModMeta chosen = platformVersion.HasValue
                ? entries.FirstOrDefault(e => e.PlatformVersion == platformVersion.Value) ?? entries[^1]
                : entries[^1];
            displayName = chosen.DisplayName;
            userVersion = chosen.UserModVersion;
            author = chosen.Author;
            requiredGameVersion = chosen.RequiredGameVersion;
            sizeBytes = chosen.SizeBytes;
            tags = chosen.Tags;
        }

        // 兼容包内 .metadata/metadata.json（如 SDK local 包或离线测试夹具）
        if (folderPath != null &&
            (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(userVersion) || string.IsNullOrWhiteSpace(author)))
        {
            ResolvedMetadata inline = LookupInlineMetadataOnly(folderPath);
            displayName ??= inline.DisplayName;
            userVersion ??= inline.UserModVersion;
            author ??= inline.Author;
            requiredGameVersion ??= inline.RequiredGameVersion;
        }

        return new ResolvedMetadata(displayName, userVersion, author, requiredGameVersion, sizeBytes, tags);
    }

    private static ResolvedMetadata LookupInlineMetadataOnly(string folderPath)
    {
        string inlineMeta = Path.Combine(folderPath, ".metadata", "metadata.json");
        if (!File.Exists(inlineMeta))
            return new ResolvedMetadata(null, null, null, null, null, []);

        try
        {
            using Stream s = OpenSharedRead(inlineMeta);
            using JsonDocument doc = JsonDocument.Parse(s);
            JsonElement root = doc.RootElement;
            string? displayName = TrimOrNull(GetOptionalString(root, "displayName") ?? GetOptionalString(root, "DisplayName"));
            string? userVersion = TrimOrNull(
                GetOptionalString(root, "userModVersion")
                ?? GetOptionalString(root, "ModVersion")
                ?? GetOptionalString(root, "version"));
            string? author = TrimOrNull(GetOptionalString(root, "author") ?? GetOptionalString(root, "Author"));
            string? reqGame = TrimOrNull(GetOptionalString(root, "requiredGameVersion") ?? GetOptionalString(root, "RequiredGameVersion"));
            return new ResolvedMetadata(displayName, userVersion, author, reqGame, null, []);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ResolvedMetadata(null, null, null, null, null, []);
        }
    }

    private static Dictionary<(string Source, string Id), List<CacheModMeta>> ReadPdxModsCache(string cacheFile)
    {
        var map = new Dictionary<(string Source, string Id), List<CacheModMeta>>();
        if (!File.Exists(cacheFile))
            return map;

        try
        {
            using Stream stream = OpenSharedRead(cacheFile);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("items", out JsonElement items) ||
                items.ValueKind != JsonValueKind.Array)
                return map;

            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                string? source = TrimOrNull(GetOptionalString(item, "source"));
                string? id = TrimOrNull(GetOptionalString(item, "id"));
                if (source == null || id == null)
                    continue;

                string? verStr = GetOptionalString(item, "version");
                int platformVer = int.TryParse(verStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pv) ? pv : 0;
                string? displayName = TrimOrNull(GetOptionalString(item, "displayName"));
                string? userModVer = TrimOrNull(GetOptionalString(item, "userModVersion"));
                string? author = TrimOrNull(GetOptionalString(item, "author"));
                string? reqGameVer = TrimOrNull(GetOptionalString(item, "requiredGameVersion"));
                ulong? sizeBytes = GetOptionalPositiveUInt64(item, "size");
                IReadOnlyList<string> tags = ReadModTagNames(item);

                var key = (source.ToLowerInvariant(), id);
                if (!map.TryGetValue(key, out List<CacheModMeta>? list))
                {
                    list = [];
                    map[key] = list;
                }

                list.Add(new CacheModMeta(id, platformVer, displayName, userModVer, author, reqGameVer, sizeBytes, tags));
            }

            foreach (List<CacheModMeta> list in map.Values)
            {
                list.Sort((a, b) => a.PlatformVersion.CompareTo(b.PlatformVersion));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // 缓存读取失败不阻断扫描
        }

        return map;
    }

    private static IReadOnlyList<string> ReadModTagNames(JsonElement item)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        CollectTagArray(item, "tags", seen, list);
        CollectTagArray(item, "systemTags", seen, list);

        return list.Count > 0 ? list : [];

        static void CollectTagArray(JsonElement obj, string propName, HashSet<string> seenSet, List<string> target)
        {
            if (!obj.TryGetProperty(propName, out JsonElement arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement el in arr.EnumerateArray())
            {
                string? name = el.ValueKind switch
                {
                    JsonValueKind.Object => TrimOrNull(GetOptionalString(el, "displayName") ?? GetOptionalString(el, "id")),
                    JsonValueKind.String => TrimOrNull(el.GetString()),
                    _ => null,
                };

                if (name != null && seenSet.Add(name))
                    target.Add(name);
            }
        }
    }

    private static HashSet<string>? TryReadManagedAssemblyNames(string? gameDir)
    {
        if (string.IsNullOrWhiteSpace(gameDir))
            return null;

        string managedDir = Path.Combine(gameDir, "Cities2_Data", "Managed");
        if (!Directory.Exists(managedDir))
            return null;

        try
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string dll in Directory.EnumerateFiles(managedDir, "*.dll"))
            {
                names.Add(Path.GetFileNameWithoutExtension(dll));
            }

            return names.Count > 0 ? names : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void EvaluateLoadability(List<CandidateAssembly> candidates, HashSet<string>? builtInManagedNames)
    {
        if (builtInManagedNames == null)
        {
            // Fail-Open：未定位游戏目录时不误剔除引用引擎库的正常 Mod
            foreach (CandidateAssembly c in candidates)
                c.CanBeLoaded = true;
            return;
        }

        // 不动点收敛：若某前置 Mod 自身缺依赖无法加载，引用它的下游也无法加载
        bool changed = true;
        while (changed)
        {
            changed = false;
            var availableModNames = new HashSet<string>(
                candidates.Where(c => c.CanBeLoaded).Select(c => c.AssemblyName),
                StringComparer.OrdinalIgnoreCase);

            foreach (CandidateAssembly c in candidates)
            {
                if (!c.CanBeLoaded)
                    continue;

                c.MissingRefs.Clear();
                foreach (string refName in c.ReferencedNames)
                {
                    if (!builtInManagedNames.Contains(refName) && !availableModNames.Contains(refName))
                    {
                        c.MissingRefs.Add(refName);
                    }
                }

                if (c.MissingRefs.Count > 0)
                {
                    c.CanBeLoaded = false;
                    changed = true;
                }
            }
        }
    }

    private static bool TryReadManagedHeader(
        string dllPath,
        string packageKey,
        string packageName,
        List<UnreadableAssemblyEntry> unreadable,
        out ManagedHeader? header)
    {
        header = null;
        try
        {
            using Stream stream = OpenSharedRead(dllPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
                return false;

            MetadataReader md = peReader.GetMetadataReader();
            if (!md.IsAssembly)
                return false;

            AssemblyDefinition asmDef = md.GetAssemblyDefinition();
            string asmName = md.GetString(asmDef.Name);
            if (string.IsNullOrWhiteSpace(asmName))
                return false;

            var refs = new List<string>(md.AssemblyReferences.Count);
            foreach (AssemblyReferenceHandle refHandle in md.AssemblyReferences)
            {
                AssemblyReference asmRef = md.GetAssemblyReference(refHandle);
                string refName = md.GetString(asmRef.Name);
                if (!string.IsNullOrEmpty(refName))
                    refs.Add(refName);
            }

            bool implementsIMod = CheckImplementsIMod(md);
            header = new ManagedHeader(asmName, NormalizeEcmaVersion(asmDef.Version), refs, implementsIMod);
            return true;
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            unreadable.Add(new UnreadableAssemblyEntry(dllPath, packageKey, packageName, ex.Message));
            return false;
        }
    }

    internal static bool CheckImplementsIMod(MetadataReader md)
    {
        foreach (TypeDefinitionHandle typeHandle in md.TypeDefinitions)
        {
            TypeDefinition typeDef = md.GetTypeDefinition(typeHandle);
            foreach (InterfaceImplementationHandle ifaceHandle in typeDef.GetInterfaceImplementations())
            {
                InterfaceImplementation iface = md.GetInterfaceImplementation(ifaceHandle);
                if (GetEntityFullTypeName(md, iface.Interface) == "Game.Modding.IMod")
                    return true;
            }
        }

        return false;
    }

    internal static string? GetEntityFullTypeName(MetadataReader md, EntityHandle handle)
    {
        if (handle.IsNil)
            return null;

        switch (handle.Kind)
        {
            case HandleKind.TypeReference:
            {
                TypeReference tr = md.GetTypeReference((TypeReferenceHandle)handle);
                string name = md.GetString(tr.Name);
                if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
                {
                    string? parent = GetEntityFullTypeName(md, tr.ResolutionScope);
                    return parent != null ? $"{parent}+{name}" : name;
                }

                string ns = md.GetString(tr.Namespace);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            case HandleKind.TypeDefinition:
            {
                TypeDefinition td = md.GetTypeDefinition((TypeDefinitionHandle)handle);
                string name = md.GetString(td.Name);
                TypeDefinitionHandle declaring = td.GetDeclaringType();
                if (!declaring.IsNil)
                {
                    string? parent = GetEntityFullTypeName(md, declaring);
                    return parent != null ? $"{parent}+{name}" : name;
                }

                string ns = md.GetString(td.Namespace);
                return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
            }
            default:
                return null;
        }
    }

    internal static bool IsBurstNativeLibrary(string fileName)
    {
        foreach (string suffix in s_burstNativeSuffixes)
        {
            if (fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static Stream OpenSharedRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static Version NormalizeEcmaVersion(Version v)
    {
        // ECMA-335 Assembly 表以 uint16 存储四段版本号，未指定的 -1 写入后会变为 65535
        int build = v.Build is < 0 or ushort.MaxValue ? 0 : v.Build;
        int rev = v.Revision is < 0 or ushort.MaxValue ? 0 : v.Revision;
        return new Version(Math.Max(0, v.Major), Math.Max(0, v.Minor), build, rev);
    }

    private static string FormatAssemblyVersion(Version v) =>
        v.Revision > 0 && v.Revision != ushort.MaxValue ? v.ToString(4) : v.Build >= 0 ? v.ToString(3) : v.ToString();

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetOptionalString(JsonElement obj, string propName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out JsonElement prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null,
        };
    }

    private static ulong? GetOptionalPositiveUInt64(JsonElement obj, string propName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out JsonElement prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetUInt64(out ulong n) && n > 0 => n,
            JsonValueKind.String when ulong.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong s) && s > 0 => s,
            _ => null,
        };
    }

    private static bool GetOptionalBool(JsonElement obj, string propName) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(propName, out JsonElement prop) && prop.ValueKind == JsonValueKind.True;

    private static string[] ReadStringArray(JsonElement obj, string propName)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(propName, out JsonElement prop) || prop.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<string>();
        foreach (JsonElement item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                result.Add(s);
        }

        return result.ToArray();
    }
}
