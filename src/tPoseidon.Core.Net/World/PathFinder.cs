namespace tPoseidon.Core.Net.World;

public sealed class PathFinder
{
    public Stage? Stage { get; private set; }
    public LyrReader.GroundAttributeMap? GroundAttribute { get; private set; }

    public IReadOnlyList<int> StrategyIds { get; } = new[] { 1, 2, 3, 4, 5 };

    public void AcquireStage(Stage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        Stage = stage;
    }

    public void LoadGroundAttribute(LyrReader.GroundAttributeMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        GroundAttribute = map;
    }

    public bool IsWalkable(int tx, int ty)
    {
        return GroundAttribute?.IsWalkable(tx, ty) ?? true;
    }
}
