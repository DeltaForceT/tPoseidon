using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Guild;

public sealed class GuildGameManager
{
    private readonly ConcurrentDictionary<int, GuildGame> _byMatchId = new();
    private int _nextMatchId;

    public GuildGame CreateMatch(int homeGuildId, int awayGuildId)
    {
        var matchId = Interlocked.Increment(ref _nextMatchId);
        var match = new GuildGame
        {
            MatchId = matchId,
            HomeGuildId = homeGuildId,
            AwayGuildId = awayGuildId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _byMatchId[matchId] = match;
        return match;
    }

    public GuildGame? Get(int matchId)
        => _byMatchId.TryGetValue(matchId, out var v) ? v : null;

    public int ActiveMatchCount => _byMatchId.Count;
}

public sealed record GuildGame
{
    public required int MatchId { get; init; }
    public required int HomeGuildId { get; init; }
    public required int AwayGuildId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
