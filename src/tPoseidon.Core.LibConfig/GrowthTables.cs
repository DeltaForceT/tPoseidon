namespace tPoseidon.Core.LibConfig;

public sealed record EquipPropertyRow(int ItemId, float ApplyRatio, IReadOnlyList<int> EnableProperties);

public sealed record MaturingCompoundRow(int ItemId, int EnableProperty, int MinAbility, int MaxAbility, int ItemNum, int EquipLevel, string Curve);

public sealed record MaturingHelpRow(int ItemId, float ApplyRatio, float MinAbility, float MaxAbility);

public sealed record GrowthTables
{
    public const int EnablePropertyColumns = 10;
    public const int ResetMaterialItemId = 19641;
    public const int CompoundMasterSkill = 3014;

    public IReadOnlyDictionary<int, EquipPropertyRow> EquipProperties { get; init; } = new Dictionary<int, EquipPropertyRow>();
    public IReadOnlyDictionary<int, MaturingCompoundRow> Materials { get; init; } = new Dictionary<int, MaturingCompoundRow>();
    public IReadOnlyDictionary<int, MaturingHelpRow> Aids { get; init; } = new Dictionary<int, MaturingHelpRow>();
    public float CostDiscount { get; init; }
    public IReadOnlyDictionary<int, float> CompoundMasterRatios { get; init; } = new Dictionary<int, float>();
}

public sealed record PetGrowthTables
{
    public const string PropertyTable = "Pet_Property";
    public const string MaterialTable = "Maturing_CompoundPet";
    public const string AidTable = "Maturing_PetCompound_Help";
    public const int ResetMaterialItemId = 0x4D0F;

    public IReadOnlyDictionary<int, EquipPropertyRow> PetProperties { get; init; } = new Dictionary<int, EquipPropertyRow>();
    public IReadOnlyDictionary<int, MaturingCompoundRow> Materials { get; init; } = new Dictionary<int, MaturingCompoundRow>();
    public IReadOnlyDictionary<int, MaturingHelpRow> Aids { get; init; } = new Dictionary<int, MaturingHelpRow>();
}
