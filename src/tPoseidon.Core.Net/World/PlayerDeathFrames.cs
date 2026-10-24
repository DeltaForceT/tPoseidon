using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public static class PlayerDeathFrames
{
    public const int ExpValueSize = 4;

    public static IReadOnlyList<(Opcode Opcode, byte[] Payload)> ForVictim(
        ushort playerId,
        ushort x,
        ushort y,
        byte moveType,
        bool wasAttacking,
        ulong exp)
    {
        var frames = new List<(Opcode Opcode, byte[] Payload)> { (Opcode.S2C_EXP_VALUE, ExpValue(exp)) };
        frames.AddRange(ForOthers(playerId, x, y, moveType, wasAttacking));
        return frames;
    }

    public static IReadOnlyList<(Opcode Opcode, byte[] Payload)> ForOthers(
        ushort playerId,
        ushort x,
        ushort y,
        byte moveType,
        bool wasAttacking)
    {
        var frames = new List<(Opcode Opcode, byte[] Payload)>
        {
            (Opcode.S2C_OP_0019_TBD, new Packet0019Tbd
            {
                EntityId = playerId,
                WorldX = x,
                WorldY = y,
                StateFlag = moveType,
            }.EncodePayload()),
            (Opcode.S2C_OP_002B_TBD, Death(playerId)),
        };
        if (wasAttacking)
        {
            frames.Add((Opcode.S2C_OP_0020_TBD, LeaveAttack(playerId)));
        }
        return frames;
    }

    public static byte[] ExpValue(ulong exp)
    {
        var w = new PacketWriter(ExpValueSize);
        w.WriteUInt32Field(exp);
        return w.ToArray();
    }

    public static uint ReadExpValue(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != ExpValueSize)
        {
            throw new InvalidPacketException($"Exp value payload must be exactly {ExpValueSize} bytes, got {payload.Length}.");
        }
        return new PacketReader(payload).ReadUInt32();
    }

    private static byte[] Death(ushort playerId)
    {
        var w = new PacketWriter(MeleeSwingFrames.DeathSize);
        w.WriteUInt16(playerId);
        w.WriteByte(MeleeSwingFrames.DeathTypeNormal);
        return w.ToArray();
    }

    private static byte[] LeaveAttack(ushort playerId)
    {
        var w = new PacketWriter(MeleeSwingFrames.LeaveAttackSize);
        w.WriteUInt16(playerId);
        return w.ToArray();
    }
}
