using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PartyShareTests
{
    private const ulong Ana = 0x1001;
    private const ulong Bo = 0x1002;
    private const ulong Cy = 0x1003;

    private static PartyService PartyOfThreeOnOneMap()
    {
        var service = new PartyService(Encoding.Latin1, PartyRules.FromIni(1, 600_000), TimeProvider.System);
        service.Login(Ana, "Ana", 1, 40);
        service.Login(Bo, "Bo", 3, 42);
        service.Login(Cy, "Cy", 5, 44);
        service.Make(Ana, "Crew", "Bo");
        service.AcceptInvite(Bo);
        service.Invite(Ana, "Cy");
        service.AcceptInvite(Cy);
        service.ZoneEntered(Ana, 7);
        service.ZoneEntered(Bo, 7);
        return service;
    }

    [Fact]
    public void AnEvenSplitGivesTheSameMapGroupItsShareAndBonus()
    {
        var service = PartyOfThreeOnOneMap();

        var sent = service.ChangeDivision(Ana, PartyDivision.EqualRounded, Array.Empty<PartyDivisionShare>());

        var order = Assert.Single(sent.Orders, o => o.OrderType == SystemLinkFrames.OrderShareExp && !o.IsCancel);
        var share = PartyBonusTables.DecodeShare(order.Data);
        Assert.Equal(1, share.ShareFlag);
        Assert.Equal(new[] { Ana, Bo }, share.Entries.Select(e => e.CharacterId).OrderBy(id => id));
        Assert.All(share.Entries, e => Assert.Equal((ushort)50, e.ExpPercent));
        Assert.All(share.Entries, e => Assert.Equal(PartyBonusTables.ExpMultiplier(PartyType.Normal, 2) - 1f, e.ExpBonus));
        Assert.Equal(2, sent.Count(m => m.Opcode == PartyFrames.PartyBonus));
    }

    [Fact]
    public void ADeadMemberLeavesTheGroup()
    {
        var service = PartyOfThreeOnOneMap();
        service.ChangeDivision(Ana, PartyDivision.EqualRounded, Array.Empty<PartyDivisionShare>());

        var sent = service.SetDead(Bo, dead: true);

        var order = Assert.Single(sent.Orders, o => o.OrderType == SystemLinkFrames.OrderShareExp && !o.IsCancel);
        var entry = Assert.Single(PartyBonusTables.DecodeShare(order.Data).Entries);
        Assert.Equal((Ana, (ushort)100, 0f), (entry.CharacterId, entry.ExpPercent, entry.ExpBonus));
    }

    [Fact]
    public void IndividualDivisionKeepsEachEarnersExpWithoutBonus()
    {
        var service = PartyOfThreeOnOneMap();

        var sent = service.ChangeDivision(Ana, PartyDivision.Equal, Array.Empty<PartyDivisionShare>());

        var share = PartyBonusTables.DecodeShare(Assert.Single(sent.Orders, o => !o.IsCancel).Data);
        Assert.Equal(0, share.ShareFlag);
        Assert.All(share.Entries, e => Assert.Equal(((ushort)100, 0f), (e.ExpPercent, e.ExpBonus)));
    }

    [Fact]
    public void TheGameServerSplitFollowsTheKrCalculator()
    {
        Assert.Equal((int)((float)(1000 + (int)(1000 * (0.2f + PartyShareBook.RateEpsilon))) * 0.5f), PartyShareBook.GainFor(1000, 0.2f, 50));
        Assert.Equal(1005, PartyShareBook.GainFor(1000, 0f, 100));
    }

    [Fact]
    public void ARecoveryOrderRoundTrips()
    {
        var recovery = new PartyQuestRecovery(Bo, 77, 3006, 2094, 5, 64, 123, 1_200_000, 2, new[] { (Ana, (ushort)3), (Bo, (ushort)2) });

        var bytes = SystemLinkFrames.EncodeQuestRecovery(recovery);
        var back = SystemLinkFrames.DecodeQuestRecovery(bytes);

        Assert.Equal(SystemLinkFrames.QuestRecoveryLength, bytes.Length);
        Assert.Equal(recovery with { Members = back.Members }, back);
        Assert.Equal(recovery.Members, back.Members);
    }
}
