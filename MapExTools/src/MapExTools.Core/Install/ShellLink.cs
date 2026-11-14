using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace MapExTools.Core.Install;

/// <summary>快捷方式（.lnk）的各项内容。</summary>
public sealed record ShellLinkInfo(
    string TargetPath,
    string Arguments,
    string WorkingDirectory,
    string IconPath,
    int IconIndex,
    string Description);

/// <summary>
/// 用 Windows Shell 的 <c>IShellLinkW</c> 读写快捷方式（计划 2.3）。不经 <c>WScript.Shell</c>，不依赖脚本宿主。
/// ShellLink 组件的线程模型为 Both，界面线程（STA）与线程池（MTA）都能用。
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShellLink
{
    // 读出各项时的缓冲区长度（字符）：路径按长路径留足，说明文字上限 INFOTIPSIZE 为 1024
    private const int BufferLength = 1024;

    private const int StgmRead = 0;

    /// <summary>写入或覆盖快捷方式。</summary>
    /// <exception cref="COMException">Shell 写入失败。</exception>
    /// <exception cref="UnauthorizedAccessException">没有写入权限。</exception>
    public static void Save(string linkPath, ShellLinkInfo info)
    {
        ArgumentException.ThrowIfNullOrEmpty(linkPath);
        ArgumentNullException.ThrowIfNull(info);

        var link = (IShellLinkW)new CShellLink();
        try
        {
            link.SetPath(info.TargetPath);
            link.SetArguments(info.Arguments);
            link.SetWorkingDirectory(info.WorkingDirectory);
            link.SetIconLocation(info.IconPath, info.IconIndex);
            link.SetDescription(info.Description);
            ((IPersistFile)link).Save(Path.GetFullPath(linkPath), true);
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>读取快捷方式；文件不存在或不是有效的快捷方式时返回 null。</summary>
    public static ShellLinkInfo? TryRead(string linkPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(linkPath);
        if (!File.Exists(linkPath))
            return null;

        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(Path.GetFullPath(linkPath), StgmRead);

            var buffer = new StringBuilder(BufferLength);
            link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
            string target = buffer.ToString();

            buffer.Clear();
            link.GetArguments(buffer, buffer.Capacity);
            string arguments = buffer.ToString();

            buffer.Clear();
            link.GetWorkingDirectory(buffer, buffer.Capacity);
            string workingDirectory = buffer.ToString();

            buffer.Clear();
            link.GetIconLocation(buffer, buffer.Capacity, out int iconIndex);
            string iconPath = buffer.ToString();

            buffer.Clear();
            link.GetDescription(buffer, buffer.Capacity);
            string description = buffer.ToString();

            return new ShellLinkInfo(target, arguments, workingDirectory, iconPath, iconIndex, description);
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    // === COM 定义（ShObjIdl_core.h） ===

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private class CShellLink
    {
    }

    /// <summary>方法顺序须与 vtable 一致，不能调换。</summary>
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);

        void GetIDList(out IntPtr ppidl);

        void SetIDList(IntPtr pidl);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        void GetHotkey(out ushort pwHotkey);

        void SetHotkey(ushort wHotkey);

        void GetShowCmd(out int piShowCmd);

        void SetShowCmd(int iShowCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        void Resolve(IntPtr hwnd, uint fFlags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
