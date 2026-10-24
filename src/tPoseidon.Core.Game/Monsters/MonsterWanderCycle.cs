using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public abstract record MonsterWanderEvent(ushort EntityId);

public sealed record MonsterMoveStarted(ushort EntityId, ushort FromX, ushort FromY, ushort ToX, ushort ToY)
    : MonsterWanderEvent(EntityId);

public sealed record MonsterMoveRefused(ushort EntityId, ushort X, ushort Y) : MonsterWanderEvent(EntityId);

public sealed record MonsterArrived(ushort EntityId) : MonsterWanderEvent(EntityId);

public sealed record MonsterSpeedChanged(ushort EntityId, float Speed) : MonsterWanderEvent(EntityId);

public sealed class MonsterWanderCycle
{
    public const int WaitMinMs = 3000;
    public const int WaitMaxMs = 6000;
    public const int RetryMinMs = 1000;
    public const int RetryMaxMs = 2000;

    public const int StepMin = 60;
    public const int StepLimit = 250;
    public const int StepLimitInsideRegion = 350;
    public const int PortalClearance = 32;

    public const int MoveRatioPercent = 10;
    public const byte WalkMoveType = 1;

    public static readonly TimeSpan LongestCountedTick = TimeSpan.FromSeconds(1);

    private const int PercentScale = 100;
    private const int SignChoices = 2;
    private const double MillisecondsPerSecond = 1000.0;

    private readonly IMonsterParamLookup _catalog;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Slot> _slots = new();

    public MonsterWanderCycle(IMonsterParamLookup catalog, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _rng = rng ?? new Random();
    }

    public IReadOnlyList<MonsterWanderEvent> Tick(
        ZoneGeometry geometry,
        IReadOnlyCollection<MonsterEntity> monsters,
        IReadOnlyCollection<(int X, int Y)> players,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(players);
        if (monsters.Count == 0) return Array.Empty<MonsterWanderEvent>();

        var sectors = new SectorGrid(geometry);
        var watched = sectors.SectorsWatchedFrom(players);
        var events = new List<MonsterWanderEvent>();

        lock (_gate)
        {
            foreach (var monster in monsters)
            {
                if (monster.IsDead || monster.SnapshotAttackers().Count != 0)
                {
                    Interrupt(monster, now);
                    continue;
                }

                if (!_slots.TryGetValue(monster.EntityId, out var slot))
                {
                    _slots[monster.EntityId] = new Slot { CountdownMs = WaitMinMs, LastTick = now };
                    continue;
                }

                var elapsed = now - slot.LastTick;
                if (elapsed > LongestCountedTick) elapsed = LongestCountedTick;
                slot.LastTick = now;

                if (slot.Moving)
                {
                    if (now < slot.ArrivesAt) continue;
                    slot.Moving = false;
                    slot.CountdownMs = _rng.Next(WaitMinMs, WaitMaxMs);
                    events.Add(new MonsterArrived(monster.EntityId));
                    continue;
                }

                var playerNear = watched.Contains(sectors.SectorOf(monster.CurrentX, monster.CurrentY));
                if (!playerNear && _rng.Next(PercentScale) >= MoveRatioPercent) continue;

                slot.CountdownMs -= (int)elapsed.TotalMilliseconds;
                if (slot.CountdownMs >= 1) continue;

                StartMove(geometry, monster, slot, now, events);
            }
        }
        return events;
    }

    public void Forget(ushort entityId)
    {
        lock (_gate)
        {
            _slots.Remove(entityId);
        }
    }

    private void Interrupt(MonsterEntity monster, DateTimeOffset now)
    {
        if (!_slots.Remove(monster.EntityId, out var slot)) return;
        if (!slot.Moving || monster.IsDead) return;

        var (x, y) = monster.GetInterpolatedPosition(now);
        monster.InitPosition(x, y);
    }

    private void StartMove(
        ZoneGeometry geometry,
        MonsterEntity monster,
        Slot slot,
        DateTimeOffset now,
        List<MonsterWanderEvent> events)
    {
        var (fromX, fromY) = monster.GetInterpolatedPosition(now);
        var (toX, toY) = ChooseDestination(geometry, monster, fromX, fromY);

        var definition = _catalog.GetById(monster.MonsterParamId);
        var route = definition is null || definition.WalkSpeed <= 0 || definition.RunSpeed <= 0
            ? null
            : WanderRoute.Find(geometry, fromX, fromY, toX, toY);

        if (route is null || definition is null)
        {
            slot.CountdownMs = _rng.Next(RetryMinMs, RetryMaxMs);
            events.Add(new MonsterMoveRefused(monster.EntityId, fromX, fromY));
            return;
        }

        var walkSpeed = monster.WalkSpeedOf(definition.WalkSpeed);
        if (walkSpeed <= 0f || monster.RunSpeedOf(definition.RunSpeed) <= 0f) return;
        var travelMs = (int)Math.Ceiling(route.Length / walkSpeed * MillisecondsPerSecond);
        var destinationX = (ushort)route.DestinationX;
        var destinationY = (ushort)route.DestinationY;
        monster.MoveAlong(route.Points, now, travelMs);

        if (monster.TakeOrder(WalkMoveType, walkSpeed))
        {
            events.Add(new MonsterSpeedChanged(monster.EntityId, walkSpeed));
        }
        slot.Moving = true;
        slot.ArrivesAt = now + TimeSpan.FromMilliseconds(travelMs);
        events.Add(new MonsterMoveStarted(monster.EntityId, fromX, fromY, destinationX, destinationY));
    }

    private (int X, int Y) ChooseDestination(ZoneGeometry geometry, MonsterEntity monster, int fromX, int fromY)
    {
        var region = geometry.FindRegion(ZoneGeometry.RegionTypeMonsterMovement, monster.RegionId);
        var insideRegion = region is { } found && found.X1 != found.X2 && found.Y1 != found.Y2;
        var limit = insideRegion ? StepLimitInsideRegion : StepLimit;

        var stepY = _rng.Next(StepMin, limit);
        var stepX = _rng.Next(StepMin, limit);
        if (_rng.Next(SignChoices) != 0) stepX = -stepX;
        if (_rng.Next(SignChoices) != 0) stepY = -stepY;

        var x = fromX + stepX;
        var y = fromY + stepY;
        if (geometry.OverlapsRegionOfType(
                ZoneGeometry.RegionTypePortal,
                x - PortalClearance,
                y - PortalClearance,
                x + PortalClearance,
                y + PortalClearance))
        {
            x = fromX - stepX;
            y = fromY - stepY;
        }

        return insideRegion
            ? (Confine(x, region!.Value.X1, region.Value.X2), Confine(y, region.Value.Y1, region.Value.Y2))
            : (Confine(x, geometry.MinX, geometry.MaxX), Confine(y, geometry.MinY, geometry.MaxY));
    }

    private static int Confine(int value, int low, int high)
    {
        if (value < low) return low;
        if (high < value) return high;
        return value;
    }

    private sealed class Slot
    {
        public int CountdownMs;
        public DateTimeOffset LastTick;
        public bool Moving;
        public DateTimeOffset ArrivesAt;
    }
}
