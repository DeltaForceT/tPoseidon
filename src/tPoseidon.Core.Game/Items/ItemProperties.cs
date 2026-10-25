using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class ItemPropertyRules
{
    public const int FirstStatId = 10000;
    public const int GunApId = 10012;
    public const int HpRecoveryId = 24000;
    public const int MpRecoveryId = 24001;
    public const int CurveBins = 101;
    public const int CurveRolls = 10000;
    public const int BaseStatCount = 12;

    private readonly ItemPropertyTables _tables;
    private readonly Dictionary<string, int[]> _curves = new(StringComparer.Ordinal);

    public ItemPropertyRules(ItemPropertyTables tables)
    {
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        foreach (var (name, steps) in tables.Curves) _curves[name] = Curve(steps);
    }

    public int MixSlots(int itemId, int current, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (!_tables.RandomStats.TryGetValue(itemId, out var row) || row.MixSlot is not { } range || !_curves.TryGetValue(range.Curve, out var curve)) return current;
        var low = Math.Max(range.Min, current);
        return low + (int)Math.Truncate((range.Max - low) / 100.0 * curve[rng.Next(CurveRolls)]);
    }

    public int StatSum(ItemParamEntry definition, IReadOnlyList<ItemProperty> stored, int propertyId)
    {
        var matching = Full(definition, stored).Where(property => property.Id == propertyId).ToList();
        return matching.Count == 0 ? ItemStatCheckEntry.NoRequirement : matching.Sum(property => (int)property.Value);
    }

    public int? CurveBin(string curveName, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return _curves.TryGetValue(curveName, out var curve) ? curve[rng.Next(CurveRolls)] : null;
    }

    public static bool HasAttribute(int index) => index is >= 0 and <= 7 or >= 10 and <= 21;

    public static int AttributeId(int index) => index switch
    {
        >= 0 and <= 7 => 20000 + index,
        10 => 20008,
        >= 11 and <= 18 => 21000 + index - 11,
        19 => 21009,
        20 => 21008,
        21 => 21010,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public List<ItemProperty> Defaults(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var list = new List<ItemProperty>();
        for (var i = 0; i < BaseStatCount && i < definition.StatBonuses.Count; i++)
        {
            if (definition.StatBonuses[i] != 0) list.Add(new ItemProperty(ItemPropertyTypes.Stat, FirstStatId + i, (short)definition.StatBonuses[i], 0));
        }
        if (definition.SubType == ItemParamEntry.GunSubType) list.Add(new ItemProperty(ItemPropertyTypes.Stat, GunApId, (short)definition.Ap, 0));
        if (definition.HpRecoveryRate != 0) list.Add(new ItemProperty(ItemPropertyTypes.Stat, HpRecoveryId, (short)Math.Truncate(definition.HpRecoveryRate), 0));
        if (definition.MpRecoveryRate != 0) list.Add(new ItemProperty(ItemPropertyTypes.Stat, MpRecoveryId, (short)Math.Truncate(definition.MpRecoveryRate), 0));
        if (_tables.Attributes.TryGetValue(definition.Id, out var attributes))
        {
            foreach (var (index, value) in attributes) list.Add(new ItemProperty(ItemPropertyTypes.Stat, AttributeId(index), (short)value, 0));
        }
        return list;
    }

    public List<ItemProperty> Create(ItemParamEntry definition, bool rollRandom, Random rng)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(rng);
        var list = Defaults(definition);
        if (!rollRandom || !definition.HasRandomStats || !_tables.RandomStats.TryGetValue(definition.Id, out var row)) return list;
        for (var j = 0; j < row.Stats.Count; j++)
        {
            if (row.Stats[j] is not { } range || !_curves.TryGetValue(range.Curve, out var curve)) continue;
            var value = (short)Values(range.Min, range.Max)[curve[rng.Next(CurveRolls)]];
            var id = FirstStatId + j;
            var at = list.FindIndex(property => property.Type == ItemPropertyTypes.Stat && property.Id == id);
            if (at >= 0) list[at] = list[at] with { Value = value };
            else list.Add(new ItemProperty(ItemPropertyTypes.Stat, id, value, 0));
        }
        return list;
    }

    public List<ItemProperty> Evolved(ItemParamEntry result, IReadOnlyList<ItemProperty> previous, Random rng)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(rng);
        var list = new List<ItemProperty>();
        if (result.HasRandomStats && _tables.RandomStats.TryGetValue(result.Id, out var row))
        {
            for (var j = 0; j < row.Stats.Count; j++)
            {
                if (row.Stats[j] is not { } range || !_curves.TryGetValue(range.Curve, out var curve)) continue;
                var id = FirstStatId + j;
                var low = range.Min;
                foreach (var held in previous)
                {
                    if (held.Type == ItemPropertyTypes.Stat && held.Id == id && held.Value > low) low = held.Value;
                }
                list.Add(new ItemProperty(ItemPropertyTypes.Stat, id, (short)Values(low, range.Max)[curve[rng.Next(CurveRolls)]], 0));
            }
        }
        foreach (var fallback in Defaults(result))
        {
            if (!list.Any(property => property.Type == fallback.Type && property.Id == fallback.Id)) list.Add(fallback);
        }
        return list;
    }

    public List<ItemProperty> Full(ItemParamEntry definition, IReadOnlyList<ItemProperty> stored)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(stored);
        var list = Defaults(definition);
        foreach (var property in stored)
        {
            var at = property.Type == ItemPropertyTypes.Stat ? list.FindIndex(p => p.Type == ItemPropertyTypes.Stat && p.Id == property.Id) : -1;
            if (at >= 0) list[at] = property;
            else list.Add(property);
        }
        return list.Take(ItemPropertyTypes.MaxPerItem).ToList();
    }

    public static List<ItemProperty> ForClient(IEnumerable<ItemProperty> properties, ItemParamEntry? definition)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return properties
            .Where(property => !(property.Type == ItemPropertyTypes.Stat
                && property.Id >= FirstStatId
                && property.Id < FirstStatId + BaseStatCount
                && definition is not null
                && property.Id - FirstStatId < definition.StatBonuses.Count
                && property.Value == definition.StatBonuses[property.Id - FirstStatId]))
            .ToList();
    }

    public static int[] Values(int min, int max)
    {
        var values = new int[CurveBins];
        var step = (float)((max - min) / 100.0);
        var current = (float)min;
        for (var i = 0; i < CurveBins; i++)
        {
            values[i] = (int)Math.Truncate(current);
            current += step;
        }
        return values;
    }

    public static int[] Curve(IReadOnlyList<int> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var weights = new int[CurveBins];
        var anchors = new List<(int Bin, int Weight)>();
        for (var k = 0; k < steps.Count; k++)
        {
            if (steps[k] != 0) anchors.Add((k * 10, steps[k]));
        }
        for (var a = 0; a < anchors.Count; a++)
        {
            weights[anchors[a].Bin] = anchors[a].Weight;
            if (a == 0) continue;
            var (fromBin, fromWeight) = anchors[a - 1];
            var (toBin, toWeight) = anchors[a];
            for (var bin = fromBin + 1; bin < toBin; bin++)
            {
                weights[bin] = (int)Math.Truncate(fromWeight + (toWeight - fromWeight) * (double)(bin - fromBin) / (toBin - fromBin));
            }
        }
        var sum = weights.Sum();
        var table = new int[CurveRolls];
        Array.Fill(table, CurveBins - 1);
        if (sum <= 0) return table;
        var filled = 0;
        var carry = 0f;
        for (var bin = 0; bin < CurveBins && filled < CurveRolls; bin++)
        {
            var exact = weights[bin] * (float)CurveRolls / sum + carry;
            var count = (int)Math.Truncate(exact);
            carry = exact - count;
            for (var i = 0; i < count && filled < CurveRolls; i++) table[filled++] = bin;
        }
        return table;
    }
}
