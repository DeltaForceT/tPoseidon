namespace tPoseidon.Core.Game.Combat;

public static class AttackInterval
{
    public static DateTimeOffset FirstSwingAt(DateTimeOffset now, DateTimeOffset notBefore) =>
        notBefore > now ? notBefore : now;

    public static bool IsAttacking(ushort targetEntityId, DateTimeOffset nextSwingAt) =>
        targetEntityId != 0 && nextSwingAt != default;
}
