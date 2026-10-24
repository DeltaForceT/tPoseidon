using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record OccupationChange(ushort Type, byte Job, byte Hair, byte Tier);

public sealed record OccupationChanged(uint Type, uint Job, uint Tier);

public static class OccupationMessages
{
    public static byte[] EncodeChange(OccupationChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var w = new PacketWriter(5);
        w.WriteUInt16(change.Type);
        w.WriteByte(change.Job);
        w.WriteByte(change.Hair);
        w.WriteByte(change.Tier);
        return w.ToArray();
    }

    public static OccupationChange DecodeChange(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new OccupationChange(r.ReadUInt16(), r.ReadByte(), r.ReadByte(), r.ReadByte());
    }

    public static byte[] EncodeChanged(OccupationChanged changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var w = new PacketWriter(12);
        w.WriteUInt32(changed.Type);
        w.WriteUInt32(changed.Job);
        w.WriteUInt32(changed.Tier);
        return w.ToArray();
    }

    public static OccupationChanged DecodeChanged(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new OccupationChanged(r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32());
    }
}

public sealed class MasterOccupation
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly HashSet<InterServerOpcode> Replies = new()
    {
        InterServerOpcode.CcmdChangeOccupationAckOk,
        InterServerOpcode.CcmdChangeOccupationAckCancel,
    };

    private static readonly HashSet<InterServerOpcode> StateReplies = new()
    {
        InterServerOpcode.TpJobQuestStateAckOk,
        InterServerOpcode.TpJobQuestStateAckCancel,
    };

    private static readonly HashSet<InterServerOpcode> IndexReplies = new()
    {
        InterServerOpcode.CcmdUpdateOccupationIndexAckOk,
        InterServerOpcode.CcmdUpdateOccupationIndexAckCancel,
    };

    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterOccupation(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<bool> SetJobQuestStateAsync(ulong characterId, byte state, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client) return false;
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.TpJobQuestState,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = client.AllocateRequestId(),
                },
                Body = new[] { state },
            }, StateReplies, Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.TpJobQuestStateAckOk;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[job-change] Master did not answer the quest state for 0x{characterId:X}");
            return false;
        }
    }

    public async Task<bool> SetJobIndexAsync(ulong characterId, byte jobIndex, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[look-change] Master not connected; index for 0x{characterId:X} not sent");
            return false;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.CcmdUpdateOccupationIndex,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = client.AllocateRequestId(),
                },
                Body = new[] { jobIndex },
            }, IndexReplies, Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.CcmdUpdateOccupationIndexAckOk;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[look-change] Master did not answer the index for 0x{characterId:X}");
            return false;
        }
    }

    public async Task<OccupationChanged?> ChangeAsync(ulong characterId, OccupationChange change, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[job-change] Master not connected; change for 0x{characterId:X} not sent");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.CcmdChangeOccupation,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = client.AllocateRequestId(),
                },
                Body = OccupationMessages.EncodeChange(change),
            }, Replies, Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.CcmdChangeOccupationAckOk ? OccupationMessages.DecodeChanged(reply.Body) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[job-change] Master did not answer the change for 0x{characterId:X}");
            return null;
        }
    }
}
