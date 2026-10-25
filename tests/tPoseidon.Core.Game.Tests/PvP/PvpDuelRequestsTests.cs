using tPoseidon.Core.Game.PvP;

namespace tPoseidon.Core.Game.Tests.PvP;

public sealed class PvpDuelRequestsTests
{
    [Fact]
    public void AnAcceptedChallengeCanBeStartedOnce()
    {
        var requests = new PvpDuelRequests();
        requests.Ask(1, 2);
        Assert.False(requests.Start(1, 2));
        Assert.True(requests.Answer(2, 1, accepted: true));
        Assert.True(requests.Start(1, 2));
        Assert.False(requests.Start(1, 2));
    }

    [Fact]
    public void ARefusedOrUnaskedAnswerStartsNothing()
    {
        var requests = new PvpDuelRequests();
        Assert.False(requests.Answer(2, 1, accepted: true));
        requests.Ask(1, 2);
        Assert.False(requests.Answer(3, 1, accepted: true));
        Assert.True(requests.Answer(2, 1, accepted: false));
        Assert.False(requests.Start(1, 2));
    }

    [Fact]
    public void CancellingOrLeavingForgetsTheChallenge()
    {
        var requests = new PvpDuelRequests();
        requests.Ask(1, 2);
        requests.Answer(2, 1, accepted: true);
        requests.Cancel(1, 2);
        Assert.False(requests.Start(1, 2));
        requests.Ask(1, 2);
        requests.Answer(2, 1, accepted: true);
        requests.Forget(2);
        Assert.False(requests.Start(1, 2));
    }

    [Fact]
    public void AskingAgainReplacesTheEarlierChallenge()
    {
        var requests = new PvpDuelRequests();
        requests.Ask(1, 2);
        requests.Ask(1, 3);
        Assert.False(requests.Answer(2, 1, accepted: true));
        Assert.True(requests.Answer(3, 1, accepted: true));
        Assert.True(requests.Start(1, 3));
    }
}
