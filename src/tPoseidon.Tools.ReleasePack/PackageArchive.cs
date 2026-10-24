using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace tPoseidon.Tools.ReleasePack;

internal sealed class PackageArchive : IDisposable
{
    public const UnixFileMode RegularMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
    public const UnixFileMode ExecutableMode = RegularMode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
    public const string CommentAttribute = "comment";
    private const UnixFileMode DirectoryType = (UnixFileMode)0x4000;

    private readonly ZipArchive? _zip;
    private readonly TarWriter? _tar;
    private readonly Stream _file;
    private readonly Stream? _gzip;
    private readonly DateTimeOffset _timestamp = DateTimeOffset.UtcNow;
    private readonly List<PackageEntry> _entries = new();

    public PackageArchive(string path, bool zip, string comment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);
        _file = File.Create(path);
        if (zip)
        {
            _zip = new ZipArchive(_file, ZipArchiveMode.Create, leaveOpen: true) { Comment = comment };
        }
        else
        {
            _gzip = new GZipStream(_file, CompressionLevel.Optimal, leaveOpen: true);
            _tar = new TarWriter(_gzip, TarEntryFormat.Pax, leaveOpen: true);
            _tar.WriteEntry(new PaxGlobalExtendedAttributesTarEntry(new Dictionary<string, string> { [CommentAttribute] = comment }));
        }
    }

    public IReadOnlyList<PackageEntry> Entries => _entries;

    public void AddFile(string sourcePath, string entryName, UnixFileMode mode)
    {
        using var source = File.OpenRead(sourcePath);
        AddStream(source, entryName, mode, File.GetLastWriteTimeUtc(sourcePath));
    }

    public void AddText(string entryName, string text, UnixFileMode mode)
    {
        using var source = new MemoryStream(new System.Text.UTF8Encoding(false).GetBytes(text));
        AddStream(source, entryName, mode, _timestamp);
    }

    public void AddDirectory(string entryName, UnixFileMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        var name = entryName.EndsWith('/') ? entryName : entryName + "/";
        if (_zip is not null)
        {
            var entry = _zip.CreateEntry(name, CompressionLevel.NoCompression);
            entry.LastWriteTime = _timestamp;
            entry.ExternalAttributes = (int)(mode | DirectoryType) << 16;
            return;
        }

        _tar!.WriteEntry(new PaxTarEntry(TarEntryType.Directory, name) { Mode = mode, ModificationTime = _timestamp });
    }

    public void Dispose()
    {
        _zip?.Dispose();
        _tar?.Dispose();
        _gzip?.Dispose();
        _file.Dispose();
    }

    private void AddStream(Stream source, string entryName, UnixFileMode mode, DateTimeOffset modified)
    {
        var digest = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        source.Position = 0;
        _entries.Add(new PackageEntry(entryName, source.Length, digest));

        if (_zip is not null)
        {
            var entry = _zip.CreateEntry(entryName, CompressionLevel.Optimal);
            entry.LastWriteTime = modified < new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero) ? _timestamp : modified;
            entry.ExternalAttributes = (int)mode << 16;
            using var target = entry.Open();
            source.CopyTo(target);
            return;
        }

        _tar!.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, entryName)
        {
            Mode = mode,
            ModificationTime = modified,
            DataStream = source,
        });
    }
}
