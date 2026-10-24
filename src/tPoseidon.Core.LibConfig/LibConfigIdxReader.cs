using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public sealed class LibConfigIdxReader
{
    public sealed record IdxEntry(string TableName, long Offset);

    private readonly Dictionary<string, IdxEntry> _byTable;

    public LibConfigIdxReader(IEnumerable<IdxEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byTable = new Dictionary<string, IdxEntry>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            _byTable[e.TableName] = e;
        }
    }

    public int Count => _byTable.Count;

    public IdxEntry? GetByTable(string tableName)
        => _byTable.TryGetValue(tableName, out var v) ? v : null;

    public IReadOnlyCollection<IdxEntry> All => _byTable.Values;

    public static bool TryLoad(string idxPath, out LibConfigIdxReader? reader)
    {
        reader = null;
        if (!File.Exists(idxPath)) return false;
        try
        {
            var bytes = File.ReadAllBytes(idxPath);
            var entries = ParseIdx(bytes);
            if (entries.Count == 0) return false;
            reader = new LibConfigIdxReader(entries);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<IdxEntry> ParseIdx(byte[] bytes)
    {
        var list = new List<IdxEntry>();
        var span = bytes.AsSpan();
        var pos = 0;
        while (pos < span.Length)
        {
            var nulIdx = span[pos..].IndexOf((byte)0);
            if (nulIdx < 0) break;
            if (nulIdx == 0)
            {
                pos++;
                continue;
            }
            var name = Encoding.ASCII.GetString(span.Slice(pos, nulIdx));
            pos += nulIdx + 1;
            if (pos + 4 > span.Length) break;
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(span[pos..]);
            pos += 4;
            list.Add(new IdxEntry(name, offset));
        }
        return list;
    }
}
