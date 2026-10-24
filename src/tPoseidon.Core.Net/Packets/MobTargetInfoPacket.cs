using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_MOB_TARGET_INFO)]
public sealed class MobTargetInfoPacket
{
    public const int PayloadSize = 13;

    public required ushort EntityId { get; init; }
    public required uint FieldAt02Tbd { get; init; }
    public required uint FieldAt06Tbd { get; init; }
    public ushort FieldAt0ATbd { get; init; }
    public byte FieldAt0CTbd { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(2, 4), FieldAt02Tbd);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(6, 4), FieldAt06Tbd);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(10, 2), FieldAt0ATbd);
        buf[12] = FieldAt0CTbd;
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
            FieldAt02Tbd = BinaryPrimitives.ReadUInt32LittleEndian(payload[2..6]),
            FieldAt06Tbd = BinaryPrimitives.ReadUInt32LittleEndian(payload[6..10]),
            FieldAt0ATbd = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..12]),
            FieldAt0CTbd = payload[12],
        };
    }
}
