using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class MobHpUpdatePacket
{
    public const int PayloadSize = 9;

    public required ushort EntityId { get; init; }
    public required uint RemainingHp { get; init; }
    public ushort SecondLifeCount { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(2, 4), RemainingHp);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), SecondLifeCount);
        return buf;
    }

    public static MobHpUpdatePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"MobHpUpdate payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new MobHpUpdatePacket
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            RemainingHp = BinaryPrimitives.ReadUInt32LittleEndian(payload[2..6]),
            SecondLifeCount = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]),
        };
    }
}
