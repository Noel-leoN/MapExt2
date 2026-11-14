using System.Text;

namespace MapExTools.Core.Launch;

/// <summary>
/// <c>{UserData}/runOnce.txt</c>：游戏启动时读出全文、按空格拆分并入命令行，随即删除（<c>GameManager.ParseOptions</c>）。
/// 不论由哪条通道启动都会读，所以页面上显示里面待生效的参数。
/// </summary>
public static class RunOnceFile
{
    private static readonly UTF8Encoding s_utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// 待生效的参数，空白折成单个空格；文件不存在、只有空白或读不了时为 null。
    /// </summary>
    public static string? ReadPending(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        try
        {
            if (!File.Exists(path))
                return null;

            string[] tokens = File.ReadAllText(path).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return tokens.Length == 0 ? null : string.Join(' ', tokens);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 整个覆盖写入（不与旧内容合并）：UTF-8 无 BOM、不带结尾换行；先写同目录的临时文件再整体替换。
    /// 内容为空时删除文件，免得旧参数留到这次启动。
    /// </summary>
    /// <exception cref="IOException">写入失败。</exception>
    /// <exception cref="UnauthorizedAccessException">没有写入权限。</exception>
    public static void Write(string path, string content)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length == 0)
        {
            Delete(path);
            return;
        }

        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);

        // 以点开头：游戏扫描用户目录时跳过这类文件
        string temp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, content, s_utf8NoBom);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    /// <summary>删除文件；不存在时什么也不做。</summary>
    /// <exception cref="IOException">删除失败。</exception>
    public static void Delete(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (File.Exists(path))
            File.Delete(path);
    }
}
