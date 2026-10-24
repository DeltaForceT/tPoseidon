using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet00E0Tbd
{
    public const int PayloadSize = 14;

    public const byte ConstFlagAt08 = 0x02;
    public const byte ConstFlagAt09 = 0xFF;

    public required ushort PlayerEntityId { get; init; }
    public required ushort TargetEntityId { get; init; }
    public required ushort RendererFactoryId { get; init; }
    public required ushort SkillId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), PlayerEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), TargetEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), RendererFactoryId);
        buf[8] = ConstFlagAt08;
        buf[9] = ConstFlagAt09;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(10, 2), SkillId);
        return buf;
    }

    public static Packet00E0Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet00E0Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet00E0Tbd
        {
            PlayerEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            TargetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            RendererFactoryId = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]),
            SkillId = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..12]),
        };
    }
}
