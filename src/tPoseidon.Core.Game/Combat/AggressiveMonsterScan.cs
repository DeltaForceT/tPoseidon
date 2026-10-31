using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Combat;

public readonly record struct AggroPlayer(ushort EntityId, int X, int Y);

public sealed class AggressiveMonsterScan
{
    public const int ScanIntervalMs = 500;

    private readonly object _gate = new();
    private readonly Dictionary<ushort, DateTimeOffset> _nextScanAt = new();

    public IReadOnlyList<(MonsterEntity Monster, ushort TargetEntityId)> Due(
        IEnumerable<MonsterEntity> monsters,
        IReadOnlyList<AggroPlayer> players,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(players);
        var engagements = new List<(MonsterEntity, ushort)>();
        foreach (var monster in monsters)
        {
            if (monster.Atype != MonsterAType.AggressiveMonster || monster.IsDead || monster.AiViewRange <= 0) continue;
            lock (_gate)
            {
                if (_nextScanAt.TryGetValue(monster.EntityId, out var nextAt) && now < nextAt) continue;
                _nextScanAt[monster.EntityId] = now.AddMilliseconds(ScanIntervalMs);
            }

            if (Nearest(monster, players, now) is { } target)
            {
                engagements.Add((monster, target));
            }
        }

        return engagements;
    }

    public void Forget(ushort monsterEntityId)
    {
        lock (_gate)
        {
            _nextScanAt.Remove(monsterEntityId);
        }
    }

    private static ushort? Nearest(MonsterEntity monster, IReadOnlyList<AggroPlayer> players, DateTimeOffset now)
    {
        var radiusSquared = (long)monster.AiViewRange * monster.AiViewRange;
        var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
        ushort? best = null;
        var bestDistance = long.MaxValue;
        foreach (var player in players)
        {
            var dx = (long)player.X - monsterX;
            var dy = (long)player.Y - monsterY;
            var distance = dx * dx + dy * dy;
            if (distance > radiusSquared || distance >= bestDistance) continue;
            bestDistance = distance;
            best = player.EntityId;
        }

        return best;
    }
}
