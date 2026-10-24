using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public sealed class DrillLootRoller
{
    private readonly IMapItemZoneLookup _catalog;

    public DrillLootRoller(IMapItemZoneLookup catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<MapItemZoneEntry> GetEligibleAtDepth(string mapName, long depthCount)
    {
        ArgumentException.ThrowIfNullOrEmpty(mapName);
        var rows = _catalog.GetByMap(mapName);
        if (rows.Count == 0) return Array.Empty<MapItemZoneEntry>();
        var eligible = new List<MapItemZoneEntry>();
        foreach (var row in rows)
        {
            if (depthCount >= row.MinDepth && depthCount <= row.MaxDepth)
            {
                eligible.Add(row);
            }
        }
        return eligible;
    }

    public DrillRollResult Roll(string mapName, long depthCount, Random? rng = null)
    {
        throw new NotImplementedException(
            "DrillLootRoller.Roll is not implemented: the Avr/Max/Priority roll semantics are not known yet. " +
            "Eligibility filtering in GetEligibleAtDepth is available.");
    }
}

public sealed record DrillRollResult
{
    public required int ItemId { get; init; }
    public required int Quantity { get; init; }
}
