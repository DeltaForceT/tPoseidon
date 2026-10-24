using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public enum QuestStoreSubOp : byte
{
    AddActive = 0x01,
    GetActiveForCharacter = 0x02,
    FindActive = 0x03,
    CountActiveOfType = 0x04,
    UpdateMonsterProgress = 0x05,
    RemoveActive = 0x06,

    HistoryFind = 0x10,
    HistoryInsert = 0x11,
    HistoryUpdate = 0x12,
    HistoryGetForCharacter = 0x13,
    HistoryAppendLog = 0x14,
    HistoryCompletedToday = 0x15,
    HistoryHasCompletedAtLeast = 0x16,
}

public static class QuestStoreWire
{
    public static byte[] EncodeQuest(Quest quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var writer = new PacketWriter(40);
        WriteQuest(writer, quest);
        return writer.ToArray();
    }

    public static byte[] EncodeQuestHistory(QuestHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var writer = new PacketWriter(40);
        WriteQuestHistory(writer, history);
        return writer.ToArray();
    }

    public static byte[] EncodeQuestList(IReadOnlyList<Quest> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count > ushort.MaxValue)
        {
            throw new InvalidPacketException($"quest list has {list.Count} entries; exceeds u16 count max.");
        }
        var writer = new PacketWriter(2 + (list.Count * 40));
        writer.WriteUInt16((ushort)list.Count);
        foreach (var q in list)
        {
            WriteQuest(writer, q);
        }
        return writer.ToArray();
    }

    public static byte[] EncodeQuestHistoryList(IReadOnlyList<QuestHistory> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count > ushort.MaxValue)
        {
            throw new InvalidPacketException($"quest-history list has {list.Count} entries; exceeds u16 count max.");
        }
        var writer = new PacketWriter(2 + (list.Count * 40));
        writer.WriteUInt16((ushort)list.Count);
        foreach (var h in list)
        {
            WriteQuestHistory(writer, h);
        }
        return writer.ToArray();
    }

    public static Quest DecodeQuest(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return ReadQuest(ref reader);
    }

    public static QuestHistory DecodeQuestHistory(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return ReadQuestHistory(ref reader);
    }

    public static IReadOnlyList<Quest> DecodeQuestList(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var count = reader.ReadUInt16();
        var list = new List<Quest>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(ReadQuest(ref reader));
        }
        return list;
    }

    public static IReadOnlyList<QuestHistory> DecodeQuestHistoryList(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var count = reader.ReadUInt16();
        var list = new List<QuestHistory>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(ReadQuestHistory(ref reader));
        }
        return list;
    }

    private static void WriteQuest(PacketWriter writer, Quest q)
    {
        writer.WriteUInt64(q.CharacterId);
        writer.WriteByte(q.QuestType);
        writer.WriteInt32(q.QuestId);
        writer.WriteByte(q.CompletionCount);
        writer.WriteInt32(q.MonsterId);
        writer.WriteInt16(q.KillCount);
        writer.WriteInt32(q.RecordTime);
        writer.WriteInt64(q.AcceptedAt.UtcTicks);
    }

    private static Quest ReadQuest(ref PacketReader reader) => new()
    {
        CharacterId = reader.ReadUInt64(),
        QuestType = reader.ReadByte(),
        QuestId = reader.ReadInt32(),
        CompletionCount = reader.ReadByte(),
        MonsterId = reader.ReadInt32(),
        KillCount = reader.ReadInt16(),
        RecordTime = reader.ReadInt32(),
        AcceptedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
    };

    private static void WriteQuestHistory(PacketWriter writer, QuestHistory h)
    {
        writer.WriteUInt64(h.CharacterId);
        writer.WriteByte(h.QuestType);
        writer.WriteInt32(h.QuestId);
        writer.WriteByte(h.CompletionCount);
        writer.WriteInt32(h.MonsterId);
        writer.WriteInt16(h.KillCount);
        writer.WriteInt32(h.RecordTime);
        writer.WriteInt64(h.CompletedAt.UtcTicks);
    }

    private static QuestHistory ReadQuestHistory(ref PacketReader reader) => new()
    {
        CharacterId = reader.ReadUInt64(),
        QuestType = reader.ReadByte(),
        QuestId = reader.ReadInt32(),
        CompletionCount = reader.ReadByte(),
        MonsterId = reader.ReadInt32(),
        KillCount = reader.ReadInt16(),
        RecordTime = reader.ReadInt32(),
        CompletedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
    };
}
