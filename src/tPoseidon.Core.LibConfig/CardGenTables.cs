namespace tPoseidon.Core.LibConfig;

public sealed record CardGenGradeEntry(int Grade, float Param);

public sealed record CardGenCardEntry(int ItemId, int ItemFType, int CardNum, int MpCon, int Grade, float Param, int DailyLimit);

public sealed record CardGenTables(IReadOnlyList<CardGenGradeEntry> Grades, IReadOnlyList<CardGenCardEntry> Cards)
{
    public const string GradeTable = "CardGenGrade";
    public const string ItemTable = "ItemParam2";
    public const string ItemExtensionTable = "ItemParamEx2";
    public const int CardItemType = 11;

    public static CardGenTables Empty { get; } = new(Array.Empty<CardGenGradeEntry>(), Array.Empty<CardGenCardEntry>());
}
