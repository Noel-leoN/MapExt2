using MapExTools.Core.Paths;

namespace MapExTools.Core.Packages;

/// <summary>一次扫描的结果。</summary>
public sealed record PackageScanResult(IReadOnlyList<CokPackageInfo> Packages, DateTime ScannedAt, TimeSpan Elapsed);

/// <summary>
/// 扫描 <c>{UserData}/Saves</c> 与 <c>{UserData}/Maps</c> 下的全部 .cok（计划 4.1）。
/// 照游戏的目录扫描规则递归进入子目录（<c>Saves/&lt;Steam ID&gt;/</c> 等），跳过以 <c>~</c> 或 <c>.</c> 开头的文件和目录
/// （<c>FileSystemDataSource.PopulateFromDirectory</c>）。单个包读不了只标在该包上，不中断扫描。
/// PDX 订阅的地图在 <c>{UserData}/.cache/Mods/</c> 下，不在扫描范围内。
/// </summary>
public static class PackageScanner
{
    /// <summary>扫描两个目录；目录不存在时跳过。</summary>
    public static PackageScanResult Scan(UserDataPaths paths, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        DateTime started = DateTime.Now;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var packages = new List<CokPackageInfo>();
        foreach ((string root, PackageFolder folder) in new[] { (paths.SavesDir, PackageFolder.Saves), (paths.MapsDir, PackageFolder.Maps) })
        {
            foreach (string file in EnumeratePackageFiles(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                packages.Add(CokPackageReader.Read(file, folder));
            }
        }

        return new PackageScanResult(MarkDuplicates(packages), started, watch.Elapsed);
    }

    /// <summary>递归列出目录下的 .cok，按路径排序。读不了的子目录跳过。</summary>
    public static IReadOnlyList<string> EnumeratePackageFiles(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        var result = new List<string>();
        var pending = new Stack<string>();
        if (Directory.Exists(root))
            pending.Push(Path.GetFullPath(root));

        while (pending.Count > 0)
        {
            string dir = pending.Pop();
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir))
                {
                    string name = Path.GetFileName(file);
                    if (!IsSkipped(name) && string.Equals(Path.GetExtension(name), CokPackageReader.PackageExtension, StringComparison.OrdinalIgnoreCase))
                        result.Add(file);
                }

                foreach (string sub in Directory.EnumerateDirectories(dir))
                {
                    if (!IsSkipped(Path.GetFileName(sub)))
                        pending.Push(sub);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 与游戏一致：这个目录读不了就跳过
            }
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// 元数据 guid 相同的包互相标出（Phase 0.3：复制出来的存档与原包 guid 相同，游戏里只会认其中一个）。
    /// </summary>
    public static IReadOnlyList<CokPackageInfo> MarkDuplicates(IReadOnlyList<CokPackageInfo> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var groups = packages
            .Where(p => p.MetadataGuid != null)
            .GroupBy(p => p.MetadataGuid!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Path).ToList(), StringComparer.Ordinal);
        if (groups.Count == 0)
            return packages;

        return packages
            .Select(p => p.MetadataGuid != null && groups.TryGetValue(p.MetadataGuid, out List<string>? paths)
                ? p with { DuplicatePaths = paths.Where(other => !string.Equals(other, p.Path, StringComparison.OrdinalIgnoreCase)).ToList() }
                : p)
            .ToList();
    }

    /// <summary>游戏跳过以 <c>~</c> 或 <c>.</c> 开头的文件和目录（含 <c>.packages</c>）。</summary>
    private static bool IsSkipped(string name) => name.StartsWith('~') || name.StartsWith('.');
}
