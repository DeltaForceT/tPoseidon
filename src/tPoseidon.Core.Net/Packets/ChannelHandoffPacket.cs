using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.CHANNEL_HANDOFF)]
public sealed class ChannelHandoffPacket
{
    public const int IpBufferWidth = 16;
    public const int MaxIpChars = IpBufferWidth - 1;
    public const int PortWidth = sizeof(ushort);
    public const int PayloadWidth = IpBufferWidth + PortWidth;

    public string IpAddress { get; init; } = string.Empty;
    public byte ReservedByte { get; init; } = 0x37;
    public ushort ChannelPort { get; init; } = 10006;

    public byte[] EncodePayload()
    {
        var ipBytes = Encoding.ASCII.GetBytes(IpAddress);
        if (ipBytes.Length > MaxIpChars)
        {
            throw new InvalidPacketException(
                $"ChannelHandoff IP '{IpAddress}' is {ipBytes.Length} bytes; the buffer holds {MaxIpChars} chars + NUL.");
        }
        var w = new PacketWriter(PayloadWidth);
        w.WriteBytes(ipBytes);
        for (var i = ipBytes.Length; i < MaxIpChars; i++)
        {
            w.WriteByte(0x00);
        }
        w.WriteByte(ipBytes.Length == MaxIpChars ? (byte)0x00 : ReservedByte);
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
        var ipField = payload[..IpBufferWidth];
        var nul = ipField.IndexOf((byte)0);
        var ip = Encoding.ASCII.GetString(nul < 0 ? ipField : ipField[..nul]);
        var reserved = payload[MaxIpChars];
        var port = BinaryPrimitives.ReadUInt16LittleEndian(payload[IpBufferWidth..]);
        return new ChannelHandoffPacket
        {
            IpAddress = ip,
            ReservedByte = reserved,
            ChannelPort = port,
        };
    }
}
