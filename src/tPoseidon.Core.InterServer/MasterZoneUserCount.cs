namespace tPoseidon.Core.InterServer;

public sealed class MasterZoneUserCount
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterZoneUserCount(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<int?> CountAsync(ulong charUid, ushort zone, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[zone count] Master not connected; user count of zone {zone} not asked");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.McmdRequestZoneUserCount,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = charUid,
                    RequestId = client.AllocateRequestId(),
                },
                Body = ZoneUserCountMessages.EncodeRequest(zone),
            }, new HashSet<InterServerOpcode> { InterServerOpcode.McmdRequestZoneUserCountAckOk, InterServerOpcode.McmdRequestZoneUserCountAckCancel }, Timeout, ct).ConfigureAwait(false);
            if (reply.Opcode != InterServerOpcode.McmdRequestZoneUserCountAckOk) return null;
            var (answered, count) = ZoneUserCountMessages.DecodeCount(reply.Body);
            return answered == zone ? count : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[zone count] Master did not answer the user count of zone {zone}");
            return null;
        }
    }

    public static InterServerFrame Answer(InterServerFrame routed, ushort selfIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(routed);
        var (zone, originRequestId) = ZoneUserCountMessages.DecodeRouted(routed.Body);
        return new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdRequestZoneUserCountAckOk,
            Header = new InterServerHeader
            {
                SourceServerIndex = selfIndex,
                DestServerIndex = routed.Header.SourceServerIndex,
                CharUid = routed.Header.CharUid,
                RequestId = originRequestId,
            },
            Body = ZoneUserCountMessages.EncodeCount(zone, count),
        };
    }
}
