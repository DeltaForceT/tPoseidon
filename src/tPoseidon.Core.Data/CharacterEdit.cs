namespace tPoseidon.Core.Data;

public enum CharacterEditKind : byte
{
    Rename = 1,
    Hair = 2,
    Gender = 3,
    GrowthWeights = 4,
    WarehouseSlots = 5,
}

public static class CharacterEditCodes
{
    public const uint Done = 0;
    public const uint Failed = 101;
    public const uint InvalidName = 1019;
    public const uint NameTaken = 1023;
}

public sealed record CharacterEdit
{
    public required CharacterEditKind Kind { get; init; }
    public string NewName { get; init; } = string.Empty;
    public int ItemIndex { get; init; }
    public byte HairSlot { get; init; }
    public ushort HairColor { get; init; }
    public Species NewSpecies { get; init; }
    public ushort EarItemIndex { get; init; }
    public ushort TailItemIndex { get; init; }
    public byte PtsP { get; init; }
    public byte PtsM { get; init; }
    public byte PtsS { get; init; }
    public byte PtsC { get; init; }
    public ushort WarehouseSlots { get; init; }

    public static CharacterEdit Rename(string newName, int itemIndex) =>
        new() { Kind = CharacterEditKind.Rename, NewName = newName, ItemIndex = itemIndex };

    public static CharacterEdit Hair(byte slot, ushort color) =>
        new() { Kind = CharacterEditKind.Hair, HairSlot = slot, HairColor = color };

    public static CharacterEdit Gender(Species newSpecies) =>
        new()
        {
            Kind = CharacterEditKind.Gender,
            NewSpecies = newSpecies,
            EarItemIndex = SpeciesDefaults.EarItemIndexForSpecies(newSpecies),
            TailItemIndex = SpeciesDefaults.TailItemIndexForSpecies(newSpecies),
        };

    public static CharacterEdit GrowthWeights(byte p, byte m, byte s, byte c) =>
        new() { Kind = CharacterEditKind.GrowthWeights, PtsP = p, PtsM = m, PtsS = s, PtsC = c };

    public static CharacterEdit Storage(ushort slots) =>
        new() { Kind = CharacterEditKind.WarehouseSlots, WarehouseSlots = slots };
}
