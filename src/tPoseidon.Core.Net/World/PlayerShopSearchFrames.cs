using System.Text;
using tPoseidon.Core.Game.PlayerShop;

namespace tPoseidon.Core.Net.World;

public static class PlayerShopSearchFrames
{
    public const int QueryFixedSize = 1 + 1 + 2 + 2;
    public const int BuySize = 2 + 8 + 2 + 4;
    public const int NameBytes = 0x20;

    public static bool TryReadQuery(ReadOnlySpan<byte> payload, Encoding encoding, out PlayerShopSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        query = new PlayerShopSearchQuery(0, 0, 0, 0, string.Empty);
        if (payload.Length < QueryFixedSize + 1) return false;
        var r = new PacketReader(payload);
        var category = r.ReadByte();
        var grade = r.ReadByte();
        var min = r.ReadUInt16();
        var max = r.ReadUInt16();
        query = new PlayerShopSearchQuery(category, grade, min, max, r.ReadNulTerminated(encoding));
        return true;
    }

    public static bool TryReadBuy(ReadOnlySpan<byte> payload, out PlayerShopRemoteBuy buy)
    {
        buy = default;
        if (payload.Length < BuySize) return false;
        var r = new PacketReader(payload);
        buy = new PlayerShopRemoteBuy(r.ReadUInt16(), r.ReadUInt64(), r.ReadUInt16(), r.ReadUInt32());
        return true;
    }

    public static byte[] Code(byte code) => [code];

    public static byte[] Page(IReadOnlyList<PlayerShopSearchRow> rows, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(1 + rows.Count * 64);
        w.WriteByte((byte)rows.Count);
        foreach (var row in rows)
        {
            w.WriteUInt16(row.KeeperId);
            w.WriteByte(row.CharacterType);
            w.WriteNulTerminated(Truncate(row.KeeperName, encoding), encoding);
            w.WriteByte(row.ItemClass);
            w.WriteUInt32(row.ItemId);
            w.WriteUInt64(row.WireUid);
            w.WriteUInt16(row.Stack);
            w.WriteUInt32(row.UnitPrice);
            w.WriteUInt16(row.Count);
        }
        return w.ToArray();
    }

    public static byte[] Bought(uint money, BagOperation operation, ulong wireUid, ushort remaining)
    {
        var w = new PacketWriter(40);
        w.WriteUInt32(money);
        w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        w.WriteUInt64(wireUid);
        w.WriteUInt16(remaining);
        return w.ToArray();
    }

    private static string Truncate(string name, Encoding encoding)
    {
        var text = name;
        while (text.Length > 0 && encoding.GetByteCount(text) > NameBytes - 1) text = text[..^1];
        return text;
    }
}
