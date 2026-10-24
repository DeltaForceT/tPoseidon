namespace tPoseidon.Core.InterServer;

public readonly record struct MasterAnswer(bool Ok, byte[] Body);

public sealed class MasterChannel
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly string _area;
    private readonly Action<string> _log;

    public MasterChannel(Func<MasterServerClient?> client, ushort selfIndex, string area, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task SendAsync(ulong charUid, InterServerOpcode opcode, byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[{_area}] Master not connected; 0x{(ushort)opcode:X4} for 0x{charUid:X} not sent");
            return;
        }
        await client.SendAsync(Frame(client, charUid, opcode, body), ct).ConfigureAwait(false);
    }

    public async Task<MasterAnswer?> CallAsync(ulong charUid, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode? cancel, byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[{_area}] Master not connected; 0x{(ushort)request:X4} for 0x{charUid:X} not sent");
            return null;
        }
        var expected = new HashSet<InterServerOpcode> { ok };
        if (cancel is { } cancelOpcode) expected.Add(cancelOpcode);
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(Frame(client, charUid, request, body), expected, Timeout, ct).ConfigureAwait(false);
            return new MasterAnswer(reply.Opcode == ok, reply.Body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[{_area}] Master did not answer 0x{(ushort)request:X4} for 0x{charUid:X}");
            return null;
        }
    }

    private InterServerFrame Frame(MasterServerClient client, ulong charUid, InterServerOpcode opcode, byte[] body) => new()
    {
        Opcode = opcode,
        Header = new InterServerHeader
        {
            SourceServerIndex = _selfIndex,
            DestServerIndex = 0,
            CharUid = charUid,
            RequestId = client.AllocateRequestId(),
        },
        Body = body,
    };
}
