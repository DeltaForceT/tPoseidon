using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public enum RecycleKind : ushort
{
    Inventory = 0,
    MyCamp = 1,
    CouponExchange = 2,
}

public enum CouponExchangeRefusal
{
    NoRow,
    SourceMismatch,
    SourceNotInBag,
    SourceCountTooLow,
    NoBagSpace,
}

public readonly record struct CouponExchangeRequest(uint SourceItemId, ulong SourceWireUid, ushort SourceCount, uint ResultItemId, ulong ResultWireUid, ushort ResultCount);

public readonly record struct ItemRecycleEntry(ulong WireUid, uint ItemId);

public readonly record struct ItemRecycleRequest(uint Reserved, ushort CouponTotal, IReadOnlyList<ItemRecycleEntry> Entries);

public readonly record struct MyCampRecycleEntry(ulong ItemUid, ushort ItemId);

public readonly record struct MyCampRecycleRequest(uint CouponItemId, ushort CouponCount, IReadOnlyList<MyCampRecycleEntry> Entries);

public static class RecycleRules
{
    public const int RecyclableOption = 0x1;
    public const int RecyclableOptionA = 0x40000;
    public const int RecyclableOptionB = 0x4000000;
    public const int MyCampRecycleType = 40;
    public const ushort MyCampItemRefused = 2;
    public const ushort MyCampExchangeFailed = 1;

    private static readonly Dictionary<int, (int Plain, int Marked)> TypeByItemType = new()
    {
        [2] = (0x00, 0x14),
        [3] = (0x01, 0x15),
        [4] = (0x02, 0x16),
        [5] = (0x0C, 0x20),
        [6] = (0x04, 0x18),
        [7] = (0x05, 0x19),
        [8] = (0x06, 0x1A),
        [0x0D] = (0x03, 0x17),
        [0x11] = (0x0A, 0x1E),
        [0x12] = (0x0B, 0x1F),
        [0x13] = (0x0D, 0x21),
        [0x14] = (0x0E, 0x22),
        [0x15] = (0x0F, 0x23),
        [0x16] = (0x10, 0x24),
        [0x17] = (0x11, 0x25),
        [0x18] = (0x07, 0x1B),
        [0x19] = (0x08, 0x1C),
        [0x1A] = (0x12, 0x26),
        [0x1B] = (0x13, 0x27),
        [0x22] = (0x09, 0x1D),
    };

    public static int? RecycleTypeOf(int itemType, int options)
    {
        var plain = (options & (RecyclableOption | RecyclableOptionA)) == (RecyclableOption | RecyclableOptionA);
        if (!plain) return null;
        var marked = (options & RecyclableOptionB) != 0;
        return TypeByItemType.TryGetValue(itemType, out var types) ? (marked ? types.Marked : types.Plain) : null;
    }

    public const ushort CouponExchangeRefusesSkills = 0x0A;

    public static CouponExchangeRefusal? Check(
        CouponExchangeRequest request,
        RecycleChangeEntry? row,
        int? heldItemId,
        int heldCount,
        bool resultFits)
    {
        if (row is null) return CouponExchangeRefusal.NoRow;
        if (row.ExchangeSrc != request.SourceItemId || row.ExchangeSrcCnt != request.SourceCount) return CouponExchangeRefusal.SourceMismatch;
        if (heldItemId != (int)request.SourceItemId) return CouponExchangeRefusal.SourceNotInBag;
        if (heldCount < request.SourceCount) return CouponExchangeRefusal.SourceCountTooLow;
        if (!resultFits) return CouponExchangeRefusal.NoBagSpace;
        return null;
    }
}
