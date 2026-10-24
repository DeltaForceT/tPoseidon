namespace tPoseidon.Core.LibConfig;

public sealed record MonItemTableDropEntry
{
    public const int SlotCount = 20;

    public required int Id { get; init; }
    public required IReadOnlyList<DropItem> Slots { get; init; }
}
