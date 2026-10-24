using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public enum MonsterBattleLeaveReason
{
    TargetGone,
    OutOfChaseRange,
    ChaseTimeSpent,
    ApproachRefused,
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

public sealed record MonsterBattleSwung(
    ushort EntityId,
    ushort X,
    ushort Y,
    ushort TargetEntityId,
    ushort TargetX,
    ushort TargetY,
    int Damage,
    byte Result,
    bool Fatal,
    ushort GuardLeft = 0) : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleLeft(ushort EntityId, MonsterBattleLeaveReason Reason)
    : MonsterBattleEvent(EntityId);

public sealed record MonsterBattleCast(ushort EntityId, ushort TargetEntityId, int SkillId, int Level, string Speech)
    : MonsterBattleEvent(EntityId);

public readonly record struct MonsterBattleTarget(
    ushort X,
    ushort Y,
    int Dp,
    int Hv,
    int Lk,
    int Level,
    int Hp,
    bool Protected = false,
    int MaxHp = 0,
    int Job = 0);

public sealed class MonsterBattleCycle
{
    public const byte StoredMoveType = MeleeApproach.DefaultMoveType;
    public const int DefaultChaseTimeMs = 3000;

    public static readonly TimeSpan LongestCountedTick = TimeSpan.FromSeconds(1);

    private const double MillisecondsPerSecond = 1000.0;
    private const float ChaseSpeedDivisor = 2f;

    private readonly IMonsterParamLookup _catalog;
    private readonly IReadOnlyDictionary<int, CharacterCombatInfo> _reachByType;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Fight> _fights = new();
    private readonly Func<int, IReadOnlyList<MonsterTacticEntry>>? _tacticsOf;
    private readonly Func<int, int, bool>? _castable;

    public MonsterBattleCycle(
        IMonsterParamLookup catalog,
        IReadOnlyDictionary<int, CharacterCombatInfo> reachByType,
        Random? rng = null,
        Func<int, IReadOnlyList<MonsterTacticEntry>>? tacticsOf = null,
        Func<int, int, bool>? castable = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(reachByType);
        _catalog = catalog;
        _reachByType = reachByType;
        _rng = rng ?? new Random();
        _tacticsOf = tacticsOf;
        _castable = castable;
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

    public void Forget(ushort monsterEntityId)
    {
        lock (_gate)
        {
            _fights.Remove(monsterEntityId);
        }
    }

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
                Tactics = _tacticsOf?.Invoke(monster.MonsterParamId) is { Count: > 0 } tactics ? new MonsterTactics(tactics) : null,
            };
            var haltedType = monster.MoveType == MeleeApproach.DefaultMoveType
                ? MeleeApproach.WalkMoveType
                : monster.MoveType;
            return new MonsterBattleHalted(monster.EntityId, x, y, haltedType);
        }
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
            return true;
        }
    }

    public IReadOnlyList<MonsterBattleEvent> Tick(
        ushort mapId,
        ZoneGeometry? geometry,
        IReadOnlyCollection<MonsterEntity> monsters,
        IReadOnlyDictionary<ushort, MonsterBattleTarget> targets,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(targets);

        var events = new List<MonsterBattleEvent>();
        lock (_gate)
        {
            if (_fights.Count == 0) return events;

            var hpLeftByTarget = new Dictionary<ushort, int>();
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

                if (monster.IsHeld(now))
                {
                    fight.LastTick = now;
                    continue;
                }

                var countedFrom = fight.LastTick > fight.NextSwingAt ? fight.LastTick : fight.NextSwingAt;
                var sinceLastTick = now - fight.LastTick;
                if (sinceLastTick > LongestCountedTick) sinceLastTick = LongestCountedTick;
                fight.LastTick = now;
                if (fight.Tactics is { } tactics && tactics.Advance((int)Math.Max(0, sinceLastTick.TotalMilliseconds)))
                {
                    TacticTarget? tacticTarget = targets.TryGetValue(fight.TargetEntityId, out var tactical)
                        ? new TacticTarget(tactical.Level, tactical.Job, tactical.Hp, tactical.MaxHp)
                        : null;
                    tactics.Rearm(_rng.Next(MonsterTactics.RatioCap), tacticTarget, monster.CurrentHp);
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

                var decision = MeleeApproach.Decide(
                    reach, monster.CurrentX, monster.CurrentY, target.X, target.Y, fight.Settled, _rng);
                var canMove = monster.WalkSpeedOf(definition.WalkSpeed) > 0 && monster.RunSpeedOf(definition.RunSpeed) > 0;
                var backingOff = decision.Kind == MeleeApproachKind.BackOff;

                if (decision.Kind == MeleeApproachKind.GiveUp)
                {
                    Leave(monsterId, MonsterBattleLeaveReason.OutOfChaseRange, events);
                    continue;
                }
                if (decision.Kind == MeleeApproachKind.InRange || (backingOff && !canMove))
                {
                    fight.ChaseBudgetMs = ChaseTimeMs(definition);
                    Swing(monster, fight, definition, reach, target, hpLeftByTarget, now, events);
                    continue;
                }

                var gap = backingOff ? MeleeApproach.BackOffGap : MeleeApproach.OrderGap;
                var moveType = !backingOff && fight.Settled ? MeleeApproach.ChaseMoveType : StoredMoveType;
                var ordered = canMove
                    && now - fight.LastOrderAt >= gap
                    && TryMove(geometry, monster, fight, definition, decision, moveType, backingOff, now, events);
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
        Dictionary<ushort, int> hpLeftByTarget,
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
            && (_castable?.Invoke(tactic.SkillId, tactic.SkillLevel) ?? false))
        {
            fight.Settled = true;
            fight.NextSwingAt = now.AddMilliseconds(reach.MonsterSwingDelayMs(definition.DxLv + monster.StatDelta(MonsterStat.Dx)));
            events.Add(new MonsterBattleCast(monster.EntityId, fight.TargetEntityId, tactic.SkillId, tactic.SkillLevel, tactic.Speech));
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
            _rng);
        var fatal = outcome.Damage >= targetHp;
        hpLeftByTarget[fight.TargetEntityId] = fatal ? 0 : targetHp - outcome.Damage;
        fight.Settled = true;
        fight.NextSwingAt = now.AddMilliseconds(reach.MonsterSwingDelayMs(definition.DxLv + monster.StatDelta(MonsterStat.Dx)));
        events.Add(new MonsterBattleSwung(
            monster.EntityId,
            monster.CurrentX,
            monster.CurrentY,
            fight.TargetEntityId,
            target.X,
            target.Y,
            outcome.Damage,
            outcome.Result,
            fatal));
    }

    private static bool TryMove(
        ZoneGeometry? geometry,
        MonsterEntity monster,
        Fight fight,
        MonsterParamEntry definition,
        MeleeApproachDecision decision,
        byte moveType,
        bool backingOff,
        DateTimeOffset now,
        List<MonsterBattleEvent> events)
    {
        var effectiveType = MeleeApproach.EffectiveMoveType(moveType);
        var walkSpeed = monster.WalkSpeedOf(definition.WalkSpeed);
        var runSpeed = monster.RunSpeedOf(definition.RunSpeed);
        var speed = effectiveType == MeleeApproach.ChaseMoveType
            ? (walkSpeed + runSpeed) / ChaseSpeedDivisor
            : runSpeed;
        if (geometry is null || speed <= 0) return false;

        var route = WanderRoute.FindToPoint(geometry, monster.CurrentX, monster.CurrentY, decision.X, decision.Y);
        if (route is null) return false;

        var travelMs = (int)Math.Ceiling(route.Length / speed * MillisecondsPerSecond);
        var destinationX = (ushort)route.DestinationX;
        var destinationY = (ushort)route.DestinationY;
        if (monster.TakeOrder(effectiveType, speed))
        {
            events.Add(new MonsterBattleSpeedChanged(monster.EntityId, speed));
        }
        var (fromX, fromY) = monster.MoveAlong(route.Points, now, travelMs);

        fight.LastOrderAt = now;
        fight.MoveEndsAt = now.AddMilliseconds(travelMs);
        fight.SettlesOnArrival = backingOff;
        events.Add(new MonsterBattleMoved(monster.EntityId, fromX, fromY, destinationX, destinationY, moveType));
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
        public DateTimeOffset LastTick;
        public int ChaseBudgetMs;
        public MonsterTactics? Tactics;
    }
}
