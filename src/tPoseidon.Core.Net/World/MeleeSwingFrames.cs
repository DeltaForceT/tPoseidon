using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Net.World;

public enum SwingAudience
{
    Everyone,
    Attacker,
    Owner,
    Engaged,
}

public readonly record struct SwingFrame(Opcode Opcode, byte[] Payload, ushort BlockId, SwingAudience Audience);

public sealed record SwingAdditions(
    byte[]? OwnerTaken,
    IReadOnlyList<(Opcode Opcode, byte[] Payload)> OwnerRewards,
    IReadOnlyList<(Opcode Opcode, byte[] Payload, SwingAudience Audience)> AttackerRewards,
    IReadOnlyList<(Opcode Opcode, byte[] Payload)>? Drops = null)
{
    public static readonly SwingAdditions None = new(
        null,
        Array.Empty<(Opcode Opcode, byte[] Payload)>(),
        Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>());
}

public readonly record struct SwingStart(ushort AttackerEntityId, ushort X, ushort Y, byte Motion);

public readonly record struct AttackLanded(
    ushort AttackerEntityId,
    ushort Kind,
    ushort TargetEntityId,
    ushort AttackerX,
    ushort AttackerY,
    ushort TargetX,
    ushort TargetY,
    short Damage,
    byte Result);

public readonly record struct StrikeResult(
    ushort VictimEntityId,
    int Damage,
    int DeathType,
    int Result,
    int ElementMask,
    int Flags,
    int NumberArgument,
    int Guard);

public readonly record struct MonsterHealth(ushort MonsterEntityId, int Hp, ushort GaugeIndex, byte MultiGauge);

public readonly record struct EntityDeath(ushort EntityId, byte DeathType);

public static class MeleeSwingFrames
{
    public const ushort NoBlock = 0;
    public const ushort HighestBlockId = 0x7FFF;
    public const byte SwingMotion = 2;
    public const ushort MeleeAttackKind = 1;
    public const ushort GunAttackKind = 2;
    public const byte ResultHit = 0;
    public const byte ResultFinishingFlag = 0x80;
    public const byte DeathTypeNormal = 0;
    public const int PlayerHitPresentation = 1;

    public const int SwingSize = 7;
    public const int AttackSize = 17;
    public const int HitResultSize = 30;
    public const int MonsterHpSize = 9;
    public const int DeathSize = 3;
    public const int LeaveAttackSize = 2;

    public static ushort NextBlockId(ushort current) =>
        current >= HighestBlockId ? (ushort)1 : (ushort)(current + 1);

    public static IReadOnlyList<SwingFrame> Encode(
        PlayerStrike strike,
        ushort blockId,
        SwingAdditions? additions = null) => Chain(strike, blockId, additions, Opcode.S2C_OP_001F_TBD, MeleeAttackKind);

    public static IReadOnlyList<SwingFrame> EncodeOnPlayer(
        PlayerStrike strike,
        ushort blockId,
        IReadOnlyList<SwingFrame> beforeHit,
        IReadOnlyList<SwingFrame> afterHit)
    {
        ArgumentNullException.ThrowIfNull(beforeHit);
        ArgumentNullException.ThrowIfNull(afterHit);
        if (blockId == NoBlock)
        {
            throw new ArgumentOutOfRangeException(nameof(blockId), "A swing needs a non-zero block id.");
        }
        var result = (byte)strike.HitKind;
        var frames = new List<SwingFrame>
        {
            new(Opcode.S2C_OP_001B_TBD, Swing(strike), NoBlock, SwingAudience.Everyone),
            new(Opcode.S2C_OP_001F_TBD, Attack(strike, result, MeleeAttackKind), blockId, SwingAudience.Everyone),
        };
        frames.AddRange(beforeHit);
        frames.Add(new(Opcode.S2C_OP_002A_TBD, HitResult(strike, result), blockId, SwingAudience.Everyone));
        frames.AddRange(afterHit);
        frames.Add(new(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), blockId, SwingAudience.Everyone));
        if (strike.Killed)
        {
            frames.Add(new(Opcode.S2C_OP_0020_TBD, LeaveAttack(strike), NoBlock, SwingAudience.Everyone));
        }
        return frames;
    }

    public static IReadOnlyList<SwingFrame> EncodeGunShot(
        PlayerStrike strike,
        ushort blockId,
        SwingAdditions? additions = null) => Chain(strike, blockId, additions, Opcode.S2C_GUN_SHOT, GunAttackKind);

    private static List<SwingFrame> Chain(
        PlayerStrike strike,
        ushort blockId,
        SwingAdditions? additions,
        Opcode attackOpcode,
        ushort attackKind)
    {
        if (blockId == NoBlock)
        {
            throw new ArgumentOutOfRangeException(nameof(blockId), "A swing needs a non-zero block id.");
        }

        var extra = additions ?? SwingAdditions.None;
        var result = (byte)strike.HitKind;
        var frames = new List<SwingFrame>
        {
            new(Opcode.S2C_OP_001B_TBD, Swing(strike), NoBlock, SwingAudience.Everyone),
            new(attackOpcode, Attack(strike, result, attackKind), blockId, SwingAudience.Everyone),
        };
        if (extra.OwnerTaken is { } ownerTaken)
        {
            frames.Add(new(Opcode.S2C_OP_016D_TBD, ownerTaken, blockId, SwingAudience.Everyone));
        }
        frames.Add(new(Opcode.S2C_OP_002A_TBD, HitResult(strike, result), blockId, SwingAudience.Everyone));
        if (strike.Killed)
        {
            frames.Add(new(Opcode.S2C_OP_002B_TBD, Death(strike), blockId, SwingAudience.Everyone));
            foreach (var (opcode, payload) in extra.OwnerRewards)
            {
                frames.Add(new(opcode, payload, blockId, SwingAudience.Owner));
            }
            if (extra.Drops is { } drops)
            {
                foreach (var (opcode, payload) in drops)
                {
                    frames.Add(new(opcode, payload, blockId, SwingAudience.Everyone));
                }
            }
        }
        frames.Add(new(Opcode.S2C_OP_011E_TBD, MonsterHp(strike), blockId, SwingAudience.Engaged));
        foreach (var (opcode, payload, audience) in extra.AttackerRewards)
        {
            frames.Add(new(opcode, payload, blockId, audience));
        }
        frames.Add(new(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), blockId, SwingAudience.Everyone));
        if (strike.Killed)
        {
            frames.Add(new(Opcode.S2C_OP_0020_TBD, LeaveAttack(strike), NoBlock, SwingAudience.Everyone));
        }
        return frames;
    }

    public static SwingStart ReadSwing(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, SwingSize, "swing"));
        return new SwingStart(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadByte());
    }

    public static AttackLanded ReadAttack(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, AttackSize, "attack"));
        return new AttackLanded(
            r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadInt16(), r.ReadByte());
    }

    public static StrikeResult ReadHitResult(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, HitResultSize, "hit result"));
        return new StrikeResult(r.ReadUInt16(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
    }

    public static MonsterHealth ReadMonsterHp(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, MonsterHpSize, "monster HP"));
        return new MonsterHealth(r.ReadUInt16(), r.ReadInt32(), r.ReadUInt16(), r.ReadByte());
    }

    public static EntityDeath ReadDeath(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(Sized(payload, DeathSize, "death"));
        return new EntityDeath(r.ReadUInt16(), r.ReadByte());
    }

    public static ushort ReadLeaveAttack(ReadOnlySpan<byte> payload) =>
        new PacketReader(Sized(payload, LeaveAttackSize, "leave attack")).ReadUInt16();

    private static ReadOnlySpan<byte> Sized(ReadOnlySpan<byte> payload, int size, string what) =>
        payload.Length == size
            ? payload
            : throw new InvalidPacketException($"The {what} payload must be exactly {size} bytes, got {payload.Length}.");

    private static byte[] Swing(PlayerStrike strike)
    {
        var w = new PacketWriter(SwingSize);
        w.WriteUInt16(strike.AttackerEntityId);
        w.WriteUInt16(strike.AttackerX);
        w.WriteUInt16(strike.AttackerY);
        w.WriteByte(SwingMotion);
        return w.ToArray();
    }

    private static byte[] Attack(PlayerStrike strike, byte result, ushort attackKind)
    {
        var w = new PacketWriter(AttackSize);
        w.WriteUInt16(strike.AttackerEntityId);
        w.WriteUInt16(attackKind);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteUInt16(strike.AttackerX);
        w.WriteUInt16(strike.AttackerY);
        w.WriteUInt16(strike.TargetX);
        w.WriteUInt16(strike.TargetY);
        w.WriteInt16((short)Math.Clamp(strike.Damage, 0, short.MaxValue));
        w.WriteByte(result);
        return w.ToArray();
    }

    private static byte[] HitResult(PlayerStrike strike, byte result)
    {
        var w = new PacketWriter(HitResultSize);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteInt32(strike.Damage);
        w.WriteInt32(DeathTypeNormal);
        w.WriteInt32(result);
        w.WriteInt32(0);
        w.WriteInt32(0);
        w.WriteInt32(PlayerHitPresentation);
        w.WriteInt32(0);
        return w.ToArray();
    }

    private static byte[] MonsterHp(PlayerStrike strike)
    {
        var w = new PacketWriter(MonsterHpSize);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteInt32(Math.Max(0, strike.MobHpAfter));
        w.WriteUInt16(0);
        w.WriteByte(0);
        return w.ToArray();
    }

    private static byte[] Death(PlayerStrike strike)
    {
        var w = new PacketWriter(DeathSize);
        w.WriteUInt16(strike.TargetEntityId);
        w.WriteByte(DeathTypeNormal);
        return w.ToArray();
    }

    private static byte[] LeaveAttack(PlayerStrike strike)
    {
        var w = new PacketWriter(LeaveAttackSize);
        w.WriteUInt16(strike.AttackerEntityId);
        return w.ToArray();
    }
}
