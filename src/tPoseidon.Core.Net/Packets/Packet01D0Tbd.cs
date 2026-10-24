using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet01D0Tbd
{
    public ushort EntityId { get; init; }

    public string Speech { get; init; } = string.Empty;

    public byte[] EncodePayload() => EncodePayload(Encoding.UTF8);

    public byte[] EncodePayload(Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(textEncoding);
        ArgumentNullException.ThrowIfNull(Speech);

        var speechBytes = textEncoding.GetBytes(Speech);
        var total = 2 + speechBytes.Length + 1;

        var w = new PacketWriter(total);
        w.WriteUInt16(EntityId);
        w.WriteBytes(speechBytes);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static Packet01D0Tbd DecodePayload(ReadOnlySpan<byte> payload)
        => DecodePayload(payload, Encoding.UTF8);

    public static Packet01D0Tbd DecodePayload(ReadOnlySpan<byte> payload, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(textEncoding);

        const int FixedPrefixSize = 2;
        if (payload.Length < FixedPrefixSize + 1)
        {
            throw new InvalidPacketException(
                $"Packet01D0Tbd payload {payload.Length} bytes is below {FixedPrefixSize + 1}-byte minimum.");
        }

        var r = new PacketReader(payload);
        var entityId = r.ReadUInt16();

        var rest = payload[FixedPrefixSize..];
        var nul = rest.IndexOf((byte)0);
        if (nul < 0)
        {
            throw new InvalidPacketException(
                "Packet01D0Tbd payload has no NUL terminator after speech field.");
        }

        return new Packet01D0Tbd
        {
            EntityId = entityId,
            Speech = textEncoding.GetString(rest[..nul]),
        };
    }
}
