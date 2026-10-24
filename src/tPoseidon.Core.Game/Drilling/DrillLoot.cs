using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public sealed class DrillLoot
{
    public const int SpecialRateType = 0;
    public const int EquipRateType = 1;
    public const int NormalRateType = 2;
    public const int QuestRateType = 3;

    private readonly int[] _weights;
    private readonly IReadOnlyList<MapItemChance>[] _lists;
    private readonly float _questBoost;

    public DrillLoot(MapItemZoneRow row, IReadOnlyDictionary<string, IReadOnlyList<MapItemChance>> tables, IReadOnlyList<float> eventRateAdd)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(eventRateAdd);
        _weights = new int[MapItemZoneRow.Categories];
        _lists = new IReadOnlyList<MapItemChance>[MapItemZoneRow.Categories];
        for (var category = 0; category < MapItemZoneRow.TableCategories; category++)
        {
            _lists[category] = Array.Empty<MapItemChance>();
            if (category >= row.Tables.Count || row.Tables[category] is not { } table || table.TableName.Length == 0) continue;
            var add = RateAdd(eventRateAdd, RateTypeOf(category));
            _weights[category] = (ushort)(int)Math.Truncate(table.Rate + table.Rate * add);
            _lists[category] = tables.TryGetValue(table.TableName, out var list) ? list : Array.Empty<MapItemChance>();
        }
        _lists[MapItemZoneRow.RowCategory] = row.Items;
        _weights[MapItemZoneRow.RowCategory] = row.Items.Sum(chance => chance.Rate);
        _questBoost = RateAdd(eventRateAdd, QuestRateType);
    }

    public int TotalWeight => _weights.Sum();

    public int WeightOf(int category) => _weights[category];

    public int Pop(Func<int, bool> questNeeds, Random rng)
    {
        ArgumentNullException.ThrowIfNull(questNeeds);
        ArgumentNullException.ThrowIfNull(rng);
        var total = TotalWeight;
        if (total <= 0) return 0;
        var roll = rng.Next(total);
        var category = -1;
        var low = 0;
        for (var c = 0; c < _weights.Length; c++)
        {
            if (roll >= low && roll < low + _weights[c])
            {
                category = c;
                break;
            }
            low += _weights[c];
        }
        if (category < 0) return 0;

        var rates = _lists[category].Select(chance => chance.Rate).ToArray();
        var sum = rates.Sum();
        if (category == MapItemZoneRow.RowCategory)
        {
            for (var i = 0; i < rates.Length; i++)
            {
                if (!questNeeds(_lists[category][i].ItemId)) continue;
                var boost = (int)Math.Truncate(rates[i] * _questBoost);
                sum += boost;
                rates[i] += boost;
            }
        }
        if (sum <= 0) return 0;
        var pick = rng.Next(sum);
        var floor = 0;
        for (var i = 0; i < rates.Length; i++)
        {
            if (pick >= floor && pick < floor + rates[i]) return _lists[category][i].ItemId;
            floor += rates[i];
        }
        return 0;
    }

    private static int RateTypeOf(int category) => category switch
    {
        MapItemZoneRow.SpecialCategory => SpecialRateType,
        MapItemZoneRow.EquipCategory => EquipRateType,
        _ => NormalRateType,
    };

    private static float RateAdd(IReadOnlyList<float> eventRateAdd, int type) => type < eventRateAdd.Count ? eventRateAdd[type] : 0;
}
