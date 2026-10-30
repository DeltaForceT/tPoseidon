using tPoseidon.Core.Game.Fortune;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.ShadowWorld;

public sealed record ShadowBonus(int RewardId, IReadOnlyList<int> Stats, DateTimeOffset Expires)
{
    public static ShadowBonus Of(ShadowRewardEntry reward, DateTimeOffset expires)
    {
        ArgumentNullException.ThrowIfNull(reward);
        var stats = new int[FortuneRules.StatSlots];
        Add(stats, "AP", reward.Ap);
        Add(stats, "AC", reward.Ac);
        Add(stats, "DX", reward.Dx);
        Add(stats, "MA", reward.Ma);
        Add(stats, "MD", reward.Md);
        Add(stats, "DA", reward.Da);
        Add(stats, "LK", reward.Lk);
        Add(stats, "DP", reward.Dp);
        Add(stats, "HV", reward.Hv);
        return new ShadowBonus(reward.Id, stats, expires);
    }

    public bool IsActive(DateTimeOffset now) => now < Expires;

    public int[] ActiveStats(DateTimeOffset now)
    {
        var stats = new int[FortuneRules.StatSlots];
        if (!IsActive(now)) return stats;
        for (var slot = 0; slot < stats.Length && slot < Stats.Count; slot++) stats[slot] = Stats[slot];
        return stats;
    }

    private static void Add(int[] stats, string column, int value)
    {
        if (FortuneRules.TryGetStatSlot(column, out var slot)) stats[slot] += value;
    }
}
