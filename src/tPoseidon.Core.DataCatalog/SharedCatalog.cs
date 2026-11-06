using tPoseidon.Core.DataCatalog.Store;

namespace tPoseidon.Core.DataCatalog;

public static class SharedCatalog
{
    public const string CacheDirectoryVariable = "MT_CATALOG_CACHE";

    public const string DataDirectoryVariable = "MT_DATA_DIR";

    private static readonly object Gate = new();

    private static readonly Dictionary<string, (StoreCatalog Catalog, string SupplementName, long Length, long Ticks)> Open = new(StringComparer.OrdinalIgnoreCase);

    public static string DefaultCacheDirectory()
    {
        var configured = Environment.GetEnvironmentVariable(CacheDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var dataDirectory = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        return string.IsNullOrWhiteSpace(dataDirectory)
            ? Path.Combine(Path.GetTempPath(), "tposeidon-catalog-cache")
            : Path.Combine(dataDirectory, "catalog-cache");
    }

    public static StoreCatalog For(string libconfigPath, string? cacheDirectory = null, CatalogSupplement? supplement = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigPath);
        var key = Path.GetFullPath(libconfigPath);
        var name = supplement?.Name ?? string.Empty;
        lock (Gate)
        {
            var info = new FileInfo(key);
            if (Open.TryGetValue(key, out var existing) && existing.Length == info.Length && existing.Ticks == info.LastWriteTimeUtc.Ticks)
            {
                if (existing.SupplementName != name && name.Length > 0) throw new InvalidOperationException($"The catalog of {key} is already open with supplement '{existing.SupplementName}', not '{name}'.");
                return existing.Catalog;
            }

            var catalog = StoreCatalog.Open(key, cacheDirectory ?? DefaultCacheDirectory(), supplement);
            Open[key] = (catalog, name, info.Length, info.LastWriteTimeUtc.Ticks);
            return catalog;
        }
    }

    public static void Reset()
    {
        lock (Gate) Open.Clear();
    }
}
