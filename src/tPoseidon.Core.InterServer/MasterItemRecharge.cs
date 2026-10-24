using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterItemRecharge
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterItemRecharge(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<ItemRechargeResult> RechargeAsync(ItemRecharge recharge, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recharge);
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[recharge] Master not connected; recharge of item 0x{recharge.ItemUid:X} not sent");
            return ItemRechargeResult.Refused(ItemRechargeErrors.StoreFailed);
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.IcmdChargeTimerItem,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = recharge.CharacterId,
                    RequestId = client.AllocateRequestId(),
                },
                Body = ItemRechargeMessages.EncodeRequest(recharge, now),
            }, new HashSet<InterServerOpcode> { InterServerOpcode.IcmdChargeTimerItemAckOk, InterServerOpcode.IcmdChargeTimerItemAckCancel }, Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.IcmdChargeTimerItemAckOk
                ? ItemRechargeMessages.DecodeResult(reply.Body)
                : ItemRechargeResult.Refused(ItemRechargeMessages.DecodeError(reply.Body));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[recharge] Master did not answer the recharge of item 0x{recharge.ItemUid:X}");
            return ItemRechargeResult.Refused(ItemRechargeErrors.StoreFailed);
        }
    }
}
