using System.Runtime.InteropServices;
using tPoseidon.Core.DataCatalog.Store;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog;

public sealed class TableIndex
{
    private readonly Dictionary<string, TableDescriptor> _byName;

    private TableIndex(IReadOnlyList<TableDescriptor> tables)
    {
        Tables = tables;
        SpanCount = tables.Sum(table => table.Occurrences);
        _byName = tables.ToDictionary(table => table.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<TableDescriptor> Tables { get; }

    public int SpanCount { get; }

    public IReadOnlyList<TableDescriptor> Duplicated => Tables.Where(table => table.Occurrences > 1).ToArray();

    public TableDescriptor? Find(string name) => _byName.GetValueOrDefault(name);

    public IReadOnlyList<TableDescriptor> InFamily(string family) =>
        Tables.Where(table => table.Family == family).ToArray();

    public IReadOnlyList<TableDescriptor> WithPrefix(string prefix) =>
        Tables.Where(table => table.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

    public static TableIndex Build(LibConfigXmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Build(BytesOf(reader));
    }

    internal static byte[] BytesOf(LibConfigXmlReader reader)
    {
        if (!MemoryMarshal.TryGetArray(reader.DecodedXml, out var segment) || segment.Array is null || segment.Offset != 0 || segment.Count != segment.Array.Length)
        {
            throw new InvalidOperationException("The libconfig reader does not expose its decoded XML as a whole array.");
        }
        return segment.Array;
    }

    public static TableIndex FromStore(TableStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var occurrences = store.Tables.GroupBy(table => table.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var described = new Dictionary<string, TableDescriptor>(StringComparer.Ordinal);
        foreach (var table in store.Tables)
        {
            if (described.ContainsKey(table.Name)) continue;
            described[table.Name] = new TableDescriptor(table.Name, TableFamilies.Of(table.Name), occurrences[table.Name], table.RowCount, table.Columns, TableReaders.Of(table.Name));
        }
        return new TableIndex(described.Values.OrderBy(table => table.Name, StringComparer.Ordinal).ToArray());
    }

    public static TableIndex Build(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var spans = LibConfigTableIndex.Of(xml).Tables;
        var occurrences = spans.GroupBy(span => span.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var described = new Dictionary<string, TableDescriptor>(StringComparer.Ordinal);
        foreach (var span in spans)
        {
            if (described.ContainsKey(span.Name)) continue;
            var (rows, columns) = TableScanner.Scan(xml, span);
            described[span.Name] = new TableDescriptor(span.Name, TableFamilies.Of(span.Name), occurrences[span.Name], rows, columns, TableReaders.Of(span.Name));
        }
        return new TableIndex(described.Values.OrderBy(table => table.Name, StringComparer.Ordinal).ToArray());
    }
}
