using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_SKILL_LIST)]
public sealed class GameSkillListPacket
{
    public sealed class Entry
    {
        public required ushort SkillId { get; init; }
        public required byte Level { get; init; }
        public byte UnknownByte3 { get; init; } = 0x0F;
        public byte UnknownByte4 { get; init; } = 0xA4;
    }

    public const int EntrySize = 5;
    public const int HeaderSize = 2;

    public IReadOnlyList<Entry> Entries { get; init; } = Array.Empty<Entry>();

    public byte[] EncodePayload()
    {
        if (Entries.Count > ushort.MaxValue)
        {
            throw new InvalidPacketException(
                $"GameSkillList max entries is {ushort.MaxValue}, got {Entries.Count}.");
        }

        var w = new PacketWriter(HeaderSize + Entries.Count * EntrySize);
        w.WriteUInt16((ushort)Entries.Count);
        foreach (var entry in Entries)
        {
            w.WriteUInt16(entry.SkillId);
            w.WriteByte(entry.Level);
            w.WriteByte(entry.UnknownByte3);
            w.WriteByte(entry.UnknownByte4);
        }
        return w.ToArray();
    }

    public static GameSkillListPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderSize)
        {
            throw new InvalidPacketException(
                $"GameSkillList payload too short: {payload.Length} bytes, need at least {HeaderSize}.");
        }

        var r = new PacketReader(payload);
        var count = r.ReadUInt16();

        var expected = HeaderSize + count * EntrySize;
        if (payload.Length != expected)
        {
            throw new InvalidPacketException(
                $"GameSkillList payload {payload.Length} bytes does not match " +
                $"count={count} (expected {expected} bytes).");
        }

        var entries = new Entry[count];
        for (var i = 0; i < count; i++)
        {
            entries[i] = new Entry
            {
                SkillId = r.ReadUInt16(),
                Level = r.ReadByte(),
                UnknownByte3 = r.ReadByte(),
                UnknownByte4 = r.ReadByte(),
            };
        }
        return new GameSkillListPacket { Entries = entries };
    }
}
