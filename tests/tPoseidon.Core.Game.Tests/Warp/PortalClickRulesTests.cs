using tPoseidon.Core.Game.Fiesta;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Warp;

public sealed class PortalClickRulesTests
{
    private const int Town = 148;
    private const int Gate = 312;
    private const int Hall = 180;

    private static readonly FiestaSlotEntry[] Slots =
    {
        new(1, Gate, 0, 0, 0, 0, 0, 60, 70),
        new(2, Hall, 1, 5, 0, 0, 0, 60, 70),
        new(3, Gate, 0, 0, 0, 0, 0, 180, 190),
    };

    private static readonly PortalLevelBands Bands = new(new Dictionary<(int Map, int Portal), PortalLevelBand>
    {
        [(Town, 1)] = new PortalLevelBand(5, 400),
    });

    private static readonly DateTime OpenAt = new(2026, 10, 25, 1, 3, 0);
    private static readonly DateTime LateOpenAt = new(2026, 10, 25, 1, 6, 0);
    private static readonly DateTime ClosedAt = new(2026, 10, 25, 2, 0, 0);
    private static readonly DateTime SlotStart = new(2026, 10, 25, 1, 0, 0);
    private static readonly DateTime LaterSlotAt = new(2026, 10, 25, 3, 3, 0);

    private static int ClassOf(int map) => map is Gate or Hall ? PortalClickRules.FiestaMapClass : 0;

    private static int Check(PortalRegionClick click, DateTime now, FiestaEntryRecord? entry = null) =>
        PortalClickRules.Check(click, ClassOf, Bands, Slots, entry ?? new FiestaEntryRecord(0, null), now);

    [Fact]
    public void AnOrdinaryPortalInsideItsLevelBandIsAccepted()
    {
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, 149, 1, 50), ClosedAt));
    }

    [Fact]
    public void ALevelOutsideTheBandIsRefusedWithTheLevelCode()
    {
        Assert.Equal(PortalClickRules.LevelRefused, Check(new PortalRegionClick(Town, 149, 1, 4), ClosedAt));
        Assert.Equal(PortalClickRules.LevelRefused, Check(new PortalRegionClick(Town, 149, 1, 401), ClosedAt));
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, 149, 1, 5), ClosedAt));
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, 149, 1, 400), ClosedAt));
    }

    [Fact]
    public void APortalWithoutABandIsAcceptedAtAnyLevel()
    {
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, 149, 2, 1), ClosedAt));
    }

    [Theory]
    [InlineData(0, 149)]
    [InlineData(Town, 0)]
    [InlineData(1000, 149)]
    [InlineData(Town, 1000)]
    public void AMapOutsideOneToNineNineNineIsRefusedWithTheLevelCode(int current, int destination)
    {
        Assert.Equal(PortalClickRules.LevelRefused, Check(new PortalRegionClick(current, destination, 1, 50), OpenAt));
    }

    [Fact]
    public void AFiestaMapIsClosedOutsideItsWindow()
    {
        Assert.Equal(PortalClickRules.Closed, Check(new PortalRegionClick(Town, Gate, 1, 50), ClosedAt));
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, Gate, 1, 50), OpenAt));
    }

    [Fact]
    public void LeavingTheBanquetHallNeedsTheOpenPortalAndTheHallRegion()
    {
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Hall, Town, 1, 50), OpenAt));
        Assert.Equal(PortalClickRules.Closed, Check(new PortalRegionClick(Hall, Hall, 1, 50), OpenAt));
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Hall, Hall, 1, 50), LateOpenAt));
        Assert.Equal(PortalClickRules.Closed, Check(new PortalRegionClick(Hall, Hall, 2, 50), LateOpenAt));
    }

    [Fact]
    public void EnteringAFiestaMapAThirdTimeInADayIsRefusedWithTheDailyCode()
    {
        var twice = new FiestaEntryRecord(2, SlotStart);

        Assert.Equal(PortalClickRules.DailyLimit, Check(new PortalRegionClick(Town, Gate, 1, 50), LaterSlotAt, twice));
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, Gate, 1, 50), OpenAt, twice));
        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, Gate, 1, 50), LaterSlotAt, new FiestaEntryRecord(1, SlotStart)));
    }

    [Fact]
    public void ACountFromAnotherDayDoesNotLimitToday()
    {
        var yesterday = new FiestaEntryRecord(2, SlotStart.AddDays(-1));

        Assert.Equal(PortalClickRules.Accepted, Check(new PortalRegionClick(Town, Gate, 1, 50), OpenAt, yesterday));
    }
}
