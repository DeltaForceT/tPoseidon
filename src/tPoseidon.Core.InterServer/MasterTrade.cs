using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterTrade
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterTrade(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<TradeResult> TradeAsync(TradeOffer first, TradeOffer second, int bagCapacity, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        var reply = await CallAsync(first.CharacterId, InterServerOpcode.IcmdItemExchange, InterServerOpcode.IcmdItemExchangeAckOk,
            InterServerOpcode.IcmdItemExchangeAckCancel, TradeMessages.EncodeTrade(first, second, bagCapacity), ct).ConfigureAwait(false);
        if (reply is not { } answer) return new TradeResult(TradeRules.StoreFailed, null, null);
        return answer.Ok ? TradeMessages.DecodeResult(answer.Body) : new TradeResult(TradeMessages.DecodeError(answer.Body), null, null);
    }

    private async Task<(bool Ok, byte[] Body)?> CallAsync(ulong charUid, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode cancel, byte[] body, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[trade] Master not connected; 0x{(ushort)request:X4} for 0x{charUid:X} not sent");
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
            _log($"[trade] Master did not answer 0x{(ushort)request:X4} for 0x{charUid:X}");
            return null;
        }
    }
}
