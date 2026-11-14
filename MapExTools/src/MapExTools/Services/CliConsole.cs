using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace MapExTools.Services;

/// <summary>
/// 命令行模式的输出（计划 2.2）。本程序是窗口程序，从控制台启动时没有自己的控制台：标准输出被重定向（管道、文件）
/// 就直接写；否则用 <c>AttachConsole</c> 接上启动它的控制台。都没有（从资源管理器或快捷方式运行）时，
/// 出错、<c>--show</c>、<c>--help</c> 改为弹窗显示，成功的静默结束。
/// </summary>
internal static class CliConsole
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;
    private const uint FileTypeDisk = 0x0001;
    private const uint FileTypePipe = 0x0003;

    /// <param name="text">要输出的全部文字。</param>
    /// <param name="dialogWithoutConsole">没有控制台时是否弹窗显示。</param>
    /// <param name="isError">弹窗用警告图标。</param>
    public static void Write(string text, bool dialogWithoutConsole, bool isError)
    {
        bool redirected = IsStdOutRedirected();

        // 重定向时也接上父控制台：控制台的代码页即外壳解码用的编码，中文不会乱码
        bool attached = AttachConsole(AttachParentProcess);
        if (redirected || attached)
        {
            if (!redirected)
            {
                // 直接写到控制台：用 UTF-16（WriteConsoleW），任何字符都能显示，也不改控制台的代码页。
                // 外壳不等窗口程序退出、提示符已先打出，空一行再输出
                Console.OutputEncoding = Encoding.Unicode;
                text = Environment.NewLine + text;
            }

            Console.Out.Write(text);
            if (!text.EndsWith('\n'))
                Console.Out.WriteLine();
            Console.Out.Flush();
            return;
        }

        if (dialogWithoutConsole)
        {
            MessageBox.Show(text.TrimEnd(), "MapExTools", MessageBoxButton.OK,
                isError ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
    }

    private static bool IsStdOutRedirected()
    {
        IntPtr handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return false;

        uint type = GetFileType(handle);
        return type is FileTypeDisk or FileTypePipe;
    }

    // === Win32 ===

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr hFile);
}
