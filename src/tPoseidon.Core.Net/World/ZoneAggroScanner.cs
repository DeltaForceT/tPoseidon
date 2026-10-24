using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneAggroScanner : IZoneAggroScanner
{
    private readonly PlayerPositionRegistry _players;

    public ZoneAggroScanner(PlayerPositionRegistry players)
    {
        _players = players ?? throw new ArgumentNullException(nameof(players));
    }

    public ushort FindNearestHostile(MonsterEntity scanner, int radius)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        if (radius <= 0) return 0;
        var radiusSquared = (long)radius * radius;

        ushort bestId = 0;
        long bestDistSquared = long.MaxValue;

        var (mobX, mobY) = scanner.GetInterpolatedPosition(DateTimeOffset.UtcNow);
        foreach (var p in _players.OnMap(scanner.MapId))
        {
            var dx = (long)p.X - mobX;
            var dy = (long)p.Y - mobY;
            var distSquared = dx * dx + dy * dy;
            if (distSquared > radiusSquared) continue;
            if (distSquared < bestDistSquared)
            {
                bestDistSquared = distSquared;
                bestId = p.EntityId;
            }
        }
        return bestId;
    }
}
