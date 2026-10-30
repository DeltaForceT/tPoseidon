using System.Collections.Concurrent;

namespace tPoseidon.Core.Net.Social;

public sealed record PartyVitalsMessage(ulong To, byte[] Payload);

public sealed class PartyObserveBook
{
    public const int PollIntervalMilliseconds = 500;

    private readonly ConcurrentDictionary<ulong, Watch> _watches = new();

    public PartyObserveBook(SystemServerLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        link.OrderReceived += ApplyAsync;
    }

    public int Count => _watches.Count;

    public Task ApplyAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        switch (order.OrderType)
        {
            case SystemLinkFrames.OrderObserve:
                _watches[order.CharacterId] = new Watch(order.OrderId, SystemLinkFrames.DecodeObserve(order.Data));
                break;
            case SystemLinkFrames.OrderCancel:
                var cancelled = SystemLinkFrames.ReadCancelledOrderId(order.Data);
                foreach (var (characterId, watch) in _watches)
                {
                    if (watch.OrderId == cancelled) _watches.TryRemove(new KeyValuePair<ulong, Watch>(characterId, watch));
                }
                break;
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<PartyVitalsMessage> Poll(TimeSpan elapsed, Func<ulong, PartyVitals?> vitalsOf)
    {
        ArgumentNullException.ThrowIfNull(vitalsOf);
        var messages = new List<PartyVitalsMessage>();
        foreach (var (observedId, watch) in _watches)
        {
            watch.Remaining -= elapsed;
            if (watch.Remaining > TimeSpan.Zero) continue;
            watch.Remaining = TimeSpan.FromMilliseconds(PollIntervalMilliseconds);
            if (vitalsOf(observedId) is not { } live) continue;
            var mask = PartyFrames.VitalsChanges(watch.Cached, live);
            if (mask == 0) continue;
            watch.Cached = live;
            var payload = PartyFrames.MemberVitalsFor(watch.Observe.ObservedMemberId, mask, live);
            foreach (var other in watch.Observe.Others)
            {
                if (other.CharacterId == observedId || vitalsOf(other.CharacterId) is null) continue;
                messages.Add(new PartyVitalsMessage(other.CharacterId, payload));
            }
        }
        return messages;
    }

    private sealed class Watch
    {
        public Watch(ushort orderId, PartyObserve observe)
        {
            OrderId = orderId;
            Observe = observe;
        }

        public ushort OrderId { get; }
        public PartyObserve Observe { get; }
        public PartyVitals Cached { get; set; } = new(0, 0, 0, 0, 0, 0, 0);
        public TimeSpan Remaining { get; set; }
    }
}
