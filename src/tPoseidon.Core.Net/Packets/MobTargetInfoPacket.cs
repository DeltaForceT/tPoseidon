using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_MOB_TARGET_INFO)]
public sealed class MobTargetInfoPacket
{
    public const int PayloadSize = 13;

    public required ushort EntityId { get; init; }
    public required uint Hp { get; init; }
    public required uint Mp { get; init; }
    public ushort HpBars { get; init; }
    public byte BossFlag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(2, 4), Hp);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(6, 4), Mp);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(10, 2), HpBars);
        buf[12] = BossFlag;
        return buf;
    }

    public static MobTargetInfoPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"MobTargetInfo payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new MobTargetInfoPacket
        {
            EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            Hp = BinaryPrimitives.ReadUInt32LittleEndian(payload[2..6]),
            Mp = BinaryPrimitives.ReadUInt32LittleEndian(payload[6..10]),
            HpBars = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..12]),
            BossFlag = payload[12],
        };
    }
}
