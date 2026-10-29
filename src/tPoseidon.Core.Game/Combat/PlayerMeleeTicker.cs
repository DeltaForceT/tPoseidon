using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public readonly record struct PlayerMeleeAttacker(
    ushort EntityId,
    ushort TargetEntityId,
    ushort X,
    ushort Y,
    long Ap,
    long Ac,
    long Lk,
    DateTimeOffset NextSwingAt,
    int CriticalBonus = 0);

public sealed class PlayerMeleeTicker
{
    private readonly MonsterManager _monsters;
    private readonly IMonsterParamLookup _catalog;
    private readonly Random _rng;
    private readonly Dictionary<ushort, byte> _armedByAttacker = new();

    public PlayerMeleeTicker(MonsterManager monsters, IMonsterParamLookup catalog, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(catalog);
        _monsters = monsters;
        _catalog = catalog;
        _rng = rng ?? new Random();
    }

    public const double HardenedShare = 0.5;

    public IReadOnlyList<PlayerStrike> Tick(
        IReadOnlyList<PlayerMeleeAttacker> attackers, DateTimeOffset now, Func<ushort, bool>? halvesHits = null, Func<ushort, bool>? cannotEvade = null)
    {
        ArgumentNullException.ThrowIfNull(attackers);
        if (attackers.Count == 0) return Array.Empty<PlayerStrike>();

        var strikes = new List<PlayerStrike>();
        foreach (var attacker in attackers)
        {
            if (attacker.TargetEntityId == 0 || attacker.NextSwingAt > now) continue;
            var monster = _monsters.Get(attacker.TargetEntityId);
            if (monster is null || monster.IsDead) continue;
            var definition = _catalog.GetById(monster.MonsterParamId);

            _armedByAttacker.TryGetValue(attacker.EntityId, out var armed);
            var outcome = MeleeResolution.PlayerAgainstMonster(
                attacker.Ap,
                attacker.Ac,
                attacker.Lk,
                armed,
                (definition?.DpLv ?? 0) + monster.StatDelta(MonsterStat.Dp),
                (definition?.HvLv ?? 0) + monster.StatDelta(MonsterStat.Hv),
                (definition?.LkLv ?? 0) + monster.StatDelta(MonsterStat.Lk),
                monster.CurrentHp,
                _rng,
                attacker.CriticalBonus,
                cannotEvade?.Invoke(monster.EntityId) == true);
            _armedByAttacker[attacker.EntityId] = outcome.ArmedNext;
            if (outcome.Damage > 0 && halvesHits?.Invoke(monster.EntityId) == true)
            {
                var halved = (long)(outcome.Damage * HardenedShare);
                outcome = outcome with
                {
                    Damage = halved,
                    Result = (byte)((outcome.Result & MeleeResolution.ResultKindMask)
                        | (halved >= monster.CurrentHp ? MeleeResolution.ResultFinishingFlag : 0)),
                };
            }

            var (targetX, targetY) = monster.GetInterpolatedPosition(now);
            var hpBefore = monster.CurrentHp;
            monster.AddAttacker(attacker.EntityId);
            monster.ApplyDamage(outcome.Damage);
            strikes.Add(new PlayerStrike(
                AttackerEntityId: attacker.EntityId,
                TargetEntityId: monster.EntityId,
                AttackerX: attacker.X,
                AttackerY: attacker.Y,
                TargetX: targetX,
                TargetY: targetY,
                Damage: outcome.Damage,
                MobHpAfter: monster.CurrentHp,
                HitFlags: 0,
                HitKind: outcome.Result,
                Killed: monster.IsDead,
                NextSwingAtUtc: now,
                MonsterParamId: monster.MonsterParamId,
                MobMaxHp: monster.MaxHp,
                MobHpBefore: hpBefore));
        }
        return strikes;
    }
}
