using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GuideHistoryFramesTests
{
    [Fact]
    public void TheHistoryIsTheHelpBlockThenTheTutorialStep()
    {
        var help = new byte[GuideHistoryFrames.SystemGuideSize];
        help[0] = 1;
        help[255] = 2;

        var payload = GuideHistoryFrames.History(help, 5);

        Assert.Equal(GuideHistoryFrames.HistorySize, payload.Length);
        Assert.Equal(help, payload[..256]);
        Assert.Equal(new byte[] { 5, 0, 0, 0, 0, 0, 0, 0 }, payload[256..]);
    }

    [Fact]
    public void AFreshCharacterGetsAnAllZeroHistory()
    {
        Assert.Equal(new byte[GuideHistoryFrames.HistorySize], GuideHistoryFrames.History(GuideHistoryFrames.FromStored(null), 0));
    }

    [Fact]
    public void TheClientReportsTheStepAsEightBytesAndTheHelpBlockAsTwoHundredFiftySix()
    {
        Assert.Equal(3UL, GuideHistoryFrames.ReadTutorialGuide(new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 }));
        Assert.Null(GuideHistoryFrames.ReadTutorialGuide(new byte[] { 3, 0, 0, 0 }));
        Assert.NotNull(GuideHistoryFrames.ReadSystemGuide(new byte[256]));
        Assert.Null(GuideHistoryFrames.ReadSystemGuide(new byte[255]));
    }

    [Fact]
    public void TheHelpBlockIsStoredAsTextAndComesBackUnchanged()
    {
        var help = new byte[GuideHistoryFrames.SystemGuideSize];
        help[3] = 0xFF;
        help[200] = 0x10;

        var stored = GuideHistoryFrames.Stored(help);

        Assert.Equal(512, stored.Length);
        Assert.Equal(help, GuideHistoryFrames.FromStored(stored));
    }

    [Theory]
    [InlineData("")]
    [InlineData("seen")]
    public void AStoredValueThatIsNotABlockReadsAsNothingSeen(string stored)
    {
        Assert.Equal(new byte[GuideHistoryFrames.SystemGuideSize], GuideHistoryFrames.FromStored(stored));
    }
}
