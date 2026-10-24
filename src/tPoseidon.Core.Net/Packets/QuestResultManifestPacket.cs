using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public enum QuestResultRecordKind : ushort
{
    RewardGrant = 0x0204,
    ItemRevoke = 0x0103,
}

public sealed record QuestResultRecord
{
    public required QuestResultRecordKind Kind { get; init; }
    public required uint ItemIndex { get; init; }
    public required uint ItemUid { get; init; }
    public uint Count { get; init; } = 1;
}

[WireOpcode((ushort)Opcode.S2C_QUEST_RESULT_MANIFEST)]
public sealed class QuestResultManifestPacket
{
    public required uint CompletedQuestId { get; init; }
    public required IReadOnlyList<QuestResultRecord> Records { get; init; }

    public byte[] EncodePayload()
    {
        const int grantStride = 21;
        const int revokeStride = 16;

        var ordered = new List<QuestResultRecord>(Records.Count);
        var rewardCount = 0;
        foreach (var rec in Records)
        {
            if (rec.Kind == QuestResultRecordKind.RewardGrant)
            {
                ordered.Add(rec);
                rewardCount++;
            }
        }
        foreach (var rec in Records)
        {
            if (rec.Kind == QuestResultRecordKind.ItemRevoke)
            {
                ordered.Add(rec);
            }
        }

        var recordsBytes = 0;
        foreach (var rec in ordered)
        {
            recordsBytes += rec.Kind == QuestResultRecordKind.RewardGrant ? grantStride : revokeStride;
        }

        var headerBytes = 2;
        var summaryHeaderBytes = 2;
        var perRewardSummaryBytes = 6;
        var rewardSummaryTotal = rewardCount * perRewardSummaryBytes;
        var trailerBytes = 18;
        var totalBytes = headerBytes + recordsBytes + summaryHeaderBytes + rewardSummaryTotal + trailerBytes;

        var buf = new byte[totalBytes];
        var off = 0;

        WriteUInt16(buf, off, (ushort)ordered.Count); off += 2;

        foreach (var rec in ordered)
        {
            WriteUInt16(buf, off, (ushort)rec.Kind); off += 2;
            WriteUInt32(buf, off, rec.ItemIndex); off += 4;
            WriteUInt32(buf, off, rec.ItemUid); off += 4;
            var wireCount = rec.Kind == QuestResultRecordKind.ItemRevoke ? 1u : rec.Count;
            WriteUInt32(buf, off, wireCount); off += 4;
            if (rec.Kind == QuestResultRecordKind.RewardGrant)
            {
                WriteUInt32(buf, off, 1u); off += 4;
                off += 3;
            }
            else
            {
                off += 2;
            }
        }

        WriteUInt16(buf, off, (ushort)rewardCount); off += 2;
        foreach (var rec in ordered)
        {
            if (rec.Kind != QuestResultRecordKind.RewardGrant) continue;
            WriteUInt32(buf, off, rec.ItemIndex); off += 4;
            WriteUInt16(buf, off, (ushort)rec.Count); off += 2;
        }

        WriteUInt32(buf, off, 0x00000100); off += 4;
        WriteUInt32(buf, off, 0x00000100); off += 4;
        WriteUInt32(buf, off, CompletedQuestId); off += 4;
        WriteUInt16(buf, off, 0x0001); off += 2;
        off += 4;
        return buf;
    }

    private static void WriteUInt16(byte[] buf, int off, ushort v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteUInt32(byte[] buf, int off, uint v)
    {
        buf[off] = (byte)(v & 0xFF);
        buf[off + 1] = (byte)((v >> 8) & 0xFF);
        buf[off + 2] = (byte)((v >> 16) & 0xFF);
        buf[off + 3] = (byte)((v >> 24) & 0xFF);
    }
}
