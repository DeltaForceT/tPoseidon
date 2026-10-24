namespace tPoseidon.Core.LibConfig;

public sealed record PartyZoneTables(IReadOnlyDictionary<int, int> ReEnterMinutes, IReadOnlyDictionary<int, IReadOnlySet<int>> Bosses)
{
    public const string InfoTable = "PartyZoneInfo";

    public static PartyZoneTables Empty { get; } = new(new Dictionary<int, int>(), new Dictionary<int, IReadOnlySet<int>>());
}
