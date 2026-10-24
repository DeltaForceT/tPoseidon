using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class BagWeightTests
{
    private static Item Held(byte itemClass, ushort stack) => new()
    {
        ItemUid = 1,
        ItemIndex = 8000,
        ItemClass = itemClass,
        Stack = stack,
        CharacterId = 1,
        SlotKind = SlotKind.Inventory,
        SlotPosition = 0,
    };

    [Fact]
    public void ARareItemWeighsOneUnitWhateverItsStackHolds()
    {
        Assert.Equal(1, Held(Item.RareClass, 600).WeightUnits);
    }

    [Fact]
    public void ACommonItemWeighsItsCount()
    {
        Assert.Equal(12, Held(1, 12).WeightUnits);
    }
}
