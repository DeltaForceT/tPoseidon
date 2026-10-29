using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public readonly record struct QuestListEntry(int QuestId, int Count);

public readonly record struct MonsterQuestListEntry(int QuestId, int Monster, int Count, int Grade, int Kills, int ClearTimeMs);

public readonly record struct QuestTexts(string Name, string Client, string Pos, string Comment);

public readonly record struct NpcMessagePage(ushort NpcEntityId, ushort MessageIndex, string Text, ushort Kind, ushort QuestId);

public static class QuestFrames
{
    public const ushort JobQuestAdded = 0x0126;
    public const ushort JobQuestRemoved = 0x0127;
    public const ushort JobQuestGiveUp = 0x012A;
    public const ushort HistoryRequest = 0x02F0;
    public const ushort HistoryList = 0x02F1;
    public const ushort HistoryAlreadySent = 0x02F2;
    public const ushort NormalHistoryAdded = 0x0087;
    public const ushort NormalHistoryCounted = 0x0088;
    public const ushort MonsterHistoryAdded = 0x00D5;
    public const ushort MonsterHistoryCounted = 0x00D6;
    public const ushort ScenarioHistoryAdded = 0x0342;
    public const ushort ScenarioHistoryCounted = 0x0343;
    public const ushort PartyHistoryAdded = 0x036A;
    public const ushort PartyHistoryCounted = 0x036B;
    public const byte HistoryOfNormal = 1;
    public const byte HistoryOfMonster = 2;
    public const byte HistoryOfScenario = 8;
    public const byte HistoryOfParty = 9;
    public const int FirstCompletionRow = 2;
    public const ushort ReturnWarpRequest = 0x0410;
    public const ushort ReturnWarpAccepted = 0x0411;
    public const ushort ReturnWarpRefused = 0x0412;
    public const uint ReturnFromMonsterQuest = 2;
    public const uint ReturnFromPartyQuest = 9;
    public const int LastReturnMap = 1000;

    public static byte[] QuestAdded(int questId, int count, QuestTexts texts)
    {
        var w = new PacketWriter(64);
        w.WriteUInt32((uint)questId);
        w.WriteUInt16((ushort)count);
        WriteTexts(w, texts);
        w.WriteUInt16((ushort)questId);
        return w.ToArray();
    }

    public static byte[] ScenarioQuestAdded(int questId, int count, QuestTexts texts)
    {
        var w = new PacketWriter(64);
        w.WriteUInt32((uint)questId);
        w.WriteUInt16((ushort)count);
        WriteTexts(w, texts);
        return w.ToArray();
    }

    public static byte[] HistoryListFor(byte type, IReadOnlyList<tPoseidon.Core.Data.QuestHistory> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var w = new PacketWriter(3 + rows.Count * 11);
        w.WriteByte(type);
        w.WriteUInt16((ushort)rows.Count);
        foreach (var row in rows)
        {
            w.WriteUInt16((ushort)row.QuestId);
            if (type == HistoryOfMonster)
            {
                w.WriteUInt16((ushort)row.MonsterId);
                w.WriteByte(row.CompletionCount);
                w.WriteUInt16((ushort)row.KillCount);
                w.WriteUInt32((uint)row.RecordTime);
            }
            else
            {
                w.WriteByte(row.CompletionCount);
            }
        }
        return w.ToArray();
    }

    public static byte[] HistoryCounted(int questId, int count)
    {
        var w = new PacketWriter(6);
        w.WriteUInt32((uint)questId);
        w.WriteUInt16((ushort)count);
        return w.ToArray();
    }

    public static byte[] PartyHistoryFor(uint questId, int count)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32(questId);
        w.WriteUInt32((uint)count);
        return w.ToArray();
    }

    public static byte[] MonsterHistoryCountedFor(MonsterQuestListEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var w = new PacketWriter(12);
        w.WriteUInt16((ushort)entry.QuestId);
        w.WriteUInt16((ushort)entry.Monster);
        w.WriteByte((byte)entry.Grade);
        w.WriteByte((byte)entry.Count);
        w.WriteUInt16((ushort)entry.Kills);
        w.WriteUInt32((uint)entry.ClearTimeMs);
        return w.ToArray();
    }

    public static byte[] QuestRemoved(int questId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32((uint)questId);
        return w.ToArray();
    }

    public static byte[] MonsterQuestAdded(MonsterQuestListEntry entry, QuestTexts texts)
    {
        var w = new PacketWriter(64);
        w.WriteUInt16((ushort)entry.QuestId);
        w.WriteUInt16((ushort)entry.Monster);
        w.WriteByte((byte)entry.Grade);
        w.WriteByte((byte)entry.Count);
        w.WriteUInt16((ushort)entry.Kills);
        w.WriteUInt32((uint)entry.ClearTimeMs);
        WriteTexts(w, texts);
        return w.ToArray();
    }

    public static byte[] MonsterQuestRemoved(int questId) => QuestIdOnly(questId);

    public static byte[] MonsterQuestTimedOut(int questId) => QuestIdOnly(questId);

    public static byte[] MonsterQuestTimer(int remainingMs, int limitMs)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32((uint)remainingMs);
        w.WriteUInt32((uint)limitMs);
        return w.ToArray();
    }

    public static byte[] MonsterQuestKills(int kills, int target)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16((ushort)kills);
        w.WriteUInt16((ushort)target);
        return w.ToArray();
    }

    public static byte[] MonsterQuestSucceeded(int questId, bool offersReturn)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16((ushort)questId);
        w.WriteByte(offersReturn ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] QuestList(IReadOnlyList<QuestListEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var w = new PacketWriter(2 + entries.Count * 6);
        w.WriteUInt16((ushort)entries.Count);
        foreach (var entry in entries)
        {
            w.WriteUInt32((uint)entry.QuestId);
            w.WriteUInt16((ushort)entry.Count);
        }
        return w.ToArray();
    }

    public static byte[] MonsterQuestList(IReadOnlyList<MonsterQuestListEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var w = new PacketWriter(2 + entries.Count * 12);
        w.WriteUInt16((ushort)entries.Count);
        foreach (var entry in entries)
        {
            w.WriteUInt16((ushort)entry.QuestId);
            w.WriteUInt16((ushort)entry.Monster);
            w.WriteByte((byte)entry.Count);
            w.WriteByte((byte)entry.Grade);
            w.WriteUInt16((ushort)entry.Kills);
            w.WriteUInt32((uint)entry.ClearTimeMs);
        }
        return w.ToArray();
    }

    public static byte[] QuestIdList(IReadOnlyList<int> questIds)
    {
        ArgumentNullException.ThrowIfNull(questIds);
        var w = new PacketWriter(2 + questIds.Count * 4);
        w.WriteUInt16((ushort)questIds.Count);
        foreach (var questId in questIds) w.WriteUInt32((uint)questId);
        return w.ToArray();
    }

    public static byte[] ZoneRankMonsters(int questId, string questName, IReadOnlyList<(int MonsterId, string Name)> monstersLastFirst)
    {
        ArgumentNullException.ThrowIfNull(monstersLastFirst);
        var w = new PacketWriter(8 + monstersLastFirst.Count * 24);
        w.WriteUInt16((ushort)questId);
        WriteText(w, questName);
        w.WriteByte((byte)monstersLastFirst.Count);
        foreach (var (monsterId, name) in monstersLastFirst)
        {
            w.WriteUInt16((ushort)monsterId);
            WriteText(w, name);
        }
        return w.ToArray();
    }

    public static byte[] NpcMessage(ushort npcEntityId, int messageIndex, string text, int kind, int questId)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(16 + text.Length);
        w.WriteUInt16(npcEntityId);
        w.WriteUInt16((ushort)messageIndex);
        WriteText(w, text);
        w.WriteUInt16((ushort)kind);
        w.WriteUInt16((ushort)questId);
        return w.ToArray();
    }

    public static NpcMessagePage ReadNpcMessage(ReadOnlySpan<byte> payload, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var r = new PacketReader(payload);
        var npcEntityId = r.ReadUInt16();
        var messageIndex = r.ReadUInt16();
        var text = r.ReadNulTerminated(encoding);
        var kind = r.ReadUInt16();
        var questId = r.ReadUInt16();
        if (r.Remaining != 0)
        {
            throw new InvalidPacketException($"The NPC message payload has {r.Remaining} bytes after its quest id.");
        }
        return new NpcMessagePage(npcEntityId, messageIndex, text, kind, questId);
    }

    public static byte[] NpcMessageMissing(ushort npcEntityId, int messageIndex)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(npcEntityId);
        w.WriteUInt16((ushort)messageIndex);
        return w.ToArray();
    }

    private static byte[] QuestIdOnly(int questId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16((ushort)questId);
        return w.ToArray();
    }

    private static void WriteTexts(PacketWriter w, QuestTexts texts)
    {
        WriteText(w, texts.Name);
        WriteText(w, texts.Client);
        WriteText(w, texts.Pos);
        WriteText(w, texts.Comment);
    }

    public static byte[] Text(string text)
    {
        var w = new PacketWriter((text?.Length ?? 0) + 1);
        WriteText(w, text ?? string.Empty);
        return w.ToArray();
    }

    private static void WriteText(PacketWriter w, string text)
    {
        w.WriteBytes(ClientText.Encoding.GetBytes(text ?? string.Empty));
        w.WriteByte(0);
    }
}
