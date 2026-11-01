namespace tPoseidon.Core.LibConfig;

public sealed record MyCampItemEntry(int Id, string Name, string IconFile, int IconBundle, int Attr = 0)
{
    public const string TableName = "MCItemParam";
}
