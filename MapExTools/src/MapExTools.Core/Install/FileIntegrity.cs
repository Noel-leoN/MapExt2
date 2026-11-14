using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MapExTools.Core.Install;

/// <summary>
/// 文件的强制完整性标签（Mandatory Label）。用户目录 <c>AppData\LocalLow</c> 带可继承的 Low 标签，
/// 放在那里的 exe 也是 Low；Windows Shell 不从 Low 完整性级别的文件提取快捷方式图标，桌面上会显示空白图标。
/// 普通（Medium）进程不需要管理员就能把自己能写的文件改为 Medium（可行性调研 F11，2026-11-04 本机实测）。
/// </summary>
[SupportedOSPlatform("windows")]
public static class FileIntegrity
{
    /// <summary>Low 完整性级别的 RID（S-1-16-4096）。</summary>
    public const int LowRid = 0x1000;

    /// <summary>Medium 完整性级别的 RID（S-1-16-8192），文件没有标签时按它处理。</summary>
    public const int MediumRid = 0x2000;

    private const int SeFileObject = 1;
    private const uint LabelSecurityInformation = 0x00000010;
    private const uint SddlRevision1 = 1;

    /// <summary>
    /// 读文件的完整性标签，返回级别的 RID；没有标签（显式的或继承的都没有）时返回 null，系统按 Medium 处理。
    /// </summary>
    /// <exception cref="Win32Exception">读取失败（文件不存在、没有权限等）。</exception>
    public static int? GetLabelRid(string path) => ParseLabelRid(GetLabelSddl(path));

    /// <summary>文件标签部分的 SDDL，如 <c>S:(ML;ID;NW;;;LW)</c>（<c>ID</c> 表示继承来的）；没有标签时为空字符串。</summary>
    /// <exception cref="Win32Exception">读取失败（文件不存在、没有权限等）。</exception>
    public static string GetLabelSddl(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        uint error = GetNamedSecurityInfoW(Path.GetFullPath(path), SeFileObject, LabelSecurityInformation,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out _, out IntPtr descriptor);
        if (error != 0)
            throw new Win32Exception((int)error);

        try
        {
            if (!ConvertSecurityDescriptorToStringSecurityDescriptorW(descriptor, SddlRevision1, LabelSecurityInformation,
                    out IntPtr text, out _))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            try
            {
                return Marshal.PtrToStringUni(text) ?? string.Empty;
            }
            finally
            {
                LocalFree(text);
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    /// <summary>把文件的完整性标签设为 Medium（策略 No-Write-Up，与 <c>icacls /setintegritylevel Medium</c> 相同）。</summary>
    /// <exception cref="Win32Exception">设置失败。</exception>
    public static void SetMedium(string path) => SetLabel(path, MediumRid);

    /// <summary>
    /// 设置完整性标签（策略 No-Write-Up）。<paramref name="inheritable"/> 为 true 时目录下新建的文件和子目录继承这个标签，
    /// 与 LocalLow 目录本身的标签相同；测试用它模拟 LocalLow。
    /// </summary>
    /// <exception cref="Win32Exception">设置失败，如要升到高于当前进程的级别。</exception>
    public static void SetLabel(string path, int rid, bool inheritable = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string sddl = string.Create(CultureInfo.InvariantCulture, $"S:(ML;{(inheritable ? "OICI" : "")};NW;;;S-1-16-{rid})");
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, SddlRevision1, out IntPtr descriptor, out _))
            throw new Win32Exception(Marshal.GetLastPInvokeError());

        try
        {
            if (!GetSecurityDescriptorSacl(descriptor, out _, out IntPtr sacl, out _))
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            uint error = SetNamedSecurityInfoW(Path.GetFullPath(path), SeFileObject, LabelSecurityInformation,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, sacl);
            if (error != 0)
                throw new Win32Exception((int)error);
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    /// <summary>从 SDDL 里取第一个强制标签 ACE（<c>ML</c>）的级别；没有时返回 null。</summary>
    public static int? ParseLabelRid(string sddl)
    {
        ArgumentNullException.ThrowIfNull(sddl);

        int start = sddl.IndexOf("(ML;", StringComparison.Ordinal);
        if (start < 0)
            return null;
        int end = sddl.IndexOf(')', start);
        if (end < 0)
            return null;

        // ACE 字符串：(类型;标志;权限;对象类型;继承对象类型;SID)
        string[] fields = sddl[(start + 1)..end].Split(';');
        if (fields.Length < 6)
            return null;

        string sid = fields[5];
        return sid switch
        {
            "LW" => LowRid,
            "ME" => MediumRid,
            "MP" => 0x2100,
            "HI" => 0x3000,
            "SI" => 0x4000,
            _ when sid.StartsWith("S-1-16-", StringComparison.Ordinal)
                   && int.TryParse(sid["S-1-16-".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rid) => rid,
            _ => null,
        };
    }

    // === Win32 ===

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetNamedSecurityInfoW(
        string pObjectName, int objectType, uint securityInfo,
        IntPtr ppsidOwner, IntPtr ppsidGroup, IntPtr ppDacl, out IntPtr ppSacl, out IntPtr ppSecurityDescriptor);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint SetNamedSecurityInfoW(
        string pObjectName, int objectType, uint securityInfo,
        IntPtr psidOwner, IntPtr psidGroup, IntPtr pDacl, IntPtr pSacl);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
        string stringSecurityDescriptor, uint stringSdRevision, out IntPtr securityDescriptor, out uint securityDescriptorSize);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSecurityDescriptorToStringSecurityDescriptorW(
        IntPtr securityDescriptor, uint requestedStringSdRevision, uint securityInformation,
        out IntPtr stringSecurityDescriptor, out uint stringSecurityDescriptorLength);

    [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorSacl(
        IntPtr securityDescriptor,
        [MarshalAs(UnmanagedType.Bool)] out bool saclPresent,
        out IntPtr sacl,
        [MarshalAs(UnmanagedType.Bool)] out bool saclDefaulted);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
