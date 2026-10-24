using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet011DTbd
{
    public const int PayloadSize = 5;

    public const byte ToggleCastInit = 0x00;
    public const byte ToggleCastEnd = 0x01;

    public required byte Toggle { get; init; }
    public required uint CombatSequence { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = Toggle;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(1, 4), CombatSequence);
        return buf;
    }

    public static Packet011DTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet011DTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet011DTbd
        {
            Toggle = payload[0],
            CombatSequence = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..5]),
        };
    }
}
