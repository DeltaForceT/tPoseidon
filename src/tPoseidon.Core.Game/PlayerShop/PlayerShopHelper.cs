namespace tPoseidon.Core.Game.PlayerShop;

public readonly record struct PlayerShopHelperRow(string ItemName, string KeeperName, string ShopTitle, uint Price, ushort X, ushort Y);

public static class PlayerShopHelperRules
{
    public const int BufferSize = 0x7F8;
    public const int BufferMargin = 0x10;
    public const int CountSize = sizeof(ushort);
    public const int RecordFixedSize = 0xB;

    public static int RecordSize(PlayerShopHelperRow row, Func<string, int> byteCount)
    {
        ArgumentNullException.ThrowIfNull(byteCount);
        return byteCount(row.ItemName) + byteCount(row.KeeperName) + byteCount(row.ShopTitle) + RecordFixedSize;
    }

    public static bool Fits(int used, int recordSize) => BufferSize - BufferMargin > used + recordSize;
}
