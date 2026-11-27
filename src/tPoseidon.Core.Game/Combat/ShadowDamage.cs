namespace tPoseidon.Core.Game.Combat;

public readonly record struct ShadowDamageResult(long Damage, int ElementMask);

public static class ShadowDamage
{
    public const int AttackSlot = 10;
    public const int ResistSlot = 21;
    public const int ElementMask = 0x400;

    private const double PercentScale = 0.009999999776482582;

    public static ShadowDamageResult PlayerAgainstShadowMonster(long damage, bool attackerSeesShadow, int attackSlot, int monsterShadowResist, bool shadowMonster)
    {
        if (!attackerSeesShadow || !shadowMonster || attackSlot == 0) return new ShadowDamageResult(damage, 0);
        var bonus = Scale(damage * attackSlot);
        var cut = Scale(bonus * monsterShadowResist);
        return new ShadowDamageResult(damage + (bonus - cut), ElementMask);
    }

    public static ShadowDamageResult ShadowMonsterAgainstPlayer(long damage, bool shadowMonster, bool victimSeesShadow, int playerShadowResist)
    {
        if (!shadowMonster || !victimSeesShadow) return new ShadowDamageResult(damage, 0);
        return new ShadowDamageResult(damage - Scale(damage * playerShadowResist), ElementMask);
    }

    private static long Scale(long value) => (long)(value * PercentScale);
}
