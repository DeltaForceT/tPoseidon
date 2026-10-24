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

    public static MeleeApproachDecision Decide(
        CharacterCombatInfo reach,
        int attackerX,
        int attackerY,
        int targetX,
        int targetY,
        bool afterFirstSwing,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(reach);
        ArgumentNullException.ThrowIfNull(rng);

        long dx = attackerX - targetX;
        long dy = attackerY - targetY;
        var distanceSquared = dx * dx + dy * dy;
        long max = reach.MaxAttackRange;
        long min = reach.MinAttackRange;
        long chase = reach.ChaseRange;

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
            var angle = rng.NextDouble() * Math.Tau;
            directionX = Math.Cos(angle);
            directionY = Math.Sin(angle);
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
