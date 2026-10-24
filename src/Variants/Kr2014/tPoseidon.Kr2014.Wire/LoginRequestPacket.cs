using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net;

namespace tPoseidon.Kr2014.Wire;

public sealed class LoginRequestPacket
{
    public const int PayloadSize = 46;
    public const int UsernameFieldBytes = 19;
    public const int PasswordFieldBytes = 19;
    public const int TrailingReservedBytes = 8;

        public string Username { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;

        public byte[] TrailingReserved { get; init; } = new byte[TrailingReservedBytes];

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Username);
        ArgumentNullException.ThrowIfNull(Password);
        ArgumentNullException.ThrowIfNull(TrailingReserved);
        if (TrailingReserved.Length != TrailingReservedBytes)
        {
            throw new InvalidOperationException(
                $"TrailingReserved must be exactly {TrailingReservedBytes} bytes, got {TrailingReserved.Length}.");
        }

        var userBytes = Encoding.ASCII.GetBytes(Username);
        if (userBytes.Length > UsernameFieldBytes)
        {
            throw new InvalidOperationException(
                $"Username is {userBytes.Length} bytes; max is {UsernameFieldBytes}.");
        }
        var passBytes = Encoding.ASCII.GetBytes(Password);
        if (passBytes.Length > PasswordFieldBytes)
        {
            throw new InvalidOperationException(
                $"Password is {passBytes.Length} bytes; max is {PasswordFieldBytes}.");
        }

        var w = new PacketWriter(PayloadSize);
        w.WriteBytes(userBytes);
        w.WriteZero(UsernameFieldBytes - userBytes.Length);
        w.WriteBytes(passBytes);
        w.WriteZero(PasswordFieldBytes - passBytes.Length);
        w.WriteBytes(TrailingReserved);
        return w.ToArray();
    }

    public static LoginRequestPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Kr2014 LoginRequest payload must be {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var username = r.ReadFixedString(UsernameFieldBytes);
        var password = r.ReadFixedString(PasswordFieldBytes);
        var trailing = payload.Slice(UsernameFieldBytes + PasswordFieldBytes, TrailingReservedBytes).ToArray();
        return new LoginRequestPacket
        {
            Username = username,
            Password = password,
            TrailingReserved = trailing,
        };
    }
}
