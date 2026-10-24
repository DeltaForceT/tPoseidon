using tPoseidon.Core.Game.HarconDefence;

namespace tPoseidon.Core.Game.Tests.HarconDefence;

public sealed class HarconBattleTests
{
    private const ushort Map = 663;
    private const ushort Harcon = 10;
    private const ushort SecondHarcon = 11;
    private const ushort NearWave = 20;
    private const ushort FarWave = 21;
    private const ushort Turret = 30;
    private const ulong Builder = 7;

    private static readonly Dictionary<ushort, (int X, int Y)> Positions = new()
    {
        [Harcon] = (100, 100),
        [SecondHarcon] = (900, 900),
        [NearWave] = (300, 100),
        [FarWave] = (800, 800),
        [Turret] = (150, 250),
    };

    private static HarconBattle Battle() => new(Map, [Harcon, SecondHarcon], [NearWave, FarWave], [(Turret, Builder)]);

    private static (int X, int Y)? At(ushort id) => Positions.TryGetValue(id, out var at) ? at : null;

    [Fact]
    public void IdleWavesGoForTheNearestHarconAndTurretsForTheNearestWaveInReach()
    {
        var engagements = Battle().Engagements(At, _ => false, _ => 270);

        Assert.Contains(new HarconEngagement(NearWave, Harcon), engagements);
        Assert.Contains(new HarconEngagement(FarWave, SecondHarcon), engagements);
        Assert.Contains(new HarconEngagement(Turret, NearWave), engagements);
        Assert.DoesNotContain(engagements, engagement => engagement.Attacker is Harcon or SecondHarcon);
    }

    [Fact]
    public void TurretsWithoutAWaveInReachAndFightingMonstersStayAsTheyAre()
    {
        var engagements = Battle().Engagements(At, id => id == NearWave, _ => 100);

        Assert.Equal([new HarconEngagement(FarWave, SecondHarcon)], engagements);
    }

    [Fact]
    public void DeadFightersAreNeitherAttackersNorTargets()
    {
        var engagements = Battle().Engagements(id => id == Harcon ? null : At(id), _ => false, _ => 270);

        Assert.Contains(new HarconEngagement(NearWave, SecondHarcon), engagements);
    }

    [Fact]
    public void WavesAreTheEnemiesOfHarconsAndTurrets()
    {
        var battle = Battle();

        Assert.True(battle.AreEnemies(NearWave, Turret));
        Assert.True(battle.AreEnemies(Harcon, FarWave));
        Assert.False(battle.AreEnemies(Harcon, Turret));
        Assert.False(battle.AreEnemies(NearWave, FarWave));
        Assert.Equal([NearWave, FarWave], battle.EnemiesOf(Turret).Order());
        Assert.Equal(Builder, battle.BuilderOf(Turret));
        Assert.Equal(HarconSide.Harcon, battle.SideOf(Harcon));
    }

    [Fact]
    public void HitWavesAndTurretsTurnOnTheirAttackerButTheHarconDoesNot()
    {
        var battle = Battle();

        Assert.True(battle.TurnsOnAttacker(NearWave, Turret));
        Assert.True(battle.TurnsOnAttacker(Turret, NearWave));
        Assert.False(battle.TurnsOnAttacker(Harcon, NearWave));
        Assert.False(battle.TurnsOnAttacker(NearWave, FarWave));
    }
}
