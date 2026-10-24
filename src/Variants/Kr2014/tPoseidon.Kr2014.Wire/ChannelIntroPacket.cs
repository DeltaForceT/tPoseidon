using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Kr2014.Wire;

public sealed class ChannelIntroPacket
{
    public const int TokenFieldBytes = 12;
    public const int FixedTrailerBytes = ClientVersion.WireBytes + TokenFieldBytes + 1;

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public ClientVersion Version { get; init; }
    public byte[] LoginResultToken { get; init; } = Array.Empty<byte>();

        public byte TrailingReserved { get; init; }

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Username);
        ArgumentNullException.ThrowIfNull(Password);
        ArgumentNullException.ThrowIfNull(LoginResultToken);
        if (LoginResultToken.Length != TokenFieldBytes)
        {
            throw new InvalidOperationException(
                $"LoginResultToken must be {TokenFieldBytes} bytes, got {LoginResultToken.Length}.");
        }
        var userBytes = Encoding.ASCII.GetBytes(Username);
        var passBytes = Encoding.ASCII.GetBytes(Password);

        var totalSize = userBytes.Length + 1 + passBytes.Length + 1 + FixedTrailerBytes;
        var w = new PacketWriter(totalSize);
        w.WriteBytes(userBytes);
        w.WriteByte(0);
        w.WriteBytes(passBytes);
        w.WriteByte(0);
        w.WriteBytes(Version.ToWire());
        w.WriteBytes(LoginResultToken);
        w.WriteByte(TrailingReserved);
        return w.ToArray();
    }

    public static ChannelIntroPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2 + FixedTrailerBytes)
        {
            throw new InvalidPacketException(
                $"Kr2014 ChannelIntro payload must be at least {2 + FixedTrailerBytes} bytes, got {payload.Length}.");
        }

        var nul1 = payload.IndexOf((byte)0);
        if (nul1 < 0)
        {
            throw new InvalidPacketException("Kr2014 ChannelIntro: no NUL terminator found for username.");
        }
        var afterUser = payload[(nul1 + 1)..];
        var nul2 = afterUser.IndexOf((byte)0);
        if (nul2 < 0)
        {
            throw new InvalidPacketException("Kr2014 ChannelIntro: no NUL terminator found for password.");
        }
        var afterPass = afterUser[(nul2 + 1)..];
        if (afterPass.Length != FixedTrailerBytes)
        {
            throw new InvalidPacketException(
                $"Kr2014 ChannelIntro: expected exactly {FixedTrailerBytes} bytes after password NUL, got {afterPass.Length}.");
        }

        var username = Encoding.ASCII.GetString(payload[..nul1]);
        var password = Encoding.ASCII.GetString(afterUser[..nul2]);
        var version = ClientVersion.FromWire(afterPass[..ClientVersion.WireBytes]);
        var token = afterPass.Slice(ClientVersion.WireBytes, TokenFieldBytes).ToArray();
        var trailingByte = afterPass[ClientVersion.WireBytes + TokenFieldBytes];

        return new ChannelIntroPacket
        {
            Username = username,
            Password = password,
            Version = version,
            LoginResultToken = token,
            TrailingReserved = trailingByte,
        };
    }
}
