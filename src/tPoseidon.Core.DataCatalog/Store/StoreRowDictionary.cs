using System.Collections;
using System.Text;

namespace tPoseidon.Core.DataCatalog.Store;

public sealed class StoreRowDictionary : IReadOnlyDictionary<string, string>
{
    private readonly byte[] _data;
    private readonly StoreTable _table;
    private readonly int[] _start;
    private readonly int[] _length;

    internal StoreRowDictionary(byte[] data, StoreTable table)
    {
        _data = data;
        _table = table;
        _start = new int[table.Columns.Count];
        _length = new int[table.Columns.Count];
    }

    public int Count => _length.Count(length => length >= 0);

    public IEnumerable<string> Keys => this.Select(pair => pair.Key);

    public IEnumerable<string> Values => this.Select(pair => pair.Value);

    public string this[string key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException(key);

    internal void Set(int slot, int start, int length)
    {
        _start[slot] = start;
        _length[slot] = length;
    }

    public bool ContainsKey(string key) => _table.SlotByName.TryGetValue(key, out var slot) && _length[slot] >= 0;

    public bool TryGetValue(string key, out string value)
    {
        if (_table.SlotByName.TryGetValue(key, out var slot) && _length[slot] >= 0)
        {
            value = _length[slot] == 0 ? string.Empty : Encoding.UTF8.GetString(_data, _start[slot], _length[slot]);
            return true;
        }
        value = string.Empty;
        return false;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        for (var slot = 0; slot < _length.Length; slot++)
        {
            if (_length[slot] < 0) continue;
            var value = _length[slot] == 0 ? string.Empty : Encoding.UTF8.GetString(_data, _start[slot], _length[slot]);
            yield return new KeyValuePair<string, string>(_table.Columns[slot], value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
