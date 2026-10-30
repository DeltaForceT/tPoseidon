namespace tPoseidon.Core.LibConfig;

public sealed record ShadowRewardEntry(
    int Id,
    string Scenario,
    int Rank,
    int Ap,
    int Ac,
    int Dx,
    int Ma,
    int Md,
    int Da,
    int Lk,
    int Dp,
    int Hv,
    int MonsterRespawnDecrease,
    int Group,
    string Title,
    string Comment,
    int Hours);

public sealed record ShadowEventTables(
    IReadOnlyDictionary<int, int> HarconHealthCounts,
    IReadOnlyList<ShadowRewardEntry> Rewards)
{
    public const string RewardTable = "ShadowResult";

    public static ShadowEventTables Empty { get; } = new(
        new Dictionary<int, int>(),
        Array.Empty<ShadowRewardEntry>());

    public ShadowRewardEntry? Reward(string scenario, int rank) =>
        Rewards.FirstOrDefault(entry => entry.Rank == rank && string.Equals(entry.Scenario, scenario, StringComparison.Ordinal));

    public ShadowRewardEntry? RewardById(int id) => Rewards.FirstOrDefault(entry => entry.Id == id);

    public int HealthCountOf(int mapId) => HarconHealthCounts.GetValueOrDefault(mapId);
}
