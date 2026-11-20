using MapExtPDX.MapExt.Core;

namespace MapExTools.Core.Conflicts;

/// <summary>单条冲突或备注发现的事实机理种类（供 <see cref="ConflictText"/> 三语渲染）。</summary>
public enum ConflictFindingKind
{
    /// <summary>方法对 MapExt 替换或修改的原版系统调用了 <c>ComponentSystemBase.set_Enabled</c>。</summary>
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

    /// <summary>写原版 <c>WaterSystem.WaterSimSpeed</c>（MapExt 也会写，只作备注；<c>Arg0</c> 为逗号分隔的调用方法）。</summary>
    WaterSimSpeedWrite,

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

/// <summary>发现的适用范围：在哪种地图模式、哪些设定下出现，界面据此在条目前标注。</summary>
public enum ConflictScope
{
    /// <summary>不标注（待下载、缺前置、无法读取等诊断项）。</summary>
    Unspecified,

    /// <summary>只在扩展地图模式出现，与经济分组开关无关。</summary>
    ExpandedMapModes,

    /// <summary>开启对应经济分组时出现，与地图模式无关（分组见 <see cref="ConflictFinding.ScopeGroupKeys"/>）。</summary>
    EconomyGroups,

    /// <summary>任何地图模式、任何分组设定下都会出现。</summary>
    AllModes,
}

/// <summary>单条结构化观测事实发现。</summary>
/// <param name="Scope">适用范围，由分析器按 MapExt 介入该目标的模式与分组推出。</param>
/// <param name="ScopeGroupKeys"><see cref="ConflictScope.EconomyGroups"/> 时相关的经济分组键。</param>
/// <param name="SystemRole"><see cref="ConflictFindingKind.SystemEnabledWrite"/> 时目标系统在共用清单中的角色（区分 MapExt 替换还是修改它）。</param>
public sealed record ConflictFinding(
    ConflictFindingKind Kind,
    ConflictSeverity Severity,
    string Arg0,
    string? Arg1 = null,
    string? Arg2 = null,
    IReadOnlyList<string>? MitigatingToggleKeys = null,
    ConflictScope Scope = ConflictScope.Unspecified,
    IReadOnlyList<string>? ScopeGroupKeys = null,
    ProtectedSystemRole? SystemRole = null);

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
