using System.Text;
using tPoseidon.Core.Game.PlayerShop;

namespace tPoseidon.Core.Net.World;

public static class PlayerShopHelperFrames
{
    public static bool TryReadQuery(ReadOnlySpan<byte> payload, Encoding encoding, out string query)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        query = string.Empty;
        if (payload.Length < 1 || payload.IndexOf((byte)0) < 0) return false;
        query = new PacketReader(payload).ReadNulTerminated(encoding);
        return true;
    }

    public static byte[] Reply(IEnumerable<PlayerShopHelperRow> rows, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(encoding);
        var body = new PacketWriter(PlayerShopHelperRules.BufferSize);
        var used = PlayerShopHelperRules.CountSize;
        ushort count = 0;
        foreach (var row in rows)
        {
            var size = PlayerShopHelperRules.RecordSize(row, text => encoding.GetByteCount(text));
            if (!PlayerShopHelperRules.Fits(used, size)) break;
            body.WriteNulTerminated(row.ItemName, encoding);
            body.WriteNulTerminated(row.KeeperName, encoding);
            body.WriteNulTerminated(row.ShopTitle, encoding);
            body.WriteUInt32(row.Price);
            body.WriteUInt16(row.X);
            body.WriteUInt16(row.Y);
            used += size;
            count++;
        }
        var w = new PacketWriter(used);
        w.WriteUInt16(count);
        w.WriteBytes(body.ToArray());
        return w.ToArray();
    }
}
