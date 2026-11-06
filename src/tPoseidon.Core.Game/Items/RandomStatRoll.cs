using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class RandomStatRoll
{
    private readonly Func<ItemPropertyRules> _rules;
    private readonly IItemPropertyStore _properties;
    private readonly IItemParamLookup _catalog;
    private readonly Random _rng = new();

    public RandomStatRoll(Func<ItemPropertyRules> rules, IItemPropertyStore properties, IItemParamLookup catalog)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<ItemProperty> Apply(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.ItemClass != ItemParamEntry.RareClass) return [];
        if (_catalog.GetById(item.ItemIndex) is not { HasRandomStats: true } definition) return [];
        var rules = _rules();
        List<ItemProperty> created;
        lock (_rng) created = rules.Create(definition, true, _rng);
        var kept = ItemPropertyRules.Persistable(created);
        _properties.Replace(item.ItemUid, kept);
        return rules.ForClient(definition, kept);
    }
}
