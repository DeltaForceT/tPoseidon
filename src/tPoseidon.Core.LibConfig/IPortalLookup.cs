namespace tPoseidon.Core.LibConfig;

public interface IPortalLookup
{
    PortalDestination? FindDestination(ushort sourceMapId, int posX, int posY);
    PortalDestination? FindDestinationWithin(ushort sourceMapId, int posX, int posY, int margin);
    IReadOnlyList<PortalEntrance> GetEntrances(ushort mapId);
    int MapCount { get; }
    int TotalPortals { get; }

    (ushort X, ushort Y) FindArrival(ushort fromMapId, ushort destMapId);
}

public sealed record PortalDestination
{
    public required ushort DestMapId { get; init; }
    public required int DestPortalId { get; init; }
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
}
