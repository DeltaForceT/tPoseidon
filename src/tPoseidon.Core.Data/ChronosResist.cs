namespace tPoseidon.Core.Data;

public static class ChronosResist
{
    public const int MonsterId = 16718;
    public const int GrowthPerBar = 2;

    public static BossLifeProfile Adjusted(int monsterId, BossLifeProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return monsterId == MonsterId
            ? profile with { ResistGrowthMin = GrowthPerBar, ResistGrowthMax = GrowthPerBar }
            : profile;
    }
}
