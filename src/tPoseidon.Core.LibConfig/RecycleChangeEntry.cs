namespace tPoseidon.Core.LibConfig;

public sealed record RecycleChangeEntry(int ExchangeRes, int ExchangeResCnt, int ExchangeSrc, int ExchangeSrcCnt)
{
    public const string TableName = "RecycleChange";
}
