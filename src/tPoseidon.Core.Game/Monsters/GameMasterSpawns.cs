using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class GameMasterSpawnLedger
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, List<MonsterEntity>> _byGameMaster = new();

    public void Record(ulong gameMaster, MonsterEntity monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        lock (_gate)
        {
            if (!_byGameMaster.TryGetValue(gameMaster, out var spawned))
            {
                spawned = new List<MonsterEntity>();
                _byGameMaster[gameMaster] = spawned;
            }
            spawned.Add(monster);
        }
    }

    public IReadOnlyList<MonsterEntity> Take(ulong gameMaster)
    {
        lock (_gate)
        {
            return _byGameMaster.Remove(gameMaster, out var spawned) ? spawned : Array.Empty<MonsterEntity>();
        }
    }
}

public static class GameMasterSpawnArea
{
    public const int PlacementTries = 8;

    public static IReadOnlyList<(ushort X, ushort Y)> Scatter(ushort centreX, ushort centreY, ushort radius, int count, ZoneGeometry? geometry, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var spots = new List<(ushort X, ushort Y)>(count);
        for (var i = 0; i < count; i++)
        {
            spots.Add(radius == 0 ? (centreX, centreY) : Place(centreX, centreY, radius, geometry, rng));
        }
        return spots;
    }

    private static (ushort X, ushort Y) Place(ushort centreX, ushort centreY, ushort radius, ZoneGeometry? geometry, Random rng)
    {
        for (var attempt = 0; attempt < PlacementTries; attempt++)
        {
            var distance = radius * Math.Sqrt(rng.NextDouble());
            var angle = rng.NextDouble() * 2 * Math.PI;
            var x = centreX + (int)Math.Round(distance * Math.Cos(angle));
            var y = centreY + (int)Math.Round(distance * Math.Sin(angle));
            if (x < 0 || y < 0 || x > ushort.MaxValue || y > ushort.MaxValue) continue;
            if (geometry is not null)
            {
                var cellX = ZoneGeometry.CellOf(x);
                var cellY = ZoneGeometry.CellOf(y);
                if (!geometry.ContainsCell(cellX, cellY) || !geometry.IsFreeDestination(cellX, cellY)) continue;
            }
            return ((ushort)x, (ushort)y);
        }
        return (centreX, centreY);
    }
}
