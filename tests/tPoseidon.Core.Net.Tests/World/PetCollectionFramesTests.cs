using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PetCollectionFramesTests
{
    [Fact]
    public void StartIsPetThenFoodThenCount()
    {
        var body = new byte[PetCollectionFrames.StartSize];
        body[0] = 0x11;
        body[4] = 0x01;
        body[8] = 0x22;
        body[16] = 0x05;
        body[17] = 0x01;
        Assert.True(PetCollectionFrames.TryReadStart(body, out var request));
        Assert.Equal(new PetCollectionStartFrame(0x1_0000_0011, 0x22, 0x0105), request);
        Assert.False(PetCollectionFrames.TryReadStart(body.AsSpan(0, PetCollectionFrames.StartSize - 1), out _));
    }

    [Fact]
    public void ItemRequestIsOneUid()
    {
        Assert.True(PetCollectionFrames.TryReadItem(new byte[] { 7, 0, 0, 0, 0, 0, 0, 0 }, out var uid));
        Assert.Equal(7UL, uid);
        Assert.False(PetCollectionFrames.TryReadItem(new byte[7], out _));
        Assert.Equal(new byte[] { 4 }, PetCollectionFrames.Code(4));
    }

    [Fact]
    public void CollectedCarriesTheFoodOnlyWhenSpent()
    {
        var collected = new BagOperation(QuestResultFrames.Created, 1, 500, 9, 2);
        var food = new BagOperation(QuestResultFrames.StackChanged, 1, 19854, 0x1_0000_0003, 4);
        var spent = PetCollectionFrames.Collected(collected, food);
        var item = QuestResultFrames.ItemOperation(collected);
        Assert.Equal(item, spent[..item.Length]);
        Assert.Equal(new byte[] { 1, 0 }, spent[item.Length..(item.Length + 2)]);
        Assert.Equal(QuestResultFrames.ItemOperation(food), spent[(item.Length + 2)..]);
        var kept = PetCollectionFrames.Collected(collected, null);
        Assert.Equal(item.Concat(new byte[] { 0, 0 }).ToArray(), kept);
    }

    [Fact]
    public void RareCollectedItemHasStateBytesAndNoOptions()
    {
        var collected = new BagOperation(QuestResultFrames.Created, 2, 11640, 0x10, 1);
        var frame = PetCollectionFrames.Collected(collected, null);
        Assert.Equal(
            new byte[] { 4, 2, 0x78, 0x2D, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0 },
            frame);
    }

    [Fact]
    public void MovedIsPetBagRemovalThenBagRecord()
    {
        var removal = new BagOperation(QuestResultFrames.Removed, 1, 500, 9, 0);
        var placed = new BagOperation(QuestResultFrames.StackChanged, 1, 500, 0x1_0000_0002, 12);
        Assert.Equal(QuestResultFrames.ItemOperation(removal).Concat(QuestResultFrames.ItemOperation(placed)).ToArray(), PetCollectionFrames.Moved(removal, placed));
        Assert.Equal(QuestResultFrames.ItemOperation(removal), PetCollectionFrames.Destroyed(removal));
    }

    [Fact]
    public void BagListHasNoActionByte()
    {
        var frame = PetCollectionFrames.Bag(new[]
        {
            new PetBagEntry(1, 500, 9, 3),
            new PetBagEntry(2, 11640, 10, 1),
        });
        Assert.Equal(
            new byte[]
            {
                2, 0,
                1, 0xF4, 1, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0, 3, 0,
                2, 0x78, 0x2D, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
            },
            frame);
        Assert.Equal(new byte[] { 0, 0 }, PetCollectionFrames.Bag(Array.Empty<PetBagEntry>()));
    }
}
