using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class CharacterListPacket
{
        public sealed class Entry
    {
        public required ReadOnlyMemory<byte> Raw { get; init; }
        public required string Name { get; init; }
    }

        public const int EntryWidth = 156;

        public const int TrailerLength = 12;

    public IReadOnlyList<Entry> Entries { get; init; } = Array.Empty<Entry>();

        public byte[] Trailer { get; init; } = TrailerWithSlotQuota(8);

        public static byte[] TrailerWithSlotQuota(byte slotQuota)
    {
        var trailer = new byte[TrailerLength];
        trailer[11] = slotQuota;
        return trailer;
    }

    public byte[] EncodePayload()
    {
        if (Entries.Count > byte.MaxValue)
        {
            throw new InvalidPacketException(
                $"CharacterList supports max {byte.MaxValue} entries, got {Entries.Count}.");
        }
        if (Trailer.Length != TrailerLength)
        {
            throw new InvalidPacketException(
                $"CharacterList trailer must be {TrailerLength} bytes, got {Trailer.Length}.");
        }

        var total = 1 + (Entries.Count * EntryWidth) + TrailerLength;
        var w = new PacketWriter(total);
        w.WriteByte((byte)Entries.Count);
        foreach (var entry in Entries)
        {
            if (entry.Raw.Length != EntryWidth)
            {
                throw new InvalidPacketException(
                    $"Entry width mismatch: expected {EntryWidth} but entry has {entry.Raw.Length} bytes.");
            }
            w.WriteBytes(entry.Raw.Span);
        }
        w.WriteBytes(Trailer);
        return w.ToArray();
    }

        public static CharacterListPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 1)
        {
            throw new InvalidPacketException("CharacterList payload empty.");
        }

        var count = payload[0];
        var expectedLength = 1 + (count * EntryWidth) + TrailerLength;
        if (payload.Length != expectedLength)
        {
            throw new InvalidPacketException(
                $"CharacterList payload length mismatch: count={count} expected {expectedLength} bytes, got {payload.Length}.");
        }

        var entries = new Entry[count];
        var off = 1;
        for (var i = 0; i < count; i++)
        {
            var raw = payload.Slice(off, EntryWidth).ToArray();
            entries[i] = new Entry
            {
                Raw = raw,
                Name = ExtractName(raw),
            };
            off += EntryWidth;
        }
        var trailer = payload.Slice(off, TrailerLength).ToArray();

        return new CharacterListPacket
        {
            Entries = entries,
            Trailer = trailer,
        };
    }

        private static string ExtractName(ReadOnlySpan<byte> raw)
    {
        const int NameOffset = 0x09;
        if (raw.Length <= NameOffset) return string.Empty;
        var nameRegion = raw[NameOffset..];
        var nul = nameRegion.IndexOf((byte)0);
        var len = nul < 0 ? nameRegion.Length : nul;
        if (len == 0) return string.Empty;
        return Encoding.ASCII.GetString(nameRegion[..len]);
    }
}
