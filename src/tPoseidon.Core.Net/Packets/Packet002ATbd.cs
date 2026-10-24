using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet002ATbd
{
    public const int PayloadSize = 26;

    public const ushort MagicSoulAnimSubOp = 0x001E;
    public const ushort HitFlagNormal = 0x0003;
    public const ushort HitFlagCrit = 0x0083;
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

    public static Packet002ATbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet002ATbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet002ATbd
        {
            VictimEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            AnimSubOp = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            HitFlag = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]),
            MarkerAt10 = payload[10],
            MarkerAt14 = payload[14],
            HitKind = BinaryPrimitives.ReadUInt32LittleEndian(payload[22..26]),
        };
    }
}
