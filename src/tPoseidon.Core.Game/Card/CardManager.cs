using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Card;

public sealed class CardManager
{
    private readonly ConcurrentDictionary<int, CardBattle> _byBattleId = new();
    private int _nextBattleId;

    public CardBattle CreateBattle(ulong player1, ulong player2)
    {
        var battleId = Interlocked.Increment(ref _nextBattleId);
        var battle = new CardBattle
        {
            BattleId = battleId,
            Player1 = player1,
            Player2 = player2,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _byBattleId[battleId] = battle;
        return battle;
    }

    public CardBattle? Get(int battleId)
        => _byBattleId.TryGetValue(battleId, out var v) ? v : null;

    public int ActiveBattleCount => _byBattleId.Count;
}

public sealed record CardBattle
{
    public required int BattleId { get; init; }
    public required ulong Player1 { get; init; }
    public required ulong Player2 { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
