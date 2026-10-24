using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public static class ExchangeRules
{
    public const int RequiredTimes = 1;

    public static bool Matches(ExchangeOffer? offer, IReadOnlyList<CompoundMaterial> materials, int times)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (offer is null || times != RequiredTimes || materials.Count < offer.Requirements.Count) return false;
        var matched = 0;
        for (var j = 0; j < materials.Count && j < offer.Requirements.Count; j++)
        {
            var requirement = offer.Requirements[j];
            if (materials.Any(material => material.ItemId == requirement.ItemId && material.Count >= requirement.Count)) matched++;
        }
        return matched == offer.Requirements.Count && materials.Count == offer.Requirements.Count;
    }
}
