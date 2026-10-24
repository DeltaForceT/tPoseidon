using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterCashBox
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterCashBox(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<IReadOnlyList<ChargedItem>?> ListAsync(ulong charUid, uint accountId, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, InterServerOpcode.IcmdPsgRetrieveItem, InterServerOpcode.IcmdPsgRetrieveItemAckOk,
            InterServerOpcode.IcmdPsgRetrieveItemAckCancel, CashBoxMessages.EncodeList(accountId), ct).ConfigureAwait(false);
        return reply is { Ok: true } ok ? CashBoxMessages.DecodeRows(ok.Body) : null;
    }

    public async Task<ChargedItemMoveResult> MoveAsync(ChargedItemMove move, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(move);
        var reply = await CallAsync(move.CharacterId, InterServerOpcode.IcmdPsgExchange, InterServerOpcode.IcmdPsgExchangeAckOk,
            InterServerOpcode.IcmdPsgExchangeAckCancel, CashBoxMessages.EncodeMove(move), ct).ConfigureAwait(false);
        if (reply is not { } answer) return new ChargedItemMoveResult(null, ChargedItemRules.MoveFailed);
        return answer.Ok
            ? new ChargedItemMoveResult(CashBoxMessages.DecodeMoved(answer.Body), 0)
            : new ChargedItemMoveResult(null, CashBoxMessages.DecodeError(answer.Body));
    }

    private async Task<(bool Ok, byte[] Body)?> CallAsync(ulong charUid, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode cancel, byte[] body, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[cashbox] Master not connected; 0x{(ushort)request:X4} for 0x{charUid:X} not sent");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = request,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = charUid,
                    RequestId = client.AllocateRequestId(),
                },
                Body = body,
            }, new HashSet<InterServerOpcode> { ok, cancel }, Timeout, ct).ConfigureAwait(false);
            return (reply.Opcode == ok, reply.Body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[cashbox] Master did not answer 0x{(ushort)request:X4} for 0x{charUid:X}");
            return null;
        }
    }
}
