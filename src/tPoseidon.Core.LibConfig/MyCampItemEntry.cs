namespace tPoseidon.Core.LibConfig;

public sealed record MyCampItemEntry(int Id, string Name, string IconFile, int IconBundle)
{
    public const string TableName = "MCItemParam";
}
