namespace tPoseidon.Core.LibConfig;

public sealed record TeleportDestination(int Zone, int Cost, string LoadImage);

public sealed record TeleportEntry
{
    public const int DestinationColumns = 7;

    public required int Id { get; init; }
    public int MinLevel { get; init; }
    public IReadOnlyList<TeleportDestination> Destinations { get; init; } = Array.Empty<TeleportDestination>();
}
