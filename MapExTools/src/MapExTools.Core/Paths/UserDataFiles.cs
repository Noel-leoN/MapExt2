namespace MapExTools.Core.Paths;

/// <summary>
/// 按游戏的规则列出用户数据目录下的文件：递归进入子目录，跳过以 <c>~</c> 或 <c>.</c> 开头的文件和目录
/// （<c>FileSystemDataSource.PopulateFromDirectory</c>；<c>.packages</c> 也在其中）。读不了的子目录跳过，与游戏一致。
/// </summary>
public static class UserDataFiles
{
    /// <summary>递归列出 <paramref name="root"/> 下扩展名在 <paramref name="extensions"/> 里的文件（不分大小写），按路径排序。</summary>
    public static IReadOnlyList<string> Enumerate(string root, IReadOnlyCollection<string> extensions)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(extensions);

        var wanted = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
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
                    if (!IsSkipped(name) && wanted.Contains(Path.GetExtension(name)))
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

    /// <summary>游戏跳过以 <c>~</c> 或 <c>.</c> 开头的文件和目录。</summary>
    public static bool IsSkipped(string name) => name.StartsWith('~') || name.StartsWith('.');
}
