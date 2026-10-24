using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Drilling;

public sealed class SurpriseSpotRulesTests
{
    [Fact]
    public void OnSpotMeansTwoCellsAroundTheSpotPoint()
    {
        var spot = new SurpriseSpot(63934, 9, 160, 320, DateTimeOffset.UnixEpoch.AddMinutes(10));
        Assert.True(SurpriseSpotRules.IsOnSpot(spot, 9, 160 + 2 * 16, 320 - 2 * 16));
        Assert.False(SurpriseSpotRules.IsOnSpot(spot, 9, 160 + 3 * 16, 320));
        Assert.False(SurpriseSpotRules.IsOnSpot(spot, 12, 160, 320));
        Assert.False(SurpriseSpotRules.HasExpired(spot, DateTimeOffset.UnixEpoch.AddMinutes(9)));
        Assert.True(SurpriseSpotRules.HasExpired(spot, DateTimeOffset.UnixEpoch.AddMinutes(10)));
    }

    [Fact]
    public void DestinationFollowsTheRates()
    {
        var row = new TreasureTeleportEntry(1, 10, [9], [new TreasureTeleportDestination(513, 25f, 0), new TreasureTeleportDestination(514, 75f, 3)]);
        Assert.Equal(513, SurpriseSpotRules.PickDestination(row, () => 0.2)!.ZoneId);
        Assert.Equal(514, SurpriseSpotRules.PickDestination(row, () => 0.25)!.ZoneId);
        Assert.Null(SurpriseSpotRules.PickDestination(row with { Destinations = [] }, () => 0.5));
    }
}
