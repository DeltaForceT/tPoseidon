using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Items;

public readonly record struct ItemThrow(uint ItemId, ulong WireUid, ushort Count, short X, short Y);

public enum ThrowOutcome
{
    Refused,
    Ignored,
    WholeItem,
    PartOfStack,
}

public static class ItemThrowRules
{
    public const int BodySize = 18;
    public const double LandingReach = 80.0;

    public static bool TryRead(ReadOnlySpan<byte> body, out ItemThrow request)
    {
        request = default;
        if (body.Length < BodySize) return false;
        request = new ItemThrow(
            BitConverter.ToUInt32(body),
            BitConverter.ToUInt64(body[4..]),
            BitConverter.ToUInt16(body[12..]),
            BitConverter.ToInt16(body[14..]),
            BitConverter.ToInt16(body[16..]));
        return true;
    }

    public static ThrowOutcome Judge(Item? held, ulong characterId, ushort count, bool keptInBag)
    {
        if (keptInBag) return ThrowOutcome.Ignored;
        if (held is null || held.CharacterId != characterId || held.SlotKind != SlotKind.Inventory) return ThrowOutcome.Refused;
        if (count == 0 || count > held.WeightUnits) return ThrowOutcome.Refused;
        return count == held.WeightUnits ? ThrowOutcome.WholeItem : ThrowOutcome.PartOfStack;
    }

    public static (ushort X, ushort Y) Landing(ushort fromX, ushort fromY, short toX, short toY)
    {
        var dx = (double)toX - fromX;
        var dy = (double)toY - fromY;
        return toX >= 0 && toY >= 0 && Math.Sqrt(dx * dx + dy * dy) <= LandingReach
            ? ((ushort)toX, (ushort)toY)
            : (fromX, fromY);
    }
}
