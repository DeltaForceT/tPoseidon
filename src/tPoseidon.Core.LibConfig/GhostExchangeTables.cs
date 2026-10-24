namespace tPoseidon.Core.LibConfig;

public sealed record GhostExchangeTables(IReadOnlyDictionary<int, IReadOnlyList<int>> Groups, IReadOnlyList<int> RefineLevels)
{
    public const string GroupTable = "GhostExchange_Group";
    public const string RefineLevelTable = "GhostExchange_RefineLevel";
    public const int GroupColumns = 12;
    public const int RefineLevelColumns = 14;

    public static GhostExchangeTables Empty { get; } = new(new Dictionary<int, IReadOnlyList<int>>(), Array.Empty<int>());
}
