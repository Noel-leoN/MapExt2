namespace MapExTools.Core.Install;

/// <summary>MapExTools.exe 所在位置的类别（计划 §2 第 9 条）。</summary>
public enum InstallLocationKind
{
    /// <summary>常规目录，如 <c>D:\Tools\MapExTools\</c>。</summary>
    Normal,

    /// <summary>已固定到本地：<c>%LOCALAPPDATA%\Programs\MapExTools\</c>。</summary>
    Pinned,

    /// <summary>
    /// 游戏用户目录的 <c>Mods/</c> 下。游戏会扫描这里，解压时留下的压缩包会被当作资产包，工具也不该与 Mod 混放，要求移出。
    /// </summary>
    GameMods,

    /// <summary>临时目录：在压缩包里直接双击时，解压软件把 exe 放在这里，关掉后可能被清理。</summary>
    Temp,

    /// <summary>下载文件夹。</summary>
    Downloads,
}

/// <summary>判定位置用到的几个目录，都应是完整的长路径（不含 8.3 短名）；测试时可换成临时目录。</summary>
/// <param name="PinnedDir">固定位置，见 <see cref="ToolPinning.DefaultPinnedDir"/>。</param>
/// <param name="GameModsDir">游戏用户目录下的 <c>Mods</c>。</param>
/// <param name="TempDir">临时目录。</param>
/// <param name="DownloadsDir">下载文件夹；取不到时为 null。</param>
public sealed record InstallFolders(string PinnedDir, string GameModsDir, string TempDir, string? DownloadsDir);

/// <summary>exe 的位置及其类别。</summary>
public sealed record InstallLocation(string ExePath, InstallLocationKind Kind)
{
    /// <summary>是否该提供"固定到本地"：在临时目录、下载文件夹或游戏的 Mods 下运行时。</summary>
    public bool OffersPinning => Kind is InstallLocationKind.Temp or InstallLocationKind.Downloads or InstallLocationKind.GameMods;

    /// <summary>按所在目录归类。几个目录有重叠时依次以 Mods、固定位置、临时目录、下载文件夹为准。</summary>
    public static InstallLocation Classify(string exePath, InstallFolders folders)
    {
        ArgumentException.ThrowIfNullOrEmpty(exePath);
        ArgumentNullException.ThrowIfNull(folders);

        InstallLocationKind kind =
            IsUnder(exePath, folders.GameModsDir) ? InstallLocationKind.GameMods
            : IsUnder(exePath, folders.PinnedDir) ? InstallLocationKind.Pinned
            : IsUnder(exePath, folders.TempDir) ? InstallLocationKind.Temp
            : IsUnder(exePath, folders.DownloadsDir) ? InstallLocationKind.Downloads
            : InstallLocationKind.Normal;
        return new InstallLocation(Path.GetFullPath(exePath), kind);
    }

    /// <summary>
    /// <paramref name="path"/> 是否在 <paramref name="dir"/> 之下（含子目录）。不分大小写，与 Windows 文件系统一致；
    /// 按整段目录名比较，<c>C:\Temp2\a.exe</c> 不算在 <c>C:\Temp</c> 下。
    /// </summary>
    public static bool IsUnder(string path, string? dir)
    {
        if (string.IsNullOrEmpty(dir))
            return false;

        string full = Path.GetFullPath(path);
        string root = Path.GetFullPath(dir);
        if (!Path.EndsInDirectorySeparator(root))
            root += Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
