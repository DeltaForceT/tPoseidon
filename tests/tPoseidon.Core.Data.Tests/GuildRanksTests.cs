namespace tPoseidon.Core.Data.Tests;

public sealed class GuildRanksTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(300, 255)]
    [InlineData(GuildLadderRules.NotRanked, 0)]
    public void LadderPositionsShowAsARankByte(int position, byte shown)
    {
        Assert.Equal(shown, GuildLadderRules.DisplayedRank(position));
    }
}
