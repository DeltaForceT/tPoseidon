using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public sealed class PlayerAttackTicker
{
    private readonly MonsterManager _manager;
    private readonly IMonsterParamLookup _catalog;
    private readonly CadenceCalculator? _cadence;
    private readonly DamageCalculator? _damage;
    private readonly ActionManager? _actionManager;

    public const int StrikeFallbackMs = 1000;
    public const int DefaultAttackRangeSquared = 120 * 120;

    public PlayerAttackTicker(
        MonsterManager manager,
        IMonsterParamLookup catalog,
        CadenceCalculator? cadence = null,
        DamageCalculator? damage = null,
        ActionManager? actionManager = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(catalog);
        _manager = manager;
        _catalog = catalog;
        _cadence = cadence;
        _damage = damage;
        _actionManager = actionManager;
    }

    public ActionManager? ActionManager => _actionManager;

    public IReadOnlyList<PlayerStrike> Tick(
        IReadOnlyList<PlayerCombatSnapshot> attackingPlayers,
        DateTimeOffset now)
    {
        if (attackingPlayers.Count == 0) return Array.Empty<PlayerStrike>();

        var strikes = new List<PlayerStrike>();
        foreach (var p in attackingPlayers)
        {
            if (p.TargetEntityId == 0) continue;
            if (p.NextSwingAt > now) continue;

            var mob = _manager.Get(p.TargetEntityId);
            if (mob is null || mob.IsDead) continue;

            var distSq = DistanceSquared(p.X, p.Y, mob.CurrentX, mob.CurrentY);
            if (distSq > DefaultAttackRangeSquared * 4) continue;
            if (distSq > DefaultAttackRangeSquared) continue;

            var def = _catalog.GetById(mob.MonsterParamId);

            long requestedDamage;
            byte hitFlags;
            uint hitKind;
            if (_damage is not null && def is not null)
            {
                var attacker = new AttackerStats(Ap: p.Ap, Ma: p.Ma, Ac: p.Ac, GunAp: 0);
                var defender = new DefenderStats(
                    Dp: def.DpLv,
                    Md: def.MdLv,
                    Hv: 0,
                    CurrentHp: mob.CurrentHp,
                    PhysResistPct: 0, MagicResistPct: 0, GunResistPct: 0,
                    FireResistPct: 0, WaterResistPct: 0, AirResistPct: 0,
                    SoilResistPct: 0, ElectricityResistPct: 0,
                    LightResistPct: 0, DarkResistPct: 0, ShadowResistPct: 0);
                var result = _damage.Compute(attacker, defender, new DamageInput(
                    Kind: DamageKind.PhysicalMelee, IsMobAttack: false));
                requestedDamage = result.Damage;
                hitFlags = result.HitFlags;
                hitKind = result.HitKind;
            }
            else
            {
                requestedDamage = Math.Max(1L, p.Ap);
                hitFlags = DamageHitFlags.Normal;
                hitKind = DamageHitKind.PlayerToMob;
            }

            mob.AddAttacker(p.EntityId);
            _actionManager?.Create(p.EntityId, mob.EntityId, skillId: 0);
            var actualDamage = mob.ApplyDamage(requestedDamage);
            var killed = mob.IsDead;
            if (killed)
            {
                hitFlags |= DamageHitFlags.Kill;
            }

            var cadenceMs = _cadence?.PlayerSwingCadenceMs(p.Dx) ?? StrikeFallbackMs;
            strikes.Add(new PlayerStrike(
                AttackerEntityId: p.EntityId,
                TargetEntityId: mob.EntityId,
                AttackerX: p.X,
                AttackerY: p.Y,
                TargetX: mob.CurrentX,
                TargetY: mob.CurrentY,
                Damage: actualDamage,
                MobHpAfter: mob.CurrentHp,
                HitFlags: hitFlags,
                HitKind: hitKind,
                Killed: killed,
                NextSwingAtUtc: now.AddMilliseconds(cadenceMs),
                MonsterParamId: mob.MonsterParamId,
                MobMaxHp: mob.MaxHp));
        }
        return strikes;
    }

    private static int DistanceSquared(int ax, int ay, int bx, int by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return dx * dx + dy * dy;
    }
}

public readonly record struct PlayerCombatSnapshot(
    ushort EntityId,
    ushort TargetEntityId,
    ushort X,
    ushort Y,
    ushort Ap,
    ushort Ma,
    ushort Ac,
    ushort Dx,
    DateTimeOffset NextSwingAt);

public readonly record struct PlayerStrike(
    ushort AttackerEntityId,
    ushort TargetEntityId,
    ushort AttackerX,
    ushort AttackerY,
    ushort TargetX,
    ushort TargetY,
    long Damage,
    long MobHpAfter,
    byte HitFlags,
    uint HitKind,
    bool Killed,
    DateTimeOffset NextSwingAtUtc,
    int MonsterParamId,
    long MobMaxHp,
    long MobHpBefore = 0);
