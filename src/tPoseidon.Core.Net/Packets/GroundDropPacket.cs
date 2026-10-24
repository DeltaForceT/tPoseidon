using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class GroundDropPacket
{
    public const int PayloadSize = 28;
    public const uint TrailerSignature = 0x84010080u;

    public required ushort DeadMobEntityId { get; init; }
    public required ushort GroundItemEntityId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public required uint ItemIndex { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), DeadMobEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), GroundItemEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), Y);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(12, 4), TrailerSignature);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(16, 4), ItemIndex);
        buf[26] = 0x01;
        return buf;
    }

    public static GroundDropPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GroundDrop payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new GroundDropPacket
        {
            DeadMobEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
            GroundItemEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            X = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            Y = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]),
            ItemIndex = BinaryPrimitives.ReadUInt32LittleEndian(payload[16..20]),
        };
    }
}
