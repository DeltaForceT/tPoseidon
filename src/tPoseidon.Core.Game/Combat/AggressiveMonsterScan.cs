using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;

namespace tPoseidon.Core.Game.Combat;

public readonly record struct AggroPlayer(ushort EntityId, int X, int Y, bool SeesShadowWorld = false);

public sealed class AggressiveMonsterScan
{
    public const int ScanIntervalMs = 500;
    public const int ViewRange = 200;

    private readonly object _gate = new();
    private readonly Dictionary<ushort, DateTimeOffset> _nextScanAt = new();

    public IReadOnlyList<(MonsterEntity Monster, ushort TargetEntityId)> Due(
        IEnumerable<MonsterEntity> monsters,
        IReadOnlyList<AggroPlayer> players,
        DateTimeOffset now,
        Func<MonsterEntity, AggroPlayer, bool>? ignores = null)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(players);
        var engagements = new List<(MonsterEntity, ushort)>();
        foreach (var monster in monsters)
        {
            if (!Scans(monster.Atype) || monster.IsDead) continue;
            lock (_gate)
            {
                if (_nextScanAt.TryGetValue(monster.EntityId, out var nextAt) && now < nextAt) continue;
                _nextScanAt[monster.EntityId] = now.AddMilliseconds(ScanIntervalMs);
            }

            if (Nearest(monster, players, now, ignores) is { } target)
            {
                engagements.Add((monster, target));
            }
        }

        return engagements;
    }

    public static bool Scans(MonsterAType atype) =>
        atype is MonsterAType.AggressiveMonster or MonsterAType.BossMonster or MonsterAType.NonresidentBossMonster;

    public static int RangeOf(MonsterEntity monster) => ViewRange;

    public void Forget(ushort monsterEntityId)
    {
        lock (_gate)
        {
            _nextScanAt.Remove(monsterEntityId);
        }
    }

    private static ushort? Nearest(MonsterEntity monster, IReadOnlyList<AggroPlayer> players, DateTimeOffset now, Func<MonsterEntity, AggroPlayer, bool>? ignores)
    {
        var range = RangeOf(monster);
        var radiusSquared = (long)range * range;
        var (monsterX, monsterY) = monster.GetInterpolatedPosition(now);
        ushort? best = null;
        var bestDistance = long.MaxValue;
        var shadow = ShadowActors.IsMonster(monster.MonsterParamId);
        foreach (var player in players)
        {
            if (shadow && !player.SeesShadowWorld) continue;
            if (ignores is not null && ignores(monster, player)) continue;
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
