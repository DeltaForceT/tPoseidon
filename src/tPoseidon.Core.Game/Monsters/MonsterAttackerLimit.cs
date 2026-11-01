namespace tPoseidon.Core.Game.Monsters;

public static class MonsterAttackerLimit
{
    public const int DefaultLimit = 5;
    public const int ZoneRowLimited = 1;
    public const int ZoneRowUnlimited = 2;

    public static int? Resolve(bool hasZoneRow, int zoneRowType, int zoneRowCount, bool paramCounts, int paramCount)
    {
        if (hasZoneRow && zoneRowType == ZoneRowLimited) return zoneRowCount;
        if (hasZoneRow && zoneRowType == ZoneRowUnlimited) return null;
        return paramCounts ? paramCount : DefaultLimit;
    }

    public static bool Exceeded(int? limit, int otherAttackers) =>
        limit is { } cap && otherAttackers >= 1 && otherAttackers >= cap;
}
