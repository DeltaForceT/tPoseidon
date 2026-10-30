using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public sealed class BossGiveItemRoller
{
    public const int MostRecipients = 10;
    public const int RateScale = 1000;

    private readonly Dictionary<int, BossGiveItemEntry> _byMonster = new();

    public BossGiveItemRoller(IEnumerable<BossGiveItemEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries) _byMonster[entry.MonsterId] = entry;
    }

    public int Count => _byMonster.Count;

    public bool Gives(int monsterId) => _byMonster.ContainsKey(monsterId);

    public bool Gives(MonsterEntity monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.Atype != MonsterAType.DoppelGanger && Gives(monster.MonsterParamId);
    }

    public int? Roll(int monsterId, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (!_byMonster.TryGetValue(monsterId, out var entry)) return null;
        var slots = entry.Slots.Take(entry.FilledSlots).ToArray();
        var total = slots.Sum(slot => WeightOf(slot.Rate));
        if (total == 0) return null;
        var pick = rng.Next(total);
        var reached = 0;
        foreach (var slot in slots)
        {
            reached += WeightOf(slot.Rate);
            if (reached > pick) return slot.ItemId == 0 ? null : slot.ItemId;
        }
        return null;
    }

    private static int WeightOf(float rate) => (int)(rate * RateScale);
}
