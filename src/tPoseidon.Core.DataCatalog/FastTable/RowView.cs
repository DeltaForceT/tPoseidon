using System.Globalization;
using System.Text;

namespace tPoseidon.Core.DataCatalog.FastTable;

public sealed class Column
{
    internal Column(string name)
    {
        Name = name;
    }

    public string Name { get; }

    internal int Slot { get; set; } = -1;
}

public sealed class RowView
{
    private readonly byte[] _xml;
    private readonly Dictionary<string, Column> _columns = new(StringComparer.Ordinal);
    private int[] _start = new int[64];
    private int[] _length = new int[64];
    private readonly List<string> _slotNames = new();
    private int _slotCount;

    internal RowView(byte[] xml)
    {
        _xml = xml;
    }

    public Column Col(string name)
    {
        if (_columns.TryGetValue(name, out var column)) return column;
        column = new Column(name);
        _columns[name] = column;
        return column;
    }

    public IReadOnlyList<string> SlotNames => _slotNames;

    public int SlotCount => _slotCount;

    public string SlotValue(int slot)
    {
        if (slot >= _slotCount || _length[slot] < 0) return string.Empty;
        return Decode(Raw(slot)).Trim();
    }

    public bool SlotPresent(int slot) => slot < _slotCount && _length[slot] >= 0;

    public string SlotRaw(int slot)
    {
        if (slot >= _slotCount || _length[slot] < 0) return string.Empty;
        return Decode(Raw(slot));
    }

    internal void Begin() => Array.Fill(_length, -1, 0, _slotCount);

    internal int SlotFor(string name)
    {
        if (!_columns.TryGetValue(name, out var column))
        {
            column = new Column(name);
            _columns[name] = column;
        }
        if (column.Slot >= 0) return column.Slot;
        column.Slot = _slotCount++;
        _slotNames.Add(name);
        if (column.Slot >= _start.Length)
        {
            Array.Resize(ref _start, _start.Length * 2);
            Array.Resize(ref _length, _length.Length * 2);
        }
        _length[column.Slot] = -1;
        return column.Slot;
    }

    internal void Set(int slot, int start, int length)
    {
        _start[slot] = start;
        _length[slot] = length;
    }

    public bool Has(Column column) => column.Slot >= 0 && _length[column.Slot] >= 0;

    public int AsInt(Column column)
    {
        if (column.Slot < 0 || _length[column.Slot] < 0) return 0;
        var raw = Raw(column.Slot);
        if (raw.IndexOf((byte)'&') >= 0) return int.TryParse(Decode(raw).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var decoded) ? decoded : 0;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    public float AsFloat(Column column)
    {
        if (column.Slot < 0 || _length[column.Slot] < 0) return 0f;
        var raw = Raw(column.Slot);
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    public double AsDouble(Column column)
    {
        if (column.Slot < 0 || _length[column.Slot] < 0) return 0.0;
        var raw = Raw(column.Slot);
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;
    }

    public string Str(Column column)
    {
        if (column.Slot < 0 || _length[column.Slot] < 0) return string.Empty;
        return Decode(Raw(column.Slot)).Trim();
    }

    public int Flags(Column column)
    {
        var raw = Str(column);
        if (raw.Length == 0) return 0;
        var flags = 0;
        foreach (var part in raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag)) flags |= flag;
        }
        return flags;
    }

    private ReadOnlySpan<byte> Raw(int slot) => _xml.AsSpan(_start[slot], _length[slot]);

    private static string Decode(ReadOnlySpan<byte> raw)
    {
        var text = Encoding.UTF8.GetString(raw);
        if (text.AsSpan().IndexOfAny('&', '\r', '<') < 0) return text;
        return XmlText.Unescape(text);
    }
}
