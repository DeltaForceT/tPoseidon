using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet000CTbd
{
    public const int PayloadSize = 28;

    public const ushort ConstAt0A = 0x8000;
    public const ushort ConstAt0C = 0x0100;
    public const ushort ConstAt0E = 0x2E84;
    public const byte ConstAt1A = 0x01;

    public required ushort TargetEntityId { get; init; }
    public required ushort ProjectileEntityId { get; init; }
    public required ushort TargetX { get; init; }
    public required ushort TargetY { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), TargetEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), ProjectileEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), TargetX);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), TargetY);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(10, 2), ConstAt0A);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(12, 2), ConstAt0C);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(14, 2), ConstAt0E);
        buf[26] = ConstAt1A;
        return buf;
    }
}
