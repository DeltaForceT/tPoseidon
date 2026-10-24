using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public static class CharacterPetReinforceRules
{
    public const ushort Ok = 0;
    public const ushort ReinforceItemMissing = 1;
    public const ushort NotReinforceItem = 2;
    public const ushort PetMissing = 4;
    public const ushort NotCharacterPet = 5;
    public const ushort PetEquipped = 6;
    public const ushort Failed = 7;

    public const int ReinforceEffect = 48;
    public const int CharacterPetSubType = 78;
    public const int StatType = 0xE;
    public const int MarkType = 0xF;
    public const int MarkId = 27001;
    public const int LastStatIdIndex = 11;

    public static ushort Check(
        ItemParamEntry? reinforceItem,
        int reinforceCount,
        ItemParamEntry? pet,
        bool petEquipped,
        IReadOnlyList<ItemProperty> petProperties,
        CharacterPetReinforceTables tables,
        out CharacterPetReinforceEntry? row)
    {
        ArgumentNullException.ThrowIfNull(petProperties);
        ArgumentNullException.ThrowIfNull(tables);
        row = null;
        if (reinforceItem is null || reinforceCount < 1) return ReinforceItemMissing;
        if (reinforceItem.Effect != ReinforceEffect) return NotReinforceItem;
        if (pet is null) return PetMissing;
        if (pet.SubType != CharacterPetSubType) return NotCharacterPet;
        if (petEquipped) return PetEquipped;
        if (!tables.Rows.TryGetValue((reinforceItem.Id, BasicId(pet.Id, petProperties)), out var found)) return Failed;
        row = found;
        return Ok;
    }

    public static int BasicId(int petItemId, IReadOnlyList<ItemProperty> petProperties)
    {
        ArgumentNullException.ThrowIfNull(petProperties);
        foreach (var property in petProperties)
        {
            if (property.Type == MarkType) return (int)property.Extra;
        }
        return petItemId;
    }

    public static List<ItemProperty> ResultProperties(CharacterPetReinforceEntry row, IReadOnlyList<ItemProperty> petProperties, ItemParamEntry result)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(petProperties);
        ArgumentNullException.ThrowIfNull(result);
        var list = petProperties.Where(property => Keeps(property, row.RareType)).ToList();
        if (result.HpRecoveryRate != 0) list.Add(new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.HpRecoveryId, (short)Math.Truncate(result.HpRecoveryRate), 0));
        if (result.MpRecoveryRate != 0) list.Add(new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.MpRecoveryId, (short)Math.Truncate(result.MpRecoveryRate), 0));
        for (var i = 0; i < row.Stats.Count; i++)
        {
            if (row.Stats[i] != 0) list.Add(new ItemProperty(StatType, StatId(i), (short)row.Stats[i], 0));
        }
        list.Add(new ItemProperty(MarkType, MarkId, 0, (uint)row.BasicId));
        return list;
    }

    public static int StatId(int index) => index is >= 0 and <= LastStatIdIndex ? ItemPropertyRules.FirstStatId + index : 0;

    private static bool Keeps(ItemProperty property, bool rareType)
    {
        if (property.Type is StatType or MarkType) return false;
        if (property.Type == ItemPropertyTypes.Stat && property.Id is ItemPropertyRules.HpRecoveryId or ItemPropertyRules.MpRecoveryId) return false;
        if (!rareType) return true;
        if (property.Type == ItemComposingRules.AppearanceType && property.Id == ItemComposingRules.AppearanceId) return false;
        if (property.Type == ItemComposingRules.OptionsType && property.Id == ItemComposingRules.OptionsId) return false;
        return true;
    }
}
