using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record PartyQuestAcceptRequest(uint QuestId, ushort MonsterId, ushort TargetCount, IReadOnlyList<ulong> Members);

public sealed record PartyQuestAccepted(ulong PartyUid, IReadOnlyDictionary<ulong, int> CompletionCounts);

public sealed record PartyQuestCompleteRequest(uint QuestId, ulong PartyUid, ushort KillCount, int RecordTime, byte MemberCount);

public sealed record PartyQuestRewardRequest(ulong PartyUid, uint QuestId);

public static class PartyQuestMessages
{
    public static byte[] EncodeAccept(PartyQuestAcceptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(16 + request.Members.Count * 8);
        w.WriteUInt32(request.QuestId);
        w.WriteUInt16(request.MonsterId);
        w.WriteUInt16(request.TargetCount);
        w.WriteByte((byte)request.Members.Count);
        foreach (var member in request.Members) w.WriteUInt64(member);
        return w.ToArray();
    }

    public static PartyQuestAcceptRequest DecodeAccept(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var questId = r.ReadUInt32();
        var monsterId = r.ReadUInt16();
        var targetCount = r.ReadUInt16();
        var count = r.ReadByte();
        var members = new ulong[count];
        for (var i = 0; i < count; i++) members[i] = r.ReadUInt64();
        return new PartyQuestAcceptRequest(questId, monsterId, targetCount, members);
    }

    public static byte[] EncodeAccepted(PartyQuestAccepted accepted)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        var w = new PacketWriter(16 + accepted.CompletionCounts.Count * 9);
        w.WriteUInt64(accepted.PartyUid);
        w.WriteByte((byte)accepted.CompletionCounts.Count);
        foreach (var (characterId, count) in accepted.CompletionCounts)
        {
            w.WriteUInt64(characterId);
            w.WriteByte((byte)count);
        }
        return w.ToArray();
    }

    public static PartyQuestAccepted DecodeAccepted(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var partyUid = r.ReadUInt64();
        var count = r.ReadByte();
        var counts = new Dictionary<ulong, int>();
        for (var i = 0; i < count; i++) counts[r.ReadUInt64()] = r.ReadByte();
        return new PartyQuestAccepted(partyUid, counts);
    }

    public static byte[] EncodeComplete(PartyQuestCompleteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(19);
        w.WriteUInt32(request.QuestId);
        w.WriteUInt64(request.PartyUid);
        w.WriteUInt16(request.KillCount);
        w.WriteInt32(request.RecordTime);
        w.WriteByte(request.MemberCount);
        return w.ToArray();
    }

    public static PartyQuestCompleteRequest DecodeComplete(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PartyQuestCompleteRequest(r.ReadUInt32(), r.ReadUInt64(), r.ReadUInt16(), r.ReadInt32(), r.ReadByte());
    }

    public static byte[] EncodeReward(PartyQuestRewardRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(12);
        w.WriteUInt64(request.PartyUid);
        w.WriteUInt32(request.QuestId);
        return w.ToArray();
    }

    public static PartyQuestRewardRequest DecodeReward(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new PartyQuestRewardRequest(r.ReadUInt64(), r.ReadUInt32());
    }

    public static byte[] EncodeCompletionCount(int comCount)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(comCount);
        return w.ToArray();
    }

    public static int DecodeCompletionCount(ReadOnlySpan<byte> body) => new PacketReader(body).ReadInt32();

    public static byte[] EncodeList(IReadOnlyList<PartyQuestRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var w = new PacketWriter(1 + records.Count * 23);
        w.WriteByte((byte)records.Count);
        foreach (var record in records)
        {
            w.WriteUInt64(record.PartyUid);
            w.WriteUInt16(record.QuestId);
            w.WriteUInt16(record.MonsterId);
            w.WriteUInt16(record.KillCount);
            w.WriteUInt16(record.TargetCount);
            w.WriteInt32(record.RecordTime);
            w.WriteByte(record.MemberCount);
            w.WriteUInt16((ushort)record.ComCount);
        }
        return w.ToArray();
    }

    public static IReadOnlyList<PartyQuestRecord> DecodeList(ulong characterId, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadByte();
        var records = new List<PartyQuestRecord>(count);
        for (var i = 0; i < count; i++)
        {
            records.Add(new PartyQuestRecord(
                r.ReadUInt64(), characterId, r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(),
                r.ReadInt32(), r.ReadByte(), r.ReadUInt16()));
        }
        return records;
    }
}
