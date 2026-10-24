using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed record PlayerShopRegisterRequest(byte ShopType, IReadOnlyList<PlayerShopSlot> Slots, string Title);

public readonly record struct PlayerShopListedSlot(PlayerShopSlot Slot, TradeItemView? Item, IReadOnlyList<ItemProperty> Properties);

public static class PlayerShopFrames
{
    public const int SlotRecordSize = 18;
    public const int VisitRequestSize = 2;
    public const int BuyRequestSize = 20;
    public const byte MissingItem = 0;

    public static byte[] Code(byte code) => [code];

    public static byte[] Word(ushort value)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(value);
        return w.ToArray();
    }

    public static bool TryReadRegister(ReadOnlySpan<byte> payload, Encoding encoding, out PlayerShopRegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        request = new PlayerShopRegisterRequest(0, Array.Empty<PlayerShopSlot>(), string.Empty);
        var r = new PacketReader(payload);
        if (r.Remaining < 3) return false;
        var shopType = r.ReadByte();
        var count = r.ReadUInt16();
        if (r.Remaining < count * SlotRecordSize) return false;
        var slots = new PlayerShopSlot[count];
        for (var i = 0; i < count; i++) slots[i] = new PlayerShopSlot(r.ReadUInt64(), r.ReadUInt32(), r.ReadInt32(), r.ReadUInt16());
        if (r.Remaining < 1) return false;
        request = new PlayerShopRegisterRequest(shopType, slots, r.ReadNulTerminated(encoding));
        return true;
    }

    public static byte[] Sign(ushort keeperId, string title, byte shopType, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(4 + title.Length * 2);
        w.WriteUInt16(keeperId);
        w.WriteNulTerminated(title, encoding);
        w.WriteByte(shopType);
        return w.ToArray();
    }

    public static bool TryReadVisit(ReadOnlySpan<byte> payload, out ushort keeperId)
    {
        keeperId = 0;
        if (payload.Length < VisitRequestSize) return false;
        keeperId = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static byte[] List(IReadOnlyList<PlayerShopListedSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var w = new PacketWriter(8 + slots.Count * 32);
        w.WriteUInt32((uint)slots.Count);
        foreach (var listed in slots)
        {
            w.WriteUInt32((uint)listed.Slot.UnitPrice);
            w.WriteUInt16(listed.Slot.Count);
            if (listed.Item is not { } item)
            {
                w.WriteByte(MissingItem);
                continue;
            }
            w.WriteByte(item.ItemClass);
            w.WriteUInt32(item.ItemId);
            w.WriteUInt64(item.WireUid);
            w.WriteUInt16(item.Count);
            if (item.ItemClass != ItemParamEntry.RareClass) continue;
            w.WriteByte(item.RefineType);
            w.WriteByte(item.RefineLevel);
            w.WriteByte(item.RefineState);
            w.WriteByte(item.MixSlot);
        }
        var withProperties = slots.Where(listed => listed.Item is not null && listed.Properties.Count > 0).ToArray();
        w.WriteUInt16((ushort)withProperties.Length);
        foreach (var listed in withProperties)
        {
            w.WriteUInt64(listed.Slot.WireUid);
            w.WriteByte((byte)listed.Properties.Count);
            foreach (var property in listed.Properties)
            {
                w.WriteUInt32((uint)property.Type);
                w.WriteUInt16((ushort)property.Id);
                w.WriteInt16(property.Value);
                w.WriteUInt32(property.Extra);
            }
        }
        return w.ToArray();
    }

    public static bool TryReadBuy(ReadOnlySpan<byte> payload, out PlayerShopPurchase purchase)
    {
        purchase = default;
        if (payload.Length < BuyRequestSize) return false;
        var r = new PacketReader(payload);
        purchase = new PlayerShopPurchase(r.ReadUInt16(), r.ReadUInt64(), r.ReadUInt32(), r.ReadUInt16(), r.ReadUInt32());
        return true;
    }

    public static byte[] Bought(uint money, BagOperation operation)
    {
        var w = new PacketWriter(28);
        w.WriteUInt32(money);
        w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static byte[] Sold(uint money, ushort count, BagOperation operation)
    {
        var w = new PacketWriter(30);
        w.WriteUInt32(money);
        w.WriteUInt16(count);
        w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }
}
