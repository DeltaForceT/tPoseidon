using System.Buffers.Binary;
using System.Security.Cryptography;
using tPoseidon.Core.Common;

namespace tPoseidon.Hosting.ClientScan;

public sealed record KnownMapFile(string FileName, int MapId, string MapName, long Size, string Sha256);

public sealed class ClientMapFilesCheck : IClientCheck
{
    public const string CheckId = "map-files";
    public const string Group = "Damaged map files";
    public const int CompressedBacVersion = 303;
    public const int CompressedBacMagic = 0xA0B0;

    private const string Md3Extension = ".md3";
    private const string BacExtension = ".bac";
    private const int Md3Signature = 0x5F4E444D;
    private const int VersionOffset = 4;
    private const int HeaderReadBytes = 8;

    public static IReadOnlyList<KnownMapFile> DefaultKnownFiles { get; } =
    [
        new("map_fd09a.md3", 89, "Rose Garden Field 1 - Stairs of Sorrow", 195_112, "194C170040FA1AD265A69C866B86CFE0E8C65A5BAD08D0283BE77A6EBDC0036D"),
    ];

    private readonly IReadOnlyList<KnownMapFile> _known;

    public ClientMapFilesCheck(IReadOnlyList<KnownMapFile>? known = null)
    {
        _known = known ?? DefaultKnownFiles;
    }

    public string Id => CheckId;

    public bool ReadsWorldData => false;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Directory.Exists(context.MapFolder))
        {
            return [];
        }

        var findings = new List<ScanFinding>();
        var flagged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var known in _known)
        {
            if (ClientFiles.Locate(Path.Combine(context.MapFolder, known.FileName)) is not { } path)
            {
                continue;
            }

            var size = new FileInfo(path).Length;
            if (size == known.Size && string.Equals(Sha256Of(path), known.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            flagged.Add(known.FileName);
            findings.Add(ScanFinding.Problem(
                Id,
                $"data\\map\\{known.FileName} is damaged or a wrong version ({size:N0} bytes; the original client file is {known.Size:N0} bytes, SHA-256 {known.Sha256}). " +
                $"The client crashes when it enters map {known.MapId} {known.MapName} until this file is replaced with a good copy of the original client file. This is a client file problem, not the server.",
                group: Group));
        }

        foreach (var md3 in Directory.EnumerateFiles(context.MapFolder, "*" + Md3Extension))
        {
            var name = Path.GetFileName(md3);
            if (flagged.Contains(name) || VersionMismatch(md3) is not { } mismatch)
            {
                continue;
            }

            findings.Add(ScanFinding.Problem(
                Id,
                $"data\\map\\{name} is a wrong version for its {Path.GetFileNameWithoutExtension(name)}{BacExtension}: the map file says version {mismatch.Version}, " +
                $"but the {BacExtension} file is in the {(mismatch.CompressedBac ? "compressed" : "older")} format, which the client reads only for version {(mismatch.CompressedBac ? $"{CompressedBacVersion} and later" : $"{CompressedBacVersion - 1} and earlier")}. " +
                "The client crashes when it enters this map until the file is replaced with a good copy of the original client file. This is a client file problem, not the server.",
                group: Group));
        }

        if (findings.Count == 0)
        {
            findings.Add(ScanFinding.Pass(Id, "The map files and their background files have matching versions."));
        }

        return findings;
    }

    public static (int Version, bool CompressedBac)? VersionMismatch(string md3Path)
    {
        ArgumentException.ThrowIfNullOrEmpty(md3Path);
        var bac = ClientFiles.Locate(Path.ChangeExtension(md3Path, BacExtension));
        if (bac is null || ReadHeader(md3Path) is not { } md3Header || ReadHeader(bac) is not { } bacHeader)
        {
            return null;
        }

        if (BinaryPrimitives.ReadInt32LittleEndian(md3Header) != Md3Signature)
        {
            return null;
        }

        var version = BinaryPrimitives.ReadInt32LittleEndian(md3Header.AsSpan(VersionOffset));
        var compressedBac = BinaryPrimitives.ReadInt32LittleEndian(bacHeader) == CompressedBacMagic;
        return compressedBac == version >= CompressedBacVersion ? null : (version, compressedBac);
    }

    private static byte[]? ReadHeader(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[HeaderReadBytes];
            return stream.ReadAtLeast(buffer, HeaderReadBytes, throwOnEndOfStream: false) == HeaderReadBytes ? buffer : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
