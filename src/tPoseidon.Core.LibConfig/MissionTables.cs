namespace tPoseidon.Core.LibConfig;

public sealed record MissionInfoEntry(
    int Id,
    string Name,
    int MinLevel,
    int MaxLevel,
    int MissionMinutes,
    int PlayMapId,
    int MissionType,
    int TypeLimitValue,
    int MonsterType,
    string MonsterTable);

public sealed record MissionWave(int Wave, IReadOnlyList<(int MonsterId, int RegionId)> Monsters);

public sealed record MissionTables(IReadOnlyDictionary<int, MissionInfoEntry> Missions, IReadOnlyDictionary<string, IReadOnlyList<MissionWave>> Waves)
{
    public const string InfoTable = "MissionInfo";
    public const string WaveTablePrefix = "MissionMonster_";
    public const int WaveColumns = 5;

    public static MissionTables Empty { get; } = new(new Dictionary<int, MissionInfoEntry>(), new Dictionary<string, IReadOnlyList<MissionWave>>());
}
