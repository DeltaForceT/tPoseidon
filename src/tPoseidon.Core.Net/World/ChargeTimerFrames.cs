using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class ChargeTimerFrames
{
    public const ushort Opened = 0x0300;
    public const ushort Recharge = 0x0301;
    public const ushort Recharged = 0x0302;
    public const ushort Refused = 0x0303;
    public const ushort Close = 0x0304;
    public const ushort Closed = 0x0305;
    public const int RequestSize = 32;

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out RechargeRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        request = new RechargeRequest(r.ReadUInt64(), r.ReadUInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        return true;
    }

    public static byte[] Done(ulong itemWireUid, int itemIndex, DateTimeOffset expiry, BagOperation coupon)
    {
        var w = new PacketWriter(48);
        w.WriteUInt64(itemWireUid);
        w.WriteUInt32((uint)itemIndex);
        w.WriteUInt64(ClientClock.FileTime(expiry));
        w.WriteBytes(QuestResultFrames.ItemOperation(coupon));
        return w.ToArray();
    }
}
