using tPoseidon.Core.Game.Quests;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class MonsterHuntTests
{
    [Fact]
    public void AStartedHuntCountsDownAndReportsEverySecond()
    {
        var hunt = new MonsterHunt();
        hunt.Start(9001, 10_000);

        Assert.Equal(new HuntTick(false, false), hunt.Advance(400));
        Assert.Equal(new HuntTick(false, false), hunt.Advance(500));
        Assert.Equal(new HuntTick(true, false), hunt.Advance(100));
        Assert.Equal(9_000, hunt.RemainingMs);
        Assert.Equal(0, hunt.AccumulatorMs);
    }

    [Fact]
    public void TheHuntTimesOutWhenNoTimeIsLeft()
    {
        var hunt = new MonsterHunt();
        hunt.Start(9001, 1_000);

        Assert.Equal(new HuntTick(true, true), hunt.Advance(1_000));
    }

    [Fact]
    public void AStoppedHuntDoesNotTick()
    {
        var hunt = new MonsterHunt();
        hunt.Start(9001, 5_000);
        hunt.Stop();

        Assert.Equal(default, hunt.Advance(5_000));
        Assert.False(hunt.Active);
        Assert.Equal(0, hunt.RemainingMs);
    }

    [Fact]
    public void TheTargetIsReachedOnlyOnTheKillThatEqualsIt()
    {
        var hunt = new MonsterHunt();
        hunt.Start(9001, 60_000);
        hunt.SetCounter(0, 2);

        Assert.False(hunt.CountKill());
        Assert.True(hunt.CountKill());
        Assert.False(hunt.CountKill());
        Assert.Equal(3, hunt.Kills);
    }

    [Fact]
    public void AnotherQuestIsRunningOnlyWhileTheHuntIsActive()
    {
        var hunt = new MonsterHunt();
        hunt.Start(9001, 60_000);

        Assert.True(hunt.IsRunningAnother(9002));
        Assert.False(hunt.IsRunningAnother(9001));
        hunt.Stop();
        Assert.False(hunt.IsRunningAnother(9002));
    }

    [Fact]
    public void ASnapshotRestoresTheWholeHunt()
    {
        var hunt = new MonsterHunt { Grade = 1 };
        hunt.Start(9001, 30_000);
        hunt.SetCounter(4, 8);
        hunt.Advance(1_500);

        var restored = new MonsterHunt();
        restored.Restore(hunt.Snapshot());

        Assert.Equal(hunt.Snapshot(), restored.Snapshot());
        Assert.Equal(new MonsterHuntSnapshot(true, 9001, 1, 28_500, 0, 4, 8), restored.Snapshot());
    }

    [Fact]
    public void ResetClearsTheQuestAndTheCounter()
    {
        var hunt = new MonsterHunt { Grade = 2 };
        hunt.Start(9001, 30_000);
        hunt.SetCounter(4, 8);

        hunt.Reset();

        Assert.Equal(new MonsterHuntSnapshot(false, 0, 0, 0, 0, 0, 0), hunt.Snapshot());
    }
}
