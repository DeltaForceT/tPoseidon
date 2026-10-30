using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.SESSION_PARAM_1)]
public sealed class SessionParam1Packet
{
    public const int CharUidOffset = 0;
    public const int IpOffset = 8;
    public const int MinimumPayloadSize = IpOffset + 1 + sizeof(uint) + sizeof(uint);

    public ulong SelectedCharUid { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public uint LoginPort { get; init; }
    public uint SessionToken { get; init; }

    public byte[] EncodePayload()
    {
        var ipBytes = Encoding.ASCII.GetBytes(IpAddress);
        if (ipBytes.Contains((byte)0))
        {
            throw new InvalidOperationException("IpAddress must not contain a NUL.");
        }
        var output = new byte[MinimumPayloadSize + ipBytes.Length];
        WriteUInt64Le(output, CharUidOffset, SelectedCharUid);
        Array.Copy(ipBytes, 0, output, IpOffset, ipBytes.Length);
        var portOffset = IpOffset + ipBytes.Length + 1;
        WriteUInt32Le(output, portOffset, LoginPort);
        WriteUInt32Le(output, portOffset + sizeof(uint), SessionToken);
        return output;
    }

    public static SessionParam1Packet DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < MinimumPayloadSize)
        {
            throw new InvalidPacketException(
                $"SessionParam1 payload must be at least {MinimumPayloadSize} bytes, got {payload.Length}.");
        }
        var nul = payload[IpOffset..].IndexOf((byte)0);
        if (nul < 0 || payload.Length < IpOffset + nul + 1 + sizeof(uint) + sizeof(uint))
        {
            throw new InvalidPacketException(
                $"SessionParam1 payload of {payload.Length} bytes is not a NUL-terminated host followed by port and token.");
        }
        var portOffset = IpOffset + nul + 1;
        return new SessionParam1Packet
        {
            SelectedCharUid = ReadUInt64Le(payload, CharUidOffset),
            IpAddress = Encoding.ASCII.GetString(payload.Slice(IpOffset, nul)),
            LoginPort = ReadUInt32Le(payload, portOffset),
            SessionToken = ReadUInt32Le(payload, portOffset + sizeof(uint)),
        };
    }

    private static void WriteUInt64Le(byte[] buf, int offset, ulong value)
    {
        for (var i = 0; i < sizeof(ulong); i++) buf[offset + i] = (byte)((value >> (i * 8)) & 0xFF);
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        for (var i = 0; i < sizeof(uint); i++) buf[offset + i] = (byte)((value >> (i * 8)) & 0xFF);
    }

    private static ulong ReadUInt64Le(ReadOnlySpan<byte> buf, int offset)
    {
        ulong value = 0;
        for (var i = 0; i < sizeof(ulong); i++) value |= (ulong)buf[offset + i] << (i * 8);
        return value;
    }

    private static uint ReadUInt32Le(ReadOnlySpan<byte> buf, int offset) =>
        (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
}
