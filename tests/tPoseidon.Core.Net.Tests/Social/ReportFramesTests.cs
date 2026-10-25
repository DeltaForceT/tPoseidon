using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class ReportFramesTests
{
    [Fact]
    public void AChatLineIsNamePrefixTextAndALineFeed()
    {
        Assert.Equal("Tata(whisper):hello\n", ReportFrames.ChatLineText(new ReportChatLine("Tata", 3, "hello")));
    }

    [Fact]
    public void UnknownChatTypesAreEtc()
    {
        Assert.Equal("Tata(etc):x\n", ReportFrames.ChatLineText(new ReportChatLine("Tata", 99, "x")));
    }

    [Fact]
    public void LinesArePackedIntoRowsOfAtMost0x707Characters()
    {
        var line = new ReportChatLine("N", 1, new string('a', 120));
        var rows = ReportFrames.PackChatting(Enumerable.Repeat(line, 20));

        Assert.All(rows, row => Assert.True(row.Length <= ReportFrames.ChattingPack));
        Assert.Equal(20 * ReportFrames.ChatLineText(line).Length, rows.Sum(row => row.Length));
        Assert.True(rows.Count > 1);
    }

    [Fact]
    public void ReportingAGameMasterIsAnsweredWithAnEmptyFrame()
    {
        Assert.Equal(0x2727, ReportFrames.DefendantIsGameMaster);
    }
}
