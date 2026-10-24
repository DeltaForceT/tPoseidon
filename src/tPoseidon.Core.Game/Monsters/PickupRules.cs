using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public static class PickupRules
{
    public const int RequestSize = 3;
    public const int RequestMoveTypeOffset = 2;

    public const double ReachInCells = 4.0;
    public const int BagCapacity = 300;

    public const int WalkFirstRing = 3;
    public const int WalkReach = 7;

    public const byte StackGrew = 1;
    public const byte ItemCreated = 4;

    public static double CellDistance(int fromX, int fromY, int toX, int toY)
    {
        double dx = ZoneGeometry.CellOf(fromX) - ZoneGeometry.CellOf(toX);
        double dy = ZoneGeometry.CellOf(fromY) - ZoneGeometry.CellOf(toY);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool IsWithinReach(int fromX, int fromY, int toX, int toY) =>
        CellDistance(fromX, fromY, toX, toY) <= ReachInCells;

    public static bool MayTake(FieldItem item, ushort playerEntityId, DateTimeOffset now)
    {
        var owner = GroundItemField.OwnerAt(item, now);
        return owner == FieldItem.NoOwner || owner == playerEntityId;
    }

    public static (ushort X, ushort Y) WalkEnd(ZoneGeometry? geometry, FieldItem item, Random rng)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rng);
        if (geometry is null) return (item.X, item.Y);

        var cell = DropPlacement.FindFreeCell(
            geometry, ZoneGeometry.CellOf(item.X), ZoneGeometry.CellOf(item.Y),
            WalkFirstRing, WalkReach, new HashSet<(int X, int Y)>(), rng);
        return cell is { } found
            ? ((ushort)ZoneGeometry.CentreOf(found.X), (ushort)ZoneGeometry.CentreOf(found.Y))
            : (item.X, item.Y);
    }

    public static int AcceptableCount(int maxWeight, int bagWeight, int itemWeight, int count)
    {
        var free = maxWeight - bagWeight;
        if (free < 1 || itemWeight < 0 || free < itemWeight) return 0;
        var acceptable = itemWeight > 0 ? free / itemWeight : count;
        return count < acceptable ? count : acceptable;
    }

    public static bool Fits(
        int maxWeight, int bagWeight, int bagItems, bool joinsStack, int itemWeight, int count)
    {
        if (AcceptableCount(maxWeight, bagWeight, itemWeight, count) < 1) return false;
        var added = itemWeight * count;
        var carried = maxWeight >= bagWeight + added;
        return joinsStack ? carried : bagItems < BagCapacity && (added == 0 || carried);
    }

    public static bool MoneyFits(long held, long amount, long limit)
    {
        if (amount < 0 || held < 0 || held > limit) return false;
        return amount <= limit - held;
    }
}
