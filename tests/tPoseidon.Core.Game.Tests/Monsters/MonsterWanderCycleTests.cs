using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Monsters;

public sealed class MonsterWanderCycleTests
{
    private const int MonsterParamId = 2100;
    private const int MovementRegionId = 7;
    private const int KeepSign = 0;
    private const int FlipSign = 1;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly (int X, int Y)[] PlayerNearby = { (300, 300) };
    private static readonly (int X, int Y)[] NoPlayers = Array.Empty<(int X, int Y)>();

    private static readonly IMonsterParamLookup Catalog = new InMemoryMonsterParamLookup(new[]
    {
        new MonsterParamEntry { Id = MonsterParamId, Name = "Walker", WalkSpeed = 50f, RunSpeed = 80f },
    });

    private static ZoneGeometry OpenField(params ZoneRegion[] regions) => new()
    {
        MinX = 0,
        MinY = 0,
        MaxX = 639,
        MaxY = 639,
        CellsX = 40,
        CellsY = 40,
        CellAttributes = new byte[40 * 40],
        Regions = regions,
    };

    private static MonsterEntity MonsterAt(ushort x, ushort y, int regionId = 0)
    {
        var monster = new MonsterEntity
        {
            EntityId = 0x0201,
            MapId = 1,
            MonsterParamId = MonsterParamId,
            Name = "Walker",
            SpawnX = x,
            SpawnY = y,
            MaxHp = 10,
            Level = 1,
            RegionId = regionId,
        };
        monster.InitHp(10);
        monster.InitPosition(x, y);
        monster.InitSpeed(50f);
        return monster;
    }

    private static DateTimeOffset At(int milliseconds) => Start + TimeSpan.FromMilliseconds(milliseconds);

    private static IReadOnlyList<MonsterWanderEvent> TickEachSecond(
        MonsterWanderCycle cycle,
        ZoneGeometry geometry,
        MonsterEntity monster,
        (int X, int Y)[] players,
        int fromSecond,
        int toSecond)
    {
        IReadOnlyList<MonsterWanderEvent> last = Array.Empty<MonsterWanderEvent>();
        for (var second = fromSecond; second <= toSecond; second++)
        {
            last = cycle.Tick(geometry, new[] { monster }, players, At(second * 1000));
        }
        return last;
    }

    [Fact]
    public void FirstWaitIsThreeSecondsThenTheMonsterWalks()
    {
        var cycle = new MonsterWanderCycle(Catalog, new ScriptedRandom(100, 80, KeepSign, FlipSign));
        var geometry = OpenField();
        var monster = MonsterAt(328, 328);

        Assert.Empty(TickEachSecond(cycle, geometry, monster, PlayerNearby, 0, 2));
        var events = cycle.Tick(geometry, new[] { monster }, PlayerNearby, At(3000));

        var started = Assert.IsType<MonsterMoveStarted>(Assert.Single(events));
        Assert.Equal((328, 328), (started.FromX, started.FromY));
        Assert.Equal((408, 232), (started.ToX, started.ToY));
        Assert.Equal((408, 232), (monster.CurrentX, monster.CurrentY));
    }

    [Fact]
    public void ArrivalIsReportedAfterTheWalkTimeAndStartsANewWait()
    {
        var random = new ScriptedRandom(100, 80, KeepSign, FlipSign, 3000);
        var cycle = new MonsterWanderCycle(Catalog, random);
        var geometry = OpenField();
        var monster = MonsterAt(328, 328);
        TickEachSecond(cycle, geometry, monster, PlayerNearby, 0, 3);

        Assert.Empty(cycle.Tick(geometry, new[] { monster }, PlayerNearby, At(5500)));
        var events = cycle.Tick(geometry, new[] { monster }, PlayerNearby, At(5600));

        Assert.IsType<MonsterArrived>(Assert.Single(events));
        Assert.Contains((MonsterWanderCycle.WaitMinMs, MonsterWanderCycle.WaitMaxMs), random.RangeRequests);
    }

    [Fact]
    public void WithoutAPlayerNearbyTheCountdownOnlyRunsOnTheMoveRatioChance()
    {
        var blockedDraws = Enumerable.Repeat(MonsterWanderCycle.MoveRatioPercent, 6).ToArray();
        var cycle = new MonsterWanderCycle(Catalog, new ScriptedRandom(blockedDraws));
        var geometry = OpenField();
        var monster = MonsterAt(328, 328);

        Assert.Empty(TickEachSecond(cycle, geometry, monster, NoPlayers, 0, 6));
        Assert.Equal((328, 328), (monster.CurrentX, monster.CurrentY));
    }

    [Fact]
    public void WithoutAPlayerNearbyALuckyDrawStillCountsTheTick()
    {
        var lucky = MonsterWanderCycle.MoveRatioPercent - 1;
        var cycle = new MonsterWanderCycle(Catalog, new ScriptedRandom(lucky, lucky, lucky, 100, 80, KeepSign, KeepSign));
        var geometry = OpenField();
        var monster = MonsterAt(328, 328);

        var events = TickEachSecond(cycle, geometry, monster, NoPlayers, 0, 3);

        Assert.IsType<MonsterMoveStarted>(Assert.Single(events));
    }

    [Fact]
    public void DestinationNextToAPortalIsMirrored()
    {
        var portal = new ZoneRegion(Id: 0, Type: ZoneGeometry.RegionTypePortal, Param: 9, X1: 400, Y1: 400, X2: 440, Y2: 440);
        var cycle = new MonsterWanderCycle(Catalog, new ScriptedRandom(100, 80, KeepSign, KeepSign));
        var geometry = OpenField(portal);
        var monster = MonsterAt(328, 328);

        var events = TickEachSecond(cycle, geometry, monster, PlayerNearby, 0, 3);

        var started = Assert.IsType<MonsterMoveStarted>(Assert.Single(events));
        Assert.Equal((248, 232), (started.ToX, started.ToY));
    }

    [Fact]
    public void MovementRegionKeepsTheDestinationInsideAndAllowsLongerSteps()
    {
        var region = new ZoneRegion(MovementRegionId, ZoneGeometry.RegionTypeMonsterMovement, 0, 300, 300, 420, 420);
        var random = new ScriptedRandom(300, 300, KeepSign, KeepSign);
        var cycle = new MonsterWanderCycle(Catalog, random);
        var geometry = OpenField(region);
        var monster = MonsterAt(328, 328, MovementRegionId);

        var events = TickEachSecond(cycle, geometry, monster, PlayerNearby, 0, 3);

        var started = Assert.IsType<MonsterMoveStarted>(Assert.Single(events));
        Assert.Equal((424, 424), (started.ToX, started.ToY));
        Assert.Contains((MonsterWanderCycle.StepMin, MonsterWanderCycle.StepLimitInsideRegion), random.RangeRequests);
    }

    [Fact]
    public void MoveIntoTheSameCellIsRefusedAndRetriedAfterAShortWait()
    {
        var region = new ZoneRegion(MovementRegionId, ZoneGeometry.RegionTypeMonsterMovement, 0, 322, 322, 334, 334);
        var random = new ScriptedRandom(100, 80, KeepSign, KeepSign, 1000);
        var cycle = new MonsterWanderCycle(Catalog, random);
        var geometry = OpenField(region);
        var monster = MonsterAt(328, 328, MovementRegionId);

        var events = TickEachSecond(cycle, geometry, monster, PlayerNearby, 0, 3);

        var refused = Assert.IsType<MonsterMoveRefused>(Assert.Single(events));
        Assert.Equal((328, 328), (refused.X, refused.Y));
        Assert.Contains((MonsterWanderCycle.RetryMinMs, MonsterWanderCycle.RetryMaxMs), random.RangeRequests);
    }

    [Fact]
    public void MonsterWithAnAttackerDoesNotWander()
    {
        var cycle = new MonsterWanderCycle(Catalog, new ScriptedRandom());
        var geometry = OpenField();
        var monster = MonsterAt(328, 328);
        monster.AddAttacker(0x0301);

        Assert.Empty(TickEachSecond(cycle, geometry, monster, PlayerNearby, 0, 8));
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _draws;

        public ScriptedRandom(params int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public List<(int Min, int Max)> RangeRequests { get; } = new();

        public override int Next(int maxValue) => _draws.Dequeue();

        public override int Next(int minValue, int maxValue)
        {
            RangeRequests.Add((minValue, maxValue));
            return _draws.Dequeue();
        }
    }
}
