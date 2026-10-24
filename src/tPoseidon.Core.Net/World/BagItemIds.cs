namespace tPoseidon.Core.Net.World;

public static class BagItemIds
{
    public const ulong ListHighPart = 0x1_0000_0000;
    public const ulong StoredPartMask = 0xFFFF_FFFF;

    public static ulong OnWire(ulong storedItemUid) => (storedItemUid & StoredPartMask) | ListHighPart;

    public static ulong Stored(ulong wireItemUid) => wireItemUid & StoredPartMask;
}
