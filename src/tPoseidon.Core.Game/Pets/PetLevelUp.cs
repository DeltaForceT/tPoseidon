using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public readonly record struct PetLevelUpRequest(ulong PetWireUid, ulong CatalystWireUid);

public static class PetLevelUpRules
{
    public const ushort Ok = 0;
    public const ushort CannotLevelUp = 1;
    public const ushort WrongCatalyst = 2;
    public const ushort PetMissing = 7;
    public const ushort CatalystMissing = 8;
    public const ushort NotEnoughCatalyst = 9;
    public const ushort Failed = 10;

    public static ushort Check(ItemParamEntry? pet, HeldItem? catalyst, PetLevelUpTables tables, out PetLevelUpEntry? row)
    {
        ArgumentNullException.ThrowIfNull(tables);
        row = null;
        if (pet is null) return PetMissing;
        if (catalyst is not { } held) return CatalystMissing;
        if (pet.Type == PetRules.PetItemType && pet.CompoundSlot > 0) return CannotLevelUp;
        if (pet.Type != PetRules.PetItemType || !tables.Rows.TryGetValue(pet.Id, out var found)) return CannotLevelUp;
        if (found.CatalystItemId != held.ItemId) return WrongCatalyst;
        if (held.Stack < found.CatalystCount) return NotEnoughCatalyst;
        row = found;
        return Ok;
    }
}
