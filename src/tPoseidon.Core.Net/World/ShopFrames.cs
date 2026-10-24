using System.Text;

namespace tPoseidon.Core.Net.World;

public static class ShopFrames
{
    public const ushort MoneyFullCode = 0;

    public static byte[] Opened(ushort shopEntityId, string script, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(textEncoding);
        var text = textEncoding.GetBytes(script);
        var w = new PacketWriter(3 + text.Length);
        w.WriteUInt16(shopEntityId);
        w.WriteBytes(text);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] BuyPrices(IReadOnlyList<(int ItemId, int UnitPrice)> goods)
    {
        ArgumentNullException.ThrowIfNull(goods);
        var w = new PacketWriter(2 + goods.Count * 8);
        w.WriteUInt16((ushort)goods.Count);
        foreach (var (itemId, unitPrice) in goods)
        {
            w.WriteUInt32((uint)itemId);
            w.WriteUInt32((uint)unitPrice);
        }
        return w.ToArray();
    }

    public static byte[] SellRates(float shopRate, IReadOnlyList<(int ItemId, float Rate)> goods)
    {
        ArgumentNullException.ThrowIfNull(goods);
        var w = new PacketWriter(6 + goods.Count * 8);
        w.WriteUInt32(BitConverter.SingleToUInt32Bits(shopRate));
        w.WriteUInt16((ushort)goods.Count);
        foreach (var (itemId, rate) in goods)
        {
            w.WriteUInt32((uint)itemId);
            w.WriteUInt32(BitConverter.SingleToUInt32Bits(rate));
        }
        return w.ToArray();
    }

    public static byte[] Traded(long money, IReadOnlyList<BagOperation> operations)
    {
        var w = new PacketWriter(32);
        w.WriteUInt32Field(money);
        w.WriteBytes(QuestResultFrames.ItemOperations(operations));
        return w.ToArray();
    }

    public static (uint Money, IReadOnlyList<BagOperation> Operations) ReadTraded(ReadOnlySpan<byte> payload)
    {
        var money = new PacketReader(payload).ReadUInt32();
        return (money, QuestResultFrames.ReadItemOperations(payload[sizeof(uint)..]));
    }

    public static byte[] MoneyFull()
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(MoneyFullCode);
        return w.ToArray();
    }

    public static byte[] PlayerInShop(ushort playerEntityId, ushort x, ushort y, byte moveType)
    {
        var w = new PacketWriter(7);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        w.WriteByte((byte)(moveType & 7));
        return w.ToArray();
    }
}
