using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Kr2014.GameServer;

internal static class ShopSpawnFrames
{
    private const int TrailerSize = 12;
    private const int TrailerDirectionOffset = 1;
    private const byte DefaultDirection = 0x80;
    private const int SkillShopTrailerSize = TrailerSize + 2;

    public static byte[] Encode(ShopEntity shop, string name)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(name);

        var trailer = new byte[TrailerSize];
        trailer[TrailerDirectionOffset] = DefaultDirection;

        return new NpcSpawnPacket
        {
            EntityId = shop.EntityId,
            Kind = NpcSpawnPacket.EntityKind.Shop,
            TemplateId = (ushort)shop.NpcType,
            SpawnX = shop.SpawnX,
            SpawnY = shop.SpawnY,
            CurrentX = shop.SpawnX,
            CurrentY = shop.SpawnY,
            Name = name,
            Trailer = trailer,
        }.EncodePayload();
    }

    public static byte[] EncodeSkillShop(SkillShopEntity shop, string name)
    {
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(name);

        var trailer = new byte[SkillShopTrailerSize];
        trailer[TrailerDirectionOffset] = DefaultDirection;
        trailer[TrailerSize] = (byte)(shop.PosId & 0xFF);
        trailer[TrailerSize + 1] = (byte)((shop.PosId >> 8) & 0xFF);

        return new NpcSpawnPacket
        {
            EntityId = shop.EntityId,
            Kind = NpcSpawnPacket.EntityKind.SkillShop,
            TemplateId = (ushort)shop.NpcType,
            SpawnX = shop.SpawnX,
            SpawnY = shop.SpawnY,
            CurrentX = shop.SpawnX,
            CurrentY = shop.SpawnY,
            Name = name,
            Trailer = trailer,
        }.EncodePayload();
    }
}
