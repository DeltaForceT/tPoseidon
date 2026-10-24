using tPoseidon.Core.Game.SoulSeed;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public static class SoulSeedFrames
{
    public const int RecordSize = 0x25;
    public const int SessionSize = 0x7E;
    public const int BuffSize = 0x3C;
    public const int EventRowSize = 0x24;
    public const int EventRowRateOffset = 0x08;
    public const int EventRowPointsOffset = 0x14;
    public const byte Granted = 1;
    public const byte NotGranted = 0;

    public static byte[] Planted(SoulSeedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var w = new PacketWriter(1 + RecordSize);
        w.WriteByte(record.Planted ? (byte)1 : (byte)0);
        WriteRecord(w, record);
        return w.ToArray();
    }

    public static byte[] State(SoulSeedRecord record, IReadOnlyList<short> sessionPoints, IReadOnlyList<short> counters)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(sessionPoints);
        ArgumentNullException.ThrowIfNull(counters);
        var w = new PacketWriter(1 + RecordSize + SessionSize);
        w.WriteByte(record.Planted ? (byte)1 : (byte)0);
        WriteRecord(w, record);
        for (var i = 0; i < SoulSeedRules.TypeCount; i++) w.WriteInt16(i < sessionPoints.Count ? sessionPoints[i] : (short)0);
        for (var i = 0; i < SoulSeedRules.EventTypeCount; i++) w.WriteInt16(i < counters.Count ? counters[i] : (short)0);
        return w.ToArray();
    }

    public static byte[] Buff(SoulSeedBuff buff)
    {
        ArgumentNullException.ThrowIfNull(buff);
        var w = new PacketWriter(BuffSize);
        w.WriteInt32(buff.Group);
        w.WriteInt32(buff.MillisecondsLeft);
        w.WriteInt32(buff.Stacks);
        for (var i = 0; i < SoulSeedRules.BuffValueSlots; i++) w.WriteInt32(i < buff.Values.Count ? buff.Values[i] : 0);
        return w.ToArray();
    }

    public static byte[] Progress(bool granted, int counter, SoulSeedEventEntry row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var w = new PacketWriter(5 + EventRowSize);
        w.WriteByte(granted ? Granted : NotGranted);
        if (!granted) w.WriteInt32(counter);
        var raw = new byte[EventRowSize];
        BitConverter.TryWriteBytes(raw.AsSpan(EventRowRateOffset), row.Rate);
        BitConverter.TryWriteBytes(raw.AsSpan(EventRowRateOffset + 4), row.MaximumLevel);
        raw[EventRowRateOffset + 8] = row.Special ? (byte)1 : (byte)0;
        for (var i = 0; i < SoulSeedTables.GuardianKinds && i < row.Points.Count; i++)
        {
            BitConverter.TryWriteBytes(raw.AsSpan(EventRowPointsOffset + i * sizeof(int)), row.Points[i]);
        }
        w.WriteBytes(raw);
        return w.ToArray();
    }

    private static void WriteRecord(PacketWriter w, SoulSeedRecord record)
    {
        w.WriteByte(record.Planted ? (byte)1 : (byte)0);
        w.WriteInt32(record.SeedItemId);
        w.WriteInt64(record.StartAt.ToUnixTimeSeconds());
        w.WriteInt64(record.LastUpdateAt.ToUnixTimeSeconds());
        for (var i = 0; i < SoulSeedRules.TypeCount; i++) w.WriteInt16(i < record.SavedPoints.Count ? record.SavedPoints[i] : (short)0);
    }
}
