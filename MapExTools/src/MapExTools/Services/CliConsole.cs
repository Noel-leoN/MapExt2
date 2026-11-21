using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace MapExTools.Services;

/// <summary>
/// 命令行模式的输出（计划 2.2）。正常结果写 stdout，错误与警告写 stderr。本程序是窗口程序，从控制台启动时没有自己的控制台：
/// 标准句柄被重定向（管道、文件）就直接写；否则用 <c>AttachConsole</c> 接上启动它的控制台。都没有（从资源管理器或快捷方式运行）时，
/// 出错、<c>--show</c>、<c>--help</c> 改为弹窗显示，成功的静默结束。
/// </summary>
internal static class CliConsole
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint FileTypeDisk = 0x0001;
    private const uint FileTypeChar = 0x0002;
    private const uint FileTypePipe = 0x0003;

    /// <summary>只有一段文字时：出错写 stderr，否则写 stdout。</summary>
    public static void Write(string text, bool dialogWithoutConsole, bool isError) =>
        Write(isError ? string.Empty : text, isError ? text : string.Empty, dialogWithoutConsole, isError);

    /// <param name="output">正常结果，写 stdout。</param>
    /// <param name="error">错误与警告，写 stderr。</param>
    /// <param name="dialogWithoutConsole">两路都无处可写（从资源管理器或快捷方式运行）时是否弹窗显示，两段合在一起。</param>
    /// <param name="isError">退出码表示出错：弹窗用警告图标。</param>
    public static void Write(string output, string error, bool dialogWithoutConsole, bool isError)
    {
        bool outRedirected = IsHandleRedirected(StdOutputHandle);
        bool errRedirected = IsHandleRedirected(StdErrorHandle);

        // 重定向时也接上父控制台：控制台的代码页即外壳解码用的编码，中文不会乱码
        bool attached = AttachConsole(AttachParentProcess);
        bool outReachable = outRedirected || attached;
        bool errReachable = errRedirected || attached;

        // 某一路既没重定向、也没有控制台可写：并入另一路，免得这段文字丢掉
        if (!errReachable)
        {
            output = Join(output, error);
            error = string.Empty;
        }
        else if (!outReachable)
        {
            error = Join(output, error);
            output = string.Empty;
        }

        if (!outReachable && !errReachable)
        {
            if (dialogWithoutConsole && output.Length > 0)
            {
                MessageBox.Show(output.TrimEnd(), "MapExTools", MessageBoxButton.OK,
                    isError ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }

            return;
        }

        if (!attached)
        {
            // 已重定向、附不上父控制台（非控制台宿主捕获输出）：按无 BOM 的 UTF-8 直接写标准句柄，不回退 ANSI 代码页。
            // 不改 Console.OutputEncoding：设成 UTF-16 以外的编码会调用 SetConsoleOutputCP，没有控制台时会失败
            WriteUtf8(Console.OpenStandardOutput, output);
            WriteUtf8(Console.OpenStandardError, error);
            return;
        }

        if (!outRedirected && !errRedirected)
        {
            // 两路都直接写到控制台：用 UTF-16（WriteConsoleW），任何字符都能显示，也不改控制台的代码页
            Console.OutputEncoding = Encoding.Unicode;
        }

        // 直接写到控制台的那一路：外壳不等窗口程序退出、提示符已先打出，先空一行
        if (!outRedirected && output.Length > 0)
            output = Environment.NewLine + output;
        else if (!errRedirected && error.Length > 0)
            error = Environment.NewLine + error;

        WriteAll(Console.Out, output);
        WriteAll(Console.Error, error);
    }

    private static void WriteAll(TextWriter writer, string text)
    {
        if (text.Length == 0)
            return;

        writer.Write(text);
        if (!text.EndsWith('\n'))
            writer.WriteLine();
        writer.Flush();
    }

    private static void WriteUtf8(Func<Stream> open, string text)
    {
        if (text.Length == 0)
            return;

        using var writer = new StreamWriter(open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: -1, leaveOpen: true);
        writer.Write(text);
        if (!text.EndsWith('\n'))
            writer.WriteLine();
        writer.Flush();
    }

    private static string Join(string first, string second) =>
        first.Length == 0 ? second
        : second.Length == 0 ? first
        : first.TrimEnd('\r', '\n') + Environment.NewLine + second;

    private static bool IsHandleRedirected(int stdHandle)
    {
        IntPtr handle = GetStdHandle(stdHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            return false;

        uint type = GetFileType(handle);
        if (type is FileTypeDisk or FileTypePipe)
            return true;

        // 字符设备但 GetConsoleMode 失败（例如重定向到 NUL）也视为已重定向
        if (type == FileTypeChar)
            return !GetConsoleMode(handle, out _);

        return false;
    }

    // === Win32 ===

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(IntPtr hFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
}
