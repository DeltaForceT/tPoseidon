using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public readonly record struct UniqueWithdrawRequest(ushort ZoneId, ushort PlayerId, ulong CharUid, ushort Callback, IReadOnlyList<UniqueHeldItem> Items);

public static class UniqueItemMessages
{
    public const ushort WithdrawCallback = 0x761B;

    public static byte[] EncodeCount(uint key, uint units)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32(key);
        w.WriteUInt32(units);
        return w.ToArray();
    }

    public static (uint Key, uint Units) DecodeCount(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt32(), r.ReadUInt32());
    }

    public static byte[] EncodeIssue(uint key, IReadOnlyList<int> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        var w = new PacketWriter(8 + itemIds.Count * 4);
        w.WriteUInt32(key);
        w.WriteUInt32((uint)itemIds.Count);
        foreach (var itemId in itemIds) w.WriteUInt32((uint)itemId);
        return w.ToArray();
    }

    public static (uint Key, IReadOnlyList<int> ItemIds) DecodeIssue(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var key = r.ReadUInt32();
        var count = r.ReadUInt32();
        var ids = new List<int>((int)Math.Min(count, 64));
        for (var i = 0; i < count; i++) ids.Add((int)r.ReadUInt32());
        return (key, ids);
    }

    public static byte[] EncodeBossKill(ulong charUid, ushort monsterId)
    {
        var w = new PacketWriter(10);
        w.WriteUInt64(charUid);
        w.WriteUInt16(monsterId);
        return w.ToArray();
    }

    public static (ulong CharUid, ushort MonsterId) DecodeBossKill(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadUInt16());
    }

    public static byte[] EncodeLatestBossKills(IReadOnlyList<BossKillRecord> kills)
    {
        ArgumentNullException.ThrowIfNull(kills);
        var shown = kills.Take(byte.MaxValue).ToList();
        var w = new PacketWriter(1 + shown.Count * 10);
        w.WriteByte((byte)shown.Count);
        foreach (var kill in shown)
        {
            w.WriteUInt16((ushort)kill.MonsterId);
            w.WriteInt64(kill.LatestKill.ToFileTime());
        }
        return w.ToArray();
    }

    public static IReadOnlyList<BossKillRecord> DecodeLatestBossKills(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadByte();
        var kills = new List<BossKillRecord>(count);
        for (var i = 0; i < count; i++) kills.Add(new BossKillRecord(r.ReadUInt16(), DateTimeOffset.FromFileTime(r.ReadInt64())));
        return kills;
    }

    public static byte[] EncodeDutyExpire(ulong charUid, DateTimeOffset expire, DateTimeOffset now)
    {
        var w = new PacketWriter(24);
        w.WriteUInt64(charUid);
        w.WriteInt64(expire.ToFileTime());
        w.WriteInt64(now.ToFileTime());
        return w.ToArray();
    }

    public static (ulong CharUid, long ExpireFileTime, long NowFileTime) DecodeDutyExpire(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadInt64(), r.ReadInt64());
    }

    public static byte[] EncodePickup(ulong itemUid, int itemIndex)
    {
        var w = new PacketWriter(12);
        w.WriteUInt64(itemUid);
        w.WriteInt32(itemIndex);
        return w.ToArray();
    }

    public static (ulong ItemUid, int ItemIndex) DecodePickup(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadInt32());
    }

    public static byte[] EncodeWithdraw(UniqueWithdrawRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Items);
        var w = new PacketWriter(14 + UniqueItemRules.MaxWithdrawnPerRequest * 12);
        w.WriteUInt16(request.ZoneId);
        w.WriteUInt16(request.PlayerId);
        w.WriteUInt64(request.CharUid);
        w.WriteUInt16(request.Callback);
        for (var i = 0; i < UniqueItemRules.MaxWithdrawnPerRequest; i++)
        {
            var item = i < request.Items.Count ? request.Items[i] : default;
            w.WriteUInt64(item.ItemUid);
            w.WriteUInt32((uint)item.ItemIndex);
        }
        return w.ToArray();
    }

    public static UniqueWithdrawRequest DecodeWithdraw(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var zone = r.ReadUInt16();
        var player = r.ReadUInt16();
        var charUid = r.ReadUInt64();
        var callback = r.ReadUInt16();
        var items = new List<UniqueHeldItem>(UniqueItemRules.MaxWithdrawnPerRequest);
        for (var i = 0; i < UniqueItemRules.MaxWithdrawnPerRequest; i++)
        {
            var item = new UniqueHeldItem(r.ReadUInt64(), (int)r.ReadUInt32());
            if (item.ItemUid != 0) items.Add(item);
        }
        return new UniqueWithdrawRequest(zone, player, charUid, callback, items);
    }

    public static byte[] EncodeWithdrawn(IReadOnlyList<UniqueHeldItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = new PacketWriter(1 + items.Count * 12);
        w.WriteByte((byte)items.Count);
        foreach (var item in items)
        {
            w.WriteUInt64(item.ItemUid);
            w.WriteInt32(item.ItemIndex);
        }
        return w.ToArray();
    }

    public static IReadOnlyList<UniqueHeldItem> DecodeWithdrawn(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadByte();
        var items = new List<UniqueHeldItem>(count);
        for (var i = 0; i < count; i++) items.Add(new UniqueHeldItem(r.ReadUInt64(), r.ReadInt32()));
        return items;
    }
}
