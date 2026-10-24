using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class LoginRequestPacket
{
        public const int PayloadSize = 38;

        public const int PayloadSizeKr2014 = 46;

    public const int UsernameFieldBytes = 19;
    public const int PasswordFieldBytes = 19;

        public string Username { get; init; } = string.Empty;

        public string Password { get; init; } = string.Empty;

        public byte[] ExtraBytes { get; init; } = Array.Empty<byte>();

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Username);
        ArgumentNullException.ThrowIfNull(Password);
        ArgumentNullException.ThrowIfNull(ExtraBytes);

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

        var totalSize = PayloadSize + ExtraBytes.Length;
        var w = new PacketWriter(totalSize);
        w.WriteBytes(userBytes);
        w.WriteZero(UsernameFieldBytes - userBytes.Length);
        w.WriteBytes(passBytes);
        w.WriteZero(PasswordFieldBytes - passBytes.Length);
        if (ExtraBytes.Length > 0)
        {
            w.WriteBytes(ExtraBytes);
        }
        return w.ToArray();
    }

    public static LoginRequestPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize && payload.Length != PayloadSizeKr2014)
        {
            throw new InvalidPacketException(
                $"LoginRequest payload must be {PayloadSize} (2010 EN) or {PayloadSizeKr2014} (2014 KR) bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var username = r.ReadFixedString(UsernameFieldBytes);
        var password = r.ReadFixedString(PasswordFieldBytes);
        var extraLen = payload.Length - PayloadSize;
        var extra = extraLen > 0 ? payload.Slice(PayloadSize, extraLen).ToArray() : Array.Empty<byte>();
        return new LoginRequestPacket
        {
            Username = username,
            Password = password,
            ExtraBytes = extra,
        };
    }
}
