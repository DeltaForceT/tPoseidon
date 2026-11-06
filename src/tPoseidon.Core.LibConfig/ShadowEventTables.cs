using tPoseidon.Core.Common.Indexing;

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

    private readonly SourceBoundIndex<int, ShadowRewardEntry> _byId = new(entry => entry.Id);

    private readonly SourceBoundIndex<(string Scenario, int Rank), ShadowRewardEntry> _byScenarioRank = new(entry => (entry.Scenario, entry.Rank));

    public ShadowRewardEntry? Reward(string scenario, int rank) => scenario is null ? null : _byScenarioRank.Over(Rewards, Rewards).Find((scenario, rank));

    public ShadowRewardEntry? RewardById(int id) => _byId.Over(Rewards, Rewards).Find(id);

    public int HealthCountOf(int mapId) => HarconHealthCounts.GetValueOrDefault(mapId);
}
