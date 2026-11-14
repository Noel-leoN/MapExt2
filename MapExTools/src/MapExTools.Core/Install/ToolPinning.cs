using System.ComponentModel;
using System.Diagnostics;

namespace MapExTools.Core.Install;

/// <summary>"固定到本地"的预检结果：目标位置有没有文件、内容是否相同、两边的版本。</summary>
/// <param name="SourceVersion">当前 exe 的文件版本；读不到时为 null。</param>
/// <param name="TargetVersion">目标位置已有 exe 的文件版本；没有文件或读不到时为 null。</param>
public sealed record PinPlan(
    string SourcePath,
    string TargetPath,
    bool TargetExists,
    bool SameContent,
    Version? SourceVersion,
    Version? TargetVersion)
{
    /// <summary>当前运行的就是目标位置那一份。</summary>
    public bool IsSameFile =>
        string.Equals(Path.GetFullPath(SourcePath), Path.GetFullPath(TargetPath), StringComparison.OrdinalIgnoreCase);

    /// <summary>要不要复制：目标不存在，或内容不同。</summary>
    public bool NeedsCopy => !IsSameFile && !SameContent;

    /// <summary>要覆盖目标位置已有的另一份。</summary>
    public bool ReplacesExisting => NeedsCopy && TargetExists;

    /// <summary>会用较旧的版本覆盖目标位置较新的版本。</summary>
    public bool IsDowngrade => ReplacesExisting && SourceVersion != null && TargetVersion != null && TargetVersion > SourceVersion;
}

/// <summary>
/// "固定到本地"（计划 §2 第 9 条）：把单文件的 MapExTools.exe 复制到 <see cref="DefaultPinnedDir"/>，
/// 已有旧版本时覆盖更新。只复制 exe 一个文件，所以只适用于单文件发布（计划 5.4）。
///
/// <para>复制用 <see cref="File.Copy(string, string, bool)"/>，下载标记（Zone.Identifier）随文件保留，
/// 不替玩家去掉；只在玩家点击后执行，不自动执行、不写注册表与开机启动（防杀软误判，计划 §2 第 9 条）。</para>
/// </summary>
public static class ToolPinning
{
    /// <summary>
    /// 固定位置的文件名。发布包里就叫这个名字；改过名的副本固定后也用它，覆盖更新时快捷方式不用改。
    /// </summary>
    public const string ExeName = "MapExTools.exe";

    /// <summary>
    /// 固定位置 <c>%LOCALAPPDATA%\Programs\MapExTools</c>：按用户安装的程序惯用的目录，普通完整性级别，不需要管理员权限。
    /// </summary>
    public static string DefaultPinnedDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MapExTools");

    /// <summary>预检：目标位置有没有文件、与当前 exe 是否相同、两边的版本。</summary>
    public static PinPlan Plan(string sourcePath, string pinnedDir)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(pinnedDir);

        string source = Path.GetFullPath(sourcePath);
        string target = Path.Combine(Path.GetFullPath(pinnedDir), ExeName);
        bool exists = File.Exists(target);
        bool sameFile = string.Equals(source, target, StringComparison.OrdinalIgnoreCase);
        bool sameContent = exists && (sameFile || ContentEquals(source, target));
        return new PinPlan(source, target, exists, sameContent, ReadVersion(source), exists ? ReadVersion(target) : null);
    }

    /// <summary>
    /// 复制到固定位置：先写同目录的临时文件，再整体替换。目标正在运行等原因替换失败时抛出异常，原有的那份不变。
    /// </summary>
    /// <exception cref="IOException">读写错误，或目标被占用。</exception>
    /// <exception cref="UnauthorizedAccessException">没有写入权限，或目标正在运行。</exception>
    public static void Copy(PinPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.NeedsCopy)
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(plan.TargetPath)!);

        // 临时文件不用 .exe 扩展名，中途失败留下的残件不会被当成程序
        string temp = plan.TargetPath + ".new";
        try
        {
            File.Copy(plan.SourcePath, temp, overwrite: true);
            File.Move(temp, plan.TargetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    /// <summary>有没有别的进程正从 <paramref name="exePath"/> 运行（同名进程逐个比主模块路径，读不到的跳过）。</summary>
    public static bool IsRunningFrom(string exePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(exePath);

        string target = Path.GetFullPath(exePath);
        Process[] processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target));
        try
        {
            foreach (Process process in processes)
            {
                if (process.Id == Environment.ProcessId)
                    continue;
                try
                {
                    if (string.Equals(process.MainModule?.FileName, target, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // 进程已退出或无权读取，跳过
                }
            }

            return false;
        }
        finally
        {
            foreach (Process process in processes)
                process.Dispose();
        }
    }

    /// <summary>文件版本资源里的版本号；没有版本资源（各部分都为 0）时返回 null。</summary>
    public static Version? ReadVersion(string path)
    {
        FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
        if (info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0 && info.FilePrivatePart == 0)
            return null;
        return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
    }

    /// <summary>逐字节比较两个文件。</summary>
    public static bool ContentEquals(string first, string second)
    {
        using var a = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var b = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (a.Length != b.Length)
            return false;

        byte[] bufferA = new byte[81920];
        byte[] bufferB = new byte[81920];
        while (true)
        {
            int read = a.ReadAtLeast(bufferA, bufferA.Length, throwOnEndOfStream: false);
            if (read == 0)
                return true;
            b.ReadExactly(bufferB, 0, read);
            if (!bufferA.AsSpan(0, read).SequenceEqual(bufferB.AsSpan(0, read)))
                return false;
        }
    }
}
