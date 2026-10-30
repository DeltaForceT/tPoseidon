namespace tPoseidon.Core.Game.Combat;

public readonly record struct PlayerStrike(
    ushort AttackerEntityId,
    ushort TargetEntityId,
    ushort AttackerX,
    ushort AttackerY,
    ushort TargetX,
    ushort TargetY,
    long Damage,
    long MobHpAfter,
    byte HitFlags,
    uint HitKind,
    bool Killed,
    DateTimeOffset NextSwingAtUtc,
    int MonsterParamId,
    long MobMaxHp,
    long MobHpBefore = 0,
    int ElementMask = 0,
    ushort MobBars = 0,
    bool MobBoss = false);
