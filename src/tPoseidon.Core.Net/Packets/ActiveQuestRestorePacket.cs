using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_ACTIVE_QUEST_RESTORE)]
public sealed class ActiveQuestRestorePacket
{
    public sealed class Entry
    {
        public required ushort QuestId { get; init; }

                public uint Trailer { get; init; } = 0x00010000u;
    }

    public IReadOnlyList<Entry> Entries { get; init; } = Array.Empty<Entry>();

    public static readonly ActiveQuestRestorePacket Empty = new();

    public byte[] EncodePayload()
    {
        var count = Entries.Count;
        if (count > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"ActiveQuestRestore supports max {ushort.MaxValue} entries, got {count}.");
        }
        var buf = new byte[2 + count * 6];
        buf[0] = (byte)(count & 0xFF);
        buf[1] = (byte)((count >> 8) & 0xFF);
        for (var i = 0; i < count; i++)
        {
            var off = 2 + i * 6;
            var e = Entries[i];
            buf[off + 0] = (byte)(e.QuestId & 0xFF);
            buf[off + 1] = (byte)((e.QuestId >> 8) & 0xFF);
            buf[off + 2] = (byte)(e.Trailer & 0xFF);
            buf[off + 3] = (byte)((e.Trailer >> 8) & 0xFF);
            buf[off + 4] = (byte)((e.Trailer >> 16) & 0xFF);
            buf[off + 5] = (byte)((e.Trailer >> 24) & 0xFF);
        }
        return buf;
    }

    public static ActiveQuestRestorePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
        {
            throw new InvalidPacketException(
                $"ActiveQuestRestore payload must be at least 2 bytes (count), got {payload.Length}.");
        }
        var count = (ushort)(payload[0] | (payload[1] << 8));
        var expected = 2 + count * 6;
        if (payload.Length != expected)
        {
            throw new InvalidPacketException(
                $"ActiveQuestRestore payload {payload.Length}B != expected {expected}B for count={count}.");
        }
        var entries = new Entry[count];
        for (var i = 0; i < count; i++)
        {
            var off = 2 + i * 6;
            entries[i] = new Entry
            {
                QuestId = (ushort)(payload[off] | (payload[off + 1] << 8)),
                Trailer = (uint)(payload[off + 2]
                              | (payload[off + 3] << 8)
                              | (payload[off + 4] << 16)
                              | (payload[off + 5] << 24)),
            };
        }
        return new ActiveQuestRestorePacket { Entries = entries };
    }
}
