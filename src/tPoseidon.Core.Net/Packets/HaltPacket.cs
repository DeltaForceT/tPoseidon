using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class HaltPacket
{
    public const int PayloadSize = 7;
    public const byte HaltFlag = 0x01;

    public required ushort EntityId { get; init; }
    public required ushort X { get; init; }
    public required ushort Y { get; init; }
    public byte Flag { get; init; } = HaltFlag;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), X);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), Y);
        buf[6] = Flag;
        return buf;
    }
}
