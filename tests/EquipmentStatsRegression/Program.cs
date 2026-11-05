using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

var checks = 0;
void Check(IReadOnlyList<int> expected, EffectiveStats actual, string scenario)
{
    var totals = EquipmentRules.Totals(actual);
    if (!expected.SequenceEqual(totals))
        throw new InvalidOperationException($"{scenario}: expected [{string.Join(",", expected)}], got [{string.Join(",", totals)}]");
    checks++;
}

var rules = new ItemPropertyRules(new ItemPropertyTables());
var properties = new PropertyStore();
var items = new InMemoryItemStore();
var defaults = Enumerable.Range(1, 12).Select(i => i * 10).ToArray();
var definition = new ItemParamEntry { Id = 100, Class = 2, StatBonuses = defaults };
var empty = new ItemParamEntry { Id = 101, Class = 2 };
var catalog = new InMemoryItemParamLookup(new[] { definition, empty });
var stats = new EffectiveStats(10, 20, 30, 40, 50, 60, 70, 80, 90, 780, 110, 120);
var baseline = EquipmentRules.Totals(stats);
var extra = Enumerable.Range(1, 12).ToArray();
EffectiveStats Calculate() => EquipmentRules.WithEquipment(stats,
    EquipmentRules.WornBy(items, catalog, 1), properties, rules, extra);
int[] Expected(params int[][] bonuses) => Enumerable.Range(0, 12)
    .Select(i => baseline[i] + extra[i] + bonuses.Sum(b => b[i])).ToArray();

Check(Expected(), Calculate(), "no equipment; set/fortune/shadow extras preserved");
var first = items.GrantToCharacter(1, 100, 2, SlotKind.Inventory, 0);
properties.Replace(first.ItemUid, new[] { new ItemProperty(0, 10009, 4836, 0) });
Check(Expected(), Calculate(), "inventory options do not contribute");
items.MoveToSlot(first.ItemUid, SlotKind.Equipment, EquipmentSlot.Hat);
properties.Delete(first.ItemUid);
Check(Expected(defaults), Calculate(), "definition fallback without stored options");
properties.Replace(first.ItemUid, rules.Defaults(definition));
Check(Expected(defaults), Calculate(), "stored defaults counted once");

// Exercise all twelve indices, every mix slot and a stored random base below/at/above default.
for (var stat = 0; stat < 12; stat++)
{
    foreach (var delta in new[] { -5, 0, 5 })
    {
        for (var slot = 0; slot <= ItemPropertyTypes.LastMixSlot; slot++)
        {
            properties.Replace(first.ItemUid, new[]
            {
                new ItemProperty(ItemPropertyTypes.Stat, 10000 + stat, (short)(defaults[stat] + delta), 0),
                new ItemProperty(slot, 10000 + stat, 7, 0),
                new ItemProperty(ItemPropertyTypes.RefineBonus, 10000 + stat, 11, 0),
                new ItemProperty(ItemPropertyTypes.Options, 10000 + stat, 999, 0),
                new ItemProperty(-1, 10000 + stat, 999, 0),
                new ItemProperty(0, ItemPropertyRules.GunApId, 999, 0),
                new ItemProperty(0, ItemPropertyRules.HpRecoveryId, 999, 0),
            });
            var expected = (int[])defaults.Clone();
            expected[stat] += delta + 7 + 11;
            Check(Expected(expected), Calculate(), $"stat {stat}, base delta {delta}, mix slot {slot}, refine and unrelated properties");
        }
    }
}

// Synthetic reproduction from the supplied patch: 3042 -> 8122 before unrelated extras.
var hpDefaults = new int[12];
hpDefaults[9] = 2262;
hpDefaults[3] = 100;
var hpDefinition = definition with { StatBonuses = hpDefaults };
properties.Replace(first.ItemUid, new[]
{
    new ItemProperty(10, 10009, 2506, 0),
    new ItemProperty(0, 10009, 4836, 0),
    new ItemProperty(10, 10003, 500, 0),
});
var reproduction = EquipmentRules.WithEquipment(stats,
    new[] { new WornItem(EquipmentSlot.Hat, first, hpDefinition) }, properties, rules, new int[12]);
var reproductionExpected = baseline.ToArray();
reproductionExpected[9] = 8122;
reproductionExpected[3] += 500;
Check(reproductionExpected, reproduction, "synthetic HP and MP reproduction");

// Multiple slots, option-only equipment, and repeated equip/release recalculate from current state.
var contribution = new int[12];
contribution[9] = 13;
contribution[3] = 17;
properties.Replace(first.ItemUid, new[] { new ItemProperty(0, 10009, 13, 0), new ItemProperty(9, 10003, 17, 0) });
var second = items.GrantToCharacter(1, 100, 2, SlotKind.Equipment, EquipmentSlot.Shield);
properties.Replace(second.ItemUid, rules.Defaults(definition));
var third = items.GrantToCharacter(1, 101, 2, SlotKind.Equipment, EquipmentSlot.Accessory);
properties.Replace(third.ItemUid, new[] { new ItemProperty(11, 10009, 13, 0), new ItemProperty(0, 10003, 17, 0) });
items.GrantToCharacter(2, 100, 2, SlotKind.Equipment, EquipmentSlot.Hat);
for (var cycle = 0; cycle < 3; cycle++)
{
    Check(Expected(defaults, defaults, contribution, contribution), Calculate(), "multiple equipment and option-only definition");
    Check(Expected(defaults, defaults, contribution, contribution), Calculate(), "repeat calculation has no accumulated bonus");
    if (!extra.SequenceEqual(Enumerable.Range(1, 12))) throw new InvalidOperationException("extra was mutated");
    items.MoveToSlot(first.ItemUid, SlotKind.Inventory, 0);
    Check(Expected(defaults, contribution), Calculate(), "release removes base and options");
    items.MoveToSlot(first.ItemUid, SlotKind.Equipment, EquipmentSlot.Hat);
}
items.MoveToSlot(first.ItemUid, SlotKind.Inventory, 0);
items.MoveToSlot(second.ItemUid, SlotKind.Inventory, 1);
items.MoveToSlot(third.ItemUid, SlotKind.Inventory, 2);
Check(Expected(), Calculate(), "all released; other character excluded");
Console.WriteLine($"PASS: {checks} synthetic equipment-stat scenarios. No database or client used.");

sealed class PropertyStore : IItemPropertyStore
{
    private readonly Dictionary<ulong, IReadOnlyList<ItemProperty>> _items = new();
    public IReadOnlyList<ItemProperty> ForItem(ulong itemUid) => _items.GetValueOrDefault(itemUid, Array.Empty<ItemProperty>());
    public void Replace(ulong itemUid, IReadOnlyList<ItemProperty> properties) => _items[itemUid] = properties;
    public void Delete(ulong itemUid) => _items.Remove(itemUid);
}
