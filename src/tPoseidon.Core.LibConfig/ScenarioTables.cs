namespace tPoseidon.Core.LibConfig;

public sealed record ScenarioStep(
    int Id,
    int TimeSeconds,
    int RegionId,
    int MonsterId,
    int Result,
    int ResultValue,
    int Condition,
    int Value,
    int CheckMonsterId,
    int AllMapSkillId,
    string Comment,
    string SkillComment);

public static class ScenarioResults
{
    public const int RefillEveryFiveSeconds = 1;
    public const int RemoveRefill = 2;
    public const int RefillEverySecond = 3;
}

public static class ScenarioConditions
{
    public const int Unconditional = 0;
    public const int AliveAtLeast = 1;
    public const int AliveBelow = 2;
    public const int KillsAtLeast = 3;
    public const int TargetAliveAtLeast = 4;
    public const int TargetAliveBelow = 5;
    public const int TargetKillsAtLeast = 6;
    public const int CastAllMapSkill = 7;
    public const int PlayersAboveAlive = 8;
    public const int SpawnPerPlayer = 9;
}

public sealed record ScenarioTables(
    IReadOnlyDictionary<int, IReadOnlyList<string>> TablesOfMap,
    IReadOnlyDictionary<string, IReadOnlyList<ScenarioStep>> Steps)
{
    public const string MapTable = "MonsterScenario";
    public const string StepTablePrefix = "ScMon_";
    public const int TableSlots = 3;

    public static ScenarioTables Empty { get; } = new(
        new Dictionary<int, IReadOnlyList<string>>(),
        new Dictionary<string, IReadOnlyList<ScenarioStep>>());

    public bool HasScenario(int mapId) => TablesOfMap.ContainsKey(mapId);

    public string? TableOf(int mapId, int roll)
    {
        if (!TablesOfMap.TryGetValue(mapId, out var slots)) return null;
        var filled = slots.Count(slot => slot.Length != 0);
        if (filled == 0) return null;
        var table = slots[roll % filled];
        return table.Length == 0 ? null : table;
    }
}
