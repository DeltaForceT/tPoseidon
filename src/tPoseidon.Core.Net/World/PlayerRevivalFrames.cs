using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public static class PlayerRevivalFrames
{
    public const int ResurrectedSize = 4;
    public const ushort NoReviver = 0;
    public const int RestartNoticeSize = 10;
    public const int LeftMapSize = 2;
    public const int ItemUsedSize = 8;

    public static readonly TimeSpan CompletionDelay = TimeSpan.FromMilliseconds(1500);
    public static readonly TimeSpan Protection = TimeSpan.FromMilliseconds(5000);

    public static byte[] RestartNotice(ushort mapId, uint hp)
    {
        var w = new PacketWriter(RestartNoticeSize);
        w.WriteUInt16(mapId);
        w.WriteUInt16(0);
        w.WriteUInt16(0);
        w.WriteUInt32(hp);
        return w.ToArray();
    }

    public static byte[] ItemUsed(ulong itemUid)
    {
        var w = new PacketWriter(ItemUsedSize);
        w.WriteUInt64(itemUid);
        return w.ToArray();
    }

    public static byte[] LeftMap(ushort playerId)
    {
        var w = new PacketWriter(LeftMapSize);
        w.WriteUInt16(playerId);
        return w.ToArray();
    }

    public static byte[] Resurrected(ushort playerId, ushort reviverId)
    {
        var w = new PacketWriter(ResurrectedSize);
        w.WriteUInt16(playerId);
        w.WriteUInt16(reviverId);
        return w.ToArray();
    }

    public static (ushort MapId, uint Hp) ReadRestartNotice(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, RestartNoticeSize, "restart notice"));
        var mapId = r.ReadUInt16();
        r.ReadUInt16();
        r.ReadUInt16();
        return (mapId, r.ReadUInt32());
    }

    public static ushort ReadLeftMap(ReadOnlySpan<byte> payload) =>
        new PacketReader(Sized(payload, LeftMapSize, "left map")).ReadUInt16();

    public static (ushort PlayerId, ushort ReviverId) ReadResurrected(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, ResurrectedSize, "resurrection"));
        return (r.ReadUInt16(), r.ReadUInt16());
    }

    private static ReadOnlySpan<byte> Sized(ReadOnlySpan<byte> payload, int size, string what) =>
        payload.Length == size
            ? payload
            : throw new InvalidPacketException($"The {what} payload must be exactly {size} bytes, got {payload.Length}.");
}
