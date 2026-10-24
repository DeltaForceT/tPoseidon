using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class SkillAreaRules
{
    public const int ManaRing = 1086;
    public const int ManaRingBooster = 2012;
    public const int MostVictims = 7;
    public const string RadiusColumn = "Radius";
    public const string PowerColumn = "AP";

    public static (int Power, float Coefficient, float Radius) Ring(SkillTableRow action, SkillTableRow? booster)
    {
        ArgumentNullException.ThrowIfNull(action);
        var power = action.Amount(PowerColumn) ?? 0;
        var coefficient = action.Value(SkillHitShapes.ApplyRatioColumn) ?? 0f;
        var radius = (float)(action.Amount(RadiusColumn) ?? 0);
        if (booster is null) return (power, coefficient, radius);
        var boosted = booster.Value(SkillHitShapes.ApplyRatioColumn) ?? 0f;
        return (power + (booster.Amount(PowerColumn) ?? 0), boosted > 0f ? boosted : coefficient, radius);
    }

    public static float Distance(int fromX, int fromY, int toX, int toY)
    {
        double dx = toX - fromX;
        double dy = toY - fromY;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool Reaches(float distance, float radius) => !(radius < distance);

    public static long WithFalloff(long damage, float distance, float radius)
    {
        var falloff = 1.0f - distance / radius;
        return WireLimits.Saturate(Math.Truncate((float)damage * falloff));
    }
}
