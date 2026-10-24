using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public enum ItemStoreSubOp : byte
{
    GrantToCharacter = 0x01,
    GetAllForCharacter = 0x02,
    GetForCharacterBySlotKind = 0x03,
    FindByUid = 0x04,
    FindBySlot = 0x05,
    MoveToSlot = 0x06,
    Delete = 0x07,
    AdjustStack = 0x08,
    UnassignFromCharacter = 0x09,
    AssignToCharacter = 0x0A,
    SetStack = 0x0B,
}

public static class ItemStoreWire
{
    public static byte[] EncodeItem(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var writer = new PacketWriter(48);
        WriteItem(writer, item);
        return writer.ToArray();
    }

    public static Item DecodeItem(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return ReadItem(ref reader);
    }

    public static byte[] EncodeItemList(IReadOnlyList<Item> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count > ushort.MaxValue)
        {
            throw new InvalidPacketException($"item list has {list.Count} entries; exceeds u16 count max.");
        }
        var writer = new PacketWriter(2 + (list.Count * 48));
        writer.WriteUInt16((ushort)list.Count);
        foreach (var i in list)
        {
            WriteItem(writer, i);
        }
        return writer.ToArray();
    }

    public static IReadOnlyList<Item> DecodeItemList(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var count = reader.ReadUInt16();
        var list = new List<Item>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(ReadItem(ref reader));
        }
        return list;
    }

    private static void WriteItem(PacketWriter writer, Item item)
    {
        writer.WriteUInt64(item.ItemUid);
        writer.WriteInt32(item.ItemIndex);
        writer.WriteByte(item.ItemClass);
        writer.WriteUInt16(item.Stack);
        writer.WriteInt16(item.ItemAttr);
        writer.WriteByte(item.ItemCharged);
        writer.WriteInt16(item.RefineType);
        writer.WriteByte(item.RefineLevel);
        writer.WriteByte(item.RefineState);
        writer.WriteByte(item.ItemSlot);
        writer.WriteInt64(item.BreakedAt.UtcTicks);
        writer.WriteUInt64(item.CharacterId);
        writer.WriteByte(item.SlotKind);
        writer.WriteByte(item.SlotPosition);
    }

    private static Item ReadItem(ref PacketReader reader) => new()
    {
        ItemUid = reader.ReadUInt64(),
        ItemIndex = reader.ReadInt32(),
        ItemClass = reader.ReadByte(),
        Stack = reader.ReadUInt16(),
        ItemAttr = reader.ReadInt16(),
        ItemCharged = reader.ReadByte(),
        RefineType = reader.ReadInt16(),
        RefineLevel = reader.ReadByte(),
        RefineState = reader.ReadByte(),
        ItemSlot = reader.ReadByte(),
        BreakedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        CharacterId = reader.ReadUInt64(),
        SlotKind = reader.ReadByte(),
        SlotPosition = reader.ReadByte(),
    };
}
