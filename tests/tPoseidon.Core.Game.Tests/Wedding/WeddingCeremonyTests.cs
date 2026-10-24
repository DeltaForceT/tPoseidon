using tPoseidon.Core.Game.Wedding;

namespace tPoseidon.Core.Game.Tests.Wedding;

public sealed class WeddingCeremonyTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GatheringSendsThreeStagesThenTheScenesStartAtFiveMinutes()
    {
        var ceremony = new WeddingCeremony([]);
        Assert.Equal([new WeddingEvent(WeddingEventKind.GatheringTimer, 1)], ceremony.Open(Start));
        Assert.Empty(ceremony.Tick(Start.AddSeconds(119)));
        Assert.Equal([new WeddingEvent(WeddingEventKind.GatheringTimer, 2)], ceremony.Tick(Start.AddSeconds(120)));
        Assert.Equal([new WeddingEvent(WeddingEventKind.GatheringTimer, 3)], ceremony.Tick(Start.AddSeconds(270)));
        Assert.Equal([new WeddingEvent(WeddingEventKind.Scene, WeddingScene.StartMessage)], ceremony.Tick(Start.AddSeconds(300)));
        Assert.Equal(WeddingHallState.Ceremony, ceremony.State);
        Assert.Empty(ceremony.Tick(Start.AddSeconds(304)));
        Assert.Equal([new WeddingEvent(WeddingEventKind.Scene, WeddingScene.IntroduceOfficator)], ceremony.Tick(Start.AddSeconds(305)));
    }

    [Fact]
    public void TheWholeHallRunsToTheClose()
    {
        var ceremony = new WeddingCeremony([90]);
        ceremony.Open(Start);
        var sceneMs = WeddingScene.FixedSteps.Sum(step => step.DelayMs) + WeddingScene.OfficiateLineMs(90);
        var end = Start + WeddingCeremony.GatheringLength + TimeSpan.FromMilliseconds(sceneMs);
        var events = ceremony.Tick(end);
        Assert.Equal(2 + WeddingScene.FixedSteps.Count + 2, events.Count);
        Assert.Contains(new WeddingEvent(WeddingEventKind.Scene, WeddingScene.SceneEnd), events);
        Assert.Contains(new WeddingEvent(WeddingEventKind.CeremonyEnded, 0), events);
        Assert.Equal(new WeddingEvent(WeddingEventKind.ClosingTimer, 1), events[^1]);
        Assert.Equal([new WeddingEvent(WeddingEventKind.ClosingTimer, 2)], ceremony.Tick(end.AddSeconds(600)));
        Assert.Equal([new WeddingEvent(WeddingEventKind.ClosingTimer, 3)], ceremony.Tick(end.AddSeconds(1140)));
        Assert.Equal([new WeddingEvent(WeddingEventKind.HallClosed, 0)], ceremony.Tick(end.AddSeconds(1200)));
        Assert.Equal(WeddingHallState.Closed, ceremony.State);
    }

    [Fact]
    public void OfficiantLinesLastByLength()
    {
        Assert.Equal(5000, WeddingScene.OfficiateLineMs(90));
        Assert.Equal(0, WeddingScene.OfficiateLineMs(30));
        Assert.False(WeddingScene.IsSent(WeddingScene.OfficiateMessage));
        Assert.True(WeddingScene.IsSent(WeddingScene.SceneEnd));
    }
}
