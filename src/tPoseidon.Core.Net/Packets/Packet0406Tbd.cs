using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet0406Tbd
{
    public const int PayloadSize = 4;

    public const uint MagicSoulDefault = 0x0000000E;

    public required uint PostAnimCode { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), PostAnimCode);
        return buf;
    }

}
