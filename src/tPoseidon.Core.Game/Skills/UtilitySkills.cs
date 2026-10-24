namespace tPoseidon.Core.Game.Skills;

public static class UtilitySkills
{
    public const int Dash = 1142;
    public const int ItemDetector = 1147;
    public const int BasicDetector = 1170;
    public const int Mine = 1192;
    public const int MineBlast = 1193;
    public const int MineMaster = 1267;
    public const int MineMasterBlast = 1268;
    public const int Hiding = 1286;
    public const int OpticalEyes = 1289;
    public const int LandmineSkill = 3104;

    public const string ApplyRatioColumn = "ApplyRatio";
    public const string TrapPowerColumn = "TrapAp";
    public const string RadiusColumn = "Radius";
    public const string SecondsColumn = "Seconds";
    public const string SecondColumn = "Second";

    public const float DashSpeed = 600f;
    public const int DashStopDistance = 30;
    public const int DashLongestMs = 2001;
    public const int MineArmingMs = 0xC80;
    public const int RevealScanMs = 400;
    public const uint HiddenMask = 1;
    public const uint VisibleMask = 0;

    private const int TrapStep = 8;
    private const int MasterDigStep = 16;
    private const double MillisecondsPerSecond = 1000.0;

    public static bool IsSelf(int actionId) => actionId == BasicDetector;

    public static bool IsGround(int actionId) => actionId is Dash or ItemDetector or Mine or MineMaster;

    public static int BlastOf(int actionId) => actionId == MineMaster ? MineMasterBlast : MineBlast;

    public static int MinePower(int actionId, int da, int lk, float trapAp) => actionId == MineMaster
        ? (int)((da * MasterDigStep + lk * TrapStep) * (double)trapAp)
        : (int)((da + lk) * TrapStep * (double)trapAp);

    public static int DashMs(double distance) =>
        distance <= DashStopDistance ? 0 : (int)Math.Min(DashLongestMs, Math.Ceiling((distance - DashStopDistance) / DashSpeed * MillisecondsPerSecond));

    public static (int X, int Y) DashEnd((int X, int Y) from, (int X, int Y) to, int dashMs)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= DashStopDistance) return from;
        var travelled = Math.Min(distance - DashStopDistance, DashSpeed * dashMs / MillisecondsPerSecond);
        return ((int)Math.Round(from.X + dx / distance * travelled), (int)Math.Round(from.Y + dy / distance * travelled));
    }

    public static (IReadOnlyList<ushort> Added, IReadOnlyList<ushort> Removed) RevealDiff(IReadOnlyCollection<ushort> known, IReadOnlyCollection<ushort> seen)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(seen);
        return (seen.Where(id => !known.Contains(id)).Distinct().ToList(), known.Where(id => !seen.Contains(id)).ToList());
    }
}
