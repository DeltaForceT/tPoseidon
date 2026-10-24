using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class RankServiceTests
{
    private sealed class FakeRanks : IRankingStore
    {
        public int RebuildMonsterRanks(DateTime weekStart, DateTime weekEnd, int topCount) => 0;

        public IReadOnlyList<RankingRow> Top(int rankingType, int monsterId) =>
            rankingType == 6 && monsterId == 3057 ? [new RankingRow(1, 7, 2, "Ann", 30, 7000, 0)] : [];
    }

    [Fact]
    public void MonsterRankReplyCarriesRankNameAndValue()
    {
        var service = new RankService(new FakeRanks(), Encoding.Latin1);
        Assert.Equal(
            new byte[] { 1, 0xF1, 0x0B, 0, 1, 1, 0, (byte)'A', (byte)'n', (byte)'n', 0, 0x58, 0x1B, 0, 0 },
            service.Answer([1, 0xF1, 0x0B, 0]));
        Assert.Equal(new byte[] { 1, 0xF1, 0x0B, 1, 0 }, service.Answer([1, 0xF1, 0x0B, 1]));
        Assert.Equal(new byte[] { 2, 3, 0 }, service.Answer([2, 3]));
        Assert.Null(service.Answer([3, 0]));
    }
}
