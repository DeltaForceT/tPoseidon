namespace tPoseidon.Core.LibConfig;

public sealed record CardSpecialSetEntry(int Id, int Grade, IReadOnlyList<int> CardIds, IReadOnlyList<(int ItemId, float Rate)> Presents)
{
    public const string TableName = "Card_SpecialSet";
    public const int CardColumns = 5;
    public const int PresentColumns = 4;
}
