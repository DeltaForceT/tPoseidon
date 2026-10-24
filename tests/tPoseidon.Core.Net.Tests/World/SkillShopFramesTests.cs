using System.Text;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SkillShopFramesTests
{
    private static readonly int[] MasterableSkills = { 1001, 1104 };

    [Fact]
    public void OpenedCarriesNpcTypeCountAndTheFirstPage()
    {
        var payload = SkillShopFrames.Opened(0x0123, 6300, 3, "Hi", Encoding.ASCII);

        Assert.Equal(new byte[] { 0x23, 0x01, 0x9C, 0x18, 0x03, (byte)'H', (byte)'i', 0x00 }, payload);
    }

    [Fact]
    public void OpenedWithoutPagesHasNoText()
    {
        Assert.Equal(new byte[] { 0x23, 0x01, 0x9C, 0x18, 0x00 }, SkillShopFrames.Opened(0x0123, 6300, 0, "ignored", Encoding.ASCII));
    }

    [Fact]
    public void APageCarriesTextKindJobQuestAndIndex()
    {
        var payload = SkillShopFrames.Page(0x0123, "Ok", 1, 7, 2, Encoding.ASCII);

        Assert.Equal(new byte[] { 0x23, 0x01, (byte)'O', (byte)'k', 0x00, 0x01, 0x00, 0x07, 0x00, 0x02, 0x00 }, payload);
    }

    [Fact]
    public void TheMasterListIsACountOfSkillIds()
    {
        Assert.Equal(new byte[] { 0x02, 0x00, 0xE9, 0x03, 0x50, 0x04 }, SkillShopFrames.MasterList(MasterableSkills));
    }

    [Fact]
    public void TheJobChangeViewCarriesTypeAndJob()
    {
        Assert.Equal(new byte[] { 0x01, 0x00, 0x18, 0x00 }, SkillShopFrames.JobChangeView(1, 24));
    }
}
