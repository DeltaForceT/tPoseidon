using System.Buffers.Binary;
using tPoseidon.Core.Common;

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

    public static PlayerPresenceResetPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"PlayerPresenceReset payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new PlayerPresenceResetPacket
        {
            Value = BinaryPrimitives.ReadUInt32LittleEndian(payload[0..4]),
        };
    }
}
