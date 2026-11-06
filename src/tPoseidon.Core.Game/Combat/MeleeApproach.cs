using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public enum MeleeApproachKind
{
    InRange,
    Move,
    BackOff,
    GiveUp,
}

public readonly record struct MeleeApproachDecision(MeleeApproachKind Kind, int X, int Y);

public static class MeleeApproach
{
    public const byte DefaultMoveType = 0;
    public const byte WalkMoveType = 1;
    public const byte RunMoveType = 2;
    public const byte ChaseMoveType = 3;

    public static byte EffectiveMoveType(byte ordered) => ordered == DefaultMoveType ? RunMoveType : ordered;

    public static readonly TimeSpan OrderGap = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan BackOffGap = TimeSpan.FromMilliseconds(100);

    public const int RandomSideAfterOrders = 2;
    public const int RandomSideAngles = 6;

    public static bool OrdersFromRandomSide(int ordersSinceInRange) => ordersSinceInRange > RandomSideAfterOrders;

    public static (int X, int Y) RandomSidePoint(CharacterCombatInfo reach, int targetX, int targetY, Random rng)
    {
        ArgumentNullException.ThrowIfNull(reach);
        ArgumentNullException.ThrowIfNull(rng);
        var (directionX, directionY) = RandomSide(rng);
        var radius = reach.ApproachRadius;
        return (targetX + (int)(directionX * radius), targetY + (int)(directionY * radius));
    }

    private static (double X, double Y) RandomSide(Random rng)
    {
        double angle = rng.Next(RandomSideAngles);
        return (Math.Cos(angle), -Math.Sin(angle));
    }

    public static bool StopsMidLeg(
        CharacterCombatInfo reach,
        int attackerX,
        int attackerY,
        int targetX,
        int targetY)
    {
        ArgumentNullException.ThrowIfNull(reach);

        var distanceSquared = SquaredDistance(attackerX, attackerY, targetX, targetY);
        long max = reach.MaxAttackRange;
        long min = reach.MinAttackRange;
        return distanceSquared <= max * max && distanceSquared >= min * min;
    }

    private static long SquaredDistance(int ax, int ay, int bx, int by)
    {
        long dx = ax - bx;
        long dy = ay - by;
        return dx * dx + dy * dy;
    }

    public static MeleeApproachDecision Decide(
        CharacterCombatInfo reach,
        int attackerX,
        int attackerY,
        int targetX,
        int targetY,
        bool afterFirstSwing,
        Random rng,
        bool immobile = false)
    {
        ArgumentNullException.ThrowIfNull(reach);
        ArgumentNullException.ThrowIfNull(rng);

        long dx = attackerX - targetX;
        long dy = attackerY - targetY;
        var distanceSquared = dx * dx + dy * dy;
        long max = reach.MaxAttackRange;
        long min = reach.MinAttackRange;
        long chase = immobile ? Math.Max(reach.ChaseRange, max) : reach.ChaseRange;

        if (afterFirstSwing && distanceSquared > chase * chase)
        {
            return new MeleeApproachDecision(MeleeApproachKind.GiveUp, attackerX, attackerY);
        }
        if (distanceSquared <= max * max && distanceSquared >= min * min)
        {
            return new MeleeApproachDecision(MeleeApproachKind.InRange, attackerX, attackerY);
        }

        double directionX;
        double directionY;
        if (distanceSquared == 0)
        {
            (directionX, directionY) = RandomSide(rng);
        }
        else
        {
            var distance = Math.Sqrt(distanceSquared);
            directionX = dx / distance;
            directionY = dy / distance;
        }

        var radius = reach.ApproachRadius;
        return new MeleeApproachDecision(
            distanceSquared < min * min ? MeleeApproachKind.BackOff : MeleeApproachKind.Move,
            targetX + (int)(directionX * radius),
            targetY + (int)(directionY * radius));
    }
}
