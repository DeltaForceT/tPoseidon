namespace tPoseidon.Core.LibConfig;

public readonly record struct PartyZoneClosing(int Seconds, int DestZone);

public sealed record PartyZoneTables(
    IReadOnlyDictionary<int, int> ReEnterMinutes,
    IReadOnlyDictionary<int, IReadOnlySet<int>> Bosses,
    IReadOnlyDictionary<int, PartyZoneClosing> Closings)
{
    public const string InfoTable = "PartyZoneInfo";

    public static PartyZoneTables Empty { get; } = new(
        new Dictionary<int, int>(),
        new Dictionary<int, IReadOnlySet<int>>(),
        new Dictionary<int, PartyZoneClosing>());
}
