using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.S2;

public class GuideHistoryFrameTests
{
    [Fact]
    public void NoStore_ReturnsAllZeros()
    {
        var frame = GuideHistoryFrameBuilder.Build(0x1234, store: null);
        Assert.Equal(264, frame.Length);
        Assert.All(frame, b => Assert.Equal(0, b));
    }

    [Fact]
    public void EmptyStore_ReturnsAllZeros()
    {
        var store = new InMemoryGuideHistoryStore();
        var frame = GuideHistoryFrameBuilder.Build(0x1234, store);
        Assert.Equal(264, frame.Length);
        Assert.All(frame, b => Assert.Equal(0, b));
    }

    [Fact]
    public void TutorialGuide_WrittenAsLittleEndianAtOffset256()
    {
        var store = new InMemoryGuideHistoryStore();
        store.UpsertTutorialGuide(0x1234, 0xDEADBEEFCAFEBABE);

        var frame = GuideHistoryFrameBuilder.Build(0x1234, store);

        for (int i = 0; i < 256; i++) Assert.Equal(0, frame[i]);

        Assert.Equal(0xBE, frame[256]);
        Assert.Equal(0xBA, frame[257]);
        Assert.Equal(0xFE, frame[258]);
        Assert.Equal(0xCA, frame[259]);
        Assert.Equal(0xEF, frame[260]);
        Assert.Equal(0xBE, frame[261]);
        Assert.Equal(0xAD, frame[262]);
        Assert.Equal(0xDE, frame[263]);
    }

    [Fact]
    public void SystemGuide_WrittenAsAsciiAtOffset0()
    {
        var store = new InMemoryGuideHistoryStore();
        store.UpsertSystemGuide(0x1234, "abc");

        var frame = GuideHistoryFrameBuilder.Build(0x1234, store);

        Assert.Equal((byte)'a', frame[0]);
        Assert.Equal((byte)'b', frame[1]);
        Assert.Equal((byte)'c', frame[2]);
        for (int i = 3; i < 256; i++) Assert.Equal(0, frame[i]);
        for (int i = 256; i < 264; i++) Assert.Equal(0, frame[i]);
    }

    [Fact]
    public void SystemGuide_OverflowsBufferAreTruncatedAt256()
    {
        var store = new InMemoryGuideHistoryStore();
        var oversize = new string('A', 300);
        store.UpsertSystemGuide(0x1234, oversize);

        var frame = GuideHistoryFrameBuilder.Build(0x1234, store);

        for (int i = 0; i < 256; i++) Assert.Equal((byte)'A', frame[i]);
        for (int i = 256; i < 264; i++) Assert.Equal(0, frame[i]);
    }
}
