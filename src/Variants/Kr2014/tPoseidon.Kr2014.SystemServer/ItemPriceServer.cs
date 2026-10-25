using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class ItemPriceServer
{
    private readonly ItemPriceBoard _board = new();

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register(ItemPriceFrames.Report, (_, payload, _) =>
        {
            if (ItemPriceFrames.TryReadReport(payload.Span, out var itemId, out var price)) _board.Record(itemId, price);
            return Task.CompletedTask;
        });
        dispatcher.Register(ItemPriceFrames.Query, (_, payload, ct) =>
            ItemPriceFrames.TryReadQuery(payload.Span, out var itemId)
                ? ctx.SendFrameAsync(ItemPriceFrames.Statistics, ItemPriceFrames.Reply(itemId, _board.Find(itemId)), (byte)Random.Shared.Next(1, 256), ct)
                : Task.CompletedTask);
    }
}
