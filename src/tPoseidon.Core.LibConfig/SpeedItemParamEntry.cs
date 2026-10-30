namespace tPoseidon.Core.LibConfig;

public readonly record struct SpeedItemParamEntry(int ItemId, float AddSpeedRatio)
{
    public const string TableName = "SpeedItemParam";
}
