using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet011ETbd
{
    public const int PayloadSize = 9;

    public required ushort TargetEntityId { get; init; }
    public required ushort Damage { get; init; }
    public required uint ZeroPad0407 { get; init; }
    public required byte Flag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), TargetEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), Damage);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4, 4), ZeroPad0407);
        buf[8] = Flag;
        return buf;
    }

    public static Packet011ETbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet011ETbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet011ETbd
        {
            TargetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            Damage = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            ZeroPad0407 = BinaryPrimitives.ReadUInt32LittleEndian(payload[4..8]),
            Flag = payload[8],
        };
    }
}
