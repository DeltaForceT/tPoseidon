using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class PolarBearPeriodic
{
    public const double DefenceShare = 0.75;
    public const double PercentDivisor = 100.0;

    public const int DigPowerStep = 16;

    public static long MoleHammerDamage(SkillTableRow action, long casterDa, long weaponDa, long targetDp, int physicalResist)
    {
        ArgumentNullException.ThrowIfNull(action);
        var power = (double)(action.Value(SkillHitShapes.RatioColumn) ?? 0f) * (DigPowerStep * casterDa + weaponDa);
        var damage = (power - DefenceShare * targetDp) * (1.0 - physicalResist / PercentDivisor);
        return Math.Max(0L, WireLimits.Saturate(Math.Truncate(damage)));
    }
}
