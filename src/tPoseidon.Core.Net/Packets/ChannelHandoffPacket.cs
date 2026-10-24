using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.CHANNEL_HANDOFF)]
public sealed class ChannelHandoffPacket
{
    public const int IpFieldWidth = 15;
    public const int PayloadWidth = IpFieldWidth + 1 + 2;

    public string IpAddress { get; init; } = string.Empty;
    public byte ReservedByte { get; init; } = 0x37;
    public ushort ChannelPort { get; init; } = 10006;

    public byte[] EncodePayload()
    {
        var ipBytes = Encoding.ASCII.GetBytes(IpAddress);
        if (ipBytes.Length >= IpFieldWidth)
        {
            throw new InvalidPacketException(
                $"ChannelHandoff IP '{IpAddress}' is {ipBytes.Length} bytes; field holds {IpFieldWidth - 1} chars + NUL.");
        }
        var w = new PacketWriter(PayloadWidth);
        w.WriteBytes(ipBytes);
        for (var i = ipBytes.Length; i < IpFieldWidth; i++)
        {
            w.WriteByte(0x00);
        }
        w.WriteByte(ReservedByte);
        w.WriteUInt16(ChannelPort);
        return w.ToArray();
    }

    public static ChannelHandoffPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadWidth)
        {
            throw new InvalidPacketException(
                $"ChannelHandoff payload must be exactly {PayloadWidth} bytes, got {payload.Length}.");
        }
        var ipField = payload[..IpFieldWidth];
        var nul = ipField.IndexOf((byte)0);
        var ip = Encoding.ASCII.GetString(nul < 0 ? ipField : ipField[..nul]);
        var reserved = payload[IpFieldWidth];
        var port = (ushort)(payload[IpFieldWidth + 1] | (payload[IpFieldWidth + 2] << 8));
        return new ChannelHandoffPacket
        {
            IpAddress = ip,
            ReservedByte = reserved,
            ChannelPort = port,
        };
    }
}
