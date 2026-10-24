using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public static class MonsterBattleFrames
{
    public static IReadOnlyList<(Opcode Opcode, byte[] Payload, ushort BlockId)> Encode(
        MonsterBattleEvent battleEvent,
        ushort swingBlockId,
        IReadOnlyList<(Opcode Opcode, byte[] Payload)>? insideSwingBlock = null)
    {
        ArgumentNullException.ThrowIfNull(battleEvent);

        switch (battleEvent)
        {
            case MonsterBattleHalted halted:
                return new[]
                {
                    (Opcode.S2C_OP_0019_TBD, new Packet0019Tbd
                    {
                        EntityId = halted.EntityId,
                        WorldX = halted.X,
                        WorldY = halted.Y,
                        StateFlag = halted.MoveType,
                    }.EncodePayload(), MeleeSwingFrames.NoBlock),
                };

            case MonsterBattleSpeedChanged changed:
                return new[]
                {
                    (Opcode.S2C_ENTITY_SPEED, ProgressFrames.Speed(changed.EntityId, changed.Speed), MeleeSwingFrames.NoBlock),
                };

            case MonsterBattleMoved moved:
                return new[]
                {
                    (Opcode.ENTITY_MOVE, new EntityMovementPacket
                    {
                        EntityId = moved.EntityId,
                        FromX = moved.FromX,
                        FromY = moved.FromY,
                        ToX = moved.ToX,
                        ToY = moved.ToY,
                        Flag = moved.MoveType,
                    }.EncodePayload(), MeleeSwingFrames.NoBlock),
                };

            case MonsterBattleSwung swung:
                if (swingBlockId == MeleeSwingFrames.NoBlock)
                {
                    throw new ArgumentOutOfRangeException(nameof(swingBlockId), "A swing needs a non-zero block id.");
                }
                var frames = new List<(Opcode Opcode, byte[] Payload, ushort BlockId)>
                {
                    (Opcode.S2C_OP_001B_TBD, Swing(swung), MeleeSwingFrames.NoBlock),
                    (Opcode.S2C_OP_001F_TBD, Attack(swung), swingBlockId),
                    (Opcode.S2C_OP_002A_TBD, HitResult(swung), swingBlockId),
                };
                if (insideSwingBlock is not null)
                {
                    foreach (var (opcode, payload) in insideSwingBlock)
                    {
                        frames.Add((opcode, payload, swingBlockId));
                    }
                }
                frames.Add((Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), swingBlockId));
                return frames;

            case MonsterBattleCast:
                return Array.Empty<(Opcode Opcode, byte[] Payload, ushort BlockId)>();

            case MonsterBattleLeft left:
                return new[]
                {
                    (Opcode.S2C_OP_0020_TBD, LeaveAttack(left), MeleeSwingFrames.NoBlock),
                };

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(battleEvent), battleEvent.GetType().Name, "Unknown monster battle event.");
        }
    }

    private static byte[] Swing(MonsterBattleSwung swung)
    {
        var w = new PacketWriter(MeleeSwingFrames.SwingSize);
        w.WriteUInt16(swung.EntityId);
        w.WriteUInt16(swung.X);
        w.WriteUInt16(swung.Y);
        w.WriteByte(MeleeSwingFrames.SwingMotion);
        return w.ToArray();
    }

    private static byte[] Attack(MonsterBattleSwung swung)
    {
        var w = new PacketWriter(MeleeSwingFrames.AttackSize);
        w.WriteUInt16(swung.EntityId);
        w.WriteUInt16(MeleeSwingFrames.MeleeAttackKind);
        w.WriteUInt16(swung.TargetEntityId);
        w.WriteUInt16(swung.X);
        w.WriteUInt16(swung.Y);
        w.WriteUInt16(swung.TargetX);
        w.WriteUInt16(swung.TargetY);
        w.WriteInt16Field(swung.Damage);
        w.WriteByte(swung.Result);
        return w.ToArray();
    }

    private static byte[] HitResult(MonsterBattleSwung swung)
    {
        var w = new PacketWriter(MeleeSwingFrames.HitResultSize);
        w.WriteUInt16(swung.TargetEntityId);
        w.WriteInt32Field(swung.Damage);
        w.WriteInt32(MeleeSwingFrames.DeathTypeNormal);
        w.WriteInt32(swung.Result);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteUInt16(swung.GuardLeft);
        w.WriteUInt16(0);
        return w.ToArray();
    }

    private static byte[] LeaveAttack(MonsterBattleLeft left)
    {
        var w = new PacketWriter(MeleeSwingFrames.LeaveAttackSize);
        w.WriteUInt16(left.EntityId);
        return w.ToArray();
    }
}
