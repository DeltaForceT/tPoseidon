using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet00F0Tbd
{
    public const int PayloadSize = 3;

    public required ushort FieldAt00Tbd { get; init; }
    public required byte FieldAt02Tbd { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), FieldAt00Tbd);
        buf[2] = FieldAt02Tbd;
        return buf;
    }

}
