namespace tPoseidon.Core.LibConfig;

public readonly record struct CouplePointRange(int MinRelation, int MaxRelation, int MinLove, int MaxLove, int MinFriend, int MaxFriend)
{
    public bool Contains(int relation, int love, int friend) =>
        relation >= MinRelation && relation <= MaxRelation
        && love >= MinLove && love <= MaxLove
        && friend >= MinFriend && friend <= MaxFriend;
}

public sealed record MarriageExchangeEntry(int ResId, string Name, int ResNum, int ReqId, int ReqNum, CouplePointRange Points, int Fee, int Category);

public sealed record MarriageTitleEntry(int Index, string Text, CouplePointRange Points);

public sealed record WeddingRingEntry(int ItemId, int SkillIdMale, int SkillIdFemale);

public sealed record WeddingDressEntry(int ItemId, int DressMale, int DressFemale);

public sealed record MarriageOfficiateLine(int Index, string Text, int Scene);

public readonly record struct CoupleEventPoints(int Relation, int Love, int Friend);

public sealed record CoupleTables(
    IReadOnlyList<MarriageExchangeEntry> Exchange,
    IReadOnlyList<MarriageTitleEntry> Titles,
    IReadOnlyDictionary<int, WeddingRingEntry> Rings,
    IReadOnlyDictionary<int, WeddingDressEntry> Dresses,
    IReadOnlyList<MarriageOfficiateLine> OfficiateLines,
    IReadOnlySet<int> HallZones,
    IReadOnlyList<int> DeleteQuestIds,
    IReadOnlyList<int> DeleteSkillIds,
    IReadOnlyDictionary<int, CoupleEventPoints> EventPoints)
{
    public const string ExchangeTable = "ExchangeMarriage";
    public const string TitleTable = "MarriageTitleInfo";
    public const string RingTable = "WeddingRingInfo";
    public const string DressTable = "WeddingDressInfo";
    public const string OfficiateTable = "MarriageOfficiateMessage";
    public const string HallTable = "WeddingHallInfo";
    public const string DeleteTable = "CoupleDeleteInfo";
    public const string StatusTable = "CoupleStatusInfo";
    public const string DeleteQuestType = "quest";
    public const string DeleteSkillType = "skill";
    public const int DeleteIdColumns = 25;

    public static IReadOnlySet<string> TableNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ExchangeTable, TitleTable, RingTable, DressTable, OfficiateTable, HallTable, DeleteTable, StatusTable,
    };

    public static CoupleTables Empty { get; } = new(
        Array.Empty<MarriageExchangeEntry>(),
        Array.Empty<MarriageTitleEntry>(),
        new Dictionary<int, WeddingRingEntry>(),
        new Dictionary<int, WeddingDressEntry>(),
        Array.Empty<MarriageOfficiateLine>(),
        new HashSet<int>(),
        Array.Empty<int>(),
        Array.Empty<int>(),
        new Dictionary<int, CoupleEventPoints>());

    public MarriageExchangeEntry? ExchangeRow(int resId) => Exchange.FirstOrDefault(row => row.ResId == resId);
}
