using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_DIALOG_STATE_ACK)]
public sealed class DialogStateAckPacket
{
    public const int PayloadSize = 2;

        public const ushort StandardMarker = 0x007F;

    public ushort Marker { get; init; } = StandardMarker;

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(Marker);
        return w.ToArray();
    }

    public static DialogStateAckPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"DialogStateAck payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new DialogStateAckPacket { Marker = r.ReadUInt16() };
    }
}
