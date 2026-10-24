using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class AttackStancePacket
{
    public const int PayloadSize = 7;
    public const byte MeleeStanceFlag = 0x02;

    public required ushort EntityId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public byte StanceFlag { get; init; } = MeleeStanceFlag;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), Y);
        buf[6] = StanceFlag;
        return buf;
    }

    public static AttackStancePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"AttackStance payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new AttackStancePacket
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
            X = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            Y = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            StanceFlag = payload[6],
        };
    }
}
