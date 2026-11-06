using System.Globalization;
using System.Text;
using tPoseidon.Core.DataCatalog.FastTable;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Cache;

public enum CacheOutcome
{
    Hit,
    Built,
}

public sealed record CachedItems(ItemCatalog Items, CacheOutcome Outcome, string CacheFile);

public static class CatalogCache
{
    public const int SchemaNumber = 1;

    private const string Magic = "TPCAT";

    public static string KeyOf(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        var info = new FileInfo(Path.GetFullPath(sourcePath));
        var text = string.Create(CultureInfo.InvariantCulture, $"{info.Name}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{SchemaNumber}");
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    public static CachedItems LoadItems(string sourcePath, string cacheDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentException.ThrowIfNullOrEmpty(cacheDirectory);
        Directory.CreateDirectory(cacheDirectory);
        var key = KeyOf(sourcePath);
        var file = Path.Combine(cacheDirectory, $"items-{key}.bin");
        if (TryRead(file, key, out var cached)) return new CachedItems(cached!, CacheOutcome.Hit, file);

        var xml = LibConfigXmlReader.DecodeFile(sourcePath);
        var tables = ItemTables.All.Select(table => FastItemReader.Read(xml, ItemTables.NameOf(table))).ToArray();
        Write(file, key, tables);
        foreach (var stale in Directory.EnumerateFiles(cacheDirectory, "items-*.bin").Where(path => path != file)) File.Delete(stale);
        return new CachedItems(new ItemCatalog(tables[0], tables[1], tables[2], tables[3]), CacheOutcome.Built, file);
    }

    private static bool TryRead(string file, string key, out ItemCatalog? items)
    {
        items = null;
        if (!File.Exists(file)) return false;
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadString() != Magic || reader.ReadInt32() != SchemaNumber || reader.ReadString() != key) return false;
            var tables = ItemCacheCodec.Read(reader);
            if (tables.Count != ItemTables.All.Count) return false;
            items = new ItemCatalog(tables[0], tables[1], tables[2], tables[3]);
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (IndexOutOfRangeException)
        {
            return false;
        }
    }

    private static void Write(string file, string key, IReadOnlyList<IReadOnlyList<ItemParamEntry>> tables)
    {
        var temporary = $"{file}.{Environment.ProcessId}.tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write(Magic);
            writer.Write(SchemaNumber);
            writer.Write(key);
            ItemCacheCodec.Write(writer, tables);
        }
        File.Move(temporary, file, overwrite: true);
    }
}
