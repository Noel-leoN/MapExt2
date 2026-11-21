namespace MapExTools.Core.Conflicts;

/// <summary>启用模组包的物理来源分类。</summary>
public enum ModInstallSource
{
    /// <summary><c>{UserData}/Mods/</c> 本地目录（对应引擎 <c>ExecutableAsset.isLocal == true</c>）。</summary>
    UserMods,

    /// <summary>激活 Playset 中的 Paradox Mods 订阅源（<c>source == "pdx_mods"</c>，<c>isLocal == false</c>）。</summary>
    PdxMods,

    /// <summary>激活 Playset 中的 SDK 本地打包源（<c>source == "local"</c>，位于 <c>.cache/Mods/local/</c>，<c>isLocal == false</c>）。</summary>
    PdxLocal,
}

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

    /// <summary>在激活 Playset 中已启用，但本地存储目录尚不存在（云端待下载）。</summary>
    public bool IsPendingCloudDownload { get; init; }

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
    IReadOnlySet<string> DualLocalAndPdxAssemblyNames);
