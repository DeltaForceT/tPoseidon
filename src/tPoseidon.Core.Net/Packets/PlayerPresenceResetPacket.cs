using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Packets;

public sealed class PlayerPresenceResetPacket
{
    public const int PayloadSize = 4;

    public uint Value { get; init; }

    public static readonly PlayerPresenceResetPacket Zero = new();

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), Value);
        return buf;
    }

}
