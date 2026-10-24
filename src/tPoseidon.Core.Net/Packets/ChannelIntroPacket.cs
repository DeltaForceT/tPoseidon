using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class ChannelIntroPacket
{
    public const int NonceFieldBytes = 4;
    public const int TokenFieldBytes = 12;
    public const int FixedTrailerBytes = NonceFieldBytes + TokenFieldBytes;
    public const int FixedTrailerBytesKr2014 = FixedTrailerBytes + 1;

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public byte[] Nonce { get; init; } = Array.Empty<byte>();
    public byte[] LoginResultToken { get; init; } = Array.Empty<byte>();

        public byte[] ExtraTrailerBytes { get; init; } = Array.Empty<byte>();

        public static readonly ChannelIntroPacket SnapshotDefault = new()
    {
        Username = "tester",
        Password = "tester",
        Nonce = new byte[] { 0x21, 0x56, 0x5B, 0x56 },
        LoginResultToken = new byte[]
        {
            0xC1, 0x8B, 0x1E, 0x06, 0x68, 0xF4,
            0x25, 0xED, 0x9C, 0xD1, 0x4E, 0x02,
        },
    };

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Username);
        ArgumentNullException.ThrowIfNull(Password);
        ArgumentNullException.ThrowIfNull(Nonce);
        ArgumentNullException.ThrowIfNull(LoginResultToken);
        ArgumentNullException.ThrowIfNull(ExtraTrailerBytes);
        if (Nonce.Length != NonceFieldBytes)
        {
            throw new InvalidOperationException(
                $"Nonce must be {NonceFieldBytes} bytes, got {Nonce.Length}.");
        }
        if (LoginResultToken.Length != TokenFieldBytes)
        {
            throw new InvalidOperationException(
                $"LoginResultToken must be {TokenFieldBytes} bytes, got {LoginResultToken.Length}.");
        }
        var userBytes = Encoding.ASCII.GetBytes(Username);
        var passBytes = Encoding.ASCII.GetBytes(Password);

        var totalSize = userBytes.Length + 1 + passBytes.Length + 1
                      + FixedTrailerBytes + ExtraTrailerBytes.Length;
        var w = new PacketWriter(totalSize);
        w.WriteBytes(userBytes);
        w.WriteByte(0);
        w.WriteBytes(passBytes);
        w.WriteByte(0);
        w.WriteBytes(Nonce);
        w.WriteBytes(LoginResultToken);
        if (ExtraTrailerBytes.Length > 0)
        {
            w.WriteBytes(ExtraTrailerBytes);
        }
        return w.ToArray();
    }

    public static ChannelIntroPacket DecodePayload(ReadOnlySpan<byte> payload)
    {

        if (payload.Length < 2 + FixedTrailerBytes)
        {
            throw new InvalidPacketException(
                $"ChannelIntro payload must be at least {2 + FixedTrailerBytes} bytes, got {payload.Length}.");
        }

        var nul1 = payload.IndexOf((byte)0);
        if (nul1 < 0)
        {
            throw new InvalidPacketException("ChannelIntro: no NUL terminator found for username.");
        }
        var afterUser = payload[(nul1 + 1)..];
        var nul2 = afterUser.IndexOf((byte)0);
        if (nul2 < 0)
        {
            throw new InvalidPacketException("ChannelIntro: no NUL terminator found for password.");
        }
        var afterPass = afterUser[(nul2 + 1)..];
        if (afterPass.Length != FixedTrailerBytes && afterPass.Length != FixedTrailerBytesKr2014)
        {
            throw new InvalidPacketException(
                $"ChannelIntro: expected {FixedTrailerBytes} (2010 EN) or {FixedTrailerBytesKr2014} (2014 KR) bytes after password NUL, got {afterPass.Length}.");
        }

        var username = Encoding.ASCII.GetString(payload[..nul1]);
        var password = Encoding.ASCII.GetString(afterUser[..nul2]);
        var nonce = afterPass[..NonceFieldBytes].ToArray();
        var token = afterPass.Slice(NonceFieldBytes, TokenFieldBytes).ToArray();
        var extraLen = afterPass.Length - FixedTrailerBytes;
        var extra = extraLen > 0 ? afterPass.Slice(FixedTrailerBytes, extraLen).ToArray() : Array.Empty<byte>();

        return new ChannelIntroPacket
        {
            Username = username,
            Password = password,
            Nonce = nonce,
            LoginResultToken = token,
            ExtraTrailerBytes = extra,
        };
    }
}
