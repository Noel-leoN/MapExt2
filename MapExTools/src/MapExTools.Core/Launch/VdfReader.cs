using System.Text;

namespace MapExTools.Core.Launch;

/// <summary>
/// Steam KeyValues 文本（<c>.vdf</c>／<c>.acf</c>）的一个节点：要么是字符串值，要么含子节点。
/// 键名查找不分大小写，与 Steam 一致。
/// </summary>
public sealed class VdfNode
{
    private static readonly IReadOnlyList<VdfNode> s_noChildren = [];

    internal VdfNode(string key, string value)
    {
        Key = key;
        Value = value;
        Children = s_noChildren;
    }

    internal VdfNode(string key, IReadOnlyList<VdfNode> children)
    {
        Key = key;
        Children = children;
    }

    public string Key { get; }

    /// <summary>字符串值；含子节点时为 null。</summary>
    public string? Value { get; }

    /// <summary>子节点，按文件顺序；字符串值时为空。</summary>
    public IReadOnlyList<VdfNode> Children { get; }

    public bool IsObject => Value == null;

    /// <summary>第一个同名子节点；没有时为 null。</summary>
    public VdfNode? this[string key] =>
        Children.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// 读 Steam 的 KeyValues 文本格式：<c>"键" "值"</c> 或 <c>"键" { … }</c>，<c>//</c> 起为注释，
/// 引号内认 <c>\\</c>、<c>\"</c>、<c>\n</c>、<c>\t</c> 四种转义（Steam 写路径时把反斜杠写成 <c>\\</c>）。
/// 只用来取 <c>libraryfolders.vdf</c> 的库路径与 <c>appmanifest_*.acf</c> 的安装目录名。
/// </summary>
public static class VdfReader
{
    // 这两个文件只有三四层，限个深度防止异常文件递归过深
    private const int MaxDepth = 32;

    /// <summary>解析全文，返回键名为空的根节点，顶层各项是它的子节点。</summary>
    /// <exception cref="FormatException">括号不配对、缺少取值或引号没有闭合。</exception>
    public static VdfNode Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokenizer = new Tokenizer(text);
        return new VdfNode(string.Empty, ParseBody(tokenizer, depth: 0));
    }

    private static List<VdfNode> ParseBody(Tokenizer tokenizer, int depth)
    {
        var nodes = new List<VdfNode>();
        while (true)
        {
            Token token = tokenizer.Next();
            switch (token.Kind)
            {
                case TokenKind.End when depth == 0:
                    return nodes;
                case TokenKind.End:
                    throw new FormatException("缺少右花括号");
                case TokenKind.Close when depth == 0:
                    throw new FormatException("多余的右花括号");
                case TokenKind.Close:
                    return nodes;
                case TokenKind.Open:
                    throw new FormatException("左花括号前缺少键名");
            }

            string key = token.Text;
            Token value = tokenizer.Next();
            if (value.Kind == TokenKind.Text)
            {
                nodes.Add(new VdfNode(key, value.Text));
            }
            else if (value.Kind == TokenKind.Open)
            {
                if (depth + 1 > MaxDepth)
                    throw new FormatException("嵌套层数过多");
                nodes.Add(new VdfNode(key, ParseBody(tokenizer, depth + 1)));
            }
            else
            {
                throw new FormatException($"键 '{key}' 缺少取值");
            }
        }
    }

    // === 分词 ===

    private enum TokenKind
    {
        Text,
        Open,
        Close,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text = "");

    private sealed class Tokenizer(string text)
    {
        private int _pos;

        public Token Next()
        {
            SkipTrivia();
            if (_pos >= text.Length)
                return new Token(TokenKind.End);

            char c = text[_pos];
            switch (c)
            {
                case '{':
                    _pos++;
                    return new Token(TokenKind.Open);
                case '}':
                    _pos++;
                    return new Token(TokenKind.Close);
                case '"':
                    return new Token(TokenKind.Text, ReadQuoted());
                default:
                    return new Token(TokenKind.Text, ReadBare());
            }
        }

        /// <summary>跳过空白、<c>//</c> 注释与 <c>[$WIN32]</c> 这类平台条件（这里不区分平台）。</summary>
        private void SkipTrivia()
        {
            while (_pos < text.Length)
            {
                char c = text[_pos];
                if (char.IsWhiteSpace(c))
                {
                    _pos++;
                }
                else if (c == '/' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                {
                    int end = text.IndexOf('\n', _pos);
                    _pos = end < 0 ? text.Length : end + 1;
                }
                else if (c == '[')
                {
                    int end = text.IndexOf(']', _pos);
                    _pos = end < 0 ? text.Length : end + 1;
                }
                else
                {
                    return;
                }
            }
        }

        private string ReadQuoted()
        {
            var sb = new StringBuilder();
            _pos++; // 开头的引号
            while (_pos < text.Length)
            {
                char c = text[_pos++];
                if (c == '"')
                    return sb.ToString();
                if (c == '\\' && _pos < text.Length)
                {
                    char next = text[_pos++];
                    switch (next)
                    {
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '"':
                            sb.Append('"');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        default:
                            // 不认识的转义原样保留，免得改掉路径里的单个反斜杠
                            sb.Append('\\').Append(next);
                            break;
                    }

                    continue;
                }

                sb.Append(c);
            }

            throw new FormatException("引号没有闭合");
        }

        private string ReadBare()
        {
            int start = _pos;
            while (_pos < text.Length)
            {
                char c = text[_pos];
                if (char.IsWhiteSpace(c) || c is '{' or '}' or '"')
                    break;
                _pos++;
            }

            return text[start.._pos];
        }
    }
}
