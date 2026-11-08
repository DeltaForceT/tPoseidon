using tPoseidon.Core.Common;
namespace tPoseidon.Core.Game.Skills.Effects;

public static class MonsterLifeBarrier
{
    private const double PercentScale = 100.0;
    private const long LeftAlive = 1;

    public static bool Reflects(IncomingHit hit) =>
        hit.Damage > 0
        && (hit.Flags & IncomingHits.FlagBarrier) == 0
        && hit.Kind is not (IncomingHits.KindMiss or IncomingHits.KindGuarded or IncomingHits.KindBlockedByGuard or IncomingHits.KindAbsorbed);

    public static long Reflection(long damage, long ownerMaxHp, long attackerHp, float ratio)
    {
        if (ownerMaxHp <= 0 || attackerHp <= LeftAlive) return 0;
        var reflect = WireLimits.Saturate(damage / (double)ownerMaxHp * attackerHp * PercentScale * ratio);
        return reflect > 0 && reflect <= attackerHp ? attackerHp - LeftAlive : reflect;
    }
}
