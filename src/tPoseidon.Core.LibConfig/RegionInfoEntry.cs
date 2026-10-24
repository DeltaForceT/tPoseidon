namespace tPoseidon.Core.LibConfig;

public sealed record RegionInfoEntry
{
    public required string MapName { get; init; }
    public required int RegionId { get; init; }
    public required int LTcoordX { get; init; }
    public required int LTcoordY { get; init; }
    public required int RBcoordX { get; init; }
    public required int RBcoordY { get; init; }

    public ushort CenterX => (ushort)((LTcoordX + RBcoordX) / 2);
    public ushort CenterY => (ushort)((LTcoordY + RBcoordY) / 2);
    public int Width => RBcoordX - LTcoordX;
    public int Height => RBcoordY - LTcoordY;
}
