using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.Social;

public static class ItemPriceFrames
{
    public const ushort Report = 0xFDE9;
    public const ushort Query = 0xFDEA;
    public const ushort Statistics = 0xFDEB;
    public const int ReportSize = 2 * sizeof(uint);
    public const int QuerySize = sizeof(uint);

    public static bool TryReadReport(ReadOnlySpan<byte> payload, out uint itemId, out int price)
    {
        itemId = 0;
        price = 0;
        if (payload.Length < ReportSize) return false;
        var r = new PacketReader(payload);
        itemId = r.ReadUInt32();
        price = r.ReadInt32();
        return true;
    }

    public static bool TryReadQuery(ReadOnlySpan<byte> payload, out uint itemId)
    {
        itemId = 0;
        if (payload.Length < QuerySize) return false;
        itemId = new PacketReader(payload).ReadUInt32();
        return true;
    }

    public static byte[] Reply(uint itemId, ItemPriceStatistics? statistics)
    {
        var w = new PacketWriter(4 * sizeof(uint));
        w.WriteUInt32(itemId);
        w.WriteInt32(statistics?.Minimum ?? 0);
        w.WriteInt32(statistics?.Maximum ?? 0);
        w.WriteInt32(statistics?.Average ?? 0);
        return w.ToArray();
    }
}
