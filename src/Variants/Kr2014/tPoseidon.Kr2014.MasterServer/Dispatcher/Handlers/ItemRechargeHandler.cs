using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class ItemRechargeHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests = { InterServerOpcode.IcmdChargeTimerItem };

    private readonly IItemRechargeStore _store;

    public ItemRechargeHandler(IItemRechargeStore store)
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
            var (recharge, now) = ItemRechargeMessages.DecodeRequest(frame.Body);
            var result = _store.Recharge(recharge, now);
            (opcode, body) = result.Succeeded
                ? (InterServerOpcode.IcmdChargeTimerItemAckOk, ItemRechargeMessages.EncodeResult(result))
                : (InterServerOpcode.IcmdChargeTimerItemAckCancel, ItemRechargeMessages.EncodeError(result.Error));
            ctx.Log(result.Succeeded
                ? $"item recharge 0x{recharge.CharacterId:X16}: item 0x{recharge.ItemUid:X} +{recharge.Hours} h until {result.BreakedAt:u}, {recharge.CouponCount} coupons used, {result.CouponLeft} left"
                : $"item recharge 0x{recharge.CharacterId:X16}: refused with code {result.Error}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"item recharge char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.IcmdChargeTimerItemAckCancel, ItemRechargeMessages.EncodeError(ItemRechargeErrors.StoreFailed));
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
