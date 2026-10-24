namespace tPoseidon.Core.Data;

public enum GuardianSkillType : byte
{
    Card = 0,
    Special = 1,
    Order = 2,
}

public sealed record GuardianSkill(int SkillId, GuardianSkillType Type, byte FType, short Level, byte Slot);

public sealed record GuardianSummary(ulong GuardianUid, short GuardianType, byte SlotNum, bool Equipped, string Name, byte RareType);

public sealed record GuardianState(
    ulong GuardianUid,
    byte SlotNum,
    short GuardianType,
    string Name,
    short Level,
    byte FType,
    byte RareType,
    byte TitleIndex,
    byte PtsP,
    byte PtsM,
    byte PtsS,
    byte PtsC,
    IReadOnlyList<short> StatPoints,
    int Exp,
    int Gauge,
    int SkillPoint,
    IReadOnlyList<GuardianSkill> Skills);

public sealed record GuardianLogin(bool Awakened, short SlotMaxCount, IReadOnlyList<GuardianSummary> Guardians, GuardianState? Equipped);

public sealed record GuardianAwakening(
    ulong CharacterId,
    string Name,
    short GuardianType,
    byte FType,
    byte RareType,
    byte TitleIndex,
    byte PtsP,
    byte PtsM,
    byte PtsS,
    byte PtsC,
    int SummonSkillId);

public enum GuardianAwakenError
{
    None = 0,
    NoAwakening = 101,
    SlotsFull = 103,
}

public sealed record GuardianAwakenResult(GuardianAwakenError Error, ulong GuardianUid, byte SlotNum)
{
    public bool Succeeded => Error == GuardianAwakenError.None;
}

public static class GuardianStateRules
{
    public const int StatCount = 12;
    public const int NameBytes = 30;
    public const int OrderSlotCount = 6;
}

public interface IGuardianStateStore
{
    GuardianAwakenResult Awaken(GuardianAwakening awakening);

    void ResetAwakening(ulong characterId, int summonSkillId);

    GuardianLogin LoadLogin(ulong characterId);

    GuardianState? Load(ulong characterId, ulong guardianUid);

    bool Equip(ulong characterId, ulong guardianUid);

    bool SaveExp(ulong guardianUid, int exp, int gauge);

    bool SaveLevelUp(ulong guardianUid, short level, IReadOnlyList<short> statPoints, int skillPoint);

    bool SaveCards(ulong guardianUid, int skillPoint, int updateCount, IReadOnlyList<GuardianSkill> cards);

    bool SetOrder(ulong guardianUid, int skillId, byte slot, bool remove);

    bool SetSpecial(ulong guardianUid, int skillId, byte fType, byte slot);

    bool Rename(ulong guardianUid, string name);
}
