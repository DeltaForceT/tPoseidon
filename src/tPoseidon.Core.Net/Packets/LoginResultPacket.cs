using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.LOGIN_RESULT)]
public sealed class LoginResultPacket
{
    public const int PayloadSize = 88;
    public const int AccountCodeOffset = 0;
    public const int AuthcodeOffset = 4;
    public const int AuthcodeBytes = 8;
    public const int ServerNameOffset = 18;
    public const int ChannelNameOffset = 51;
    public const int NameFieldBytes = 32;

        public static readonly LoginResultPacket SnapshotDefault = new()
    {
        Bytes = new byte[]
        {
            0xC1, 0x8B, 0x1E, 0x06, 0x68, 0xF4, 0x25, 0xED,
            0x9C, 0xD1, 0x4E, 0x02, 0x01, 0x01, 0x01, 0x00,
            0x01, 0x00, 0x53, 0x4F, 0x4C, 0x41, 0x52, 0x49,
            0x53, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x43, 0x61, 0x72, 0x6C, 0x69,
            0x74, 0x6F, 0x73, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x08, 0x07, 0x01, 0x00,
        },
    };

        public byte[] Bytes { get; init; } = Array.Empty<byte>();

        public static LoginResultPacket Create(
        uint accountCode, long authcode, string worldName, string channelName)
    {
        ArgumentNullException.ThrowIfNull(worldName);
        ArgumentNullException.ThrowIfNull(channelName);

        var bytes = new byte[PayloadSize];
        Array.Copy(SnapshotDefault.Bytes, bytes, PayloadSize);

        bytes[AccountCodeOffset + 0] = (byte)(accountCode & 0xFF);
        bytes[AccountCodeOffset + 1] = (byte)((accountCode >> 8) & 0xFF);
        bytes[AccountCodeOffset + 2] = (byte)((accountCode >> 16) & 0xFF);
        bytes[AccountCodeOffset + 3] = (byte)((accountCode >> 24) & 0xFF);

        for (var i = 0; i < AuthcodeBytes; i++)
        {
            bytes[AuthcodeOffset + i] = (byte)((authcode >> (i * 8)) & 0xFF);
        }

        WriteFixedAscii(bytes, ServerNameOffset, NameFieldBytes, worldName);
        WriteFixedAscii(bytes, ChannelNameOffset, NameFieldBytes, channelName);

        return new LoginResultPacket { Bytes = bytes };
    }

    private static void WriteFixedAscii(byte[] dest, int offset, int fieldBytes, string value)
    {


        var encoded = Encoding.ASCII.GetBytes(value);
        var copyLen = Math.Min(encoded.Length, fieldBytes);


        Array.Clear(dest, offset, fieldBytes);
        if (copyLen > 0)
        {
            Array.Copy(encoded, 0, dest, offset, copyLen);
        }
    }

    public byte[] EncodePayload()
    {
        if (Bytes.Length != PayloadSize)
        {
            throw new InvalidOperationException(
                $"LoginResult.Bytes must be exactly {PayloadSize} bytes, got {Bytes.Length}.");
        }
        var copy = new byte[PayloadSize];
        Array.Copy(Bytes, copy, PayloadSize);
        return copy;
    }

    public static LoginResultPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"LoginResult payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new LoginResultPacket { Bytes = payload.ToArray() };
    }
}
