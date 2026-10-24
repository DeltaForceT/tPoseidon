using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class PolarBearPeriodic
{
    public const double DefenceShare = 0.75;
    public const double PercentDivisor = 100.0;

    public static int MoleHammerDamage(SkillTableRow action, int casterAp, int targetDp, int physicalResist)
    {
        ArgumentNullException.ThrowIfNull(action);
        var power = casterAp * (double)(action.Value(SkillHitShapes.RatioColumn) ?? 0f);
        var damage = (power - DefenceShare * targetDp) * (1.0 - physicalResist / PercentDivisor);
        return (int)Math.Clamp(Math.Truncate(damage), 0, int.MaxValue);
    }
}
