using System.Text.RegularExpressions;

namespace MapExTools.Core.Coc;

/// <summary>.coc 里的一个设定块：块名行之后的 JSON 对象，<see cref="Open"/>、<see cref="Close"/> 是两个大括号的位置。</summary>
public sealed record CocBlock(string Name, int Open, int Close);

/// <summary>
/// 对象顶层的一项。<see cref="KeyStart"/> 是键名开引号的位置，取值占 [<see cref="Start"/>, <see cref="End"/>)，
/// <see cref="Raw"/> 是取值原文。键名取引号之间的原文，不做转义还原。
/// </summary>
public sealed record CocEntry(string Key, int KeyStart, int Start, int End, string Raw);

/// <summary>
/// .coc 的切块与严格 JSON 检查，移植自开发版 <c>Set-MapExtStartup.ps1</c>。
///
/// <para>切块规则与游戏的 <c>COCParser</c> 一致：块名是 <c>{</c> 或换行之前的文字，其后是一个 JSON 对象；
/// 同名块重复时游戏只认第一个。</para>
///
/// <para>对象内部按严格 JSON 检查，手动编辑后靠它报出缺逗号、多逗号、取值写错、键名重复等问题；
/// 嵌套的对象与数组只核对括号配对。游戏的解码器其实更宽松（逗号可省），这里从严，
/// 免得留下只有游戏读得懂的写法。</para>
///
/// <para>空白只认空格、Tab、CR、LF，与游戏的 <c>COCParser</c> 和 JSON 解码器相同。
/// 全角空格、不换行空格等不算空白，游戏读到会出错，这里按格式错误报出。</para>
/// </summary>
public static partial class CocParser
{
    // JSON 字面量；NaN、Infinity 不是标准 JSON，但游戏写浮点时可能产生，放行以免误报
    [GeneratedRegex(@"^(true|false|null|-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?|NaN|-?Infinity)$", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralRegex();

    /// <summary>是否为游戏认可的空白字符。</summary>
    public static bool IsWhitespace(char c) => c is ' ' or '\t' or '\n' or '\r';

    /// <summary>文本是否为空或只有空白。</summary>
    public static bool IsBlank(string text)
    {
        foreach (char c in text)
        {
            if (!IsWhitespace(c))
                return false;
        }

        return true;
    }

    /// <summary>按游戏的规则切出全部块。</summary>
    /// <exception cref="CocFormatException">块名之后没有 <c>{</c>、括号或字符串没有闭合。</exception>
    public static IReadOnlyList<CocBlock> ParseBlocks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var blocks = new List<CocBlock>();
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && IsWhitespace(text[i]))
                i++;
            if (i >= text.Length)
                break;

            int nameStart = i;
            while (i < text.Length && text[i] != '{' && text[i] != '\n' && text[i] != '\r')
                i++;
            string name = text[nameStart..i].Trim(' ', '\t');

            // 块名与 { 之间的其他文字游戏也跳过
            while (i < text.Length && text[i] != '{')
                i++;
            if (i >= text.Length)
                throw CocFormatException.At(CocErrorKind.NoOpenBrace, text, nameStart, name);

            int close = FindClosingBracket(text, i);
            blocks.Add(new CocBlock(name, i, close));
            i = close + 1;
        }

        return blocks;
    }

    /// <summary>严格按 JSON 解析块内对象的顶层各项。</summary>
    /// <exception cref="CocFormatException">对象内容不是合格的 JSON，或键名重复。</exception>
    public static IReadOnlyList<CocEntry> ParseEntries(string text, CocBlock block)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(block);

        var entries = new List<CocEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int close = block.Close;
        int i = SkipWhitespace(text, block.Open + 1, close);
        if (i >= close)
            return entries;

        while (true)
        {
            if (text[i] != '"')
                throw CocFormatException.At(CocErrorKind.KeyExpected, text, i);

            int keyStart = i;
            i = SkipString(text, i);
            string key = text[(keyStart + 1)..(i - 1)];
            if (!seen.Add(key))
                throw CocFormatException.At(CocErrorKind.DuplicateKey, text, keyStart, key);

            i = SkipWhitespace(text, i, close);
            if (i >= close || text[i] != ':')
                throw CocFormatException.At(CocErrorKind.ColonMissing, text, i, key);

            i = SkipWhitespace(text, i + 1, close);
            if (i >= close || text[i] == ',')
                throw CocFormatException.At(CocErrorKind.ValueMissing, text, i, key);

            int valueStart = i;
            char c = text[i];
            if (c == '"')
            {
                i = SkipString(text, i);
            }
            else if (c is '{' or '[')
            {
                i = FindClosingBracket(text, i) + 1;
            }
            else
            {
                while (i < close && text[i] != ',' && !IsWhitespace(text[i]))
                    i++;
                string literal = text[valueStart..i];
                if (!LiteralRegex().IsMatch(literal))
                    throw CocFormatException.At(CocErrorKind.BadLiteral, text, valueStart, literal);
            }

            entries.Add(new CocEntry(key, keyStart, valueStart, i, text[valueStart..i]));

            i = SkipWhitespace(text, i, close);
            if (i >= close)
                return entries;
            if (text[i] != ',')
                throw CocFormatException.At(CocErrorKind.CommaMissing, text, i);

            int comma = i;
            i = SkipWhitespace(text, i + 1, close);
            if (i >= close)
                throw CocFormatException.At(CocErrorKind.TrailingComma, text, comma);
        }
    }

    private static int SkipWhitespace(string text, int index, int limit)
    {
        while (index < limit && IsWhitespace(text[index]))
            index++;
        return index;
    }

    /// <summary><paramref name="index"/> 指向开引号，返回闭引号之后的位置；JSON 字符串里不能有原样的换行。</summary>
    private static int SkipString(string text, int index)
    {
        int i = index + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '"')
                return i + 1;
            if (c == '\n')
                break;
            i++;
        }

        throw CocFormatException.At(CocErrorKind.StringUnclosed, text, index);
    }

    /// <summary><paramref name="index"/> 指向 <c>{</c> 或 <c>[</c>，返回配对闭括号的位置；字符串里的括号不计。</summary>
    private static int FindClosingBracket(string text, int index)
    {
        int depth = 0;
        int i = index;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '"')
            {
                i = SkipString(text, i);
                continue;
            }

            if (c is '{' or '[')
            {
                depth++;
            }
            else if (c is '}' or ']')
            {
                depth--;
                if (depth == 0)
                    return i;
            }

            i++;
        }

        throw CocFormatException.At(CocErrorKind.BracketUnclosed, text, index);
    }
}
