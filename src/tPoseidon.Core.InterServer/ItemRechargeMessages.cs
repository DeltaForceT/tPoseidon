using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class ItemRechargeMessages
{
    public static byte[] EncodeRequest(ItemRecharge recharge, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recharge);
        var w = new PacketWriter(44);
        w.WriteUInt64(recharge.CharacterId);
        w.WriteUInt64(recharge.ItemUid);
        w.WriteInt32(recharge.Hours);
        w.WriteUInt64(recharge.CouponUid);
        w.WriteInt32(recharge.CouponCount);
        w.WriteInt64(now.ToUnixTimeMilliseconds());
        return w.ToArray();
    }

    public static (ItemRecharge Recharge, DateTimeOffset Now) DecodeRequest(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var recharge = new ItemRecharge(r.ReadUInt64(), r.ReadUInt64(), r.ReadInt32(), r.ReadUInt64(), r.ReadInt32());
        return (recharge, DateTimeOffset.FromUnixTimeMilliseconds(r.ReadInt64()));
    }

    public static byte[] EncodeResult(ItemRechargeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var w = new PacketWriter(12);
        w.WriteInt64(result.BreakedAt.ToUnixTimeMilliseconds());
        w.WriteInt32(result.CouponLeft);
        return w.ToArray();
    }

    public static ItemRechargeResult DecodeResult(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var breaked = DateTimeOffset.FromUnixTimeMilliseconds(r.ReadInt64());
        return new ItemRechargeResult(ItemRechargeErrors.Ok, breaked, r.ReadInt32());
    }

    public static byte[] EncodeError(int error)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(error);
        return w.ToArray();
    }

    public static int DecodeError(ReadOnlySpan<byte> body) =>
        body.Length >= 4 ? new PacketReader(body).ReadInt32() : ItemRechargeErrors.StoreFailed;
}
