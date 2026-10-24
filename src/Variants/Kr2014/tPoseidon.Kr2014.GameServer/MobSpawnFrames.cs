using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Kr2014.GameServer;

internal static class MobSpawnFrames
{
    private const int TrailerSize = 23;
    private const int TrailerSpeedOffset = 2;
    private const int TrailerOwnerOffset = TrailerSize - 3;
    private const int TrailerFlagOffset = 7;
    private const byte TrailerFlag = 0x01;
    private const uint SpawnReserved = 0x00000001;

    public static byte[] Encode(MonsterEntity mob, MonsterParamEntry? definition)
    {
        ArgumentNullException.ThrowIfNull(mob);

        var trailer = new byte[TrailerSize];
        trailer[TrailerSpeedOffset] = tPoseidon.Core.Net.World.ProgressFrames.SpeedQuarter(mob.Speed);
        trailer[TrailerFlagOffset] = TrailerFlag;
        trailer[TrailerOwnerOffset] = (byte)(mob.OwnerEntityId & 0xFF);
        trailer[TrailerOwnerOffset + 1] = (byte)(mob.OwnerEntityId >> 8);

        return new NpcSpawnPacket
        {
            EntityId = mob.EntityId,
            Kind = NpcSpawnPacket.EntityKind.Mob,
            TemplateId = (ushort)(mob.MonsterParamId & 0xFFFF),
            UnknownField6 = 0,
            UnknownField8 = (ushort)Math.Clamp(definition?.Level ?? 1, 1, ushort.MaxValue),
            Reserved10 = SpawnReserved,
            SpawnX = mob.SpawnX,
            SpawnY = mob.SpawnY,
            CurrentX = mob.CurrentX,
            CurrentY = mob.CurrentY,
            Name = mob.Name,
            Trailer = trailer,
        }.EncodePayload();
    }
}
