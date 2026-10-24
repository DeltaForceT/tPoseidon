using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ChainHitsTests
{
    private static SkillTableRow Row(string table, Dictionary<string, string> columns) => new()
    {
        Table = table, Level = 1, Columns = columns,
    };

    private static readonly SkillAttacker Fighter = new(500, 300, 0, 0, 60, Hv: 20, Dx: 0);

    private static readonly SkillTarget Dummy = new(0, 0, 0, 0, 100000, 1, 0);

    private static readonly Dictionary<int, int> NoSkills = new();

    [Fact]
    public void QuadPunchHitsFourTimesWithApTimesRatio()
    {
        var row = Row("ESAction_ChainPunch", new() { ["Ratio"] = "1.100000", ["ApplyRatio"] = "5.000000" });

        var hits = ChainHits.Resolve(ChainHits.QuadPunch, row, Fighter, Dummy, NoSkills, new Random(1));

        Assert.Equal(4, hits.Count);
        Assert.Equal((int)(500 * (double)1.1f), ChainHits.AttackPower(row, Fighter));
    }

    [Fact]
    public void TetraPunchDoublesThePreviousLandedHit()
    {
        var row = Row("ESAction_ComboHit", new() { ["Ratio"] = "1.000000", ["ApplyRatio"] = "5.000000" });

        var hits = ChainHits.Resolve(ChainHits.TetraPunch, row, Fighter, Dummy, NoSkills, new Random(3));

        for (var index = 1; index < hits.Count; index++)
        {
            if (hits[index].Damage > 0 && hits[index - 1].Damage > 0) Assert.Equal(hits[index - 1].Damage * 2, hits[index].Damage);
        }
    }

    [Fact]
    public void TwoHitComboScalesEachHitByTheLevelOfItsBaseSkill()
    {
        var row = Row("ESAction_TwoChainAttack", new() { ["Ratio"] = "1.000000" });

        var (first, second) = ChainHits.ComboPowers(row, Fighter, 1, 11);

        Assert.Equal((int)((21 * (double)0.2f - 3.0) * 500.0), first);
        Assert.Equal((int)((31 * (double)0.2f - 3.0) * (500 + 16 * 20) * 1.0 * 0.75), second);
    }

    [Fact]
    public void TwoHitComboNeedsBothBaseSkills()
    {
        Assert.Equal(SkillFailure.General, ChainHits.Refusal(ChainHits.TwoHitCombo, null, 0, new Dictionary<int, int> { [4001] = 3 }));
        Assert.Null(ChainHits.Refusal(ChainHits.TwoHitCombo, null, 0, new Dictionary<int, int> { [4001] = 3, [4101] = 1 }));
    }

    [Fact]
    public void FistFullOfGaldersCostsOneHundredPerHitAndIgnoresDefence()
    {
        var row = Row("ESAction_GellderChainHit", new() { ["Ratio"] = "10.400000", ["Count"] = "4", ["ApplyRatio"] = "5.000000" });

        Assert.Equal(1039, ChainHits.GalderPower(row));
        Assert.Equal(4, ChainHits.GalderHits(row));
        Assert.Equal(400, ChainHits.GalderCost(4));
        Assert.Equal(SkillFailure.General, ChainHits.Refusal(ChainHits.FistFullOfGalders, row, 399, NoSkills));
        Assert.Null(ChainHits.Refusal(ChainHits.FistFullOfGalders, row, 400, NoSkills));
    }

    [Fact]
    public void BlazingStrikeHitsMoreOftenWithLowerDex()
    {
        Assert.Equal(2, ChainHits.BlazingHits(0));
        Assert.Equal(7, ChainHits.BlazingHits(-30));
        Assert.Equal(10, ChainHits.BlazingHits(-200));
        Assert.Equal(0, ChainHits.BlazingHits(40));
    }
}
