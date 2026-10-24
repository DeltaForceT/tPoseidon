using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class CashBoxHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.IcmdPsgRetrieveItem,
        InterServerOpcode.IcmdPsgExchange,
    };

    private readonly IChargedItemStore _store;

    public CashBoxHandler(IChargedItemStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        var listing = frame.Opcode == InterServerOpcode.IcmdPsgRetrieveItem;
        InterServerOpcode opcode;
        byte[] body;
        try
        {
            if (listing)
            {
                (opcode, body) = (InterServerOpcode.IcmdPsgRetrieveItemAckOk, CashBoxMessages.EncodeRows(_store.ListWaiting(CashBoxMessages.DecodeList(frame.Body))));
            }
            else
            {
                var result = _store.MoveToInventory(CashBoxMessages.DecodeMove(charUid, frame.Body));
                (opcode, body) = result.Succeeded
                    ? (InterServerOpcode.IcmdPsgExchangeAckOk, CashBoxMessages.EncodeMoved(result.Moved!))
                    : (InterServerOpcode.IcmdPsgExchangeAckCancel, CashBoxMessages.EncodeError(result.Error));
                if (!result.Succeeded) ctx.Log($"cash box take char 0x{charUid:X16}: error {result.Error}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"cash box 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = listing
                ? (InterServerOpcode.IcmdPsgRetrieveItemAckCancel, Array.Empty<byte>())
                : (InterServerOpcode.IcmdPsgExchangeAckCancel, CashBoxMessages.EncodeError(ChargedItemRules.MoveFailed));
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
