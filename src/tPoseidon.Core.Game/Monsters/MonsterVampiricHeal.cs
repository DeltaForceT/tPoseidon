using System.Collections.Concurrent;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters;

public readonly record struct VampiricHealNotice(
    ushort MonsterEntityId,
    ushort MapId,
    int Hp,
    ushort HpBars,
    bool BossFlag,
    int Healed);

public sealed class MonsterVampiricHeal : IMonsterDamageObserver
{
    public const double MaxHpDivisor = 100.0;

    private readonly ConcurrentQueue<VampiricHealNotice> _notices = new();

    public Func<ushort, bool> HasVampiricState { get; set; } = static _ => false;

    public static int HealAmount(long damage, long maxHp)
    {
        var damageShare = damage / MaxHpDivisor;
        var maxHpRatio = (double)maxHp / damage;
        return (int)(damage - maxHpRatio * damageShare);
    }

    public static bool IsBossFlagged(MonsterAType atype) =>
        atype is MonsterAType.BossMonster or MonsterAType.DoppelGanger
            or MonsterAType.NonresidentBossMonster or MonsterAType.LowHarconAttackBosMonster;

    public void Damaged(MonsterEntity monster, long damage)
    {
        if (damage <= 0 || monster.IsDead || !HasVampiricState(monster.EntityId)) return;
        var healed = HealAmount(damage, monster.MaxHp);
        if (healed <= 0) return;
        var hp = monster.Heal(healed);
        _notices.Enqueue(new VampiricHealNotice(
            monster.EntityId, monster.MapId, (int)hp, monster.HpBarsField, IsBossFlagged(monster.Atype), healed));
    }

    public bool TryTake(out VampiricHealNotice notice) => _notices.TryDequeue(out notice);
}
