using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public enum EquipRefusal
{
    GmOnly,
    NotEquipable,
    SameCharacterTypeAccessory,
    WrongCharacterType,
    SameAccessoryKind,
    AlreadyEquipped,
    LevelTooLow,
    StatTooLow,
    SameAccessory,
    WrongSlot,
    ShieldWithGun,
    MissingWeaponAbility,
    OccupantInWrongSlot,
    NotCommonItem,
    BulletWithoutGun,
    SameBulletLoaded,
}

public enum ReleaseRefusal
{
    EarsOrTailBelowLevel,
    SlotHoldsAnotherItem,
    NotLoaded,
}

public sealed record WornItem(byte Slot, Item Item, ItemParamEntry Definition);

public static class EquipmentRules
{
    public const int NoSlot = -1;
    public const int EquipableOption = 0x10;
    public const int BrokenEquipableOption = 0x40000;
    public const byte BrokenRefineState = 1;
    public const int CharacterTypeOption = 0x4000;
    public const int GmOnlyExtendedOption = 0x400;
    public const int AccessoryType = 7;
    public const int ShieldType = 8;
    public const int EarsType = 17;
    public const int TailType = 18;
    public const int GunSubType = 54;
    public const int HammerSubType = 82;
    public const int WeaponType = 2;
    public const int GunCarrierAbility = 1;
    public const int HammerAbility = 13;
    public const int SpeedAccessorySubType = 53;
    public const int EarsAndTailReleaseLevel = 20;
    public const int CharacterTypeCount = 27;
    public const uint AllCharacterTypes = 0x07FF_FFFF;
    public const uint GenderOneCharacterTypes = 0x02A9_54AA;
    public const int GenderOne = 1;
    public const int ItemStatCount = 12;
    public const int CommonClass = 1;
    public const int BulletType = 29;
    public const int ThrowItemType = 16;
    public const int CardType = 11;
    public const int ThrowCardSubType = 60;

    private static readonly uint[] CharacterGroupTypes = { 0x040E_0703, 0x0030_180C, 0x00C0_6030, 0x0301_80C0 };
    private static readonly int[] OnePerWearerSubTypes = { 52, SpeedAccessorySubType, 62 };
    private static readonly byte[] AccessorySlots = { EquipmentSlot.Accessory, EquipmentSlot.SecondAccessory, EquipmentSlot.SpeedAccessory };
    private static readonly byte[] WeaponAndAccessorySlots = { EquipmentSlot.Accessory, EquipmentSlot.SecondAccessory, EquipmentSlot.Weapon, EquipmentSlot.SpeedAccessory };

    public static IReadOnlyList<WornItem> WornBy(IItemStore items, IItemParamLookup catalog, ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(catalog);
        var worn = new List<WornItem>(EquipmentSlot.Count);
        foreach (var item in items.GetForCharacterBySlotKind(characterId, SlotKind.Equipment))
        {
            if (item.SlotPosition >= EquipmentSlot.Count || catalog.GetById(item.ItemIndex) is not { } definition) continue;
            worn.Add(new WornItem(item.SlotPosition, item, definition));
        }
        return worn;
    }

    public static byte FirstFreeBagPosition(IReadOnlyList<Item> bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var taken = new HashSet<byte>(bag.Select(held => held.SlotPosition));
        for (var position = 0; position < byte.MaxValue; position++)
        {
            if (!taken.Contains((byte)position)) return (byte)position;
        }
        return byte.MaxValue;
    }

    public static int SlotOfType(int type) => type switch
    {
        3 => EquipmentSlot.Hat,
        2 => EquipmentSlot.Weapon,
        ShieldType => EquipmentSlot.Shield,
        4 => EquipmentSlot.Belt,
        AccessoryType => EquipmentSlot.Accessory,
        EarsType => EquipmentSlot.Ear,
        TailType => EquipmentSlot.Tail,
        6 => EquipmentSlot.Drill,
        13 => EquipmentSlot.Pet,
        >= 19 and <= 26 => EquipmentSlot.FirstCostume + (type - 19),
        5 => EquipmentSlot.Shoes,
        27 => EquipmentSlot.Socks,
        34 => EquipmentSlot.Cape,
        _ => NoSlot,
    };

    public static int SlotFor(ItemParamEntry definition, Func<byte, bool> occupied)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(occupied);
        if (definition.Type != AccessoryType) return SlotOfType(definition.Type);
        if (definition.SubType == SpeedAccessorySubType && !occupied(EquipmentSlot.SpeedAccessory)) return EquipmentSlot.SpeedAccessory;
        if (occupied(EquipmentSlot.Accessory) && !occupied(EquipmentSlot.SecondAccessory)) return EquipmentSlot.SecondAccessory;
        return EquipmentSlot.Accessory;
    }

    public static bool IsAccessorySlot(int slot) => slot is >= 0 and <= byte.MaxValue && Array.IndexOf(AccessorySlots, (byte)slot) >= 0;

    public static bool RestrictsCharacterType(ItemParamEntry definition) =>
        (definition.Options & CharacterTypeOption) != 0 || definition.CharacterGroupFlags != 0 || definition.CharacterGender != 0;

    public static uint CharacterTypeMask(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.CharacterGroupFlags == 0 && definition.CharacterGender == 0) return (uint)definition.CharacterTypeFlags;
        var mask = AllCharacterTypes;
        for (var group = 0; group < CharacterGroupTypes.Length; group++)
        {
            if (definition.CharacterGroupFlags != 0 && (definition.CharacterGroupFlags & (1 << group)) == 0) mask &= ~CharacterGroupTypes[group];
        }
        if (definition.CharacterGender != 0)
        {
            mask &= definition.CharacterGender == GenderOne ? GenderOneCharacterTypes : ~GenderOneCharacterTypes;
        }
        return mask;
    }

    public static bool FitsCharacterType(uint mask, int characterType) =>
        characterType is >= 1 and <= CharacterTypeCount && (mask & (1u << (characterType - 1))) != 0;

    public static EquipRefusal? CheckEquip(
        ItemParamEntry definition,
        Item item,
        int slot,
        IReadOnlyList<WornItem> worn,
        int level,
        int characterType,
        IReadOnlyList<int> statTotals,
        Func<int, bool> holdsAbility)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(worn);
        ArgumentNullException.ThrowIfNull(statTotals);
        ArgumentNullException.ThrowIfNull(holdsAbility);

        if ((definition.ExtendedOptions & GmOnlyExtendedOption) != 0) return EquipRefusal.GmOnly;
        if ((definition.Options & EquipableOption) == 0
            || (item.RefineState == BrokenRefineState && (definition.Options & BrokenEquipableOption) == 0))
        {
            return EquipRefusal.NotEquipable;
        }
        if (RestrictsCharacterType(definition))
        {
            var mask = CharacterTypeMask(definition);
            if (IsAccessorySlot(slot) && worn.Any(held => IsAccessorySlot(held.Slot) && CharacterTypeMask(held.Definition) == mask))
            {
                return EquipRefusal.SameCharacterTypeAccessory;
            }
            if (!FitsCharacterType(mask, characterType)) return EquipRefusal.WrongCharacterType;
        }
        if (IsAccessorySlot(slot)
            && Array.IndexOf(OnePerWearerSubTypes, definition.SubType) >= 0
            && worn.Any(held => Array.IndexOf(WeaponAndAccessorySlots, held.Slot) >= 0 && held.Definition.SubType == definition.SubType))
        {
            return EquipRefusal.SameAccessoryKind;
        }
        if (worn.Any(held => held.Item.ItemUid == item.ItemUid)) return EquipRefusal.AlreadyEquipped;
        if (level < definition.MinLevel) return EquipRefusal.LevelTooLow;
        if (definition.MinStatLevel != 0
            && (definition.MinStatType < 0 || definition.MinStatType >= statTotals.Count || statTotals[definition.MinStatType] < definition.MinStatLevel))
        {
            return EquipRefusal.StatTooLow;
        }
        if (worn.Any(held => IsAccessorySlot(held.Slot) && held.Item.ItemIndex == definition.Id)) return EquipRefusal.SameAccessory;
        if (definition.Type == AccessoryType ? !IsAccessorySlot(slot) : slot != SlotOfType(definition.Type)) return EquipRefusal.WrongSlot;
        if (RequiredAbility(definition) is { } ability && !holdsAbility(ability)) return EquipRefusal.MissingWeaponAbility;
        if (definition.Type == ShieldType
            && worn.Any(held => Array.IndexOf(WeaponAndAccessorySlots, held.Slot) >= 0 && held.Definition.SubType == GunSubType))
        {
            return EquipRefusal.ShieldWithGun;
        }
        return null;
    }

    public static int? RequiredAbility(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.SubType == GunSubType) return GunCarrierAbility;
        if (definition.Type == WeaponType && definition.SubType == HammerSubType) return HammerAbility;
        return null;
    }

    public static IReadOnlyList<WornItem>? ReleasesBeforeEquip(ItemParamEntry definition, int slot, IReadOnlyList<WornItem> worn)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(worn);
        var releases = new List<WornItem>();
        if (worn.FirstOrDefault(held => held.Slot == slot) is { } occupant)
        {
            if (SlotFor(occupant.Definition, candidate => worn.Any(held => held.Slot == candidate)) != slot) return null;
            releases.Add(occupant);
        }
        if (definition.SubType == GunSubType
            && worn.FirstOrDefault(held => held.Slot == EquipmentSlot.Common) is { } thrown
            && thrown.Definition.Type != BulletType
            && !releases.Contains(thrown))
        {
            releases.Add(thrown);
        }
        if (definition.SubType == GunSubType
            && worn.FirstOrDefault(held => held.Slot == EquipmentSlot.Shield) is { } shield
            && !releases.Contains(shield))
        {
            releases.Add(shield);
        }
        return releases;
    }

    public static bool IsCommonSlotItem(ItemParamEntry definition) =>
        definition.Type == BulletType
        || definition.Type == ThrowItemType
        || (definition.Type == CardType && definition.SubType == ThrowCardSubType);

    public static EquipRefusal? CheckEquipCommon(
        ItemParamEntry definition,
        Item item,
        IReadOnlyList<WornItem> worn,
        int level,
        int characterType,
        IReadOnlyList<int> statTotals)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(worn);
        ArgumentNullException.ThrowIfNull(statTotals);

        if ((definition.ExtendedOptions & GmOnlyExtendedOption) != 0) return EquipRefusal.GmOnly;
        if ((definition.Options & EquipableOption) == 0
            || (item.RefineState == BrokenRefineState && (definition.Options & BrokenEquipableOption) == 0))
        {
            return EquipRefusal.NotEquipable;
        }
        if (RestrictsCharacterType(definition) && !FitsCharacterType(CharacterTypeMask(definition), characterType))
        {
            return EquipRefusal.WrongCharacterType;
        }
        if (level < definition.MinLevel) return EquipRefusal.LevelTooLow;
        if (definition.MinStatLevel != 0
            && (definition.MinStatType < 0 || definition.MinStatType >= statTotals.Count || statTotals[definition.MinStatType] < definition.MinStatLevel))
        {
            return EquipRefusal.StatTooLow;
        }
        var loaded = worn.FirstOrDefault(held => held.Slot == EquipmentSlot.Common);
        switch (definition.Type)
        {
            case BulletType:
                if (GunOf(worn) is null) return EquipRefusal.BulletWithoutGun;
                if (loaded is not null && loaded.Definition.Type == BulletType && loaded.Item.ItemIndex == definition.Id)
                {
                    return EquipRefusal.SameBulletLoaded;
                }
                return null;
            case ThrowItemType:
            case CardType:
                if (loaded is not null && loaded.Definition.Type != BulletType && loaded.Item.ItemUid == item.ItemUid)
                {
                    return EquipRefusal.AlreadyEquipped;
                }
                return null;
            default:
                return EquipRefusal.NotCommonItem;
        }
    }

    public static IReadOnlyList<WornItem>? ReleasesBeforeEquipCommon(ItemParamEntry definition, IReadOnlyList<WornItem> worn)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(worn);
        if (definition.Type == CardType && definition.SubType != ThrowCardSubType) return null;
        var releases = new List<WornItem>();
        if (definition.Type != BulletType && GunOf(worn) is { } gun)
        {
            if (!worn.Any(held => held.Slot == EquipmentSlot.Weapon)) return null;
            releases.Add(gun);
        }
        if (worn.FirstOrDefault(held => held.Slot == EquipmentSlot.Common) is { } loaded) releases.Add(loaded);
        return releases;
    }

    public static ReleaseRefusal? CheckReleaseCommon(ItemParamEntry definition, Item item, int slot, IReadOnlyList<WornItem> worn)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(worn);
        if (slot != EquipmentSlot.Common || !IsCommonSlotItem(definition)) return ReleaseRefusal.NotLoaded;
        return worn.FirstOrDefault(held => held.Slot == EquipmentSlot.Common) is { } loaded && loaded.Item.ItemUid == item.ItemUid
            ? null
            : ReleaseRefusal.NotLoaded;
    }

    private static WornItem? GunOf(IReadOnlyList<WornItem> worn) =>
        worn.FirstOrDefault(held => Array.IndexOf(WeaponAndAccessorySlots, held.Slot) >= 0 && held.Definition.SubType == GunSubType);

    public static ReleaseRefusal? CheckRelease(WornItem target, int slot, IReadOnlyList<WornItem> worn, int level)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(worn);
        if ((target.Definition.Type == EarsType || target.Definition.Type == TailType) && level < EarsAndTailReleaseLevel)
        {
            return ReleaseRefusal.EarsOrTailBelowLevel;
        }
        if (worn.FirstOrDefault(held => held.Slot == slot) is { } occupant && occupant.Item.ItemUid != target.Item.ItemUid)
        {
            return ReleaseRefusal.SlotHoldsAnotherItem;
        }
        return null;
    }

    public static EffectiveStats WithEquipment(EffectiveStats stats, IEnumerable<ItemParamEntry> worn) =>
        WithEquipment(stats, worn, new int[ItemStatCount]);

    public static EffectiveStats WithEquipment(EffectiveStats stats, IEnumerable<ItemParamEntry> worn, IReadOnlyList<int> extra)
    {
        ArgumentNullException.ThrowIfNull(worn);
        ArgumentNullException.ThrowIfNull(extra);
        var bonus = new int[ItemStatCount];
        for (var stat = 0; stat < ItemStatCount && stat < extra.Count; stat++) bonus[stat] = extra[stat];
        foreach (var definition in worn)
        {
            for (var stat = 0; stat < ItemStatCount && stat < definition.StatBonuses.Count; stat++) bonus[stat] += definition.StatBonuses[stat];
        }
        return new EffectiveStats(
            stats.Ap + bonus[0], stats.Ac + bonus[1], stats.Dx + bonus[2],
            stats.Mp + bonus[3], stats.Ma + bonus[4], stats.Md + bonus[5],
            stats.Wt + bonus[6], stats.Da + bonus[7], stats.Lk + bonus[8],
            stats.Hp + bonus[9], stats.Dp + bonus[10], stats.Hv + bonus[11]);
    }

    public static IReadOnlyList<int> Totals(EffectiveStats stats) => new[]
    {
        stats.Ap, stats.Ac, stats.Dx, stats.Mp, stats.Ma, stats.Md,
        stats.Wt, stats.Da, stats.Lk, stats.Hp, stats.Dp, stats.Hv,
    };
}
