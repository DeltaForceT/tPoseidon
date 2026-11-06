using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.DataCatalog.FastTable;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Store;

public sealed class StoreTable
{
    internal StoreTable(string name, string[] columns, int rowCount, int lensStart, int blobStart)
    {
        Name = name;
        Columns = columns;
        RowCount = rowCount;
        LensStart = lensStart;
        BlobStart = blobStart;
        SlotByName = new Dictionary<string, int>(columns.Length, StringComparer.Ordinal);
        for (var slot = 0; slot < columns.Length; slot++) SlotByName.TryAdd(columns[slot], slot);
    }

    public string Name { get; }

    public IReadOnlyList<string> Columns { get; }

    public int RowCount { get; }

    internal int LensStart { get; }

    internal int BlobStart { get; }

    internal Dictionary<string, int> SlotByName { get; }
}

public sealed class TableStore
{
    private const string Magic = "TPSTORE";

    private readonly byte[] _data;
    private readonly Dictionary<string, StoreTable> _first;

    private TableStore(byte[] data, IReadOnlyList<StoreTable> tables)
    {
        _data = data;
        Tables = tables;
        _first = new Dictionary<string, StoreTable>(StringComparer.Ordinal);
        foreach (var table in tables) _first.TryAdd(table.Name, table);
    }

    public IReadOnlyList<StoreTable> Tables { get; }

    public long SizeInBytes => _data.Length;

    public bool TryFind(string name, out StoreTable table) => _first.TryGetValue(name, out table!);

    public void ForEachRow(StoreTable table, Action<StoreRowDictionary> handler)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(handler);
        var row = new StoreRowDictionary(_data, table);
        var pointer = table.BlobStart;
        var columns = table.Columns.Count;
        for (var index = 0; index < table.RowCount; index++)
        {
            var lensAt = table.LensStart + index * columns * 4;
            for (var slot = 0; slot < columns; slot++)
            {
                var length = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(lensAt + slot * 4, 4));
                row.Set(slot, pointer, length);
                if (length > 0) pointer += length;
            }
            handler(row);
        }
    }

    public static byte[] Build(byte[] xml, int schema)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var spans = LibConfigTableIndex.Of(xml).Tables;
        var body = new MemoryStream();
        var directory = new List<(string Name, int Offset)>(spans.Count);
        using (var writer = new BinaryWriter(body, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var span in spans)
            {
                directory.Add((span.Name, (int)body.Position));
                WriteSection(writer, xml, span);
            }
        }

        var output = new MemoryStream((int)body.Length + 1024);
        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(schema);
            writer.Write(directory.Count);
            foreach (var (name, offset) in directory)
            {
                writer.Write(name);
                writer.Write(offset);
            }
            writer.Write((int)body.Length);
            writer.Write(body.GetBuffer(), 0, (int)body.Length);
        }
        return output.ToArray();
    }

    public static TableStore Load(byte[] data, int schema)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var stream = new MemoryStream(data, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (reader.ReadString() != Magic || reader.ReadInt32() != schema) throw new InvalidDataException("The table store was written by another version.");
        var count = reader.ReadInt32();
        var directory = new (string Name, int Offset)[count];
        for (var index = 0; index < count; index++) directory[index] = (reader.ReadString(), reader.ReadInt32());
        var bodyLength = reader.ReadInt32();
        var bodyStart = (int)stream.Position;
        if (bodyStart + bodyLength != data.Length) throw new InvalidDataException("The table store is truncated.");
        var tables = new List<StoreTable>(count);
        foreach (var (name, offset) in directory)
        {
            stream.Position = bodyStart + offset;
            var columns = new string[reader.ReadInt32()];
            for (var slot = 0; slot < columns.Length; slot++) columns[slot] = reader.ReadString();
            var rowCount = reader.ReadInt32();
            var lensStart = (int)stream.Position;
            stream.Position += (long)rowCount * columns.Length * 4;
            var blobLength = reader.ReadInt32();
            var blobStart = (int)stream.Position;
            if (blobStart + blobLength > data.Length) throw new InvalidDataException("The table store is truncated.");
            tables.Add(new StoreTable(name, columns, rowCount, lensStart, blobStart));
        }
        return new TableStore(data, tables);
    }

    private static void WriteSection(BinaryWriter writer, byte[] xml, LibConfigTableSpan span)
    {
        if (!xml.AsSpan(span.Start, span.Length).TrimEnd((byte)' ').EndsWith("</TABLE>"u8)) throw new InvalidDataException($"Table {span.Name} is malformed: the table is not closed.");
        IReadOnlyList<string> columns = Array.Empty<string>();
        var rows = 0;
        FastTableReader.Read(xml, span, row =>
        {
            rows++;
            columns = row.SlotNames;
        });
        var columnCount = columns.Count;
        writer.Write(columnCount);
        foreach (var column in columns) writer.Write(column);
        writer.Write(rows);
        var blob = new MemoryStream();
        var lens = new MemoryStream(rows * columnCount * 4);
        using (var lensWriter = new BinaryWriter(lens, Encoding.UTF8, leaveOpen: true))
        {
            FastTableReader.Read(xml, span, row =>
            {
                for (var slot = 0; slot < columnCount; slot++)
                {
                    if (!row.SlotPresent(slot))
                    {
                        lensWriter.Write(-1);
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(row.SlotRaw(slot));
                    lensWriter.Write(bytes.Length);
                    blob.Write(bytes);
                }
            });
        }
        writer.Write(lens.GetBuffer(), 0, (int)lens.Length);
        writer.Write((int)blob.Length);
        writer.Write(blob.GetBuffer(), 0, (int)blob.Length);
    }
}
