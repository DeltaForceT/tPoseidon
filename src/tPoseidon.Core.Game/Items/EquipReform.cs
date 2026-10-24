using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct ReformStatChoice(int Slot, int ElixirItemId)
{
    public const int None = -1;

    public bool IsChosen => Slot != None;
}

public readonly record struct EquipReformRequest(
    ulong TargetWireUid,
    ulong MaterialWireUid,
    ulong FirstElixirWireUid,
    ulong SecondElixirWireUid,
    ReformStatChoice First,
    ReformStatChoice Second)
{
    public IEnumerable<ReformStatChoice> Choices
    {
        get
        {
            yield return First;
            yield return Second;
        }
    }
}

public readonly record struct ReformCountResetRequest(ulong TargetWireUid, ulong MaterialWireUid);

public readonly record struct ReformRefineBonus(int PropertyId, short Value)
{
    public static ReformRefineBonus None { get; } = new(0, 0);
}

public sealed record EquipReformOutcome(IReadOnlyList<ItemProperty> Stored, IReadOnlyList<ItemProperty> Rolled, int Remaining, ReformRefineBonus Refine);

public static class StatReroll
{
    public const double PercentBase = 100.0;
    public const double ShiftUnit = 0.01;

    public static int[] Curve(IReadOnlyList<int> steps)
    {
        var table = ItemPropertyRules.Curve(steps);
        table[^1] = ItemPropertyRules.CurveBins - 1;
        return table;
    }

    public static (int Low, int High) Range(int baseValue, int minPercent, int maxPercent) =>
        (baseValue + (int)Math.Truncate(baseValue * minPercent / PercentBase),
         baseValue + (int)Math.Truncate(baseValue * maxPercent / PercentBase));

    public static int Shift(int value, int shift)
    {
        var product = value * (1 + shift * ShiftUnit);
        var whole = Math.Truncate(product);
        return (int)(whole == product ? whole : whole + 1.0);
    }

    public static int Roll(int low, int high, int bin)
    {
        var value = low + (int)Math.Truncate((high - low) / PercentBase * bin);
        return value < low ? low : value;
    }
}

public static class EquipReformRules
{
    public const uint BadMaterial = 5;
    public const uint Failed = 0x0D;
    public const uint NotEnoughMoney = 0x0E;
    public const uint AtMaximum = 0x13;
    public const uint NotReformable = 0x14;
    public const uint ElixirShort = 0x17;
    public const uint ElixirMismatch = 0x18;

    public const int RemainingId = 27000;
    public const int NotReformOption = 0x1000000;
    public const int GunApSlot = ItemPropertyRules.BaseStatCount;
    public const int ResetMaterialEffect = 0x26;
    public const double FeeMarkup = 0.75;
    public const int FeePerLevel = 100;

    public const string WeaponCurve = "reform_wp";
    public const string HatCurve = "reform_ht";
    public const string ArmourCurve = "reform_ac";
    public const string ShieldCurve = "reform_sd";
    public const string HeadCurve = "reform_hd";
    public const string FaceCurve = "reform_fc";
    public const string CapeCurve = "reform_cp";
    public const string ClothCurve = "reform_cl";

    public static string CurveFor(int itemType) => itemType switch
    {
        2 => WeaponCurve,
        3 => HatCurve,
        7 => ArmourCurve,
        8 => ShieldCurve,
        0x18 => HeadCurve,
        0x19 => FaceCurve,
        0x22 => CapeCurve,
        _ => ClothCurve,
    };

    public static int ElixirsNeeded(EquipReformRequest request) => request.SecondElixirWireUid != 0 ? 2 : 1;

    public static long Fee(ItemParamEntry definition, float discount)
    {
        ArgumentNullException.ThrowIfNull(definition);
        long fee = definition.Value + (long)Math.Truncate(definition.Value * FeeMarkup);
        if (fee <= 0) fee = (long)definition.MinLevel * FeePerLevel;
        return (long)Math.Truncate(fee * (1.0 - discount));
    }

    public static int? StoredRemaining(IReadOnlyList<ItemProperty> stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        foreach (var property in stored)
        {
            if (property.Type == ItemPropertyTypes.Stat && property.Id == RemainingId) return property.Value;
        }
        return null;
    }

    public static int RemainingAfter(ItemParamEntry definition, IReadOnlyList<ItemProperty> stored)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return (StoredRemaining(stored) ?? definition.ReformCount) - 1;
    }

    public static bool IsNotReformable(ItemParamEntry definition, IReadOnlyList<ItemProperty> stored) =>
        (ItemComposingRules.Options(definition, stored) & NotReformOption) != 0;

    public static uint CheckMaterial(ItemParamEntry target, IReadOnlyList<ItemProperty> stored, ItemReformEntry? row, int materialItemId, int materialStack)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (row is null || row.ItemId != materialItemId || materialStack < row.ItemCount) return Failed;
        if (IsNotReformable(target, stored)) return Failed;
        return RemainingAfter(target, stored) < 0 ? Failed : 0;
    }

    public static uint CheckReset(ItemParamEntry target, IReadOnlyList<ItemProperty> stored, ItemParamEntry material)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(material);
        var current = StoredRemaining(stored) ?? target.ReformCount;
        if (current < 0 || current == target.ReformCount) return AtMaximum;
        if (material.Effect != ResetMaterialEffect) return BadMaterial;
        return IsNotReformable(target, stored) ? NotReformable : 0;
    }

    public static IReadOnlyList<ItemProperty> Reset(ItemParamEntry target, IReadOnlyList<ItemProperty> stored)
    {
        ArgumentNullException.ThrowIfNull(target);
        return WithStat(stored, RemainingId, (short)target.ReformCount);
    }

    public static EquipReformOutcome Reform(
        ItemParamEntry definition,
        IReadOnlyList<ItemProperty> stored,
        bool hasGunAp,
        byte refineLevel,
        EquipReformRequest request,
        EquipReformTables tables,
        RefineTables refineTables,
        Func<int> curveRoll)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(refineTables);
        ArgumentNullException.ThrowIfNull(curveRoll);
        var curve = tables.Curves.TryGetValue(CurveFor(definition.Type), out var steps) ? StatReroll.Curve(steps) : null;
        var rolled = new List<ItemProperty>(ItemPropertyRules.BaseStatCount + 1);
        for (var slot = 0; slot <= GunApSlot; slot++)
        {
            var baseValue = slot < GunApSlot
                ? slot < definition.StatBonuses.Count ? definition.StatBonuses[slot] : 0
                : hasGunAp ? definition.Ap : 0;
            if (baseValue == 0) continue;
            var (low, high) = StatReroll.Range(baseValue, tables.RandomStatMinPercent, tables.RandomStatMaxPercent);
            if (ElixirFor(request, slot, tables) is { } elixir)
            {
                low = StatReroll.Shift(low, elixir.MinShift);
                high = StatReroll.Shift(high, elixir.MaxShift);
            }
            var bin = curve is null ? 0 : curve[curveRoll()];
            rolled.Add(new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.FirstStatId + slot, (short)StatReroll.Roll(low, high, bin), 0));
        }
        var remaining = RemainingAfter(definition, stored);
        var result = stored.ToList();
        foreach (var property in rolled) result = WithStat(result, property.Id, property.Value);
        result = WithStat(result, RemainingId, (short)remaining);
        var refine = RefineBonus(definition, refineLevel, rolled, refineTables);
        if (refine.PropertyId != 0)
        {
            result.RemoveAll(property => property.Type == ItemPropertyTypes.RefineBonus && property.Id == refine.PropertyId);
            result.Add(new ItemProperty(ItemPropertyTypes.RefineBonus, refine.PropertyId, refine.Value, 0));
        }
        return new EquipReformOutcome(result, rolled, remaining, refine);
    }

    public static ReformRefineBonus RefineBonus(ItemParamEntry definition, byte refineLevel, IReadOnlyList<ItemProperty> rolled, RefineTables refineTables)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(rolled);
        ArgumentNullException.ThrowIfNull(refineTables);
        if (refineLevel == 0 || RefineRules.PropertyIdFor(definition.RefineType) is not { } propertyId) return ReformRefineBonus.None;
        if (!refineTables.LevelRows.TryGetValue(definition.RefineIndex, out var row) || refineLevel > row.Accumulation.Count) return ReformRefineBonus.None;
        foreach (var property in rolled)
        {
            if (property.Id == propertyId) return new ReformRefineBonus(propertyId, RefineRules.Bonus(property.Value, row.Accumulation[refineLevel - 1]));
        }
        return ReformRefineBonus.None;
    }

    private static ReformElixirEntry? ElixirFor(EquipReformRequest request, int slot, EquipReformTables tables)
    {
        foreach (var choice in request.Choices)
        {
            if (choice.IsChosen && choice.Slot == slot && tables.Elixirs.TryGetValue(choice.ElixirItemId, out var elixir)) return elixir;
        }
        return null;
    }

    private static List<ItemProperty> WithStat(IReadOnlyList<ItemProperty> stored, int id, short value)
    {
        var list = stored.ToList();
        var at = list.FindIndex(property => property.Type == ItemPropertyTypes.Stat && property.Id == id);
        var property = new ItemProperty(ItemPropertyTypes.Stat, id, value, 0);
        if (at >= 0) list[at] = property;
        else list.Add(property);
        return list;
    }
}
