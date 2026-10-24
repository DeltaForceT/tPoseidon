using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class TradeHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests = { InterServerOpcode.IcmdItemExchange };

    private readonly ITradeStore _store;

    public TradeHandler(ITradeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        InterServerOpcode opcode;
        byte[] body;
        try
        {
            var (first, second, bagCapacity) = TradeMessages.DecodeTrade(frame.Body);
            var result = _store.Trade(first, second, bagCapacity);
            (opcode, body) = result.Succeeded
                ? (InterServerOpcode.IcmdItemExchangeAckOk, TradeMessages.EncodeResult(result.First!, result.Second!))
                : (InterServerOpcode.IcmdItemExchangeAckCancel, TradeMessages.EncodeError(result.Error));
            ctx.Log(result.Succeeded
                ? $"trade 0x{first.CharacterId:X16} <-> 0x{second.CharacterId:X16}: {first.Slots.Count} + {second.Slots.Count} items, {first.Money} / {second.Money} money"
                : $"trade 0x{first.CharacterId:X16} <-> 0x{second.CharacterId:X16}: refused with code {result.Error}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"trade char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.IcmdItemExchangeAckCancel, TradeMessages.EncodeError(TradeRules.StoreFailed));
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = body,
        }, ct).ConfigureAwait(false);
    }
}
