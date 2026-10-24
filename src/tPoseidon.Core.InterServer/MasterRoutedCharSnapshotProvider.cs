using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedCharSnapshotProvider : ICharSnapshotProvider
{
    private static readonly HashSet<InterServerOpcode> AckOpcodes = new()
    {
        InterServerOpcode.TpCharStoreCallAckOk,
        InterServerOpcode.TpCharStoreCallAckCancel,
    };

    private readonly MasterServerClient _client;
    private readonly ICharSnapshotProvider _fallback;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;
    private readonly TimeSpan _timeout;

    public MasterRoutedCharSnapshotProvider(
        MasterServerClient client,
        ICharSnapshotProvider fallback,
        ushort selfIndex,
        Action<string> log,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(log);
        _client = client;
        _fallback = fallback;
        _selfIndex = selfIndex;
        _log = log;
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public CharacterSnapshot? GetSnapshot(ulong characterId)
    {
        if (!_client.IsConnected)
        {
            _log($"snapshot char 0x{characterId:X16}: master not connected; using local fallback");
            return _fallback.GetSnapshot(characterId);
        }

        try
        {
            var subOp = new byte[1] { (byte)CharStoreSubOp.GetSnapshot };
            var request = new InterServerFrame
            {
                Opcode = InterServerOpcode.TpCharStoreCall,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = _client.AllocateRequestId(),
                },
                Body = subOp,
            };
            var reply = _client
                .SendAndAwaitReplyAsync(request, AckOpcodes, _timeout, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (reply.Opcode != InterServerOpcode.TpCharStoreCallAckOk)
            {
                _log($"snapshot char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GetSnapshot(characterId);
            }
            var reader = new PacketReader(reply.Body);
            var present = reader.ReadByte() != 0;
            if (!present) return null;
            return CharSnapshotWire.Decode(reply.Body.AsSpan(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"snapshot char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetSnapshot(characterId);
        }
    }
}
