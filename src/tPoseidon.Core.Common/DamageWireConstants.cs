namespace tPoseidon.Core.Common;

public static class DamageHitFlags
{
    public const byte Normal = 0x00;
    public const byte Miss = 0x01;
    public const byte MobHit = 0x03;
    public const byte Kill = 0x80;
    public const byte CritKill = 0x83;
    public const byte Crit = 0x80;
}

public static class DamageHitKind
{
    public const uint PlayerToMob = 0x00000001;
    public const uint MobToPlayer = 0x00000003;
}
