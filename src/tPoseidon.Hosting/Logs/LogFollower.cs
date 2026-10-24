using System.Text;

namespace tPoseidon.Hosting.Logs;

public sealed class LogFollower
{
    public const int DefaultHistoryBytes = 64 * 1024;
    private const int ChunkBytes = 64 * 1024;

    private readonly string _path;
    private readonly int _historyBytes;
    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
    private readonly StringBuilder _pending = new();
    private readonly byte[] _buffer = new byte[ChunkBytes];
    private readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(ChunkBytes)];
    private long _position = -1;
    private bool _dropFirstLine;

    public LogFollower(string path, int historyBytes = DefaultHistoryBytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegative(historyBytes);
        _path = path;
        _historyBytes = historyBytes;
    }

    public string Path => _path;

    public IReadOnlyList<string> ReadNew()
    {
        if (!File.Exists(_path))
        {
            if (_position > 0)
            {
                Reset(0);
            }

            return Array.Empty<string>();
        }

        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        if (_position < 0)
        {
            _position = Math.Max(0, length - _historyBytes);
            _dropFirstLine = _position > 0 && !FollowsNewline(stream, _position);
        }
        else if (length < _position)
        {
            Reset(0);
        }

        if (length == _position)
        {
            return Array.Empty<string>();
        }

        stream.Seek(_position, SeekOrigin.Begin);
        var lines = new List<string>();
        int read;
        while ((read = stream.Read(_buffer, 0, _buffer.Length)) > 0)
        {
            _position += read;
            var count = _decoder.GetChars(_buffer, 0, read, _chars, 0);
            Split(_chars.AsSpan(0, count), lines);
        }

        return lines;
    }

    private static bool FollowsNewline(FileStream stream, long position)
    {
        stream.Seek(position - 1, SeekOrigin.Begin);
        return stream.ReadByte() == '\n';
    }

    private void Split(ReadOnlySpan<char> text, List<string> lines)
    {
        while (true)
        {
            var newline = text.IndexOf('\n');
            if (newline < 0)
            {
                _pending.Append(text);
                return;
            }

            _pending.Append(text[..newline]);
            var line = _pending.ToString().TrimEnd('\r');
            _pending.Clear();
            text = text[(newline + 1)..];
            if (_dropFirstLine)
            {
                _dropFirstLine = false;
                continue;
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }
    }

    private void Reset(long position)
    {
        _position = position;
        _pending.Clear();
        _decoder.Reset();
        _dropFirstLine = false;
    }
}
