namespace tPoseidon.Core.LibConfig;

public interface IMonsterSpawnProvider
{
    IReadOnlyList<MonsterSpawnPoint> GetSpawnPoints(ushort mapId);
    IReadOnlyCollection<ushort> KnownMaps { get; }
    int TotalPoints { get; }
}

public sealed record MonsterSpawnPoint
{
    public required int Id { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
}
