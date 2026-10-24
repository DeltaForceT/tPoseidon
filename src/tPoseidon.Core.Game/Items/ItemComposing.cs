using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct ComposingRequest(byte Kind, ulong BaseWireUid, ulong MaterialWireUid, ulong SupportWireUid);

public sealed record ComposingCandidate(
    ItemParamEntry? Base,
    IReadOnlyList<ItemProperty> BaseProperties,
    ItemParamEntry? Material,
    IReadOnlyList<ItemProperty> MaterialProperties,
    bool SupportGiven,
    ItemParamEntry? Support,
    byte Kind = ItemComposingRules.EquipmentKind);

public sealed record PetComposingPresent(int ItemId, int MinLevel, int MaxLevel, bool CashPet)
{
    public const string TableName = "PetComposingPresentInfo";
}

public readonly record struct ComposedLook(int AppearanceItemId, int Options, bool Inserts);

public static class ItemComposingRules
{
    public const byte EquipmentKind = 0;
    public const byte PetKind = 1;

    public const byte Ok = 0;
    public const byte NotInBag = 2;
    public const byte NotComposable = 6;
    public const byte DifferentKind = 7;
    public const byte AlreadyRequesting = 9;
    public const byte CatalystMissing = 0x0E;
    public const byte BadCatalyst = 0x0F;
    public const byte PetLevelLow = 19;

    public const int AppearanceType = 12;
    public const int AppearanceId = 26000;
    public const int OptionsType = ItemPropertyTypes.Options;
    public const int OptionsId = 26001;

    public const int ComposableOption = 0x1;
    public const int ComposedOption = 0x1000;
    public const int NoCatalystOption = 0x4000000;
    public static readonly int[] BlockingOptions = [0x100000, 0x200000, 0x800000];

    public const int WeaponType = 2;
    public const int HatType = 3;
    public const int ShieldType = 8;
    public const int HeadType = 0x18;
    public const int FaceType = 0x19;
    public const int CapeType = 0x22;
    public static readonly int[] EquipmentTypes = [WeaponType, HatType, ShieldType, HeadType, FaceType, CapeType];

    public const int AnySubType = 0x4E;
    public const int EquipmentCatalystEffect = 36;
    public const int PetType = 0x0D;
    public const int PetCatalystEffect = 40;
    public const int CashPetOption = 0x40000;

    public static int Options(ItemParamEntry definition, IReadOnlyList<ItemProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(properties);
        foreach (var property in properties)
        {
            if (property.Type == OptionsType && property.Id == OptionsId) return unchecked((int)property.Extra);
        }
        return definition.Options;
    }

    public static bool IsComposableEquipment(ItemParamEntry definition, IReadOnlyList<ItemProperty> properties)
    {
        var options = Options(definition, properties);
        if ((options & ComposableOption) == 0) return false;
        if (BlockingOptions.Any(bit => (options & bit) != 0)) return false;
        return EquipmentTypes.Contains(definition.Type);
    }

    public static bool IsComposablePet(ItemParamEntry definition, IReadOnlyList<ItemProperty> properties)
    {
        var options = Options(definition, properties);
        if ((options & ComposableOption) == 0) return false;
        if (BlockingOptions.Any(bit => (options & bit) != 0)) return false;
        return definition.Type == PetType;
    }

    public static int? PetPresent(IReadOnlyList<PetComposingPresent> presents, int petLevel, bool cashPet)
    {
        ArgumentNullException.ThrowIfNull(presents);
        return presents.FirstOrDefault(row => row.MinLevel <= petLevel && petLevel < row.MaxLevel && row.CashPet == cashPet)?.ItemId;
    }

    public static bool NeedsCatalyst(ItemParamEntry baseDefinition, IReadOnlyList<ItemProperty> baseProperties) =>
        (Options(baseDefinition, baseProperties) & NoCatalystOption) == 0;

    public static byte Check(ComposingCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Base is not { } baseDefinition || candidate.Material is not { } material) return NotInBag;
        var needsCatalyst = NeedsCatalyst(baseDefinition, candidate.BaseProperties);
        if (needsCatalyst && candidate.SupportGiven && candidate.Support is null) return NotInBag;
        var pets = candidate.Kind == PetKind;
        Func<ItemParamEntry, IReadOnlyList<ItemProperty>, bool> composable = pets ? IsComposablePet : IsComposableEquipment;
        if (!composable(baseDefinition, candidate.BaseProperties) || !composable(material, candidate.MaterialProperties)) return NotComposable;
        if (baseDefinition.Type != material.Type) return DifferentKind;
        if (baseDefinition.SubType != material.SubType && baseDefinition.SubType != AnySubType && material.SubType != AnySubType) return DifferentKind;
        if (pets && material.MinLevel < baseDefinition.MinLevel) return PetLevelLow;
        if (!needsCatalyst) return Ok;
        if (candidate.Support is not { } support) return CatalystMissing;
        return support.Effect == (pets ? PetCatalystEffect : EquipmentCatalystEffect) ? Ok : BadCatalyst;
    }

    public static ComposedLook Look(ItemParamEntry baseDefinition, IReadOnlyList<ItemProperty> baseProperties, int materialItemId, IReadOnlyList<ItemProperty> materialProperties)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        ArgumentNullException.ThrowIfNull(materialProperties);
        var passedOn = materialProperties.Where(property => property.Type == AppearanceType).Select(property => (int?)unchecked((int)property.Extra)).FirstOrDefault();
        var inserts = !baseProperties.Any(property => property.Type == AppearanceType);
        return new ComposedLook(passedOn ?? materialItemId, Options(baseDefinition, baseProperties) | ComposedOption, inserts);
    }

    public static IReadOnlyList<ItemProperty> Apply(IReadOnlyList<ItemProperty> baseProperties, ComposedLook look)
    {
        ArgumentNullException.ThrowIfNull(baseProperties);
        var kept = baseProperties.Where(property => property.Type != AppearanceType && property.Type != OptionsType).ToList();
        kept.Add(AppearanceProperty(look));
        kept.Add(OptionsProperty(look));
        return kept;
    }

    public static ItemProperty AppearanceProperty(ComposedLook look) => new(AppearanceType, AppearanceId, 0, unchecked((uint)look.AppearanceItemId));

    public static ItemProperty OptionsProperty(ComposedLook look) => new(OptionsType, OptionsId, 0, unchecked((uint)look.Options));
}

public readonly record struct DecomposeRequest(ulong ItemWireUid, ulong CatalystWireUid);

public static class DecomposeRules
{
    public const string DummyTable = "DecompoundDummyItem";
    public const byte NotInBag = 2;
    public const byte NotDecomposable = 4;
    public const byte BadCatalyst = 0x0F;
    public const byte BagFull = 0x12;
    public const int MaxProperties = 20;
    public const int OriginalLookOption = 0x800000;
    public const byte DummyMixSlot = 1;
    public const byte OriginalMixSlot = 0;
    public const int GunSubType = 54;
    public const int HammerSubType = 82;

    public static int? DummyRow(int type, int subType) => type switch
    {
        ItemComposingRules.WeaponType when subType == GunSubType => 1,
        ItemComposingRules.WeaponType when subType == HammerSubType => 7,
        ItemComposingRules.WeaponType => 0,
        ItemComposingRules.HatType => 2,
        ItemComposingRules.ShieldType => 3,
        ItemComposingRules.HeadType => 4,
        ItemComposingRules.FaceType => 5,
        ItemComposingRules.CapeType => 6,
        _ => null,
    };

    public static bool IsLookProperty(ItemProperty property) =>
        property.Type is ItemComposingRules.AppearanceType or ItemComposingRules.OptionsType;

    public static byte Check(IReadOnlyList<ItemProperty> properties, ItemParamEntry? catalyst)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (catalyst is null) return NotInBag;
        if (catalyst.Effect != ItemComposingRules.EquipmentCatalystEffect) return BadCatalyst;
        if (!properties.Any(property => property.Type == ItemComposingRules.AppearanceType) || properties.Count > MaxProperties) return NotDecomposable;
        return ItemComposingRules.Ok;
    }
}
