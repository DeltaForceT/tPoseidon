namespace tPoseidon.Core.LibConfig;

public sealed record PetRetrainMaterialEntry(int ItemId, int ItemCount, int LockItemCount, int RandomStatMinPercent, int RandomStatMaxPercent);

public sealed record PetRetrainTables(
    IReadOnlyDictionary<int, PetRetrainMaterialEntry> Materials,
    IReadOnlyDictionary<int, int> LockItems,
    IReadOnlyList<int> Curve)
{
    public const string MaterialTable = "PetRetrainMaterialInfoEx";
    public const string LockItemTable = "PetRetrainMaterialLockItem";
    public const string CurveName = "retrain_pet";

    public static PetRetrainTables Empty { get; } = new(
        new Dictionary<int, PetRetrainMaterialEntry>(),
        new Dictionary<int, int>(),
        Array.Empty<int>());
}
