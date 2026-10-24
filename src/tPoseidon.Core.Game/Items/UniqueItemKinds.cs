namespace tPoseidon.Core.Game.Items;

public static class UniqueItemKinds
{
    public const int UniqueOption = 0x1;
    public const int AbsoluteOption = 0x100000;
    public const int NormalOption = 0x200000;
    public const int EventOption = 0x8000000;
    public const byte None = 0;
    public const byte Absolute = 1;
    public const byte Normal = 2;
    public const byte Event = 3;

    public static byte KindOf(int options) =>
        (options & AbsoluteOption) != 0 ? Absolute
        : (options & NormalOption) != 0 ? Normal
        : (options & EventOption) != 0 ? Event
        : None;

    public const int BossMonsterType = 3;
    public const int NonresidentBossMonsterType = 7;

    public static bool KilledAsBoss(int monsterType) => monsterType is BossMonsterType or NonresidentBossMonsterType;

    public static bool WithdrawnOnRelease(int options) =>
        (options & UniqueOption) != 0 && (options & AbsoluteOption) != 0;
}
