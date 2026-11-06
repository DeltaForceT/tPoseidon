using System.Security.Cryptography;

namespace tPoseidon.Core.DataCatalog;

public sealed record CatalogVersion(string Source, long Length, DateTime LastWriteUtc, string Sha256)
{
    public static CatalogVersion Of(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var info = new FileInfo(Path.GetFullPath(path));
        using var stream = info.OpenRead();
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        return new CatalogVersion(info.Name, info.Length, info.LastWriteTimeUtc, hash);
    }

    public bool SameContent(CatalogVersion other) => Sha256 == other.Sha256;
}
