using tPoseidon.Core.DataCatalog.Store;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Maps;

public sealed class MapCatalog
{
    public const string TableName = "MapInfoEx";
    public const int FirstDrillZone = 1;
    public const int LastDrillZone = 999;

    private readonly TableStore _store;
    private readonly Dictionary<int, MapRow> _byId;

    public MapCatalog(TableStore store, TranslationOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(overrides);
        _store = store;
        Rows = ReadRows(store, overrides);
        _byId = new Dictionary<int, MapRow>(Rows.Count);
        foreach (var row in Rows) _byId[row.Id] = row;
    }

    public IReadOnlyList<MapRow> Rows { get; }

    public int Count => _byId.Count;

    public MapRow? Find(int id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<MapInfoEntry> ToMapInfo()
    {
        var entries = new List<MapInfoEntry>(Rows.Count);
        foreach (var row in Rows)
        {
            if (row.Id is < 0 or > ushort.MaxValue) continue;
            var file = row.FileName.Replace('\\', '/');
            entries.Add(new MapInfoEntry
            {
                Id = (ushort)row.Id,
                Name = row.Name,
                Md3FileName = file[(file.LastIndexOf('/') + 1)..],
                NpcTable = row.NpcTable,
                MonsterTable = row.MonsterTable,
                BossMonsterTable = row.BossMonsterTable,
                ItemTable = row.ItemTable,
                ShopTable = row.ShopTable,
                SkillShopTable = row.SkillShopTable,
                MonsterAvrNum = row.MonsterAvrNum,
                MaxDepth = row.MaxDepth,
                BossMonsterCnt = row.BossMonsterCnt,
                BossMonCheckTime = row.BossMonCheckTime,
            });
        }
        return entries;
    }

    public IReadOnlyList<MapKindEntry> ToMapKinds() =>
        Rows.Select(row => new MapKindEntry(row.Id, row.Type, row.Class, row.Option, row.ParallelZoneType, row.WorldMapRegion, row.Group)).ToArray();

    public IReadOnlyList<MapFileEntry> ToMapFiles() =>
        Rows.Select(row => new MapFileEntry(row.Id, row.Name, row.FileName)).ToArray();

    public IReadOnlyList<(int MapId, float ShopPriceRate)> ToShopPriceRates() =>
        Rows.Select(row => (row.Id, row.ShopPriceRate)).ToArray();

    public IReadOnlyList<MapMonsterTableEntry> ToMapMonsterTables() =>
        Rows.Select(row => new MapMonsterTableEntry(row.Id, row.MonsterTable, row.MonsterAvrNum)).ToArray();

    public IReadOnlyDictionary<int, int> ToTreasureZoneRegions()
    {
        var regions = new Dictionary<int, int>();
        foreach (var row in Rows) regions.TryAdd(row.Id, row.WorldMapRegion);
        return regions;
    }

    public IReadOnlyDictionary<int, DrillZoneEntry> ToDrillZones()
    {
        var zones = new Dictionary<int, DrillZoneEntry>();
        foreach (var row in Rows)
        {
            if (row.Id < FirstDrillZone || row.Id > LastDrillZone) continue;
            zones.TryAdd(row.Id, new DrillZoneEntry(row.Id, row.MinItemDepth, row.MaxItemDepth, row.ItemPercentage));
        }
        return zones;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<int>> ZonesByBossTable()
    {
        var zones = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        foreach (var row in Rows)
        {
            if (row.BossMonsterTable.Length == 0) continue;
            if (!zones.TryGetValue(row.BossMonsterTable, out var list)) zones[row.BossMonsterTable] = list = new List<int>();
            list.Add(row.Id);
        }
        return zones.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<int>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlyList<BossSpawnTableEntry> ToBossSpawnTables()
    {
        var entries = new List<BossSpawnTableEntry>();
        ReadBossTables((zones, row) =>
        {
            foreach (var zone in zones)
            {
                entries.Add(new BossSpawnTableEntry(
                    zone,
                    RowValues.ReadInt(row, "BossMonID"),
                    RowValues.ReadInt(row, "BossMonCnt"),
                    RowValues.ReadInt(row, "BossMonCheckTime"),
                    RowValues.ReadInt(row, "BossMonRegionID")));
            }
        });
        return entries;
    }

    public IReadOnlyDictionary<int, IReadOnlySet<int>> ToPartyZoneBosses()
    {
        var bosses = new Dictionary<int, HashSet<int>>();
        ReadBossTables((zones, row) =>
        {
            foreach (var zone in zones)
            {
                if (!bosses.TryGetValue(zone, out var ids)) bosses[zone] = ids = new HashSet<int>();
                ids.Add(RowValues.ReadInt(row, "BossMonID"));
            }
        });
        return bosses.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<int>)pair.Value);
    }

    private void ReadBossTables(Action<IReadOnlyList<int>, IReadOnlyDictionary<string, string>> onRow)
    {
        var zonesByTable = ZonesByBossTable();
        foreach (var table in _store.Tables)
        {
            if (!zonesByTable.TryGetValue(table.Name, out var zones)) continue;
            _store.ForEachRow(table, row => onRow(zones, row));
        }
    }

    private static List<MapRow> ReadRows(TableStore store, TranslationOverrides overrides)
    {
        var rows = new List<MapRow>(700);
        if (!store.TryFind(TableName, out var table)) return rows;
        store.ForEachRow(table, row =>
        {
            var id = RowValues.ReadInt(row, "ID");
            rows.Add(new MapRow(
                id,
                overrides.Apply(TableName, id, "Name", RowValues.Text(row, "Name")),
                RowValues.Text(row, "FileName"),
                RowValues.Text(row, "NpcTable"),
                RowValues.Text(row, "MonsterTable"),
                RowValues.Text(row, "BossMonsterTable"),
                RowValues.Text(row, "ItemTable"),
                RowValues.Text(row, "ShopTable"),
                RowValues.Text(row, "SkillShopTable"),
                RowValues.ReadInt(row, "MonsterAvrNum"),
                RowValues.ReadInt(row, "MaxDepth"),
                RowValues.ReadInt(row, "BossMonsterCnt"),
                RowValues.ReadInt(row, "BossMonCheckTime"),
                RowValues.ReadInt(row, "Type"),
                RowValues.ReadInt(row, "Class"),
                RowValues.Flags(row, "Option"),
                RowValues.ReadInt(row, "PZType"),
                RowValues.ReadInt(row, "WMRegion"),
                RowValues.ReadFloat(row, "ShopPriceRate"),
                RowValues.ReadInt(row, "MinItemDepth"),
                RowValues.ReadInt(row, "MaxItemDepth"),
                RowValues.ReadFloat(row, "ItemPercentage"),
                RowValues.ReadInt(row, "Group")));
        });
        return rows;
    }
}
