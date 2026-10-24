using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class SelectServerPacket
{
    public const int PayloadSize = 4;

    public ushort ServerIndex { get; init; }
    public ushort ChannelIndex { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(ServerIndex);
        w.WriteUInt16(ChannelIndex);
        return w.ToArray();
    }

    public static SelectServerPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"SelectServer payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new SelectServerPacket
        {
            ServerIndex = r.ReadUInt16(),
            ChannelIndex = r.ReadUInt16(),
        };
    }
}
