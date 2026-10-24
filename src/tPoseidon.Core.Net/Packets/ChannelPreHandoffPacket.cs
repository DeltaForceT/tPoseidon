using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.CHANNEL_PRE_HANDOFF)]
public sealed class ChannelPreHandoffPacket
{
    public const int PayloadSize = 10;

    public static readonly ChannelPreHandoffPacket SnapshotDefault = new()
    {
        Bytes = new byte[]
        {
            0x00, 0x00, 0xC1, 0x8B, 0x1E, 0x06, 0x00, 0x00, 0x00, 0x00,
        },
    };

    public byte[] Bytes { get; init; } = Array.Empty<byte>();

    public byte[] EncodePayload()
    {
        if (Bytes.Length != PayloadSize)
        {
            throw new InvalidOperationException(
                $"ChannelPreHandoff.Bytes must be exactly {PayloadSize} bytes, got {Bytes.Length}.");
        }
        var copy = new byte[PayloadSize];
        Array.Copy(Bytes, copy, PayloadSize);
        return copy;
    }

    public static ChannelPreHandoffPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"ChannelPreHandoff payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new ChannelPreHandoffPacket { Bytes = payload.ToArray() };
    }
}
