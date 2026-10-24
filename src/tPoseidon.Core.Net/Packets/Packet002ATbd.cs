using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet002ATbd
{
    public const int PayloadSize = 26;

    public const ushort HitFlagNormal = 0x0003;
    public const byte HitMarkerA = 0x80;
    public const byte HitMarkerB = 0x80;

    public required ushort VictimEntityId { get; init; }
    public required ushort AnimSubOp { get; init; }
    public required ushort HitFlag { get; init; }
    public required byte MarkerAt10 { get; init; }
    public required byte MarkerAt14 { get; init; }
    public required uint HitKind { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), VictimEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), AnimSubOp);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), HitFlag);
        buf[10] = MarkerAt10;
        buf[14] = MarkerAt14;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(22, 4), HitKind);
        return buf;
    }

}
