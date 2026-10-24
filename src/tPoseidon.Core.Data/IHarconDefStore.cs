namespace tPoseidon.Core.Data;

public sealed record HarconDefRecord(int Coins, int EntryCount, DateOnly? EntryDay, IReadOnlySet<int> Cleared)
{
    public static HarconDefRecord Empty { get; } = new(0, 0, null, new HashSet<int>());
}

public sealed record HarconDefTotals(int Plays, int Wins, int Losses, IReadOnlyList<(int StageId, int Clears)> TopStages)
{
    public const int ShownStages = 3;

    public static HarconDefTotals Empty { get; } = new(0, 0, 0, Array.Empty<(int StageId, int Clears)>());
}

public sealed record HarconStageRecord(
    int StageId,
    int Plays,
    int Wins,
    int Losses,
    long TotalScore,
    float ContributionSum,
    int BestScore,
    int BestRank,
    float BestContribution,
    int BestDeaths,
    int BestTowerScore,
    int BestTowersBuilt)
{
    public float AverageContribution => Plays > 0 ? ContributionSum / Plays : 0f;

    public static HarconStageRecord Empty(int stageId) => new(stageId, 0, 0, 0, 0, 0f, 0, 0, 0f, 0, 0, 0);
}

public readonly record struct HarconStageResult(
    int StageId, bool Won, int Score, int Rank, float Contribution, int Deaths, int TowerScore, int TowersBuilt);

public interface IHarconDefStore
{
    HarconStageRecord StageRecord(ulong characterId, int stageId);

    void AddStageResult(ulong characterId, HarconStageResult result);

    HarconDefTotals Totals(ulong characterId);

    void AddResult(ulong characterId, bool won);

    HarconDefRecord Load(ulong characterId);

    void SaveEntries(ulong characterId, int count, DateOnly day);

    int AddCoins(ulong characterId, int delta);

    void AddClear(ulong characterId, int stageId);
}
