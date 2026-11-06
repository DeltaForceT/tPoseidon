using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public static class ReferencedItemRows
{
    public static LibConfigRowPatchData Data { get; } = new(ReferencedRows.Patches, "rows the game data leads to", "ids", "found");
}
