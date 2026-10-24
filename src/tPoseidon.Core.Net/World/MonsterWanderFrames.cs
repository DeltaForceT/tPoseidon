using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public static class MonsterWanderFrames
{
    public static (Opcode Opcode, byte[] Payload) Encode(MonsterWanderEvent wanderEvent)
    {
        ArgumentNullException.ThrowIfNull(wanderEvent);

        switch (wanderEvent)
        {
            case MonsterMoveStarted started:
                return (Opcode.ENTITY_MOVE, new EntityMovementPacket
                {
                    EntityId = started.EntityId,
                    FromX = started.FromX,
                    FromY = started.FromY,
                    ToX = started.ToX,
                    ToY = started.ToY,
                    Flag = MonsterWanderCycle.WalkMoveType,
                }.EncodePayload());

            case MonsterMoveRefused refused:
                return (Opcode.S2C_OP_0019_TBD, new Packet0019Tbd
                {
                    EntityId = refused.EntityId,
                    WorldX = refused.X,
                    WorldY = refused.Y,
                    StateFlag = MonsterWanderCycle.WalkMoveType,
                }.EncodePayload());

            case MonsterSpeedChanged changed:
                return (Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(changed.EntityId, changed.Speed));

            case MonsterArrived arrived:
                return (Opcode.ENTITY_EMOTE, new EntityTickPingPacket
                {
                    EntityId = arrived.EntityId,
                }.EncodePayload());

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(wanderEvent), wanderEvent.GetType().Name, "Unknown monster wander event.");
        }
    }
}
