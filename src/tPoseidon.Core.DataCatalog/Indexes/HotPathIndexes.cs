using tPoseidon.Core.Common.Indexing;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Indexes;

public sealed class MapTableIndex
{
    private readonly FirstIndex<int, MapMonsterTableEntry> _monsterTables;
    private readonly GroupIndex<int, BossSpawnTableEntry> _bossSpawns;

    public MapTableIndex(IEnumerable<MapMonsterTableEntry> monsterTables, IEnumerable<BossSpawnTableEntry> bossSpawns)
    {
        _monsterTables = new FirstIndex<int, MapMonsterTableEntry>(monsterTables, table => table.Id);
        _bossSpawns = new GroupIndex<int, BossSpawnTableEntry>(bossSpawns, entry => entry.MapId);
    }

    public string MonsterTableOf(int mapId) => _monsterTables.TryGet(mapId, out var table) ? table.MonsterTable ?? string.Empty : string.Empty;

    public int MonsterAverageOf(int mapId) => _monsterTables.TryGet(mapId, out var table) ? table.MonsterAvrNum : 0;

    public IReadOnlyList<BossSpawnTableEntry> BossSpawnsOf(int mapId) => _bossSpawns.Of(mapId);
}
