using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters;

public static class MonsterNaturalRecovery
{
    public const int PeriodMs = 6000;
    public const int SpawnFType = -1;

    public static (int Hp, int Mp) Amounts(int fType, int hpRegen, int mpRegen)
    {
        var (hp, mp) = fType switch
        {
            0 or 3 => (16, 8),
            1 or 2 => (8, 16),
            _ => (8, 8),
        };
        return (hpRegen != 0 ? hpRegen : hp, mpRegen != 0 ? mpRegen : mp);
    }

    public static long Tick(MonsterEntity monster, bool fighting, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.RecoverHp(now, fighting, PeriodMs);
    }
}
