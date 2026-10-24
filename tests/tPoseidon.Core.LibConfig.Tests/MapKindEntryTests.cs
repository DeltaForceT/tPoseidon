using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public sealed class MapKindEntryTests
{
    [Theory]
    [InlineData(1, 0, true, true)]
    [InlineData(2, 0, true, true)]
    [InlineData(1, 3, false, false)]
    [InlineData(2, 5, true, false)]
    [InlineData(3, 0, false, false)]
    [InlineData(4, 0, false, false)]
    [InlineData(5, 0, false, false)]
    public void ReturnMapsAreTypeOneOrTwoOutsideTheExcludedClasses(int type, int mapClass, bool atLogin, bool onArrival)
    {
        var kind = new MapKindEntry(Id: 33, type, mapClass);

        Assert.Equal(atLogin, kind.IsReturnMapAtLogin);
        Assert.Equal(onArrival, kind.IsReturnMapOnArrival);
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(3, 0x04 | 0x10, true)]
    [InlineData(7, 0, false)]
    [InlineData(8, 0, false)]
    [InlineData(9, 0, false)]
    [InlineData(3, 0x20, false)]
    [InlineData(3, 0x04 | 0x20, false)]
    public void RebirthItemsWorkOutsideThreeMapTypesAndOneOption(int type, int option, bool allowed)
    {
        Assert.Equal(allowed, new MapKindEntry(Id: 33, type, Class: 0, option).AllowsRebirthItem);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(3, 0x10 | 0x80, true)]
    [InlineData(2, 0, false)]
    [InlineData(0, 0x100, false)]
    [InlineData(5, 0x100 | 0x4, false)]
    public void LoverMoveIsRefusedOnClassTwoAndOption0x100Maps(int mapClass, int option, bool allowed)
    {
        Assert.Equal(allowed, new MapKindEntry(Id: 33, Type: 0, mapClass, option).AllowsLoverMove);
    }
}
