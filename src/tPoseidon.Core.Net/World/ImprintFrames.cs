using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class ImprintFrames
{
    public const ushort List = 0x056A;
    public const ushort Used = 0x056B;
    public const int MaxListed = byte.MaxValue;

    public static byte[] ListOf(IReadOnlyList<ActiveImprint> active, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(active);
        var listed = Math.Min(active.Count, MaxListed);
        var w = new PacketWriter(1 + listed * 24);
        w.WriteByte((byte)listed);
        for (var i = 0; i < listed; i++) WriteImprint(w, active[i], now);
        return w.ToArray();
    }

    public static byte[] UsedWith(ActiveImprint imprint, DateTimeOffset now, BagOperation consumed)
    {
        ArgumentNullException.ThrowIfNull(imprint);
        var w = new PacketWriter(48);
        w.WriteUInt32((uint)imprint.Type);
        w.WriteUInt64(ClientClock.FileTime(now));
        w.WriteUInt64(ClientClock.FileTime(imprint.Until));
        w.WriteBytes(QuestResultFrames.ItemOperation(consumed));
        return w.ToArray();
    }

    private static void WriteImprint(PacketWriter w, ActiveImprint imprint, DateTimeOffset now)
    {
        w.WriteUInt32((uint)imprint.Type);
        w.WriteUInt32((uint)imprint.ItemId);
        w.WriteUInt64(ClientClock.FileTime(now));
        w.WriteUInt64(ClientClock.FileTime(imprint.Until));
    }
}
