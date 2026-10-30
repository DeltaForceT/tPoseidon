using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class BossLifeCycle
{
    public const int SpreadWidth = 600;
    public const int SpreadHeight = 400;
    public const int PlacementAttempts = 64;
    public const int ChildIdleDespawnMs = 100000;
    public const int SummonSlotMask = 3;
    public const int CellSize = ZoneGeometry.CellSize;

    private readonly MonsterManager _monsters;
    private readonly Func<ushort, ZoneGeometry?> _geometryOf;
    private readonly Random _rng;
    private readonly Dictionary<ushort, long> _lastTickMs = new();
    private readonly Dictionary<ushort, Family> _families = new();

    public BossLifeCycle(MonsterManager monsters, Func<ushort, ZoneGeometry?>? geometryOf = null, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        _monsters = monsters;
        _geometryOf = geometryOf ?? (static _ => null);
        _rng = rng ?? new Random();
    }

    public IReadOnlyList<ushort> ChildrenOf(ushort bossEntityId) =>
        _families.TryGetValue(bossEntityId, out var family) ? family.Children.ToArray() : Array.Empty<ushort>();

    public void Tick(long nowMs)
    {
        var live = new HashSet<ushort>();
        foreach (var boss in _monsters.All)
        {
            if (boss.Boss is not { } life || boss.IsDead) continue;
            live.Add(boss.EntityId);
            var elapsed = _lastTickMs.TryGetValue(boss.EntityId, out var last) ? (int)Math.Min(nowMs - last, int.MaxValue) : 0;
            _lastTickMs[boss.EntityId] = nowMs;
            var engaged = boss.SnapshotAttackers().Count > 0;
            var family = FamilyOf(boss.EntityId);
            family.Children.RemoveAll(child => _monsters.Get(child) is not { IsDead: false });
            var called = boss.TickBoss(
                nowMs, elapsed, engaged, _monsters.GetByMap(boss.MapId).Count, family.Children.Count, _rng.Next(2) == 0);
            if (called) Summon(boss, life.Profile, family);
            boss.TickBossTimers(elapsed, engaged, boss.Atype != MonsterAType.DoppelGanger, NextRoll);
            DespawnWhenIdle(boss, life.Profile, family, elapsed, engaged);
        }
        foreach (var gone in _families.Keys.Where(id => !live.Contains(id)).ToArray())
        {
            Despawn(gone, _families[gone]);
            _families.Remove(gone);
            _lastTickMs.Remove(gone);
        }
        foreach (var stale in _lastTickMs.Keys.Where(id => !live.Contains(id)).ToArray()) _lastTickMs.Remove(stale);
    }

    private int NextRoll() => _rng.Next();

    private Family FamilyOf(ushort bossEntityId)
    {
        if (!_families.TryGetValue(bossEntityId, out var family))
        {
            family = new Family();
            _families[bossEntityId] = family;
        }
        return family;
    }

    private void Summon(MonsterEntity boss, BossLifeProfile profile, Family family)
    {
        var slots = profile.SummonMonsters;
        if (slots is { Count: > 0 } && profile.SummonMax > 0)
        {
            var count = Math.Clamp(_rng.Next(profile.SummonMax), profile.SummonMin, profile.SummonMax);
            var shared = slots[_rng.Next(int.MaxValue) & SummonSlotMask];
            for (var i = 0; i < count; i++)
            {
                if (_monsters.GetByMap(boss.MapId).Count >= BossLife.SummonMapMonsterLimit
                    || family.Children.Count >= profile.SummonTotalCount)
                {
                    continue;
                }
                var type = profile.SummonBType == 0 ? slots[_rng.Next(int.MaxValue) & SummonSlotMask] : shared;
                if (!TryPlace(boss, out var x, out var y)) continue;
                if (_monsters.Spawn(type, boss.MapId, x, y) is not { } child) continue;
                family.Children.Add(child.EntityId);
                _monsters.BossEvents.Enqueue(new BossEvent(
                    BossEventKind.ChildSpawned, child.EntityId, boss.MapId, 0, false, string.Empty, boss.EntityId));
            }
        }
        _monsters.BossEvents.Enqueue(new BossEvent(
            BossEventKind.Spoke, boss.EntityId, boss.MapId, 0, false, profile.SummonSpeech));
    }

    private bool TryPlace(MonsterEntity boss, out ushort x, out ushort y)
    {
        var geometry = _geometryOf(boss.MapId);
        var (bossX, bossY) = boss.GetInterpolatedPosition(DateTimeOffset.UtcNow);
        x = 0;
        y = 0;
        for (var attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            var candidateX = bossX - SpreadWidth / 2 + _rng.Next(SpreadWidth);
            var candidateY = bossY - SpreadHeight / 2 + _rng.Next(SpreadHeight);
            if (candidateX < 0 || candidateY < 0 || candidateX > ushort.MaxValue || candidateY > ushort.MaxValue) continue;
            if (geometry is not null)
            {
                var cellX = candidateX / CellSize;
                var cellY = candidateY / CellSize;
                if (!geometry.ContainsCell(cellX, cellY) || geometry.IsBlocked(cellX, cellY)) continue;
            }
            x = (ushort)candidateX;
            y = (ushort)candidateY;
            return true;
        }
        return false;
    }

    private void DespawnWhenIdle(MonsterEntity boss, BossLifeProfile profile, Family family, int elapsed, bool engaged)
    {
        if (family.Children.Count == 0 || boss.Boss!.BarsLeft != profile.LifeCount) return;
        family.IdleMs = engaged ? 0 : family.IdleMs + elapsed;
        if (family.IdleMs < ChildIdleDespawnMs) return;
        family.IdleMs = 0;
        Despawn(boss.EntityId, family);
    }

    private void Despawn(ushort bossEntityId, Family family)
    {
        foreach (var child in family.Children)
        {
            if (_monsters.Get(child) is not { } entity) continue;
            _monsters.BossEvents.Enqueue(new BossEvent(
                BossEventKind.ChildDespawned, child, entity.MapId, 0, false, string.Empty, bossEntityId));
        }
        family.Children.Clear();
    }

    private sealed class Family
    {
        public List<ushort> Children { get; } = new();
        public int IdleMs { get; set; }
    }
}
