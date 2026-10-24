using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public readonly record struct PetCollectionStartFrame(ulong PetWireUid, ulong FoodWireUid, ushort FoodCount);

public readonly record struct PetBagEntry(byte ItemClass, uint ItemId, ulong ItemUid, ushort Count);

public static class PetCollectionFrames
{
    public const int StartSize = 2 * sizeof(ulong) + sizeof(ushort);
    public const int ItemRequestSize = sizeof(ulong);
    public const ushort FoodNotSpent = 0;
    public const ushort FoodSpent = 1;
    public const int RareEntryStateBytes = 4;
    private const int RecordCapacity = 24;

    public static bool TryReadStart(ReadOnlySpan<byte> payload, out PetCollectionStartFrame request)
    {
        request = default;
        if (payload.Length < StartSize) return false;
        var r = new PacketReader(payload);
        request = new PetCollectionStartFrame(r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt16());
        return true;
    }

    public static bool TryReadItem(ReadOnlySpan<byte> payload, out ulong itemUid)
    {
        itemUid = 0;
        if (payload.Length < ItemRequestSize) return false;
        var r = new PacketReader(payload);
        itemUid = r.ReadUInt64();
        return true;
    }

    public static byte[] Code(byte code) => new[] { code };

    public static byte[] Collected(BagOperation collected, BagOperation? food)
    {
        var w = new PacketWriter(2 * RecordCapacity + sizeof(ushort));
        w.WriteBytes(QuestResultFrames.ItemOperation(collected));
        w.WriteUInt16(food is null ? FoodNotSpent : FoodSpent);
        if (food is { } spent) w.WriteBytes(QuestResultFrames.ItemOperation(spent));
        return w.ToArray();
    }

    public static byte[] Moved(BagOperation petBag, BagOperation bag)
    {
        var w = new PacketWriter(2 * RecordCapacity);
        w.WriteBytes(QuestResultFrames.ItemOperation(petBag));
        w.WriteBytes(QuestResultFrames.ItemOperation(bag));
        return w.ToArray();
    }

    public static byte[] Destroyed(BagOperation removal) => QuestResultFrames.ItemOperation(removal);

    public static byte[] Bag(IReadOnlyList<PetBagEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var w = new PacketWriter(sizeof(ushort) + entries.Count * RecordCapacity);
        w.WriteUInt16((ushort)entries.Count);
        foreach (var entry in entries)
        {
            w.WriteByte(entry.ItemClass);
            w.WriteUInt32(entry.ItemId);
            w.WriteUInt64(entry.ItemUid);
            w.WriteUInt16(entry.Count);
            if (entry.ItemClass != ItemParamEntry.RareClass) continue;
            w.WriteZero(RareEntryStateBytes);
            w.WriteByte(QuestResultFrames.NoOptions);
        }
        return w.ToArray();
    }
}
