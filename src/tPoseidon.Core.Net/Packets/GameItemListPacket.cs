using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_ENTITY_LIST)]
public sealed class GameItemListPacket
{
    public sealed class ItemEntry
    {
        public required uint ItemIndex { get; init; }
        public required uint ItemUid { get; init; }
        public uint FieldA { get; init; } = 1;
    }

    public sealed class EquipmentEntry
    {
        public required uint ItemIndex { get; init; }
        public required uint ItemUid { get; init; }
        public uint FieldA { get; init; } = 1;
        public uint FieldB { get; init; } = 1;
    }

    public sealed class ItemRecord
    {
        public required byte Class { get; init; }
        public required uint ItemIndex { get; init; }
        public required uint ItemUid { get; init; }
        public uint FieldA { get; init; } = 1;

        public ushort StackCount { get; init; }

        public ushort EquipmentCount { get; init; } = 1;

        public byte RefineType { get; init; }

        public byte RefineLevel { get; init; }

        public byte RefineState { get; init; }

        public byte MixSlot { get; init; }

        public IReadOnlyList<tPoseidon.Core.Data.ItemProperty> Options { get; init; } = Array.Empty<tPoseidon.Core.Data.ItemProperty>();

        public int WireSize => Class == 2 ? 20 + Options.Count * OptionSize : 15;
    }

    public IReadOnlyList<ItemRecord> Records { get; init; } = Array.Empty<ItemRecord>();

    public byte[] EncodePayload()
    {
        if (Records.Count > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"GameItemList supports max {ushort.MaxValue} entries, got {Records.Count}.");
        }

        var totalSize = 2;
        foreach (var r in Records) totalSize += r.WireSize;
        var w = new PacketWriter(totalSize);

        w.WriteUInt16((ushort)Records.Count);
        foreach (var r in Records) WriteRecord(w, r);
        return w.ToArray();
    }

    public static void WriteRecord(PacketWriter w, ItemRecord r)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(r);
        if (r.Class is not 1 and not 2)
        {
            throw new InvalidOperationException(
                $"ItemRecord.Class must be 1 (Item) or 2 (Equipment); got {r.Class}.");
        }
        w.WriteByte(r.Class);
        w.WriteUInt32(r.ItemIndex);
        w.WriteUInt32(r.ItemUid);
        w.WriteUInt32(r.FieldA);
        if (r.Class == 2)
        {
            w.WriteUInt16(r.EquipmentCount);
            w.WriteByte(r.RefineType);
            w.WriteByte(r.RefineLevel);
            w.WriteByte(r.RefineState);
            w.WriteByte(r.MixSlot);
            w.WriteByte((byte)r.Options.Count);
            foreach (var option in r.Options) WriteOption(w, option);
        }
        else
        {
            w.WriteUInt16(r.StackCount);
        }
    }

    public static GameItemListPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
        {
            throw new InvalidPacketException(
                $"GameItemList payload must be at least 2 bytes (count), got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var count = r.ReadUInt16();
        var records = new List<ItemRecord>(count);
        for (var i = 0; i < count; i++)
        {
            var cls = r.ReadByte();
            var itemIndex = r.ReadUInt32();
            var itemUid = r.ReadUInt32();
            var fieldA = r.ReadUInt32();
            ushort stackCount = 0;
            ushort equipmentCount = 0;
            byte refineType = 0;
            byte refineLevel = 0;
            byte refineState = 0;
            byte mixSlot = 0;
            var options = new List<tPoseidon.Core.Data.ItemProperty>();
            if (cls == 2)
            {
                equipmentCount = r.ReadUInt16();
                refineType = r.ReadByte();
                refineLevel = r.ReadByte();
                refineState = r.ReadByte();
                mixSlot = r.ReadByte();
                var optionCount = r.ReadByte();
                for (var o = 0; o < optionCount; o++)
                {
                    var type = (int)r.ReadUInt32();
                    var id = r.ReadUInt16();
                    var value = (short)r.ReadUInt16();
                    options.Add(new tPoseidon.Core.Data.ItemProperty(type, id, value, r.ReadUInt32()));
                }
            }
            else if (cls == 1)
            {
                stackCount = r.ReadUInt16();
            }
            else
            {
                throw new InvalidPacketException(
                    $"GameItemList record {i} has unknown class {cls}; expected 1 or 2.");
            }
            records.Add(new ItemRecord
            {
                Class = cls,
                ItemIndex = itemIndex,
                ItemUid = itemUid,
                FieldA = fieldA,
                StackCount = stackCount,
                EquipmentCount = equipmentCount,
                RefineType = refineType,
                RefineLevel = refineLevel,
                RefineState = refineState,
                MixSlot = mixSlot,
                Options = options,
            });
        }
        return new GameItemListPacket { Records = records };
    }

    public const int OptionSize = 12;

    public static void WriteOption(PacketWriter w, tPoseidon.Core.Data.ItemProperty option)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteUInt32((uint)option.Type);
        w.WriteUInt16((ushort)option.Id);
        w.WriteUInt16((ushort)option.Value);
        w.WriteUInt32(option.Extra);
    }
}
