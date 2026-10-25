namespace tPoseidon.Core.LibConfig;

public sealed record BonusPresentEntry(int PresentId, int SelectItemId, int PickBonusPresentId, int UnpickBonusPresentId)
{
    public const string TableName = "BonusPresentItemInfo";
}

public sealed record KeyboxEntry(
    int BoxItemId, IReadOnlyList<int> KeyItemIds, bool OnlyKey, int NormalItemId, int RareItemIdA, int RareItemIdB, int RareBRatePercent)
{
    public const string TableName = "KeyboxItemInfo";
    public const char KeySeparator = '|';
}

public sealed record KeyRateEntry(int KeyItemId, int RaiseDroprate)
{
    public const string TableName = "KeyItemRateInfo";
}

public sealed record BoxTables(
    IReadOnlyDictionary<int, BonusPresentEntry> Bonuses,
    IReadOnlyDictionary<int, KeyboxEntry> Keyboxes,
    IReadOnlyDictionary<int, KeyRateEntry> KeyRates)
{
    public static IReadOnlySet<string> TableNames { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        BonusPresentEntry.TableName, KeyboxEntry.TableName, KeyRateEntry.TableName,
    };

    public static BoxTables Empty { get; } = new(
        new Dictionary<int, BonusPresentEntry>(), new Dictionary<int, KeyboxEntry>(), new Dictionary<int, KeyRateEntry>());
}
