namespace tPoseidon.Core.LibConfig;

public sealed record TransScrollEntry(int ItemId, int TransChrType)
{
    public const string TableName = "TransScrollInfo";
}
