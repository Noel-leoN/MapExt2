using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>单条冲突或备注发现的事实机理种类（供 <see cref="ConflictText"/> 三语渲染）。</summary>
public enum ConflictFindingKind
{
    /// <summary>方法对受保护原版系统调用了 <c>ComponentSystemBase.set_Enabled</c>。</summary>
    SystemEnabledWrite,

    /// <summary>对已被替换停用的原版系统生命周期方法（如 <c>OnUpdate</c>）挂载了 Harmony 补丁。</summary>
    ReplacedSystemPatch,

    /// <summary>将原版 <c>UpdateGroupSystem</c> 跨阶段注册至非 <c>Modification5</c> 阶段（引发 <c>ModificationBarrier5</c> 报错）。</summary>
    CrossPhaseSystemSchedule,

    /// <summary>对 MapExt 挂载的目标方法同时声明或应用了重叠的 Harmony 补丁。</summary>
    HarmonyPatchOverlap,

    /// <summary>在初始化或运行期回调路径中调用了无参 <c>Harmony.UnpatchAll(null)</c>。</summary>
    UnpatchAllNull,

    /// <summary>仅在 <c>IMod.OnDispose</c>（或其私有卸载辅助方法）内包含无参 <c>Harmony.UnpatchAll(null)</c>。</summary>
    UnpatchAllDisposeOnly,

    /// <summary>同一方法体内同时包含 <c>"kMapSize"</c>/<c>"kTextureSize"</c> 字符串与 <c>FieldInfo.SetValue</c>。</summary>
    StaticFieldReflectionWrite,

    /// <summary>命中审计与存量指纹规则表（<c>Arg0</c> 为 <c>RuleId</c>）。</summary>
    KnownAuditRule,

    /// <summary>在激活 Playset 中已启用，但本地尚未下载 DLL，未扫描字节码。</summary>
    PendingCloudDownload,

    /// <summary>检测到缺少前置依赖程序集（游戏启动时不会加载该模组）。</summary>
    MissingDependencies,

    /// <summary>检测到损坏或无法读取元数据的程序集文件。</summary>
    UnreadableAssembly,

    /// <summary>配套子模组 <c>EconomyEX</c> 与 <c>MapExtPDX</c> 同时启用时的自动休眠知情说明。</summary>
    CompanionAutoHibernates,
}

/// <summary>单条结构化观测事实发现。</summary>
public sealed record ConflictFinding(
    ConflictFindingKind Kind,
    ConflictSeverity Severity,
    string Arg0,
    string? Arg1 = null,
    string? Arg2 = null,
    IReadOnlyList<string>? MitigatingToggleKeys = null);

/// <summary>按模组包聚合后的单张冲突评估卡片数据。</summary>
public sealed record ModConflictItem(
    string PackageKey,
    string DisplayName,
    ModInstallSource Source,
    string? PdxModId,
    string? InstalledVersion,
    string? AuditedVersion,
    bool IsNewerThanAudited,
    ConflictSeverity Severity,
    IReadOnlyList<ConflictFinding> Findings,
    IReadOnlyList<string> OneClickDisableKeys)
{
    /// <summary>是否提供“关闭对应经济分组并保存”一键消解按钮。</summary>
    public bool CanOneClickMitigate =>
        Severity == ConflictSeverity.Warning && OneClickDisableKeys.Count > 0;
}

/// <summary>磁盘启用集与字节码解析缓存（不依赖 <c>.coc</c> 设定，供修改设定后零 I/O 瞬时重算）。</summary>
public sealed record ScannedModWorkspace(
    PlaysetScanResult Playset,
    IReadOnlyDictionary<string, IReadOnlyList<AssemblyInspectionResult>> InspectionsByPackageKey,
    int EnabledCodeModCount,
    long ScanElapsedMs,
    DateTime ScannedAtUtc);

/// <summary>结合当前 <c>MapExtPDX.coc</c> 设定生成的最终二维冲突预检报告。</summary>
public sealed record ConflictReport(
    ScannedModWorkspace Workspace,
    ConflictEvalSettings Settings,
    IReadOnlyList<ModConflictItem> Items,
    int HardConflictCount,
    int WarningCount,
    int MitigatedCount,
    int InfoCount)
{
    public bool HasHardConflicts => HardConflictCount > 0;

    public bool HasWarnings => WarningCount > 0;
}
