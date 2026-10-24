using System.Globalization;
using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Config;

namespace tPoseidon.DrillEgg.Prizes;

internal sealed record PrizePool(short Area, bool Deep, IReadOnlyList<GachaPrize> Prizes);

internal sealed class PrizeCatalog
{
    private readonly Dictionary<(short Area, bool Deep), IReadOnlyList<GachaPrize>> _pools;
    private readonly Dictionary<int, PrizeSettings> _items;

    private PrizeCatalog(Dictionary<(short, bool), IReadOnlyList<GachaPrize>> pools, Dictionary<int, PrizeSettings> items)
    {
        _pools = pools;
        _items = items;
    }

    public IEnumerable<PrizePool> Pools =>
        _pools.OrderBy(p => p.Key.Area).ThenBy(p => p.Key.Deep).Select(p => new PrizePool(p.Key.Area, p.Key.Deep, p.Value));

    public static PrizeCatalog From(DrillEggSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var pools = new Dictionary<(short, bool), IReadOnlyList<GachaPrize>>();
        var items = new Dictionary<int, PrizeSettings>();
        foreach (var prize in settings.Prizes) items.TryAdd(prize.Id, prize);
        for (var area = GachaRules.FirstArea; area <= GachaRules.LastArea; area++)
        {
            var list = settings.Areas.TryGetValue(area.ToString(CultureInfo.InvariantCulture), out var custom) && custom.Prizes.Count > 0
                ? custom.Prizes
                : settings.Prizes;
            foreach (var prize in list) items.TryAdd(prize.Id, prize);
            pools[(area, false)] = list.Select(p => ToPrize(p, p.Weight)).ToList();
            pools[(area, true)] = list.Select(p => ToPrize(p, checked((int)DeepWeightOf(p, settings.DeepDrill.RareBoost)))).ToList();
        }
        return new PrizeCatalog(pools, items);
    }

    public static long DeepWeightOf(PrizeSettings prize, int rareBoost)
    {
        ArgumentNullException.ThrowIfNull(prize);
        if (prize.DeepWeight is { } deep) return deep;
        return prize.Rare ? (long)prize.Weight * rareBoost : prize.Weight;
    }

    public IReadOnlyList<GachaPrize> Pool(short area, bool deep) =>
        _pools.TryGetValue((area, deep), out var pool) ? pool : [];

    public string NameOf(int itemId) =>
        _items.TryGetValue(itemId, out var item) ? item.Name : string.Create(CultureInfo.InvariantCulture, $"Item {itemId}");

    public string? PictureOf(int itemId) => _items.TryGetValue(itemId, out var item) ? item.Picture : null;

    private static GachaPrize ToPrize(PrizeSettings prize, int weight) =>
        new(prize.Id, prize.ItemClass, prize.Count, prize.UseHours, prize.ItemFlag, weight);
}
