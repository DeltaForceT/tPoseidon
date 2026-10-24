namespace tPoseidon.Core.LibConfig;

public interface IZoneMonsterPoolLookup
{
    ZoneMonsterPool? GetByMap(ushort mapId);
    IReadOnlyCollection<ushort> KnownMaps { get; }
    int TotalPoolRows { get; }
}

public sealed record ZoneMonsterPool
{
    public required ushort MapId { get; init; }
    public required string MapName { get; init; }
    public required string MonsterTable { get; init; }
    public required int MonsterAvrNum { get; init; }
    public required IReadOnlyList<ZoneMonsterPoolEntry> Pool { get; init; }
}

public sealed record ZoneMonsterPoolEntry
{
    public required int MonsterID { get; init; }
    public required double Ratio { get; init; }
    public required int Avr { get; init; }
    public required int Max { get; init; }
}
