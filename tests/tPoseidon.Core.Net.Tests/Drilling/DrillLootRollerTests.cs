using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.Tests.Drilling;

public class DrillLootRollerTests
{
    private const string BeachMap = "Beach";

    private static DrillLootRoller MakeRoller(IEnumerable<MapItemZoneEntry>? entries = null)
    {
        var lookup = new InMemoryMapItemZoneLookup(entries ?? Array.Empty<MapItemZoneEntry>());
        return new DrillLootRoller(lookup);
    }

    [Fact]
    public void GetEligibleAtDepth_DepthInRange_IncludesRow()
    {
        var row = new MapItemZoneEntry
        {
            MapName = BeachMap,
            ItemId = 11004,
            MinDepth = 10,
            MaxDepth = 20,
            Avr = 600,
            Max = 600,
            Priority = 0,
        };
        var roller = MakeRoller(new[] { row });

        Assert.Single(roller.GetEligibleAtDepth(BeachMap, 10));
        Assert.Single(roller.GetEligibleAtDepth(BeachMap, 15));
        Assert.Single(roller.GetEligibleAtDepth(BeachMap, 20));
    }

    [Fact]
    public void GetEligibleAtDepth_DepthOutOfRange_ExcludesRow()
    {
        var row = new MapItemZoneEntry
        {
            MapName = BeachMap,
            ItemId = 11004,
            MinDepth = 10,
            MaxDepth = 20,
        };
        var roller = MakeRoller(new[] { row });

        Assert.Empty(roller.GetEligibleAtDepth(BeachMap, 9));
        Assert.Empty(roller.GetEligibleAtDepth(BeachMap, 21));
    }

    [Fact]
    public void GetEligibleAtDepth_UnknownMap_ReturnsEmpty()
    {
        var roller = MakeRoller(new[]
        {
            new MapItemZoneEntry { MapName = BeachMap, ItemId = 11004, MinDepth = 0, MaxDepth = 9999 },
        });

        Assert.Empty(roller.GetEligibleAtDepth("Other_Map", 50));
    }

    [Fact]
    public void GetEligibleAtDepth_MultipleRows_FiltersByDepthOnly()
    {
        var rows = new[]
        {
            new MapItemZoneEntry { MapName = BeachMap, ItemId = 1, MinDepth = 0, MaxDepth = 50 },
            new MapItemZoneEntry { MapName = BeachMap, ItemId = 2, MinDepth = 51, MaxDepth = 100 },
            new MapItemZoneEntry { MapName = BeachMap, ItemId = 3, MinDepth = 101, MaxDepth = 200 },
        };
        var roller = MakeRoller(rows);

        var eligibleAt25 = roller.GetEligibleAtDepth(BeachMap, 25);
        Assert.Single(eligibleAt25);
        Assert.Equal(1, eligibleAt25[0].ItemId);

        var eligibleAt75 = roller.GetEligibleAtDepth(BeachMap, 75);
        Assert.Single(eligibleAt75);
        Assert.Equal(2, eligibleAt75[0].ItemId);
    }

    [Fact]
    public void Roll_ThrowsNotImplemented_UntilLootMathIsKnown()
    {
        var roller = MakeRoller(new[]
        {
            new MapItemZoneEntry { MapName = BeachMap, ItemId = 11004, MinDepth = 0, MaxDepth = 9999, Avr = 600 },
        });

        Assert.Throws<NotImplementedException>(() => roller.Roll(BeachMap, 100));
    }
}
