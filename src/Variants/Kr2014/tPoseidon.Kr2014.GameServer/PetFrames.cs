using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Kr2014.GameServer;

internal static class PetFrames
{
    private const int CharacterPartSize = 12;
    private const int TrailerSize = CharacterPartSize + 2;
    private const int TrailerDirectionOffset = 1;
    private const int TrailerSpeedOffset = 2;
    private const byte DefaultDirection = 0x80;
    private const int RemovalSize = 3;
    private const int TeleportSize = 6;

    public static byte[] Spawn(PetActor pet, ushort ownerEntityId, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(pet);

        var trailer = new byte[TrailerSize];
        trailer[TrailerDirectionOffset] = DefaultDirection;
        trailer[TrailerSpeedOffset] = pet.SpeedQuarter;
        trailer[CharacterPartSize] = (byte)(ownerEntityId & 0xFF);
        trailer[CharacterPartSize + 1] = (byte)(ownerEntityId >> 8);

        return new NpcSpawnPacket
        {
            EntityId = pet.EntityId,
            Kind = NpcSpawnPacket.EntityKind.Pet,
            TemplateId = (ushort)pet.CharacterType,
            SpawnX = (ushort)x,
            SpawnY = (ushort)y,
            CurrentX = (ushort)x,
            CurrentY = (ushort)y,
            Name = pet.Name,
            Trailer = trailer,
        }.EncodePayload();
    }

    public static byte[] Removed(ushort petEntityId)
    {
        var w = new PacketWriter(RemovalSize);
        w.WriteUInt16(petEntityId);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] Teleported(ushort petEntityId, int x, int y)
    {
        var w = new PacketWriter(TeleportSize);
        w.WriteUInt16(petEntityId);
        w.WriteUInt16((ushort)x);
        w.WriteUInt16((ushort)y);
        return w.ToArray();
    }
}
