namespace tPoseidon.Core.Data;

public sealed record CharacterSnapshot
{
    public required Character Character { get; init; }

    public IReadOnlyList<Item> EquippedItems { get; init; } = Array.Empty<Item>();
    public IReadOnlyList<Item> InventoryItems { get; init; } = Array.Empty<Item>();

    public IReadOnlyList<Skill> Skills { get; init; } = Array.Empty<Skill>();

    public IReadOnlyList<Quest> ActiveQuests { get; init; } = Array.Empty<Quest>();

    public UniqueCharDutyInfo? UniqueDutyInfo { get; init; }

    public CharAwakening? Awakening { get; init; }

    public ulong CoupleCode { get; init; }

    public PkInfo? PkInfo { get; init; }

    public AccountBlock? ActiveBlock { get; init; }
}
