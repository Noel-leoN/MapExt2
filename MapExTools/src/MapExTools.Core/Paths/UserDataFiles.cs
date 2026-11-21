namespace MapExTools.Core.Paths;

/// <summary>
/// 按游戏的规则列出用户数据目录下的文件：递归进入子目录，跳过以 <c>~</c> 或 <c>.</c> 开头的文件和目录
/// （<c>FileSystemDataSource.PopulateFromDirectory</c>；<c>.packages</c> 也在其中）。读不了的子目录跳过，与游戏一致。
/// </summary>
public static class UserDataFiles
{
    /// <summary>防止深层嵌套或复杂挂载结构无限深入的最大目录深度。</summary>
    private const int MaxDepth = 64;

    /// <summary>递归列出 <paramref name="root"/> 下扩展名在 <paramref name="extensions"/> 里的文件（不分大小写），按路径排序。</summary>
    public static IReadOnlyList<string> Enumerate(string root, IReadOnlyCollection<string> extensions)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(extensions);

        var wanted = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        var pending = new Stack<(string Path, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(root))
        {
            string fullRoot = Path.GetFullPath(root);
            if (visited.Add(ResolveRealDirectoryPath(fullRoot)))
                pending.Push((fullRoot, 0));
        }

        while (pending.Count > 0)
        {
            (string dir, int depth) = pending.Pop();
            try
            {
                foreach (string file in Directory.EnumerateFiles(dir))
                {
                    string name = Path.GetFileName(file);
                    if (!IsSkipped(name) && wanted.Contains(Path.GetExtension(name)))
                        result.Add(file);
                }

                if (depth >= MaxDepth)
                    continue;

                foreach (string sub in Directory.EnumerateDirectories(dir))
                {
                    if (IsSkipped(Path.GetFileName(sub)))
                        continue;

                    string realPath = ResolveRealDirectoryPath(sub);
                    if (visited.Add(realPath))
                        pending.Push((sub, depth + 1));
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

    /// <summary>解析 Junction／符号链接的最终目标目录用于判环；普通目录或解析失败时退回规范化全路径。</summary>
    private static string ResolveRealDirectoryPath(string dirPath)
    {
        string fallback = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dirPath));
        try
        {
            FileSystemInfo? target = new DirectoryInfo(dirPath).ResolveLinkTarget(returnFinalTarget: true);
            if (target != null)
                return Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // 链接损坏或无权限时按原路径去重
        }

        return fallback;
    }

    /// <summary>游戏跳过以 <c>~</c> 或 <c>.</c> 开头的文件和目录。</summary>
    public static bool IsSkipped(string name) => name.StartsWith('~') || name.StartsWith('.');
}
