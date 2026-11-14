using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace MapExTools.Core.Install;

/// <summary>取判定运行位置要用的系统目录（下载文件夹、临时目录），并统一成长路径。</summary>
[SupportedOSPlatform("windows")]
public static class ShellFolders
{
    // FOLDERID_Downloads（KnownFolders.h）；Environment.SpecialFolder 里没有下载文件夹
    private static readonly Guid s_downloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>当前用户的下载文件夹（跟随玩家在资源管理器里改过的位置）；取不到时返回 null。</summary>
    public static string? GetDownloads()
    {
        int hr = SHGetKnownFolderPath(s_downloadsId, 0, IntPtr.Zero, out string? path);
        return hr == 0 && !string.IsNullOrEmpty(path) ? GetLongPath(path) : null;
    }

    /// <summary>
    /// 展开 8.3 短名：用户名较长时 <c>TEMP</c> 常是 <c>C:\Users\ABCDEF~1\…</c> 这样的短路径，
    /// 而 exe 的路径是长路径，不展开就比不上。目录不存在或展开失败时原样返回完整路径。
    /// </summary>
    public static string GetLongPath(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string full = Path.GetFullPath(path);
        var buffer = new StringBuilder(1024);
        uint length = GetLongPathNameW(full, buffer, (uint)buffer.Capacity);
        if (length > buffer.Capacity)
        {
            buffer.EnsureCapacity((int)length);
            length = GetLongPathNameW(full, buffer, (uint)buffer.Capacity);
        }

        return length == 0 || length > buffer.Capacity ? full : buffer.ToString();
    }

    /// <summary>本机的判定目录：固定位置、游戏的 Mods、临时目录、下载文件夹。</summary>
    public static InstallFolders Detect(string gameModsDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(gameModsDir);
        return new InstallFolders(
            GetLongPath(ToolPinning.DefaultPinnedDir),
            GetLongPath(gameModsDir),
            GetLongPath(Path.GetTempPath()),
            GetDownloads());
    }

    // === Win32 ===

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out string? ppszPath);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetLongPathNameW(string lpszShortPath, StringBuilder lpszLongPath, uint cchBuffer);
}
