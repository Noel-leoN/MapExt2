using System.Globalization;
using MapExTools.Core.Localization;

namespace MapExTools.Core.Coc;

/// <summary>.coc 格式错误的种类，对应文案表里的 <c>Coc.Err.&lt;种类&gt;</c>。</summary>
public enum CocErrorKind
{
    /// <summary>字符串没有闭合（JSON 字符串里不能有原样的换行）。</summary>
    StringUnclosed,

    /// <summary>括号没有闭合。</summary>
    BracketUnclosed,

    /// <summary>块名之后直到文件末尾都没有 <c>{</c>。</summary>
    NoOpenBrace,

    /// <summary>这里应为带引号的键名。</summary>
    KeyExpected,

    /// <summary>键名之后缺少冒号。</summary>
    ColonMissing,

    /// <summary>键缺少取值。</summary>
    ValueMissing,

    /// <summary>两项之间缺少逗号。</summary>
    CommaMissing,

    /// <summary>最后一项后面多了逗号。</summary>
    TrailingComma,

    /// <summary>无法识别的取值（不是字符串、对象、数组，也不是 JSON 字面量）。</summary>
    BadLiteral,

    /// <summary>
    /// 同一对象里键名重复。游戏解码 JSON 对象时用 <c>Dictionary.Add</c>，遇到重复键会抛异常，整个块都读不进来。
    /// </summary>
    DuplicateKey,

    /// <summary>文件不是有效的 UTF-8 文本（没有行列）。</summary>
    InvalidEncoding,
}

/// <summary>
/// .coc 格式错误，带出错处的行列（从 1 起），供手动编辑后定位。
/// <see cref="Exception.Message"/> 是英文；界面用 <see cref="GetMessage"/> 取当前语言的文字。
/// </summary>
public sealed class CocFormatException : FormatException
{
    public CocFormatException(CocErrorKind kind, int line, int column, string? detail = null)
        : base(BuildMessage(UiLanguage.English, kind, line, column, detail))
    {
        Kind = kind;
        Line = line;
        Column = column;
        Detail = detail;
    }

    public CocErrorKind Kind { get; }

    /// <summary>出错处的行号，从 1 起；0 表示没有位置（如编码错误）。</summary>
    public int Line { get; }

    /// <summary>出错处的列号，从 1 起，按字符计。</summary>
    public int Column { get; }

    /// <summary>附带的键名、块名或取值原文。</summary>
    public string? Detail { get; }

    /// <summary>按界面语言生成错误说明，如"第 4 行第 5 列：两项之间缺少逗号"。</summary>
    public string GetMessage(string language) => BuildMessage(language, Kind, Line, Column, Detail);

    /// <summary>按文本中的位置建异常：位置换算成行列，换行以 LF 为准，CR 算作上一行的末尾字符。</summary>
    internal static CocFormatException At(CocErrorKind kind, string text, int index, string? detail = null)
    {
        int limit = Math.Clamp(index, 0, text.Length);
        int line = 1;
        int lineStart = 0;
        for (int i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return new CocFormatException(kind, line, limit - lineStart + 1, detail);
    }

    private static string BuildMessage(string language, CocErrorKind kind, int line, int column, string? detail)
    {
        string template = Strings.Get(language, "Coc.Err." + kind);
        if (line <= 0)
            return template;

        string position = string.Format(CultureInfo.InvariantCulture, Strings.Get(language, "Coc.Position"), line, column);
        return string.Format(CultureInfo.InvariantCulture, template, position, detail ?? string.Empty);
    }
}
