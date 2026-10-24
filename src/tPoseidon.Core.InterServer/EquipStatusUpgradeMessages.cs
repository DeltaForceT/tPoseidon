using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class EquipStatusUpgradeMessages
{
    public static byte[] EncodeRequest(EquipStatusUpgrade upgrade)
    {
        ArgumentNullException.ThrowIfNull(upgrade);
        var w = new PacketWriter(16 + upgrade.Items.Count * 24);
        w.WriteUInt64(upgrade.CharacterId);
        w.WriteUInt32(upgrade.Fee);
        w.WriteInt32(upgrade.BagCapacity);
        WriteItems(w, upgrade.Items);
        return w.ToArray();
    }

    public static EquipStatusUpgrade DecodeRequest(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var characterId = r.ReadUInt64();
        var fee = r.ReadUInt32();
        var bagCapacity = r.ReadInt32();
        return new EquipStatusUpgrade(characterId, fee, bagCapacity, ReadItems(ref r));
    }

    public static byte[] EncodeResult(EquipStatusUpgradeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var w = new PacketWriter(8 + result.Items.Count * 24);
        w.WriteUInt32(result.RemainMoney);
        WriteItems(w, result.Items);
        return w.ToArray();
    }

    public static EquipStatusUpgradeResult DecodeResult(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var remain = r.ReadUInt32();
        return new EquipStatusUpgradeResult(EquipStatusUpgradeRules.Ok, remain, ReadItems(ref r));
    }

    public static byte[] EncodeError(int error)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(error);
        return w.ToArray();
    }

    public static int DecodeError(ReadOnlySpan<byte> body) =>
        body.Length >= sizeof(int) ? new PacketReader(body).ReadInt32() : EquipStatusUpgradeRules.StoreFailed;

    private static void WriteItems(PacketWriter w, IReadOnlyList<EquipUpgradeItem> items)
    {
        w.WriteUInt16((ushort)items.Count);
        foreach (var item in items)
        {
            w.WriteByte((byte)item.Action);
            w.WriteUInt64(item.ItemUid);
            w.WriteInt32(item.ItemIndex);
            w.WriteByte(item.ItemClass);
            w.WriteUInt16(item.Count);
            w.WriteByte(item.Slot);
            w.WriteInt16(item.RefineType);
            w.WriteByte(item.RefineLevel);
            w.WriteByte(item.RefineState);
        }
    }

    private static List<EquipUpgradeItem> ReadItems(ref PacketReader r)
    {
        var count = r.ReadUInt16();
        var items = new List<EquipUpgradeItem>(count);
        for (var i = 0; i < count; i++)
        {
            items.Add(new EquipUpgradeItem((EquipUpgradeAction)r.ReadByte(), r.ReadUInt64(), r.ReadInt32(), r.ReadByte(), r.ReadUInt16(),
                r.ReadByte(), r.ReadInt16(), r.ReadByte(), r.ReadByte()));
        }
        return items;
    }
}
