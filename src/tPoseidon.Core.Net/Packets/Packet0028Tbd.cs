using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet0028Tbd
{
    public const int PayloadSize = 4;

    public required ushort CasterEntityId { get; init; }
    public required ushort FieldAt02Tbd { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), CasterEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), FieldAt02Tbd);
        return buf;
    }

    public static Packet0028Tbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet0028Tbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet0028Tbd
        {
            CasterEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            FieldAt02Tbd = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
        };
    }
}
