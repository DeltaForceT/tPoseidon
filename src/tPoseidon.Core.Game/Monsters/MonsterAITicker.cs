using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class MonsterAITicker
{
    private readonly MonsterManager _manager;
    private readonly IMonsterParamLookup _catalog;
    private readonly DamageCalculator? _damage;
    private readonly CadenceCalculator? _cadence;
    private readonly Random _rng;
    private readonly object _rngLock = new();

    public const int WanderMinDelayMs = 3000;
    public const int WanderMaxDelayMs = 6000;
    public const int WanderRange = 300;
    public const byte WalkMovType = 0x01;

    public const int StrikeFallbackMs = 1500;

        public const int DefaultAttackRangeSquared = 100 * 100;

    public MonsterAITicker(
        MonsterManager manager,
        IMonsterParamLookup catalog,
        DamageCalculator? damage = null,
        CadenceCalculator? cadence = null,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(catalog);
        _manager = manager;
        _catalog = catalog;
        _damage = damage;
        _cadence = cadence;
        _rng = seed is int s ? new Random(s) : new Random();
    }

        public IReadOnlyList<MonsterMove> Tick(ushort mapId, DateTimeOffset now)
    {
        var mobs = _manager.GetByMap(mapId);
        if (mobs.Count == 0) return Array.Empty<MonsterMove>();

        var moves = new List<MonsterMove>();
        foreach (var mob in mobs)
        {
            if (mob.NextMoveAt > now) continue;
            if (mob.IsInCombat) continue; 

            int delayMs;
            lock (_rngLock)
            {
                var dx = _rng.Next(-WanderRange, WanderRange + 1);
                var dy = _rng.Next(-WanderRange, WanderRange + 1);
                delayMs = _rng.Next(WanderMinDelayMs, WanderMaxDelayMs + 1);

                var toX = (ushort)Math.Clamp(mob.SpawnX + dx, 0, ushort.MaxValue);
                var toY = (ushort)Math.Clamp(mob.SpawnY + dy, 0, ushort.MaxValue);

                var moveDistance = Math.Sqrt((double)dx * dx + (double)dy * dy);
                var moveDurationMs = (int)Math.Clamp(moveDistance / 0.112, 200, WanderMaxDelayMs);

                var (fromX, fromY) = mob.MoveTo(toX, toY, now, moveDurationMs);
                mob.NextMoveAt = now.AddMilliseconds(delayMs);

                moves.Add(new MonsterMove(mob.EntityId, fromX, fromY, toX, toY));
            }
        }
        return moves;
    }

        public IReadOnlyList<MonsterStrike> TickCombat(
        ushort mapId,
        IReadOnlyList<PlayerSnapshot> playersOnMap,
        DateTimeOffset now)
    {
        if (playersOnMap.Count == 0) return Array.Empty<MonsterStrike>();
        var mobs = _manager.GetByMap(mapId);
        if (mobs.Count == 0) return Array.Empty<MonsterStrike>();

        var strikes = new List<MonsterStrike>();
        foreach (var mob in mobs)
        {
            var def = _catalog.GetById(mob.MonsterParamId);
            if (def is null) continue;

            PlayerSnapshot? target = null;
            var primaryAttacker = mob.PrimaryAttackerEntityId;
            if (primaryAttacker != 0)
            {
                foreach (var p in playersOnMap)
                {
                    if (p.EntityId == primaryAttacker) { target = p; break; }
                }
                if (target is null)
                {
                    mob.RemoveAttacker(primaryAttacker);
                    continue;
                }
            }
            else if (def.AiAttackDispos > 0)
            {
                var viewSq = def.AiViewRange > 0 ? def.AiViewRange * def.AiViewRange : 250 * 250;
                foreach (var p in playersOnMap)
                {
                    if (DistanceSquared(mob.CurrentX, mob.CurrentY, p.X, p.Y) <= viewSq)
                    {
                        mob.AddAttacker(p.EntityId);
                        target = p;
                        break;
                    }
                }
                if (target is null) continue;
            }
            else
            {
                continue;
            }

            var attackRangeSq = DefaultAttackRangeSquared;
            var distSq = DistanceSquared(mob.CurrentX, mob.CurrentY, target.Value.X, target.Value.Y);
            if (distSq > attackRangeSq * 4)
            {
                mob.RemoveAttacker(target.Value.EntityId);
                continue;
            }
            if (distSq > attackRangeSq) continue;

            if (mob.NextStrikeAt > now) continue;

            int damage;
            byte hitFlags;
            uint hitKind;
            if (_damage is not null)
            {
                var attacker = new AttackerStats(Ap: def.ApLv, Ma: 0, Ac: def.AcLv, GunAp: 0);
                var defender = new DefenderStats(
                    Dp: target.Value.Dp,
                    Md: target.Value.Md,
                    Hv: target.Value.Hv,
                    CurrentHp: target.Value.CurrentHp,
                    PhysResistPct: 0, MagicResistPct: 0, GunResistPct: 0,
                    FireResistPct: 0, WaterResistPct: 0, AirResistPct: 0,
                    SoilResistPct: 0, ElectricityResistPct: 0,
                    LightResistPct: 0, DarkResistPct: 0, ShadowResistPct: 0);
                var result = _damage.Compute(attacker, defender, new DamageInput(
                    Kind: DamageKind.PhysicalMelee, IsMobAttack: true));
                damage = result.Damage;
                hitFlags = result.HitFlags;
                hitKind = result.HitKind;
            }
            else
            {
                damage = Math.Max(1, def.ApLv);
                hitFlags = DamageHitFlags.MobHit;
                hitKind = DamageHitKind.MobToPlayer;
            }

            var cadenceMs = _cadence?.MobStrikeCadenceMs() ?? StrikeFallbackMs;
            mob.NextStrikeAt = now.AddMilliseconds(cadenceMs);
            strikes.Add(new MonsterStrike(
                MobEntityId: mob.EntityId,
                TargetEntityId: target.Value.EntityId,
                MobX: mob.CurrentX,
                MobY: mob.CurrentY,
                TargetX: target.Value.X,
                TargetY: target.Value.Y,
                Damage: damage,
                HitFlags: hitFlags,
                HitKind: hitKind));
        }
        return strikes;
    }

    public static void NotifyMobDamaged(MonsterEntity mob, ushort attackerEntityId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(mob);
        if (!mob.AddAttacker(attackerEntityId)) return;
        mob.NextStrikeAt = now.AddMilliseconds(500);
    }

    public static void NotifyAttackerDisengaged(MonsterEntity mob, ushort attackerEntityId)
    {
        ArgumentNullException.ThrowIfNull(mob);
        mob.RemoveAttacker(attackerEntityId);
    }

    private static int DistanceSquared(int ax, int ay, int bx, int by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }
}

public readonly record struct MonsterMove(ushort EntityId, ushort FromX, ushort FromY, ushort ToX, ushort ToY);

public readonly record struct MonsterStrike(
    ushort MobEntityId,
    ushort TargetEntityId,
    ushort MobX,
    ushort MobY,
    ushort TargetX,
    ushort TargetY,
    int Damage,
    byte HitFlags = DamageHitFlags.MobHit,
    uint HitKind = DamageHitKind.MobToPlayer);

public readonly record struct PlayerSnapshot(
    ushort EntityId,
    ushort X,
    ushort Y,
    int Dp = 0,
    int Md = 0,
    int Hv = 0,
    int CurrentHp = int.MaxValue);
