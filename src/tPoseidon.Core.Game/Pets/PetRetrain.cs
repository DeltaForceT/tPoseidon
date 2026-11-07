using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public readonly record struct PetRetrainRequest(ulong PetWireUid, ulong MaterialWireUid, ulong LockWireUid, int LockStatId);

public readonly record struct HeldItem(int ItemId, int Stack);

public sealed record PetRetrainOutcome(IReadOnlyList<ItemProperty> Stored, IReadOnlyList<ItemProperty> Rolled);

public static class PetRetrainRules
{
    public const ushort Ok = 0;
    public const ushort NotAPet = 1;
    public const ushort NotALockItem = 3;
    public const ushort NoPet = 4;
    public const ushort NoMaterial = 5;
    public const ushort NotEnoughMaterial = 6;
    public const ushort StoreFailed = 7;
    public const ushort StatNotLockable = 8;
    public const ushort LockItemMissing = 9;
    public const ushort LockStatMissing = 10;

    public static ushort Check(
        ItemParamEntry? pet,
        HeldItem? material,
        bool lockGiven,
        HeldItem? lockItem,
        int lockStatId,
        PetRetrainTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (pet is null) return NoPet;
        if (!PetRules.IsPet(pet)) return NotAPet;
        if (material is not { } held) return NoMaterial;
        if (!tables.Materials.TryGetValue(held.ItemId, out var row)) return StoreFailed;
        if (held.Stack < row.ItemCount) return NotEnoughMaterial;
        if (lockStatId != 0 && !lockGiven) return LockItemMissing;
        if (lockGiven && lockStatId == 0) return LockStatMissing;
        if (!lockGiven) return Ok;
        if (lockItem is not { } lockHeld) return LockItemMissing;
        if (!tables.LockItems.ContainsKey(lockHeld.ItemId)) return NotALockItem;
        if (lockHeld.Stack < row.LockItemCount) return NotEnoughMaterial;
        return IsRolledStat(pet, lockStatId) ? Ok : StatNotLockable;
    }

    public static bool IsRolledStat(ItemParamEntry pet, int propertyId)
    {
        ArgumentNullException.ThrowIfNull(pet);
        var slot = propertyId - ItemPropertyRules.FirstStatId;
        return slot >= 0 && slot < ItemPropertyRules.BaseStatCount && slot < pet.StatBonuses.Count && pet.StatBonuses[slot] != 0;
    }

    public static PetRetrainOutcome Retrain(
        ItemParamEntry pet,
        IReadOnlyList<ItemProperty> stored,
        IReadOnlyList<ItemProperty> current,
        PetRetrainMaterialEntry material,
        int lockStatId,
        PetRetrainTables tables,
        Func<int> curveRoll,
        Func<IReadOnlyList<ItemProperty>>? randomStats = null)
    {
        ArgumentNullException.ThrowIfNull(pet);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(curveRoll);
        if (pet.HasRandomStats && randomStats is not null) return RetrainRandomStats(stored, current, lockStatId, randomStats());
        var curve = tables.Curve.Count == 0 ? null : StatReroll.Curve(tables.Curve);
        var rolled = new List<ItemProperty>(ItemPropertyRules.BaseStatCount);
        for (var slot = 0; slot < ItemPropertyRules.BaseStatCount && slot < pet.StatBonuses.Count; slot++)
        {
            var baseValue = pet.StatBonuses[slot];
            if (baseValue == 0) continue;
            var id = ItemPropertyRules.FirstStatId + slot;
            short value;
            if (id == lockStatId)
            {
                value = current.Where(property => property.Type == ItemPropertyTypes.Stat && property.Id == id)
                    .Select(property => (short?)property.Value).FirstOrDefault() ?? (short)baseValue;
            }
            else
            {
                var low = baseValue + Offset(baseValue, material.RandomStatMinPercent);
                var high = baseValue + Offset(baseValue, material.RandomStatMaxPercent);
                value = (short)StatReroll.Roll(low, high, curve is null ? 0 : curve[curveRoll()]);
            }
            rolled.Add(new ItemProperty(ItemPropertyTypes.Stat, id, value, 0));
        }
        var result = stored.ToList();
        foreach (var property in rolled)
        {
            var at = result.FindIndex(held => held.Type == ItemPropertyTypes.Stat && held.Id == property.Id);
            if (at >= 0) result[at] = property;
            else result.Add(property);
        }
        return new PetRetrainOutcome(result, rolled);
    }

    private static int Offset(int baseValue, int percent) =>
        (int)Math.Truncate(baseValue * (double)(float)(percent / StatReroll.PercentBase));

    private static PetRetrainOutcome RetrainRandomStats(
        IReadOnlyList<ItemProperty> stored,
        IReadOnlyList<ItemProperty> current,
        int lockStatId,
        IReadOnlyList<ItemProperty> created)
    {
        var rolled = new List<ItemProperty>(created.Count);
        foreach (var property in created)
        {
            if (property.Type != ItemPropertyTypes.Stat) continue;
            if (property.Id == lockStatId)
            {
                var kept = current.Where(held => held.Type == ItemPropertyTypes.Stat && held.Id == property.Id).Select(held => (short?)held.Value).FirstOrDefault();
                rolled.Add(kept is { } value ? property with { Value = value } : property);
            }
            else rolled.Add(property);
        }
        var result = stored.ToList();
        foreach (var property in rolled)
        {
            var at = result.FindIndex(held => held.Type == ItemPropertyTypes.Stat && held.Id == property.Id);
            if (at >= 0) result[at] = property;
            else result.Add(property);
        }
        return new PetRetrainOutcome(result, rolled);
    }
}
