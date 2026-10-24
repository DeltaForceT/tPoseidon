using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public enum RandomSpotKind
{
    Monster,
    Treasure,
}

public readonly record struct RandomSpotRoll(RandomSpotKind Kind, int Id);

public static class RandomSpot
{
    public const double BaseChance = 0.005;
    public const int RandMax = 32767;
    public const int MaxTreasureCount = 64;

    public static RandomSpotEntry? RowFor(TreasureMapTables tables, ushort mapId)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return tables.ZoneRegions.TryGetValue(mapId, out var region) && tables.RandomSpots.TryGetValue(region, out var row) ? row : null;
    }

    public static double Chance(float drillRatio, float addedRatio) => BaseChance + drillRatio + addedRatio;

    public static bool Triggers(double chance, int rand) => chance > rand / (double)RandMax;

    public static RandomSpotRoll? Choose(RandomSpotEntry row, int rand)
    {
        ArgumentNullException.ThrowIfNull(row);
        var monsterSum = row.Monsters.Sum(monster => (double)monster.Ratio);
        var total = monsterSum + row.TreasureRatio;
        if (total <= 0) return null;
        var roll = rand / (double)RandMax;
        if (roll > monsterSum / total) return new RandomSpotRoll(RandomSpotKind.Treasure, row.TreasureId);
        var cumulative = 0.0;
        foreach (var (monsterId, ratio) in row.Monsters)
        {
            cumulative += ratio;
            if (cumulative / total > roll) return new RandomSpotRoll(RandomSpotKind.Monster, monsterId);
        }
        return null;
    }

    public static bool CountAllowed(int count) => count >= 1 && count <= MaxTreasureCount;
}
