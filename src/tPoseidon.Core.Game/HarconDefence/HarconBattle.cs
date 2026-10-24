namespace tPoseidon.Core.Game.HarconDefence;

public enum HarconSide
{
    Harcon,
    Wave,
    Turret,
}

public readonly record struct HarconEngagement(ushort Attacker, ushort Target);

public sealed class HarconBattle
{
    public static readonly TimeSpan WaveChase = TimeSpan.FromMinutes(5);

    private readonly Dictionary<ushort, HarconSide> _sides = new();
    private readonly Dictionary<ushort, ulong> _builders = new();

    public HarconBattle(ushort mapId, IEnumerable<ushort> harcons, IEnumerable<ushort> waves, IEnumerable<(ushort Turret, ulong Builder)> turrets)
    {
        ArgumentNullException.ThrowIfNull(harcons);
        ArgumentNullException.ThrowIfNull(waves);
        ArgumentNullException.ThrowIfNull(turrets);
        MapId = mapId;
        foreach (var harcon in harcons) _sides[harcon] = HarconSide.Harcon;
        foreach (var wave in waves) _sides[wave] = HarconSide.Wave;
        foreach (var (turret, builder) in turrets)
        {
            _sides[turret] = HarconSide.Turret;
            _builders[turret] = builder;
        }
    }

    public ushort MapId { get; }

    public IEnumerable<ushort> Fighters => _sides.Keys;

    public HarconSide? SideOf(ushort entityId) => _sides.TryGetValue(entityId, out var side) ? side : null;

    public bool Holds(ushort entityId) => _sides.ContainsKey(entityId);

    public ulong? BuilderOf(ushort turret) => _builders.TryGetValue(turret, out var builder) ? builder : null;

    public bool AreEnemies(ushort first, ushort second) =>
        SideOf(first) is { } firstSide && SideOf(second) is { } secondSide && (firstSide == HarconSide.Wave) != (secondSide == HarconSide.Wave);

    public IEnumerable<ushort> EnemiesOf(ushort entityId) =>
        _sides.Keys.Where(other => other != entityId && AreEnemies(entityId, other));

    public bool TurnsOnAttacker(ushort victim, ushort attacker) =>
        SideOf(victim) is HarconSide.Wave or HarconSide.Turret && AreEnemies(victim, attacker);

    public IReadOnlyList<HarconEngagement> Engagements(
        Func<ushort, (int X, int Y)?> positionOf,
        Func<ushort, bool> fighting,
        Func<ushort, int> reachOf)
    {
        ArgumentNullException.ThrowIfNull(positionOf);
        ArgumentNullException.ThrowIfNull(fighting);
        ArgumentNullException.ThrowIfNull(reachOf);
        var alive = _sides
            .Select(entry => (Id: entry.Key, Side: entry.Value, At: positionOf(entry.Key)))
            .Where(entry => entry.At is not null)
            .Select(entry => (entry.Id, entry.Side, At: entry.At!.Value))
            .ToList();
        var engagements = new List<HarconEngagement>();
        foreach (var (id, side, at) in alive)
        {
            if (side == HarconSide.Harcon || fighting(id)) continue;
            var reach = side == HarconSide.Turret ? (long)reachOf(id) : long.MaxValue;
            var wanted = side == HarconSide.Turret ? HarconSide.Wave : HarconSide.Harcon;
            var target = alive
                .Where(other => other.Side == wanted)
                .Select(other => (other.Id, Distance: DistanceSquared(at, other.At)))
                .Where(other => side != HarconSide.Turret || other.Distance <= reach * reach)
                .OrderBy(other => other.Distance)
                .ThenBy(other => other.Id)
                .Select(other => (ushort?)other.Id)
                .FirstOrDefault();
            if (target is { } chosen) engagements.Add(new HarconEngagement(id, chosen));
        }
        return engagements;
    }

    private static long DistanceSquared((int X, int Y) from, (int X, int Y) to)
    {
        long dx = from.X - to.X;
        long dy = from.Y - to.Y;
        return dx * dx + dy * dy;
    }
}
