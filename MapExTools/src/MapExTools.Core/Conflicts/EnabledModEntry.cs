namespace MapExTools.Core.Conflicts;

/// <summary>启用模组包的物理来源分类。</summary>
public enum ModInstallSource
{
    /// <summary><c>{UserData}/Mods/</c> 本地目录（对应引擎 <c>ExecutableAsset.isLocal == true</c>）。</summary>
    UserMods,

    /// <summary>Playset 中的 Paradox Mods 订阅源（<c>source == "pdx_mods"</c>，<c>isLocal == false</c>）。</summary>
    PdxMods,

    /// <summary>Playset 中的 SDK 本地打包源（<c>source == "local"</c>，位于 <c>.cache/Mods/local/</c>，<c>isLocal == false</c>）。</summary>
    PdxLocal,
}

/// <summary><c>playset_config.json</c> 中单个播放集的只读摘要（合并可选的 <c>playset_sync.json</c> 商店数字 ID）。</summary>
public sealed record PlaysetSummary(
    string Id,
    string? BackendPlaysetId,
    string Name,
    bool IsActive,
    bool IsOffdisk,
    bool IsImmutable,
    int EnabledModCount,
    int TotalModCount);

/// <summary>播放集按 ID 或名称查询的匹配结果分类。</summary>
public enum PlaysetLookupKind
{
    Matched,
    NotFound,
    Ambiguous,
}

/// <summary>按本地 ID、商店数字 ID 或名称解析目标播放集的结果。</summary>
public sealed record PlaysetLookupResult(
    PlaysetLookupKind Kind,
    PlaysetSummary? Match,
    IReadOnlyList<PlaysetSummary> Candidates);

/// <summary>仅读取 <c>playset_config.json</c> 与 <c>playset_sync.json</c> 的播放集目录结果（不扫描模组 DLL）。</summary>
public sealed record PlaysetCatalogResult(
    bool ConfigExists,
    string? ConfigError,
    string? ActivePlaysetId,
    string? ActivePlaysetName,
    IReadOnlyList<PlaysetSummary> Playsets);

/// <summary>模组包在目标播放集与本地目录合并后的加载状态分类。</summary>
public enum ModLoadState
{
    /// <summary>已启用且至少有一个托管程序集在唯一版本裁决中胜出加载。</summary>
    LoadedCodeMod,

    /// <summary>已启用且本地目录存在，但包内不含任何非内置托管程序集（地图、资产、本地化包等）。</summary>
    AssetOrNonCode,

    /// <summary>在目标播放集中处于禁用状态（<c>isEnabled == false</c>，未扫描 DLL）。</summary>
    Disabled,

    /// <summary>已启用但本地版本目录尚不存在（云端待下载或 <c>isOffdisk</c> 未保留本地文件）。</summary>
    PendingCloudDownload,

    /// <summary>包内托管程序集因缺少前置依赖程序集无法加载。</summary>
    MissingDependencies,

    /// <summary>包内所有托管程序集均被更高优先级同名副本（如本地 <c>Mods/</c> 或更高版本）覆盖。</summary>
    ShadowedDuplicate,

    /// <summary>已启用但包内程序集均损坏或无法读取 PE 元数据。</summary>
    UnreadableAssembly,
}

/// <summary>面向「模组列表」视图与 <c>--list-mods</c> 的完整单行模组信息。</summary>
public sealed record PlaysetModListItem(
    string PackageKey,
    int? LoadOrder,
    bool IsEnabled,
    ModInstallSource Source,
    string? PdxModId,
    string DisplayName,
    string? Author,
    string? DisplayVersion,
    int? PlatformVersion,
    string? LockedPlatformVersion,
    string? RequiredGameVersion,
    ulong? SizeBytes,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> WinningAssemblyNames,
    IReadOnlyList<string> MissingDependencies,
    ModLoadState LoadState,
    string? DirectoryPath);

/// <summary>无法读取或损坏的托管程序集记录。</summary>
public sealed record UnreadableAssemblyEntry(
    string FilePath,
    string PackageKey,
    string PackageDisplayName,
    string ErrorMessage);

/// <summary>经过唯一版本与前置依赖裁决后胜出的单个托管程序集。</summary>
public sealed record WinningAssemblyEntry(
    string FilePath,
    string AssemblyName,
    Version AssemblyVersion,
    EnabledModPackage Package,
    IReadOnlyList<string> ReferencedAssemblyNames)
{
    /// <summary>仅 <c>{UserData}/Mods/</c> 下的资产具有 <c>isLocal == true</c> 优先权。</summary>
    public bool IsLocal => Package.Source == ModInstallSource.UserMods;
}

/// <summary>当前启用集合中的一个模组包（订阅包、SDK 本地包或 <c>Mods/</c> 子目录）。</summary>
public sealed class EnabledModPackage
{
    public required string PackageKey { get; init; }

    public required ModInstallSource Source { get; init; }

    public string? PdxModId { get; init; }

    public required string DisplayName { get; set; }

    /// <summary>面向用户展示的模组版本号（优先取商店 <c>userModVersion</c>，否则取主程序集版本）。</summary>
    public string? DisplayVersion { get; set; }

    /// <summary>PDX 平台递增版本号（如 <c>74417_17</c> 中的 <c>17</c>）。</summary>
    public int? PlatformVersion { get; init; }

    /// <summary>包在磁盘上的根目录；若订阅项尚未下载落盘则为 null。</summary>
    public string? DirectoryPath { get; init; }

    /// <summary>在目标 Playset 中已启用，但本地存储目录尚不存在（云端待下载）。</summary>
    public bool IsPendingCloudDownload { get; init; }

    /// <summary>模组作者（取自 <c>pdx_mods_cache.json</c> 或 <c>.metadata/metadata.json</c>）。</summary>
    public string? Author { get; init; }

    /// <summary>声明兼容的游戏版本（取自 <c>pdx_mods_cache.json</c> 或 <c>.metadata/metadata.json</c>）。</summary>
    public string? RequiredGameVersion { get; init; }

    /// <summary>包体积字节数（大于 0 时有效）。</summary>
    public ulong? SizeBytes { get; init; }

    /// <summary>商店分类与系统标签（合并 <c>tags</c> 与 <c>systemTags</c>）。</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>该包内是否至少发现了一个候选托管程序集（区分代码 Mod 与纯地图/电台/图标资产包）。</summary>
    public bool HasManagedAssemblies { get; set; }

    /// <summary>在全局同名裁决中胜出、实际会被游戏加载的托管程序集。</summary>
    public List<WinningAssemblyEntry> WinningAssemblies { get; } = [];

    /// <summary>因缺少前置程序集而无法加载的依赖名称列表（仅当已定位游戏 <c>Managed/</c> 目录时填充）。</summary>
    public List<string> MissingDependencies { get; } = [];

    /// <summary>该包的所有候选程序集均在同名裁决中败给更高优先级副本（如本地 <c>Mods/</c> 覆盖了订阅版）。</summary>
    public bool IsShadowedDuplicate { get; set; }
}

/// <summary><see cref="PlaysetModScanner"/> 的完整只读扫描快照。</summary>
public sealed record PlaysetScanResult(
    string? ActivePlaysetId,
    string? ActivePlaysetName,
    bool PlaysetConfigExists,
    string? PlaysetConfigError,
    bool ManagedDirectoryAvailable,
    IReadOnlyList<EnabledModPackage> Packages,
    IReadOnlyList<WinningAssemblyEntry> WinningAssemblies,
    IReadOnlyList<UnreadableAssemblyEntry> UnreadableAssemblies,
    IReadOnlySet<string> DualLocalAndPdxAssemblyNames,
    string? TargetPlaysetId = null,
    string? TargetPlaysetName = null,
    bool IsTargetActivePlayset = true,
    bool IsTargetOffdisk = false,
    bool TargetPlaysetFound = true,
    bool IsMapExtEnabledInTarget = true,
    IReadOnlyList<PlaysetSummary>? AvailablePlaysets = null,
    IReadOnlyList<PlaysetModListItem>? AllModItems = null,
    bool IsMapExtPendingDownloadInTarget = false)
{
    /// <summary>本次实际扫描的目标播放集 ID（未显式传入时退回 <see cref="ActivePlaysetId"/>）。</summary>
    public string? EffectivePlaysetId => IsTargetActivePlayset ? (TargetPlaysetId ?? ActivePlaysetId) : TargetPlaysetId;

    /// <summary>本次实际扫描的目标播放集显示名（未显式传入时退回 <see cref="ActivePlaysetName"/>）。</summary>
    public string? EffectivePlaysetName => IsTargetActivePlayset ? (TargetPlaysetName ?? ActivePlaysetName) : TargetPlaysetName;

    public IReadOnlyList<PlaysetSummary> EffectiveAvailablePlaysets => AvailablePlaysets ?? [];

    public IReadOnlyList<PlaysetModListItem> EffectiveAllModItems => AllModItems ?? [];
}
