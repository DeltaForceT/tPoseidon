using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.PvP;

public sealed class PvPGameManager
{
    private readonly ConcurrentDictionary<int, PvPGame> _byGameId = new();
    private int _nextGameId;

    public PvPGame Create(string gameMode)
    {
        var gameId = Interlocked.Increment(ref _nextGameId);
        var game = new PvPGame
        {
            GameId = gameId,
            Mode = gameMode,
            State = PvPGameState.Lobby,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _byGameId[gameId] = game;
        return game;
    }

    public PvPGame? Get(int gameId)
        => _byGameId.TryGetValue(gameId, out var v) ? v : null;

    public bool End(int gameId)
    {
        if (_byGameId.TryGetValue(gameId, out var g))
        {
            g.State = PvPGameState.Finished;
            return true;
        }
        return false;
    }

    public int ActiveGameCount => _byGameId.Count;
}

public enum PvPGameState
{
    Lobby,
    InProgress,
    Finished,
}

public sealed class PvPGame
{
    public required int GameId { get; init; }
    public required string Mode { get; init; }
    public required PvPGameState State { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
}
