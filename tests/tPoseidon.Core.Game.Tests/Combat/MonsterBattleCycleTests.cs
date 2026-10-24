using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Combat;

public sealed class MonsterBattleCycleTests
{
    private const int MonsterParamId = 2100;
    private const ushort MonsterId = 0x0201;
    private const ushort PlayerId = 0x0301;
    private const ushort MapId = 1;
    private const int TickMs = 250;
    private const int SwingDelayMs = 1800;

    private const int Hits = 0;
    private const int Fails = 99;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly IMonsterParamLookup Catalog = new InMemoryMonsterParamLookup(new[]
    {
        new MonsterParamEntry
        {
            Id = MonsterParamId,
            Name = "Biter",
            Level = 1,
            WalkSpeed = 80f,
            RunSpeed = 120f,
            ChaseTime = 4f,
            ApLv = 10,
            AcLv = 50,
            DxLv = 10,
        },
    });

    private static readonly IReadOnlyDictionary<int, CharacterCombatInfo> Reach =
        new Dictionary<int, CharacterCombatInfo>
        {
            [MonsterParamId] = new()
            {
                Id = MonsterParamId,
                MinAttackRange = 60,
                MaxAttackRange = 86,
                ChaseRange = 216,
                WalkSpeed = 0f,
                RunSpeed = 0f,
                AttackMs = 800,
                Attack2Ms = 0,
                AttackReadyMs = 0,
            },
        };

    private static ZoneGeometry OpenField() => new()
    {
        MinX = 0,
        MinY = 0,
        MaxX = 1279,
        MaxY = 1279,
        CellsX = 80,
        CellsY = 80,
        CellAttributes = new byte[80 * 80],
        Regions = Array.Empty<ZoneRegion>(),
    };

    private static MonsterEntity MonsterAt(ushort x, ushort y)
    {
        var monster = new MonsterEntity
        {
            EntityId = MonsterId,
            MapId = MapId,
            MonsterParamId = MonsterParamId,
            Name = "Biter",
            SpawnX = x,
            SpawnY = y,
            MaxHp = 100,
            Level = 1,
        };
        monster.InitHp(100);
        monster.InitPosition(x, y);
        monster.InitSpeed(80f);
        return monster;
    }

    private static DateTimeOffset At(int milliseconds) => Start + TimeSpan.FromMilliseconds(milliseconds);

    private static Dictionary<ushort, MonsterBattleTarget> PlayerAt(ushort x, ushort y, int hp = 200) =>
        new()
        {
            [PlayerId] = new MonsterBattleTarget(x, y, Dp: 0, Hv: 0, Lk: 0, Level: 1, Hp: hp),
        };

    private static MonsterBattleCycle Cycle(params int[] draws) => new(Catalog, Reach, new ScriptedRandom(draws));

    private static int[] PlainHit => new[] { Hits, Fails, Fails, Fails };

    [Fact]
    public void EngagingStopsTheMonsterWhereItIsAndOnlyOnce()
    {
        var cycle = Cycle();
        var monster = MonsterAt(328, 328);
        monster.MoveTo(488, 328, At(0), 2000);

        var halted = cycle.Engage(monster, PlayerId, At(1000));

        Assert.Equal(new MonsterBattleHalted(MonsterId, 408, 328, MeleeApproach.WalkMoveType), halted);
        Assert.Equal((ushort)408, monster.CurrentX);
        Assert.True(cycle.IsFighting(MonsterId));
        Assert.Null(cycle.Engage(monster, PlayerId, At(1250)));
    }

    [Fact]
    public void TargetInRangeIsHitAtOnceThenAfterTheSwingDelay()
    {
        var cycle = Cycle(PlainHit.Concat(PlainHit).ToArray());
        var monster = MonsterAt(400, 400);
        var geometry = OpenField();
        var player = PlayerAt(470, 400);
        cycle.Engage(monster, PlayerId, At(0));

        var first = cycle.Tick(MapId, geometry, new[] { monster }, player, At(TickMs));
        var waiting = cycle.Tick(MapId, geometry, new[] { monster }, player, At(TickMs * 2));
        var second = cycle.Tick(MapId, geometry, new[] { monster }, player, At(TickMs + SwingDelayMs));

        var blow = new MonsterBattleSwung(
            MonsterId, 400, 400, PlayerId, 470, 400, 10, MeleeResolution.ResultHit, Fatal: false);
        Assert.Equal(new MonsterBattleEvent[] { blow }, first);
        Assert.Empty(waiting);
        Assert.Equal(new MonsterBattleEvent[] { blow }, second);
    }

    [Fact]
    public void TargetTooCloseMakesItBackOffAtRunSpeedAnnouncedFirstThenHit()
    {
        var cycle = Cycle(PlainHit);
        var monster = MonsterAt(408, 408);
        var geometry = OpenField();
        var player = PlayerAt(424, 408);
        var route = WanderRoute.FindToPoint(geometry, 408, 408, 351, 408)!;
        var travelMs = (int)Math.Ceiling(route.Length / 120.0 * 1000.0);
        cycle.Engage(monster, PlayerId, At(0));

        var backOff = cycle.Tick(MapId, geometry, new[] { monster }, player, At(TickMs));
        var moving = cycle.Tick(MapId, geometry, new[] { monster }, player, At(TickMs + travelMs - 1));
        var arrived = cycle.Tick(MapId, geometry, new[] { monster }, player, At(TickMs + travelMs));

        Assert.Equal(
            new MonsterBattleEvent[]
            {
                new MonsterBattleSpeedChanged(MonsterId, 120f),
                new MonsterBattleMoved(
                    MonsterId, 408, 408, (ushort)route.DestinationX, (ushort)route.DestinationY,
                    MonsterBattleCycle.StoredMoveType),
            },
            backOff);
        Assert.Equal(MeleeApproach.RunMoveType, monster.MoveType);
        Assert.Empty(moving);
        Assert.IsType<MonsterBattleSwung>(Assert.Single(arrived));
    }

    [Fact]
    public void AfterASwingItChasesWithTheChaseMoveTypeAtTheAverageSpeed()
    {
        var cycle = Cycle(PlainHit);
        var monster = MonsterAt(400, 400);
        var geometry = OpenField();
        cycle.Engage(monster, PlayerId, At(0));
        cycle.Tick(MapId, geometry, new[] { monster }, PlayerAt(470, 400), At(TickMs));

        var fartherPlayer = PlayerAt(550, 400);
        var route = WanderRoute.FindToPoint(geometry, 400, 400, 477, 400)!;
        var travelMs = (int)Math.Ceiling(route.Length / 100.0 * 1000.0);
        var chase = cycle.Tick(MapId, geometry, new[] { monster }, fartherPlayer, At(TickMs + SwingDelayMs));
        var moving = cycle.Tick(MapId, geometry, new[] { monster }, fartherPlayer, At(TickMs + SwingDelayMs + travelMs - 1));

        Assert.Equal(
            new MonsterBattleEvent[]
            {
                new MonsterBattleSpeedChanged(MonsterId, 100f),
                new MonsterBattleMoved(
                    MonsterId, 400, 400, (ushort)route.DestinationX, (ushort)route.DestinationY,
                    MeleeApproach.ChaseMoveType),
            },
            chase);
        Assert.Empty(moving);
    }

    [Fact]
    public void AfterASwingATargetBeyondTheChaseRangeEndsTheFight()
    {
        var cycle = Cycle(PlainHit);
        var monster = MonsterAt(400, 400);
        var geometry = OpenField();
        cycle.Engage(monster, PlayerId, At(0));
        cycle.Tick(MapId, geometry, new[] { monster }, PlayerAt(470, 400), At(TickMs));

        var events = cycle.Tick(MapId, geometry, new[] { monster }, PlayerAt(700, 400), At(TickMs + SwingDelayMs));

        Assert.Equal(
            new MonsterBattleEvent[] { new MonsterBattleLeft(MonsterId, MonsterBattleLeaveReason.OutOfChaseRange) },
            events);
        Assert.False(cycle.IsFighting(MonsterId));
    }

    [Fact]
    public void FirstApproachThatCannotBeWalkedEndsTheFight()
    {
        var cycle = Cycle();
        var monster = MonsterAt(400, 400);
        cycle.Engage(monster, PlayerId, At(0));

        var events = cycle.Tick(MapId, geometry: null, new[] { monster }, PlayerAt(550, 400), At(TickMs));

        Assert.Equal(
            new MonsterBattleEvent[] { new MonsterBattleLeft(MonsterId, MonsterBattleLeaveReason.ApproachRefused) },
            events);
    }

    [Fact]
    public void ChaseTimeLimitsHowLongItTriesToGetBackInRange()
    {
        var cycle = Cycle(PlainHit);
        var monster = MonsterAt(400, 400);
        cycle.Engage(monster, PlayerId, At(0));
        cycle.Tick(MapId, OpenField(), new[] { monster }, PlayerAt(470, 400), At(TickMs));

        var outOfReach = PlayerAt(550, 400);
        var cooldownEnds = TickMs + SwingDelayMs;
        IReadOnlyList<MonsterBattleEvent> events = Array.Empty<MonsterBattleEvent>();
        for (var elapsed = TickMs; elapsed < 4000; elapsed += TickMs)
        {
            events = cycle.Tick(MapId, geometry: null, new[] { monster }, outOfReach, At(cooldownEnds + elapsed));
            Assert.Empty(events);
        }
        events = cycle.Tick(MapId, geometry: null, new[] { monster }, outOfReach, At(cooldownEnds + 4000));

        Assert.Equal(
            new MonsterBattleEvent[] { new MonsterBattleLeft(MonsterId, MonsterBattleLeaveReason.ChaseTimeSpent) },
            events);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AChaseGivenAtEngageOutlastsTheTableChaseTimeOnTheWayToTheTarget(bool longChase, bool leaves)
    {
        var cycle = Cycle();
        var monster = MonsterAt(50, 50);
        cycle.Engage(monster, PlayerId, At(0), longChase ? TimeSpan.FromSeconds(20) : null);
        var farAway = PlayerAt(1200, 1200);
        var events = new List<MonsterBattleEvent>();

        for (var elapsed = TickMs; elapsed <= 6000; elapsed += TickMs)
        {
            events.AddRange(cycle.Tick(MapId, OpenField(), new[] { monster }, farAway, At(elapsed)));
        }

        Assert.Equal(leaves, events.Contains(new MonsterBattleLeft(MonsterId, MonsterBattleLeaveReason.ChaseTimeSpent)));
        Assert.Equal(!leaves, cycle.IsFighting(MonsterId));
    }

    [Fact]
    public void TargetThatIsNoLongerThereEndsTheFight()
    {
        var cycle = Cycle();
        var monster = MonsterAt(400, 400);
        cycle.Engage(monster, PlayerId, At(0));

        var events = cycle.Tick(
            MapId, OpenField(), new[] { monster }, new Dictionary<ushort, MonsterBattleTarget>(), At(TickMs));

        Assert.Equal(
            new MonsterBattleEvent[] { new MonsterBattleLeft(MonsterId, MonsterBattleLeaveReason.TargetGone) },
            events);
    }

    [Fact]
    public void BlowThatEmptiesTheTargetIsDeliveredAsFatal()
    {
        var cycle = Cycle(PlainHit);
        var monster = MonsterAt(400, 400);
        cycle.Engage(monster, PlayerId, At(0));

        var events = cycle.Tick(MapId, OpenField(), new[] { monster }, PlayerAt(470, 400, hp: 10), At(TickMs));

        Assert.Equal(
            new MonsterBattleEvent[]
            {
                new MonsterBattleSwung(
                    MonsterId, 400, 400, PlayerId, 470, 400, 10,
                    (byte)(MeleeResolution.ResultHit | MeleeResolution.ResultFinishingFlag), Fatal: true),
            },
            events);
        Assert.True(cycle.IsFighting(MonsterId));
    }

    [Fact]
    public void AfterAFatalBlowAnotherMonsterInTheSameTickLeavesItsDeadTarget()
    {
        var cycle = Cycle(PlainHit);
        var first = MonsterAt(400, 400);
        var second = new MonsterEntity
        {
            EntityId = 0x0202,
            MapId = MapId,
            MonsterParamId = MonsterParamId,
            Name = "Biter",
            SpawnX = 540,
            SpawnY = 400,
            MaxHp = 100,
            Level = 1,
        };
        second.InitHp(100);
        second.InitPosition(540, 400);
        cycle.Engage(first, PlayerId, At(0));
        cycle.Engage(second, PlayerId, At(0));

        var events = cycle.Tick(
            MapId, OpenField(), new[] { first, second }, PlayerAt(470, 400, hp: 10), At(TickMs));

        Assert.True(Assert.Single(events.OfType<MonsterBattleSwung>()).Fatal);
        Assert.Equal(
            new MonsterBattleLeft(0x0202, MonsterBattleLeaveReason.TargetGone),
            Assert.Single(events.OfType<MonsterBattleLeft>()));
    }

    [Fact]
    public void ProtectedTargetIsHitWithABlockAndNoDamage()
    {
        var cycle = Cycle(Hits, Fails, Fails);
        var monster = MonsterAt(400, 400);
        cycle.Engage(monster, PlayerId, At(0));
        var protectedPlayer = new Dictionary<ushort, MonsterBattleTarget>
        {
            [PlayerId] = new MonsterBattleTarget(470, 400, Dp: 0, Hv: 0, Lk: 0, Level: 1, Hp: 200, Protected: true),
        };

        var events = cycle.Tick(MapId, OpenField(), new[] { monster }, protectedPlayer, At(TickMs));

        Assert.Equal(
            new MonsterBattleEvent[]
            {
                new MonsterBattleSwung(
                    MonsterId, 400, 400, PlayerId, 470, 400, 0, MeleeResolution.ResultBlock, Fatal: false),
            },
            events);
    }

    [Fact]
    public void DeadMonsterIsDroppedWithoutAnEvent()
    {
        var cycle = Cycle();
        var monster = MonsterAt(400, 400);
        cycle.Engage(monster, PlayerId, At(0));
        monster.ApplyDamage(100);

        var events = cycle.Tick(MapId, OpenField(), new[] { monster }, PlayerAt(470, 400), At(TickMs));

        Assert.Empty(events);
        Assert.False(cycle.IsFighting(MonsterId));
    }

    [Fact]
    public void ChaseTimeComesFromTheTableInSecondsWithADefault()
    {
        Assert.Equal(4000, MonsterBattleCycle.ChaseTimeMs(new MonsterParamEntry { Id = 1, ChaseTime = 4f }));
        Assert.Equal(
            MonsterBattleCycle.DefaultChaseTimeMs,
            MonsterBattleCycle.ChaseTimeMs(new MonsterParamEntry { Id = 1, ChaseTime = 0f }));
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _draws;

        public ScriptedRandom(int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public override int Next(int maxValue) => _draws.Dequeue();
    }
}
