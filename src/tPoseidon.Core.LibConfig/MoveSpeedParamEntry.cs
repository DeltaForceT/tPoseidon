namespace tPoseidon.Core.LibConfig;

public readonly record struct MoveSpeedParamEntry(int ObjectType, float WalkSpeed, float RunSpeed, float ChaseSpeed)
{
    public const int PlayerObjectType = 1;
}
