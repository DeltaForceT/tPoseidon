using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public readonly record struct GroundItemSighting(
    ushort EntityId,
    ushort X,
    ushort Y,
    bool IsHidden,
    byte ItemClass,
    uint ItemId,
    ushort Count,
    ushort SourceEntityId,
    ushort? FromX,
    ushort? FromY);

public readonly record struct GroundItemPickup(ushort PickerEntityId, ushort ItemEntityId, byte ItemClass, uint ItemId, ulong ItemUid, ushort Count);

public static class GroundItemFrames
{
    public const ushort NoSourceEntity = 0;

    public const byte RestingState = 0;
    public const byte RestingFacing = 0x80;
    public const byte RestingSpeed = 0;
    public const int RareItemExtraBytes = 4;
    public const ulong NoUid = 0;
    public const byte RemovedPlainly = 0;
    public const byte NotUnique = 0;
    public const byte NoOptions = 0;
    public const ushort MoneyPickupCount = 1;
    public const ushort MoneyFullCode = 1;

    public const int DroppedSize = 28;
    public const int AlreadyThereSize = 26;
    public const int RemovedSize = 3;
    public const int PickedUpSize = 19;
    public const int AddedToBagSize = 17;
    public const int MoneySize = 4;
    public const int MoneyFullSize = 2;
    public const int StoppedSize = 7;

    public static byte[] Dropped(FieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var w = new PacketWriter(DroppedSize + RareItemExtraBytes);
        w.WriteUInt16(item.DropperEntityId);
        WriteActor(w, item);
        return w.ToArray();
    }

    public static byte[] AlreadyThere(FieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var w = new PacketWriter(AlreadyThereSize + RareItemExtraBytes);
        WriteActor(w, item);
        return w.ToArray();
    }

    public static byte[] Removed(ushort itemEntityId)
    {
        var w = new PacketWriter(RemovedSize);
        w.WriteUInt16(itemEntityId);
        w.WriteByte(RemovedPlainly);
        return w.ToArray();
    }

    public static ushort ReadRemoved(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != RemovedSize)
        {
            throw new InvalidPacketException($"Removal payload must be exactly {RemovedSize} bytes, got {payload.Length}.");
        }
        return new PacketReader(payload).ReadUInt16();
    }

    public static byte[] PickedUp(ushort playerEntityId, FieldItem item, ushort count)
    {
        ArgumentNullException.ThrowIfNull(item);
        var w = new PacketWriter(PickedUpSize + RareItemExtraBytes);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt16(item.EntityId);
        WriteItem(w, item.ItemClass, (uint)item.ItemId, NoUid, count);
        return w.ToArray();
    }

    public static byte[] AddedToBag(byte kind, byte itemClass, uint itemId, ulong itemUid, ushort count, byte mixSlot = 0)
    {
        var w = new PacketWriter(AddedToBagSize + RareItemExtraBytes + 1);
        w.WriteByte(kind);
        WriteItem(w, itemClass, itemId, itemUid, count, mixSlot);
        if (itemClass == ItemParamEntry.RareClass && kind != PickupRules.StackGrew) w.WriteByte(NoOptions);
        w.WriteByte(NotUnique);
        return w.ToArray();
    }

    public static BagOperation ReadAddedToBag(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var kind = r.ReadByte();
        var itemClass = r.ReadByte();
        var itemId = r.ReadUInt32();
        var itemUid = r.ReadUInt64();
        var count = r.ReadUInt16();
        byte mixSlot = 0;
        if (itemClass == ItemParamEntry.RareClass)
        {
            r.Skip(RareItemExtraBytes - 1);
            mixSlot = r.ReadByte();
            if (kind != PickupRules.StackGrew)
            {
                var options = r.ReadByte();
                if (options != NoOptions)
                {
                    throw new InvalidPacketException($"The bag addition of item {itemId} lists {options} options; additions carry none.");
                }
            }
        }
        r.ReadByte();
        if (r.Remaining != 0)
        {
            throw new InvalidPacketException($"The bag addition payload has {r.Remaining} bytes after its item.");
        }
        return new BagOperation(kind, itemClass, itemId, itemUid, count, mixSlot);
    }

    public static byte[] Money(long total)
    {
        var w = new PacketWriter(MoneySize);
        w.WriteUInt32Field(total);
        return w.ToArray();
    }

    public static uint ReadMoney(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != MoneySize)
        {
            throw new InvalidPacketException($"Money payload must be exactly {MoneySize} bytes, got {payload.Length}.");
        }
        return new PacketReader(payload).ReadUInt32();
    }

    public static byte[] MoneyFull()
    {
        var w = new PacketWriter(MoneyFullSize);
        w.WriteUInt16(MoneyFullCode);
        return w.ToArray();
    }

    public static byte[] Stopped(ushort playerEntityId, ushort x, ushort y, byte moveType)
    {
        var w = new PacketWriter(StoppedSize);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        w.WriteByte(moveType);
        return w.ToArray();
    }

    public static byte[] DugUp(FieldItem item, ushort fromX, ushort fromY)
    {
        ArgumentNullException.ThrowIfNull(item);
        var w = new PacketWriter(DroppedSize + 4 + RareItemExtraBytes);
        w.WriteUInt16(item.OwnerEntityId);
        w.WriteUInt16(item.EntityId);
        w.WriteUInt16(item.X);
        w.WriteUInt16(item.Y);
        w.WriteUInt16(fromX);
        w.WriteUInt16(fromY);
        w.WriteUInt16(item.IsHidden ? (ushort)1 : (ushort)0);
        WriteResting(w, item);
        return w.ToArray();
    }

    public static byte[] Thrown(FieldItem item, ushort fromX, ushort fromY)
    {
        ArgumentNullException.ThrowIfNull(item);
        var w = new PacketWriter(DroppedSize + 2 + RareItemExtraBytes);
        w.WriteUInt16(item.EntityId);
        w.WriteUInt16(item.X);
        w.WriteUInt16(item.Y);
        w.WriteUInt16(fromX);
        w.WriteUInt16(fromY);
        w.WriteUInt16(0);
        WriteResting(w, item);
        return w.ToArray();
    }

    public static GroundItemSighting ReadDropped(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var dropper = r.ReadUInt16();
        return ReadActor(ref r, dropper, "drop");
    }

    public static GroundItemSighting ReadAlreadyThere(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        return ReadActor(ref r, NoSourceEntity, "item on the ground");
    }

    public static GroundItemSighting ReadThrown(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        return ReadFlight(ref r, NoSourceEntity, "thrown item");
    }

    public static GroundItemSighting ReadDugUp(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var owner = r.ReadUInt16();
        return ReadFlight(ref r, owner, "dug-up item");
    }

    public static GroundItemPickup ReadPickedUp(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var picker = r.ReadUInt16();
        var itemEntityId = r.ReadUInt16();
        var (itemClass, itemId, itemUid, count) = ReadItem(ref r);
        EnsureEnd(ref r, "pickup");
        return new GroundItemPickup(picker, itemEntityId, itemClass, itemId, itemUid, count);
    }

    private static GroundItemSighting ReadFlight(ref PacketReader r, ushort source, string what)
    {
        var entityId = r.ReadUInt16();
        var x = r.ReadUInt16();
        var y = r.ReadUInt16();
        var fromX = r.ReadUInt16();
        var fromY = r.ReadUInt16();
        var hidden = r.ReadUInt16() != 0;
        var item = ReadResting(ref r, what);
        return new GroundItemSighting(entityId, x, y, hidden, item.ItemClass, item.ItemId, item.Count, source, fromX, fromY);
    }

    private static GroundItemSighting ReadActor(ref PacketReader r, ushort source, string what)
    {
        var entityId = r.ReadUInt16();
        var x = r.ReadUInt16();
        var y = r.ReadUInt16();
        var hidden = r.ReadUInt16() != 0;
        var item = ReadResting(ref r, what);
        return new GroundItemSighting(entityId, x, y, hidden, item.ItemClass, item.ItemId, item.Count, source, null, null);
    }

    private static (byte ItemClass, uint ItemId, ushort Count) ReadResting(ref PacketReader r, string what)
    {
        r.ReadByte();
        r.ReadByte();
        r.ReadByte();
        var (itemClass, itemId, _, count) = ReadItem(ref r);
        EnsureEnd(ref r, what);
        return (itemClass, itemId, count);
    }

    private static (byte ItemClass, uint ItemId, ulong ItemUid, ushort Count) ReadItem(ref PacketReader r)
    {
        var itemClass = r.ReadByte();
        var itemId = r.ReadUInt32();
        var itemUid = r.ReadUInt64();
        var count = r.ReadUInt16();
        if (itemClass == ItemParamEntry.RareClass) r.Skip(RareItemExtraBytes);
        return (itemClass, itemId, itemUid, count);
    }

    private static void EnsureEnd(ref PacketReader r, string what)
    {
        if (r.Remaining != 0)
        {
            throw new InvalidPacketException($"The {what} payload has {r.Remaining} bytes after its item.");
        }
    }

    private static void WriteActor(PacketWriter w, FieldItem item)
    {
        w.WriteUInt16(item.EntityId);
        w.WriteUInt16(item.X);
        w.WriteUInt16(item.Y);
        w.WriteUInt16(item.IsHidden ? (ushort)1 : (ushort)0);
        WriteResting(w, item);
    }

    private static void WriteResting(PacketWriter w, FieldItem item)
    {
        w.WriteByte(RestingState);
        w.WriteByte(RestingFacing);
        w.WriteByte(RestingSpeed);
        WriteItem(w, item.ItemClass, (uint)item.ItemId, NoUid, (ushort)Math.Clamp(item.Count, 0, ushort.MaxValue));
    }

    private static void WriteItem(PacketWriter w, byte itemClass, uint itemId, ulong itemUid, ushort count, byte mixSlot = 0)
    {
        w.WriteByte(itemClass);
        w.WriteUInt32(itemId);
        w.WriteUInt64(itemUid);
        w.WriteUInt16(count);
        if (itemClass != ItemParamEntry.RareClass) return;
        w.WriteZero(RareItemExtraBytes - 1);
        w.WriteByte(mixSlot);
    }
}
