using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed record EscortReachedEnd(ushort EntityId, ushort DestinationZone) : MonsterWanderEvent(EntityId);

public sealed class EscortWalkCycle
{
    public const int EscortedNpcType = 12;
    public const int StartStandMs = 10000;
    public const int WaitMinMs = 3000;
    public const int WaitMaxMs = 6000;
    public const int RetryMinMs = 1000;
    public const int RetryMaxMs = 2000;
    public const int ArrivalNoticeDelayMs = 300;
    public const int PostResultWarpDelayMs = 10000;

    private const double MillisecondsPerSecond = 1000.0;

    private readonly IMonsterParamLookup _catalog;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Slot> _slots = new();

    public EscortWalkCycle(IMonsterParamLookup catalog, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _rng = rng ?? new Random();
    }

    public static IReadOnlyList<ZonePoint> WaypointsOf(ZoneGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return geometry.PointsOfType(ZoneGeometry.PointTypeEscortWaypoint).OrderBy(point => point.Id).ToArray();
    }

    public static bool IsEscortZone(ZoneGeometry? geometry) =>
        geometry is not null && geometry.PointsOfType(ZoneGeometry.PointTypeEscortWaypoint).Count > 0;

    public static ushort SuccessZoneOf(ZoneGeometry geometry)
    {
        var waypoints = WaypointsOf(geometry);
        return waypoints.Count == 0 ? (ushort)0 : (ushort)waypoints[^1].Param;
    }

    public IReadOnlyList<MonsterWanderEvent> Tick(ZoneGeometry geometry, IReadOnlyCollection<MonsterEntity> escorts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(escorts);
        if (escorts.Count == 0) return Array.Empty<MonsterWanderEvent>();

        var waypoints = WaypointsOf(geometry);
        var events = new List<MonsterWanderEvent>();
        if (waypoints.Count == 0) return events;

        lock (_gate)
        {
            foreach (var escort in escorts)
            {
                if (escort.IsDead)
                {
                    _slots.Remove(escort.EntityId);
                    continue;
                }

                if (!_slots.TryGetValue(escort.EntityId, out var slot))
                {
                    _slots[escort.EntityId] = new Slot { NextAt = now + TimeSpan.FromMilliseconds(StartStandMs + _rng.Next(WaitMinMs, WaitMaxMs)) };
                    continue;
                }
                if (slot.Finished) continue;

                if (slot.Moving)
                {
                    while (slot.PendingLegs.Count > 0 && now >= slot.PendingLegs.Peek().At)
                    {
                        var leg = slot.PendingLegs.Dequeue();
                        events.Add(new MonsterMoveStarted(escort.EntityId, leg.FromX, leg.FromY, leg.ToX, leg.ToY));
                    }
                    if (now < slot.ArrivesAt) continue;
                    slot.Moving = false;
                    slot.Index++;
                    slot.NextAt = now + TimeSpan.FromMilliseconds(_rng.Next(WaitMinMs, WaitMaxMs));
                    events.Add(new MonsterArrived(escort.EntityId));
                    continue;
                }

                if (now < slot.NextAt) continue;
                if (slot.Index >= waypoints.Count)
                {
                    slot.Finished = true;
                    events.Add(new EscortReachedEnd(escort.EntityId, SuccessZoneOf(geometry)));
                    continue;
                }

                StartMove(geometry, escort, slot, waypoints[slot.Index], now, events);
            }
        }
        return events;
    }

    public int IndexOf(ushort entityId)
    {
        lock (_gate)
        {
            return _slots.TryGetValue(entityId, out var slot) ? slot.Index : 0;
        }
    }

    public void Forget(ushort entityId)
    {
        lock (_gate)
        {
            _slots.Remove(entityId);
        }
    }

    private void StartMove(
        ZoneGeometry geometry,
        MonsterEntity escort,
        Slot slot,
        ZonePoint waypoint,
        DateTimeOffset now,
        List<MonsterWanderEvent> events)
    {
        var (fromX, fromY) = escort.GetInterpolatedPosition(now);
        var definition = _catalog.GetById(escort.MonsterParamId);
        var route = definition is null || definition.WalkSpeed <= 0
            ? null
            : WanderRoute.FindToPoint(geometry, fromX, fromY, waypoint.X, waypoint.Y);

        if (route is null || definition is null)
        {
            slot.NextAt = now + TimeSpan.FromMilliseconds(_rng.Next(RetryMinMs, RetryMaxMs));
            events.Add(new MonsterMoveRefused(escort.EntityId, fromX, fromY));
            return;
        }

        var walkSpeed = ClientSpeed.Drawn(escort.WalkSpeedOf(definition.WalkSpeed));
        if (walkSpeed <= 0f) return;
        var travelMs = (int)Math.Ceiling(route.Length / walkSpeed * MillisecondsPerSecond);
        escort.MoveAlong(route.Points, now, travelMs);

        if (escort.TakeOrder(MonsterWanderCycle.WalkMoveType, walkSpeed))
        {
            events.Add(new MonsterSpeedChanged(escort.EntityId, walkSpeed));
        }
        slot.Moving = true;
        slot.ArrivesAt = now + TimeSpan.FromMilliseconds(travelMs + ArrivalNoticeDelayMs);
        slot.PendingLegs.Clear();
        var legs = WanderRoute.Legs(geometry, fromX, fromY, route.Points);
        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            var started = new MonsterMoveStarted(escort.EntityId, (ushort)leg.FromX, (ushort)leg.FromY, (ushort)leg.ToX, (ushort)leg.ToY);
            if (i == 0) events.Add(started);
            else
            {
                var at = now + TimeSpan.FromMilliseconds(Math.Ceiling(leg.StartDistance / walkSpeed * MillisecondsPerSecond));
                slot.PendingLegs.Enqueue(new PendingLeg(at, started.FromX, started.FromY, started.ToX, started.ToY));
            }
        }
    }

    private sealed class Slot
    {
        public int Index;
        public bool Moving;
        public bool Finished;
        public DateTimeOffset NextAt;
        public DateTimeOffset ArrivesAt;
        public Queue<PendingLeg> PendingLegs { get; } = new();
    }

    private readonly record struct PendingLeg(DateTimeOffset At, ushort FromX, ushort FromY, ushort ToX, ushort ToY);
}
