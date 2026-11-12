namespace MapExTools.Tests.Diagnostics;

/// <summary>日志诊断的脱敏样本：测试工程把 <c>Diagnostics/Samples</c> 复制到输出目录。</summary>
internal static class LogSamples
{
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Diagnostics", "Samples");

    public static string PathOf(string fileName) => Path.Combine(Directory, fileName);

    public static string[] ReadLines(string fileName) => File.ReadAllLines(PathOf(fileName));

    /// <summary>按换行拆开内联样本，换行可以是 LF 或 CRLF。</summary>
    public static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');
}
