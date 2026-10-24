using tPoseidon.Core.Data;

namespace tPoseidon.Core.DataTests;

public class InMemoryUniqueItemStoreTests
{
    private const ulong AbsoluteHolder = 0x100;
    private const ulong NormalHolder = 0x200;
    private static readonly DateTimeOffset PickedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static InMemoryUniqueItemStore Store() => new(new[]
    {
        new UniqueItemInfo(50000, UniqueItemRules.AbsoluteUnique, 5, 0, 1, 0, 500000, 0, UniqueItemRules.Transferred, 0),
        new UniqueItemInfo(50001, UniqueItemRules.NormalUnique, 99999, 0, 1, 0, 100, 0, UniqueItemRules.Transferred, 0),
    });

    [Fact]
    public void NormalUniqueHolderIsNeverDeprivedByTheDutyCheck()
    {
        var store = Store();
        var pickup = store.PickedUp(NormalHolder, 0x5001, 50001, PickedAt);
        Assert.Equal(UniqueItemRules.NormalUnique, pickup!.UniqueType);
        Assert.Null(pickup.DutyEnd);
        Assert.False(store.HasDuty(NormalHolder));

        var outcome = store.ApplyDuty(true, PickedAt + TimeSpan.FromDays(30));
        Assert.Empty(outcome.Failed);
        Assert.Empty(outcome.Accomplished);
        Assert.Empty(store.DepriveAll(NormalHolder));
        Assert.Empty(store.DepriveSingle(NormalHolder, new[] { new UniqueHeldItem(0x5001, 50001) }));
    }

    [Fact]
    public void AbsoluteUniqueHolderWithoutBossSealsLosesThemAfterSevenDays()
    {
        var store = Store();
        var pickup = store.PickedUp(AbsoluteHolder, 0x5000, 50000, PickedAt);
        Assert.Equal(PickedAt + UniqueItemRules.DutyPeriod, pickup!.DutyEnd);

        Assert.Empty(store.ApplyDuty(true, PickedAt + TimeSpan.FromDays(6)).Failed);
        var outcome = store.ApplyDuty(true, PickedAt + TimeSpan.FromDays(7));
        Assert.Equal(new[] { AbsoluteHolder }, outcome.Failed);
        Assert.Empty(store.HeldBy(AbsoluteHolder));
        Assert.False(store.HasDuty(AbsoluteHolder));
    }

    [Fact]
    public void BossSealsRenewTheDutyForSevenMoreDays()
    {
        var store = Store();
        store.PickedUp(AbsoluteHolder, 0x5000, 50000, PickedAt);
        Assert.False(store.BossKilled(AbsoluteHolder, 2214, PickedAt + TimeSpan.FromDays(1)));
        Assert.True(store.BossKilled(AbsoluteHolder, 3001, PickedAt + TimeSpan.FromDays(1)));
        var checkedAt = PickedAt + TimeSpan.FromDays(7);
        var outcome = store.ApplyDuty(true, checkedAt);
        Assert.Empty(outcome.Failed);
        Assert.Equal(new[] { (AbsoluteHolder, checkedAt + UniqueItemRules.DutyPeriod) }, outcome.Accomplished);
        Assert.Single(store.HeldBy(AbsoluteHolder));
    }

    [Fact]
    public void SingleWithdrawTakesOnlyHeldAbsoluteUniques()
    {
        var store = Store();
        store.PickedUp(AbsoluteHolder, 0x5000, 50000, PickedAt);
        Assert.Empty(store.DepriveSingle(AbsoluteHolder, new[] { new UniqueHeldItem(0x5000, 50001) }));
        Assert.Single(store.DepriveSingle(AbsoluteHolder, new[] { new UniqueHeldItem(0x5000, 50000) }));
        Assert.False(store.HasDuty(AbsoluteHolder));
    }

    [Fact]
    public void DutyCheckRunsAtMostOncePerHour()
    {
        Assert.True(UniqueItemRules.DutyCheckDue(null, PickedAt));
        Assert.False(UniqueItemRules.DutyCheckDue(PickedAt, PickedAt + TimeSpan.FromMilliseconds(3_600_000)));
        Assert.True(UniqueItemRules.DutyCheckDue(PickedAt, PickedAt + TimeSpan.FromMilliseconds(3_600_001)));
        Assert.True(UniqueItemRules.CarriesDuty(UniqueItemRules.AbsoluteUnique));
        Assert.False(UniqueItemRules.CarriesDuty(UniqueItemRules.NormalUnique));
        Assert.False(UniqueItemRules.CarriesDuty(UniqueItemRules.EventUnique));
    }
}
