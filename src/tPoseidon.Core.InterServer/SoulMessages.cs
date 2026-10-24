using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class SoulMessages
{
    private const int RowSize = 8 + 4 + 8 + 4 + 4 + 8;

    public static byte[] EncodeAccount(SoulAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var w = new PacketWriter(6 + account.Guardians.Count * RowSize);
        w.WriteUInt32(account.Soul);
        w.WriteUInt16((ushort)account.Guardians.Count);
        foreach (var row in account.Guardians) WriteRow(w, row);
        return w.ToArray();
    }

    public static SoulAccount DecodeAccount(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var soul = r.ReadUInt32();
        var rows = new GuardianSoulRow[r.ReadUInt16()];
        for (var i = 0; i < rows.Length; i++) rows[i] = ReadRow(ref r);
        return new SoulAccount(soul, rows);
    }

    public static byte[] EncodeSoul(uint soul)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(soul);
        return w.ToArray();
    }

    public static uint DecodeSoul(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt32();

    public static byte[] EncodeGuardian(GuardianSoulRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var w = new PacketWriter(RowSize);
        WriteRow(w, row);
        return w.ToArray();
    }

    public static GuardianSoulRow DecodeGuardian(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return ReadRow(ref r);
    }

    private static void WriteRow(PacketWriter w, GuardianSoulRow row)
    {
        w.WriteUInt64(row.GuardianUid);
        w.WriteInt32(row.Gauge);
        w.WriteUInt64(row.SourceItemUid);
        w.WriteInt32(row.SourceCount);
        w.WriteInt32(row.PotionsReady);
        w.WriteInt64(row.LastTick.ToUnixTimeMilliseconds());
    }

    private static GuardianSoulRow ReadRow(ref PacketReader r) => new(
        r.ReadUInt64(),
        r.ReadInt32(),
        r.ReadUInt64(),
        r.ReadInt32(),
        r.ReadInt32(),
        DateTimeOffset.FromUnixTimeMilliseconds(r.ReadInt64()));
}
