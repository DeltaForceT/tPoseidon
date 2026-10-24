using tPoseidon.Core.Game.Fiesta;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Fiesta;

public sealed class FiestaRulesTests
{
    private static readonly FiestaSlotEntry Gate = new(1, 180, 1, 5, 0, 0, 0, 60, 70);
    private static readonly FiestaSlotEntry Hall = new(2, 312, 0, 0, 0, 0, 0, 180, 190);
    private static readonly LevelPortEntry Ticket = new(63242, "go", 1, [new LevelPortBand(579, 21, 59), new LevelPortBand(580, 60, 89)]);

    [Fact]
    public void WindowsFollowTheInAndOutMinutes()
    {
        var day = new DateTime(2026, 10, 19);
        Assert.Null(FiestaRules.Open(Gate, day.AddHours(1).AddMinutes(-1)));
        var window = FiestaRules.Open(Gate, day.AddHours(1).AddMinutes(4))!.Value;
        Assert.Equal(TimeSpan.FromMinutes(10), window.Duration);
        Assert.Equal(TimeSpan.FromMinutes(4), window.Elapsed);
        Assert.False(FiestaRules.PortalOpens(window));
        Assert.True(FiestaRules.PortalOpens(FiestaRules.Open(Gate, day.AddHours(1).AddMinutes(5))!.Value));
        Assert.True(FiestaRules.PortalOpens(FiestaRules.Open(Hall, day.AddHours(3))!.Value));
        Assert.Null(FiestaRules.Open(Gate, day.AddHours(1).AddMinutes(10)));
    }

    [Fact]
    public void TwoSlotsPerDayAndFreeReentry()
    {
        var day = new DateTime(2026, 10, 19);
        var first = day.AddHours(1);
        var record = FiestaRules.Entered(default, first, first.AddMinutes(1));
        Assert.Equal(1, record.Count);
        Assert.Equal(record, FiestaRules.Entered(record, first, first.AddMinutes(2)));
        var second = day.AddHours(3);
        record = FiestaRules.Entered(record, second, second);
        Assert.Equal(2, record.Count);
        Assert.True(FiestaRules.TicketAllowed(record, second, second.AddMinutes(3)));
        Assert.False(FiestaRules.TicketAllowed(record, day.AddHours(5), day.AddHours(5)));
        Assert.True(FiestaRules.TicketAllowed(record, day.AddDays(1).AddHours(1), day.AddDays(1).AddHours(1)));
    }

    [Fact]
    public void TicketChecksMapBandAndOpenSlot()
    {
        var open = new DateTime(2026, 10, 19, 1, 2, 0);
        Assert.Equal(FiestaTicketRefusal.MapRefuses, FiestaRules.Ticket(Ticket, 30, 0x80, [Gate], default, open).Refusal);
        Assert.Equal(FiestaTicketRefusal.NoLevelBand, FiestaRules.Ticket(Ticket, 10, 0, [Gate], default, open).Refusal);
        Assert.Equal(FiestaTicketRefusal.Closed, FiestaRules.Ticket(Ticket, 30, 0, [Gate], default, open.AddHours(1)).Refusal);
        var (refusal, band) = FiestaRules.Ticket(Ticket, 60, 0, [Gate], default, open);
        Assert.Equal(FiestaTicketRefusal.None, refusal);
        Assert.Equal(580, band!.ZoneId);
        Assert.Equal(17, FiestaRules.ReturnZone(17, 3));
        Assert.Equal(3, FiestaRules.ReturnZone(0, 3));
    }
}
