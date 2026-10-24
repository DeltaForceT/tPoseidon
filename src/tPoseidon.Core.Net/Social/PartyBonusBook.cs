using System.Collections.Concurrent;

namespace tPoseidon.Core.Net.Social;

public sealed class PartyBonusBook
{
    public const uint SameGenderBonus = 1;
    public const uint FirstClassBonus = 2;
    public const uint LastClassBonus = 5;
    public const int StatsPerClass = 3;
    public const short ClassStatPoints = 4;
    public const double RecoveryFactor = 0.800000011920929;

    private readonly ConcurrentDictionary<ulong, (ushort OrderId, uint Bonus)> _bonuses = new();

    public PartyBonusBook(SystemServerLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        link.OrderReceived += ApplyAsync;
    }

    public event Func<ulong, Task>? Changed;

    public uint? BonusOf(ulong characterId) => _bonuses.TryGetValue(characterId, out var held) ? held.Bonus : null;

    public int RecoveryPeriod(ulong characterId, int periodMs) =>
        BonusOf(characterId) == SameGenderBonus ? (int)(periodMs * RecoveryFactor) : periodMs;

    public IReadOnlyList<short> WithBonus(ulong characterId, IReadOnlyList<short> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (BonusOf(characterId) is not uint bonus || bonus < FirstClassBonus || bonus > LastClassBonus) return points;
        var boostedClass = (int)(bonus - FirstClassBonus);
        return points.Select((value, index) => index / StatsPerClass == boostedClass ? (short)(value + ClassStatPoints) : value).ToArray();
    }

    public async Task ApplyAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        switch (order.OrderType)
        {
            case SystemLinkFrames.OrderTypeBonus:
                var bonus = new PacketReader(order.Data).ReadUInt32();
                var previous = BonusOf(order.CharacterId);
                _bonuses[order.CharacterId] = (order.OrderId, bonus);
                if (previous != bonus) await RaiseAsync(order.CharacterId).ConfigureAwait(false);
                break;
            case SystemLinkFrames.OrderCancel:
                var cancelled = SystemLinkFrames.ReadCancelledOrderId(order.Data);
                if (_bonuses.TryGetValue(order.CharacterId, out var held) && held.OrderId == cancelled
                    && _bonuses.TryRemove(new KeyValuePair<ulong, (ushort, uint)>(order.CharacterId, held)))
                {
                    await RaiseAsync(order.CharacterId).ConfigureAwait(false);
                }
                break;
        }
    }

    private Task RaiseAsync(ulong characterId) => Changed is { } changed ? changed(characterId) : Task.CompletedTask;
}
