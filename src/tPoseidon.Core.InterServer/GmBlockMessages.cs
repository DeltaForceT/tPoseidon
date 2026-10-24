using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public readonly record struct GmBlockRequest(ulong CharacterId, byte Type, ushort Minutes, ulong GameMasterId);

public readonly record struct GmUnblockRequest(ulong CharacterId, byte Type, ulong GameMasterId);

public static class GmBlockMessages
{
    public const int BlockBodyLength = sizeof(ulong) + sizeof(byte) + sizeof(ushort) + sizeof(ulong);
    public const int UnblockBodyLength = sizeof(ulong) + sizeof(byte) + sizeof(ulong);
    public const int AckBodyLength = UnblockBodyLength;

    public static byte[] EncodeBlock(GmBlockRequest request)
    {
        var w = new PacketWriter(BlockBodyLength);
        w.WriteUInt64(request.CharacterId);
        w.WriteByte(request.Type);
        w.WriteUInt16(request.Minutes);
        w.WriteUInt64(request.GameMasterId);
        return w.ToArray();
    }

    public static bool TryDecodeBlock(ReadOnlySpan<byte> body, out GmBlockRequest request)
    {
        request = default;
        if (body.Length < BlockBodyLength) return false;
        var r = new PacketReader(body);
        request = new GmBlockRequest(r.ReadUInt64(), r.ReadByte(), r.ReadUInt16(), r.ReadUInt64());
        return true;
    }

    public static byte[] EncodeUnblock(GmUnblockRequest request)
    {
        var w = new PacketWriter(UnblockBodyLength);
        w.WriteUInt64(request.CharacterId);
        w.WriteByte(request.Type);
        w.WriteUInt64(request.GameMasterId);
        return w.ToArray();
    }

    public static bool TryDecodeUnblock(ReadOnlySpan<byte> body, out GmUnblockRequest request)
    {
        request = default;
        if (body.Length < UnblockBodyLength) return false;
        var r = new PacketReader(body);
        request = new GmUnblockRequest(r.ReadUInt64(), r.ReadByte(), r.ReadUInt64());
        return true;
    }

    public static byte[] EncodeAck(GmUnblockRequest target) => EncodeUnblock(target);

    public static bool TryDecodeAck(ReadOnlySpan<byte> body, out GmUnblockRequest target) => TryDecodeUnblock(body, out target);

    public static InterServerFrame? Answer(IGmBlockStore store, InterServerFrame request)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        switch (request.Opcode)
        {
            case InterServerOpcode.CcmdBlock when TryDecodeBlock(request.Body, out var block):
                return Reply(request, new GmUnblockRequest(block.CharacterId, block.Type, block.GameMasterId), block.CharacterId == 0
                    ? GmBlockRules.NoCharacter
                    : store.BlockCharacter(block.CharacterId, block.Type, block.Minutes, block.GameMasterId));
            case InterServerOpcode.CcmdUnblock when TryDecodeUnblock(request.Body, out var unblock):
                return Reply(request, unblock, unblock.CharacterId == 0
                    ? GmBlockRules.NoCharacter
                    : store.UnblockCharacter(unblock.CharacterId, unblock.Type, unblock.GameMasterId));
            default:
                return null;
        }
    }

    public static InterServerFrame? Cancelled(InterServerFrame request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Opcode switch
        {
            InterServerOpcode.CcmdBlock when TryDecodeBlock(request.Body, out var block) =>
                Reply(request, new GmUnblockRequest(block.CharacterId, block.Type, block.GameMasterId), GmBlockRules.MasterCancelled),
            InterServerOpcode.CcmdUnblock when TryDecodeUnblock(request.Body, out var unblock) =>
                Reply(request, unblock, GmBlockRules.MasterCancelled),
            _ => null,
        };
    }

    private static InterServerFrame Reply(InterServerFrame request, GmUnblockRequest target, int result)
    {
        var (ok, cancel) = request.Opcode == InterServerOpcode.CcmdBlock
            ? (InterServerOpcode.CcmdBlockAckOk, InterServerOpcode.CcmdBlockAckCancel)
            : (InterServerOpcode.CcmdUnblockAckOk, InterServerOpcode.CcmdUnblockAckCancel);
        return new InterServerFrame
        {
            Opcode = result == GmBlockRules.Ok ? ok : cancel,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = request.Header.SourceServerIndex,
                CharUid = request.Header.CharUid,
                RequestId = request.Header.RequestId,
            },
            Body = EncodeAck(target),
        };
    }
}
