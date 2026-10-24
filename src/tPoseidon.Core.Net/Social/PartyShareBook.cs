using System.Collections.Concurrent;

namespace tPoseidon.Core.Net.Social;

public sealed class PartyShareBook
{
    public const float RateEpsilon = 0.005f;
    public const float PercentUnit = 0.01f;
    public const double PostThreshold = 0.99;

    private readonly ConcurrentDictionary<ulong, Bound> _bound = new();

    public PartyShareBook(SystemServerLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        link.OrderReceived += ApplyAsync;
    }

    public IReadOnlyList<(ulong CharacterId, int Gain)>? SplitExp(ulong earner, int amount) =>
        Split(earner, amount, e => e.ExpPercent, e => e.ExpBonus);

    public IReadOnlyList<(ulong CharacterId, int Gain)>? SplitTm(ulong earner, int amount) =>
        Split(earner, amount, e => e.TmPercent, e => e.TmBonus);

    public static int GainFor(int amount, float bonus, ushort percent)
    {
        var share = Math.Min(percent * PercentUnit, 1f);
        var boosted = (float)(amount + (int)(amount * (bonus + RateEpsilon)));
        return (int)(boosted * share);
    }

    public Task ApplyAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        switch (order.OrderType)
        {
            case SystemLinkFrames.OrderShareExp:
                var share = PartyBonusTables.DecodeShare(order.Data);
                var group = share.Entries.Select(e => e.CharacterId).ToArray();
                foreach (var entry in share.Entries) _bound[entry.CharacterId] = new Bound(order.OrderId, share.ShareFlag, entry, group);
                break;
            case SystemLinkFrames.OrderCancel:
                var cancelled = SystemLinkFrames.ReadCancelledOrderId(order.Data);
                foreach (var (characterId, bound) in _bound)
                {
                    if (bound.OrderId == cancelled) _bound.TryRemove(new KeyValuePair<ulong, Bound>(characterId, bound));
                }
                break;
        }
        return Task.CompletedTask;
    }

    private List<(ulong CharacterId, int Gain)>? Split(ulong earner, int amount, Func<PartyShareEntry, ushort> percentOf, Func<PartyShareEntry, float> bonusOf)
    {
        if (!_bound.TryGetValue(earner, out var bound)) return null;
        var recipients = bound.ShareFlag == 0 ? new[] { earner } : bound.Group;
        var gains = new List<(ulong CharacterId, int Gain)>(recipients.Count);
        foreach (var recipient in recipients)
        {
            if (!_bound.TryGetValue(recipient, out var theirs) || theirs.OrderId != bound.OrderId) continue;
            var percent = percentOf(theirs.Entry);
            if ((int)(amount * Math.Min(percent * PercentUnit, 1f) + PostThreshold) == 0) continue;
            gains.Add((recipient, GainFor(amount, bonusOf(theirs.Entry), percent)));
        }
        return gains;
    }

    private sealed record Bound(ushort OrderId, byte ShareFlag, PartyShareEntry Entry, IReadOnlyList<ulong> Group);
}
