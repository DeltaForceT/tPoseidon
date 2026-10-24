using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class GhostExchangeRulesTests
{
    private static readonly GhostExchangeTables Tables = new(
        new Dictionary<int, IReadOnlyList<int>>
        {
            [20] = new[] { 60410, 60462, 60488, 0, 60436, 0, 0, 0, 0, 0, 0, 0 },
            [502] = new[] { 40180, 40155, 44108, 46102, 0, 0, 0, 0, 0, 0, 0, 0 },
        },
        new[] { 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

    private static readonly int[] StoneTargets = [60462, 60488];

    private static ItemParamEntry Item(int id, int itemClass, int type, int groupId) => new() { Id = id, Class = itemClass, Type = type, GroupId = groupId };

    [Fact]
    public void TargetsStopAtZeroAndSkipTheSource()
    {
        Assert.Equal(StoneTargets, GhostExchangeRules.Targets(Tables, Item(60410, 1, 14, 20)));
        Assert.Empty(GhostExchangeRules.Targets(Tables, Item(60410, 1, 14, 0)));
    }

    [Fact]
    public void StoneChecksFollowTheErrorOrder()
    {
        var stone = Item(60410, 1, 14, 20);
        var other = Item(60462, 1, 14, 20);
        Assert.Equal(GhostExchangeRules.StoneInvalidSource, GhostExchangeRules.CheckStone(Tables, null, 0, new(1, 1, 60462), other));
        Assert.Equal(GhostExchangeRules.StoneInvalidCount, GhostExchangeRules.CheckStone(Tables, stone, 2, new(1, 3, 60462), other));
        Assert.Equal(GhostExchangeRules.StoneInvalidCount, GhostExchangeRules.CheckStone(Tables, stone, 2, new(1, 0, 60462), other));
        Assert.Equal(GhostExchangeRules.StoneNotExchangeable, GhostExchangeRules.CheckStone(Tables, stone with { GroupId = 7 }, 2, new(1, 1, 60462), other));
        Assert.Equal(GhostExchangeRules.StoneInvalidTarget, GhostExchangeRules.CheckStone(Tables, stone, 2, new(1, 1, 60436), Item(60436, 1, 14, 20)));
        Assert.Equal(GhostExchangeRules.StoneNoTargetInfo, GhostExchangeRules.CheckStone(Tables, stone, 2, new(1, 1, 60462), null));
        Assert.Equal(GhostExchangeRules.StoneInvalidTarget, GhostExchangeRules.CheckStone(Tables, stone, 2, new(1, 1, 60462), other with { Type = 15 }));
        Assert.Equal(GhostExchangeRules.Ok, GhostExchangeRules.CheckStone(Tables, stone, 2, new(1, 2, 60462), other));
    }

    [Fact]
    public void NeoChecksAndRefineLevels()
    {
        var weapon = Item(40001, 2, 2, 502);
        Assert.Equal(GhostExchangeRules.NeoNoSource, GhostExchangeRules.CheckNeo(Tables, false, null, new(1, 40180), null));
        Assert.Equal(GhostExchangeRules.NeoInvalidSource, GhostExchangeRules.CheckNeo(Tables, true, weapon with { GroupId = 0 }, new(1, 40180), null));
        Assert.Equal(GhostExchangeRules.NeoInvalidTarget, GhostExchangeRules.CheckNeo(Tables, true, weapon, new(1, 40999), null));
        Assert.Equal(GhostExchangeRules.NeoNoTargetInfo, GhostExchangeRules.CheckNeo(Tables, true, weapon, new(1, 40180), null));
        Assert.Equal(GhostExchangeRules.NeoDifferentKind, GhostExchangeRules.CheckNeo(Tables, true, weapon, new(1, 44108), Item(44108, 2, 3, 503)));
        Assert.Equal(GhostExchangeRules.Ok, GhostExchangeRules.CheckNeo(Tables, true, weapon, new(1, 40180), Item(40180, 2, 2, 503)));
        Assert.Equal(0, GhostExchangeRules.RefineLevelAfter(Tables, 3));
        Assert.Equal(1, GhostExchangeRules.RefineLevelAfter(Tables, 4));
        Assert.Equal(10, GhostExchangeRules.RefineLevelAfter(Tables, 13));
        Assert.Null(GhostExchangeRules.RefineLevelAfter(Tables, 14));
    }
}
