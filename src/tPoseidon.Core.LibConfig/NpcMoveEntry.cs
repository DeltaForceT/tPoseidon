namespace tPoseidon.Core.LibConfig;

public sealed record NpcMoveEntry
{
    public const int ItemColumns = 4;
    public const int FirstZone = 1;
    public const int LastZone = 999;
    public const int AnyFType = -1;
    public const int AnyCharacterType = 0;
    public const int TakesItems = 1;

    public required int Id { get; init; }
    public int Zone { get; init; }
    public int DestId { get; init; }
    public IReadOnlyList<(int ItemId, int Count)> Items { get; init; } = Array.Empty<(int, int)>();
    public int MinLevel { get; init; }
    public int MaxLevel { get; init; }
    public int FType { get; init; } = AnyFType;
    public int CharacterType { get; init; }
    public int TakeItem { get; init; }
}
