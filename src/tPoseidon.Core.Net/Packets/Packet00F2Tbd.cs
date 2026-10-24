using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet00F2Tbd
{
    public const int PayloadSize = 9;

    public required ushort PlayerEntityId { get; init; }
    public required byte FieldAt02Tbd { get; init; }
    public required ushort FieldAt03Tbd { get; init; }
    public required byte FieldAt05Tbd { get; init; }
    public required byte FieldAt06Tbd { get; init; }
    public required ushort TargetEntityId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), PlayerEntityId);
        buf[2] = FieldAt02Tbd;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(3, 2), FieldAt03Tbd);
        buf[5] = FieldAt05Tbd;
        buf[6] = FieldAt06Tbd;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(7, 2), TargetEntityId);
        return buf;
    }

}
