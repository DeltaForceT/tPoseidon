namespace tPoseidon.Core.Data;

public enum SoulSeedUseError
{
    None = 0,
    AlreadyActive = 104,
    SlotsFull = 106,
}

public sealed record SoulSeedState(int SeedItemIndex, DateTimeOffset StartAt, DateTimeOffset LastUpdateAt, IReadOnlyList<short> Points);

public interface ISoulSeedStateStore
{
    SoulSeedUseError Use(ulong characterId, int seedItemIndex);

    bool SavePoints(ulong characterId, IReadOnlyList<short> points);

    SoulSeedState? LoadActive(ulong characterId);
}
