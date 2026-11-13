using MapExTools.Core.Paths;

namespace MapExTools.Core.Heightmaps;

/// <summary>一个高度图文件：只读了文件头，像素在选中时才解码。</summary>
/// <param name="RelativeName">相对 <c>Heightmaps</c> 目录的路径；目录外的文件（打开或拖入的）为文件名。</param>
/// <param name="Header">文件头；认不出格式或读不了时为 null。</param>
/// <param name="ReadError">读文件出错的信息。</param>
public sealed record HeightmapFileInfo(string Path, string RelativeName, long Length, DateTime LastWriteTime, ImageFileHeader? Header, string? ReadError)
{
    /// <summary>不能导入游戏的原因；认不出格式时为 null。</summary>
    public HeightmapIssues? Issues => Header?.Issues;
}

/// <summary>
/// 列出 <c>{UserData}/Heightmaps</c> 下的高度图（计划 4.2）。编辑器"导入高度图"列出的是用户数据里子路径以 <c>Heightmaps</c>
/// 开头的图片（<c>TerrainPanelSystem.GetHeightmaps</c>），所以子目录也算；扩展名与跳过规则同游戏。
/// </summary>
public static class HeightmapFiles
{
    /// <summary>扫描 Heightmaps 目录，读各文件的文件头；目录不存在时为空。</summary>
    public static IReadOnlyList<HeightmapFileInfo> Scan(UserDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        return UserDataFiles.Enumerate(paths.HeightmapsDir, HeightmapRules.Extensions)
            .Select(file => Describe(file, paths.HeightmapsDir))
            .ToList();
    }

    /// <summary>读一个文件的大小、时间与文件头；读不了时记下原因，不抛异常。</summary>
    /// <param name="root">Heightmaps 目录；文件在它下面时显示相对路径。</param>
    public static HeightmapFileInfo Describe(string path, string? root = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string full = System.IO.Path.GetFullPath(path);
        string name = System.IO.Path.GetFileName(full);
        if (root != null)
        {
            string relative = System.IO.Path.GetRelativePath(root, full);
            if (!relative.StartsWith("..", StringComparison.Ordinal) && !System.IO.Path.IsPathRooted(relative))
                name = relative;
        }

        try
        {
            var info = new FileInfo(full);
            return new HeightmapFileInfo(full, name, info.Length, info.LastWriteTime, ImageFileHeader.TryRead(full), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new HeightmapFileInfo(full, name, 0, default, null, ex.Message);
        }
    }
}
