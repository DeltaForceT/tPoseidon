using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class LeaveZoneRequestPacket
{
    public const int PayloadSize = 5;

    public required ushort DestZone { get; init; }
    public required ushort SourceEntity { get; init; }
    public byte ChangeType { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(DestZone & 0xFF);
        buf[1] = (byte)((DestZone >> 8) & 0xFF);
        buf[2] = (byte)(SourceEntity & 0xFF);
        buf[3] = (byte)((SourceEntity >> 8) & 0xFF);
        buf[4] = ChangeType;
        return buf;
    }

    public static LeaveZoneRequestPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"LeaveZoneRequest payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new LeaveZoneRequestPacket
        {
            DestZone = (ushort)(payload[0] | (payload[1] << 8)),
            SourceEntity = (ushort)(payload[2] | (payload[3] << 8)),
            ChangeType = payload[4],
        };
    }
}
