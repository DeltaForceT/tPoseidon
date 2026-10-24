using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class UtilitySkillsTests
{
    private static readonly ushort[] Known = { 1, 2, 3 };
    private static readonly ushort[] Seen = { 2, 3, 4, 4 };
    private static readonly ushort[] Added = { 4 };
    private static readonly ushort[] Removed = { 1 };

    [Fact]
    public void MinesUseTheirOwnPowerFormulas()
    {
        Assert.Equal((int)((40 + 25) * 8 * (double)2.5f), UtilitySkills.MinePower(UtilitySkills.Mine, 40, 25, 2.5f));
        Assert.Equal((int)((40 * 16 + 25 * 8) * (double)1.6f), UtilitySkills.MinePower(UtilitySkills.MineMaster, 40, 25, 1.6f));
        Assert.Equal(UtilitySkills.MineMasterBlast, UtilitySkills.BlastOf(UtilitySkills.MineMaster));
        Assert.Equal(UtilitySkills.MineBlast, UtilitySkills.BlastOf(UtilitySkills.Mine));
    }

    [Fact]
    public void DashRunsAtSixHundredUntilThirtyShortForAtMostTwoSeconds()
    {
        Assert.Equal(0, UtilitySkills.DashMs(20));
        Assert.Equal(500, UtilitySkills.DashMs(330));
        Assert.Equal(UtilitySkills.DashLongestMs, UtilitySkills.DashMs(5000));
        Assert.Equal((300, 0), UtilitySkills.DashEnd((0, 0), (330, 0), 500));
        Assert.Equal((1201, 0), UtilitySkills.DashEnd((0, 0), (5000, 0), UtilitySkills.DashLongestMs));
    }

    [Fact]
    public void RevealDiffSplitsNewAndLostIds()
    {
        var (added, removed) = UtilitySkills.RevealDiff(Known, Seen);

        Assert.Equal(Added, added);
        Assert.Equal(Removed, removed);
    }

    [Fact]
    public void HidingAndOpticalEyesAreSelfEffects()
    {
        Assert.True(SkillEffectKinds.Find(UtilitySkills.Hiding) is { HidesOwner: true, OnTarget: false, DurationColumn: UtilitySkills.SecondColumn });
        Assert.True(SkillEffectKinds.Find(UtilitySkills.OpticalEyes) is { Periodic: SkillPeriodicHit.RevealScan, PeriodMs: UtilitySkills.RevealScanMs });
        Assert.True(GroundSkills.IsGround(UtilitySkills.Mine));
        Assert.True(ObjectHits.IsSelf(UtilitySkills.BasicDetector));
    }
}
