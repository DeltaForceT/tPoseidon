using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public static class MobSpawnFrames
{
    public const Opcode BirthOpcode = Opcode.S2C_PLAYER_SPAWN_POST_WARP;
    public const Opcode InViewOpcode = Opcode.NPC_SPAWN;

    private const int TrailerSize = 23;
    private const int TrailerSpeedOffset = 2;
    private const int TrailerOwnerOffset = TrailerSize - 3;
    private const int TrailerPvpModeOffset = TrailerSize - 1;
    private const int TrailerFlagOffset = 7;
    private const byte TrailerFlag = 0x01;
    private const uint SpawnReserved = 0x00000001;

    public static byte[] Encode(MonsterEntity mob, MonsterParamEntry? definition, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(mob);

        var (positionX, positionY) = mob.GetInterpolatedPosition(now);

        var trailer = new byte[TrailerSize];
        trailer[TrailerSpeedOffset] = ProgressFrames.SpeedQuarter(mob.Speed);
        trailer[TrailerFlagOffset] = TrailerFlag;
        trailer[TrailerOwnerOffset] = (byte)(mob.OwnerEntityId & 0xFF);
        trailer[TrailerOwnerOffset + 1] = (byte)(mob.OwnerEntityId >> 8);
        trailer[TrailerPvpModeOffset] = mob.PvpMode;

        return new NpcSpawnPacket
        {
            EntityId = mob.EntityId,
            Kind = NpcSpawnPacket.EntityKind.Mob,
            TemplateId = (ushort)(mob.MonsterParamId & 0xFFFF),
            UnknownField6 = 0,
            UnknownField8 = (ushort)Math.Clamp(definition?.Level ?? 1, 1, ushort.MaxValue),
            Reserved10 = SpawnReserved,
            PositionX = positionX,
            PositionY = positionY,
            DestinationX = mob.DestinationX,
            DestinationY = mob.DestinationY,
            Name = mob.Name,
            Trailer = trailer,
        }.EncodePayload();
    }
}
