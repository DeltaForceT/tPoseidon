using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class JobChangeTests
{
    [Theory]
    [InlineData(10, 10, OccupationRules.SecondJobTier)]
    [InlineData(18, 18, OccupationRules.SecondJobTier)]
    [InlineData(19, 20, OccupationRules.ThirdJobTier)]
    [InlineData(27, 36, OccupationRules.ThirdJobTier)]
    public void TheTierComesFromTheSkillMastersRow(int changeType, int changeJob, byte tier)
    {
        Assert.Equal(tier, OccupationRules.TierOf(changeType, changeJob));
    }

    [Theory]
    [InlineData(10, 11)]
    [InlineData(9, 9)]
    [InlineData(28, 36)]
    public void OtherRowsAreNotAJobChange(int changeType, int changeJob)
    {
        Assert.Null(OccupationRules.TierOf(changeType, changeJob));
    }

    [Fact]
    public void TheJobChangedBroadcastIsEntityTypeJobHairTierAndZero()
    {
        Assert.Equal(new byte[] { 0x7E, 0x2E, 19, 0, 20, 0, 3, 0 }, SkillShopFrames.JobChangedFor(0x2E7E, 19, 20, 0, 3));
    }

    [Fact]
    public void ANewTypeIsToldToThePartyAndChangesItsComposition()
    {
        var service = new PartyService(Encoding.Latin1, PartyRules.FromIni(1, 600_000), TimeProvider.System);
        service.Login(0x1001, "Ana", 1, 40);
        service.Login(0x1002, "Bo", 3, 42);
        service.Make(0x1001, "Crew", "Bo");
        service.AcceptInvite(0x1002);

        var sent = service.KindChanged(0x1002, 12, 12);

        var state = Assert.Single(sent, m => m.To == 0x1001 && m.Opcode == PartyFrames.MemberStateChanged);
        Assert.Equal(new byte[] { 12, 12 }, state.Payload[2..]);
    }
}

public sealed class JobQuestRulesTests
{
    [Theory]
    [InlineData(JobQuestRulesProbe.SecondJob, 60, 50, true)]
    [InlineData(JobQuestRulesProbe.SecondJob, 59, 50, false)]
    [InlineData(JobQuestRulesProbe.SecondJob, 60, 49, false)]
    [InlineData(JobQuestRulesProbe.ThirdJob, 130, 120, true)]
    [InlineData(JobQuestRulesProbe.ThirdJob, 130, 119, false)]
    public void JobQuestsNeedTheLevelAndTmLevelOfTheirTier(int tier, int level, int tmLevel, bool fits)
    {
        Assert.Equal(fits, tPoseidon.Core.Net.Dialogs.JobQuestRules.LevelsFit(tier, level, tmLevel));
    }
}

internal static class JobQuestRulesProbe
{
    public const int SecondJob = tPoseidon.Core.Net.Dialogs.JobQuestRules.SecondJobQuest;
    public const int ThirdJob = tPoseidon.Core.Net.Dialogs.JobQuestRules.ThirdJobQuest;
}
