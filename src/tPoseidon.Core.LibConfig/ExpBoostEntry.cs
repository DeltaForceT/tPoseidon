namespace tPoseidon.Core.LibConfig;

public sealed record ExpBoostEntry(int ItemId, float ExpAdvantage, float TmAdvantage)
{
    public const string TableName = "Item_ExpParam";
}
