using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_UNK_9A)]
public sealed class LeaveZoneAckPacket
{
    public uint Header { get; init; }
    public required ulong CharUid { get; init; }
    public required string DestinationIp { get; init; }
    public required uint DestinationPort { get; init; }
    public uint SessionToken { get; init; }
    public required ushort DestMapId { get; init; }
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
    public byte ChangeType { get; init; }

    public int PayloadSize =>
        4
      + 8
      + Encoding.ASCII.GetByteCount(DestinationIp ?? string.Empty) + 1
      + 4
      + 4
      + 2
      + 2
      + 2
      + 1;

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(DestinationIp);

        var buf = new byte[PayloadSize];
        var off = 0;

        WriteU32(buf, off, Header); off += 4;
        WriteU64(buf, off, CharUid); off += 8;

        var ipBytes = Encoding.ASCII.GetBytes(DestinationIp);
        ipBytes.CopyTo(buf, off);
        off += ipBytes.Length;
        buf[off++] = 0;

        WriteU32(buf, off, DestinationPort); off += 4;
        WriteU32(buf, off, SessionToken); off += 4;
        WriteU16(buf, off, DestMapId); off += 2;
        WriteU16(buf, off, SpawnX); off += 2;
        WriteU16(buf, off, SpawnY); off += 2;
        buf[off] = ChangeType;

        return buf;
    }

    public static LeaveZoneAckPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 24)
        {
            throw new InvalidPacketException(
                $"LeaveZoneAck payload too short: {payload.Length} bytes (need at least 24).");
        }
        var header = ReadU32(payload, 0);
        var charUid = ReadU64(payload, 4);

        var ipStart = 12;
        var nul = payload[ipStart..].IndexOf((byte)0);
        if (nul < 0)
        {
            throw new InvalidPacketException("LeaveZoneAck: IP string has no NUL terminator.");
        }
        var ip = Encoding.ASCII.GetString(payload.Slice(ipStart, nul));
        var afterIp = ipStart + nul + 1;
        if (payload.Length - afterIp < 15)
        {
            throw new InvalidPacketException(
                $"LeaveZoneAck: trailing fields truncated ({payload.Length - afterIp} bytes after IP, need 15).");
        }

        return new LeaveZoneAckPacket
        {
            Header = header,
            CharUid = charUid,
            DestinationIp = ip,
            DestinationPort = ReadU32(payload, afterIp),
            SessionToken = ReadU32(payload, afterIp + 4),
            DestMapId = ReadU16(payload, afterIp + 8),
            SpawnX = ReadU16(payload, afterIp + 10),
            SpawnY = ReadU16(payload, afterIp + 12),
            ChangeType = payload[afterIp + 14],
        };
    }

    private static void WriteU16(byte[] buf, int off, ushort v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteU32(byte[] buf, int off, uint v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
        buf[off + 2] = (byte)((v >> 16) & 0xFF);
        buf[off + 3] = (byte)((v >> 24) & 0xFF);
    }

    private static void WriteU64(byte[] buf, int off, ulong v)
    {
        for (var i = 0; i < 8; i++) buf[off + i] = (byte)((v >> (i * 8)) & 0xFF);
    }

    private static ushort ReadU16(ReadOnlySpan<byte> b, int off) =>
        (ushort)(b[off] | (b[off + 1] << 8));

    private static uint ReadU32(ReadOnlySpan<byte> b, int off) =>
        (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

    private static ulong ReadU64(ReadOnlySpan<byte> b, int off)
    {
        ulong v = 0;
        for (var i = 0; i < 8; i++) v |= ((ulong)b[off + i]) << (i * 8);
        return v;
    }
}
