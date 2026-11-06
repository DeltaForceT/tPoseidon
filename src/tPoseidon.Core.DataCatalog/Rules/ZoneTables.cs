using tPoseidon.Core.DataCatalog.Store;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Rules;

public sealed class ZoneTables
{
    private readonly TableStore _store;
    private readonly TableIndex _index;

    public ZoneTables(TableStore store, TableIndex index)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(index);
        _store = store;
        _index = index;
    }

    public IReadOnlyList<MonZoneEntry> ToMonZones()
    {
        var entries = new List<MonZoneEntry>();
        Read(TableKinds.MonsterPool, TableRules.MonsterPrefix, (name, row) => entries.Add(new MonZoneEntry
        {
            MapName = name,
            MonsterId = RowValues.ReadInt(row, "MonsterID"),
            Ratio = RowValues.ReadFloat(row, "Ratio"),
            Avr = RowValues.ReadInt(row, "Avr"),
            Max = RowValues.ReadInt(row, "Max"),
            TimeTable = RowValues.Text(row, "TimeTable"),
            Region = RowValues.ReadInt(row, "Region"),
            RetrMonNumType = RowValues.ReadInt(row, "RetrMonNumType"),
            RetrMonNum = RowValues.ReadInt(row, "RetrMonNum"),
        }));
        return entries;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<MonsterPoolRow>> ToMonsterPools()
    {
        var pools = new Dictionary<string, List<MonsterPoolRow>>(StringComparer.Ordinal);
        Read(TableKinds.MonsterPool, TableRules.MonsterPrefix, (name, row) =>
        {
            var key = TableRules.MonsterPrefix + name;
            if (!pools.TryGetValue(key, out var rows)) pools[key] = rows = new List<MonsterPoolRow>();
            rows.Add(new MonsterPoolRow(RowValues.ReadInt(row, "MonsterID"), RowValues.ReadInt(row, "Avr"), RowValues.ReadInt(row, "Max"), RowValues.ReadDouble(row, "Ratio"), RowValues.ReadInt(row, "Region")));
        });
        return pools.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MonsterPoolRow>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<MapItemZoneEntry> ToMapItemZones()
    {
        var entries = new List<MapItemZoneEntry>();
        Read(TableKinds.MapItemZone, TableRules.MapItemPrefix, (name, row) => entries.Add(new MapItemZoneEntry
        {
            MapName = name,
            ItemId = RowValues.ReadInt(row, "ItemID"),
            MonsterId = RowValues.ReadInt(row, "MonsterID"),
            MinDepth = RowValues.ReadInt(row, "MinDepth"),
            MaxDepth = RowValues.ReadInt(row, "MaxDepth"),
            Avr = RowValues.ReadInt(row, "Avr"),
            Max = RowValues.ReadInt(row, "Max"),
            Priority = RowValues.ReadInt(row, "Priority"),
            TimeTable = RowValues.Text(row, "TimeTable"),
        }));
        return entries;
    }

    public IReadOnlyList<EsActionEntry> ToSkillActions()
    {
        var entries = new List<EsActionEntry>();
        Read(TableKinds.SkillAction, TableRules.SkillActionPrefix, (name, row) => entries.Add(new EsActionEntry
        {
            SkillName = name,
            Level = RowValues.ReadInt(row, "Level"),
            Ratio1 = RowValues.ReadInt(row, "Ratio1"),
            Ratio2 = RowValues.ReadInt(row, "Ratio2"),
            ApplyRatio = RowValues.ReadFloat(row, "ApplyRatio"),
        }));
        return entries;
    }

    private void Read(string kind, string prefix, Action<string, IReadOnlyDictionary<string, string>> onRow)
    {
        var wanted = TableRules.TablesOfKind(_index, kind).Select(table => table.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var table in _store.Tables)
        {
            if (!wanted.Contains(table.Name)) continue;
            var name = table.Name[prefix.Length..];
            _store.ForEachRow(table, row => onRow(name, row));
        }
    }
}
