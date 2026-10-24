using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class CashBoxMessages
{
    public static byte[] EncodeList(uint accountId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(accountId);
        return w.ToArray();
    }

    public static uint DecodeList(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt32();

    public static byte[] EncodeRows(IReadOnlyList<ChargedItem> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var w = new PacketWriter(2 + rows.Count * 18);
        w.WriteUInt16((ushort)Math.Min(rows.Count, ushort.MaxValue));
        foreach (var row in rows.Take(ushort.MaxValue))
        {
            w.WriteByte(row.ItemClass);
            w.WriteInt32(row.ItemIndex);
            w.WriteUInt64(row.ItemUid);
            w.WriteInt16(row.ItemAttr);
            w.WriteInt16(row.UseTimeHours);
            w.WriteByte(row.ItemFlag);
        }
        return w.ToArray();
    }

    public static IReadOnlyList<ChargedItem> DecodeRows(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadUInt16();
        var rows = new List<ChargedItem>(count);
        for (var i = 0; i < count; i++)
        {
            var itemClass = r.ReadByte();
            var index = r.ReadInt32();
            var uid = r.ReadUInt64();
            var attr = r.ReadInt16();
            var hours = r.ReadInt16();
            var flag = r.ReadByte();
            rows.Add(new ChargedItem(uid, itemClass, index, attr, hours, flag));
        }
        return rows;
    }

    public static byte[] EncodeMove(ChargedItemMove move)
    {
        ArgumentNullException.ThrowIfNull(move);
        var w = new PacketWriter(13);
        w.WriteUInt32(move.AccountId);
        w.WriteUInt64(move.ItemUid);
        w.WriteByte(move.JoinStack ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static ChargedItemMove DecodeMove(ulong characterId, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var account = r.ReadUInt32();
        var uid = r.ReadUInt64();
        return new ChargedItemMove(account, uid, characterId, r.ReadByte() != 0);
    }

    public static byte[] EncodeMoved(ChargedItemMoved moved)
    {
        ArgumentNullException.ThrowIfNull(moved);
        var w = new PacketWriter(26);
        w.WriteUInt64(moved.ItemUid);
        w.WriteByte(moved.JoinedStack ? (byte)1 : (byte)0);
        w.WriteByte(moved.ItemClass);
        w.WriteInt32(moved.ItemIndex);
        w.WriteUInt16(moved.Count);
        w.WriteUInt16(moved.Total);
        w.WriteInt64(moved.BreakedAt.UtcTicks);
        return w.ToArray();
    }

    public static ChargedItemMoved DecodeMoved(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var uid = r.ReadUInt64();
        var joined = r.ReadByte() != 0;
        var itemClass = r.ReadByte();
        var index = r.ReadInt32();
        var count = r.ReadUInt16();
        var total = r.ReadUInt16();
        var breaked = new DateTimeOffset(r.ReadInt64(), TimeSpan.Zero);
        return new ChargedItemMoved(uid, joined, itemClass, index, count, total, breaked);
    }

    public static byte[] EncodeError(int error)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(error);
        return w.ToArray();
    }

    public static int DecodeError(ReadOnlySpan<byte> body) =>
        body.Length >= 4 ? new PacketReader(body).ReadInt32() : ChargedItemRules.MoveFailed;
}
