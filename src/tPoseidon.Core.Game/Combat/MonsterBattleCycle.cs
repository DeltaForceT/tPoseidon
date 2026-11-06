using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public enum MonsterBattleLeaveReason
{
    TargetGone,
    OutOfChaseRange,
    ChaseTimeSpent,
    ApproachRefused,
    TargetNotVisible,
    TargetUnreachable,
    DestinationsCrowded,
}

public abstract record MonsterBattleEvent(ushort EntityId);

public sealed record MonsterBattleHalted(ushort EntityId, ushort X, ushort Y, byte MoveType)
    : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleSpeedChanged(ushort EntityId, float Speed) : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleMoved(
    ushort EntityId,
    ushort FromX,
    ushort FromY,
    ushort ToX,
    ushort ToY,
    byte MoveType) : MonsterBattleEvent(EntityId);

public enum MonsterBattleReachKind
{
    Swing,
    CloseIn,
    BackOff,
}

public readonly record struct MonsterBattleReachSample(
    ushort EntityId,
    MonsterBattleReachKind Kind,
    int Distance,
    int MinRange,
    int MaxRange,
    int AimRadius);

public sealed record MonsterBattleSwung(
    ushort EntityId,
    ushort X,
    ushort Y,
    ushort TargetEntityId,
    ushort TargetX,
    ushort TargetY,
    long Damage,
    byte Result,
    bool Fatal,
    ushort GuardLeft = 0) : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleLeft(ushort EntityId, MonsterBattleLeaveReason Reason)
    : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleCast(ushort EntityId, ushort TargetEntityId, int SkillId, int Level, string Speech)
    : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleCastStarted(ushort EntityId, ushort TargetEntityId, int SkillId, int Level)
    : MonsterBattleEvent(EntityId);

public readonly record struct MonsterBattleTarget(
    ushort X,
    ushort Y,
    int Dp,
    int Hv,
    int Lk,
    int Level,
    long Hp,
    bool Protected = false,
    long MaxHp = 0,
    int Job = 0,
    bool DefenceParalysed = false);

public sealed class MonsterBattleCycle
{
    public const byte StoredMoveType = MeleeApproach.DefaultMoveType;
    public const int DefaultChaseTimeMs = 3000;
    public const int UnreachableSameSpotOrders = 10;
    public const int MaxDestinationShifts = 5;
    public const int MaxReapproachFailures = 3;
    public static readonly TimeSpan UnreachableMemory = TimeSpan.FromSeconds(20);

    public static readonly TimeSpan LongestCountedTick = TimeSpan.FromSeconds(1);

    private const double MillisecondsPerSecond = 1000.0;
    private const float ChaseSpeedDivisor = 2f;

    private readonly IMonsterParamLookup _catalog;
    private readonly IReadOnlyDictionary<int, CharacterCombatInfo> _reachByType;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Fight> _fights = new();
    private readonly Dictionary<ushort, UnreachableSpot> _unreachable = new();
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<MonsterEntity, MonsterTactics> _timedTactics = new();
    private readonly Func<int, IReadOnlyList<MonsterTacticEntry>>? _tacticsOf;
    private readonly Func<int, int, bool>? _castable;
    private readonly Func<int, int, MonsterSkillTiming>? _skillTimingOf;
    private readonly Dictionary<(ushort Monster, int Skill), DateTimeOffset> _skillCooling = new();
    private readonly Func<MonsterEntity, int?>? _attackerLimitOf;

    public MonsterBattleCycle(
        IMonsterParamLookup catalog,
        IReadOnlyDictionary<int, CharacterCombatInfo> reachByType,
        Random? rng = null,
        Func<int, IReadOnlyList<MonsterTacticEntry>>? tacticsOf = null,
        Func<int, int, bool>? castable = null,
        Func<MonsterEntity, int?>? attackerLimitOf = null,
        Func<int, int, MonsterSkillTiming>? skillTimingOf = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reachByType);
        _catalog = catalog;
        _reachByType = reachByType;
        _rng = rng ?? new Random();
        _tacticsOf = tacticsOf;
        _castable = castable;
        _attackerLimitOf = attackerLimitOf;
        _skillTimingOf = skillTimingOf;
    }

    public bool AtAttackerLimit(MonsterEntity monster, ushort targetEntityId)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (_attackerLimitOf is null) return false;
        var limit = _attackerLimitOf(monster);
        lock (_gate)
        {
            var others = _fights.Count(pair => pair.Key != monster.EntityId && pair.Value.TargetEntityId == targetEntityId);
            return MonsterAttackerLimit.Exceeded(limit, others);
        }
    }

    public static int ChaseTimeMs(MonsterParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var chaseTimeMs = (int)(definition.ChaseTime * MillisecondsPerSecond);
        return chaseTimeMs < 1 ? DefaultChaseTimeMs : chaseTimeMs;
    }

    public bool IsFighting(ushort monsterEntityId)
    {
        lock (_gate)
        {
            return _fights.ContainsKey(monsterEntityId);
        }
    }

    public ushort? TargetOf(ushort monsterEntityId)
    {
        lock (_gate)
        {
            return _fights.TryGetValue(monsterEntityId, out var fight) ? fight.TargetEntityId : null;
        }
    }

    public void Forget(ushort monsterEntityId)
    {
        lock (_gate)
        {
            _fights.Remove(monsterEntityId);
            _unreachable.Remove(monsterEntityId);
            foreach (var key in _skillCooling.Keys.Where(key => key.Monster == monsterEntityId).ToArray()) _skillCooling.Remove(key);
        }
    }

    public bool IgnoresSpot(ushort monsterEntityId, int x, int y, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_unreachable.TryGetValue(monsterEntityId, out var spot)) return false;
            if (now >= spot.Until)
            {
                _unreachable.Remove(monsterEntityId);
                return false;
            }
            return spot.X == x && spot.Y == y;
        }
    }

    public void HoldFor(MonsterEntity monster, DateTimeOffset now, int skillMs)
    {
        ArgumentNullException.ThrowIfNull(monster);
        var definition = _catalog.GetById(monster.MonsterParamId);
        lock (_gate)
        {
            if (definition is null
                || !_reachByType.TryGetValue(monster.MonsterParamId, out var reach)
                || !_fights.TryGetValue(monster.EntityId, out var fight)) return;
            var until = now.AddMilliseconds(skillMs + reach.MonsterSwingDelayMs(definition.DxLv + monster.StatDelta(MonsterStat.Dx)));
            if (until > fight.NextSwingAt) fight.NextSwingAt = until;
        }
    }

    public Action<MonsterBattleReachSample>? Observer { get; set; }

    public MonsterBattleHalted? Engage(MonsterEntity monster, ushort targetEntityId, DateTimeOffset now, TimeSpan? chase = null)
    {
        ArgumentNullException.ThrowIfNull(monster);

        lock (_gate)
        {
            if (monster.IsDead || _fights.ContainsKey(monster.EntityId)) return null;
            var definition = _catalog.GetById(monster.MonsterParamId);
            if (definition is null || !_reachByType.ContainsKey(monster.MonsterParamId)) return null;

            var (x, y) = monster.GetInterpolatedPosition(now);
            monster.InitPosition(x, y);
            _fights[monster.EntityId] = new Fight
            {
                MapId = monster.MapId,
                TargetEntityId = targetEntityId,
                NextSwingAt = now,
                LastTick = now,
                ChaseBudgetMs = chase is { } chaseFor ? (int)Math.Clamp(chaseFor.TotalMilliseconds, 1, int.MaxValue) : ChaseTimeMs(definition),
                Tactics = TacticsOf(monster),
            };
            var haltedType = monster.MoveType == MeleeApproach.DefaultMoveType
                ? MeleeApproach.WalkMoveType
                : monster.MoveType;
            return new MonsterBattleHalted(monster.EntityId, x, y, haltedType);
        }
    }

    private MonsterTactics? TacticsOf(MonsterEntity monster)
    {
        if (_tacticsOf?.Invoke(monster.MonsterParamId) is not { Count: > 0 } tactics) return null;
        if (!tactics.Any(MonsterTactics.IsTimed)) return new MonsterTactics(tactics);
        return _timedTactics.GetValue(monster, _ => new MonsterTactics(tactics));
    }

    public bool Retarget(MonsterEntity monster, ushort targetEntityId)
    {
        ArgumentNullException.ThrowIfNull(monster);

        lock (_gate)
        {
            if (!_fights.TryGetValue(monster.EntityId, out var fight) || fight.TargetEntityId == targetEntityId) return false;
            var definition = _catalog.GetById(monster.MonsterParamId);
            if (definition is null) return false;

            fight.TargetEntityId = targetEntityId;
            fight.Settled = false;
            fight.SettlesOnArrival = false;
            fight.ChaseBudgetMs = ChaseTimeMs(definition);
            fight.DestinationShifts = 0;
            fight.Reapproaching = false;
            fight.ReapproachFailures = 0;
            return true;
        }
    }

    public IReadOnlyList<MonsterBattleEvent> Tick(
        ushort mapId,
        ZoneGeometry? geometry,
        IReadOnlyCollection<MonsterEntity> monsters,
        IReadOnlyDictionary<ushort, MonsterBattleTarget> targets,
        DateTimeOffset now,
        Func<MonsterEntity, ushort, bool>? mayHold = null)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(targets);

        var events = new List<MonsterBattleEvent>();
        lock (_gate)
        {
            if (_fights.Count == 0) return events;

            var hpLeftByTarget = new Dictionary<ushort, long>();
            var monstersById = new Dictionary<ushort, MonsterEntity>(monsters.Count);
            foreach (var monster in monsters) monstersById[monster.EntityId] = monster;

            foreach (var (monsterId, fight) in _fights.ToArray())
            {
                if (fight.MapId != mapId) continue;
                if (!monstersById.TryGetValue(monsterId, out var monster) || monster.IsDead)
                {
                    _fights.Remove(monsterId);
                    continue;
                }

                if (mayHold is not null && !mayHold(monster, fight.TargetEntityId))
                {
                    Leave(monsterId, MonsterBattleLeaveReason.TargetNotVisible, events);
                    continue;
                }

                if (monster.IsHeld(now))
                {
                    fight.LastTick = now;
                    fight.Legs.Clear();
                    continue;
                }

                while (fight.Legs.Count > 0 && now >= fight.Legs.Peek().At)
                {
                    var leg = fight.Legs.Dequeue();
                    events.Add(new MonsterBattleMoved(monsterId, leg.FromX, leg.FromY, leg.ToX, leg.ToY, leg.MoveType));
                }

                var countedFrom = fight.LastTick > fight.NextSwingAt ? fight.LastTick : fight.NextSwingAt;
                var sinceLastTick = now - fight.LastTick;
                if (sinceLastTick > LongestCountedTick) sinceLastTick = LongestCountedTick;
                fight.LastTick = now;
                if (fight.Tactics is { } tactics && tactics.Advance((int)Math.Max(0, sinceLastTick.TotalMilliseconds)))
                {
                    TacticTarget? tacticTarget = targets.TryGetValue(fight.TargetEntityId, out var tactical)
                        ? new TacticTarget(tactical.Level, tactical.Job, WireLimits.Int32Field(tactical.Hp), WireLimits.Int32Field(tactical.MaxHp))
                        : null;
                    tactics.Rearm(_rng.Next(MonsterTactics.RatioCap), tacticTarget, WireLimits.Int32Field(monster.CurrentHp));
                }

                var definition = _catalog.GetById(monster.MonsterParamId);
                if (definition is null
                    || !_reachByType.TryGetValue(monster.MonsterParamId, out var reach)
                    || !targets.TryGetValue(fight.TargetEntityId, out var target))
                {
                    Leave(monsterId, MonsterBattleLeaveReason.TargetGone, events);
                    continue;
                }
                if (now < fight.NextSwingAt) continue;

                if (fight.PendingCast is { } pendingCast)
                {
                    fight.PendingCast = null;
                    CompleteCast(monster, fight, definition, reach, pendingCast, now, events);
                    continue;
                }

                var counted = now - countedFrom;
                if (counted > LongestCountedTick) counted = LongestCountedTick;
                fight.ChaseBudgetMs -= (int)counted.TotalMilliseconds;
                if (fight.ChaseBudgetMs < 1)
                {
                    Leave(monsterId, MonsterBattleLeaveReason.ChaseTimeSpent, events);
                    continue;
                }

                if (fight.MoveEndsAt != default)
                {
                    if (now < fight.MoveEndsAt) continue;
                    fight.MoveEndsAt = default;
                    if (fight.SettlesOnArrival)
                    {
                        fight.SettlesOnArrival = false;
                        fight.Settled = true;
                    }
                }

                var canMove = monster.WalkSpeedOf(definition.WalkSpeed) > 0 && monster.RunSpeedOf(definition.RunSpeed) > 0 && !monster.IsRooted(now);
                var decision = MeleeApproach.Decide(
                    reach, monster.DestinationX, monster.DestinationY, target.X, target.Y, fight.Settled, _rng,
                    immobile: monster.WalkSpeedOf(definition.WalkSpeed) <= 0 || monster.RunSpeedOf(definition.RunSpeed) <= 0);
                var backingOff = decision.Kind == MeleeApproachKind.BackOff;

                if (decision.Kind == MeleeApproachKind.GiveUp)
                {
                    Leave(monsterId, MonsterBattleLeaveReason.OutOfChaseRange, events);
                    continue;
                }
                if (decision.Kind == MeleeApproachKind.InRange || (backingOff && !canMove))
                {
                    fight.OrdersSinceInRange = 0;
                    fight.ChaseBudgetMs = ChaseTimeMs(definition);
                    Swing(monster, fight, definition, reach, target, hpLeftByTarget, now, events);
                    continue;
                }

                if (monster.IsRooted(now)) continue;

                var gap = backingOff ? MeleeApproach.BackOffGap : MeleeApproach.OrderGap;
                var moveType = !backingOff && fight.Settled ? MeleeApproach.ChaseMoveType : StoredMoveType;
                var orderDue = canMove && now - fight.LastOrderAt >= gap;
                if (orderDue)
                {
                    fight.LastOrderAt = now;
                    fight.OrdersSinceInRange++;
                    if (MeleeApproach.OrdersFromRandomSide(fight.OrdersSinceInRange))
                    {
                        var (sideX, sideY) = MeleeApproach.RandomSidePoint(reach, target.X, target.Y, _rng);
                        decision = decision with { X = sideX, Y = sideY };
                    }
                }
                if (!orderDue && canMove) continue;
                var reapproaching = orderDue && !backingOff && fight.Reapproaching;
                var shifted = orderDue && !backingOff && !reapproaching && reach.Box is { } ownBox && AimOverlapsAnother(monster, monsters, ownBox, decision, now);
                if (shifted) fight.DestinationShifts++;
                var ordered = orderDue
                    && !shifted
                    && TryMove(Observer, geometry, monster, fight, definition, reach, target, decision, moveType, backingOff, now, events);
                if (reapproaching)
                {
                    if (ordered)
                    {
                        fight.Reapproaching = false;
                    }
                    else if (++fight.ReapproachFailures >= MaxReapproachFailures)
                    {
                        Leave(monsterId, MonsterBattleLeaveReason.DestinationsCrowded, events);
                    }
                    continue;
                }
                if (orderDue && !ordered && !backingOff && fight.DestinationShifts > MaxDestinationShifts)
                {
                    fight.DestinationShifts = 0;
                    fight.Reapproaching = true;
                    fight.ReapproachFailures = 0;
                    continue;
                }
                if (shifted) continue;
                if (orderDue && !ordered && !backingOff && fight.Settled && NoteRefusedOrder(monsterId, fight, target, now))
                {
                    Leave(monsterId, MonsterBattleLeaveReason.TargetUnreachable, events);
                    continue;
                }
                if (!ordered && !backingOff && !fight.Settled)
                {
                    Leave(monsterId, MonsterBattleLeaveReason.ApproachRefused, events);
                }
            }
        }
        return events;
    }

    private void Swing(
        MonsterEntity monster,
        Fight fight,
        MonsterParamEntry definition,
        CharacterCombatInfo reach,
        MonsterBattleTarget target,
        Dictionary<ushort, long> hpLeftByTarget,
        DateTimeOffset now,
        List<MonsterBattleEvent> events)
    {
        if (!hpLeftByTarget.TryGetValue(fight.TargetEntityId, out var targetHp)) targetHp = target.Hp;
        if (targetHp < 1)
        {
            Leave(monster.EntityId, MonsterBattleLeaveReason.TargetGone, events);
            return;
        }

        if (fight.Tactics is { Usable: true } tactics
            && tactics.Pick(sum => _rng.Next(sum)) is { ActionType: MonsterTactics.CastActionType } tactic
            && (_castable?.Invoke(tactic.SkillId, tactic.SkillLevel) ?? false)
            && !IsCooling(monster.EntityId, tactic.SkillId, now))
        {
            var timing = _skillTimingOf?.Invoke(tactic.SkillId, tactic.SkillLevel) ?? default;
            var cast = new PendingCast(tactic.SkillId, tactic.SkillLevel, tactic.Speech, timing.CoolMs);
            fight.Settled = true;
            if (timing.CastMs > 0)
            {
                fight.PendingCast = cast;
                fight.NextSwingAt = now.AddMilliseconds(timing.CastMs);
                events.Add(new MonsterBattleCastStarted(monster.EntityId, fight.TargetEntityId, cast.SkillId, cast.Level));
                return;
            }
            CompleteCast(monster, fight, definition, reach, cast, now, events);
            return;
        }

        var outcome = MeleeResolution.MonsterAgainstPlayer(
            definition.ApLv + monster.StatDelta(MonsterStat.Ap),
            definition.AcLv + monster.StatDelta(MonsterStat.Ac),
            definition.LkLv + monster.StatDelta(MonsterStat.Lk),
            definition.Level,
            definition.AccFormula,
            target.Dp,
            target.Hv,
            target.Lk,
            target.Level,
            targetHp,
            target.Protected,
            _rng,
            target.DefenceParalysed);
        var fatal = outcome.Damage >= targetHp;
        hpLeftByTarget[fight.TargetEntityId] = fatal ? 0 : targetHp - outcome.Damage;
        fight.Settled = true;
        fight.NextSwingAt = now.AddMilliseconds(reach.MonsterSwingDelayMs(definition.DxLv + monster.StatDelta(MonsterStat.Dx)));
        Observer?.Invoke(new MonsterBattleReachSample(
            monster.EntityId,
            MonsterBattleReachKind.Swing,
            (int)Math.Round(Math.Sqrt(Math.Pow((double)monster.DestinationX - target.X, 2) + Math.Pow((double)monster.DestinationY - target.Y, 2))),
            reach.MinAttackRange, reach.MaxAttackRange, reach.ApproachRadius));
        events.Add(new MonsterBattleSwung(
            monster.EntityId,
            monster.DestinationX,
            monster.DestinationY,
            fight.TargetEntityId,
            target.X,
            target.Y,
            outcome.Damage,
            outcome.Result,
            fatal));
    }

    private static bool TryMove(
        Action<MonsterBattleReachSample>? observer,
        ZoneGeometry? geometry,
        MonsterEntity monster,
        Fight fight,
        MonsterParamEntry definition,
        CharacterCombatInfo reach,
        MonsterBattleTarget target,
        MeleeApproachDecision decision,
        byte moveType,
        bool backingOff,
        DateTimeOffset now,
        List<MonsterBattleEvent> events)
    {
        var gapX = (double)monster.DestinationX - target.X;
        var gapY = (double)monster.DestinationY - target.Y;
        var distance = (int)Math.Round(Math.Sqrt(gapX * gapX + gapY * gapY));
        var effectiveType = MeleeApproach.EffectiveMoveType(moveType);
        var walkSpeed = monster.WalkSpeedOf(definition.WalkSpeed);
        var runSpeed = monster.RunSpeedOf(definition.RunSpeed);
        var speed = effectiveType == MeleeApproach.ChaseMoveType
            ? (walkSpeed + runSpeed) / ChaseSpeedDivisor
            : runSpeed;
        if (geometry is null || speed <= 0) return false;
        speed = ClientSpeed.Drawn(speed);
        if (speed <= 0) return false;

        var route = WanderRoute.FindToApproachPoint(geometry, monster.DestinationX, monster.DestinationY, decision.X, decision.Y);
        if (route is null) return false;

        var travelMs = (int)Math.Ceiling(route.Length / speed * MillisecondsPerSecond);
        if (monster.TakeOrder(effectiveType, speed))
        {
            events.Add(new MonsterBattleSpeedChanged(monster.EntityId, speed));
        }
        var (fromX, fromY) = monster.MoveAlong(route.Points, now, travelMs);

        fight.MoveEndsAt = now.AddMilliseconds(travelMs);
        fight.SettlesOnArrival = backingOff;
        fight.Legs.Clear();
        var legs = WanderRoute.Legs(geometry, fromX, fromY, route.Points);
        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            var started = new PendingLeg(
                now.AddMilliseconds(Math.Ceiling(leg.StartDistance / speed * MillisecondsPerSecond)),
                (ushort)leg.FromX, (ushort)leg.FromY, (ushort)leg.ToX, (ushort)leg.ToY, moveType);
            if (i == 0) events.Add(new MonsterBattleMoved(monster.EntityId, started.FromX, started.FromY, started.ToX, started.ToY, moveType));
            else fight.Legs.Enqueue(started);
        }
        observer?.Invoke(new MonsterBattleReachSample(
            monster.EntityId,
            backingOff ? MonsterBattleReachKind.BackOff : MonsterBattleReachKind.CloseIn,
            distance, reach.MinAttackRange, reach.MaxAttackRange, reach.ApproachRadius));
        return true;
    }

    private bool AimOverlapsAnother(
        MonsterEntity monster, IReadOnlyCollection<MonsterEntity> monsters, BodyBox ownBox, MeleeApproachDecision aim, DateTimeOffset now)
    {
        foreach (var other in monsters)
        {
            if (other.EntityId == monster.EntityId || other.IsDead || other.MapId != monster.MapId) continue;
            if (!_reachByType.TryGetValue(other.MonsterParamId, out var otherReach) || otherReach.Box is not { } otherBox) continue;
            var (otherX, otherY) = other.GetInterpolatedPosition(now);
            if (ownBox.Overlaps(aim.X, aim.Y, otherBox, otherX, otherY)) return true;
        }
        return false;
    }

    private bool IsCooling(ushort monsterEntityId, int skillId, DateTimeOffset now) =>
        _skillCooling.TryGetValue((monsterEntityId, skillId), out var until) && now < until;

    private void CompleteCast(
        MonsterEntity monster,
        Fight fight,
        MonsterParamEntry definition,
        CharacterCombatInfo reach,
        PendingCast cast,
        DateTimeOffset now,
        List<MonsterBattleEvent> events)
    {
        if (cast.CoolMs > 0) _skillCooling[(monster.EntityId, cast.SkillId)] = now.AddMilliseconds(cast.CoolMs);
        fight.NextSwingAt = now.AddMilliseconds(reach.MonsterSwingDelayMs(definition.DxLv + monster.StatDelta(MonsterStat.Dx)));
        events.Add(new MonsterBattleCast(monster.EntityId, fight.TargetEntityId, cast.SkillId, cast.Level, cast.Speech));
    }

    private bool NoteRefusedOrder(ushort monsterEntityId, Fight fight, MonsterBattleTarget target, DateTimeOffset now)
    {
        if (!fight.RefusedAt.HasValue)
        {
            fight.RefusedAt = (target.X, target.Y);
            return false;
        }
        if (fight.RefusedAt.Value != (target.X, target.Y)) return false;
        fight.RefusedAtSameSpot++;
        if (fight.RefusedAtSameSpot < UnreachableSameSpotOrders) return false;
        _unreachable[monsterEntityId] = new UnreachableSpot(target.X, target.Y, now + UnreachableMemory);
        return true;
    }

    private void Leave(ushort monsterEntityId, MonsterBattleLeaveReason reason, List<MonsterBattleEvent> events)
    {
        _fights.Remove(monsterEntityId);
        events.Add(new MonsterBattleLeft(monsterEntityId, reason));
    }

    private sealed class Fight
    {
        public ushort MapId;
        public ushort TargetEntityId;
        public bool Settled;
        public bool SettlesOnArrival;
        public DateTimeOffset NextSwingAt;
        public DateTimeOffset MoveEndsAt;
        public DateTimeOffset LastOrderAt;
        public int OrdersSinceInRange;
        public DateTimeOffset LastTick;
        public int ChaseBudgetMs;
        public MonsterTactics? Tactics;
        public Queue<PendingLeg> Legs { get; } = new();
        public PendingCast? PendingCast;
        public (int X, int Y)? RefusedAt;
        public int RefusedAtSameSpot;
        public int DestinationShifts;
        public bool Reapproaching;
        public int ReapproachFailures;
    }

    private readonly record struct PendingCast(int SkillId, int Level, string Speech, int CoolMs);

    private readonly record struct UnreachableSpot(int X, int Y, DateTimeOffset Until);

    private readonly record struct PendingLeg(DateTimeOffset At, ushort FromX, ushort FromY, ushort ToX, ushort ToY, byte MoveType);
}
