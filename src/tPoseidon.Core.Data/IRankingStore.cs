namespace tPoseidon.Core.Data;

public sealed record RankingRow(int RankingOrder, ulong CharacterId, int CharType, string CharName, int Value1, int Value2, int Value3);

public interface IRankingStore
{
    int RebuildMonsterRanks(DateTime weekStart, DateTime weekEnd, int topCount);

    IReadOnlyList<RankingRow> Top(int rankingType, int monsterId);
}
