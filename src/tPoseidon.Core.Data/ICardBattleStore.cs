namespace tPoseidon.Core.Data;

public sealed record CardBattleStats(int WinPoint, int WinCount, int BattleCount, int Dis, int SpecialDis, int TotalBattleToday, int OtherBattleToday)
{
    public int Grade { get; init; }

    public int Lose => BattleCount - WinCount;

    public static CardBattleStats Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);
}

public interface ICardBattleStore
{
    CardBattleStats Load(ulong characterId, ulong otherId);

    void Record(ulong characterId, ulong otherId, bool won, int winPointChange);

    void RecordClosed(ulong characterId, ulong otherId, int disIncrease, int specialDisIncrease);
}
