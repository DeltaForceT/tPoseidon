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
    public const float LandingReach = 80f;
    public const float BackoffStep = 4f;
    public const int CellSize = 16;

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

    public static (ushort X, ushort Y) Landing(ushort fromX, ushort fromY, short toX, short toY, Func<int, int, bool> isCellOpen)
    {
        ArgumentNullException.ThrowIfNull(isCellOpen);
        var dx = (float)(toX - fromX);
        var dy = (float)(toY - fromY);
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        var aimX = (int)toX;
        var aimY = (int)toY;
        if (LandingReach < distance)
        {
            var scale = LandingReach / distance;
            dx *= scale;
            dy *= scale;
            aimX = fromX + (int)dx;
            aimY = fromY + (int)dy;
            distance = LandingReach;
        }
        if (IsOpen(aimX, aimY, isCellOpen)) return Pack(aimX, aimY, fromX, fromY);
        var remaining = distance;
        while (true)
        {
            remaining -= BackoffStep;
            var fraction = remaining / distance;
            if (remaining <= 0f || fraction <= 0f) return (fromX, fromY);
            aimX = fromX + (int)(dx * fraction);
            aimY = fromY + (int)(dy * fraction);
            if (IsOpen(aimX, aimY, isCellOpen)) return Pack(aimX, aimY, fromX, fromY);
        }
    }

    private static bool IsOpen(int x, int y, Func<int, int, bool> isCellOpen)
        => isCellOpen(x / CellSize, y / CellSize);

    private static (ushort X, ushort Y) Pack(int x, int y, ushort fallbackX, ushort fallbackY)
        => x is >= 0 and <= ushort.MaxValue && y is >= 0 and <= ushort.MaxValue
            ? ((ushort)x, (ushort)y)
            : (fallbackX, fallbackY);
}
