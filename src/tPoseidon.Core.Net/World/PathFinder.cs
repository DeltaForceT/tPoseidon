namespace tPoseidon.Core.Net.World;

public sealed class PathFinder
{
    public Stage? Stage { get; private set; }
    public LyrReader.GroundAttributeMap? GroundAttribute { get; private set; }

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
}
