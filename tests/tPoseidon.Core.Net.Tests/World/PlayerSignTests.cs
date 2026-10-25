using System.Text;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PlayerSignTests
{
    private const ushort Entity = 0x0102;
    private static readonly Encoding Latin = Encoding.Latin1;

    private static byte[] Shown(string text) =>
        new byte[] { 0x02, 0x01 }.Concat(Encoding.ASCII.GetBytes(text)).Append((byte)0).ToArray();

    [Fact]
    public void AHeadMessageIsBroadcastWithTheEntityAndTheText()
    {
        var sign = new PlayerSign();

        var frames = sign.ShowHeadMessage(Entity, "hi", Latin);

        var frame = Assert.Single(frames);
        Assert.Equal(PlayerSign.HeadMessageShown, frame.Opcode);
        Assert.Equal(Shown("hi"), frame.Payload);
        Assert.Equal(SignKind.HeadMessage, sign.Kind);
    }

    [Fact]
    public void TheTextIsCutAtFiftyBytes()
    {
        var frame = Assert.Single(new PlayerSign().ShowHeadMessage(Entity, new string('a', 80), Latin));

        Assert.Equal(sizeof(ushort) + PlayerSign.TextBytes + 1, frame.Payload.Length);
        Assert.Equal(0, frame.Payload[^1]);
    }

    [Fact]
    public void ACardBattleTitleFirstClearsAHeadMessage()
    {
        var sign = new PlayerSign();
        sign.ShowHeadMessage(Entity, "hi", Latin);

        var frames = sign.ShowCardBattleTitle(Entity, "duel", Latin);

        Assert.Equal(new ushort[] { PlayerSign.HeadMessageCleared, PlayerSign.CardBattleTitleShown }, frames.Select(f => f.Opcode));
        Assert.Equal(new byte[] { 0x02, 0x01 }, frames[0].Payload);
        Assert.Equal(Shown("duel"), frames[1].Payload);
        Assert.Equal(SignKind.CardBattleTitle, sign.Kind);
    }

    [Fact]
    public void AHeadMessageFirstClearsACardBattleTitle()
    {
        var sign = new PlayerSign();
        sign.ShowCardBattleTitle(Entity, "duel", Latin);

        var frames = sign.ShowHeadMessage(Entity, "hi", Latin);

        Assert.Equal(new ushort[] { PlayerSign.CardBattleTitleCleared, PlayerSign.HeadMessageShown }, frames.Select(f => f.Opcode));
    }

    [Fact]
    public void EndingTheCardBattleTitleAlwaysBroadcastsTheClear()
    {
        var sign = new PlayerSign();
        sign.ShowCardBattleTitle(Entity, "duel", Latin);

        var active = Assert.Single(sign.EndCardBattleTitle(Entity));
        var idle = Assert.Single(sign.EndCardBattleTitle(Entity));

        Assert.Equal(PlayerSign.CardBattleTitleCleared, active.Opcode);
        Assert.Equal(new byte[] { 0x02, 0x01 }, active.Payload);
        Assert.Equal(active.Opcode, idle.Opcode);
        Assert.Equal(active.Payload, idle.Payload);
        Assert.Equal(SignKind.None, sign.Kind);
    }

    [Fact]
    public void AFortuneSignClearsTheOtherTwoFirst()
    {
        var sign = new PlayerSign();
        sign.ShowHeadMessage(Entity, "hi", Latin);

        var frames = sign.ShowFortuneSign(Entity, "luck", Latin);

        Assert.Equal(new ushort[] { PlayerSign.HeadMessageCleared, PlayerSign.FortuneSignShown }, frames.Select(f => f.Opcode));
        Assert.Equal(Shown("luck"), frames[1].Payload);
        Assert.Equal(SignKind.FortuneSign, sign.Kind);
    }

    [Fact]
    public void EndingTheFortuneSignBroadcastsTheClear()
    {
        var sign = new PlayerSign();
        sign.ShowFortuneSign(Entity, "luck", Latin);

        var frame = Assert.Single(sign.EndFortuneSign(Entity));

        Assert.Equal(PlayerSign.FortuneSignCleared, frame.Opcode);
        Assert.Equal(new byte[] { 0x02, 0x01 }, frame.Payload);
        Assert.Equal(SignKind.None, sign.Kind);
    }

    [Fact]
    public void AnActionClearsTheHeadMessageAndTheTitleButNotTheFortuneSign()
    {
        var head = new PlayerSign();
        head.ShowHeadMessage(Entity, "hi", Latin);
        var title = new PlayerSign();
        title.ShowCardBattleTitle(Entity, "duel", Latin);
        var fortune = new PlayerSign();
        fortune.ShowFortuneSign(Entity, "luck", Latin);

        Assert.Equal(PlayerSign.HeadMessageCleared, Assert.Single(head.ClearOnAction(Entity)).Opcode);
        Assert.Equal(PlayerSign.CardBattleTitleCleared, Assert.Single(title.ClearOnAction(Entity)).Opcode);
        Assert.Empty(fortune.ClearOnAction(Entity));
        Assert.Empty(new PlayerSign().ClearOnAction(Entity));
        Assert.Equal(SignKind.FortuneSign, fortune.Kind);
    }

    [Fact]
    public void TheRequestOpcodesMatchTheClientSenders()
    {
        Assert.Equal(0x013D, PlayerSign.CardBattleTitleRequest);
        Assert.Equal(0x013E, PlayerSign.CardBattleTitleEndRequest);
        Assert.Equal(0x03DE, PlayerSign.FortuneSignRequest);
        Assert.Equal(0x03DF, PlayerSign.FortuneSignEndRequest);
        Assert.Equal(0x02C9, tPoseidon.Core.Net.Social.ChatFrames.GhostSay);
        Assert.Equal(0x02C8, tPoseidon.Core.Net.Social.ChatFrames.GhostSaid);
    }

    [Fact]
    public void ARecruitNoticeFirstClearsTheHeadMessageAndTheCardBattleTitle()
    {
        var sign = new PlayerSign();
        sign.ShowHeadMessage(Entity, "hi", Latin);

        var frames = sign.ShowPartyBoardNotice(Entity, 1, "Join", Latin);

        Assert.Collection(
            frames,
            cleared => Assert.Equal(PlayerSign.HeadMessageCleared, cleared.Opcode),
            shown =>
            {
                Assert.Equal(PartyBoardFrames.HeadNoticeShown, shown.Opcode);
                Assert.Equal(new byte[] { 0x02, 0x01, 0x01 }.Concat(Encoding.ASCII.GetBytes("Join")).Append((byte)0).ToArray(), shown.Payload);
            });
        Assert.Equal(SignKind.PartyBoardNotice, sign.Kind);
    }

    [Fact]
    public void ClearingARecruitNoticeBroadcastsTheEntityOnlyWhenItIsHeld()
    {
        var sign = new PlayerSign();
        Assert.Empty(sign.ClearPartyBoardNotice(Entity));
        sign.ShowPartyBoardNotice(Entity, 2, "Join", Latin);

        var frame = Assert.Single(sign.ClearPartyBoardNotice(Entity));

        Assert.Equal(PartyBoardFrames.HeadNoticeCleared, frame.Opcode);
        Assert.Equal(new byte[] { 0x02, 0x01 }, frame.Payload);
        Assert.Equal(SignKind.None, sign.Kind);
    }
}
