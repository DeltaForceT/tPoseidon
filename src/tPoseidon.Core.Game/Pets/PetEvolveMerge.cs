using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public static class PetEvolveRules
{
    public const ushort Ok = 0;
    public const ushort NotEvolvable = 1;
    public const ushort PetMissing = 7;
    public const ushort WrongMaterial = 8;
    public const ushort NotEnoughMaterial = 9;
    public const ushort PetEquipped = 0xB;
    public const ushort Failed = 0xC;

    public static ushort Check(ItemParamEntry? pet, bool petEquipped, HeldItem? material, PetLevelUpTables tables, out PetLevelUpEntry? row)
    {
        ArgumentNullException.ThrowIfNull(tables);
        row = null;
        if (pet is null) return PetMissing;
        if (pet.Type != PetRules.PetItemType || pet.ObjectClass != ItemParamEntry.RareClass) return NotEvolvable;
        if (petEquipped) return PetEquipped;
        if (!tables.Rows.TryGetValue(pet.Id, out var found)) return Failed;
        if (!found.Evolve) return NotEvolvable;
        if (material is not { } held || held.ItemId != found.CatalystItemId) return WrongMaterial;
        if (found.CatalystCount <= 0 || held.Stack < found.CatalystCount) return NotEnoughMaterial;
        row = found;
        return Ok;
    }
}

public static class PetMergeRules
{
    public const ushort Ok = 0;
    public const ushort PetMissing = 1;
    public const ushort PetEquipped = 2;
    public const ushort NotPet = 3;
    public const ushort DifferentPets = 4;
    public const ushort LowGrade = 5;
    public const ushort NoGain = 6;
    public const ushort Failed = 7;

    public const int GradeDivisor = 10;
    public const int RandomGrade = 5;
    public const int BestGrade = 6;
    public const int LowerIsBetterStat = 2;
    public const int RandMax = 0x7FFF;

    public static int Grade(ItemParamEntry pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        return pet.RefineIndex / GradeDivisor;
    }

    public static ushort Check(ItemParamEntry? first, bool firstEquipped, ItemParamEntry? second, bool secondEquipped)
    {
        if (first is null) return PetMissing;
        if (firstEquipped) return PetEquipped;
        if (!IsPet(first)) return NotPet;
        if (second is null) return PetMissing;
        if (secondEquipped) return PetEquipped;
        if (!IsPet(second)) return NotPet;
        if (first.Id != second.Id) return DifferentPets;
        if (Grade(first) < RandomGrade) return LowGrade;
        return Ok;
    }

    public static int Gains(IReadOnlyList<ItemProperty> first, IReadOnlyList<ItemProperty> second, byte firstSlots, byte secondSlots)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var gains = 0;
        for (var i = 0; i < ItemPropertyRules.BaseStatCount; i++)
        {
            if (StatOf(first, i) is not { } a || StatOf(second, i) is not { } b) continue;
            if (i == LowerIsBetterStat ? b < a : a < b) gains++;
        }
        if (firstSlots < secondSlots) gains++;
        return gains;
    }

    public static List<ItemProperty> Merged(int grade, IReadOnlyList<ItemProperty> first, IReadOnlyList<ItemProperty> second, IReadOnlyList<ItemProperty> defaults, Func<int> rand)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(rand);
        var list = new List<ItemProperty>();
        for (var i = 0; i < ItemPropertyRules.BaseStatCount; i++)
        {
            if (StatOf(first, i) is not { } a || StatOf(second, i) is not { } b) continue;
            list.Add(new ItemProperty(ItemPropertyTypes.Stat, ItemPropertyRules.FirstStatId + i, MergedValue(grade, i, a, b, rand), 0));
        }
        foreach (var fallback in defaults)
        {
            if (!list.Any(property => property.Type == fallback.Type && property.Id == fallback.Id)) list.Add(fallback);
        }
        list.AddRange(LookOverrides(first));
        return list;
    }

    public static byte MergedSlots(int grade, byte first, byte second, Func<int> rand)
    {
        ArgumentNullException.ThrowIfNull(rand);
        if (first >= second) return first;
        return grade switch
        {
            RandomGrade => (byte)Between(first, second, rand),
            BestGrade => second,
            _ => first,
        };
    }

    public static IEnumerable<ItemProperty> LookOverrides(IReadOnlyList<ItemProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var look = properties.FirstOrDefault(property => property.Type == ItemComposingRules.AppearanceType);
        if (look.Type != ItemComposingRules.AppearanceType) yield break;
        yield return new ItemProperty(ItemComposingRules.AppearanceType, ItemComposingRules.AppearanceId, 0, look.Extra);
        var options = properties.FirstOrDefault(property => property.Type == ItemComposingRules.OptionsType);
        if (options.Type == ItemComposingRules.OptionsType) yield return new ItemProperty(ItemComposingRules.OptionsType, ItemComposingRules.OptionsId, 0, options.Extra);
    }

    private static short MergedValue(int grade, int index, short a, short b, Func<int> rand)
    {
        var better = index == LowerIsBetterStat ? b < a : a < b;
        if (!better) return a;
        if (grade == BestGrade) return b;
        if (grade != RandomGrade) return a;
        if (index != LowerIsBetterStat) return (short)Between(a, b, rand);
        var low = Math.Abs((int)a);
        var high = Math.Abs((int)b);
        return high == low ? a : (short)-(rand() % (high - low) + low);
    }

    private static int Between(int low, int high, Func<int> rand) => rand() % (high - low) + low;

    private static short? StatOf(IReadOnlyList<ItemProperty> properties, int index)
    {
        var id = ItemPropertyRules.FirstStatId + index;
        foreach (var property in properties)
        {
            if (property.Type == ItemPropertyTypes.Stat && property.Id == id) return property.Value;
        }
        return null;
    }

    private static bool IsPet(ItemParamEntry item) => item.Type == PetRules.PetItemType && item.ObjectClass == ItemParamEntry.RareClass;
}
