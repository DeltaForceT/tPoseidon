using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Monsters.Strategies;

namespace tPoseidon.Core.Game.Tests.Monsters;

public class AggroDetectorTests
{
    private sealed class StubScanner : IZoneAggroScanner
    {
        public ushort NextHit { get; set; }
        public int Calls { get; private set; }
        public ushort FindNearestHostile(MonsterEntity scanner, int radius)
        {
            Calls++;
            return NextHit;
        }
    }

    private static MonsterEntity NewMob(int viewRange) => new()
    {
        EntityId = 100,
        MapId = 148,
        MonsterParamId = 2185,
        Name = "Tottochi",
        SpawnX = 1000,
        SpawnY = 1000,
        MaxHp = 100,
        Level = 2,
        Atype = MonsterAType.AggressiveMonster,
        AiViewRange = viewRange,
    };

    [Fact]
    public void Tick_SkipsBeforeNextScanAt()
    {
        var detector = new AggroDetector();
        var mob = NewMob(200);
        var strategy = (AggressiveMonsterStrategy)MonsterStrategyFactory.Create(MonsterAType.AggressiveMonster);
        strategy.OnSpawn(mob);
        var scanner = new StubScanner { NextHit = 555 };

        detector.Tick(mob, strategy, scanner, DateTimeOffset.UtcNow);

        Assert.Equal(0, scanner.Calls);
    }

    [Fact]
    public void Tick_ScansWhenIntervalElapsed()
    {
        var detector = new AggroDetector();
        var mob = NewMob(200);
        var strategy = (AggressiveMonsterStrategy)MonsterStrategyFactory.Create(MonsterAType.AggressiveMonster);
        strategy.OnSpawn(mob);
        var scanner = new StubScanner { NextHit = 555 };

        var future = DateTimeOffset.UtcNow.AddSeconds(2);
        detector.Tick(mob, strategy, scanner, future);

        Assert.Equal(1, scanner.Calls);
    }

    [Fact]
    public void Tick_ZeroRange_DoesNotScan()
    {
        var detector = new AggroDetector();
        var mob = NewMob(0);
        var strategy = (AggressiveMonsterStrategy)MonsterStrategyFactory.Create(MonsterAType.AggressiveMonster);
        strategy.OnSpawn(mob);
        var scanner = new StubScanner { NextHit = 555 };

        var future = DateTimeOffset.UtcNow.AddSeconds(2);
        detector.Tick(mob, strategy, scanner, future);

        Assert.Equal(0, scanner.Calls);
    }

    [Fact]
    public void Tick_NonAggressiveStrategy_DoesNotScan()
    {
        var detector = new AggroDetector();
        var mob = NewMob(200);
        var strategy = MonsterStrategyFactory.Create(MonsterAType.HarconMonster);
        var scanner = new StubScanner { NextHit = 555 };

        detector.Tick(mob, strategy, scanner, DateTimeOffset.UtcNow.AddSeconds(2));

        Assert.Equal(0, scanner.Calls);
    }

    [Fact]
    public void Tick_HitTarget_RaisesEvent()
    {
        var detector = new AggroDetector();
        var mob = NewMob(200);
        var strategy = (AggressiveMonsterStrategy)MonsterStrategyFactory.Create(MonsterAType.AggressiveMonster);
        strategy.OnSpawn(mob);
        var scanner = new StubScanner { NextHit = 9999 };
        ushort? captured = null;
        detector.TargetAcquired += (_, target) => captured = target;

        detector.Tick(mob, strategy, scanner, DateTimeOffset.UtcNow.AddSeconds(2));

        Assert.Equal((ushort)9999, captured);
    }
}
