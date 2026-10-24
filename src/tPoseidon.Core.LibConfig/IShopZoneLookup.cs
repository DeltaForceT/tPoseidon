namespace tPoseidon.Core.LibConfig;

public interface IShopZoneLookup
{
    IReadOnlyList<ShopZoneEntry> GetByMap(string mapName);
    IReadOnlyCollection<ShopZoneEntry> All { get; }
    int Count { get; }
}
