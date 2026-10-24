using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ExchangeFramesTests
{
    private static readonly int[] OneOffer = { 1 };

    [Fact]
    public void TheListIsCountAndIds()
    {
        Assert.Equal(new byte[] { 0x01, 0x01, 0x00 }, ExchangeFrames.List(OneOffer));
    }

    [Fact]
    public void TheOfferAnswerHasNoTypeAndNoRate()
    {
        var offer = new ExchangeOffer { Id = 1, Results = new[] { 600019 }, Requirements = new[] { new CompoundRequirement(11894, 1) } };

        Assert.Equal(new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0xD3, 0x27, 0x09, 0x00,
            0x01, 0x76, 0x2E, 0x00, 0x00, 0x01,
        }, ExchangeFrames.Offer(offer));
    }

    [Fact]
    public void TheRequestEndsWithTheTimesByte()
    {
        var payload = new byte[]
        {
            0x01, 0x00, 0x01,
            0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x76, 0x2E, 0x00, 0x00, 0x01, 0x00,
            0x01,
        };

        Assert.True(ExchangeFrames.TryReadExchange(payload, out var id, out var materials, out var times));
        Assert.Equal((1, 1), (id, times));
        Assert.Single(materials);
        Assert.False(ExchangeFrames.TryReadExchange(payload.AsSpan(0, 17), out _, out _, out _));
    }
}

public sealed class RareRecordMixSlotTests
{
    [Fact]
    public void TheFourthEquipmentByteIsTheMixSlotCount()
    {
        var record = QuestResultFrames.ItemOperation(new BagOperation(QuestResultFrames.Created, 2, 500014, 5, 1, 3));

        Assert.Equal(new byte[] { 0, 0, 0, 3 }, record[16..20]);
    }
}
