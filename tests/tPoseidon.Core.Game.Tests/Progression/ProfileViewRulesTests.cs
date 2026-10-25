using System.Text;
using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Game.Tests.Progression;

public sealed class ProfileViewRulesTests
{
    [Fact]
    public void TheDefaultFlagsShareTheInfoAndTheEquipmentButNotTheCollection()
    {
        Assert.Equal(3u, ProfileViewRules.DefaultFlags);
        Assert.True(ProfileViewRules.Shows(ProfileViewRules.DefaultFlags, ProfileViewRules.ShowInfo));
        Assert.True(ProfileViewRules.Shows(ProfileViewRules.DefaultFlags, ProfileViewRules.ShowEquipment));
        Assert.False(ProfileViewRules.Shows(ProfileViewRules.DefaultFlags, ProfileViewRules.ShowCollection));
    }

    [Fact]
    public void AMessageIsKeptUpToTwoHundredBytesAndDroppedAbove()
    {
        Assert.Equal(200, ProfileViewRules.MessageBytes(new string('a', 200), Encoding.ASCII).Length);
        Assert.Empty(ProfileViewRules.MessageBytes(new string('a', 201), Encoding.ASCII));
        Assert.Empty(ProfileViewRules.MessageBytes(string.Empty, Encoding.ASCII));
    }

    [Fact]
    public void TheMessageLimitCountsEncodedBytes()
    {
        Assert.Equal(200, ProfileViewRules.MessageBytes(new string('é', 100), Encoding.UTF8).Length);
        Assert.Empty(ProfileViewRules.MessageBytes(new string('é', 101), Encoding.UTF8));
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(3, 1, 3)]
    [InlineData(4, 2, 0)]
    [InlineData(399, 100, 3)]
    public void AStatLevelIsPointsOverFourPlusOneWithTheRemainderAsTheBar(int points, int level, int remainder)
    {
        Assert.Equal((ushort)level, ProfileViewRules.StatLevelOf(points));
        Assert.Equal((byte)remainder, ProfileViewRules.RemainderOf(points));
    }

    [Fact]
    public void TotalsAreNonNegativeSignedDwordsAndBonusesWrap()
    {
        Assert.Equal(0u, ProfileViewRules.Total(-1));
        Assert.Equal(int.MaxValue, (int)ProfileViewRules.Total(long.MaxValue));
        Assert.Equal(5u, ProfileViewRules.Bonus(15, 10));
        Assert.Equal(unchecked((uint)-5), ProfileViewRules.Bonus(10, 15));
    }
}
