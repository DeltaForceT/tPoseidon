using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet002BTbd
{
    public const int PayloadSize = 3;

    public const byte EndFlagContinuous = 0x03;

    public required ushort EntityId { get; init; }
    public required byte EndFlag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        buf[2] = EndFlag;
        return buf;
    }
}
