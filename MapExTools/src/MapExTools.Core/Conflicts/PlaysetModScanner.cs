using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using MapExTools.Core.Paths;

namespace MapExTools.Core.Conflicts;

/// <summary>
/// 只读还原游戏当前会加载的模组集合：合并激活 Playset（<c>pdx_mods</c> 与 <c>local</c>）及 <c>{UserData}/Mods/</c>，
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

    /// <summary>扫描当前用户数据目录下的启用模组与胜出程序集。</summary>
    /// <param name="paths">用户数据目录路径集。</param>
    /// <param name="gameDir">已定位的游戏安装根目录（用于定位 <c>Cities2_Data/Managed</c>；未定位时传 null 触发 Fail-Open）。</param>
    public static PlaysetScanResult Scan(UserDataPaths paths, string? gameDir = null)
    {
        ArgumentNullException.ThrowIfNull(paths);

        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap = ReadPdxModsCache(paths.PdxModsCacheFile);
        (string[] pdxRoots, string[] localRoots) = ResolveStorageRoots(paths);

        string? activePlaysetId = null;
        string? activePlaysetName = null;
        bool playsetExists = File.Exists(paths.PlaysetConfigFile);
        string? playsetError = null;
        var packages = new List<EnabledModPackage>();

        // === 1. 解析激活 Playset 中的启用项 ===
        if (playsetExists)
        {
            try
            {
                using Stream stream = OpenSharedRead(paths.PlaysetConfigFile);
                using JsonDocument doc = JsonDocument.Parse(stream);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    playsetError = "playset_config.json root is not a JSON object.";
                }
                else
                {
                    activePlaysetId = GetOptionalString(root, "activePlaysetId");
                    if (!string.IsNullOrEmpty(activePlaysetId) &&
                        root.TryGetProperty("playsets", out JsonElement playsetsEl) &&
                        playsetsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement playset in playsetsEl.EnumerateArray())
                        {
                            if (playset.ValueKind != JsonValueKind.Object)
                                continue;

                            string? id = GetOptionalString(playset, "id");
                            if (!string.Equals(id, activePlaysetId, StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (playset.TryGetProperty("presentation", out JsonElement pres) &&
                                pres.ValueKind == JsonValueKind.Object)
                            {
                                activePlaysetName = GetOptionalString(pres, "name");
                            }

                            activePlaysetName = string.IsNullOrWhiteSpace(activePlaysetName) ? activePlaysetId : activePlaysetName;

                            if (playset.TryGetProperty("mods", out JsonElement modsEl) &&
                                modsEl.ValueKind == JsonValueKind.Array)
                            {
                                var seenPlaysetMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                foreach (JsonElement modEl in modsEl.EnumerateArray())
                                {
                                    if (modEl.ValueKind != JsonValueKind.Object || !GetOptionalBool(modEl, "isEnabled"))
                                        continue;

                                    string? source = GetOptionalString(modEl, "source");
                                    string? sourceId = GetOptionalString(modEl, "sourceId");
                                    if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(sourceId))
                                        continue;

                                    string dedupKey = $"{source}:{sourceId}";
                                    if (!seenPlaysetMods.Add(dedupKey))
                                        continue;

                                    string? preferredVersion = GetOptionalString(modEl, "sourceVersion")
                                                               ?? GetOptionalString(modEl, "preferredVersion");

                                    if (string.Equals(source, "pdx_mods", StringComparison.OrdinalIgnoreCase))
                                    {
                                        packages.Add(ResolvePdxSubscribedPackage(pdxRoots, sourceId, preferredVersion, cacheMap));
                                    }
                                    else if (string.Equals(source, "local", StringComparison.OrdinalIgnoreCase))
                                    {
                                        packages.Add(ResolvePdxLocalPackage(localRoots, sourceId, preferredVersion, cacheMap));
                                    }
                                }
                            }

                            break;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                playsetError = ex.Message;
            }
        }

        // === 2. 扫描本地 {UserData}/Mods/ 目录（跳过 . 与 ~ 前缀，复用 UserDataFiles） ===
        var localPackageDlls = new Dictionary<string, List<string>>(StringComparer.Ordinal);
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
                string pkgKey = $"usermods:{folderName}";
                packages.Add(new EnabledModPackage
                {
                    PackageKey = pkgKey,
                    Source = ModInstallSource.UserMods,
                    DisplayName = folderName,
                    DirectoryPath = Path.Combine(normalizedModsRoot, folderName),
                });
                localPackageDlls[pkgKey] = dlls;
            }

            foreach (string dllPath in rootLooseDlls.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileName(dllPath);
                string stem = Path.GetFileNameWithoutExtension(fileName);
                string pkgKey = $"usermods-file:{fileName}";
                packages.Add(new EnabledModPackage
                {
                    PackageKey = pkgKey,
                    Source = ModInstallSource.UserMods,
                    DisplayName = stem,
                    DirectoryPath = normalizedModsRoot,
                });
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

        return new PlaysetScanResult(
            activePlaysetId,
            activePlaysetName,
            playsetExists,
            playsetError,
            managedAvailable,
            packages,
            winners,
            unreadable,
            dualInstalled);
    }

    private sealed record CacheModMeta(string Id, int PlatformVersion, string? DisplayName, string? UserModVersion);

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
        (string? metaName, string? metaUserVer) = LookupMetadata(cacheMap, "pdx_mods", sourceId, resolvedVer, folder);

        return new EnabledModPackage
        {
            PackageKey = $"pdx_mods:{sourceId}",
            Source = ModInstallSource.PdxMods,
            PdxModId = sourceId,
            DisplayName = !string.IsNullOrWhiteSpace(metaName) ? metaName : $"PDX:{sourceId}",
            DisplayVersion = metaUserVer,
            PlatformVersion = resolvedVer,
            DirectoryPath = folder,
            IsPendingCloudDownload = folder == null,
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

        (string? metaName, string? metaUserVer) = LookupMetadata(cacheMap, "local", sourceId, resolvedVer, folder);

        return new EnabledModPackage
        {
            PackageKey = $"local:{sourceId}",
            Source = ModInstallSource.PdxLocal,
            DisplayName = !string.IsNullOrWhiteSpace(metaName) ? metaName : sourceId,
            DisplayVersion = metaUserVer,
            PlatformVersion = resolvedVer,
            DirectoryPath = folder,
            IsPendingCloudDownload = folder == null,
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

    private static (string? DisplayName, string? UserModVersion) LookupMetadata(
        Dictionary<(string Source, string Id), List<CacheModMeta>> cacheMap,
        string source,
        string id,
        int? platformVersion,
        string? folderPath)
    {
        string? displayName = null;
        string? userVersion = null;

        if (cacheMap.TryGetValue((source.ToLowerInvariant(), id), out List<CacheModMeta>? entries) && entries.Count > 0)
        {
            CacheModMeta chosen = platformVersion.HasValue
                ? entries.FirstOrDefault(e => e.PlatformVersion == platformVersion.Value) ?? entries[^1]
                : entries[^1];
            displayName = chosen.DisplayName;
            userVersion = chosen.UserModVersion;
        }

        // 兼容包内 .metadata/metadata.json（如 SDK local 包或离线测试夹具）
        if (folderPath != null && (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(userVersion)))
        {
            string inlineMeta = Path.Combine(folderPath, ".metadata", "metadata.json");
            if (File.Exists(inlineMeta))
            {
                try
                {
                    using Stream s = OpenSharedRead(inlineMeta);
                    using JsonDocument doc = JsonDocument.Parse(s);
                    JsonElement root = doc.RootElement;
                    displayName ??= GetOptionalString(root, "displayName") ?? GetOptionalString(root, "DisplayName");
                    userVersion ??= GetOptionalString(root, "userModVersion")
                                    ?? GetOptionalString(root, "ModVersion")
                                    ?? GetOptionalString(root, "version");
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    // 忽略损坏的内联元数据
                }
            }
        }

        return (displayName, userVersion);
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
                string? source = GetOptionalString(item, "source");
                string? id = GetOptionalString(item, "id");
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(id))
                    continue;

                string? verStr = GetOptionalString(item, "version");
                int platformVer = int.TryParse(verStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pv) ? pv : 0;
                string? displayName = GetOptionalString(item, "displayName");
                string? userModVer = GetOptionalString(item, "userModVersion");

                var key = (source.ToLowerInvariant(), id);
                if (!map.TryGetValue(key, out List<CacheModMeta>? list))
                {
                    list = [];
                    map[key] = list;
                }

                list.Add(new CacheModMeta(id, platformVer, displayName, string.IsNullOrWhiteSpace(userModVer) ? null : userModVer));
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
