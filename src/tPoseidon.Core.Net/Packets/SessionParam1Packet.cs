using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.SESSION_PARAM_1)]
public sealed class SessionParam1Packet
{
    public const int PayloadSize = 31;
    public const int CharUidOffset = 0;
    public const int IpOffset = 8;
    public const int IpFieldBytes = 15;
    public const int PortOffset = 23;
    public const int SessionTokenOffset = 27;

    public static readonly byte[] SnapshotBytes =
    {
        0x8E, 0x8F, 0x09, 0x06, 0x00, 0x00, 0x00, 0x00,
        0x31, 0x32, 0x37, 0x2E, 0x30, 0x2E, 0x30, 0x2E,
        0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x18, 0x34, 0x00, 0x00,
        0x32, 0x07, 0x00, 0x00,
    };

    public ulong SelectedCharUid { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public uint LoginPort { get; init; }
        public uint SessionToken { get; init; }
    public byte[] BaseBytes { get; init; } = Array.Empty<byte>();

    public byte[] EncodePayload()
    {
        if (BaseBytes.Length != PayloadSize)
        {
            throw new InvalidOperationException(
                $"SessionParam1.BaseBytes must be {PayloadSize} bytes, got {BaseBytes.Length}.");
        }
        var ipBytes = Encoding.ASCII.GetBytes(IpAddress);
        if (ipBytes.Length >= IpFieldBytes)
        {
            throw new InvalidOperationException(
                $"IpAddress must fit in {IpFieldBytes - 1} bytes plus a NUL; got {ipBytes.Length}.");
        }
        var output = new byte[PayloadSize];
        Array.Copy(BaseBytes, output, PayloadSize);

        for (var i = 0; i < 8; i++)
        {
            output[CharUidOffset + i] = (byte)((SelectedCharUid >> (i * 8)) & 0xFF);
        }
        Array.Clear(output, IpOffset, IpFieldBytes);
        Array.Copy(ipBytes, 0, output, IpOffset, ipBytes.Length);
        WriteUInt32Le(output, PortOffset, LoginPort);
        WriteUInt32Le(output, SessionTokenOffset, SessionToken);
        return output;
    }

    public static SessionParam1Packet DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"SessionParam1 payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        ulong uid = 0;
        for (var i = 0; i < 8; i++) uid |= (ulong)payload[CharUidOffset + i] << (i * 8);
        var ipSpan = payload.Slice(IpOffset, IpFieldBytes);
        var nul = ipSpan.IndexOf((byte)0);
        var ip = Encoding.ASCII.GetString(nul < 0 ? ipSpan : ipSpan[..nul]);
        var port = ReadUInt32Le(payload, PortOffset);
        var trailing = ReadUInt32Le(payload, SessionTokenOffset);
        return new SessionParam1Packet
        {
            SelectedCharUid = uid,
            IpAddress = ip,
            LoginPort = port,
            SessionToken = trailing,
            BaseBytes = payload.ToArray(),
        };
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static uint ReadUInt32Le(ReadOnlySpan<byte> buf, int offset) =>
        (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
}
