using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters;

public static class MonsterElementDamage
{
    public static long ApplyElementDamage(this MonsterEntity monster, long amount, int elementCode)
    {
        ArgumentNullException.ThrowIfNull(monster);
        if (monster.Boss is not null && amount > 0)
        {
            var index = BossElements.IndexOf(elementCode);
            if (index != BossElements.None) monster.RecordElementDamage(index, amount);
        }
        return monster.ApplyDamage(amount);
    }
}
