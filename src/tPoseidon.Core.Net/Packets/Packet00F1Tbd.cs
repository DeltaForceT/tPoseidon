using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet00F1Tbd
{
    public const int PayloadSize = 4;

    public required byte SkillLevel { get; init; }
    public required ushort SkillId { get; init; }
    public required byte Slot { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = SkillLevel;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(1, 2), SkillId);
        buf[3] = Slot;
        return buf;
    }

    public static Packet00F1Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet00F1Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet00F1Tbd
        {
            SkillLevel = payload[0],
            SkillId = BinaryPrimitives.ReadUInt16LittleEndian(payload[1..3]),
            Slot = payload[3],
        };
    }
}
