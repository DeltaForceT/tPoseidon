using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ProgressFramesTests
{
    private static CharacterStatBlock Block() => new(
        new short[] { 18, 6, 6, 3, 3, 3, 12, 12, 12, 12, 12, 12 },
        BonusPoints: 12,
        TmPoint: 2,
        TmLevel: 3,
        Level: 4,
        CharacterStatBlock.LoginMarkWhenUnset);

    [Fact]
    public void ExpGained_IsTheAmount()
    {
        Assert.Equal(new byte[] { 0x10, 0x00, 0x00, 0x00 }, ProgressFrames.ExpGained(16));
    }

    [Fact]
    public void LevelUp_IsTheStatBlockThenHpMpExpAndTheGuideListByte()
    {
        var payload = ProgressFrames.LevelUp(Block(), hp: 210, mp: 80, exp: 40);

        Assert.Equal(ProgressFrames.LevelUpSize, payload.Length);
        Assert.Equal(46, payload.Length);
        Assert.Equal(
            new byte[]
            {
                18, 0, 6, 0, 6, 0, 3, 0, 3, 0, 3, 0, 12, 0, 12, 0, 12, 0, 12, 0, 12, 0, 12, 0,
                12, 0, 2, 0, 3, 0, 4, 0, 1,
            },
            payload[..CharacterStatBlock.Size]);
        Assert.Equal(
            new byte[] { 0xD2, 0x00, 0x00, 0x00, 0x50, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00 },
            payload[CharacterStatBlock.Size..]);
    }

    [Fact]
    public void LevelSeen_IsThePlayerAndTheLevel()
    {
        Assert.Equal(new byte[] { 0x01, 0x03, 0x04, 0x00 }, ProgressFrames.LevelSeen(0x0301, 4));
    }

    [Fact]
    public void TmTotal_IsTheSourceByteAndTheTotal()
    {
        Assert.Equal(
            new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00 },
            ProgressFrames.TmTotal(ProgressFrames.TmFromMonsterKill, 256));
    }

    [Fact]
    public void TmLevelUp_IsTheTypeLevelExpAndPoints()
    {
        Assert.Equal(
            new byte[] { 0x02, 0x03, 0x00, 0x09, 0x00, 0x00, 0x00, 0x04, 0x00 },
            ProgressFrames.TmLevelUp(characterType: 2, tmLevel: 3, tmExp: 9, tmPoint: 4));
    }

    [Fact]
    public void StatPoints_IsTheBonusPointsLeftThenTheTwelveStats()
    {
        var payload = ProgressFrames.StatPoints(3, new short[] { 22, 6, 6, 3, 3, 3, 12, 12, 12, 17, 9, 9 });

        Assert.Equal(ProgressFrames.StatPointsSize, payload.Length);
        Assert.Equal(
            new byte[] { 3, 0, 22, 0, 6, 0, 6, 0, 3, 0, 3, 0, 3, 0, 12, 0, 12, 0, 12, 0, 17, 0, 9, 0, 9, 0 },
            payload);
    }

    [Fact]
    public void OwnerFrames_NameTheMonsterAndTheOwner()
    {
        Assert.Equal(new byte[] { 0x01, 0x02, 0x01, 0x03 }, ProgressFrames.OwnerTaken(0x0201, 0x0301));
        Assert.Equal(new byte[] { 0x01, 0x02 }, ProgressFrames.OwnerReleased(0x0201));
    }

    [Theory]
    [InlineData(116f, 29)]
    [InlineData(119.9f, 29)]
    [InlineData(120f, 30)]
    [InlineData(184f, 46)]
    [InlineData(0f, 0)]
    public void Speed_IsAQuarterOfTheSpeedTruncated(float speed, byte quarter)
    {
        Assert.Equal(new byte[] { 0x01, 0x02, quarter }, ProgressFrames.Speed(0x0201, speed));
    }
}
