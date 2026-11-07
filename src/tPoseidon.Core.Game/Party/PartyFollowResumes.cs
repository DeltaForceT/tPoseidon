using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Party;

public sealed class PartyFollowResumes
{
    private readonly ConcurrentDictionary<ulong, (ulong Leader, DateTimeOffset ExpiresAt)> _waiting = new();

    public void Remember(ulong followerId, ulong leaderId, DateTimeOffset expiresAt) =>
        _waiting[followerId] = (leaderId, expiresAt);

    public void Forget(ulong followerId) => _waiting.TryRemove(followerId, out _);

    public IReadOnlyList<(ulong Follower, ulong Leader)> Waiting(DateTimeOffset now)
    {
        var waiting = new List<(ulong Follower, ulong Leader)>();
        foreach (var entry in _waiting.ToArray())
        {
            if (entry.Value.ExpiresAt <= now) _waiting.TryRemove(entry.Key, out _);
            else waiting.Add((entry.Key, entry.Value.Leader));
        }
        return waiting;
    }
}
