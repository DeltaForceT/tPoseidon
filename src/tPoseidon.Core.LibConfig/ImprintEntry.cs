namespace tPoseidon.Core.LibConfig;

public sealed record ImprintEntry(int ItemId, int Hours, int Type, float Value)
{
    public const string TableName = "ImprintSkillInfo";
}
