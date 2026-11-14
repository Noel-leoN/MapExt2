using System.ComponentModel;
using System.Runtime.Versioning;

namespace MapExTools.Core.Install;

/// <summary>桌面快捷方式的状态。</summary>
public enum ShortcutState
{
    /// <summary>没有 <c>MapExTools.lnk</c>。</summary>
    Missing,

    /// <summary>指向当前运行的这份 exe。</summary>
    PointsHere,

    /// <summary>指向另一份 MapExTools 的 exe（如旧的解压目录）。</summary>
    PointsElsewhere,

    /// <summary>有同名文件，但读不出来，或指向的不是 MapExTools 的 exe：不动它。</summary>
    Foreign,
}

/// <param name="Target">快捷方式指向的路径；<see cref="ShortcutState.Missing"/> 时为 null。</param>
public sealed record ShortcutStatus(ShortcutState State, string LinkPath, string? Target);

/// <summary>
/// MapExTools 的桌面快捷方式 <c>MapExTools.lnk</c>（计划 2.3）。文件名不随界面语言变，切换语言后仍认得出、不会重复创建。
/// 只删除、覆盖指向 MapExTools 的 exe 的那一个，同名的其他快捷方式不动。
/// </summary>
[SupportedOSPlatform("windows")]
public static class ToolShortcut
{
    public const string FileName = "MapExTools.lnk";

    /// <summary>当前用户的桌面目录；取不到时（如系统账户）为空字符串。</summary>
    public static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static ShortcutStatus Inspect(string folder, string exePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(exePath);

        string linkPath = Path.Combine(folder, FileName);
        if (!File.Exists(linkPath))
            return new ShortcutStatus(ShortcutState.Missing, linkPath, null);

        ShellLinkInfo? link = ShellLink.TryRead(linkPath);
        if (link == null || !IsToolExe(link.TargetPath))
            return new ShortcutStatus(ShortcutState.Foreign, linkPath, link?.TargetPath);

        bool here = string.Equals(Path.GetFullPath(link.TargetPath), Path.GetFullPath(exePath), StringComparison.OrdinalIgnoreCase);
        return new ShortcutStatus(here ? ShortcutState.PointsHere : ShortcutState.PointsElsewhere, linkPath, link.TargetPath);
    }

    /// <summary>
    /// 创建或改写快捷方式，指向 <paramref name="exePath"/>，图标取自 exe。exe 是 Low 完整性级别（放在 LocalLow 下）时
    /// 先改为 Medium，否则桌面图标显示空白；改不了也照常创建，只是图标可能空白。
    /// </summary>
    /// <returns>是否把 exe 的完整性级别改成了 Medium。</returns>
    /// <exception cref="InvalidOperationException">同名快捷方式不是 MapExTools 的。</exception>
    /// <exception cref="System.Runtime.InteropServices.COMException">Shell 写入失败。</exception>
    /// <exception cref="UnauthorizedAccessException">没有写入权限。</exception>
    public static bool Create(string folder, string exePath, string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentException.ThrowIfNullOrEmpty(exePath);

        ShortcutStatus status = Inspect(folder, exePath);
        if (status.State == ShortcutState.Foreign)
            throw new InvalidOperationException($"{status.LinkPath} 不是 MapExTools 的快捷方式");

        bool raised = RaiseToMediumIfLow(exePath);
        string exe = Path.GetFullPath(exePath);
        Directory.CreateDirectory(folder);
        ShellLink.Save(status.LinkPath, new ShellLinkInfo(
            exe, string.Empty, Path.GetDirectoryName(exe)!, exe, 0, description ?? string.Empty));
        return raised;
    }

    /// <summary>删除指向 MapExTools 的 exe 的快捷方式；没有或不是 MapExTools 的时返回 false。</summary>
    public static bool Delete(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);

        string linkPath = Path.Combine(folder, FileName);
        ShellLinkInfo? link = ShellLink.TryRead(linkPath);
        if (link == null || !IsToolExe(link.TargetPath))
            return false;

        File.Delete(linkPath);
        return true;
    }

    /// <summary>exe 的完整性级别低于 Medium 时改为 Medium；返回是否改过。读写标签失败时不改、返回 false。</summary>
    public static bool RaiseToMediumIfLow(string exePath)
    {
        try
        {
            if (FileIntegrity.GetLabelRid(exePath) is not int rid || rid >= FileIntegrity.MediumRid)
                return false;
            FileIntegrity.SetMedium(exePath);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    /// <summary>指向的是不是 MapExTools 的 exe：文件名以 MapExTools 开头（含改过名的副本，如 <c>MapExTools (1).exe</c>）。</summary>
    private static bool IsToolExe(string? path)
    {
        string name = Path.GetFileName(path ?? string.Empty);
        return name.StartsWith("MapExTools", StringComparison.OrdinalIgnoreCase)
               && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
