using System.Text;

namespace MapExTools.Core.Diagnostics;

/// <summary>一次增量读取的结果。</summary>
/// <param name="Exists">文件是否存在。</param>
/// <param name="Reset">文件被重写或删除过，之前读到的内容作废，<see cref="Lines"/> 从文件开头算起。</param>
/// <param name="Lines">这次新读到的完整行。</param>
public sealed record LogTailResult(bool Exists, bool Reset, IReadOnlyList<string> Lines);

/// <summary>
/// 增量读取正在写入的日志：每次只读上次之后新增的完整行，还没写完换行的半行留到下次。
/// 每次读取都重新以 <c>FileShare.ReadWrite | FileShare.Delete</c> 打开，不占着文件，游戏照常写入、轮转。
///
/// <para>游戏每次启动会重写日志，Player.log 会被改名为 Player-prev.log 再新建。文件变短，或上次读到的位置之前的
/// 字节变了，就当作重写，从头读。Player.log 每次运行的开头几行都一样，只比文件头分不出来，所以核对的是读到位置之前的字节。</para>
/// </summary>
public sealed class LogTail
{
    private const int AnchorLength = 64;
    private const int ChunkSize = 64 * 1024;

    /// <summary>首次打开或重置遇到超大日志时，仅读取末尾窗口（默认 32 MB）并对齐到下一行首，避免把数 GB 日志一次读入内存。</summary>
    internal const long DefaultMaxInitialBytes = 32L * 1024 * 1024;

    private readonly long _maxInitialBytes;
    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
    private readonly StringBuilder _partial = new();
    private long _position;
    private byte[] _anchor = [];
    private bool _seen;

    public LogTail(string path)
        : this(path, DefaultMaxInitialBytes)
    {
    }

    internal LogTail(string path, long maxInitialBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Path = path;
        _maxInitialBytes = Math.Max(AnchorLength, maxInitialBytes);
    }

    public string Path { get; }

    /// <summary>读取新增的内容。</summary>
    /// <exception cref="IOException">文件存在但读不了。</exception>
    /// <exception cref="UnauthorizedAccessException">没有读取权限。</exception>
    public LogTailResult Poll()
    {
        FileStream stream;
        try
        {
            stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            bool wasSeen = _seen;
            Restart();
            _seen = false;
            return new LogTailResult(false, wasSeen, []);
        }

        using (stream)
        {
            long length = stream.Length;
            bool reset = false;
            if (_seen && (length < _position || !AnchorMatches(stream)))
            {
                Restart();
                reset = true;
            }

            _seen = true;
            if (length <= _position)
                return new LogTailResult(true, reset, []);

            if (_position == 0 && length > _maxInitialBytes)
            {
                SeekToTailLineStart(stream, length - _maxInitialBytes);
                if (length <= _position)
                    return new LogTailResult(true, reset, []);
            }

            stream.Position = _position;
            var lines = new List<string>();
            byte[] buffer = new byte[ChunkSize];
            char[] chars = new char[Encoding.UTF8.GetMaxCharCount(ChunkSize)];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                int offset = 0;
                if (_position == 0 && read >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    offset = 3;

                int count = _decoder.GetChars(buffer, offset, read - offset, chars, 0, flush: false);
                AppendChars(chars.AsSpan(0, count), lines);
                RememberAnchor(buffer.AsSpan(0, read));
                _position += read;
            }

            return new LogTailResult(true, reset, lines);
        }
    }

    private void Restart()
    {
        _position = 0;
        _anchor = [];
        _partial.Clear();
        _decoder.Reset();
    }

    /// <summary>按换行切出完整行，去掉行尾的 CR；最后不完整的一段留在缓冲里。</summary>
    private void AppendChars(ReadOnlySpan<char> chars, List<string> lines)
    {
        while (!chars.IsEmpty)
        {
            int newline = chars.IndexOf('\n');
            if (newline < 0)
            {
                _partial.Append(chars);
                return;
            }

            _partial.Append(chars[..newline]);
            if (_partial.Length > 0 && _partial[^1] == '\r')
                _partial.Length--;
            lines.Add(_partial.ToString());
            _partial.Clear();
            chars = chars[(newline + 1)..];
        }
    }

    /// <summary>记下读到位置之前的最后几个字节，下次核对文件有没有被重写。</summary>
    private void RememberAnchor(ReadOnlySpan<byte> justRead)
    {
        if (justRead.Length >= AnchorLength)
        {
            _anchor = justRead[^AnchorLength..].ToArray();
            return;
        }

        byte[] combined = [.. _anchor, .. justRead];
        _anchor = combined.Length > AnchorLength ? combined[^AnchorLength..] : combined;
    }

    /// <summary>超大文件首读：跳到末尾窗口起点后向前跳过半截首行，对齐到第一个换行符之后，并记录该处锚点。</summary>
    private void SeekToTailLineStart(FileStream stream, long startOffset)
    {
        stream.Position = startOffset;
        byte[] skipBuf = new byte[ChunkSize];
        long cursor = startOffset;
        int read;
        while ((read = stream.Read(skipBuf, 0, skipBuf.Length)) > 0)
        {
            int nl = Array.IndexOf(skipBuf, (byte)'\n', 0, read);
            if (nl >= 0)
            {
                int count = nl + 1;
                RememberAnchor(skipBuf.AsSpan(0, count));
                _position = cursor + count;
                return;
            }

            RememberAnchor(skipBuf.AsSpan(0, read));
            cursor += read;
        }

        _position = cursor;
    }

    private bool AnchorMatches(FileStream stream)
    {
        if (_anchor.Length == 0)
            return true;

        if (_position < _anchor.Length)
            return false;

        try
        {
            stream.Position = _position - _anchor.Length;
            Span<byte> current = stackalloc byte[AnchorLength];
            current = current[.._anchor.Length];
            stream.ReadExactly(current);
            return current.SequenceEqual(_anchor);
        }
        catch (EndOfStreamException)
        {
            // 文件在检查长度后到 ReadExactly 之间被游戏截断或重写，视为重置
            return false;
        }
    }
}
