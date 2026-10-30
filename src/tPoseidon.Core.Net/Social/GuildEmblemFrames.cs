using System.Buffers.Binary;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public static class GuildEmblemFrames
{
    public const ushort Register = 0x2C3E;
    public const ushort Registered = 0x2C3F;
    public const ushort Refused = 0x2C40;
    public const ushort Emblem = 0x2C41;
    public const ushort Count = 0x2C42;

    public const int RegisterRequestLength = 10;
    public const int EmblemLength = 7;
    public const byte NoTitle = 0;

    public static GuildEmblemRequest? ReadRegister(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < RegisterRequestLength) return null;
        return new GuildEmblemRequest(
            payload[0],
            payload[1],
            BinaryPrimitives.ReadUInt64LittleEndian(payload[2..]));
    }

    public static byte[] RefusedFor(int code) => [(byte)code];

    public static byte[] EmblemFor(GuildEmblemState state)
    {
        var body = new byte[EmblemLength];
        body[0] = state.Emblem.Template;
        body[1] = state.Emblem.Colour;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(2), state.Emblem.Icon);
        body[6] = NoTitle;
        return body;
    }

    public static byte[] CountFor(uint times)
    {
        var body = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(body, times);
        return body;
    }
}
