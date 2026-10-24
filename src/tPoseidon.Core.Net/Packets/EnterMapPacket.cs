using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class EnterMapPacket
{
    public const int PayloadSize = 13;

    public required ulong CharId { get; init; }
    public ushort AuxToken { get; init; }
    public ushort Reserved10 { get; init; }
    public byte ProtocolConst { get; init; } = 0x02;

    public uint DestPortEcho => (uint)(AuxToken | (Reserved10 << 16));

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        for (var i = 0; i < 8; i++) buf[i] = (byte)((CharId >> (i * 8)) & 0xFF);
        buf[8] = (byte)(AuxToken & 0xFF);
        buf[9] = (byte)((AuxToken >> 8) & 0xFF);
        buf[10] = (byte)(Reserved10 & 0xFF);
        buf[11] = (byte)((Reserved10 >> 8) & 0xFF);
        buf[12] = ProtocolConst;
        return buf;
    }

    public static EnterMapPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"EnterMap payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        ulong charId = 0;
        for (var i = 0; i < 8; i++) charId |= ((ulong)payload[i]) << (i * 8);
        return new EnterMapPacket
        {
            CharId = charId,
            AuxToken = (ushort)(payload[8] | (payload[9] << 8)),
            Reserved10 = (ushort)(payload[10] | (payload[11] << 8)),
            ProtocolConst = payload[12],
        };
    }
}
