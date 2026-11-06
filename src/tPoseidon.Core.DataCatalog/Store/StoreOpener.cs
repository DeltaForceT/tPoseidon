using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Store;

public sealed record OpenedStore(FastLibConfigReader Reader, CacheOutcomeKind Outcome, string? CacheFile);

public sealed record CatalogSupplement(string Name, Func<byte[], byte[]> Apply);

public enum CacheOutcomeKind
{
    Hit,
    Built,
    Uncached,
}

public static class StoreOpener
{
    public const int SchemaNumber = 1;

    public static string KeyOf(string sourcePath, string supplementName)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        var info = new FileInfo(Path.GetFullPath(sourcePath));
        var text = string.Create(CultureInfo.InvariantCulture, $"{info.Name}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{supplementName}|{SchemaNumber}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 8).ToLowerInvariant();
    }

    public static OpenedStore Open(string sourcePath, string? cacheDirectory = null, CatalogSupplement? supplement = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        var overrides = TranslationOverrides.ForLibConfigSource(sourcePath);
        var prefix = $"store-{supplement?.Name ?? "plain"}-";
        string? file = null;
        if (cacheDirectory is not null)
        {
            Directory.CreateDirectory(cacheDirectory);
            file = Path.Combine(cacheDirectory, $"{prefix}{KeyOf(sourcePath, supplement?.Name ?? string.Empty)}.bin");
            if (TryLoad(file, out var cached)) return new OpenedStore(new FastLibConfigReader(cached!, overrides), CacheOutcomeKind.Hit, file);
        }

        var xml = LibConfigXmlReader.DecodeFile(sourcePath);
        if (supplement is not null) xml = supplement.Apply(xml);
        var data = TableStore.Build(xml, SchemaNumber);
        var store = TableStore.Load(data, SchemaNumber);
        if (file is null) return new OpenedStore(new FastLibConfigReader(store, overrides), CacheOutcomeKind.Uncached, null);

        var temporary = $"{file}.{Environment.ProcessId}.tmp";
        File.WriteAllBytes(temporary, data);
        File.Move(temporary, file, overwrite: true);
        foreach (var stale in Directory.EnumerateFiles(cacheDirectory!, $"{prefix}*.bin").Where(path => path != file)) File.Delete(stale);
        return new OpenedStore(new FastLibConfigReader(store, overrides), CacheOutcomeKind.Built, file);
    }

    private static bool TryLoad(string file, out TableStore? store)
    {
        store = null;
        if (!File.Exists(file)) return false;
        try
        {
            store = TableStore.Load(File.ReadAllBytes(file), SchemaNumber);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }
}
