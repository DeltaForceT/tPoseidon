using System.Text;
using tPoseidon.Core.Game.PvP;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PvpDuelFramesTests
{
    private static readonly Encoding Text = Encoding.ASCII;

    [Fact]
    public void ParticipantIsNameLevelAndTheFiveValueRecord()
    {
        var block = PvpDuelFrames.Participant("Ann", 77, new PvpRecord(1015, 6, 4, 1, 1), Text);
        Assert.Equal(PvpDuelFrames.ParticipantBytes, block.Length);
        Assert.Equal("Ann", Text.GetString(block, 0, 3));
        Assert.All(block[3..32], value => Assert.Equal(0, value));
        Assert.Equal(new byte[] { 77, 0, 0, 0, 0xF7, 0x03, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 }, block[32..]);
    }

    [Fact]
    public void ANameThatFillsTheFieldIsRefused()
    {
        Assert.Throws<ArgumentException>(() => PvpDuelFrames.Participant(new string('a', 32), 1, PvpRecord.Fresh, Text));
    }

    [Fact]
    public void AnswerCarriesTheResponderOnlyWhenAccepted()
    {
        var block = PvpDuelFrames.Participant("Bo", 5, PvpRecord.Fresh, Text);
        var accepted = PvpDuelFrames.Answer(PvpDuelFrames.AnswerAccepted, block);
        Assert.Equal(2 + PvpDuelFrames.ParticipantBytes, accepted.Length);
        Assert.Equal(new byte[] { 0, 0 }, accepted[..2]);
        Assert.Equal(block, accepted[2..]);
        Assert.Equal(new byte[] { 2, 0 }, PvpDuelFrames.Answer(PvpDuelFrames.AnswerBusy, block));
        Assert.Equal(new byte[] { 1, 0 }, PvpDuelFrames.Answer(PvpDuelFrames.AnswerRefused, null));
        Assert.Equal(new byte[] { 4, 0 }, PvpDuelFrames.Cancelled());
    }

    [Fact]
    public void PrepareBattleAndEndFramesFollowTheClientReaders()
    {
        Assert.Equal(new byte[] { 0x88, 0x13, 0, 0, (byte)'C', (byte)'y', 0, 2 }, PvpDuelFrames.Prepare(5000, "Cy", 2, Text));
        Assert.Equal(new byte[] { 0xC0, 0x27, 0x09, 0 }, PvpDuelFrames.Battle(600000));
        Assert.Equal(new byte[] { (byte)'D', (byte)'i', 0 }, PvpDuelFrames.PrepareLost("Di", Text));
        Assert.Equal(new byte[] { 1 }, PvpDuelFrames.End(1));
    }

    [Fact]
    public void ResultIsTheFiftyFourByteBlock()
    {
        var seat = new PvpDuelSeatOutcome(0x0102030405060708, 9, true, true, 1, 600000, new PvpRecord(1015, 6, 4, 1, 1), -15, 0, 2);
        var block = PvpDuelFrames.Result(seat);
        Assert.Equal(PvpDuelFrames.ResultBytes, block.Length);
        byte[] expected =
        [
            1, 1,
            8, 7, 6, 5, 4, 3, 2, 1,
            9, 0, 0, 0,
            1, 0, 0, 0,
            0xC0, 0x27, 0x09, 0,
            0xF7, 0x03, 0, 0, 6, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0,
            0xF1, 0xFF, 0xFF, 0xFF,
            0, 0, 0, 0,
            2, 0, 0, 0,
        ];
        Assert.Equal(expected, block);
    }

    [Fact]
    public void RequestsAreReadFromNulTerminatedNames()
    {
        Assert.True(PvpDuelFrames.TryReadName(new byte[] { (byte)'E', (byte)'d', 0 }, Text, out var name));
        Assert.Equal("Ed", name);
        Assert.False(PvpDuelFrames.TryReadName(new byte[] { (byte)'E', (byte)'d' }, Text, out _));
        Assert.False(PvpDuelFrames.TryReadName(new byte[] { 0 }, Text, out _));
        Assert.True(PvpDuelFrames.TryReadNamedCode(new byte[] { (byte)'E', (byte)'d', 0, 3, 0 }, Text, out var named, out var code));
        Assert.Equal("Ed", named);
        Assert.Equal(PvpDuelFrames.ControlStart, code);
        Assert.False(PvpDuelFrames.TryReadNamedCode(new byte[] { (byte)'E', (byte)'d', 0, 3 }, Text, out _, out _));
    }
}
