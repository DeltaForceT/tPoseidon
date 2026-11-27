namespace tPoseidon.Core.Game.Combat;

public static class PlayerAttackOrder
{
    public static bool IsActive(bool handedOff, DateTimeOffset nextSwingAt) =>
        !handedOff && nextSwingAt != default;
}
