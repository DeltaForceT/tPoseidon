namespace tPoseidon.Core.Game.Skills;

public static class KnockSkills
{
    public const int ImpellingRage = 1220;
    public const int ChargingHit = 1221;
    public const int PowerCharging = 1271;
    public const int PowerChargingDo = 1272;
    public const int MassiveAttack = 1348;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string RadiusColumn = "Radius";

    public const int Victims = 5;
    public const int ChargingMoveMs = 1000;
    public const int PowerChargingMoveMs = 3000;
    public const int MassiveMoveMs = 1000;
    public const int WalkStep = 16;

    private const int DistanceBase = 40;
    private const double PowerDivisor = 512.0;

    public static bool IsSelf(int actionId) => actionId is ImpellingRage or PowerCharging;

    public static int ObjectOf(int actionId) => actionId == PowerCharging ? PowerChargingDo : ChargingHit;

    public static (int X, int Y) Destination((int X, int Y) caster, (int X, int Y) victim, int radius)
    {
        var vx = (float)(victim.X - caster.X);
        var vy = (float)(victim.Y - caster.Y);
        var length = (float)Math.Sqrt(vx * vx + vy * vy);
        if (length == 0f) return victim;
        var dx = (float)(vx / length * radius);
        var dy = (float)(vy / length * radius);
        return ((short)(int)dx + caster.X, (short)(int)dy + caster.Y);
    }

    public static (int X, int Y) Walk((int X, int Y) from, (int X, int Y) to, Func<int, int, bool> blocked)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy)) / WalkStep;
        var reached = from;
        for (var step = 1; step <= steps; step++)
        {
            var point = (from.X + dx * step / steps, from.Y + dy * step / steps);
            if (blocked(point.Item1, point.Item2)) return reached;
            reached = point;
        }
        return blocked(to.X, to.Y) ? reached : to;
    }

    public static int PushedDistance((int X, int Y) from, (int X, int Y) to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        return (short)(int)Math.Sqrt((float)(dx * dx + dy * dy));
    }

    public static int Power(int pushed, int casterHp, float ratio) => (int)(unchecked((pushed + DistanceBase) * casterHp) * (double)ratio / PowerDivisor);
}
