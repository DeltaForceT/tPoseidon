namespace tPoseidon.Core.Data;

public sealed record MonsterHuntRecord
{
    public required ulong CharacterId { get; init; }
    public required int QuestId { get; init; }
    public int Grade { get; init; }
    public bool Active { get; init; }
    public int RemainingMs { get; init; }
    public int AccumulatorMs { get; init; }
    public int Kills { get; init; }
    public int Target { get; init; }
}
