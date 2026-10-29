using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public sealed record InstallerHeader(string ServerName, string Version, string Executable, string Icon, string Logo, string AccentColor);

public sealed class InstallerPayload
{
    public const int MaxHeaderBytes = 16 * 1024;

    private const int MagicBytes = 16;
    private const int TrailerBytes = MagicBytes + 12;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TPOSEIDON-SETUP1");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _path;
    private readonly long _zipStart;
    private readonly long _zipLength;

    private InstallerPayload(string path, InstallerHeader header, long zipStart, long zipLength)
    {
        _path = path;
        Header = header;
        _zipStart = zipStart;
        _zipLength = zipLength;
    }

    public InstallerHeader Header { get; }

    public long StubLength => _zipStart;

    public static void Write(string stubPath, string zipPath, InstallerHeader header, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stubPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var json = JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions);
        if (json.Length > MaxHeaderBytes)
        {
            throw new HostingException("The installer information is too big.");
        }

        var temporary = outputPath + ".tmp";
        try
        {
            using (var output = File.Create(temporary))
            {
                CopyTo(stubPath, output);
                var zipLength = CopyTo(zipPath, output);
                output.Write(json);
                var trailer = new byte[TrailerBytes];
                BinaryPrimitives.WriteInt64LittleEndian(trailer, zipLength);
                BinaryPrimitives.WriteInt32LittleEndian(trailer.AsSpan(8), json.Length);
                Magic.CopyTo(trailer, 12);
                output.Write(trailer);
            }

            File.Move(temporary, outputPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The installer could not be written to {outputPath}: {ex.Message}", ex);
        }
    }

    public static InstallerPayload? TryRead(string exePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exePath);
        using var file = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length < TrailerBytes)
        {
            return null;
        }

        var trailer = new byte[TrailerBytes];
        file.Seek(-TrailerBytes, SeekOrigin.End);
        file.ReadExactly(trailer);
        if (!trailer.AsSpan(12).SequenceEqual(Magic))
        {
            return null;
        }

        var zipLength = BinaryPrimitives.ReadInt64LittleEndian(trailer);
        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(trailer.AsSpan(8));
        var zipStart = file.Length - TrailerBytes - headerLength - zipLength;
        if (headerLength is <= 0 or > MaxHeaderBytes || zipLength <= 0 || zipStart <= 0)
        {
            throw Damaged(null);
        }

        var headerBytes = new byte[headerLength];
        file.Seek(zipStart + zipLength, SeekOrigin.Begin);
        file.ReadExactly(headerBytes);
        InstallerHeader? header;
        try
        {
            header = JsonSerializer.Deserialize<InstallerHeader>(headerBytes, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw Damaged(ex);
        }

        return header is null ? throw Damaged(null) : new InstallerPayload(exePath, header, zipStart, zipLength);
    }

    public Stream OpenZip()
    {
        var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return new SliceStream(file, _zipStart, _zipLength);
    }

    public void CopyStubTo(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        using var source = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var target = File.Create(targetPath);
        var buffer = new byte[81920];
        var remaining = _zipStart;
        while (remaining > 0)
        {
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            target.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private static HostingException Damaged(Exception? inner) =>
        new("The installer is damaged.", "Download it again.", inner);

    private static long CopyTo(string source, Stream target)
    {
        using var input = File.OpenRead(source);
        input.CopyTo(target);
        return input.Length;
    }

    private sealed class SliceStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public SliceStream(Stream inner, long start, long length)
        {
            _inner = inner;
            _start = start;
            _length = length;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _length - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            _inner.Position = _start + _position;
            var read = _inner.Read(buffer, offset, (int)Math.Min(count, remaining));
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                _ => _length + offset,
            };
            return _position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
